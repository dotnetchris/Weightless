using System.Collections.Immutable;

namespace Weightless;

internal sealed record Fixture(string Name, Graph Graph, InputCase[] Cases, Func<object[], Observation> Oracle);

internal sealed record InputCase(string Name, object[] Arguments)
{
    internal object[] FreshArguments() =>
        Arguments.Select(value => value is int[] buffer ? buffer.ToArray() : value).ToArray();
}

internal static class Fixtures
{
    internal static Graph Add(Overflow overflow, Postcondition contract = Postcondition.None) =>
        new(contract, [new Function(ValueKind.Int32, [ValueKind.Int32, ValueKind.Int32],
            [new Addition(ValueKind.Int32, overflow, 0, 1), new Return(2)])]);

    internal static Graph BufferedCall()
    {
        ImmutableArray<ValueKind> signature =
            [ValueKind.Int32Buffer, ValueKind.Int32, ValueKind.Int32, ValueKind.Boolean, ValueKind.Int32];
        return new Graph(Postcondition.None,
        [
            new Function(ValueKind.Int32, signature,
                [new Call(ValueKind.Int32, 1, [0, 1, 2, 3, 4]), new Return(5)]),
            new Function(ValueKind.Int32, signature,
            [
                new Branch(3, 1, 4),
                new Literal(ValueKind.Int32, 1),
                new Addition(ValueKind.Int32, Overflow.Checked, 4, 6),
                new Store(0, 1, 7),
                new Load(0, 2),
                new Return(9)
            ])
        ]);
    }

    internal static Graph RepeatedAddition()
    {
        var body = ImmutableArray.CreateBuilder<Instruction>(65);
        for (var index = 0; index < 64; index++)
        {
            var accumulator = index == 0 ? 0 : index + 1;
            body.Add(new Addition(ValueKind.Int32, Overflow.Wrapping, accumulator, 1));
        }
        body.Add(new Return(65));
        return new Graph(Postcondition.None, [new Function(ValueKind.Int32, [ValueKind.Int32, ValueKind.Int32],
            body.MoveToImmutable())]);
    }
}
