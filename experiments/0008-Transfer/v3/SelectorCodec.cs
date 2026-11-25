using Weightless;
using static Weightless.GraphValidation;

internal sealed record ShiftFitsInt32(int Argument, int Shift)
{
    internal bool Test(int[] values) => (long)values[Argument] + Shift is >= int.MinValue and <= int.MaxValue;
}

internal sealed record SelectorBranch(int IfBothFit, int Otherwise);
internal sealed record Selection(int SelectedFunction, Observation Outcome);

internal sealed record SelectorProgram(ShiftFitsInt32 Left, ShiftFitsInt32 Right, SelectorBranch Branch, Graph Arms)
{
    internal Selection Execute(int a, int b)
    {
        int[] values = [a, b];
        var leftFits = Left.Test(values);
        var rightFits = Right.Test(values);
        var target = leftFits && rightFits ? Branch.IfBothFit : Branch.Otherwise;
        var selected = new Graph(Postcondition.None, [Arms.Functions[target]]);
        return new Selection(target, Interpreter.Observe(selected, [a, b]));
    }

    internal bool Matches(SelectorProgram other) =>
        Left == other.Left && Right == other.Right && Branch == other.Branch && Arms.Matches(other.Arms);
}

internal static class SelectorCodec
{
    private const byte FitsShift = 1;
    private const byte BranchOnBoth = 2;
    private const int LastArgument = 1;
    private const int LastArm = 1;

    internal static (byte[] Bytes, int ArmBytes) Encode(SelectorProgram program, EncodingKind codec)
    {
        Validate(program);
        var arms = GraphCodec.Encode(program.Arms, codec);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Header(codec));
        WriteTest(writer, program.Left);
        WriteTest(writer, program.Right);
        writer.Write(BranchOnBoth);
        writer.Write7BitEncodedInt(program.Branch.IfBothFit);
        writer.Write7BitEncodedInt(program.Branch.Otherwise);
        writer.Write7BitEncodedInt(arms.Length);
        writer.Write(arms);
        return (stream.ToArray(), arms.Length);
    }

    internal static SelectorProgram Decode(byte[] bytes, EncodingKind codec)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new BinaryReader(stream);
            var header = Header(codec);
            Require(reader.ReadBytes(header.Length).AsSpan().SequenceEqual(header), "Invalid selector header.");
            var left = ReadTest(reader);
            var right = ReadTest(reader);
            Require(reader.ReadByte() == BranchOnBoth, "Expected the selector branch opcode.");
            var branch = new SelectorBranch(ReadNumber(reader, LastArm), ReadNumber(reader, LastArm));
            var armLength = ReadNumber(reader, bytes.Length);
            Require(armLength == stream.Length - stream.Position, "Incorrect selector arm length.");
            var arms = GraphCodec.Decode(reader.ReadBytes(armLength), codec);
            var program = new SelectorProgram(left, right, branch, arms);
            Validate(program);
            return program;
        }
        catch (Exception exception) when (exception is EndOfStreamException or FormatException)
        {
            throw new InvalidDataException("Invalid or truncated selector field.", exception);
        }
    }

    private static ReadOnlySpan<byte> Header(EncodingKind codec) => codec switch
    {
        EncodingKind.Baseline => "WSB\x01"u8,
        EncodingKind.Packed => "WSP\x01"u8,
        _ => throw new ArgumentOutOfRangeException(nameof(codec))
    };

    private static void Validate(SelectorProgram program)
    {
        Require(program.Left.Argument is >= 0 and <= LastArgument && program.Right.Argument is >= 0 and <= LastArgument,
            "Each shift test must read a typed entry argument.");
        Require(program.Branch.IfBothFit is >= 0 and <= LastArm && program.Branch.Otherwise is >= 0 and <= LastArm,
            "The branch must target an existing arm.");
        Require(program.Arms.Contract == Postcondition.None && program.Arms.Functions.Length == LastArm + 1 &&
            program.Arms.Functions.All(arm => arm.ReturnType == ValueKind.Int32 &&
                arm.Parameters.SequenceEqual([ValueKind.Int32, ValueKind.Int32]) &&
                arm.Body.All(node => node is Addition or Literal { Result: ValueKind.Int32 } or Return)),
            "Both selector arms must be pure (Int32, Int32) -> Int32 functions of literals, Adds and Returns.");
    }

    private static void WriteTest(BinaryWriter writer, ShiftFitsInt32 test)
    {
        writer.Write(FitsShift);
        writer.Write7BitEncodedInt(test.Argument);
        writer.Write(test.Shift);
    }

    private static ShiftFitsInt32 ReadTest(BinaryReader reader)
    {
        Require(reader.ReadByte() == FitsShift, "Expected a typed Int32 shift test opcode.");
        return new ShiftFitsInt32(ReadNumber(reader, LastArgument), reader.ReadInt32());
    }

    private static int ReadNumber(BinaryReader reader, int maximum)
    {
        var start = reader.BaseStream.Position;
        var value = reader.Read7BitEncodedInt();
        var shift = 7 * ((int)(reader.BaseStream.Position - start) - 1);
        Require(value >= 0 && value <= maximum && (shift == 0 || value >> shift != 0),
            "Invalid selector field width or range.");
        return value;
    }
}
