namespace Microsoft.Mutate4CSharp.Selection;

using Microsoft.Mutate4CSharp.Manifest;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Compares a target file's current scopes against its embedded manifest to classify each scope as
/// unchanged, unregistered (new), or manifest-violating (changed) — the faithful port of
/// mutate4java's package-private <c>ChangedScopeFinder</c>. The only shape change is that
/// mutate4java's <c>Optional&lt;DifferentialManifest&gt;</c> read becomes a nullable
/// <see cref="DifferentialManifest"/>.
/// </summary>
public sealed class ChangedScopeFinder
{
    private static readonly IReadOnlySet<string> EmptyScopeIds = new HashSet<string>(StringComparer.Ordinal);

    private readonly ManifestSupport _manifestSupport;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangedScopeFinder"/> class.
    /// </summary>
    /// <param name="manifestSupport">The manifest reader used to load the embedded manifest.</param>
    public ChangedScopeFinder(ManifestSupport manifestSupport)
    {
        ArgumentNullException.ThrowIfNull(manifestSupport);
        _manifestSupport = manifestSupport;
    }

    /// <summary>
    /// Reads the embedded manifest for <paramref name="sourceFile"/> and classifies the current
    /// analysis scopes against it.
    /// </summary>
    /// <param name="sourceFile">The target source file.</param>
    /// <param name="analysis">The current source analysis.</param>
    /// <returns>
    /// The changed-scope classification: no manifest, an unchanged module, or the sets of
    /// unregistered and manifest-violating scope ids.
    /// </returns>
    public ChangedScopes ChangedScopes(string sourceFile, SourceAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(analysis);
        DifferentialManifest? manifest = _manifestSupport.Read(sourceFile);
        if (manifest is null)
        {
            return new ChangedScopes(false, false, EmptyScopeIds, EmptyScopeIds);
        }

        DifferentialManifest previous = manifest;
        if (string.Equals(previous.ModuleHash, analysis.ModuleHash, StringComparison.Ordinal))
        {
            return new ChangedScopes(true, false, EmptyScopeIds, EmptyScopeIds);
        }

        Dictionary<string, string> previousHashes = new(StringComparer.Ordinal);
        foreach (MutationScope scope in previous.Scopes)
        {
            previousHashes[scope.Id] = scope.SemanticHash;
        }

        HashSet<string> unregisteredScopes = new(StringComparer.Ordinal);
        HashSet<string> manifestViolations = new(StringComparer.Ordinal);
        foreach (MutationScope scope in analysis.Scopes)
        {
            if (!previousHashes.TryGetValue(scope.Id, out string? previousHash))
            {
                unregisteredScopes.Add(scope.Id);
            }
            else if (!string.Equals(scope.SemanticHash, previousHash, StringComparison.Ordinal))
            {
                manifestViolations.Add(scope.Id);
            }
        }

        return new ChangedScopes(true, true, unregisteredScopes, manifestViolations);
    }
}
