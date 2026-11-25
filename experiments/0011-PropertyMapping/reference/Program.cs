using Weightless.Research.PropertyMapping;

(int Units, int AlertBelow, int OnHand, int ReorderPoint, bool Rejected)[] cases =
[
    (17, 3, 17, 3, false), (0, 0, 0, 0, false), (8, 4, 8, 4, false),
    (17, -2, 17, 4, true)
];
foreach (var test in cases)
{
    var row = new StockRow("SKU-42") { OnHand = 8, ReorderPoint = 4 };
    var update = new StockUpdate(test.Units, test.AlertBelow);
    StockRow? returned = null;
    ArgumentOutOfRangeException? fault = null;
    try { returned = StockMapping.Apply(update, row); }
    catch (ArgumentOutOfRangeException error) { fault = error; }

    var outcomeMatches = test.Rejected
        ? fault?.ParamName == "value" && Equals(fault.ActualValue, test.AlertBelow) && returned is null
        : fault is null && ReferenceEquals(returned, row);
    if (!outcomeMatches || row.OnHand != test.OnHand || row.ReorderPoint != test.ReorderPoint || row.Sku != "SKU-42")
        throw new InvalidOperationException($"Mapping {update} left ({row.OnHand}, {row.ReorderPoint}) with {fault?.ParamName}.");
}
Console.WriteLine($"{cases.Length} C# mappings matched the expected identity, fields and fault details.");
