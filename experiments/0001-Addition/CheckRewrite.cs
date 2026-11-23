#:property PublishAot=false

using System.Runtime.InteropServices;
using System.Text.Json;

// Compare two pure C# expressions. Each checked/unchecked scope covers every intermediate.
var cases = new[]
{
    new Inputs("ordinary", 0, 1, 1),
    new Inputs("left-intermediate-overflow", int.MaxValue, 0, 1),
    new Inputs("right-intermediate-overflow", 0, int.MinValue, 1),
    new Inputs("sum-overflow", int.MaxValue, 1, 0)
};

var observations = cases.Select(input =>
{
    var (name, a, b, k) = input;
    return new
    {
        name, a, b, k,
        checkedI32 = new
        {
            original = Observe(() => checked(a + b)),
            candidate = Observe(() => checked((a + k) + (b - k)))
        },
        wrappingI32 = new
        {
            original = unchecked(a + b),
            candidate = unchecked((a + k) + (b - k))
        }
    };
}).ToArray();

var witnesses = observations.Where(row => row.checkedI32.original != row.checkedI32.candidate)
    .Select(row => row.name).ToArray();
var result = new
{
    experiment = "0001-Addition/checked-transfer-v1",
    claim = "For all Int32 a, b, k, checked(a + b) and checked((a + k) + (b - k)) return the same value or throw the same exception type.",
    runtime = RuntimeInformation.FrameworkDescription,
    observations,
    checkedCounterexamples = witnesses,
    verdict = witnesses.Length > 0 ? "refuted" : "no-counterexample-in-corpus"
};
Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
}));

// Detect changes to either the witnesses or the control cases on replay.
return witnesses.SequenceEqual(new[] { "left-intermediate-overflow", "right-intermediate-overflow" })
    && observations[0].checkedI32.original == new Outcome(1, null)
    && observations[3].checkedI32.original == new Outcome(null, nameof(OverflowException))
    && observations.All(row => row.wrappingI32.original == row.wrappingI32.candidate) ? 0 : 1;

static Outcome Observe(Func<int> expression)
{
    try { return new Outcome(expression(), null); }
    catch (OverflowException) { return new Outcome(null, nameof(OverflowException)); }
}

record Inputs(string Name, int A, int B, int K);
record Outcome(int? Value, string? ExceptionType);
