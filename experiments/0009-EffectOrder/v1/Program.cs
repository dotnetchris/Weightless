using Weightless;

return LabRun.Run(args, "effects", 1, (seed, _) =>
{
    int[] shared = [7];
    var write = new Effect("buffer-write", Buffer: 0, Index: 0, Value: 5);
    var bounds = nameof(IndexOutOfRangeException);
    (InputCase Input, Observation StoreFirst, Observation LoadFirst)[] inputs =
    [
        (new("distinct-equal-buffers", [new[] { 7 }, new[] { 7 }, 2, 3, 0, 0]),
            new(7, null, "", [write], [[5], [7]]), new(7, null, "", [write], [[5], [7]])),
        (new("same-cell", [shared, shared, 2, 3, 0, 0]),
            new(5, null, "", [write], [[5]]), new(7, null, "", [write], [[5]])),
        (new("idempotent-store-before-failed-load", [new[] { 5 }, Array.Empty<int>(), 2, 3, 0, 0]),
            new(null, bounds, "", [write], [[5], []]), new(null, bounds, "", [], [[5], []]))
    ];
    var rows = new List<object>();
    var passed = true;
    foreach (var codec in new[] { EncodingKind.Baseline, EncodingKind.Packed })
    {
        var storeFirst = LabRun.RoundTrip(EffectGraphs.FromSeed(seed, true), codec);
        var loadFirst = LabRun.RoundTrip(EffectGraphs.FromSeed(seed, false), codec);
        var cases = inputs.Select(input =>
        {
            var original = Interpreter.Observe(storeFirst.Decoded, input.Input.FreshArguments());
            var reordered = Interpreter.Observe(loadFirst.Decoded, input.Input.FreshArguments());
            return new { input.Input.Name, original, reordered, equivalent = original.Matches(reordered),
                match = original.Matches(input.StoreFirst) && reordered.Matches(input.LoadFirst) };
        }).ToArray();
        passed &= cases.All(row => row.match) && cases.Count(row => !row.equivalent) == 2;
        rows.Add(new { codec = codec.ToString(), cases });
    }
    return new LabFinding("Moving a load ahead of the store following checked Add preserves all observations for every input.",
        passed ? "refuted" : "unexpected", passed, new { rows });
});
