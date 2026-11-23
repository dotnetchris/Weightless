using Weightless;

return LabRun.Run(args, "effects", 3, (seed, staging) =>
{
    int[] shared = [5, 7];
    int[] empty = [];
    Layout[] layouts =
    [
        new("aliased", shared, shared), new("equal-distinct", [5, 7], [5, 7]),
        new("different-distinct", [5, 7], [7, 5]), new("empty-source", [5, 7], []),
        new("empty-destination", [], [5, 7]), new("aliased-empty", empty, empty),
        new("distinct-empty", new int[0], new int[0])
    ];
    (string Name, int A, int B)[] additions =
    [
        ("write-five", 2, 3), ("write-seven", 3, 4),
        ("overflow-above", int.MaxValue, 1), ("overflow-below", int.MinValue, -1)
    ];
    int[] indices = [-1, 0, 1, 2];
    const int predictedNewlyDistinguished = 120;
    const int predictedAdmitted = 286;
    const int v2Admissions = 406;
    object? firstFailure = null;
    var probes = new List<(EncodingKind Codec, FaultSiteProbe StoreFirst, FaultSiteProbe LoadFirst)>();
    foreach (var (codec, extension) in new[] { (EncodingKind.Baseline, "wlb"), (EncodingKind.Packed, "wlp") })
    {
        try
        {
            var original = LabRun.RoundTrip(EffectGraphs.FromSeed(seed, storeFirst: true), codec);
            var reordered = LabRun.RoundTrip(EffectGraphs.FromSeed(seed, storeFirst: false), codec);
            probes.Add((codec, new FaultSiteProbe(original.Decoded, seed), new FaultSiteProbe(reordered.Decoded, seed)));
            File.WriteAllBytes(Path.Combine(staging, $"store-first.{extension}"), original.Bytes);
            File.WriteAllBytes(Path.Combine(staging, $"load-first.{extension}"), reordered.Bytes);
        }
        catch (InvalidDataException exception)
        {
            firstFailure ??= new { codec = codec.ToString(), error = exception.Message };
        }
    }

    object? firstLegacyCounterexample = null;
    var countsByReason = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var newlyDistinguishedByLayout = layouts.ToDictionary(layout => layout.Name, _ => 0);
    var cases = 0;
    var newlyDistinguished = 0;
    var legacyAdmitted = 0;
    var admitted = 0;
    var admittedDisagreements = 0;
    var equivalentButDeclined = 0;
    var observationsCompared = 0;
    var selectedComparisons = 0;

    foreach (var layout in layouts)
    foreach (var addition in additions)
    foreach (var write in indices)
    foreach (var read in indices)
    {
        var key = new { layout = layout.Name, addition = addition.Name, addition.A, addition.B, write, read };
        var input = new InputCase(layout.Name, [layout.Destination, layout.Source, addition.A, addition.B, write, read]);
        var original = Reference(layout, addition.A, addition.B, write, read, storeFirst: true);
        var reordered = Reference(layout, addition.A, addition.B, write, read, storeFirst: false);
        var legacyEquivalent = original.Outcome.Matches(reordered.Outcome);
        var equivalent = original.Matches(reordered);
        var newDifference = legacyEquivalent && !equivalent;
        var expectedNewDifference = (long)addition.A + addition.B is >= int.MinValue and <= int.MaxValue &&
            (write < 0 || write >= layout.Destination.Length) && (read < 0 || read >= layout.Source.Length);
        var decision = Decide(layout, addition.A, addition.B, write, read);

        cases++;
        countsByReason[decision.Reason] = countsByReason.GetValueOrDefault(decision.Reason) + 1;
        if (decision.LegacyAdmit)
            legacyAdmitted++;
        if (decision.Admit)
            admitted++;
        if (decision.Admit && !equivalent)
            admittedDisagreements++;
        if (!decision.Admit && equivalent)
            equivalentButDeclined++;
        if (newDifference)
        {
            newlyDistinguished++;
            newlyDistinguishedByLayout[layout.Name]++;
        }

        if (decision.LegacyAdmit && !equivalent)
            firstLegacyCounterexample ??= new { key, decision, storeFirst = original, loadFirst = reordered };
        if (newDifference != expectedNewDifference || decision.LegacyAdmit != legacyEquivalent || decision.Admit != equivalent)
            firstFailure ??= new { key, expectedNewDifference, newDifference, legacyEquivalent, equivalent, decision,
                storeFirst = original, loadFirst = reordered };

        foreach (var probe in probes)
        {
            try
            {
                var actualOriginal = probe.StoreFirst.Observe(input);
                var actualReordered = probe.LoadFirst.Observe(input);
                var selected = decision.Admit ? actualReordered : actualOriginal;
                observationsCompared += 2;
                selectedComparisons++;
                if (!actualOriginal.Matches(original) || !actualReordered.Matches(reordered) || !selected.Matches(original))
                    firstFailure ??= new { key, codec = probe.Codec.ToString(), decision, original, reordered,
                        actualOriginal, actualReordered, selected };
            }
            catch (InvalidDataException exception)
            {
                firstFailure ??= new { key, codec = probe.Codec.ToString(), error = exception.Message };
            }
        }
    }

    if (cases != 448 || newlyDistinguished != predictedNewlyDistinguished || admitted != predictedAdmitted ||
        legacyAdmitted != v2Admissions || admittedDisagreements != 0 || equivalentButDeclined != 0 ||
        firstLegacyCounterexample is null)
        firstFailure ??= new { cases, newlyDistinguished, admitted, legacyAdmitted, admittedDisagreements,
            equivalentButDeclined, expected = new { cases = 448, predictedNewlyDistinguished, predictedAdmitted, v2Admissions } };

    return new LabFinding("Observing fault site distinguishes 120 additional v2 schedule pairs. The refined guard admits 286 of 448 pairs with equal full observations.",
        firstFailure is null ? "tested" : "refuted", firstFailure is null, new
        {
            parentRoute = "effects/v2",
            startRevision = "d17fb0ff88f12e566f755df7b587831602c5bf81",
            observation = "Return value or exception type and faulting operation, stdout, ordered buffer writes and final buffer contents.",
            oracle = "Two instrumented compiled C# orders, each run on its own alias-preserving copy of the starting buffers.",
            siteProbe = "The shared interpreter runs the full decoded schedule and then finds the first faulting prefix on fresh arguments.",
            layouts = layouts.Select(layout => new { layout.Name, layout.Destination, layout.Source,
                aliased = ReferenceEquals(layout.Destination, layout.Source) }),
            additions = additions.Select(addition => new { addition.Name, addition.A, addition.B }), indices,
            predictions = new { newlyDistinguished = predictedNewlyDistinguished, admitted = predictedAdmitted },
            cases, newlyDistinguished, newlyDistinguishedByLayout, legacyAdmitted, admitted, declined = cases - admitted,
            admittedDisagreements, equivalentButDeclined, countsByReason, observationsCompared, selectedComparisons,
            firstLegacyCounterexample, firstFailure
        });
});

