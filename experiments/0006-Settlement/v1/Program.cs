using Weightless;

return LabRun.Run(args, "settlement", 1, (seed, _) =>
{
    var entry = seed.Functions[0];
    var add = (Addition)entry.Body[0];
    var swapped = seed with
    {
        Functions = seed.Functions.SetItem(0, entry with
        {
            Body = entry.Body.SetItem(0, add with { Left = add.Right, Right = add.Left })
        })
    };
    (int A, int B)[] inputs =
    [
        (2, 3), (-1, 0), (-1, 1), (int.MaxValue, 0), (0, int.MinValue),
        (int.MaxValue, 1), (int.MinValue, -1)
    ];
    var rows = new List<object>();
    var passed = !seed.Matches(swapped);
    foreach (var (order, graph) in new[] { ("a+b", seed), ("b+a", swapped) })
    foreach (var contract in new[] { Postcondition.None, Postcondition.NonNegativeInt32 })
    foreach (var codec in new[] { EncodingKind.Baseline, EncodingKind.Packed })
    {
        var image = LabRun.RoundTrip(graph with { Contract = contract }, codec);
        var comparisons = inputs.Select(input =>
        {
            var expected = Reference(input.A, input.B, contract);
            var actual = Interpreter.Observe(image.Decoded, [input.A, input.B]);
            return new { actual.Value, actual.ExceptionType, match = expected.Matches(actual) };
        }).ToArray();
        var expectedBytes = (codec == EncodingKind.Baseline ? 19 : 18) + (contract == Postcondition.None ? 0 : 1);
        passed &= image.Bytes.Length == expectedBytes && comparisons.All(row => row.match);
        rows.Add(new
        {
            order, contract = contract.ToString(), codec = codec.ToString(), size = image.Bytes.Length,
            hex = Convert.ToHexString(image.Bytes), comparisons
        });
    }

    var aliasIsolation = CheckFreshArguments();
    passed &= aliasIsolation;
    return new LabFinding("For the two operand orders, the recovered Add tag pays for the enforced nonnegative contract on the seven recorded pure inputs.",
        passed ? "tested" : "failed", passed, new
        {
            inputs = inputs.Select(pair => new { pair.A, pair.B }), rows,
            distinctGraphs = !seed.Matches(swapped), aliasIsolation
        });
});

static Observation Reference(int a, int b, Postcondition contract)
{
    try
    {
        var sum = checked(a + b);
        if (contract == Postcondition.NonNegativeInt32 && sum < 0)
            return new Observation(null, nameof(PostconditionViolationException), "", [], []);
        return new Observation(sum, null, "", [], []);
    }
    catch (OverflowException)
    {
        return new Observation(null, nameof(OverflowException), "", [], []);
    }
}

static bool CheckFreshArguments()
{
    int[] first = [7];
    int[] equal = [7];
    var input = new InputCase("alias-isolation", [first, first, equal, new int[0], new int[0]]);
    var copies = input.FreshArguments();
    ((int[])copies[0])[0] = 9;
    return ReferenceEquals(copies[0], copies[1]) && ((int[])copies[1])[0] == 9 &&
        !ReferenceEquals(copies[0], copies[2]) && ((int[])copies[2])[0] == 7 &&
        !ReferenceEquals(copies[3], copies[4]) && first[0] == 7 &&
        ((int[])input.FreshArguments()[0])[0] == 7;
}
