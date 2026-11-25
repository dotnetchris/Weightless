using System.Security.Cryptography;
using System.Text.Json;

namespace Weightless;

internal sealed record LabFinding(string Claim, string Verdict, bool ChecksPassed, object Evidence);

internal static class LabRun
{
    private const string Pipeline = "0006-v1";
    private const string SeedPath = "experiments/0001-Addition/baseline-v1/add-checked.wlb";
    private const string SeedHash = "F29EB79B426681794A0A7BDC22C47CF38B9792C93ADF96326CD3964563877B34";

    internal static (Graph Decoded, byte[] Bytes) RoundTrip(Graph graph, EncodingKind codec)
    {
        var bytes = GraphCodec.Encode(graph, codec);
        var decoded = GraphCodec.Decode(bytes, codec);
        GraphValidation.Require(decoded.Matches(graph), $"Graph changed during {codec} round trip.");
        GraphValidation.Require(bytes.AsSpan().SequenceEqual(GraphCodec.Encode(decoded, codec)),
            $"Re-encoding changed the {codec} bytes.");
        return (decoded, bytes);
    }

    internal static int Run(string[] args, string lab, int version, Func<Graph, string, LabFinding> experiment)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Pass an output root. Run the project from the Weightless repository root.");
            return 2;
        }

        var parent = Path.Combine(Path.GetFullPath(args[0]), Pipeline, lab);
        var destination = Path.Combine(parent, $"v{version}");
        var staging = Path.Combine(parent, $".v{version}-{Guid.NewGuid():N}");
        try
        {
            var bytes = File.ReadAllBytes(SeedPath);
            GraphValidation.Require(Convert.ToHexString(SHA256.HashData(bytes)) == SeedHash,
                "The checked Add seed differs from the pinned 0006-v1 bytes.");
            var seed = GraphCodec.Decode(bytes, EncodingKind.Baseline);
            GraphValidation.Require(seed.Matches(Fixtures.Add(Overflow.Checked)), "Unexpected checked Add seed graph.");
            Directory.CreateDirectory(staging);
            var finding = experiment(seed, staging);
            var record = new
            {
                pipeline = Pipeline, route = $"{lab}/v{version}",
                parent = new { id = "add.checked.plain", path = SeedPath, sha256 = SeedHash },
                finding
            };
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, NewLine = "\n" };
            File.WriteAllBytes(Path.Combine(staging, "result.json"), JsonSerializer.SerializeToUtf8Bytes(record, json));

            // Publish a complete version in one rename. Concurrent identical replays may share it.
            try
            {
                Directory.Move(staging, destination);
            }
            catch (IOException) when (Directory.Exists(destination))
            {
                if (!SameFiles(staging, destination))
                    throw new IOException($"Different evidence already exists at {destination}. Replay into a fresh output root.");
            }

            Console.WriteLine($"{record.route} {finding.Verdict} (checks {(finding.ChecksPassed ? "passed" : "failed")})  {destination}");
            return finding.ChecksPassed ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    private static bool SameFiles(string left, string right)
    {
        static string[] Paths(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file)).Order(StringComparer.Ordinal).ToArray();

        var files = Paths(left);
        return files.SequenceEqual(Paths(right), StringComparer.Ordinal) && files.All(file =>
            File.ReadAllBytes(Path.Combine(left, file)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(right, file))));
    }
}
