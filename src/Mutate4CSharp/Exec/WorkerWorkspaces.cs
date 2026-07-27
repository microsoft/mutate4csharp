namespace Microsoft.Mutate4CSharp.Exec;

/// <summary>
/// Handle over the per-run worker copies produced by <see cref="CopiedWorkspaceManager"/>: it exposes
/// the shared run directory and the worker roots, and deletes the run directory on
/// <see cref="Dispose"/> — the faithful analog of mutate4java's <c>WorkerWorkspaces</c> record
/// (<c>AutoCloseable</c>).
/// </summary>
/// <remarks>
/// This is the minimal T13 slice needed to type <see cref="IWorkspaceManager.CreateWorkerWorkspaces"/>
/// and satisfy the copy/cleanup contract asserted by <c>CopiedWorkspaceManagerTest</c>. mutate4java's
/// full cleanup decomposition — the retrying <c>WorkerCleanup</c> / <c>WorkerDirectoryDelete</c> /
/// <c>WorkerWorkspaceCloser</c> collaborators and their injected delete/sleep seams — lands with the
/// parallel worker pool in T14, which owns <c>WorkerWorkspacesTest</c>.
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
    /// Deletes the run directory (and every worker copy beneath it) if it still exists.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_runRoot))
        {
            Directory.Delete(_runRoot, recursive: true);
        }
    }
}
