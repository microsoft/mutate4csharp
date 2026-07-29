namespace Microsoft.Mutate4CSharp.Exec;

using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Report;

/// <summary>
/// Faithful port of mutate4java's package-private <c>IsolatedMutationWorker</c>: for each
/// <see cref="MutationJob"/> it splices the site's replacement into this worker's private copy of the
/// source file, runs the test command against the worker copy, classifies the outcome, then restores
/// the original source before the next job.
/// </summary>
/// <remarks>
/// The mutated file resolves as <c>Path.Combine(workerModuleRoot, job.SourceRelativePath)</c>. The
/// caller (the run planner) computes <see cref="MutationJob.SourceRelativePath"/> as
/// <c>Path.GetRelativePath(copyRoot, site.File)</c> with <c>copyRoot</c> == the repo root the worker
/// copies were made from, so this combine lands on the copied file — the single repo-root relative
/// base shared with <see cref="CopiedWorkspaceManager"/> (Anders' T13 invariant).
/// </remarks>
public sealed class IsolatedMutationWorker : IMutationWorker
{
    private readonly string _workerModuleRoot;
    private readonly ITestCommandExecutor _executor;
    private readonly IProgressReporter _progressReporter;
    private readonly int _workerIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="IsolatedMutationWorker"/> class.
    /// </summary>
    /// <param name="workerModuleRoot">This worker's private copy root (the repo-root relative base).</param>
    /// <param name="executor">The executor that runs the test command for a mutant.</param>
    /// <param name="progressReporter">The progress reporter for live start/finish callbacks.</param>
    /// <param name="workerIndex">This worker's 1-based index within the pool.</param>
    public IsolatedMutationWorker(
        string workerModuleRoot,
        ITestCommandExecutor executor,
        IProgressReporter progressReporter,
        int workerIndex)
    {
        ArgumentNullException.ThrowIfNull(workerModuleRoot);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(progressReporter);
        _workerModuleRoot = workerModuleRoot;
        _executor = executor;
        _progressReporter = progressReporter;
        _workerIndex = workerIndex;
    }

    /// <inheritdoc/>
    public MutationResult Run(MutationJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        _progressReporter.MutationStarting(_workerIndex, job);
        string workerFile = Path.Combine(_workerModuleRoot, job.SourceRelativePath);
        string original = File.ReadAllText(workerFile);
        File.WriteAllText(workerFile, MutatedSource(original, job.Site));
        try
        {
            TestRun run = _executor.RunTests(_workerModuleRoot, job.TimeoutMillis);
            MutationResult result = new(
                job.Site,
                !run.Passed(),
                run.DurationMillis,
                run.TimedOut,
                job.Order,
                job.TotalJobs);
            _progressReporter.MutationFinished(_workerIndex, result);
            return result;
        }
        finally
        {
            File.WriteAllText(workerFile, original);
        }
    }

    /// <summary>
    /// No-op — this worker owns no resources. The faithful analog of mutate4java's default
    /// <c>MutationWorker.close()</c>; declared here (on the sealed worker) rather than as a
    /// default-interface method to keep CA1816 satisfied.
    /// </summary>
    public void Dispose()
    {
    }

    private string MutatedSource(string source, MutationSite site)
    {
        return source[..site.Start] + site.ReplacementText + source[site.End..];
    }
}
