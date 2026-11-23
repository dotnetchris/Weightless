using System.Collections.Immutable;

namespace Weightless;

internal enum ValueKind : byte { Void, Int32, Boolean, Text, Int32Buffer }
internal enum Overflow : byte { Wrapping, Checked }
internal enum Postcondition : byte { None, NonNegativeInt32 }

internal abstract record Instruction(ValueKind Result);
internal sealed record Literal(ValueKind Result, object Value) : Instruction(Result);
internal sealed record Addition(ValueKind Result, Overflow Overflow, int Left, int Right) : Instruction(Result);
internal sealed record Branch(int Condition, int TrueTarget, int FalseTarget) : Instruction(ValueKind.Void);
internal sealed record Call(ValueKind Result, int Function, ImmutableArray<int> Arguments) : Instruction(Result);
internal sealed record Load(int Buffer, int Index) : Instruction(ValueKind.Int32);
internal sealed record Store(int Buffer, int Index, int Value) : Instruction(ValueKind.Void);
internal sealed record WriteLine(int Text) : Instruction(ValueKind.Void);
internal sealed record Return(int? Value) : Instruction(ValueKind.Void);

internal sealed record Function(ValueKind ReturnType, ImmutableArray<ValueKind> Parameters,
    ImmutableArray<Instruction> Body);

internal sealed record Graph(Postcondition Contract, ImmutableArray<Function> Functions)
{
    // Array identity is deliberately excluded from canonical graph equality.
    internal bool Matches(Graph other)
    {
        if (Contract != other.Contract || Functions.Length != other.Functions.Length)
            return false;

        return Functions.Zip(other.Functions).All(pair =>
            pair.First.ReturnType == pair.Second.ReturnType &&
            pair.First.Parameters.SequenceEqual(pair.Second.Parameters) &&
            pair.First.Body.Length == pair.Second.Body.Length &&
            pair.First.Body.Zip(pair.Second.Body).All(nodes => (nodes.First, nodes.Second) switch
            {
                (Call left, Call right) => left.Result == right.Result &&
                    left.Function == right.Function && left.Arguments.SequenceEqual(right.Arguments),
                _ => nodes.First == nodes.Second
            }));
    }

    internal int NodeCount => Functions.Sum(function => function.Body.Length);
}

internal static class GraphValidation
{
    internal const int MaxFunctions = 64;
    internal const int MaxParameters = 32;
    internal const int MaxNodes = 4096;

    internal static void Validate(Graph graph)
    {
        Require(graph.Functions.Length is > 0 and <= MaxFunctions, "Invalid function count.");
        Require(graph.Contract is Postcondition.None ||
            graph.Contract is Postcondition.NonNegativeInt32 && graph.Functions[0].ReturnType is ValueKind.Int32,
            "Invalid entry postcondition.");

        foreach (var function in graph.Functions)
        {
            Require(Enum.IsDefined(function.ReturnType), "Invalid function return type.");
            Require(function.Parameters.Length <= MaxParameters, "Too many parameters.");
            Require(function.Parameters.All(kind => Enum.IsDefined(kind) && kind != ValueKind.Void),
                "Invalid parameter type.");
            Require(function.Body.Length is > 0 and <= MaxNodes, "Invalid instruction count.");
        }

        for (var index = 0; index < graph.Functions.Length; index++)
            ValidateFunction(graph, index);
    }

    private static void ValidateFunction(Graph graph, int functionIndex)
    {
        var function = graph.Functions[functionIndex];
        var pending = new HashSet<int>?[function.Body.Length];
        HashSet<int>? available = new(Enumerable.Range(0, function.Parameters.Length));

        for (var index = 0; index < function.Body.Length; index++)
        {
            if (pending[index] is { } incoming)
            {
                if (available is null)
                    available = incoming;
                else
                    available.IntersectWith(incoming);
                pending[index] = null;
            }

            var live = available ?? throw new InvalidDataException("Unreachable instruction.");
            var instruction = function.Body[index];

            void Operand(int reference, ValueKind kind)
            {
                Require(live.Contains(reference), "Operand is unavailable on an incoming path.");
                Require(SlotType(function.Parameters, function.Body, reference) == kind,
                    "Operand type does not match its use.");
            }

            void Send(int target)
            {
                Require(target > index && target < function.Body.Length, "Invalid forward branch target.");
                if (pending[target] is { } previous)
                    previous.IntersectWith(live);
                else
                    pending[target] = new HashSet<int>(live);
            }

            switch (instruction)
            {
                case Literal literal:
                    Require(IsValue(literal.Result, literal.Value) && literal.Result != ValueKind.Int32Buffer,
                        "Invalid literal.");
                    break;
                case Addition addition:
                    Operand(addition.Left, ValueKind.Int32);
                    Operand(addition.Right, ValueKind.Int32);
                    Require(addition.Result == ValueKind.Int32 && Enum.IsDefined(addition.Overflow),
                        "Invalid addition result or overflow policy.");
                    break;
                case Branch branch:
                    Operand(branch.Condition, ValueKind.Boolean);
                    Send(branch.TrueTarget);
                    Send(branch.FalseTarget);
                    available = null;
                    break;
                case Call call:
                    Require(call.Function > functionIndex && call.Function < graph.Functions.Length,
                        "Calls must target a later function.");
                    var callee = graph.Functions[call.Function];
                    Require(callee.ReturnType == call.Result && callee.Parameters.Length == call.Arguments.Length,
                        "Call signature does not match its target.");
                    for (var parameter = 0; parameter < call.Arguments.Length; parameter++)
                        Operand(call.Arguments[parameter], callee.Parameters[parameter]);
                    break;
                case Load load:
                    Operand(load.Buffer, ValueKind.Int32Buffer);
                    Operand(load.Index, ValueKind.Int32);
                    break;
                case Store store:
                    Operand(store.Buffer, ValueKind.Int32Buffer);
                    Operand(store.Index, ValueKind.Int32);
                    Operand(store.Value, ValueKind.Int32);
                    break;
                case WriteLine write:
                    Operand(write.Text, ValueKind.Text);
                    break;
                case Return { Value: int value }:
                    Require(function.ReturnType != ValueKind.Void, "Void functions cannot return a value.");
                    Operand(value, function.ReturnType);
                    available = null;
                    break;
                case Return:
                    Require(function.ReturnType == ValueKind.Void, "A return value is required.");
                    available = null;
                    break;
                default:
                    throw new InvalidDataException("Unknown instruction.");
            }

            if (instruction.Result != ValueKind.Void)
                live.Add(function.Parameters.Length + index);
        }

        Require(available is null, "Control can fall through the end of a function.");
    }

    internal static ValueKind SlotType(ImmutableArray<ValueKind> parameters, IReadOnlyList<Instruction> body, int slot)
    {
        Require(slot >= 0 && slot < parameters.Length + body.Count, "Invalid operand reference.");
        return slot < parameters.Length ? parameters[slot] : body[slot - parameters.Length].Result;
    }

    internal static bool IsValue(ValueKind kind, object? value) => (kind, value) switch
    {
        (ValueKind.Int32, int) or (ValueKind.Boolean, bool) or (ValueKind.Text, string) or
            (ValueKind.Int32Buffer, int[]) => true,
        _ => false
    };

    internal static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
