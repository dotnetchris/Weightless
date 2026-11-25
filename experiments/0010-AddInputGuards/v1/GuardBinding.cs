using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Weightless;
using Weightless.Research.InputGuards;
using static Weightless.GraphValidation;

internal sealed record GuardOrigin(int SourceLine, string BoundTarget, string DiagnosticArgumentKind, bool ImplicitDiagnostic);
internal sealed record BoundGuardForm(GuardPlan Plan, string Method, GuardOrigin[] Origins);
internal sealed record BoundGuardedAdd(BoundGuardForm Explicit, BoundGuardForm Helper, string Compiler);

internal static class GuardBinding
{
    internal static BoundGuardedAdd Import(string path, string text, Graph seed)
    {
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(text, Encoding.UTF8), path: path);
        var root = tree.GetCompilationUnitRoot();
        var compilation = CSharpCompilation.Create("Weightless.GuardedAdd.Binding",
            [tree, CSharpSyntaxTree.ParseText("global using System;")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var error = compilation.GetDiagnostics().FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Require(error is null, $"C# source binding failed. {error}");
        var model = compilation.GetSemanticModel(tree);
        var rangeException = compilation.GetTypeByMetadataName(typeof(ArgumentOutOfRangeException).FullName!)
            ?? throw new InvalidDataException("Could not resolve ArgumentOutOfRangeException.");
        return new BoundGuardedAdd(
            BindMethod(model, FindMethod(root, nameof(GuardedAdd.WithIf)), rangeException, seed, useHelper: false),
            BindMethod(model, FindMethod(root, nameof(GuardedAdd.WithHelper)), rangeException, seed, useHelper: true),
            typeof(CSharpCompilation).Assembly.GetName().Version!.ToString());
    }

    internal static string RaiseFirstBound(string text, string name)
    {
        var root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
        var first = FindMethod(root, name).Body?.Statements[0];
        var prior = first switch
        {
            IfStatementSyntax { Condition: BinaryExpressionSyntax relation } => relation.Right,
            ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call } => call.ArgumentList.Arguments[1].Expression,
            _ => throw new InvalidDataException("Could not locate the first guard bound for the source control.")
        };
        var zero = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));
        return root.ReplaceNode(prior, zero.WithTriviaFrom(prior)).ToFullString();
    }

    private static MethodDeclarationSyntax FindMethod(CompilationUnitSyntax root, string name) =>
        root.DescendantNodes().OfType<MethodDeclarationSyntax>().SingleOrDefault(method =>
            method.Identifier.ValueText == name &&
            method.Parent is ClassDeclarationSyntax { Identifier.ValueText: nameof(GuardedAdd) })
        ?? throw new InvalidDataException($"Missing GuardedAdd.{name}.");

    private static BoundGuardForm BindMethod(SemanticModel model, MethodDeclarationSyntax declaration,
        INamedTypeSymbol rangeException, Graph seed, bool useHelper)
    {
        var method = model.GetDeclaredSymbol(declaration) ?? throw new InvalidDataException("Unbound GuardedAdd signature.");
        Require(method.IsStatic && method.Parameters.Length == 2 && method.ReturnType.SpecialType == SpecialType.System_Int32 &&
            method.Parameters.All(parameter => parameter.Type.SpecialType == SpecialType.System_Int32 && parameter.RefKind == RefKind.None),
            "GuardedAdd source has the wrong signature.");
        var statements = declaration.Body?.Statements ?? throw new InvalidDataException("Expected a guard block.");
        Require(statements.Count == 3, "Expected two guard statements and a checked return.");
        var guards = ImmutableArray.CreateBuilder<InputGuard>(2);
        var origins = new List<GuardOrigin>(2);
        foreach (var statement in statements.Take(2))
        {
            var operation = model.GetOperation(statement) ?? throw new InvalidDataException("Unbound guard statement.");
            var guard = useHelper ? BindHelper(operation, rangeException) : BindIf(operation, rangeException);
            guards.Add(guard.Guard);
            origins.Add(guard.Origin);
        }

        var seedAdd = (Addition)seed.Functions[0].Body[0];
        if (model.GetOperation(statements[^1]) is not IReturnOperation
            {
                ReturnedValue: IBinaryOperation
                {
                    OperatorKind: BinaryOperatorKind.Add, OperatorMethod: null, IsChecked: true,
                    Type.SpecialType: SpecialType.System_Int32,
                    LeftOperand: IParameterReferenceOperation left, RightOperand: IParameterReferenceOperation right
                }
            } || left.Parameter.Ordinal != seedAdd.Left || right.Parameter.Ordinal != seedAdd.Right)
            throw new InvalidDataException("The bound checked Add differs from the seed's operand order or policy.");

        return new BoundGuardForm(new GuardPlan(guards.MoveToImmutable(), seed),
            method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), origins.ToArray());
    }

    private static (InputGuard Guard, GuardOrigin Origin) BindIf(IOperation statement, INamedTypeSymbol rangeException)
    {
        if (statement is not IConditionalOperation
            {
                Condition: IBinaryOperation
                {
                    OperatorKind: BinaryOperatorKind.LessThan, OperatorMethod: null,
                    LeftOperand: IParameterReferenceOperation operand,
                    RightOperand: { Type.SpecialType: SpecialType.System_Int32, ConstantValue: { HasValue: true, Value: int bound } }
                },
                WhenFalse: null
            } conditional)
            throw new InvalidDataException("Expected an Int32 less-than conditional without an else branch.");
        var body = conditional.WhenTrue is IBlockOperation { Operations.Length: 1 } block
            ? block.Operations[0] : conditional.WhenTrue;
        if (body is not IThrowOperation { Exception: { } thrown } || UnwrapImplicit(thrown) is not IObjectCreationOperation
            {
                Constructor: { Parameters.Length: 3 } constructor
            } creation ||
            !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, rangeException) ||
            constructor.Parameters[0].Type.SpecialType != SpecialType.System_String ||
            constructor.Parameters[1].Type.SpecialType != SpecialType.System_Object ||
            constructor.Parameters[2].Type.SpecialType != SpecialType.System_String)
            throw new InvalidDataException("Expected a bound ArgumentOutOfRangeException throw with actual value.");

        var actualValue = Argument(creation.Arguments, 1);
        if (UnwrapImplicit(actualValue.Value) is not IParameterReferenceOperation actual ||
            !SymbolEqualityComparer.Default.Equals(actual.Parameter, operand.Parameter) ||
            Argument(creation.Arguments, 2).Value.ConstantValue is not { HasValue: true, Value: null })
            throw new InvalidDataException("Expected the rejected parameter as actual value and a null message.");
        return Build(operand, bound, Argument(creation.Arguments, 0), statement, constructor);
    }

    private static (InputGuard Guard, GuardOrigin Origin) BindHelper(IOperation statement, INamedTypeSymbol rangeException)
    {
        if (statement is not IExpressionStatementOperation
            {
                Operation: IInvocationOperation { TargetMethod: var target } invocation
            } || target.Name != nameof(ArgumentOutOfRangeException.ThrowIfLessThan) || !target.IsStatic || !target.ReturnsVoid ||
            !SymbolEqualityComparer.Default.Equals(target.ContainingType, rangeException) ||
            target.TypeArguments.Length != 1 || target.TypeArguments[0].SpecialType != SpecialType.System_Int32 ||
            target.Parameters.Length != 3)
            throw new InvalidDataException("Expected the resolved Int32 ArgumentOutOfRangeException.ThrowIfLessThan helper.");

        if (Argument(invocation.Arguments, 0).Value is not IParameterReferenceOperation operand ||
            Argument(invocation.Arguments, 1).Value.ConstantValue is not { HasValue: true, Value: int bound })
            throw new InvalidDataException("Expected a direct Int32 parameter and bound for the range helper.");

        return Build(operand, bound, Argument(invocation.Arguments, 2), statement, target);
    }

    private static (InputGuard Guard, GuardOrigin Origin) Build(IParameterReferenceOperation operand, int bound,
        IArgumentOperation diagnostic, IOperation statement, IMethodSymbol target)
    {
        if (operand.Parameter.Type.SpecialType != SpecialType.System_Int32 ||
            diagnostic.Value.ConstantValue is not { HasValue: true, Value: string name })
            throw new InvalidDataException("Expected an Int32 guard operand and the effective diagnostic parameter name.");
        return (new InputGuard((byte)operand.Parameter.Ordinal, bound, name),
            new GuardOrigin(statement.Syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                target.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                diagnostic.ArgumentKind.ToString(), diagnostic.IsImplicit));
    }

    private static IArgumentOperation Argument(ImmutableArray<IArgumentOperation> arguments, int ordinal) =>
        arguments.SingleOrDefault(argument => argument.Parameter?.Ordinal == ordinal)
        ?? throw new InvalidDataException($"Missing bound argument at slot {ordinal}.");

    private static IOperation UnwrapImplicit(IOperation operation) =>
        operation is IConversionOperation { IsImplicit: true, OperatorMethod: null } converted ? converted.Operand : operation;
}
