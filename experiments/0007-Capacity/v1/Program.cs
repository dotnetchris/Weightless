using Weightless;

return LabRun.Run(args, "capacity", 1, (seed, _) =>
{
    var rows = new List<object>();
    var passed = true;
    foreach (var adds in new[] { 0, 1, 2 })
    {
        var plain = AddChain.FromSeed(seed, adds);
        var required = plain with { Contract = Postcondition.NonNegativeInt32 };
        var baselinePlain = LabRun.RoundTrip(plain, EncodingKind.Baseline);
        var packedPlain = LabRun.RoundTrip(plain, EncodingKind.Packed);
        var baselineRequired = LabRun.RoundTrip(required, EncodingKind.Baseline);
        var packedRequired = LabRun.RoundTrip(required, EncodingKind.Packed);
        var delta = packedRequired.Bytes.Length - baselinePlain.Bytes.Length;
        var plainResult = new Observation(-1, null, "", [], []);
        var violation = new Observation(null, nameof(PostconditionViolationException), "", [], []);
        var match = Interpreter.Observe(baselinePlain.Decoded, [-1, 0]).Matches(plainResult) &&
            Interpreter.Observe(packedPlain.Decoded, [-1, 0]).Matches(plainResult) &&
            Interpreter.Observe(baselineRequired.Decoded, [-1, 0]).Matches(violation) &&
            Interpreter.Observe(packedRequired.Decoded, [-1, 0]).Matches(violation);
        passed &= match && delta == 1 - adds && baselinePlain.Bytes.Length - packedPlain.Bytes.Length == adds &&
            baselineRequired.Bytes.Length - packedRequired.Bytes.Length == adds;
        rows.Add(new { adds, baselinePlain = baselinePlain.Bytes.Length, packedPlain = packedPlain.Bytes.Length,
            baselineRequired = baselineRequired.Bytes.Length, packedRequired = packedRequired.Bytes.Length,
            delta, negativeWitnessMatches = match });
    }
    return new LabFinding("With zero, one and two native Add nodes, packed/constrained costs 1 - AddCount bytes relative to baseline/plain.",
        passed ? "tested" : "failed", passed, new { negativeWitness = new[] { -1, 0 }, rows });
});
