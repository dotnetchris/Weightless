#:property PublishAot=false

using System.Runtime.InteropServices;
using System.Text.Json;

int[] values = [int.MinValue, int.MinValue + 1, int.MinValue + 2, -2, -1, 0, 1, 2,
    int.MaxValue - 2, int.MaxValue - 1, int.MaxValue];
Input[] probes =
[
    new("parent-left-intermediate", int.MaxValue, 0, 1),
    new("parent-right-intermediate", 0, int.MinValue, 1),
    new("left-hits-maximum", int.MaxValue - 1, 0, 1),
    new("right-hits-minimum", 0, int.MinValue + 1, 1),
    new("final-overflow-above-maximum", int.MaxValue, 1, -1),
    new("final-overflow-below-minimum", int.MinValue, -1, 1)
];

var grid = (from a in values from b in values from k in values
    select Compare(new Input("grid", a, b, k))).ToArray();
var selected = probes.Select(Compare).ToArray();
var disagreements = grid.Concat(selected).Where(row => row.Original != row.Guarded).ToArray();
var passed = disagreements.Length == 0 &&
    selected.Take(2).All(row => !row.Transferred) &&
    selected.Skip(2).All(row => row.Transferred) &&
    selected.TakeLast(2).All(row => row.Guarded.ExceptionType == nameof(OverflowException));

Console.WriteLine(JsonSerializer.Serialize(new
{
    experiment = "0005-GuardedAddition",
    parent = "0001-Addition/checked-transfer-v1",
    runtime = RuntimeInformation.FrameworkDescription,
    values,
    gridCases = grid.Length,
    guardAccepted = grid.Count(row => row.Transferred),
    guardRejected = grid.Count(row => !row.Transferred),
    acceptedWithFinalOverflow = grid.Count(row => row.Transferred && row.Guarded.ExceptionType is not null),
    selected,
    disagreements,
    passed
}, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
return passed ? 0 : 1;

static Row Compare(Input input)
{
    var (name, a, b, k) = input;
    long left = (long)a + k;
    long right = (long)b - k;
    var transferred = FitsInt32(left) && FitsInt32(right);
    var original = Observe(() => checked(a + b));
    var guarded = Observe(() => transferred ? checked((a + k) + (b - k)) : checked(a + b));
    return new Row(name, a, b, k, left, right, transferred, original, guarded);
}

static bool FitsInt32(long value) => value is >= int.MinValue and <= int.MaxValue;

static Outcome Observe(Func<int> run)
{
    try { return new Outcome(run(), null); }
    catch (OverflowException) { return new Outcome(null, nameof(OverflowException)); }
}

record Input(string Name, int A, int B, int K);
record Outcome(int? Value, string? ExceptionType);
record Row(string Name, int A, int B, int K, long WidenedLeft, long WidenedRight,
    bool Transferred, Outcome Original, Outcome Guarded);
