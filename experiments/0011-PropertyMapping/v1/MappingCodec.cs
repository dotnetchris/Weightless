using System.Collections.Immutable;
using System.Text;
using Weightless.Research.PropertyMapping;
using static Weightless.GraphValidation;

internal sealed record Assignment(byte SourceReceiver, byte Getter, byte DestinationReceiver, byte Setter);

internal sealed record MappingPlan(ImmutableArray<Assignment> Assignments, byte ReturnReceiver)
{
    internal StockRow Execute(StockUpdate update, StockRow row)
    {
        object[] receivers = [update, row];
        foreach (var assignment in Assignments)
        {
            var source = (StockUpdate)receivers[assignment.SourceReceiver];
            var destination = (StockRow)receivers[assignment.DestinationReceiver];
            var value = StockSchema.Members[assignment.Getter].Read!(source);
            StockSchema.Members[assignment.Setter].Write!(destination, value);
        }
        return (StockRow)receivers[ReturnReceiver];
    }

    internal bool Matches(MappingPlan other) =>
        ReturnReceiver == other.ReturnReceiver && Assignments.SequenceEqual(other.Assignments);
}

internal sealed record StockMember(byte Receiver, string Name, Func<StockUpdate, int>? Read = null,
    Action<StockRow, int>? Write = null)
{
    internal byte Access => Read is not null ? (byte)1 : (byte)2;
}

internal static class StockSchema
{
    internal const byte UpdateReceiver = 0;
    internal const byte RowReceiver = 1;
    internal static readonly string[] Receivers = [typeof(StockUpdate).FullName!, typeof(StockRow).FullName!];
    internal static readonly StockMember[] Members =
    [
        new(UpdateReceiver, nameof(StockUpdate.Units), Read: update => update.Units),
        new(UpdateReceiver, nameof(StockUpdate.AlertBelow), Read: update => update.AlertBelow),
        new(RowReceiver, nameof(StockRow.OnHand), Write: (row, value) => row.OnHand = value),
        new(RowReceiver, nameof(StockRow.ReorderPoint), Write: (row, value) => row.ReorderPoint = value)
    ];

    internal static StockMember GetMember(byte id)
    {
        Require(id < Members.Length, "Unknown stock member ID.");
        return Members[id];
    }
}

internal static class MappingCodec
{
    private static ReadOnlySpan<byte> Header => "WPM\x01"u8;
    private const byte AssignmentCount = 2;

    internal static (byte[] Bytes, int SchemaBytes) Encode(MappingPlan plan)
    {
        Validate(plan);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Header);
        writer.Write((byte)StockSchema.Receivers.Length);
        foreach (var receiver in StockSchema.Receivers)
            WriteName(writer, receiver);
        writer.Write((byte)StockSchema.Members.Length);
        foreach (var member in StockSchema.Members)
        {
            writer.Write(member.Receiver);
            writer.Write(member.Access);
            WriteName(writer, member.Name);
        }
        var schemaBytes = (int)stream.Position - Header.Length;

        writer.Write(AssignmentCount);
        foreach (var assignment in plan.Assignments)
        {
            writer.Write(assignment.SourceReceiver);
            writer.Write(assignment.Getter);
            writer.Write(assignment.DestinationReceiver);
            writer.Write(assignment.Setter);
        }
        writer.Write(plan.ReturnReceiver);
        return (stream.ToArray(), schemaBytes);
    }

    internal static MappingPlan Decode(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new BinaryReader(stream);
            Require(reader.ReadBytes(Header.Length).AsSpan().SequenceEqual(Header), "Invalid property-mapping header.");
            Require(reader.ReadByte() == StockSchema.Receivers.Length, "Invalid receiver count.");
            foreach (var receiver in StockSchema.Receivers)
                ExpectName(reader, receiver);
            Require(reader.ReadByte() == StockSchema.Members.Length, "Invalid stock member count.");
            foreach (var member in StockSchema.Members)
            {
                Require(reader.ReadByte() == member.Receiver && reader.ReadByte() == member.Access,
                    "Invalid stock member receiver or access kind.");
                ExpectName(reader, member.Name);
            }
            Require(reader.ReadByte() == AssignmentCount, "Invalid mapping assignment count.");
            var assignments = ImmutableArray.CreateBuilder<Assignment>(AssignmentCount);
            for (var index = 0; index < AssignmentCount; index++)
                assignments.Add(new Assignment(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()));
            var plan = new MappingPlan(assignments.MoveToImmutable(), reader.ReadByte());
            Require(stream.Position == stream.Length, "Trailing property-mapping bytes.");
            Validate(plan);
            return plan;
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Truncated property-mapping plan.", exception);
        }
    }

    private static void Validate(MappingPlan plan)
    {
        Require(plan.Assignments.Length == AssignmentCount, "The v1 mapping requires two assignments.");
        Require(plan.ReturnReceiver == StockSchema.RowReceiver, "A mapping must return its StockRow receiver.");
        foreach (var assignment in plan.Assignments)
        {
            var getter = StockSchema.GetMember(assignment.Getter);
            var setter = StockSchema.GetMember(assignment.Setter);
            Require(getter.Receiver == assignment.SourceReceiver && getter.Read is not null &&
                setter.Receiver == assignment.DestinationReceiver && setter.Write is not null,
                "Invalid receiver or accessor for a mapping assignment.");
        }
    }

    private static void WriteName(BinaryWriter writer, string name)
    {
        var text = Encoding.UTF8.GetBytes(name);
        writer.Write(checked((byte)text.Length));
        writer.Write(text);
    }

    private static void ExpectName(BinaryReader reader, string expected)
    {
        var size = reader.ReadByte();
        Require(reader.ReadBytes(size).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(expected)),
            $"Expected stock schema name {expected}.");
    }
}
