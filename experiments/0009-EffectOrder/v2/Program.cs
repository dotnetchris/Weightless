using Weightless;

return LabRun.Run(args, "effects", 2, (seed, staging) =>
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
    var named = Witnesses();
    var graphs = new[] { EncodingKind.Baseline, EncodingKind.Packed }.Select(codec =>
    {
        var original = LabRun.RoundTrip(EffectGraphs.FromSeed(seed, true), codec);
        var reordered = LabRun.RoundTrip(EffectGraphs.FromSeed(seed, false), codec);
        var extension = codec == EncodingKind.Baseline ? "wlb" : "wlp";
        File.WriteAllBytes(Path.Combine(staging, $"store-first.{extension}"), original.Bytes);
        File.WriteAllBytes(Path.Combine(staging, $"load-first.{extension}"), reordered.Bytes);
        return (Codec: codec, Original: original.Decoded, Reordered: reordered.Decoded);
    }).ToArray();

    var countsByReason = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var witnesses = new List<object>();
    var mismatches = new List<object>();
    var cases = 0;
    var admitted = 0;
    var equivalentButDeclined = 0;
    var admittedDisagreements = 0;
    var graphComparisons = 0;
    var selectedComparisons = 0;
    var namedChecksPassed = true;
    foreach (var layout in layouts)
    foreach (var addition in additions)
    foreach (var write in indices)
    foreach (var read in indices)
    {
        var key = new CaseKey(layout.Name, addition.Name, write, read);
        var input = new InputCase(layout.Name, [layout.Destination, layout.Source, addition.A, addition.B, write, read]);
        var decision = Decide(layout, addition.A, addition.B, write, read);
        var original = Reference(layout, addition.A, addition.B, write, read, storeFirst: true);
        var reordered = Reference(layout, addition.A, addition.B, write, read, storeFirst: false);
        var equivalent = original.Matches(reordered);
        cases++;
        countsByReason[decision.Reason] = countsByReason.GetValueOrDefault(decision.Reason) + 1;
        if (decision.Admit) admitted++;
        if (decision.Admit && !equivalent) admittedDisagreements++;
        if (!decision.Admit && equivalent) equivalentButDeclined++;
        if (decision.Admit != equivalent)
            mismatches.Add(new { key, decision.Reason, decision.Admit, equivalent, original, reordered });

        foreach (var pair in graphs)
        {
            var actualOriginal = Interpreter.Observe(pair.Original, input.FreshArguments());
            var actualReordered = Interpreter.Observe(pair.Reordered, input.FreshArguments());
            var selected = decision.Admit ? actualReordered : actualOriginal;
            graphComparisons += 2;
            selectedComparisons++;
            if (!actualOriginal.Matches(original) || !actualReordered.Matches(reordered) || !selected.Matches(original))
                mismatches.Add(new { key, codec = pair.Codec.ToString(), decision.Reason, decision.Admit,
                    original, reordered, actualOriginal, actualReordered });
        }

        if (named.TryGetValue(key, out var expected))
        {
            var checksPassed = decision.Reason == expected.Reason && decision.Admit == expected.Admit &&
                equivalent == expected.Admit && Equals(original.Value, expected.StoreValue) &&
                Equals(reordered.Value, expected.LoadValue) && original.ExceptionType == expected.Fault &&
                reordered.ExceptionType == expected.Fault && original.Effects.Length == expected.StoreWrites &&
                reordered.Effects.Length == expected.LoadWrites;
            namedChecksPassed &= checksPassed;
            witnesses.Add(new { expected.Name, key, decision.Reason, decision.Admit, equivalent,
                storeFirst = original, loadFirst = reordered, checksPassed });
        }
    }

    var passed = namedChecksPassed && witnesses.Count == named.Count && admitted > 0 && admitted < cases &&
        admittedDisagreements == 0 && equivalentButDeclined == 0 && mismatches.Count == 0;
    var verdict = passed ? "tested" : mismatches.Count > 0 ? "refuted" : "unexpected";
    return new LabFinding("For the recorded single-threaded checked Add, store and load grid, the guard admits exactly the schedules with equal full observations.",
        verdict, passed, new
        {
            parentRoute = "effects/v1",
            observation = "Return or exception type, stdout, ordered writes by entry-buffer identity, and all final buffer contents.",
            layouts = layouts.Select(l => new { l.Name, l.Destination, l.Source, aliased = ReferenceEquals(l.Destination, l.Source) }),
            additions = additions.Select(i => new { i.Name, i.A, i.B }), indices,
            cases, admitted, declined = cases - admitted, admittedDisagreements, equivalentButDeclined,
            countsByReason, graphComparisons, selectedComparisons, graphAndSelectionMismatches = mismatches, witnesses
        });
});