static Decision Decide(Layout layout, int a, int b, int write, int read)
{
    var sum = (long)a + b;
    if (sum is < int.MinValue or > int.MaxValue)
        return new Decision(true, true, "overflow-before-memory");

    var readIsValid = (uint)read < (uint)layout.Source.Length;
    if ((uint)write >= (uint)layout.Destination.Length)
        return readIsValid ? new Decision(true, true, "failed-write-after-valid-read")
            : new Decision(true, false, "different-faulting-operations");
    if (!readIsValid)
        return new Decision(false, false, "write-before-failed-read");
    if (!ReferenceEquals(layout.Destination, layout.Source) || write != read)
        return new Decision(true, true, "disjoint-cells");
    return layout.Destination[write] == sum ? new Decision(true, true, "idempotent-same-cell")
        : new Decision(false, false, "changed-same-cell");
}

static FaultObservation Reference(Layout layout, int a, int b, int write, int read, bool storeFirst)
{
    var destination = (int[])layout.Destination.Clone();
    var aliased = ReferenceEquals(layout.Destination, layout.Source);
    var source = aliased ? destination : (int[])layout.Source.Clone();
    var writes = new List<Effect>();
    int? value = null;
    string? exceptionType = null;
    string? operation = "checked-add";
    try
    {
        var sum = checked(a + b);
        void Store()
        {
            operation = "store";
            destination[write] = sum;
            writes.Add(new Effect("buffer-write", Buffer: 0, Index: write, Value: sum));
        }

        int Load()
        {
            operation = "load";
            return source[read];
        }

        if (storeFirst)
        {
            Store();
            value = Load();
        }
        else
        {
            var loaded = Load();
            Store();
            value = loaded;
        }
        operation = null;
    }
    catch (Exception exception) when (exception is OverflowException or IndexOutOfRangeException)
    {
        exceptionType = exception.GetType().Name;
    }

    var finalBuffers = aliased ? new[] { destination } : [destination, source];
    return new FaultObservation(operation, new Observation(value, exceptionType, "", writes.ToArray(), finalBuffers));
}

internal sealed record Layout(string Name, int[] Destination, int[] Source);
internal sealed record Decision(bool LegacyAdmit, bool Admit, string Reason);
