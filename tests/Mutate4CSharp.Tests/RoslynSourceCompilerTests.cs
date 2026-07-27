namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Mutate4CSharp.Analysis;

/// <summary>
/// Sanity coverage for the T5 Roslyn single-file compiler that de-risks the T6 scanner's
/// primitive-numeric / reference type decisions: it proves the default framework references are
/// wired so that a trivial source parses and its semantic model resolves <c>int</c> as a
/// primitive-numeric value type and <c>string</c> as a reference type — both non-<c>Error</c>.
/// </summary>
public sealed class RoslynSourceCompilerTests : IDisposable
{
    private readonly RoslynSourceCompiler _compiler = new();
    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoslynSourceCompilerTests"/> class, creating a
    /// per-test temporary directory to hold the source file that the compiler reads from disk.
    /// </summary>
    public RoslynSourceCompilerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-compiler-" + Guid.NewGuid().ToString("N"));
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
    /// A trivial source parses and the semantic model — backed by the default framework references —
    /// resolves <c>int</c> to a non-<c>Error</c> primitive-numeric value type and <c>string</c> to a
    /// non-<c>Error</c> reference type.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ResolvesPrimitiveNumericAndReferenceTypesFromDefaultReferences()
    {
        string sourceFile = Path.Combine(_tempDir, "Sample.cs");
        string source =
            """
            class Sample
            {
                int Number()
                {
                    int value = 0;
                    string text = "x";
                    return value;
                }
            }
            """;
        File.WriteAllText(sourceFile, source);

        CompiledSource compiled = _compiler.Compile(sourceFile);

        ILocalSymbol intLocal = LocalNamed(compiled, "value");
        ILocalSymbol stringLocal = LocalNamed(compiled, "text");

        intLocal.Type.TypeKind.Should().NotBe(TypeKind.Error);
        intLocal.Type.SpecialType.Should().Be(SpecialType.System_Int32);
        intLocal.Type.IsValueType.Should().BeTrue();

        stringLocal.Type.TypeKind.Should().NotBe(TypeKind.Error);
        stringLocal.Type.SpecialType.Should().Be(SpecialType.System_String);
        stringLocal.Type.IsReferenceType.Should().BeTrue();
    }

    private static ILocalSymbol LocalNamed(CompiledSource compiled, string name)
    {
        VariableDeclaratorSyntax declarator = compiled.Root.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Single(declarator => declarator.Identifier.ValueText == name);
        return (ILocalSymbol)compiled.SemanticModel.GetDeclaredSymbol(declarator)!;
    }
}
