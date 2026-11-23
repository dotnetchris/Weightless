using System.Collections.Immutable;
using System.Text;
using static Weightless.GraphValidation;

namespace Weightless;

internal enum EncodingKind { Baseline, Packed }

internal static class GraphCodec
{
    private enum Opcode : byte { Literal = 1, Add, Branch, Call, Load, Store, WriteLine, Return }

    private const int MaxTextBytes = 65_535;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    internal static byte[] Encode(Graph graph, EncodingKind encoding)
    {
        Validate(graph);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Utf8);
        writer.Write(Header(encoding));
        switch (graph.Contract)
        {
            case Postcondition.None:
                writer.Write((byte)0);
                break;
            case Postcondition.NonNegativeInt32:
                writer.Write((byte)1);
                writer.Write((byte)1);
                break;
        }

        writer.Write7BitEncodedInt(graph.Functions.Length);
        foreach (var function in graph.Functions)
        {
            writer.Write((byte)function.ReturnType);
            writer.Write7BitEncodedInt(function.Parameters.Length);
            foreach (var parameter in function.Parameters)
                writer.Write((byte)parameter);

            writer.Write7BitEncodedInt(function.Body.Length);
            foreach (var instruction in function.Body)
                WriteInstruction(writer, instruction, encoding);
        }

