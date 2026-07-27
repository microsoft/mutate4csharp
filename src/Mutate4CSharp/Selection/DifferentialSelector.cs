namespace Microsoft.Mutate4CSharp.Selection;

using Microsoft.Mutate4CSharp.Manifest;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Differential mutation selection (spec §8): decides which mutation sites need testing given the CLI
/// flags and the target file's embedded manifest. Faithful port of mutate4java's
/// <c>DifferentialSelector</c> with one C#-specific adaptation — the synthetic <c>file:</c> fallback
/// sites are conservative-selected rather than dropped (see <see cref="Select"/>).
/// </summary>
/// <remarks>
/// The states are: <c>--mutate-all</c> or <c>--lines</c> without <c>--since-last-run</c> → not
/// differential (every site selected); no manifest → not differential; manifest with an unchanged
/// module hash → nothing selected; manifest with a changed hash → only the sites in changed scopes.
/// </remarks>
public sealed class DifferentialSelector
{
    private readonly ChangedScopeFinder _changedScopeFinder;

    /// <summary>
    /// Initializes a new instance of the <see cref="DifferentialSelector"/> class.
    /// </summary>
    /// <param name="manifestSupport">The manifest reader used to load the embedded manifest.</param>
    public DifferentialSelector(ManifestSupport manifestSupport)
    {
        ArgumentNullException.ThrowIfNull(manifestSupport);
        _changedScopeFinder = new ChangedScopeFinder(manifestSupport);
    }

    /// <summary>
    /// Selects the mutation sites that need testing for the given target file, CLI flags, and
    /// analysis, together with the diagnostic counts that explain the selection.
    /// </summary>
    /// <param name="sourceFile">The target source file.</param>
    /// <param name="parsed">The parsed CLI arguments.</param>
    /// <param name="analysis">The current source analysis.</param>
    /// <returns>The differential selection.</returns>
    public DifferentialSelection Select(string sourceFile, CliArguments parsed, SourceAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(analysis);
        if (parsed.MutateAll)
        {
            return NotDifferential(analysis);
        }

        if (!parsed.SinceLastRun && parsed.Lines.Count > 0)
        {
            return NotDifferential(analysis);
        }

        ChangedScopes changedScopes = ChangedScopesFor(sourceFile, analysis);
        IReadOnlySet<string> allScopeIds = changedScopes.AllScopeIds();
        if (allScopeIds.Count == 0 && !changedScopes.ManifestPresent)
        {
            return NotDifferential(analysis);
        }

        int changedMutationSites = MutationCount(analysis, allScopeIds);
        int differentialSurfaceArea = MutationCount(analysis, changedScopes.UnregisteredScopeIds);
        int manifestViolatingSurfaceArea = MutationCount(analysis, changedScopes.ManifestViolationScopeIds);

        // The synthetic file: fallback sites (top-level statements outside every declaration) carry no
        // manifest scope, so the changed-scope sets never mention them. Mirroring Java's tolerance of
        // its default, they are conservative-selected (treated as changed) rather than silently
        // dropped — the S3 carry-forward from Anders' T7 review. In a target with no file: sites (the
        // only shape mutate4java ever sees) this reduces to Java's exact behaviour.
        List<MutationSite> selected = analysis.Sites
            .Where(site => allScopeIds.Contains(site.ScopeId) || IsFileFallbackScope(site.ScopeId))
            .ToList();

        // Faithful to Java, the module is reported unchanged only when the manifest is present and
        // nothing is selected; conservative-selected file: sites keep it from being reported unchanged.
        bool unchangedModule = allScopeIds.Count == 0 && selected.Count == 0;
        return new DifferentialSelection(
            selected,
            unchangedModule,
            changedScopes.ManifestPresent,
            changedScopes.ModuleHashChanged,
            analysis.Sites.Count,
            changedMutationSites,
            differentialSurfaceArea,
            manifestViolatingSurfaceArea);
    }

    /// <summary>
    /// Returns the ids of the scopes that have changed relative to the embedded manifest — the scan
    /// mode's <c>*</c> markers.
    /// </summary>
    /// <param name="sourceFile">The target source file.</param>
    /// <param name="analysis">The current source analysis.</param>
    /// <returns>The set of changed scope ids.</returns>
    public IReadOnlySet<string> ChangedScopeIds(string sourceFile, SourceAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(analysis);
        return ChangedScopesFor(sourceFile, analysis).AllScopeIds();
    }

    private static bool IsFileFallbackScope(string scopeId)
    {
        return scopeId.StartsWith("file:", StringComparison.Ordinal);
    }

    private ChangedScopes ChangedScopesFor(string sourceFile, SourceAnalysis analysis)
    {
        return _changedScopeFinder.ChangedScopes(sourceFile, analysis);
    }

    private int MutationCount(SourceAnalysis analysis, IReadOnlySet<string> scopeIds)
    {
        return analysis.Sites.Count(site => scopeIds.Contains(site.ScopeId));
    }

    private DifferentialSelection NotDifferential(SourceAnalysis analysis)
    {
        return new DifferentialSelection(
            analysis.Sites,
            false,
            false,
            false,
            analysis.Sites.Count,
            0,
            0,
            0);
    }
}
