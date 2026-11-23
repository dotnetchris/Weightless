using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Weightless;

internal sealed record Corpus(Fixture[] Fixtures, object Provenance);

internal static class CSharpSeeds
{
    private const string AddSource = "experiments/0001-Addition/AddSeed.cs";
    private const string AddRecord = "experiments/0001-Addition/checked-transfer-v1.json";
    private const string HelloSource = "experiments/0002-HelloWorld/HelloWorld.cs";
    private const string BufferSource = "experiments/0003-Buffer/BufferSeed.cs";
    private const string RepeatedSource = "experiments/0004-TypedContext/RepeatedAddition.cs";

    internal static Corpus Load()
    {
        string[] paths = [AddSource, HelloSource, BufferSource, RepeatedSource];
        var files = paths.ToDictionary(path => path, File.ReadAllBytes);
        var trees = files.ToDictionary(pair => pair.Key, pair =>
        {
            using var source = new MemoryStream(pair.Value, writable: false);
            return CSharpSyntaxTree.ParseText(SourceText.From(source, System.Text.Encoding.UTF8), path: pair.Key);
        });
        var platform = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var references = platform.Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("Weightless.Oracle",
            trees.Values.Append(CSharpSyntaxTree.ParseText("global using System;")),
            references, new CSharpCompilationOptions(OutputKind.ConsoleApplication,
                optimizationLevel: OptimizationLevel.Release, nullableContextOptions: NullableContextOptions.Enable));

        using var image = new MemoryStream();
        var emit = compilation.Emit(image);
        if (!emit.Success)
            throw new InvalidDataException(string.Join(Environment.NewLine, emit.Diagnostics));
        image.Position = 0;
        var oracle = AssemblyLoadContext.Default.LoadFromStream(image);

        var (checkedAdd, checkedBinding) = ImportAdd(compilation, trees[AddSource], "Checked");
        var (wrappingAdd, wrappingBinding) = ImportAdd(compilation, trees[AddSource], "Wrapping");
        var (hello, helloBinding) = ImportHello(compilation, trees[HelloSource]);
        var checkedMethod = Method<Func<int, int, int>>(oracle, "AddSeed", "Checked");
        var wrappingMethod = Method<Func<int, int, int>>(oracle, "AddSeed", "Wrapping");
        var constrainedMethod = Method<Func<int, int, int>>(oracle, "AddSeed", "Constrained");
        var repeated = Method<Func<int, int, int>>(oracle, "RepeatedAddition", "Run");
        var buffer = Method<Func<int[], int, int, bool, int, Action<int, int>, int>>(oracle, "BufferSeed", "Run");

        var recorded = AdditionReplay.Load(AddRecord);
        foreach (var row in recorded)
        {
            Require(Observe(() => checkedMethod(row.A, row.B)).Matches(
                new Observation(row.Checked.Value, row.Checked.ExceptionType, "", [], [])),
                $"Checked C# seed disagrees with the starting experiment on {row.Name}.");
            Require(Observe(() => wrappingMethod(row.A, row.B)).Matches(
                new Observation(row.Wrapping.Value, row.Wrapping.ExceptionType, "", [], [])),
                $"Wrapping C# seed disagrees with the starting experiment on {row.Name}.");
        }

        InputCase[] addCases =
        [
            .. recorded.Select(row => new InputCase(row.Name, [row.A, row.B])),
            new("negative-result", [-1, 0]),
            new("zero-result", [-1, 1])
        ];
        InputCase[] bufferCases =
        [
            BufferCase("store-and-load", 1, 1, true, 98),
            BufferCase("skip-invalid-store", 99, 1, false, 98),
            BufferCase("load-fails-after-store", 1, 3, true, 98),
            BufferCase("store-fails", -1, 1, true, 98),
            BufferCase("addition-overflows-before-store", 1, 1, true, int.MaxValue),
            BufferCase("load-fails-without-store", 1, -1, false, 98)
        ];
        var fixtures = new Fixture[]
        {
            new("add-checked", checkedAdd, addCases, args => Add(checkedMethod, args)),
            new("add-wrapping", wrappingAdd, addCases, args => Add(wrappingMethod, args)),
            new("add-constrained", checkedAdd with { Contract = Postcondition.NonNegativeInt32 },
                addCases, args => Add(constrainedMethod, args)),
            new("hello-world", hello, [new("top-level-statement", [])], _ => ObserveHello(oracle.EntryPoint!)),
            new("buffer-call", Fixtures.BufferedCall(), bufferCases, args => ObserveBuffer(buffer, args)),
            new("repeated-addition", Fixtures.RepeatedAddition(), addCases, args => Add(repeated, args))
        };

        return new Corpus(fixtures, new
        {
            compiler = typeof(CSharpCompilation).Assembly.GetName().Version?.ToString(),
            sources = paths.Append(AddRecord).Select(path => new
            {
                path,
                sha256 = Convert.ToHexString(SHA256.HashData(files.TryGetValue(path, out var bytes)
                    ? bytes : File.ReadAllBytes(path)))
            }).ToArray(),
            imported = new[] { checkedBinding, wrappingBinding, helloBinding },
            handBuilt = new[]
            {
                new { fixture = "buffer-call", source = BufferSource },
                new { fixture = "repeated-addition", source = RepeatedSource }
            },
            contract = new { fixture = "add-constrained", reference = "AddSeed.Constrained", source = AddSource }
        });
    }

