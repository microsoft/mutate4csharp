namespace Microsoft.Mutate4CSharp.Selection;

/// <summary>
/// The changed-scope classification computed by <see cref="ChangedScopeFinder"/> for a target file
/// against its embedded manifest — faithful port of mutate4java's package-private
/// <c>ChangedScopes</c> record (widened to <see langword="public"/> under guardrail #9, which forbids
/// <c>internal</c>).
/// </summary>
/// <param name="ManifestPresent">Whether the target file carries an embedded manifest.</param>
/// <param name="ModuleHashChanged">Whether the module hash differs from the manifest's.</param>
/// <param name="UnregisteredScopeIds">
/// The ids of scopes present in the current analysis but absent from the manifest.
/// </param>
/// <param name="ManifestViolationScopeIds">
/// The ids of scopes present in the manifest whose semantic hash has since changed.
/// </param>
public sealed record ChangedScopes(
    bool ManifestPresent,
    bool ModuleHashChanged,
    IReadOnlySet<string> UnregisteredScopeIds,
    IReadOnlySet<string> ManifestViolationScopeIds)
{
    /// <summary>
    /// Returns the union of the unregistered and manifest-violating scope ids — the scopes that are
    /// considered "changed" and therefore drive differential selection.
    /// </summary>
    /// <returns>The set of all changed scope ids.</returns>
    public IReadOnlySet<string> AllScopeIds()
    {
        HashSet<string> ids = new(UnregisteredScopeIds, StringComparer.Ordinal);
        ids.UnionWith(ManifestViolationScopeIds);
        return ids;
    }
}
