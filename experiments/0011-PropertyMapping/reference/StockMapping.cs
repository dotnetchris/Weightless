namespace Weightless.Research.PropertyMapping;

public sealed record StockUpdate(int Units, int AlertBelow);

public sealed class StockRow(string sku)
{
    public string Sku { get; } = sku;
    public int OnHand { get; set; }
    private int reorderPoint;
    public int ReorderPoint
    {
        get => reorderPoint;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            reorderPoint = value;
        }
    }
}

public static class StockMapping
{
    public static StockRow Apply(StockUpdate update, StockRow row)
    {
        row.OnHand = update.Units;
        row.ReorderPoint = update.AlertBelow;
        return row;
    }
}
