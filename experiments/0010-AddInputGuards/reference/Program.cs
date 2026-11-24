using Weightless.Research.InputGuards;

(int A, int B, Outcome Expected)[] cases =
[
    (-2, 0, Range("a", -2)), (0, -2, Range("b", -2)), (-2, -3, Range("a", -2)),
    (-1, -1, new(-2)), (0, -1, new(-1)), (0, 0, new(0)), (2, 3, new(5)),
    (-1, int.MaxValue, new(int.MaxValue - 1)), (int.MaxValue, 1, new(null, nameof(OverflowException))),
    (int.MinValue, -1, Range("a", int.MinValue))
];

foreach (var (a, b, expected) in cases)
{
    var explicitIf = Observe(() => GuardedAdd.WithIf(a, b));
    var helper = Observe(() => GuardedAdd.WithHelper(a, b));
    if (explicitIf != expected || helper != expected)
        throw new InvalidOperationException($"({a}, {b}): expected {expected}, if {explicitIf}, helper {helper}");
}
Console.WriteLine($"{cases.Length} guarded Add cases matched both C# forms.");

static Outcome Range(string parameter, int value) =>
    new(null, nameof(ArgumentOutOfRangeException), parameter, value);

static Outcome Observe(Func<int> add)
{
    try { return new(add()); }
    catch (ArgumentOutOfRangeException error) { return new(null, error.GetType().Name, error.ParamName, (int?)error.ActualValue); }
    catch (OverflowException error) { return new(null, error.GetType().Name); }
}

internal sealed record Outcome(int? Value, string? Exception = null, string? Parameter = null, int? ActualValue = null);