    private static (Graph Graph, object Binding) ImportAdd(CSharpCompilation compilation, SyntaxTree tree, string name)
    {
        var declaration = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == name);
        var syntax = declaration.ExpressionBody?.Expression switch
        {
            CheckedExpressionSyntax { Expression: BinaryExpressionSyntax binary } => binary,
            BinaryExpressionSyntax binary => binary,
            _ => throw new InvalidDataException("Add import expects an expression-bodied binary operation.")
        };
        var model = compilation.GetSemanticModel(tree);
        if (model.GetOperation(syntax) is not IBinaryOperation
            {
                OperatorKind: BinaryOperatorKind.Add,
                OperatorMethod: null,
                Type.SpecialType: SpecialType.System_Int32,
                LeftOperand: IParameterReferenceOperation { Type.SpecialType: SpecialType.System_Int32 } left,
                RightOperand: IParameterReferenceOperation { Type.SpecialType: SpecialType.System_Int32 } right
            } operation ||
            model.GetDeclaredSymbol(declaration) is not { ReturnType.SpecialType: SpecialType.System_Int32 } method ||
            method.Parameters.Length != 2 || method.Parameters.Any(p => p.Type.SpecialType != SpecialType.System_Int32))
            throw new InvalidDataException("Add import expects built-in Int32 addition of two Int32 parameters.");

        var graph = new Graph(Postcondition.None, [new Function(ValueKind.Int32, [ValueKind.Int32, ValueKind.Int32],
            [new Addition(ValueKind.Int32, operation.IsChecked ? Overflow.Checked : Overflow.Wrapping,
                left.Parameter.Ordinal, right.Parameter.Ordinal), new Return(2)])]);
        return (graph, new
        {
            source = tree.FilePath, line = Line(syntax), member = method.ToDisplayString(),
            operation = operation.Kind.ToString(), @operator = operation.OperatorKind.ToString(),
            resultType = operation.Type!.ToDisplayString(), isChecked = operation.IsChecked,
            operands = new[]
            {
                new { left.Parameter.Name, left.Parameter.Ordinal, type = left.Type!.ToDisplayString(),
                    conversion = model.GetConversion(syntax.Left).ToString() },
                new { right.Parameter.Name, right.Parameter.Ordinal, type = right.Type!.ToDisplayString(),
                    conversion = model.GetConversion(syntax.Right).ToString() }
            }
        });
    }

    private static (Graph Graph, object Binding) ImportHello(CSharpCompilation compilation, SyntaxTree tree)
    {
        var statement = tree.GetCompilationUnitRoot().Members.OfType<GlobalStatementSyntax>().Single().Statement;
        var model = compilation.GetSemanticModel(tree);
        if (model.GetOperation(statement) is not IExpressionStatementOperation
            {
                Operation: IInvocationOperation { Arguments.Length: 1 } invocation
            } || invocation.TargetMethod is not { IsStatic: true, ReturnsVoid: true, Name: "WriteLine" } target ||
            target.ContainingType.ToDisplayString() != "System.Console" ||
            invocation.Arguments[0] is not
            {
                Parameter.Type.SpecialType: SpecialType.System_String,
                InConversion.IsIdentity: true,
                Value: ILiteralOperation { ConstantValue: { HasValue: true, Value: string text } }
            } argument)
            throw new InvalidDataException("Hello import expects a bound Console.WriteLine(string) literal.");

        var graph = new Graph(Postcondition.None, [new Function(ValueKind.Void, [],
            [new Literal(ValueKind.Text, text), new WriteLine(0), new Return(null)])]);
        return (graph, new
        {
            source = tree.FilePath, line = Line(statement), operation = invocation.Kind.ToString(),
            callTarget = target.ToDisplayString(), returnType = target.ReturnType.ToDisplayString(),
            argumentType = argument.Parameter!.Type.ToDisplayString(),
            identityConversion = argument.InConversion.IsIdentity
        });
    }

    private static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static T Method<T>(Assembly assembly, string type, string method) where T : Delegate =>
        assembly.GetType(type, throwOnError: true)!.GetMethod(method)!.CreateDelegate<T>();

    private static Observation Add(Func<int, int, int> method, object[] args) =>
        Observe(() => method((int)args[0], (int)args[1]));

    private static InputCase BufferCase(string name, int store, int load, bool apply, int value) =>
        new(name, [new[] { 10, 20, 30 }, store, load, apply, value]);

    private static Observation ObserveBuffer(Func<int[], int, int, bool, int, Action<int, int>, int> method,
        object[] args)
    {
        var memory = (int[])args[0];
        List<Effect> effects = [];
        var observation = Observe(() => method(memory, (int)args[1], (int)args[2], (bool)args[3], (int)args[4],
            (index, value) => effects.Add(new Effect("buffer-write", Buffer: 0, Index: index, Value: value))));
        return observation with { Effects = effects.ToArray(), Buffers = [memory.ToArray()] };
    }

    private static Observation Observe(Func<int> run)
    {
        try
        {
            return new Observation(run(), null, "", [], []);
        }
        catch (Exception exception) when (exception is OverflowException or IndexOutOfRangeException ||
                                          exception.GetType().Name == nameof(PostconditionViolationException))
        {
            return new Observation(null, exception.GetType().Name, "", [], []);
        }
    }

    private static Observation ObserveHello(MethodInfo entry)
    {
        using var output = new LineCapture();
        var previous = Console.Out;
        try
        {
            Console.SetOut(output);
            entry.Invoke(null, [Array.Empty<string>()]);
        }
        finally
        {
            Console.SetOut(previous);
        }
        return new Observation(null, null, output.ToString(), output.Writes.ToArray(), []);
    }

    private sealed class LineCapture : StringWriter
    {
        internal readonly List<Effect> Writes = [];

        public override void WriteLine(string? value)
        {
            base.WriteLine(value);
            Writes.Add(new Effect("stdout-write", Text: value + NewLine));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
