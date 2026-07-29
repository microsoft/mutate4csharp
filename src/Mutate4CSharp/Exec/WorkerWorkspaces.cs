namespace Microsoft.Mutate4CSharp.Exec;

/// <summary>
/// Handle over the per-run worker copies produced by <see cref="CopiedWorkspaceManager"/>: it exposes
/// the shared run directory and the worker roots, and deletes the run directory on
/// <see cref="Dispose"/> — the faithful analog of mutate4java's <c>WorkerWorkspaces</c> record
/// (<c>AutoCloseable</c>).
/// </summary>
/// <remarks>
/// mutate4java's <c>WorkerWorkspaces</c> is a <c>record</c>, but the C# port keeps it a
/// <c>sealed class</c> (Anders' T13 ruling): it is a reference-identity resource handle, not a value,
/// so a record over its <see cref="IReadOnlyList{T}"/> / handle fields would emit a misleading
/// reference-based <c>Equals</c> nobody should call. The full cleanup decomposition — the retrying
/// <see cref="WorkerCleanup"/> / <see cref="WorkerDirectoryDelete"/> / <see cref="WorkerWorkspaceCloser"/>
/// collaborators reached through the injected <see cref="DeleteAttempt"/> / <see cref="RetrySleeper"/> /
/// <see cref="DeleteTree"/> seams — makes the Windows file-lock retry testable deterministically.
/// </remarks>
public sealed class WorkerWorkspaces : IDisposable
{
    private readonly string _runRoot;
    private readonly IReadOnlyList<string> _workerRoots;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerWorkspaces"/> class.
    /// </summary>
    /// <param name="runRoot">The shared run directory that owns every worker root.</param>
    /// <param name="workerRoots">The per-worker copy roots.</param>
    public WorkerWorkspaces(string runRoot, IReadOnlyList<string> workerRoots)
    {
        ArgumentNullException.ThrowIfNull(runRoot);
        ArgumentNullException.ThrowIfNull(workerRoots);
        _runRoot = runRoot;
        _workerRoots = [.. workerRoots];
    }

    /// <summary>Gets the shared run directory that owns every worker root.</summary>
    public string RunRoot => _runRoot;

    /// <summary>Gets the per-worker copy roots.</summary>
    public IReadOnlyList<string> WorkerRoots => _workerRoots;

    /// <summary>
    /// Deletes the run directory (and every worker copy beneath it) via the retrying cleanup — the
    /// faithful analog of mutate4java's <c>close()</c>. Does nothing when it is already gone.
    /// </summary>
    /// <exception cref="InvalidOperationException">The run directory could not be deleted after retries.</exception>
    public void Dispose()
    {
        new WorkerWorkspaceCloser().Close(_runRoot);
    }

    /// <summary>
    /// Tries to delete <paramref name="runRoot"/> with the default recursive delete, returning the
    /// <see cref="IOException"/> it fails with (or <see langword="null"/> on success). Faithful port of
    /// mutate4java's static <c>tryDelete(runRoot)</c>.
    /// </summary>
    /// <param name="runRoot">The run directory to delete.</param>
    /// <returns>The failure, or <see langword="null"/> on success.</returns>
    public static IOException? TryDelete(string runRoot)
    {
        return TryDelete(runRoot, new WorkerDirectoryDelete().Delete);
    }

    /// <summary>
    /// Tries to delete <paramref name="runRoot"/> with the injected <paramref name="deleteTree"/>,
    /// returning the <see cref="IOException"/> it fails with (or <see langword="null"/> on success) —
    /// the seam that makes the Windows file-lock case testable. Faithful port of mutate4java's static
    /// <c>tryDelete(runRoot, deleteTree)</c>. A permission denial (<see cref="UnauthorizedAccessException"/>)
    /// from a lingering handle is normalized to a retryable <see cref="IOException"/> so it is retried
    /// the same way as a sharing violation.
    /// </summary>
    /// <param name="runRoot">The run directory to delete.</param>
    /// <param name="deleteTree">The delete seam.</param>
    /// <returns>The failure, or <see langword="null"/> on success.</returns>
    public static IOException? TryDelete(string runRoot, DeleteTree deleteTree)
    {
        ArgumentNullException.ThrowIfNull(deleteTree);
        try
        {
            deleteTree(runRoot);
            return null;
        }
        catch (IOException ex)
        {
            return ex;
        }
        catch (UnauthorizedAccessException ex)
        {
            // A real dotnet-test worker leaves a testhost / VBCSCompiler handle (or a read-only build
            // artifact) that can surface the still-locked tree as a permission denial rather than a
            // sharing violation. Java's AccessDeniedException extends IOException and is retried; the
            // .NET UnauthorizedAccessException does not, so normalize it to the same retryable
            // IOException (Anders' T14 proposal) — DeleteWithRetries then tolerates it identically.
            return new IOException(ex.Message, ex);
        }
    }

    /// <summary>
    /// Deletes <paramref name="runRoot"/> with the injected delete/sleep seams, retrying transient
    /// failures. Faithful port of mutate4java's static <c>deleteWithRetries</c>, delegating to
    /// <see cref="WorkerCleanup.DeleteWithRetries"/>.
    /// </summary>
    /// <param name="runRoot">The run directory to delete.</param>
    /// <param name="deleteAttempt">The delete seam, returning the failure or <see langword="null"/>.</param>
    /// <param name="retrySleeper">The sleep seam invoked between attempts.</param>
    /// <returns><see langword="null"/> on success, or the last failure once the retry limit is reached.</returns>
    public static IOException? DeleteWithRetries(string runRoot, DeleteAttempt deleteAttempt, RetrySleeper retrySleeper)
    {
        return WorkerCleanup.DeleteWithRetries(runRoot, deleteAttempt, retrySleeper);
    }

    /// <summary>
    /// A single delete attempt: returns the <see cref="IOException"/> it failed with, or
    /// <see langword="null"/> on success. Port of mutate4java's <c>DeleteAttempt</c> functional interface.
    /// </summary>
    /// <param name="runRoot">The run directory to delete.</param>
    /// <returns>The failure, or <see langword="null"/> on success.</returns>
    public delegate IOException? DeleteAttempt(string runRoot);

    /// <summary>
    /// Sleeps between delete attempts. Port of mutate4java's <c>RetrySleeper</c> functional interface.
    /// </summary>
    public delegate void RetrySleeper();

    /// <summary>
    /// Deletes a directory tree, throwing <see cref="IOException"/> on failure. Port of mutate4java's
    /// <c>DeleteTree</c> functional interface.
    /// </summary>
    /// <param name="runRoot">The directory tree to delete.</param>
    public delegate void DeleteTree(string runRoot);
}
