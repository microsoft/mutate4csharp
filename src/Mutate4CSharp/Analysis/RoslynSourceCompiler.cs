namespace Microsoft.Mutate4CSharp.Analysis;

using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Linq;
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
/// <remarks>
/// <para>
/// DD5: to keep parity with mutate4java on modern C# (<c>&lt;ImplicitUsings&gt;enable</c> — the SDK
/// default), the single-file compilation additionally carries the target file's owning-project
/// global/implicit using context, injected as one usings-only syntax tree. Java imports are in-file,
/// so they survive a single-file compile; C#'s implicit/global usings live at project scope, so
/// without this the implicit-using-dependent BCL types (<c>List&lt;T&gt;</c>, <c>ISet&lt;T&gt;</c>,
/// <c>Task&lt;T&gt;</c>, …) would not bind, be classified <see cref="TypeKind.Error"/>, and be
/// silently skipped for null-replacement — drawing the resolution line stricter than Java.
/// </para>
/// <para>
/// The references lever is deliberately untouched: only the BCL is referenced (via TPA), so a
/// <c>using</c> can bind ONLY a BCL type whose assembly is already present — never a third-party type
/// (its assembly is not referenced). The change is monotonic: when the target file has no owning
/// <c>.csproj</c> (loose files, temp snippets) nothing is injected and behavior is exactly as before,
/// and where a context is injected it can only ADD bindings, never remove one. Only the target file's
/// body is analyzed for mutation sites; the usings tree is context-only.
/// </para>
/// </remarks>
public sealed class RoslynSourceCompiler
{
    private static readonly IReadOnlyList<MetadataReference> DefaultReferences = LoadDefaultReferences();

    // Filesystem path comparison: case-insensitive on Windows, ordinal elsewhere. Matches ModuleResolver
    // (real on-disk paths, NOT the fidelity-landmine ordinal used for hash-affecting sort keys).
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    // The base Microsoft.NET.Sdk implicit-using namespaces. Synthesized (as global usings) only on the
    // fallback path — when the project enables <ImplicitUsings> but its SDK-generated GlobalUsings.g.cs
    // is not yet on disk (clean checkout, obj/ empty). The generated-file path (preferred) already
    // captures this base set plus any SDK-type-specific extras (Web/Worker) precisely.
    private static readonly IReadOnlyList<string> SdkImplicitUsingNamespaces =
    [
        "System",
        "System.Collections.Generic",
        "System.IO",
        "System.Linq",
        "System.Net.Http",
        "System.Threading",
        "System.Threading.Tasks",
    ];

    // Per-instance cache: owning .csproj full path -> reconstructed global-using context. All source
    // files under one project share a single project scan (O(N) rather than O(N^2) across an N-file
    // project). ConcurrentDictionary keeps the get-or-build thread-safe; the build is a pure function of
    // the project on disk, so a rare concurrent double-build produces the same value.
    private readonly ConcurrentDictionary<string, string> _usingContextByProject =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>
    /// Parses and resolves the given source file into a <see cref="CompiledSource"/>.
    /// </summary>
    /// <param name="file">The path to the C# source file to compile.</param>
    /// <returns>The parsed compilation-unit root and the resolved semantic model for the file.</returns>
    public CompiledSource Compile(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);

