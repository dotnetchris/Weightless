using Weightless.Research.BalanceHistory;

(int Opening, int[] Deltas, int[] Initial, int? Result, int[] Final)[] cases =
[
    (10, [], [90], 10, [90]), (10, [3], [], 13, [13]),
    (10, [3, 0, -2], [90], 11, [90, 13, 13, 11]),
    (int.MaxValue, [1], [90], null, [90]),
    (int.MaxValue - 1, [1, 1], [90], null, [90, int.MaxValue]),
    (int.MinValue + 1, [-1, -1], [], null, [int.MinValue])
];
foreach (var test in cases)
{
    var deltas = (int[])test.Deltas.Clone();
    var history = new List<int>(test.Initial);
    int? result = null;
    try { result = BalanceHistory.Append(test.Opening, deltas, history); }
    catch (OverflowException) when (test.Result is null) { }
    if (result != test.Result || !history.SequenceEqual(test.Final) || !deltas.SequenceEqual(test.Deltas))
        throw new InvalidOperationException($"Opening {test.Opening}, deltas [{string.Join(", ", deltas)}], history [{string.Join(", ", history)}].");
}
Console.WriteLine($"{cases.Length} C# balance histories matched the expected outcome and caller-visible sequences.");
