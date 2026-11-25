using Weightless;
using static Weightless.GraphValidation;

// WLP version 2 for this route. Ordinary records and logical instruction indexes come from packed v1.
internal static class RunPackedCodec
{
    private enum Opcode : byte { Literal = 1, Add, Branch, Call, Load, Store, WriteLine, Return, CheckedAddRun }

    private static ReadOnlySpan<byte> PackedV1 => "WLP\x01"u8;
    private static ReadOnlySpan<byte> PackedRuns => "WLP\x02"u8;

    internal static byte[] Encode(Graph graph)
    {
        var packed = GraphCodec.Encode(graph, EncodingKind.Packed);
        return Rewrite(packed, PackedV1, PackedRuns, (reader, writer, function, parameters, count) =>
        {
            var nodes = graph.Functions[function].Body;
            for (var index = 0; index < count;)
            {
                var run = CheckedRunLength(nodes, parameters, index);
                if (run < 2)
                {
                    CopyOrdinaryRecord(reader, writer, packed);
                    index++;
                    continue;
                }

                var first = (Addition)nodes[index];
                writer.Write((byte)Opcode.CheckedAddRun);
                writer.Write7BitEncodedInt(run);
                writer.Write7BitEncodedInt(first.Left);
                writer.Write7BitEncodedInt(first.Right);
                for (var step = 0; step < run; step++)
                    SkipOrdinaryRecord(reader, (Opcode)reader.ReadByte());
                index += run;
            }
        });
    }

    internal static Graph Decode(byte[] bytes)
    {
        var packed = Rewrite(bytes, PackedRuns, PackedV1, (reader, writer, _, parameters, count) =>
        {
            for (var index = 0; index < count;)
            {
                var start = (int)reader.BaseStream.Position;
                var opcode = (Opcode)reader.ReadByte();
                if (opcode != Opcode.CheckedAddRun)
                {
                    CopyOrdinaryRecord(reader, writer, bytes, start, opcode);
                    index++;
                    continue;
                }

                var length = ReadNumber(reader, MaxNodes);
                Require(length >= 2 && length <= count - index, "Invalid checked-Add run length.");
                var left = ReadNumber(reader);
                var right = ReadNumber(reader);
                Require(left < parameters + index && right < parameters + index,
                    "Run operands must precede the first logical Add.");
                for (var step = 0; step < length; step++, index++)
                {
                    writer.Write((byte)Opcode.Add);
                    writer.Write((byte)Overflow.Checked);
                    writer.Write7BitEncodedInt(left);
                    writer.Write7BitEncodedInt(right);
                    left = parameters + index;
                }
            }
        });

        var graph = GraphCodec.Decode(packed, EncodingKind.Packed);
        Require(bytes.AsSpan().SequenceEqual(Encode(graph)), "Noncanonical checked-Add run encoding.");
        return graph;
    }

    private static int CheckedRunLength(IReadOnlyList<Instruction> nodes, int parameters, int start)
    {
        if (nodes[start] is not Addition { Overflow: Overflow.Checked } first)
            return 0;

        var end = start + 1;
        while (end < nodes.Count &&
               nodes[end] is Addition { Overflow: Overflow.Checked } next &&
               next.Left == parameters + end - 1 && next.Right == first.Right)
            end++;
        return end - start;
    }

    private static byte[] Rewrite(byte[] source, ReadOnlySpan<byte> inputHeader, ReadOnlySpan<byte> outputHeader,
        Action<BinaryReader, BinaryWriter, int, int, int> rewriteBody)
    {
        try
        {
            using var input = new MemoryStream(source, writable: false);
            using var output = new MemoryStream();
            using var reader = new BinaryReader(input);
            using var writer = new BinaryWriter(output);
            Require(reader.ReadBytes(inputHeader.Length).AsSpan().SequenceEqual(inputHeader), "Invalid WLP header.");
            writer.Write(outputHeader);
            var contract = reader.ReadByte();
            Require(contract is 0 or 1, "Invalid postcondition discriminator.");
            writer.Write(contract);
            if (contract == 1)
                writer.Write(reader.ReadByte());

            var functions = ReadNumber(reader, MaxFunctions);
            Require(functions > 0, "A graph must contain an entry function.");
            writer.Write7BitEncodedInt(functions);
            for (var function = 0; function < functions; function++)
            {
                writer.Write(reader.ReadByte());
                var parameters = ReadNumber(reader, MaxParameters);
                writer.Write7BitEncodedInt(parameters);
                for (var parameter = 0; parameter < parameters; parameter++)
                    writer.Write(reader.ReadByte());

                var count = ReadNumber(reader, MaxNodes);
                Require(count > 0, "A function must contain an instruction.");
                writer.Write7BitEncodedInt(count);
                rewriteBody(reader, writer, function, parameters, count);
            }
            Require(input.Position == input.Length, "Trailing WLP bytes.");
            return output.ToArray();
        }
        catch (Exception exception) when (exception is EndOfStreamException or FormatException)
        {
            throw new InvalidDataException("Invalid or truncated WLP field.", exception);
        }
    }

    private static void CopyOrdinaryRecord(BinaryReader reader, BinaryWriter writer, byte[] source)
    {
        var start = (int)reader.BaseStream.Position;
        CopyOrdinaryRecord(reader, writer, source, start, (Opcode)reader.ReadByte());
    }

    private static void CopyOrdinaryRecord(BinaryReader reader, BinaryWriter writer, byte[] source, int start, Opcode opcode)
    {
        SkipOrdinaryRecord(reader, opcode);
        writer.Write(source.AsSpan(start, (int)reader.BaseStream.Position - start));
    }

    private static void SkipOrdinaryRecord(BinaryReader reader, Opcode opcode)
    {
        // Packed v1 Add has an overflow byte at the position occupied by the result tag on other opcodes.
        var tag = reader.ReadByte();
        switch (opcode)
        {
            case Opcode.Literal:
                var length = (ValueKind)tag switch
                {
                    ValueKind.Int32 => 4,
                    ValueKind.Boolean => 1,
                    ValueKind.Text => ReadNumber(reader, 65_535),
                    _ => throw new InvalidDataException("Invalid literal type.")
                };
                var stream = reader.BaseStream;
                Require(length <= stream.Length - stream.Position, "Truncated literal.");
                stream.Position += length;
                break;
            case Opcode.Add:
            case Opcode.Load:
                SkipNumbers(reader, 2);
                break;
            case Opcode.Branch:
            case Opcode.Store:
                SkipNumbers(reader, 3);
                break;
            case Opcode.Call:
                ReadNumber(reader, MaxFunctions - 1);
                SkipNumbers(reader, ReadNumber(reader, MaxParameters));
                break;
            case Opcode.WriteLine:
            case Opcode.Return:
                ReadNumber(reader);
                break;
            default:
                throw new InvalidDataException("Unknown packed instruction.");
        }
    }

    private static void SkipNumbers(BinaryReader reader, int count)
    {
        for (var field = 0; field < count; field++)
            ReadNumber(reader);
    }

    private static int ReadNumber(BinaryReader reader, int maximum = MaxNodes + MaxParameters)
    {
        var start = reader.BaseStream.Position;
        var value = reader.Read7BitEncodedInt();
        var shift = 7 * ((int)(reader.BaseStream.Position - start) - 1);
        Require(value >= 0 && value <= maximum && (shift == 0 || value >> shift != 0),
            "Invalid packed field width or range.");
        return value;
    }
}
