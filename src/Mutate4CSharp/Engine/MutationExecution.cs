namespace Microsoft.Mutate4CSharp.Engine;

using Microsoft.Mutate4CSharp.Exec;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Report;

/// <summary>
/// Turns the covered mutation sites into per-worker jobs and runs them across a parallel pool of
/// isolated repo-root copies. Faithful port of mutate4java's package-private <c>MutationExecution</c>,
/// with the R-C repo-root adaptation: mutate4java relativized each site against, and copied, the Maven
/// <em>module</em> root; the C# port relativizes against, and copies, the <em>repo/workspace root</em>
/// (<c>repoRoot</c>). Both the job's <see cref="MutationJob.SourceRelativePath"/> and the
/// per-worker copies therefore share the single repo-root-relative base, so each worker mutates and
/// tests <c>workerRoot/&lt;relativePath&gt;</c> — its own copy — never the un-mutated original.
/// </summary>
public sealed class MutationExecution
{
    private readonly IWorkspaceManager _workspaceManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MutationExecution"/> class.
    /// </summary>
    /// <param name="workspaceManager">The workspace manager that creates the per-worker copies.</param>
    public MutationExecution(IWorkspaceManager workspaceManager)
    {
        ArgumentNullException.ThrowIfNull(workspaceManager);
        _workspaceManager = workspaceManager;
    }

    /// <summary>
    /// Runs every covered site as an isolated mutant across a parallel worker pool and returns the
    /// aggregated per-mutant results.
    /// </summary>
    /// <param name="repoRoot">The repo/workspace root the worker copies are made from.</param>
    /// <param name="sites">The covered mutation sites to run.</param>
    /// <param name="timeoutMillis">The per-mutant timeout in milliseconds.</param>
    /// <param name="maxWorkers">The maximum parallel worker count.</param>
    /// <param name="progressReporter">The progress reporter for live run callbacks.</param>
    /// <param name="testExecutor">The executor each worker uses to test its mutant.</param>
    /// <returns>The per-mutant results.</returns>
    public IReadOnlyList<MutationResult> Run(
        string repoRoot,
        IReadOnlyList<MutationSite> sites,
        long timeoutMillis,
        int maxWorkers,
        IProgressReporter progressReporter,
        ITestCommandExecutor testExecutor)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(progressReporter);
        ArgumentNullException.ThrowIfNull(testExecutor);
        List<MutationJob> jobs = [];
        for (int i = 0; i < sites.Count; i++)
        {
            MutationSite site = sites[i];
            jobs.Add(new MutationJob(
                site, Path.GetRelativePath(repoRoot, site.File), timeoutMillis, i, sites.Count));
        }

        int workerCount = Math.Max(1, Math.Min(jobs.Count, maxWorkers));
        using WorkerWorkspaces workspaces = _workspaceManager.CreateWorkerWorkspaces(repoRoot, workerCount);
        using ParallelWorkerPool pool = new(workspaces.WorkerRoots, testExecutor, progressReporter);
        return pool.RunAll(jobs);
    }

    /// <summary>
    /// Computes the per-mutant timeout from the baseline duration and the timeout factor.
    /// </summary>
    /// <param name="baselineDurationMillis">The baseline run's wall-clock duration in milliseconds.</param>
    /// <param name="timeoutFactor">The baseline multiplier.</param>
    /// <returns>The per-mutant timeout in milliseconds (never below <c>1000</c>).</returns>
    public long TimeoutMillis(long baselineDurationMillis, int timeoutFactor)
    {
        long baseline = Math.Max(1L, baselineDurationMillis);
        return Math.Max(1_000L, baseline * timeoutFactor);
    }
}
