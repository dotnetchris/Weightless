using System.Text;

namespace Weightless;

internal sealed record Effect(string Kind, string? Text = null, int? Buffer = null, int? Index = null, int? Value = null);
internal sealed record Observation(object? Value, string? ExceptionType, string Output, Effect[] Effects, int[][] Buffers)
{
    internal bool Matches(Observation other) =>
        EqualValue(Value, other.Value) && ExceptionType == other.ExceptionType && Output == other.Output &&
        Effects.SequenceEqual(other.Effects) && Buffers.Length == other.Buffers.Length &&
        Buffers.Zip(other.Buffers).All(pair => pair.First.SequenceEqual(pair.Second));

    private static bool EqualValue(object? left, object? right) =>
        left is int[] first && right is int[] second ? first.SequenceEqual(second) : Equals(left, right);
}

internal sealed class PostconditionViolationException : Exception;

internal static class Interpreter
{
    internal static Observation Observe(Graph graph, object[] arguments)
    {
        var parameters = graph.Functions[0].Parameters;
        if (arguments.Length != parameters.Length ||
            !parameters.Zip(arguments).All(pair => GraphValidation.IsValue(pair.First, pair.Second)))
            throw new ArgumentException("Arguments do not match the entry signature.", nameof(arguments));

        var context = new Execution(arguments);
        object? value = null;
        string? exceptionType = null;
        try
        {
            var result = context.Invoke(graph, 0, arguments);
            if (graph.Contract == Postcondition.NonNegativeInt32 && (int)result! < 0)
                throw new PostconditionViolationException();
            value = result is int[] buffer ? buffer.ToArray() : result;
        }
        catch (Exception exception) when (exception is OverflowException or IndexOutOfRangeException
                                          or PostconditionViolationException)
        {
            exceptionType = exception.GetType().Name;
        }

        return new Observation(value, exceptionType, context.Output.ToString(), context.Effects.ToArray(),
            context.Buffers.Select(buffer => buffer.ToArray()).ToArray());
    }

    private sealed class Execution(object[] entryArguments)
    {
        internal readonly List<Effect> Effects = [];
        internal readonly StringBuilder Output = new();
        internal readonly int[][] Buffers = entryArguments.OfType<int[]>()
            .Distinct<int[]>(ReferenceEqualityComparer.Instance).ToArray();

        internal object? Invoke(Graph graph, int functionIndex, object[] arguments)
        {
            var function = graph.Functions[functionIndex];
            var slots = new object?[arguments.Length + function.Body.Length];
            arguments.CopyTo(slots, 0);
            for (var index = 0; ; index++)
            {
                var slot = arguments.Length + index;
                switch (function.Body[index])
                {
                    case Literal literal:
                        slots[slot] = literal.Value;
                        break;
                    case Addition addition:
                        var left = (int)slots[addition.Left]!;
                        var right = (int)slots[addition.Right]!;
                        slots[slot] = addition.Overflow == Overflow.Checked
                            ? checked(left + right) : unchecked(left + right);
                        break;
                    case Branch branch:
                        index = ((bool)slots[branch.Condition]! ? branch.TrueTarget : branch.FalseTarget) - 1;
                        break;
                    case Call call:
                        slots[slot] = Invoke(graph, call.Function,
                            call.Arguments.Select(reference => slots[reference]!).ToArray());
                        break;
                    case Load load:
                        slots[slot] = ((int[])slots[load.Buffer]!)[(int)slots[load.Index]!];
                        break;
                    case Store store:
                        var buffer = (int[])slots[store.Buffer]!;
                        var destination = (int)slots[store.Index]!;
                        var value = (int)slots[store.Value]!;
                        buffer[destination] = value;
                        Effects.Add(new Effect("buffer-write", Buffer: Array.IndexOf(Buffers, buffer),
                            Index: destination, Value: value));
                        break;
                    case WriteLine write:
                        var line = (string)slots[write.Text]! + Environment.NewLine;
                        Output.Append(line);
                        Effects.Add(new Effect("stdout-write", Text: line));
                        break;
                    case Return ret:
                        return ret.Value is int reference ? slots[reference] : null;
                }
            }
        }
    }
}
