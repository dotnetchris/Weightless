using System.Collections.Immutable;
using Weightless;

internal static class AddChain
{
    internal static Graph FromSeed(Graph seed, int count)
    {
        if (count < 0 || count >= GraphValidation.MaxNodes)
            throw new ArgumentOutOfRangeException(nameof(count));
        var function = seed.Functions[0];
        var add = (Addition)function.Body[0];
        var accumulator = add.Left;
        var body = ImmutableArray.CreateBuilder<Instruction>(count + 1);
        for (var index = 0; index < count; index++)
        {
            body.Add(add with { Left = accumulator });
            accumulator = function.Parameters.Length + index;
        }
        body.Add(new Return(accumulator));
        return seed with { Functions = seed.Functions.SetItem(0, function with { Body = body.MoveToImmutable() }) };
    }
}
