using Weightless;

return LabRun.Run(args, "transfer", 3, (seed, staging) =>
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
    var candidate = TransferGraph.FromSeed(seed);
    const int originalId = 0;
    const int transferredId = 1;
    var program = new SelectorProgram(new ShiftFitsInt32(0, 1), new ShiftFitsInt32(1, -1),
        new SelectorBranch(transferredId, originalId), seed with { Functions = [seed.Functions[0], candidate.Functions[0]] });
    object? firstFailure = null;
    object? checkFailure = null;
    var runs = new List<object>();
    foreach (var (codec, extension) in new[] { (EncodingKind.Baseline, "wlb"), (EncodingKind.Packed, "wlp") })
    {
        var codecName = codec.ToString();
        try
        {
            var image = SelectorCodec.Encode(program, codec);
            var decoded = SelectorCodec.Decode(image.Bytes, codec);
            GraphValidation.Require(decoded.Matches(program), "The decoded selector or arms changed.");
            GraphValidation.Require(SelectorCodec.Encode(decoded, codec).Bytes.AsSpan().SequenceEqual(image.Bytes),
                "Re-encoding changed the selector bytes.");
            GraphValidation.Require(!seed.Matches(candidate), "The original and transferred arms must be distinct.");
            var file = $"selector.{extension}";
            File.WriteAllBytes(Path.Combine(staging, file), image.Bytes);

            Trace Run(int a, int b)
            {
                // This endpoint oracle is independent of the decoded shifted-range tests.
                var expectedFunction = a == int.MaxValue || b == int.MinValue ? originalId : transferredId;
                var expected = CheckedSum(a, b);
                var selection = decoded.Execute(a, b);
                var actual = new Outcome((int?)selection.Outcome.Value, selection.Outcome.ExceptionType);
                return new Trace(a, b, expectedFunction, selection.SelectedFunction, expected, actual,
                    expectedFunction == selection.SelectedFunction && actual == expected &&
                    selection.Outcome.Output.Length == 0 && selection.Outcome.Effects.Length == 0 &&
                    selection.Outcome.Buffers.Length == 0);
            }

            var grid = (from a in values from b in values select Run(a, b)).ToArray();
            var named = probes.Select(probe => new { probe.Name, trace = Run(probe.A, probe.B) }).ToArray();
            var mismatch = grid.Concat(named.Select(probe => probe.trace)).FirstOrDefault(trace => !trace.Match);
            if (mismatch is not null)
                firstFailure ??= new { codec = codecName, mismatch };
            var transferred = grid.Where(trace => trace.SelectedFunction == transferredId).ToArray();
            var original = grid.Length - transferred.Length;
            var admittedUpperOverflows = transferred.Count(trace => trace.Actual.ExceptionType == nameof(OverflowException)
                && (long)trace.A + trace.B > int.MaxValue);
            var admittedLowerOverflows = transferred.Count(trace => trace.Actual.ExceptionType == nameof(OverflowException)
                && (long)trace.A + trace.B < int.MinValue);
            var fallbackOverflows = grid.Count(trace => trace.SelectedFunction == originalId &&
                trace.Actual.ExceptionType == nameof(OverflowException));
            if (original != 21 || transferred.Length != 100 || admittedUpperOverflows != 10 || admittedLowerOverflows != 10)
                firstFailure ??= new { codec = codecName, original, transferred = transferred.Length,
                    admittedUpperOverflows, admittedLowerOverflows };

            // Both arms return 1 on (0, 1). Reversing the decoded branch must still change the execution trace.
            var flipped = program with { Branch = new SelectorBranch(originalId, transferredId) };
            var flippedBytes = SelectorCodec.Encode(flipped, codec).Bytes;
            var flippedSelection = SelectorCodec.Decode(flippedBytes, codec).Execute(0, 1);
            var ordinary = decoded.Execute(0, 1);
            var branchControl = ordinary.SelectedFunction == transferredId && flippedSelection.SelectedFunction == originalId &&
                ordinary.Outcome.Matches(flippedSelection.Outcome);
            if (!branchControl)
                checkFailure ??= new { codec = codecName, ordinary, flippedSelection };

            var rejections = ProbeMalformed(image.Bytes, codec);
            var acceptedMalformed = rejections.FirstOrDefault(rejection => !rejection.Rejected);
            if (acceptedMalformed is not null)
                checkFailure ??= new { codec = codecName, acceptedMalformed };

            runs.Add(new
            {
                codec = codecName, file, totalBytes = image.Bytes.Length, armBytes = image.ArmBytes,
                selectorAndFramingBytes = image.Bytes.Length - image.ArmBytes,
                gridCases = grid.Length, original, transferred = transferred.Length, admittedUpperOverflows,
                admittedLowerOverflows, fallbackOverflows, gridMatches = grid.Count(trace => trace.Match), named,
                branchControl = new { matched = branchControl, ordinary, flippedSelection }, rejections
            });
        }
        catch (InvalidDataException exception)
        {
            checkFailure ??= new { codec = codecName, error = exception.Message };
        }
    }

    var verdict = checkFailure is not null ? "failed" : firstFailure is not null ? "refuted" : "tested";
    return new LabFinding("A fully serialized typed selector preserves v2 branch identity and checked C# outcomes on all 121 fixed-k pairs through both codecs.",
        verdict, verdict == "tested", new
        {
            parentRoute = "transfer/v2",
            startRevision = "d17fb0ff88f12e566f755df7b587831602c5bf81",
            observation = "The executed arm, return value or exception type on two already evaluated Int32 inputs.",
            oracle = "The original arm is required at a == Int32.MaxValue or b == Int32.MinValue. Compare outcomes with checked(a + b).",
            k = 1, values, armIds = new { original = originalId, transferred = transferredId }, runs,
            firstFailure = checkFailure ?? firstFailure
        });
});

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

static Rejection[] ProbeMalformed(byte[] wire, EncodingKind codec)
{
    const int firstArgument = 5;
    const int branchOpcode = 16;
    const int trueTarget = 17;
    const int armLength = 19;
    return
    [
        Reject("wrong-version", Replace(3, 2)),
        Reject("invalid-shift-argument", Replace(firstArgument, 2)),
        Reject("invalid-branch-opcode", Replace(branchOpcode, 0)),
        Reject("invalid-branch-target", Replace(trueTarget, 2)),
        Reject("overlong-arm-length", [.. wire[..armLength], (byte)(wire[armLength] | 0x80), 0, .. wire[(armLength + 1)..]]),
        Reject("truncated-arms", wire[..^1]),
        Reject("trailing-byte", [.. wire, 0])
    ];

    byte[] Replace(int offset, byte value)
    {
        var copy = wire.ToArray();
        copy[offset] = value;
        return copy;
    }

    Rejection Reject(string name, byte[] bytes)
    {
        try
        {
            SelectorCodec.Decode(bytes, codec);
            return new Rejection(name, false);
        }
        catch (InvalidDataException)
        {
            return new Rejection(name, true);
        }
    }
}

internal sealed record Outcome(int? Value, string? ExceptionType);
internal sealed record Trace(int A, int B, int ExpectedFunction, int SelectedFunction, Outcome Expected, Outcome Actual, bool Match);
internal sealed record Rejection(string Name, bool Rejected);
