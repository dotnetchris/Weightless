using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Weightless;

internal static class ExperimentSuite
{
    internal static readonly EncodingKind[] Encodings = [EncodingKind.Baseline, EncodingKind.Packed];

    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var corpus = CSharpSeeds.Load();
        SaveJson(Path.Combine(directory, "provenance.json"), corpus.Provenance);
        Directory.CreateDirectory(Path.Combine(directory, "encodings"));
        foreach (var fixture in corpus.Fixtures)
        foreach (var encoding in Encodings)
            File.WriteAllBytes(EncodingPath(directory, fixture.Name, encoding), GraphCodec.Encode(fixture.Graph, encoding));

        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        if (string.Equals(Path.GetFileNameWithoutExtension(start.FileName), "dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("verify-corpus");
        start.ArgumentList.Add(directory);
        using var verifier = Process.Start(start)!;
        verifier.WaitForExit();
        if (verifier.ExitCode != 0)
            return verifier.ExitCode;

        var verification = JsonSerializer.Deserialize<Verification>(
            File.ReadAllBytes(Path.Combine(directory, "verification.json")), Program.Json)!;
        var timings = Benchmark.Run(corpus.Fixtures, directory);
        SaveJson(Path.Combine(directory, "timings.json"), timings);
        File.WriteAllText(Path.Combine(directory, "report.md"), Benchmark.Report(verification, timings));
        Console.WriteLine($"Report and raw samples saved to {directory}");
        return 0;
    }

    internal static int Verify(string directory)
    {
        var corpus = CSharpSeeds.Load();
        var results = new List<VerificationRow>();
        foreach (var fixture in corpus.Fixtures)
        {
            var cases = fixture.Cases.Select(input => fixture.Oracle(input.FreshArguments())).ToArray();
            foreach (var encoding in Encodings)
                results.Add(VerifyFixture(directory, fixture, encoding, cases));
        }

        var contracts = Encodings.Select(encoding => VerifyContract(directory, encoding)).ToArray();
        var passed = results.All(row => row.Passed) && contracts.All(probe => probe.Passed) &&
            SizesMatchInference(corpus.Fixtures, results);
        SaveJson(Path.Combine(directory, "verification.json"),
            new Verification(Environment.ProcessId, passed, results.ToArray(), contracts));
        Console.WriteLine($"{(passed ? "Verified" : "Verification failed for")} {results.Count} fixture/codec rows " +
            $"and {contracts.Length} contract probes in process {Environment.ProcessId}.");
        return passed ? 0 : 1;
    }

    private static bool SizesMatchInference(Fixture[] fixtures, List<VerificationRow> results) => fixtures.All(fixture =>
    {
        var additions = fixture.Graph.Functions.Sum(function => function.Body.Count(node => node is Addition));
        return results.Single(row => row.Fixture == fixture.Name && row.Codec == "Baseline").Bytes -
            results.Single(row => row.Fixture == fixture.Name && row.Codec == "Packed").Bytes == additions;
    });

    private static VerificationRow VerifyFixture(string directory, Fixture fixture, EncodingKind encoding,
        Observation[] expected)
    {
        var byteCount = 0;
        try
        {
            var bytes = File.ReadAllBytes(EncodingPath(directory, fixture.Name, encoding));
            byteCount = bytes.Length;
            var decoded = GraphCodec.Decode(bytes, encoding);
            var comparisons = fixture.Cases.Select((input, index) =>
            {
                var actual = Interpreter.Observe(decoded, input.FreshArguments());
                return new Comparison(input.Name, input.Arguments, expected[index], actual, actual.Matches(expected[index]));
            }).ToArray();
            return new VerificationRow(fixture.Name, encoding.ToString(), bytes.Length, fixture.Graph.NodeCount,
                decoded.Matches(fixture.Graph), bytes.AsSpan().SequenceEqual(GraphCodec.Encode(decoded, encoding)),
                comparisons, null);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException)
        {
            return new VerificationRow(fixture.Name, encoding.ToString(), byteCount, fixture.Graph.NodeCount,
                false, false, [], exception.Message);
        }
    }

    private static ContractProbe VerifyContract(string directory, EncodingKind encoding)
    {
        try
        {
            return VerifyContract(
                File.ReadAllBytes(EncodingPath(directory, "add-checked", encoding)),
                File.ReadAllBytes(EncodingPath(directory, "add-constrained", encoding)),
                encoding);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return new ContractProbe(encoding.ToString(), null, null, null, null, false, [], false, exception.Message);
        }
    }

    private static ContractProbe VerifyContract(byte[] plain, byte[] constrained, EncodingKind encoding)
    {
        const int discriminatorOffset = 4;
        var plainGraph = GraphCodec.Decode(plain, encoding);
        var requiredGraph = GraphCodec.Decode(constrained, encoding);
        if (!plainGraph.Matches(Fixtures.Add(Overflow.Checked)) ||
            !requiredGraph.Matches(Fixtures.Add(Overflow.Checked, Postcondition.NonNegativeInt32)))
            throw new InvalidDataException("Contract probe requires the canonical plain and constrained Add graphs.");

        var invalidDiscriminator = constrained.ToArray();
        invalidDiscriminator[discriminatorOffset] = 2;
        var invalidPredicate = constrained.ToArray();
        invalidPredicate[discriminatorOffset + 1] = 255;
        var invalidOperandType = plain.ToArray();
        invalidOperandType[8] = (byte)ValueKind.Boolean;
        byte[] overlongFunctionCount = [.. plain.AsSpan(0, 5), 0x81, 0x00, .. plain.AsSpan(6)];
        var alternative = encoding == EncodingKind.Baseline ? EncodingKind.Packed : EncodingKind.Baseline;
        Rejection[] rejections =
        [
            Reject("unknown discriminator", invalidDiscriminator, encoding),
            Reject("unknown predicate", invalidPredicate, encoding),
            Reject("truncated predicate", constrained[..(discriminatorOffset + 1)], encoding),
            Reject("truncated instruction", constrained[..^1], encoding),
            Reject("trailing byte", [.. constrained, 0], encoding),
            Reject("noncanonical function count", overlongFunctionCount, encoding),
            Reject("wrong operand type", invalidOperandType, encoding),
            Reject("wrong codec header", plain, alternative)
        ];
        byte[] plainField = plain.AsSpan(discriminatorOffset, 1).ToArray();
        byte[] requiredField = constrained.AsSpan(discriminatorOffset, 2).ToArray();
        var sameBody = plainGraph.Matches(requiredGraph with { Contract = Postcondition.None });
        var passed = sameBody && plainField.SequenceEqual(new byte[] { 0 }) &&
            requiredField.SequenceEqual(new byte[] { 1, 1 }) &&
            plainGraph.Contract == Postcondition.None && requiredGraph.Contract == Postcondition.NonNegativeInt32 &&
            rejections.All(rejection => rejection.Rejected);
        return new ContractProbe(encoding.ToString(), Convert.ToHexString(plainField), plainGraph.Contract.ToString(),
            Convert.ToHexString(requiredField), requiredGraph.Contract.ToString(), sameBody, rejections, passed, null);
    }

    private static Rejection Reject(string description, byte[] bytes, EncodingKind encoding)
    {
        try
        {
            GraphCodec.Decode(bytes, encoding);
            return new Rejection(description, Convert.ToHexString(bytes), false, null);
        }
        catch (InvalidDataException exception)
        {
            return new Rejection(description, Convert.ToHexString(bytes), true, exception.Message);
        }
    }

    internal static string EncodingPath(string directory, string fixture, EncodingKind encoding) =>
        Path.Combine(directory, "encodings", $"{fixture}.{(encoding == EncodingKind.Baseline ? "wlb" : "wlp")}");

    internal static void SaveJson<T>(string path, T data) =>
        File.WriteAllText(path, JsonSerializer.Serialize(data, Program.Json) + Environment.NewLine);
}

internal sealed record Comparison(string Case, object[] Arguments, Observation Expected, Observation Actual, bool Match);
internal sealed record VerificationRow(string Fixture, string Codec, int Bytes, int Nodes, bool GraphEqual,
    bool DeterministicBytes, Comparison[] Observations, string? Error)
{
    public bool Passed => Error is null && GraphEqual && DeterministicBytes && Observations.All(row => row.Match);
}

internal sealed record Rejection(string Case, string Hex, bool Rejected, string? Error);
internal sealed record ContractProbe(string Codec, string? PlainFieldHex, string? PlainDecodedContract,
    string? ConstrainedFieldHex, string? ConstrainedDecodedContract, bool OtherwiseSameGraph,
    Rejection[] Rejections, bool Passed, string? Error);
internal sealed record Verification(int VerifierProcessId, bool Passed, VerificationRow[] Rows, ContractProbe[] Contracts);
