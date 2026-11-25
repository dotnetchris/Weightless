using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Weightless.Research.PropertyMapping;
using static Weightless.GraphValidation;

internal sealed record BoundMember(byte Receiver, byte Member, string Symbol);
internal sealed record BoundAssignment(int SourceLine, BoundMember Getter, BoundMember Setter);
internal sealed record BoundMapping(MappingPlan Plan, string Method, string Compiler, BoundAssignment[] Assignments);

internal static class MappingBinding
{
    internal static BoundMapping Import(string sourcePath, string text)
    {
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(text, Encoding.UTF8), path: sourcePath);
        var compilation = CSharpCompilation.Create("Weightless.StockMapping.Binding",
            [tree, CSharpSyntaxTree.ParseText("global using System;")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var error = compilation.GetDiagnostics().FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Require(error is null, $"C# source binding failed. {error}");
        var model = compilation.GetSemanticModel(tree);
        var declaration = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().SingleOrDefault(method =>
            method.Identifier.ValueText == nameof(StockMapping.Apply) &&
            method.Parent is ClassDeclarationSyntax { Identifier.ValueText: nameof(StockMapping) })
            ?? throw new InvalidDataException("Missing StockMapping.Apply declaration.");
        var method = model.GetDeclaredSymbol(declaration) ?? throw new InvalidDataException("Unbound StockMapping.Apply.");
        var equal = SymbolEqualityComparer.Default;
        var updateType = compilation.GetTypeByMetadataName(StockSchema.Receivers[StockSchema.UpdateReceiver]);
        var rowType = compilation.GetTypeByMetadataName(StockSchema.Receivers[StockSchema.RowReceiver]);
        Require(method.IsStatic && method.Parameters.Length == 2 &&
            method.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
            equal.Equals(method.Parameters[0].Type, updateType) && equal.Equals(method.Parameters[1].Type, rowType) &&
            equal.Equals(method.ReturnType, rowType), "Mapping source has the wrong signature.");
        var statements = declaration.Body?.Statements ?? throw new InvalidDataException("Expected a mapping block.");
        Require(statements.Count == 3, "Expected two assignments and a return.");

        var assignments = ImmutableArray.CreateBuilder<Assignment>(2);
        var bindings = new List<BoundAssignment>(2);
        foreach (var statement in statements.Take(2))
        {
            if (model.GetOperation(statement) is not IExpressionStatementOperation
                {
                    Operation: ISimpleAssignmentOperation
                    {
                        Target: IPropertyReferenceOperation setter, Value: IPropertyReferenceOperation getter
                    }
                })
                throw new InvalidDataException("Expected a direct property assignment.");
            var source = BindMember(getter, updateType, StockSchema.UpdateReceiver, reading: true);
            var target = BindMember(setter, rowType, StockSchema.RowReceiver, reading: false);
            assignments.Add(new Assignment(source.Receiver, source.Member, target.Receiver, target.Member));
            bindings.Add(new BoundAssignment(statement.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                source, target));
        }

        if (model.GetOperation(statements[^1]) is not IReturnOperation
            {
                ReturnedValue: IParameterReferenceOperation { Parameter.Ordinal: StockSchema.RowReceiver }
            })
            throw new InvalidDataException("Expected a return of the existing row.");
        return new BoundMapping(new MappingPlan(assignments.MoveToImmutable(), StockSchema.RowReceiver),
            method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            typeof(CSharpCompilation).Assembly.GetName().Version!.ToString(), bindings.ToArray());
    }

    private static BoundMember BindMember(IPropertyReferenceOperation reference, INamedTypeSymbol? receiverType,
        byte receiver, bool reading)
    {
        var property = reference.Property;
        var accessor = reading ? property.GetMethod : property.SetMethod;
        var equal = SymbolEqualityComparer.Default;
        Require(reference.Instance is IParameterReferenceOperation instance && instance.Parameter.Ordinal == receiver &&
            equal.Equals(instance.Parameter.Type, receiverType) && equal.Equals(property.ContainingType, receiverType) &&
            !property.IsStatic && !property.IsIndexer && property.Type.SpecialType == SpecialType.System_Int32 &&
            accessor is not null, "Expected an Int32 property on its declared receiver.");

        var member = Array.FindIndex(StockSchema.Members, schema => schema.Receiver == receiver &&
            schema.Name == property.Name && (reading ? schema.Read is not null : schema.Write is not null));
        Require(member >= 0, "The bound property has no typed host accessor.");
        return new BoundMember(receiver, (byte)member, accessor!.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
    }
}
