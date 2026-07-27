namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Mutate4CSharp.Analysis;

/// <summary>
/// Focused coverage for the T5-review type contracts the mutation-site factory relies on: the numeric
/// predicate accepts only primitive numeric types, the reference predicate accepts only resolved
/// reference types, and both exclude an unresolved (<c>Error</c>) type. Each expression is a body usage
/// of a distinctly named parameter so it is unambiguous in the syntax tree.
/// </summary>
public sealed class TreeTypePredicatesTests : IDisposable
{
    private const string Source =
        """
        class Types
        {
            int Numeric(int intValue) => intValue;
            string Reference(string stringValue) => stringValue;
            bool Boolean(bool boolValue) => boolValue;
            object Erroneous(Undefined errorValue) => errorValue;
        }
        """;

    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="TreeTypePredicatesTests"/> class, creating a per-test
    /// temporary directory for the source the compiler reads from disk.
    /// </summary>
    public TreeTypePredicatesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-predicates-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// Deletes the per-test temporary directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The numeric predicate is true for a primitive numeric type and false for a reference type or
    /// <c>bool</c>.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void IsNumericAcceptsOnlyPrimitiveNumericTypes()
    {
        (TreeTypePredicates predicates, CompiledSource compiled) = PredicatesFor();

        predicates.IsNumeric(Usage(compiled, "intValue")).Should().BeTrue();
        predicates.IsNumeric(Usage(compiled, "stringValue")).Should().BeFalse();
        predicates.IsNumeric(Usage(compiled, "boolValue")).Should().BeFalse();
    }

    /// <summary>
    /// The reference predicate is true for a reference type and false for primitive numeric or
    /// <c>bool</c> value types.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void IsReferenceAcceptsOnlyReferenceTypes()
    {
        (TreeTypePredicates predicates, CompiledSource compiled) = PredicatesFor();

        predicates.IsReference(Usage(compiled, "stringValue")).Should().BeTrue();
        predicates.IsReference(Usage(compiled, "intValue")).Should().BeFalse();
        predicates.IsReference(Usage(compiled, "boolValue")).Should().BeFalse();
    }

    /// <summary>
    /// An unresolved (<c>Error</c>) type is excluded from both predicates — the empty-classpath analog.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ExcludesErrorTypedExpressionFromBothPredicates()
    {
        (TreeTypePredicates predicates, CompiledSource compiled) = PredicatesFor();
        IdentifierNameSyntax errorValue = Usage(compiled, "errorValue");

        compiled.SemanticModel.GetTypeInfo(errorValue).Type!.TypeKind.Should().Be(TypeKind.Error);
        predicates.IsReference(errorValue).Should().BeFalse();
        predicates.IsNumeric(errorValue).Should().BeFalse();
    }

    private static IdentifierNameSyntax Usage(CompiledSource compiled, string name) =>
        compiled.Root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Single(identifier => identifier.Identifier.ValueText == name);

    private (TreeTypePredicates Predicates, CompiledSource Compiled) PredicatesFor()
    {
        string file = Path.Combine(_tempDir, "Types.cs");
        File.WriteAllText(file, Source);
        CompiledSource compiled = new RoslynSourceCompiler().Compile(file);
        return (new TreeTypePredicates(compiled.SemanticModel), compiled);
    }
}
