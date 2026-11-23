using Weightless;

return LabRun.Run(args, "transfer", 2, (seed, directory) =>
{
    int[] values = [int.MinValue, int.MinValue + 1, int.MinValue + 2, -2, -1, 0, 1, 2,
        int.MaxValue - 2, int.MaxValue - 1, int.MaxValue];
    (string Name, int A, int B)[] probes =
    [
        ("ordinary", 0, 1),
        ("parent-left-intermediate", int.MaxValue, 0),
        ("parent-right-intermediate", 0, int.MinValue),
        ("both-intermediates-out-of-range", int.MaxValue, int.MinValue),
        ("left-hits-maximum", int.MaxValue - 1, 0),
        ("right-hits-minimum", 0, int.MinValue + 1),
        ("admitted-final-upper-overflow", int.MaxValue - 1, 2),
        ("admitted-final-lower-overflow", int.MinValue, -1),
        ("fallback-final-overflow", int.MaxValue, 1)
    ];
    var transferred = TransferGraph.FromSeed(seed);
    var passed = !seed.Matches(transferred);
    var runs = new List<object>();
    foreach (var (codec, extension) in new[] { (EncodingKind.Baseline, "wlb"), (EncodingKind.Packed, "wlp") })
    {
        var original = LabRun.RoundTrip(seed, codec);
        var candidate = LabRun.RoundTrip(transferred, codec);
        var originalFile = $"original.{extension}";
        var candidateFile = $"transferred.{extension}";
        File.WriteAllBytes(Path.Combine(directory, originalFile), original.Bytes);
        File.WriteAllBytes(Path.Combine(directory, candidateFile), candidate.Bytes);

        Trace Run(int a, int b) => Dispatch(original.Decoded, candidate.Decoded, a, b);

        var grid = (from a in values from b in values select Run(a, b)).ToArray();
        var named = probes.Select(probe => new { probe.Name, trace = Run(probe.A, probe.B) }).ToArray();
        var mismatches = grid.Where(trace => !trace.Match).ToArray();
        var admitted = grid.Where(trace => trace.Selected == "transferred").ToArray();
        var admittedUpperOverflows = admitted.Count(trace => trace.Actual.ExceptionType == nameof(OverflowException)
            && (long)trace.A + trace.B > int.MaxValue);
        var admittedLowerOverflows = admitted.Count(trace => trace.Actual.ExceptionType == nameof(OverflowException)
            && (long)trace.A + trace.B < int.MinValue);
        passed &= mismatches.Length == 0 && named.All(probe => probe.trace.Match) &&
            admitted.Length > 0 && admitted.Length < grid.Length && admittedUpperOverflows > 0 && admittedLowerOverflows > 0;
        runs.Add(new
        {
            codec = codec.ToString(), originalFile, candidateFile,
            gridCases = grid.Length, transferred = admitted.Length, original = grid.Length - admitted.Length,
            admittedUpperOverflows, admittedLowerOverflows, gridMismatches = mismatches, named
        });
    }

    return new LabFinding("On the specified pure Int32 grid with k = 1, widened dispatch selects the intended decoded graph and matches the compiled checked C# sum.",
        passed ? "tested" : "failed", passed, new
        {
            parentRoute = "transfer/v1", modelRelationship = "guarded-checked-transfer-model-v1", k = 1,
            observation = "Return value or exception type. Both operands are already evaluated.",
            selector = "Host C# computes a + 1 and b - 1 in Int64 and tests both against the Int32 range.",
            grid = "Cartesian product of values with a outermost and b innermost",
            values, runs
        });
});

static Trace Dispatch(Graph original, Graph transferred, int a, int b)
{
    var left = (long)a + 1;
    var right = (long)b - 1;
    var admitted = left is >= int.MinValue and <= int.MaxValue && right is >= int.MinValue and <= int.MaxValue;
    var chosen = admitted ? transferred : original;
    var outcome = Interpreter.Observe(chosen, [a, b]);
    var selected = ReferenceEquals(chosen, transferred) ? "transferred" : "original";

    // With the constant k = 1, just these two endpoint values can invalidate the guard.
    var expectedPath = a == int.MaxValue || b == int.MinValue ? "original" : "transferred";
    var expected = CheckedSum(a, b);
    var actual = new Outcome((int?)outcome.Value, outcome.ExceptionType);
    return new Trace(a, b, left, right, selected, expectedPath, expected, actual,
        selected == expectedPath && actual == expected);
}

static Outcome CheckedSum(int a, int b)
{
    try
    {
        return new Outcome(checked(a + b), null);
    }
    catch (OverflowException)
    {
        return new Outcome(null, nameof(OverflowException));
    }
}

internal sealed record Outcome(int? Value, string? ExceptionType);
internal sealed record Trace(int A, int B, long WidenedLeft, long WidenedRight, string Selected,
    string ExpectedPath, Outcome Expected, Outcome Actual, bool Match);
