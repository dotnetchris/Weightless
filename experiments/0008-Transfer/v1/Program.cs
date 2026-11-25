using Weightless;

return LabRun.Run(args, "transfer", 1, (seed, _) =>
{
    var candidate = TransferGraph.FromSeed(seed);
    (int A, int B, int? Original, int? Candidate)[] inputs =
    [
        (0, 1, 1, 1), (int.MaxValue, 0, int.MaxValue, null), (0, int.MinValue, int.MinValue, null),
        (int.MaxValue - 1, 2, null, null)
    ];
    var passed = !seed.Matches(candidate);
    var rows = new List<object>();
    foreach (var codec in new[] { EncodingKind.Baseline, EncodingKind.Packed })
    {
        var original = LabRun.RoundTrip(seed, codec);
        var transferred = LabRun.RoundTrip(candidate, codec);
        var cases = inputs.Select(input =>
        {
            var left = Interpreter.Observe(original.Decoded, [input.A, input.B]);
            var right = Interpreter.Observe(transferred.Decoded, [input.A, input.B]);
            var match = left.Matches(Expected(input.Original)) && right.Matches(Expected(input.Candidate));
            return new { input.A, input.B, original = left, candidate = right, equivalent = left.Matches(right), match };
        }).ToArray();
        passed &= cases.All(row => row.match) && cases.Count(row => !row.equivalent) == 2;
        rows.Add(new { codec = codec.ToString(), candidateHex = Convert.ToHexString(transferred.Bytes), cases });
    }
    return new LabFinding("For all pure Int32 a and b, the native checked transfer with k = 1 preserves the return value or exception type.",
        passed ? "refuted" : "unexpected", passed, new { parentRelationship = "checked-transfer-v1", k = 1, rows });
});

static Observation Expected(int? value) => new(value, value.HasValue ? null : nameof(OverflowException), "", [], []);
