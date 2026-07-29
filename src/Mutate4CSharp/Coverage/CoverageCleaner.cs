namespace Microsoft.Mutate4CSharp.Coverage;

/// <summary>
/// Faithful port of mutate4java's package-private <c>CoverageCleaner</c>: deletes the stale coverage
/// artifacts left by a previous run before a fresh one is generated. mutate4java removes the JaCoCo
/// report directory tree and the <c>jacoco.exec</c> data file; the coverlet adaptation has a single
/// artifact — the results directory tree the collector writes its <c>coverage.cobertura.xml</c> (and
/// the TRX logger's <c>*.trx</c>) under — so the port keeps the tree-deletion path and drops the
/// separate exec-file deletion, which has no coverlet analog. A deletion failure is surfaced as an
/// <see cref="InvalidOperationException"/> carrying the same <c>Failed deleting stale coverage</c>
/// message mutate4java raises.
/// </summary>
public sealed class CoverageCleaner
{
    /// <summary>
    /// Deletes the coverage results directory tree if it is present, leaving a clean slate for the
    /// next coverage run. A missing directory is a no-op.
    /// </summary>
    /// <param name="directory">The coverage results directory to delete.</param>
    /// <exception cref="InvalidOperationException">The directory exists but could not be deleted.</exception>
    public void DeleteStaleCoverage(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        DeleteTreeIfPresent(directory);
    }

    private void DeleteTreeIfPresent(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Failed deleting stale coverage: {path}", ex);
        }
    }
}
