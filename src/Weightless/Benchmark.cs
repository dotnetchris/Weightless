using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace Weightless;

internal static class Benchmark
{
    private const int Seed = 20261123;
    private const int Warmup = 3000;
    private const int Batches = 9;
    private const int Iterations = 2000;
    private static readonly string[] Stages = ["Encode", "Decode + validate", "Execute", "Decode + execute"];

    internal static BenchmarkResult Run(Fixture[] fixtures, string directory)
    {
        var metadata = Metadata();
        var random = new Random(Seed);
        var jobs = fixtures.SelectMany(fixture => CreateJobs(directory, fixture, random)).ToArray();
        foreach (var job in jobs)
            _ = Measure(job, Warmup);

        for (var batch = 0; batch < Batches; batch++)
        {
            random.Shuffle(jobs.AsSpan());
            foreach (var job in jobs)
                job.Samples.Add(Measure(job, Iterations));
        }

        return new BenchmarkResult(metadata, jobs.Select(job =>
        {
            var sorted = job.Samples.Select(sample => sample.NanosecondsPerOperation).Order().ToArray();
            return new TimingRow(job.Fixture, job.Codec, job.Stage, sorted[Batches / 2], sorted[0], sorted[^1],
                job.Samples.ToArray());
        }).OrderBy(row => Array.FindIndex(fixtures, fixture => fixture.Name == row.Fixture))
          .ThenBy(row => row.Codec).ThenBy(row => Array.IndexOf(Stages, row.Stage)).ToArray());
    }

    private static IEnumerable<Job> CreateJobs(string directory, Fixture fixture, Random random)
    {
        var order = Enumerable.Range(0, fixture.Cases.Length).ToArray();
        random.Shuffle(order.AsSpan());
        var inputs = order.Select(index => fixture.Cases[index]).ToArray();
        foreach (var encoding in ExperimentSuite.Encodings)
        {
            var bytes = File.ReadAllBytes(ExperimentSuite.EncodingPath(directory, fixture.Name, encoding));
            var graph = GraphCodec.Decode(bytes, encoding);
            var codec = encoding.ToString();
            yield return new Job(fixture.Name, codec, Stages[0], inputs,
                _ => GraphCodec.Encode(fixture.Graph, encoding).Length);
            yield return new Job(fixture.Name, codec, Stages[1], inputs,
                _ => GraphCodec.Decode(bytes, encoding).Functions[0].Body.Length);
            yield return new Job(fixture.Name, codec, Stages[2], inputs,
                args => Consume(Interpreter.Observe(graph, args)));
            yield return new Job(fixture.Name, codec, Stages[3], inputs,
                args => Consume(Interpreter.Observe(GraphCodec.Decode(bytes, encoding), args)));
        }
    }

    private static Sample Measure(Job job, int iterations)
    {
        var inputs = new object[iterations][];
        for (var index = 0; index < inputs.Length; index++)
            inputs[index] = job.Inputs[index % job.Inputs.Length].FreshArguments();

        // Capture the inputs before starting the clock and consume every result inside the measured loop.
        var checksum = 17;
        var started = Stopwatch.GetTimestamp();
        foreach (var input in inputs)
            checksum = unchecked(checksum * 31 + job.Operation(input));
        var ticks = Stopwatch.GetTimestamp() - started;
        return new Sample(ticks, ticks * (1e9 / Stopwatch.Frequency) / iterations, checksum);
    }

    private static int Consume(Observation observation) =>
        unchecked((observation.Value is int number ? number : 0) + 31 * observation.Effects.Length +
                  17 * observation.Output.Length + (observation.ExceptionType?.Length ?? 0) +
                  (observation.Buffers.FirstOrDefault() is { Length: > 0 } buffer ? buffer[0] : 0));

