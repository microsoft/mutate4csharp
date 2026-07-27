namespace Microsoft.Mutate4CSharp.Analysis;

using Microsoft.Mutate4CSharp.Manifest;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// The faithful port of mutate4java's <c>MutationCatalog</c>: the public analysis entry point that
/// strips any embedded manifest, walks the parsed source once, and aggregates the discovered mutation
/// sites, the DD1 scopes, and the module hash into a <see cref="SourceAnalysis"/>.
/// </summary>
/// <remarks>
/// Where the Java catalog compiled and walked the file twice (once for sites, once for scopes), this
/// runs a single Roslyn walk that collects both — a behavior-neutral consolidation, since one
/// <see cref="AstMutationScanner"/> emits the sites and drives the scope tracker together. The module
/// hash is the aggregate hash over the sorted scopes, via the T3 hashing helper.
/// </remarks>
public sealed class MutationCatalog
{
    private readonly ManifestSupport _manifestSupport = new();
    private readonly RoslynSourceCompiler _compiler = new();

    /// <summary>
    /// Analyzes each file and returns all discovered mutation sites, ordered by file then start offset —
    /// the faithful analog of the Java <c>discover</c> aggregation.
    /// </summary>
    /// <param name="files">The source files to analyze.</param>
    /// <returns>The discovered mutation sites, ordered by file then start offset.</returns>
    public IReadOnlyList<MutationSite> Discover(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        List<MutationSite> sites = [];
        foreach (string file in files)
        {
            try
            {
                sites.AddRange(Analyze(file).Sites);
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException("Failed analyzing mutations for " + file, ex);
            }
        }

        return [.. sites.OrderBy(site => site.File, StringComparer.Ordinal).ThenBy(site => site.Start)];
    }

    /// <summary>
    /// Analyzes a single source file: strips any embedded manifest, walks the source once, and returns
    /// the manifest-free source, the discovered sites (visit order), the scopes (ordered by id), and the
    /// module hash.
    /// </summary>
    /// <param name="file">The source file to analyze.</param>
    /// <returns>The source analysis.</returns>
    public SourceAnalysis Analyze(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        string raw = File.ReadAllText(file);
        string source = _manifestSupport.StripManifest(raw);
        CompiledSource compiled = _compiler.Compile(file);
        AstMutationScanner scanner = new(file, compiled.Root, compiled.SemanticModel);
        scanner.Visit(compiled.Root);
        IReadOnlyList<MutationScope> scopes =
            [.. scanner.Scopes.OrderBy(scope => scope.Id, StringComparer.Ordinal)];
        return new SourceAnalysis(source, [.. scanner.Sites], scopes, _manifestSupport.HashScopes(scopes));
    }
}