static (bool Admit, string Reason) Decide(Layout layout, int a, int b, int write, int read)
{
    var sum = (long)a + b;
    if (sum is < int.MinValue or > int.MaxValue) return (true, "overflow-before-memory");
    if ((uint)write >= (uint)layout.Destination.Length) return (true, "failed-write");
    if ((uint)read >= (uint)layout.Source.Length) return (false, "write-before-failed-read");
    if (!ReferenceEquals(layout.Destination, layout.Source) || write != read) return (true, "disjoint-cells");
    return layout.Destination[write] == sum ? (true, "idempotent-same-cell") : (false, "changed-same-cell");
}

static Observation Reference(Layout layout, int a, int b, int write, int read, bool storeFirst)
{
    var destination = (int[])layout.Destination.Clone();
    var aliased = ReferenceEquals(layout.Destination, layout.Source);
    var source = aliased ? destination : (int[])layout.Source.Clone();
    var effects = new List<Effect>();
    int? value = null;
    string? fault = null;
    try
    {
        var sum = checked(a + b);
        void Store()
        {
            destination[write] = sum;
            effects.Add(new Effect("buffer-write", Buffer: 0, Index: write, Value: sum));
        }
        if (storeFirst)
        {
            Store();
            value = source[read];
        }
        else
        {
            var loaded = source[read];
            Store();
            value = loaded;
        }
    }
    catch (Exception exception) when (exception is OverflowException or IndexOutOfRangeException)
    {
        fault = exception.GetType().Name;
    }
    return new Observation(value, fault, "", effects.ToArray(), aliased ? [destination] : [destination, source]);
}

static Dictionary<CaseKey, Witness> Witnesses()
{
    const string bounds = nameof(IndexOutOfRangeException);
    const string overflow = nameof(OverflowException);
    return new()
    {
        [new("aliased", "write-seven", 0, 0)] = new("changed-aliased-cell", "changed-same-cell", false, 7, 5, null, 1, 1),
        [new("aliased", "write-five", 0, 0)] = new("idempotent-aliased-cell", "idempotent-same-cell", true, 5, 5, null, 1, 1),
        [new("aliased", "write-seven", 0, 1)] = new("different-indices-one-buffer", "disjoint-cells", true, 7, 7, null, 1, 1),
        [new("equal-distinct", "write-seven", 0, 0)] = new("same-index-two-buffers", "disjoint-cells", true, 5, 5, null, 1, 1),
        [new("empty-source", "write-five", 0, 0)] = new("idempotent-store-before-failed-load", "write-before-failed-read", false, null, null, bounds, 1, 0),
        [new("empty-source", "write-seven", 0, 0)] = new("changed-store-before-failed-load", "write-before-failed-read", false, null, null, bounds, 1, 0),
        [new("empty-destination", "write-five", 0, 0)] = new("failed-write-valid-read", "failed-write", true, null, null, bounds, 0, 0),
        [new("aliased-empty", "write-five", 0, 0)] = new("both-accesses-fail", "failed-write", true, null, null, bounds, 0, 0),
        [new("distinct-empty", "overflow-above", -1, -1)] = new("overflow-before-bounds", "overflow-before-memory", true, null, null, overflow, 0, 0),
        [new("aliased", "overflow-below", 1, 1)] = new("overflow-before-valid-write", "overflow-before-memory", true, null, null, overflow, 0, 0)
    };
}

internal sealed record Layout(string Name, int[] Destination, int[] Source);
internal sealed record CaseKey(string Layout, string Addition, int Write, int Read);
internal sealed record Witness(string Name, string Reason, bool Admit, int? StoreValue, int? LoadValue,
    string? Fault, int StoreWrites, int LoadWrites);
