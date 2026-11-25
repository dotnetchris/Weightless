using System.Collections.Immutable;
using Weightless;

internal static class EffectGraphs
{
    // Entry arguments are destination, source, a, b, write index and read index.
    // Both schedules call the seed as function 1 before either memory operation.
    internal static Graph FromSeed(Graph seed, bool storeFirst)
    {
        var sum = new Call(ValueKind.Int32, 1, [2, 3]);
        var store = new Store(0, 4, 6);
        var load = new Load(1, 5);
        ImmutableArray<Instruction> body = storeFirst
            ? [sum, store, load, new Return(8)]
            : [sum, load, store, new Return(7)];
        var entry = new Function(ValueKind.Int32,
            [ValueKind.Int32Buffer, ValueKind.Int32Buffer, ValueKind.Int32, ValueKind.Int32, ValueKind.Int32, ValueKind.Int32],
            body);
        return seed with { Functions = seed.Functions.Insert(0, entry) };
    }
}
