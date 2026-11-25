using Weightless;

internal static class TransferGraph
{
    // V1 specialises k = 1. Adding the literal -1 implements b - 1 with checked Int32 addition.
    internal static Graph FromSeed(Graph seed)
    {
        var entry = seed.Functions[0];
        var add = (Addition)entry.Body[0];
        var transferred = entry with
        {
            Body = [new Literal(ValueKind.Int32, 1), new Literal(ValueKind.Int32, -1),
                add with { Right = 2 }, add with { Left = add.Right, Right = 3 },
                add with { Left = 4, Right = 5 }, new Return(6)]
        };
        return seed with { Functions = seed.Functions.SetItem(0, transferred) };
    }
}
