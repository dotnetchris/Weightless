using Weightless;
using static Weightless.GraphValidation;

internal enum AppendValue : byte { UpdatedBalance, InputDelta }
internal enum AppendTiming : byte { Immediate, AfterTraversal }

internal sealed record HistoryResult(int? Value, string? ExceptionType = null);

internal sealed record HistoryPlan(byte OpeningSlot, byte ArraySlot, byte ListSlot,
    AppendValue ValueToAppend, AppendTiming Timing, Graph CheckedStep)
{
    internal HistoryResult Execute(int opening, int[] deltas, List<int> history)
    {
        object[] arguments = [opening, deltas, history];
        var balance = (int)arguments[OpeningSlot];
        var input = (int[])arguments[ArraySlot];
        var destination = (List<int>)arguments[ListSlot];
        var appended = Timing == AppendTiming.Immediate ? destination : new List<int>();

        foreach (var delta in input)
        {
            var step = Interpreter.Observe(CheckedStep, [balance, delta]);
            if (step.ExceptionType is not null)
                return new HistoryResult(null, step.ExceptionType);

            balance = (int)step.Value!;
            appended.Add(ValueToAppend == AppendValue.UpdatedBalance ? balance : delta);
        }

        if (Timing == AppendTiming.AfterTraversal)
            destination.AddRange(appended);
        return new HistoryResult(balance);
    }

    internal bool Matches(HistoryPlan other) =>
        OpeningSlot == other.OpeningSlot && ArraySlot == other.ArraySlot && ListSlot == other.ListSlot &&
        ValueToAppend == other.ValueToAppend && Timing == other.Timing && CheckedStep.Matches(other.CheckedStep);
}

internal static class HistoryCodec
{
    private enum Opcode : byte { ForEach = 1, CheckedStep, Append, ReturnBalance }
    private static ReadOnlySpan<byte> Header => "WBH\x01"u8;

    internal static byte[] Encode(HistoryPlan plan)
    {
        Validate(plan);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Header);
        writer.Write((byte)Opcode.ForEach);
        writer.Write(plan.OpeningSlot);
        writer.Write(plan.ArraySlot);
        writer.Write((byte)Opcode.CheckedStep);
        writer.Write((byte)Opcode.Append);
        writer.Write(plan.ListSlot);
        writer.Write((byte)plan.ValueToAppend);
        writer.Write((byte)plan.Timing);
        writer.Write((byte)Opcode.ReturnBalance);
        writer.Write(GraphCodec.Encode(plan.CheckedStep, EncodingKind.Packed));
        return stream.ToArray();
    }

    internal static HistoryPlan Decode(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new BinaryReader(stream);
            Require(reader.ReadBytes(Header.Length).AsSpan().SequenceEqual(Header), "Invalid balance-history header.");
            ReadOpcode(Opcode.ForEach);
            var opening = reader.ReadByte();
            var array = reader.ReadByte();
            ReadOpcode(Opcode.CheckedStep);
            ReadOpcode(Opcode.Append);
            var list = reader.ReadByte();
            var value = (AppendValue)reader.ReadByte();
            var timing = (AppendTiming)reader.ReadByte();
            ReadOpcode(Opcode.ReturnBalance);
            var step = GraphCodec.Decode(bytes[(int)stream.Position..], EncodingKind.Packed);
            var plan = new HistoryPlan(opening, array, list, value, timing, step);
            Validate(plan);
            return plan;

            void ReadOpcode(Opcode opcode) =>
                Require(reader.ReadByte() == (byte)opcode, $"Expected {opcode} in the balance-history plan.");
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Truncated balance-history plan.", exception);
        }
    }

    private static void Validate(HistoryPlan plan)
    {
        Require(plan.OpeningSlot == 0 && plan.ArraySlot == 1 && plan.ListSlot == 2,
            "Balance history expects an Int32, Int32[] and List<Int32> in that order.");
        Require(Enum.IsDefined(plan.ValueToAppend) && Enum.IsDefined(plan.Timing), "Unknown append operand or timing.");
        Require(plan.CheckedStep.Matches(Fixtures.Add(Overflow.Checked)), "Expected the original checked Add step.");
    }
}
