using Weightless;

return LabRun.Run(args, "capacity", 2, (seed, _) =>
{
    int[] lengths = [0, 1, 2, 64, 124, 125, 126, 127, 128, 129, 130, GraphValidation.MaxNodes - 1];
    Probe[] probes =
    [
        new("positive", 3, 2), new("negative", -1, 0), new("zero", 0, 0),
        new("upper-overflow", int.MaxValue, 1), new("lower-overflow", int.MinValue, -1),
        new("second-add-overflow", int.MaxValue - 1, 1)
    ];
    var rows = new List<object>();
    var comparisons = 0;
    object? firstFailure = null;
    foreach (var adds in lengths)
    {
        var plain = AddChain.FromSeed(seed, adds);
        var constrained = plain with { Contract = Postcondition.NonNegativeInt32 };
        (string Name, Graph Graph, EncodingKind Codec)[] forms =
        [
            ("baselinePlain", plain, EncodingKind.Baseline), ("packedPlain", plain, EncodingKind.Packed),
            ("baselineConstrained", constrained, EncodingKind.Baseline), ("packedConstrained", constrained, EncodingKind.Packed)
        ];
        var sizes = new Dictionary<string, int>();
        BaselineFields? fields = null;
        var matches = 0;
        foreach (var form in forms)
        {
            try
            {
                var image = LabRun.RoundTrip(form.Graph, form.Codec);
                sizes.Add(form.Name, image.Bytes.Length);
                if (form.Name == "baselinePlain")
                    fields = ReadBaselineFields(image.Bytes, adds);
                foreach (var probe in probes)
                {
                    var expected = Reference(adds, probe, form.Graph.Contract);
                    var actual = Interpreter.Observe(image.Decoded, [probe.A, probe.B]);
                    comparisons++;
                    if (actual.Matches(expected))
                        matches++;
                    else
                        firstFailure ??= new { adds, form.Name, probe, expected, actual };
                }
            }
            catch (InvalidDataException error)
            {
                firstFailure ??= new { adds, form.Name, error = error.Message };
                break;
            }
        }

        if (sizes.Count != 4)
            break;

        // In the plain WLB control, the Return field, body count and Add operands widen separately.
        var extraBytes = (adds >= 126 ? 1 : 0) + (adds >= 127 ? 1 : 0) + Math.Max(0, adds - 127);
        var expectedBaseline = 14 + 5 * adds + extraBytes;
        var delta = sizes["packedConstrained"] - sizes["baselinePlain"];
        var sizeMatches = sizes["baselinePlain"] == expectedBaseline &&
            sizes["packedPlain"] == expectedBaseline - adds && sizes["baselineConstrained"] == expectedBaseline + 1 &&
            delta == 1 - adds;
        if (!sizeMatches)
            firstFailure ??= new { adds, expectedBaseline, expectedDelta = 1 - adds, sizes, delta };

        rows.Add(new { adds, sizes, delta, fields, sizeMatches, matchedObservations = matches });
        if (firstFailure is not null)
            break;
    }

    return new LabFinding("Across the sampled checked Add chains, including the first two-byte counts and references, packed/constrained costs 1 - AddCount bytes relative to baseline/plain and preserves the specified observations.",
        firstFailure is null ? "tested" : "failed", firstFailure is null,
        new { parentRoute = "capacity/v1", oracle = "Checked C# loop followed by the entry postcondition.",
            probes, rows, comparisons, firstFailure });
});

static Observation Reference(int adds, Probe input, Postcondition contract)
{
    var value = input.A;
    try
    {
        for (var step = 0; step < adds; step++)
            value = checked(value + input.B);
    }
    catch (OverflowException)
    {
        return new Observation(null, nameof(OverflowException), "", [], []);
    }
    if (contract == Postcondition.NonNegativeInt32 && value < 0)
        return new Observation(null, nameof(PostconditionViolationException), "", [], []);
    return new Observation(value, null, "", [], []);
}

static BaselineFields ReadBaselineFields(byte[] wire, int adds)
{
    // Every WLB/plain fixture here has a 4-byte header, plain discriminator, one function,
    // Int32 return type and two Int32 parameters. The body count starts at byte offset 10.
    using var stream = new MemoryStream(wire, writable: false) { Position = 10 };
    using var reader = new BinaryReader(stream);
    string ReadField(int expected)
    {
        var start = (int)stream.Position;
        GraphValidation.Require(reader.Read7BitEncodedInt() == expected, "Unexpected checked chain field.");
        return Convert.ToHexString(wire.AsSpan(start, (int)stream.Position - start));
    }

    var bodyCount = ReadField(adds + 1);
    string? lastAddLeft = null;
    for (var step = 0; step < adds; step++)
    {
        stream.Position += 3; // Add opcode, Int32 result type, checked overflow policy.
        lastAddLeft = ReadField(step == 0 ? 0 : step + 1);
        reader.Read7BitEncodedInt(); // Right parameter reference.
    }
    stream.Position += 2; // Return opcode and void result type.
    var returnReferencePlusOne = ReadField(adds == 0 ? 1 : adds + 2);
    GraphValidation.Require(stream.Position == stream.Length, "Unexpected checked chain wire layout.");
    return new BaselineFields(bodyCount, lastAddLeft, returnReferencePlusOne);
}

internal sealed record Probe(string Name, int A, int B);
internal sealed record BaselineFields(string BodyCountHex, string? LastAddLeftHex, string ReturnReferencePlusOneHex);
