using Weightless;

return LabRun.Run(args, "capacity", 3, (seed, staging) =>
{
    int[] lengths = [0, 1, 2, 3, 64, 124, 125, 126, 127, 128, 129, 130, GraphValidation.MaxNodes - 1];
    (string Name, int A, int B)[] probes =
    [
        ("positive", 3, 2), ("negative", -1, 0), ("zero", 0, 0),
        ("upper-overflow", int.MaxValue, 1), ("lower-overflow", int.MinValue, -1),
        ("second-add-overflow", int.MaxValue - 1, 1)
    ];
    var chains = new List<ChainMeasurement>();
    var mixed = new List<object>();
    var comparisons = 0;
    object? firstFailure = null;
    object? checkFailure = null;

    foreach (var adds in lengths)
    foreach (var contract in new[] { Postcondition.None, Postcondition.NonNegativeInt32 })
    {
        var graph = AddChain.FromSeed(seed, adds) with { Contract = contract };
        var name = $"add-{adds}{(contract == Postcondition.None ? "" : "-constrained")}";
        try
        {
            var expectedSaving = adds < 2 ? 0 : 4 * adds + Math.Max(0, adds - 127) - (adds < 128 ? 4 : 5);
            var image = CompareSize(name, graph, expectedSaving);
            chains.Add(new ChainMeasurement(adds, contract.ToString(), image.V1Bytes.Length, image.RunBytes.Length,
                expectedSaving, image.V1Bytes.Length - image.RunBytes.Length));
            if ((contract == Postcondition.None && adds is 1 or 2) ||
                (contract == Postcondition.NonNegativeInt32 && adds == 2))
                Save(name, image);

            foreach (var probe in probes)
            {
                var input = new InputCase(probe.Name, [probe.A, probe.B]);
                var expected = ObserveInt(() => CheckedChain(probe.A, probe.B, adds), contract);
                CompareObservation(name, image, input, expected);
            }
        }
        catch (InvalidDataException exception)
        {
            checkFailure ??= new { name, adds, contract = contract.ToString(), error = exception.Message };
        }
    }

    var bufferInputs = new InputCase[]
    {
        BufferCase("store-and-load", 1, 1, true, 98),
        BufferCase("skip-invalid-store", 99, 1, false, 98),
        BufferCase("load-fails-after-store", 1, 3, true, 98),
        BufferCase("store-fails", -1, 1, true, 98),
        BufferCase("overflow-before-store", 1, 1, true, int.MaxValue),
        BufferCase("load-fails-without-store", 1, -1, false, 98)
    };
    CheckMixed("buffer-call", Fixtures.BufferedCall(), 0, bufferInputs, ObserveBuffer);

    var addition = (Addition)seed.Functions[0].Body[0];
    var interrupted = seed with
    {
        Functions = [seed.Functions[0] with
        {
            Body = [addition, addition with { Left = 2 }, new Literal(ValueKind.Int32, 1),
                addition with { Left = 3, Right = 4 }, addition with { Left = 5, Right = 4 }, new Return(6)]
        }]
    };
    CheckMixed("two-runs-and-literal", interrupted, 8,
        [.. probes.Select(probe => new InputCase(probe.Name, [probe.A, probe.B])),
            new InputCase("overflow-in-second-run", [int.MaxValue, 0])],
        input => ObserveInt(() => CheckedChain(CheckedChain((int)input[0], (int)input[1], 2), 1, 2), Postcondition.None));

    var firstSavingAdds = chains.Where(row => row.Saved > 0).Select(row => (int?)row.Adds).Min();
    if (firstSavingAdds != 2)
        firstFailure ??= new { expectedFirstSavingAdds = 2, actualFirstSavingAdds = firstSavingAdds };

    var rejectionProbes = ProbeMalformed(AddChain.FromSeed(seed, 2));
    checkFailure ??= rejectionProbes.FirstOrDefault(probe => !probe.Rejected);
    var verdict = checkFailure is not null ? "failed" : firstFailure is not null ? "refuted" : "tested";
    return new LabFinding("A run of checked Adds recovers the original graph and C# observations and first beats packed v1 at two Adds.",
        verdict, verdict == "tested",
        new
        {
            parentRoute = "capacity/v2",
            startRevision = "d17fb0ff88f12e566f755df7b587831602c5bf81",
            observation = "Return value or exception type, stdout, ordered writes and final buffer state.",
            wire = "Standalone WLP v2. Tag 09, run count, initial left slot and shared right slot.",
            firstSavingAdds, probes = probes.Select(probe => new { probe.Name, probe.A, probe.B }), chains, mixed,
            comparisons, rejectionProbes, firstFailure = checkFailure ?? firstFailure
        });

    Images CompareSize(string name, Graph graph, int expectedSaving)
    {
        var v1 = LabRun.RoundTrip(graph, EncodingKind.Packed);
        var wire = RunPackedCodec.Encode(graph);
        var decoded = RunPackedCodec.Decode(wire);
        GraphValidation.Require(decoded.Matches(graph), "The run codec changed the source graph.");
        GraphValidation.Require(v1.Bytes.AsSpan().SequenceEqual(GraphCodec.Encode(decoded, EncodingKind.Packed)),
            "The expanded graph changed the packed v1 bytes.");
        GraphValidation.Require(wire.AsSpan().SequenceEqual(RunPackedCodec.Encode(decoded)),
            "Re-encoding changed the run bytes.");
        var saving = v1.Bytes.Length - wire.Length;
        if (saving != expectedSaving)
            firstFailure ??= new { name, expectedSaving, actualSaving = saving, v1Bytes = v1.Bytes.Length, runBytes = wire.Length };
        return new Images(v1.Decoded, v1.Bytes, decoded, wire);
    }

    void Save(string name, Images images)
    {
        File.WriteAllBytes(Path.Combine(staging, $"{name}.wlp"), images.V1Bytes);
        File.WriteAllBytes(Path.Combine(staging, $"{name}.run.wlp"), images.RunBytes);
    }

    Observation CompareObservation(string name, Images images, InputCase input, Observation expected)
    {
        var packed = Interpreter.Observe(images.V1, input.FreshArguments());
        var run = Interpreter.Observe(images.Run, input.FreshArguments());
        comparisons += 2;
        if (!packed.Matches(expected) || !run.Matches(expected))
            firstFailure ??= new { name, input, expected, packed, run };
        return run;
    }

    void CheckMixed(string name, Graph graph, int expectedSaving, InputCase[] inputs, Func<object[], Observation> oracle)
    {
        try
        {
            var images = CompareSize(name, graph, expectedSaving);
            Save(name, images);
            var cases = inputs.Select(input => new
            {
                input.Name, input.Arguments,
                observed = CompareObservation(name, images, input, oracle(input.FreshArguments()))
            }).ToArray();
            mixed.Add(new { name, v1Bytes = images.V1Bytes.Length, runBytes = images.RunBytes.Length, expectedSaving, cases });
        }
        catch (InvalidDataException exception)
        {
            checkFailure ??= new { name, error = exception.Message };
        }
    }
});

