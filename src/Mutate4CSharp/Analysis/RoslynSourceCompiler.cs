namespace Microsoft.Mutate4CSharp.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Compiles a single C# source file into a <see cref="CompiledSource"/> — the faithful port of
/// mutate4java's <c>JavaSourceCompiler</c>. Where the Java tool drove the JDK compiler tree API
/// (<c>JavacTask.parse()</c> then <c>analyze()</c> with <c>Trees</c>), this parses with
/// <see cref="CSharpSyntaxTree"/> and resolves types through a single-file
/// <see cref="CSharpCompilation"/> that carries the running runtime's default framework references,
/// so that primitives (<c>string</c>/<c>int</c>/<c>object</c>) and in-file symbols bind for the
/// semantic model. As with the Java single-file compile, unresolved user types remain
/// <see cref="TypeKind.Error"/> — the semantic model is consumed only for primitive-numeric and
/// reference decisions.
/// </summary>
public sealed class RoslynSourceCompiler
{
    private static readonly IReadOnlyList<MetadataReference> DefaultReferences = LoadDefaultReferences();

    /// <summary>
    /// Parses and resolves the given source file into a <see cref="CompiledSource"/>.
    /// </summary>
    /// <param name="file">The path to the C# source file to compile.</param>
    /// <returns>The parsed compilation-unit root and the resolved semantic model for the file.</returns>
    public CompiledSource Compile(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Mutate4CSharp.SingleFile",
            syntaxTrees: [tree],
            references: DefaultReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        CompilationUnitSyntax root = (CompilationUnitSyntax)tree.GetRoot();
        return new CompiledSource(root, semanticModel);
    }

    private static List<MetadataReference> LoadDefaultReferences()
    {
        // Gather the running runtime's default framework assemblies from the app-context
        // "TRUSTED_PLATFORM_ASSEMBLIES" list so that string/int/object and the rest of the BCL
        // resolve — without adding a reference-assemblies NuGet package (Roslyn stays the only new
        // runtime dependency).
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string trustedAssemblies
            || trustedAssemblies.Length == 0)
        {
            return [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)];
        }

        List<MetadataReference> references = [];
        foreach (string assemblyPath in trustedAssemblies.Split(
                     Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                references.Add(MetadataReference.CreateFromFile(assemblyPath));
            }
        }

        return references;
    }
}
