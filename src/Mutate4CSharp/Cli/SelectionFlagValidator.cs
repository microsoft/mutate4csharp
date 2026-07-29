namespace Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// Enforces the mutually exclusive selection-flag combinations: scan, update-manifest, line, and
/// differential conflicts.
/// </summary>
public sealed class SelectionFlagValidator
{
    /// <summary>
    /// Validates every selection-flag conflict rule.
    /// </summary>
    /// <param name="state">The accumulated parse state.</param>
    public void Validate(CliArgumentParseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateScanConflicts(state);
        ValidateUpdateManifestConflicts(state);
        ValidateLineConflicts(state);
        ValidateDifferentialConflicts(state);
    }

    private void ValidateScanConflicts(CliArgumentParseState state)
    {
        Reject(state.Scan && state.SinceLastRun, "--scan may not be combined with --since-last-run");
        Reject(state.Scan && state.MutateAll, "--scan may not be combined with --mutate-all");
        Reject(state.Scan && state.UpdateManifest, "--scan may not be combined with --update-manifest");
        Reject(state.Scan && state.ReuseCoverage, "--scan may not be combined with --reuse-coverage");
    }

    private void ValidateUpdateManifestConflicts(CliArgumentParseState state)
    {
        Reject(state.UpdateManifest && state.SinceLastRun, "--update-manifest may not be combined with --since-last-run");
        Reject(state.UpdateManifest && state.MutateAll, "--update-manifest may not be combined with --mutate-all");
        Reject(state.UpdateManifest && state.Lines.Count > 0, "--update-manifest may not be combined with --lines");
        Reject(state.UpdateManifest && state.ReuseCoverage, "--update-manifest may not be combined with --reuse-coverage");
    }

    private void ValidateLineConflicts(CliArgumentParseState state)
    {
        Reject(state.Lines.Count > 0 && state.SinceLastRun, "--lines may not be combined with --since-last-run");
        Reject(state.Lines.Count > 0 && state.MutateAll, "--lines may not be combined with --mutate-all");
    }

    private void ValidateDifferentialConflicts(CliArgumentParseState state)
    {
        Reject(state.SinceLastRun && state.MutateAll, "--since-last-run may not be combined with --mutate-all");
    }

    private void Reject(bool invalid, string message)
    {
        if (invalid)
        {
            throw new ArgumentException(message);
        }
    }
}
