namespace Microsoft.Mutate4CSharp.Exec;

using System.Globalization;

/// <summary>
/// Faithful port of mutate4java's package-private <c>WorkerWorkspaceCloser</c>: deletes a worker run
/// directory (with the retrying cleanup) when it still exists, throwing once the retry limit is
/// reached without success.
/// </summary>
public sealed class WorkerWorkspaceCloser
{
    /// <summary>
    /// Deletes <paramref name="runRoot"/> and everything beneath it. Does nothing when it is already
    /// gone; throws when the retrying delete gives up.
    /// </summary>
    /// <param name="runRoot">The run directory to delete.</param>
    /// <exception cref="InvalidOperationException">The run directory could not be deleted after the retry limit.</exception>
    public void Close(string runRoot)
    {
        ArgumentNullException.ThrowIfNull(runRoot);
        if (!Directory.Exists(runRoot))
        {
            return;
        }

        IOException? failure = WorkerCleanup.DeleteWithRetries(
            runRoot,
            WorkerWorkspaces.TryDelete,
            WorkerCleanup.SleepBeforeRetry);
        if (failure is not null)
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Failed deleting worker workspace: {runRoot}"),
                failure);
        }
    }
}
