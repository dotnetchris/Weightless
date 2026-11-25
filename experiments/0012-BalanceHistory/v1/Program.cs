using System.Security.Cryptography;
using System.Text;
using Weightless;
using Weightless.Research.BalanceHistory;

return LabRun.Run(args, "balance-history", 1, (seed, staging) =>
{
    const string claim = "A decoded input-length traversal preserves the checked running balance and all caller-visible appends.";
    const string sourcePath = "experiments/0012-BalanceHistory/reference/BalanceHistory.cs";
    var source = new
    {
        path = sourcePath, method = $"{typeof(BalanceHistory).FullName}.Append",
        sha256LfUtf8 = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(File.ReadAllText(sourcePath).Replace("\r\n", "\n", StringComparison.Ordinal))))
    };
    HistoryCase[] cases =
    [
        new("empty", 10, [], [90]),
        new("singleton", 10, [3], []),
        new("duplicate-and-negative", 10, [3, 0, -2], [90]),
        new("first-overflow", int.MaxValue, [1], [90]),
        new("late-upper-overflow", int.MaxValue - 1, [1, 1], [90]),
        new("late-lower-overflow", int.MinValue + 1, [-1, -1], []),
        new("longer", 42, Enumerable.Range(0, 257).Select(index => index % 7 - 3).ToArray(), [99, -99])
    ];
    var original = new HistoryPlan(0, 1, 2, AppendValue.UpdatedBalance, AppendTiming.Immediate, seed);
    var images = new List<WireImage>();
    var rows = new List<CaseComparison>();
    try
    {
        var decoded = SaveAndReopen("balance-history.wlp", original);
        var appendDelta = SaveAndReopen("append-input-delta.wlp", decoded with { ValueToAppend = AppendValue.InputDelta });
        var staged = SaveAndReopen("commit-after-traversal.wlp", decoded with { Timing = AppendTiming.AfterTraversal });
        foreach (var input in cases)
        {
            var expected = Observe(input, Reference);
            var actual = Observe(input, decoded.Execute);
            rows.Add(new CaseComparison(input, expected, actual, actual.Matches(expected)));
        }

        var ordinary = rows.Single(row => row.Input.Name == "duplicate-and-negative");
        var deltaActual = Observe(ordinary.Input, appendDelta.Execute);
        var deltaExpected = new HistoryObservation(11, null, 4, [90, 3, 0, -2], [3, 0, -2]);
        var deltaControl = deltaActual.Matches(deltaExpected) && deltaActual.Value == ordinary.Expected.Value &&
            !deltaActual.Matches(ordinary.Expected);

        var stagedRows = cases.Select(input =>
        {
            var expected = Observe(input, StagedReference);
            var actual = Observe(input, staged.Execute);
            return new CaseComparison(input, expected, actual, actual.Matches(expected));
        }).ToArray();
        var counterexample = stagedRows.Zip(rows).FirstOrDefault(pair => !pair.First.Actual.Matches(pair.Second.Expected));
        var firstStagedCounterexample = counterexample.First is { } candidate && counterexample.Second is { } baseline
            ? new { candidate.Input, incremental = baseline.Expected, staged = candidate.Actual }
            : null;
        var stagedControl = stagedRows.All(row => row.Match) &&
            firstStagedCounterexample?.Input.Name == "late-upper-overflow" &&
            firstStagedCounterexample.incremental.ExceptionType == nameof(OverflowException) &&
            firstStagedCounterexample.staged.ExceptionType == nameof(OverflowException) &&
            firstStagedCounterexample.incremental.History.SequenceEqual([90, int.MaxValue]) &&
            firstStagedCounterexample.staged.History.SequenceEqual([90]);

        var firstDisagreement = rows.FirstOrDefault(row => !row.Match);
        var controlsPassed = deltaControl && stagedControl;
        var verdict = !controlsPassed ? "failed" : firstDisagreement is not null ? "refuted" : "tested";
        return new LabFinding(claim, verdict, verdict == "tested", new
        {
            startRevision = "1ac9d1946048379a1d685e4cbf79c2cb03f3198e",
            predecessor = "0003-Buffer and the checked Add seed",
            source,
            origin = "Hand-built traversal, append and return. The saved checked Add step is the frozen seed, encoded with packed v1.",
            observation = "Result or exception type, the original list's count and ordered contents, and final input array contents.",
            images, cases = rows, firstDisagreement,
            controls = new
            {
                appendInputDelta = new { ordinary.Input, expected = deltaExpected, actual = deltaActual, passed = deltaControl },
                stageAllAppends = new
                {
                    claim = "Committing appends after successful traversal is equivalent to incremental append.",
                    verdict = stagedControl ? "refuted" : "failed", passed = stagedControl,
                    firstCounterexample = firstStagedCounterexample,
                    firstOracleDisagreement = stagedRows.FirstOrDefault(row => !row.Match),
                    comparisons = stagedRows.Length
                }
            }
        });
    }
    catch (InvalidDataException exception)
    {
        return new LabFinding(claim, "failed", false, new { source, images, cases = rows, error = exception.Message });
    }

    HistoryPlan SaveAndReopen(string name, HistoryPlan plan)
    {
        var path = Path.Combine(staging, name);
        File.WriteAllBytes(path, HistoryCodec.Encode(plan));
        var bytes = File.ReadAllBytes(path);
        var recovered = HistoryCodec.Decode(bytes);
        GraphValidation.Require(recovered.Matches(plan), $"Structure changed in {name}.");
        GraphValidation.Require(bytes.AsSpan().SequenceEqual(HistoryCodec.Encode(recovered)), $"Bytes changed in {name}.");
        var stepBytes = GraphCodec.Encode(recovered.CheckedStep, EncodingKind.Packed).Length;
        images.Add(new WireImage(name, bytes.Length, bytes.Length - stepBytes, stepBytes,
            Convert.ToHexString(SHA256.HashData(bytes))));
        return recovered;
    }
});

static HistoryObservation Observe(HistoryCase input, Func<int, int[], List<int>, HistoryResult> run)
{
    var array = (int[])input.Deltas.Clone();
    var owned = new List<int>(input.InitialHistory);
    var alias = owned;
    var result = run(input.Opening, array, owned);
    return new HistoryObservation(result.Value, result.ExceptionType, alias.Count, alias.ToArray(), array);
}

static HistoryResult Reference(int opening, int[] deltas, List<int> history)
{
    try
    {
        return new HistoryResult(BalanceHistory.Append(opening, deltas, history));
    }
    catch (OverflowException exception)
    {
        return new HistoryResult(null, exception.GetType().Name);
    }
}

static HistoryResult StagedReference(int opening, int[] deltas, List<int> history)
{
    var pending = new List<int>();
    var result = Reference(opening, deltas, pending);
    if (result.ExceptionType is null)
        history.AddRange(pending);
    return result;
}

internal sealed record HistoryCase(string Name, int Opening, int[] Deltas, int[] InitialHistory);
internal sealed record HistoryObservation(int? Value, string? ExceptionType, int Count, int[] History, int[] FinalDeltas)
{
    internal bool Matches(HistoryObservation other) =>
        Value == other.Value && ExceptionType == other.ExceptionType && Count == other.Count &&
        History.SequenceEqual(other.History) && FinalDeltas.SequenceEqual(other.FinalDeltas);
}
internal sealed record CaseComparison(HistoryCase Input, HistoryObservation Expected, HistoryObservation Actual, bool Match);
internal sealed record WireImage(string File, int TotalBytes, int TraversalBytes, int CheckedStepBytes, string Sha256);