    private static BenchmarkMetadata Metadata()
    {
        var build = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
        var cpu = OperatingSystem.IsWindows()
            ? Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "ProcessorNameString", null) as string
            : ReadLinuxCpu();
        return new BenchmarkMetadata(Command("git", "rev-parse", "HEAD"),
            Command("git", "status", "--porcelain") is { } status ? status.Length != 0 : null,
            SourceHash(), Command("dotnet", "--version"), RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription, cpu?.Trim() ?? RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount, RuntimeInformation.ProcessArchitecture.ToString(), GCSettings.IsServerGC,
            Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            Environment.GetEnvironmentVariable("DOTNET_TieredPGO"),
            build, Environment.ProcessId, Seed, Warmup, Batches, Iterations, Stopwatch.Frequency);
    }

    private static string? ReadLinuxCpu()
    {
        const string cpuInfo = "/proc/cpuinfo";
        return File.Exists(cpuInfo)
            ? File.ReadLines(cpuInfo).FirstOrDefault(line => line.StartsWith("model name", StringComparison.Ordinal))
                ?.Split(':', 2).Last()
            : null;
    }

    private static string? Command(string command, params string[] args)
    {
        try
        {
            var start = new ProcessStartInfo(command)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in args)
                start.ArgumentList.Add(arg);

            using var process = Process.Start(start)!;
            var stderr = process.StandardError.ReadToEndAsync();
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            _ = stderr.GetAwaiter().GetResult();
            return process.ExitCode == 0 ? stdout.Trim() : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private static string SourceHash()
    {
        var paths = Directory.GetFiles("src/Weightless", "*.cs")
            .Append("src/Weightless/Weightless.csproj")
            .Concat(Directory.GetFiles("experiments", "*.cs", SearchOption.TopDirectoryOnly))
            .Concat(Directory.GetDirectories("experiments")
                .SelectMany(directory => Directory.GetFiles(directory, "*.cs")))
            .Append("experiments/0001-Addition/checked-transfer-v1.json")
            .Select(path => path.Replace('\\', '/')).Order(StringComparer.Ordinal);
        var manifest = string.Join("\n", paths.Select(path =>
            $"{path} {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}"));
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(manifest)));
    }

    internal static string Report(Verification verification, BenchmarkResult benchmark)
    {
        var report = new System.Text.StringBuilder("# Thanksgiving corpus results\n\n");
        report.AppendLine("Run from the repository root with the .NET 10 SDK.").AppendLine();
        report.AppendLine("```powershell");
        report.AppendLine("dotnet run -c Release --project src/Weightless -- suite artifacts/thanksgiving");
        report.AppendLine("```").AppendLine();

        var host = benchmark.Metadata;
        report.AppendLine($"Measured in {host.Build} with SDK {host.Sdk}, {host.Runtime}, and {host.Cpu} " +
            $"({host.LogicalProcessors} logical processors). The source fingerprint and host settings are in `timings.json`.");
        report.AppendLine();
        report.AppendLine($"A fresh process verified {verification.Rows.Length} fixture/codec pairs and " +
            $"{verification.Rows.Sum(row => row.Observations.Length)} C# comparisons. The decoder and its re-encoding ran " +
            "in that process using files written by the suite. See `verification.json` for the input vectors, output, " +
            "effects, postcondition discriminators, and malformed wire cases.").AppendLine();

        report.AppendLine("| Fixture | Nodes | Baseline bytes | Packed bytes | Baseline bytes/node | Packed bytes/node | Graph, bytes, C# |");
        report.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (var group in verification.Rows.GroupBy(row => row.Fixture))
        {
            var plain = group.Single(row => row.Codec == "Baseline");
            var packed = group.Single(row => row.Codec == "Packed");
            report.AppendLine(FormattableString.Invariant(
                $"| {group.Key} | {plain.Nodes} | {plain.Bytes} | {packed.Bytes} | {(double)plain.Bytes / plain.Nodes:F2} | {(double)packed.Bytes / packed.Nodes:F2} | {(plain.Passed && packed.Passed ? "Pass" : "Fail")} |"));
        }

        var baselinePlain = verification.Rows.Single(row => row.Fixture == "add-checked" && row.Codec == "Baseline");
        var packedRequired = verification.Rows.Single(row => row.Fixture == "add-constrained" && row.Codec == "Packed");
        var fits = packedRequired.Bytes <= baselinePlain.Bytes ? "fits within" : "exceeds";
        report.AppendLine().AppendLine($"Packed/constrained Add ({packedRequired.Bytes} bytes) {fits} baseline/plain Add " +
            $"({baselinePlain.Bytes} bytes). Each codec uses `00` for plain and `01 01` for the required " +
            "nonnegative result. The same input `(-1, 0)` returns `-1` in the plain graph and produces " +
            "`PostconditionViolationException` in the constrained graph.").AppendLine();

        report.AppendLine($"Times are median nanoseconds per operation with [min, max] across {host.Batches} batches " +
            $"of {host.IterationsPerBatch} operations after {host.WarmupPerRow} warm-up operations per row and stage. " +
            "Execution captures values or exceptions, output, ordered effects, and final memory in both codecs. " +
            "The harness prepares inputs outside each timed batch.").AppendLine();
        report.AppendLine("| Fixture | Codec | Encode | Decode + validate | Execute | Decode + execute |");
        report.AppendLine("| --- | --- | ---: | ---: | ---: | ---: |");
        foreach (var group in benchmark.Rows.GroupBy(row => (row.Fixture, row.Codec)))
        {
            var values = Stages.Select(stage =>
            {
                var timing = group.Single(row => row.Stage == stage);
                return string.Create(CultureInfo.InvariantCulture,
                    $"{timing.MedianNs:F0} [{timing.MinNs:F0}, {timing.MaxNs:F0}]");
            });
            report.AppendLine($"| {group.Key.Fixture} | {group.Key.Codec} | {string.Join(" | ", values)} |");
        }
        return report.ToString();
    }

    private sealed record Job(string Fixture, string Codec, string Stage, InputCase[] Inputs, Func<object[], int> Operation)
    {
        internal List<Sample> Samples { get; } = [];
    }
}

internal sealed record BenchmarkMetadata(string? BaseRevision, bool? DirtyWorktree, string SourceSha256,
    string? Sdk, string Runtime, string Os, string Cpu, int LogicalProcessors, string Architecture, bool ServerGc,
    string? TieredCompilationOverride, string? TieredPgoOverride, string? Build, int EncoderProcessId,
    int Seed, int WarmupPerRow, int Batches, int IterationsPerBatch, long StopwatchHz);
internal sealed record Sample(long ElapsedTicks, double NanosecondsPerOperation, int Checksum);
internal sealed record TimingRow(string Fixture, string Codec, string Stage, double MedianNs, double MinNs, double MaxNs,
    Sample[] Samples);
internal sealed record BenchmarkResult(BenchmarkMetadata Metadata, TimingRow[] Rows);
