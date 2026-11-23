using System.Runtime.InteropServices;
using System.Text.Json;

namespace Weightless;

internal static class AdditionReplay
{
    internal static int Run(string directory, string original)
    {
        var inputs = Load(original);
        var comparisons = new List<object>();
        var passed = true;
        foreach (var overflow in Enum.GetValues<Overflow>())
        {
            var path = Path.Combine(directory, $"add-{overflow.ToString().ToLowerInvariant()}.wlb");
            var bytes = File.ReadAllBytes(path);
            var graph = GraphCodec.Decode(bytes, EncodingKind.Baseline);
            var equal = graph.Matches(Fixtures.Add(overflow));
            var deterministic = bytes.AsSpan().SequenceEqual(GraphCodec.Encode(graph, EncodingKind.Baseline));
            var cases = new List<object>();
            passed &= equal && deterministic;
            foreach (var input in inputs)
            {
                var actual = Interpreter.Observe(graph, [input.A, input.B]);
                var expected = overflow == Overflow.Checked ? input.Checked : input.Wrapping;
                var matches = actual.Matches(new Observation(expected.Value, expected.ExceptionType, "", [], []));
                passed &= matches;
                cases.Add(new { input.Name, input.A, input.B, expected, actual, matches });
            }

            comparisons.Add(new { overflow = overflow.ToString(), bytes = bytes.Length,
                hex = Convert.ToHexString(bytes), equal, deterministic, cases });
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            experiment = "0001-Addition/baseline-v1",
            runtime = RuntimeInformation.FrameworkDescription,
            oracle = original,
            passed,
            comparisons
        }, Program.Json));
        return passed ? 0 : 1;
    }

    internal static RecordedCase[] Load(string path)
    {
        using var saved = JsonDocument.Parse(File.ReadAllBytes(path));
        return saved.RootElement.GetProperty("observations").EnumerateArray().Select(input => new RecordedCase(
            input.GetProperty("name").GetString()!, input.GetProperty("a").GetInt32(),
            input.GetProperty("b").GetInt32(),
            input.GetProperty("checkedI32").GetProperty("original").Deserialize<ReturnOutcome>(Program.Json)!,
            new ReturnOutcome(input.GetProperty("wrappingI32").GetProperty("original").GetInt32(), null))).ToArray();
    }

    internal sealed record RecordedCase(string Name, int A, int B, ReturnOutcome Checked, ReturnOutcome Wrapping);
    internal sealed record ReturnOutcome(int? Value, string? ExceptionType);
}