static int CheckedChain(int a, int b, int adds)
{
    for (var step = 0; step < adds; step++)
        a = checked(a + b);
    return a;
}

static Observation ObserveInt(Func<int> compute, Postcondition contract)
{
    try
    {
        var result = compute();
        if (contract == Postcondition.NonNegativeInt32 && result < 0)
            return new Observation(null, nameof(PostconditionViolationException), "", [], []);
        return new Observation(result, null, "", [], []);
    }
    catch (OverflowException)
    {
        return new Observation(null, nameof(OverflowException), "", [], []);
    }
}

static InputCase BufferCase(string name, int write, int read, bool store, int value) =>
    new(name, [new[] { 10, 20, 30 }, write, read, store, value]);

static Observation ObserveBuffer(object[] arguments)
{
    var buffer = (int[])arguments[0];
    var writes = new List<Effect>();
    int? value = null;
    string? exceptionType = null;
    try
    {
        value = BufferSeed.Run(buffer, (int)arguments[1], (int)arguments[2], (bool)arguments[3], (int)arguments[4],
            (index, stored) => writes.Add(new Effect("buffer-write", Buffer: 0, Index: index, Value: stored)));
    }
    catch (Exception exception) when (exception is IndexOutOfRangeException or OverflowException)
    {
        exceptionType = exception.GetType().Name;
    }
    return new Observation(value, exceptionType, "", writes.ToArray(), [buffer]);
}

static Rejection[] ProbeMalformed(Graph graph)
{
    var v1 = GraphCodec.Encode(graph, EncodingKind.Packed);
    var run = RunPackedCodec.Encode(graph);
    // This fixture's single run starts at byte 11, followed by count, left and right.
    const int count = 12;
    const int right = 14;
    return
    [
        Reject("old-version", v1),
        Reject("uncompressed-eligible-run", Replace(v1, 3, 2)),
        Reject("run-of-one", Replace(run, count, 1)),
        Reject("run-past-body", Replace(run, count, 4)),
        Reject("forward-common-operand", Replace(run, right, 2)),
        Reject("overlong-count", [.. run[..count], 0x82, 0, .. run[(count + 1)..]]),
        Reject("truncated-run", run[..right]),
        Reject("trailing-byte", [.. run, 0])
    ];

    static byte[] Replace(byte[] source, int offset, byte value)
    {
        var copy = source.ToArray();
        copy[offset] = value;
        return copy;
    }

    static Rejection Reject(string name, byte[] bytes)
    {
        try
        {
            RunPackedCodec.Decode(bytes);
            return new Rejection(name, Convert.ToHexString(bytes), false);
        }
        catch (InvalidDataException)
        {
            return new Rejection(name, Convert.ToHexString(bytes), true);
        }
    }
}

internal sealed record ChainMeasurement(int Adds, string Contract, int V1Bytes, int RunBytes, int ExpectedSaving, int Saved);
internal sealed record Images(Graph V1, byte[] V1Bytes, Graph Run, byte[] RunBytes);
internal sealed record Rejection(string Name, string Hex, bool Rejected);
