namespace Microsoft.Mutate4CSharp.Exec;

/// <summary>
/// Faithful port of mutate4java's <c>WorkspaceManager</c> abstraction: creates the per-worker copies
/// of the project tree that the parallel mutation run mutates in isolation. mutate4java copies the
/// Maven <em>module</em> tree (excluding <c>target/</c>); the C# port copies the <em>repo root</em>
/// (workspace root) instead — the survivor-safe default from <c>docs/decisions.md</c> (R-C), because a
/// <c>.csproj</c>'s <c>&lt;ProjectReference&gt;</c> closure, <c>Directory.Build.*</c>, <c>global.json</c>,
/// <c>nuget.config</c>, and lock files must all remain at valid relative paths or the mutant build
/// fails and every mutant falsely scores KILLED.
/// </summary>
public interface IWorkspaceManager
{
    /// <summary>
    /// Creates <paramref name="workerCount"/> isolated copies of <paramref name="copyRoot"/>, one per
    /// worker, and returns a handle that owns their shared run directory (and its cleanup).
    /// </summary>
    /// <param name="copyRoot">The root to copy — the repo/workspace root in the C# port.</param>
    /// <param name="workerCount">The number of worker copies to create.</param>
    /// <returns>A handle over the created worker roots and their shared run directory.</returns>
    WorkerWorkspaces CreateWorkerWorkspaces(string copyRoot, int workerCount);
}
