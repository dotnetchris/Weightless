using System.Collections.Immutable;
using System.Text;
using Weightless;
using static Weightless.GraphValidation;

internal sealed record InputGuard(byte Operand, int LowerBound, string Parameter);
internal sealed record GuardObservation(int? Value, string? ExceptionType = null, string? Parameter = null, int? ActualValue = null);

internal sealed record GuardPlan(ImmutableArray<InputGuard> Guards, Graph Sum)
{
    internal GuardObservation Execute(int a, int b)
    {
        int[] values = [a, b];
        foreach (var guard in Guards)
        {
            var actual = values[guard.Operand];
            if (actual < guard.LowerBound)
                throw new ArgumentOutOfRangeException(guard.Parameter, actual, null);
        }

        var result = Interpreter.Observe(Sum, [a, b]);
        return new GuardObservation((int?)result.Value, result.ExceptionType);
    }

    internal bool Matches(GuardPlan other) => Guards.SequenceEqual(other.Guards) && Sum.Matches(other.Sum);
}

internal static class GuardCodec
{
    private static ReadOnlySpan<byte> Header => "WIG\x01"u8;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private const byte ThrowIfLessThan = 1;
    private const byte GuardCount = 2;

    internal static byte[] Encode(GuardPlan plan)
    {
        Validate(plan);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Header);
        writer.Write(GuardCount);
        foreach (var guard in plan.Guards)
        {
            writer.Write(ThrowIfLessThan);
            writer.Write(guard.Operand);
            writer.Write(guard.LowerBound);
            var name = Utf8.GetBytes(guard.Parameter);
            writer.Write((byte)name.Length);
            writer.Write(name);
        }
        writer.Write(GraphCodec.Encode(plan.Sum, EncodingKind.Packed));
        return stream.ToArray();
    }

    internal static GuardPlan Decode(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new BinaryReader(stream);
            Require(reader.ReadBytes(Header.Length).AsSpan().SequenceEqual(Header), "Invalid input-guard header.");
            Require(reader.ReadByte() == GuardCount, "Expected two ordered input guards.");
            var guards = ImmutableArray.CreateBuilder<InputGuard>(GuardCount);
            for (var index = 0; index < GuardCount; index++)
            {
                Require(reader.ReadByte() == ThrowIfLessThan, "Unknown input-guard opcode.");
                var operand = reader.ReadByte();
                var bound = reader.ReadInt32();
                var size = reader.ReadByte();
                var name = reader.ReadBytes(size);
                Require(name.Length == size, "Truncated parameter name.");
                guards.Add(new InputGuard(operand, bound, Utf8.GetString(name)));
            }

            var sum = GraphCodec.Decode(bytes[(int)stream.Position..], EncodingKind.Packed);
            var plan = new GuardPlan(guards.MoveToImmutable(), sum);
            Validate(plan);
            return plan;
        }
        catch (Exception exception) when (exception is EndOfStreamException or DecoderFallbackException)
        {
            throw new InvalidDataException("Invalid or truncated input-guard field.", exception);
        }
    }

    private static void Validate(GuardPlan plan)
    {
        Require(plan.Guards.Length == GuardCount && plan.Guards.All(guard =>
            guard.Operand < 2 && Utf8.GetByteCount(guard.Parameter) is > 0 and <= byte.MaxValue),
            "Expected two typed guards with a valid operand and diagnostic name.");
        Require(plan.Sum.Matches(Fixtures.Add(Overflow.Checked)), "Expected the original checked Add body.");
    }
}
