namespace Microsoft.Mutate4CSharp.Exec;

using System.Globalization;

/// <summary>
/// Faithful port of mutate4java's <c>CopiedWorkspaceManager</c>: creates one isolated
/// <see cref="ModuleTreeCopier"/> copy of the project tree per worker under a fresh, unique run
/// directory. Two C# adaptations from <c>docs/decisions.md</c> preserve the class decomposition while
/// fitting the .NET ecosystem:
/// <list type="bullet">
/// <item><description>mutate4java copies the Maven module root; the port copies the repo/workspace
/// root (<c>copyRoot</c>) so every <c>&lt;ProjectReference&gt;</c>, <c>Directory.Build.*</c>,
/// <c>global.json</c>, <c>nuget.config</c>, and lock file stays at a valid relative path (R-C).</description></item>
/// <item><description>mutate4java nests its worker base under the (excluded) <c>target/</c> directory;
/// the port places it under <c>%TEMP%/mutate4csharp/run-&lt;guid&gt;/worker-N</c> — <em>outside</em>
/// <c>copyRoot</c> — so the copy never recurses into its own worker directories.</description></item>
/// </list>
/// </summary>
public sealed class CopiedWorkspaceManager : IWorkspaceManager
{
    private readonly ModuleTreeCopier _copier = new();

    /// <inheritdoc />
    public WorkerWorkspaces CreateWorkerWorkspaces(string copyRoot, int workerCount)
    {
        ArgumentNullException.ThrowIfNull(copyRoot);

        string workersBase = Path.Combine(Path.GetTempPath(), "mutate4csharp");
        Directory.CreateDirectory(workersBase);
        string runRoot = Path.Combine(workersBase, "run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);

        List<string> workerRoots = [];
        for (int worker = 1; worker <= workerCount; worker++)
        {
            string workerRoot = Path.Combine(runRoot, "worker-" + worker.ToString(CultureInfo.InvariantCulture));
            _copier.Copy(copyRoot, workerRoot, workersBase);
            workerRoots.Add(workerRoot);
        }

        return new WorkerWorkspaces(runRoot, workerRoots);
    }
}