        // DD5: inject the owning-project global/implicit using context as a context-only tree, so
        // implicit-using-dependent BCL types bind for the target tree's semantic model.
        List<SyntaxTree> syntaxTrees = [tree];
        string usingContext = ProjectUsingContext(file);
        if (usingContext.Length > 0)
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(usingContext));
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Mutate4CSharp.SingleFile",
            syntaxTrees: syntaxTrees,
            references: DefaultReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // The semantic model and root are taken for the TARGET file's tree only — the usings tree is
        // context, never analyzed for mutation sites.
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

    // DD5: resolve the target file's owning-project global/implicit using context (the `global using …;`
    // directives injected as a context tree). Returns empty when the file has no owning .csproj up to the
    // drive root — in which case Compile injects nothing and behaves exactly as before (the no-project
    // fallback that preserves every existing test). The context is memoized per owning .csproj so every
    // source file under one project shares a single project scan (O(N) rather than O(N^2)).
    private string ProjectUsingContext(string file)
    {
        string? projectFile = FindOwningProject(file);
        return projectFile is null
            ? string.Empty
            : _usingContextByProject.GetOrAdd(Path.GetFullPath(projectFile), BuildProjectUsingContext);
    }

    // Reconstruct the owning project's global/implicit using context as a usings-only source (a sequence
    // of `global using …;` directives). These are OPTIONAL reads of a possibly-UNRELATED ancestor .csproj
    // (FindOwningProject has no workspace ceiling), so a malformed or locked project must NOT abort
    // analysis: a narrow catch degrades to empty context — the pre-DD5 no-context behavior — preserving
    // DD5's monotonic guarantee. The catch is deliberately narrow (XML / IO / access), never a broad
    // Exception, so real logic bugs still surface (CA1031).
    private static string BuildProjectUsingContext(string projectFile)
    {
        try
        {
            List<string> directives = [.. BaseGlobalUsings(projectFile)];
            directives.AddRange(ExplicitGlobalUsings(projectFile));

            // De-dup, preserving first-seen order, and drop any that failed to normalize to text.
            HashSet<string> seen = new(StringComparer.Ordinal);
            List<string> unique = [];
            foreach (string directive in directives)
            {
                if (directive.Length > 0 && seen.Add(directive))
                {
                    unique.Add(directive);
                }
            }

            return string.Join("\n", unique);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    // Walk up from the file's directory to the nearest ancestor .csproj, stopping at the drive root.
    // No workspace ceiling here (unlike ModuleResolver): the compiler is a standalone analysis step.
    private static string? FindOwningProject(string file)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(file));
        while (directory is not null)
        {
            string? projectFile = Directory.EnumerateFiles(directory)
                .Where(path => path.EndsWith(".csproj", PathComparison))
                .OrderBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            if (projectFile is not null)
            {
                return projectFile;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    // Preferred: parse the SDK-generated obj/**/<Project>.GlobalUsings.g.cs (captures the ImplicitUsings
    // base set AND SDK-type-specific extras precisely). Fallback: when that file is absent (clean
    // checkout — obj/ empty), synthesize the base Microsoft.NET.Sdk set iff the .csproj enables
    // <ImplicitUsings>.
    private static IReadOnlyList<string> BaseGlobalUsings(string projectFile)
    {
        string? generated = FindGeneratedGlobalUsings(projectFile);
        if (generated is not null)
        {
            return GlobalUsingDirectivesIn(File.ReadAllText(generated));
        }

        return ImplicitUsingsEnabled(projectFile)
            ? [.. SdkImplicitUsingNamespaces.Select(ns => "global using " + ns + ";")]
            : [];
    }

    private static string? FindGeneratedGlobalUsings(string projectFile)
    {
        string objDirectory = Path.Combine(Path.GetDirectoryName(projectFile)!, "obj");
        if (!Directory.Exists(objDirectory))
        {
            return null;
        }

        string generatedName = Path.GetFileNameWithoutExtension(projectFile) + ".GlobalUsings.g.cs";
        EnumerationOptions options = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };
        return Directory.EnumerateFiles(objDirectory, generatedName, options)
            .OrderBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool ImplicitUsingsEnabled(string projectFile)
    {
        XDocument document = LoadProjectDocument(projectFile);
        foreach (XElement element in document.Descendants()
                     .Where(node => string.Equals(node.Name.LocalName, "ImplicitUsings", StringComparison.OrdinalIgnoreCase)))
        {
            string value = element.Value.Trim();
            if (string.Equals(value, "enable", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // Always additionally scan the owning project's .cs files for top-level `global using` directives —
    // parse-only, never compiling those files' bodies — excluding only build output (bin/obj). The
    // target file is intentionally NOT special-cased out: its own `global using` directives are already
    // effective through its tree, and re-collecting them into the context tree is harmless (the
    // exact-string dedup collapses the duplicate). Scanning every file uniformly is what lets the whole
    // project share one cached scan.
    private static List<string> ExplicitGlobalUsings(string projectFile)
    {
        string projectDirectory = Path.GetDirectoryName(projectFile)!;
        EnumerationOptions options = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };

        List<string> directives = [];
        foreach (string sourceFile in Directory.EnumerateFiles(projectDirectory, "*.cs", options))
        {
            if (IsUnderBuildOutput(projectDirectory, sourceFile))
            {
                continue;
            }

            directives.AddRange(GlobalUsingDirectivesIn(File.ReadAllText(sourceFile)));
        }

        return directives;
    }

    // Parse the source and return its top-level `global using …;` directives, each normalized to text.
    // Plain file-scoped `using` directives are excluded — only `global using` applies compilation-wide.
    private static IReadOnlyList<string> GlobalUsingDirectivesIn(string sourceText)
    {
        CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(sourceText).GetCompilationUnitRoot();
        return
        [
            .. root.Usings
                .Where(directive => directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                .Select(directive => directive.NormalizeWhitespace().ToFullString().Trim()),
        ];
    }

    private static bool IsUnderBuildOutput(string projectDirectory, string path)
    {
        string relative = Path.GetRelativePath(projectDirectory, path);
        foreach (string segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(segment, "bin", PathComparison) || string.Equals(segment, "obj", PathComparison))
            {
                return true;
            }
        }

        return false;
    }

    private static XDocument LoadProjectDocument(string projectFile)
    {
        // Secure reader: prohibit DTD processing and disable external entity resolution (XXE) — the
        // same posture as ModuleResolver and the Cobertura parser.
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        using FileStream stream = File.OpenRead(projectFile);
        using XmlReader reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader);
    }
}
