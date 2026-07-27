namespace Microsoft.Mutate4CSharp.Exec;

/// <summary>
/// Faithful port of mutate4java's package-private <c>WorkerCleanup</c>: retries a worker-directory
/// delete a bounded number of times, sleeping between attempts, so a transient file lock (a testhost
/// process still releasing handles after <c>dotnet test</c> exits) does not fail the run.
/// </summary>
/// <remarks>
/// mutate4java retries only <c>DirectoryNotEmptyException</c> and fails fast on any other
/// <c>IOException</c>. .NET has no such subtype: a still-locked worker tree surfaces as a plain
/// <see cref="IOException"/>, so every captured <see cref="IOException"/> is treated as the retryable
/// transient failure (the faithful ecosystem adaptation). The retry count and delay are preserved.
/// </remarks>
public sealed class WorkerCleanup
{
    /// <summary>The maximum number of delete attempts (mutate4java's <c>DELETE_RETRIES</c>).</summary>
    public const int DeleteRetries = 5;

    /// <summary>The delay between delete attempts in milliseconds (mutate4java's <c>RETRY_DELAY_MILLIS</c>).</summary>
    public const long RetryDelayMillis = 50L;

    private WorkerCleanup()
    {
    }

    /// <summary>
    /// Attempts the delete up to <see cref="DeleteRetries"/> times, invoking
    /// <paramref name="retrySleeper"/> after each failed attempt. Returns <see langword="null"/> once a
    /// delete succeeds, or the last failure once the retry limit is reached.
    /// </summary>
    /// <param name="runRoot">The run directory to delete.</param>
    /// <param name="deleteAttempt">The delete seam, returning the failure or <see langword="null"/>.</param>
    /// <param name="retrySleeper">The sleep seam invoked between attempts.</param>
    /// <returns><see langword="null"/> on success, or the last <see cref="IOException"/> on give-up.</returns>
    public static IOException? DeleteWithRetries(
        string runRoot,
        WorkerWorkspaces.DeleteAttempt deleteAttempt,
        WorkerWorkspaces.RetrySleeper retrySleeper)
    {
        ArgumentNullException.ThrowIfNull(deleteAttempt);
        ArgumentNullException.ThrowIfNull(retrySleeper);

        IOException? failure = null;
        for (int attempt = 1; attempt <= DeleteRetries; attempt++)
        {
            failure = deleteAttempt(runRoot);
            if (failure is null)
            {
                return null;
            }

            retrySleeper();
        }

        return failure;
    }

    /// <summary>
    /// Sleeps <see cref="RetryDelayMillis"/> milliseconds before the next delete attempt — the default
    /// <see cref="WorkerWorkspaces.RetrySleeper"/> used by <see cref="WorkerWorkspaceCloser"/>.
    /// </summary>
    public static void SleepBeforeRetry()
    {
        Thread.Sleep((int)RetryDelayMillis);
    }
}