        return stream.ToArray();
    }

    internal static Graph Decode(byte[] bytes, EncodingKind encoding)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new BinaryReader(stream, Utf8);
            Require(reader.ReadBytes(4).AsSpan().SequenceEqual(Header(encoding)), "Invalid codec header.");
            var contract = reader.ReadByte() switch
            {
                0 => Postcondition.None,
                1 when reader.ReadByte() == 1 => Postcondition.NonNegativeInt32,
                _ => throw new InvalidDataException("Invalid postcondition discriminator or predicate.")
            };

            var functions = ImmutableArray.CreateBuilder<Function>(ReadNumber(reader, MaxFunctions));
            while (functions.Count < functions.Capacity)
            {
                var returnType = ReadType(reader);
                var parameters = ImmutableArray.CreateBuilder<ValueKind>(ReadNumber(reader, MaxParameters));
                while (parameters.Count < parameters.Capacity)
                    parameters.Add(ReadType(reader));

                var signature = parameters.MoveToImmutable();
                var body = ImmutableArray.CreateBuilder<Instruction>(ReadNumber(reader, MaxNodes));
                while (body.Count < body.Capacity)
                    body.Add(ReadInstruction(reader, encoding, signature, body));
                functions.Add(new Function(returnType, signature, body.MoveToImmutable()));
            }

            Require(stream.Position == stream.Length, "Trailing bytes.");
            var graph = new Graph(contract, functions.MoveToImmutable());
            Validate(graph);
            return graph;
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Truncated graph encoding.", exception);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("Invalid UTF-8 literal.", exception);
        }
    }

    private static ReadOnlySpan<byte> Header(EncodingKind encoding) => encoding switch
    {
        EncodingKind.Baseline => "WLB\x01"u8,
        EncodingKind.Packed => "WLP\x01"u8,
        _ => throw new ArgumentOutOfRangeException(nameof(encoding))
    };

    private static void WriteInstruction(BinaryWriter writer, Instruction instruction, EncodingKind encoding)
    {
        var opcode = instruction switch
        {
            Literal => Opcode.Literal,
            Addition => Opcode.Add,
            Branch => Opcode.Branch,
            Call => Opcode.Call,
            Load => Opcode.Load,
            Store => Opcode.Store,
            WriteLine => Opcode.WriteLine,
            Return => Opcode.Return,
            _ => throw new InvalidDataException("Unknown instruction.")
        };

        writer.Write((byte)opcode);
        if (encoding == EncodingKind.Baseline || opcode != Opcode.Add)
            writer.Write((byte)instruction.Result);

        switch (instruction)
        {
            case Literal literal:
                WriteLiteral(writer, literal);
                break;
            case Addition addition:
                writer.Write((byte)addition.Overflow);
                writer.Write7BitEncodedInt(addition.Left);
                writer.Write7BitEncodedInt(addition.Right);
                break;
            case Branch branch:
                writer.Write7BitEncodedInt(branch.Condition);
                writer.Write7BitEncodedInt(branch.TrueTarget);
                writer.Write7BitEncodedInt(branch.FalseTarget);
                break;
            case Call call:
                writer.Write7BitEncodedInt(call.Function);
                writer.Write7BitEncodedInt(call.Arguments.Length);
                foreach (var argument in call.Arguments)
                    writer.Write7BitEncodedInt(argument);
                break;
            case Load load:
                writer.Write7BitEncodedInt(load.Buffer);
                writer.Write7BitEncodedInt(load.Index);
                break;
            case Store store:
                writer.Write7BitEncodedInt(store.Buffer);
                writer.Write7BitEncodedInt(store.Index);
                writer.Write7BitEncodedInt(store.Value);
                break;
            case WriteLine write:
                writer.Write7BitEncodedInt(write.Text);
                break;
            case Return ret:
                writer.Write7BitEncodedInt(ret.Value is int value ? value + 1 : 0);
                break;
        }
    }

    private static Instruction ReadInstruction(BinaryReader reader, EncodingKind encoding,
        ImmutableArray<ValueKind> parameters, IReadOnlyList<Instruction> body)
    {
        var opcode = (Opcode)reader.ReadByte();
        if (encoding == EncodingKind.Packed && opcode == Opcode.Add)
        {
            var overflow = (Overflow)reader.ReadByte();
            var left = ReadNumber(reader);
            var right = ReadNumber(reader);
            var leftType = SlotType(parameters, body, left);
            var rightType = SlotType(parameters, body, right);
            Require(leftType == ValueKind.Int32 && rightType == ValueKind.Int32,
                "Cannot infer a packed addition result.");
            return new Addition(leftType, overflow, left, right);
        }

        var kind = ReadType(reader);
        Instruction instruction = opcode switch
        {
            Opcode.Literal => new Literal(kind, ReadLiteral(reader, kind)),
            Opcode.Add => new Addition(kind, (Overflow)reader.ReadByte(), ReadNumber(reader), ReadNumber(reader)),
            Opcode.Branch => new Branch(ReadNumber(reader), ReadNumber(reader), ReadNumber(reader)),
            Opcode.Call => new Call(kind, ReadNumber(reader), ReadArguments(reader)),
            Opcode.Load => new Load(ReadNumber(reader), ReadNumber(reader)),
            Opcode.Store => new Store(ReadNumber(reader), ReadNumber(reader), ReadNumber(reader)),
            Opcode.WriteLine => new WriteLine(ReadNumber(reader)),
            Opcode.Return => ReadReturn(reader),
            _ => throw new InvalidDataException("Unknown opcode.")
        };
        Require(instruction.Result == kind, "Invalid instruction result type.");
        return instruction;
    }

    private static ValueKind ReadType(BinaryReader reader)
    {
        var kind = (ValueKind)reader.ReadByte();
        Require(Enum.IsDefined(kind), "Unknown type tag.");
        return kind;
    }

    private static Return ReadReturn(BinaryReader reader)
    {
        var reference = ReadNumber(reader);
        return new Return(reference == 0 ? null : reference - 1);
    }

    private static ImmutableArray<int> ReadArguments(BinaryReader reader)
    {
        var arguments = ImmutableArray.CreateBuilder<int>(ReadNumber(reader, MaxParameters));
        while (arguments.Count < arguments.Capacity)
            arguments.Add(ReadNumber(reader));
        return arguments.MoveToImmutable();
    }

    private static int ReadNumber(BinaryReader reader, int maximum = MaxParameters + MaxNodes)
    {
        var start = reader.BaseStream.Position;
        int value;
        try
        {
            value = reader.Read7BitEncodedInt();
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Invalid variable-length integer.", exception);
        }

        var length = reader.BaseStream.Position - start;
        Require(value >= 0 && value <= maximum && (length == 1 || value >> (7 * ((int)length - 1)) != 0),
            "Out-of-range or noncanonical variable-length integer.");
        return value;
    }

    private static void WriteLiteral(BinaryWriter writer, Literal literal)
    {
        switch (literal.Value)
        {
            case int number:
                writer.Write(number);
                break;
            case bool flag:
                writer.Write(flag);
                break;
            case string text:
                var bytes = Utf8.GetBytes(text);
                Require(bytes.Length <= MaxTextBytes, "Text literal is too large.");
                writer.Write7BitEncodedInt(bytes.Length);
                writer.Write(bytes);
                break;
        }
    }

    private static object ReadLiteral(BinaryReader reader, ValueKind kind) => kind switch
    {
        ValueKind.Int32 => reader.ReadInt32(),
        ValueKind.Boolean => reader.ReadByte() switch
        {
            0 => false,
            1 => true,
            _ => throw new InvalidDataException("Invalid Boolean literal.")
        },
        ValueKind.Text => ReadText(reader),
        _ => throw new InvalidDataException("Invalid literal type.")
    };

    private static string ReadText(BinaryReader reader)
    {
        var length = ReadNumber(reader, MaxTextBytes);
        var bytes = reader.ReadBytes(length);
        Require(bytes.Length == length, "Truncated text literal.");
        return Utf8.GetString(bytes);
    }
}
