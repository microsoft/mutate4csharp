namespace Microsoft.Mutate4CSharp.Model;

/// <summary>
/// The result of analyzing a source file: the source with any manifest stripped, the discovered
/// mutation sites and scopes, and the module hash.
/// </summary>
/// <param name="SourceWithoutManifest">The source text with any manifest removed.</param>
/// <param name="Sites">The discovered mutation sites.</param>
/// <param name="Scopes">The discovered mutation scopes.</param>
/// <param name="ModuleHash">The module hash.</param>
public sealed record SourceAnalysis(
    string SourceWithoutManifest,
    IReadOnlyList<MutationSite> Sites,
    IReadOnlyList<MutationScope> Scopes,
    string ModuleHash);
