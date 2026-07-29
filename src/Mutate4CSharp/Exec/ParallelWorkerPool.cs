namespace Microsoft.Mutate4CSharp.Exec;

using System.Threading.Channels;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Report;

/// <summary>
/// Faithful port of mutate4java's <c>ParallelWorkerPool</c>: N isolated workers drain a single shared
/// job queue, and their results are aggregated and ordered by <see cref="MutationResult.Order"/>.
/// </summary>
/// <remarks>
/// This is the greenlit P3 adaptation from <c>docs/decisions.md</c>: mutate4java's fixed
/// <c>ExecutorService</c> thread pool + <c>ConcurrentLinkedQueue</c> + <c>Future</c> become a
/// <see cref="Channel{T}"/> shared queue drained by <see cref="Task"/> workers — identical scheduling
/// semantics (N workers, one shared queue, results ordered by <c>order</c>). All jobs are written and
/// the channel completed before the workers start, so each worker's <see cref="ChannelReader{T}.TryRead"/>
/// loop is the faithful analog of mutate4java's <c>queue.poll()</c> drain. There is no thread pool to
/// shut down, so <see cref="Dispose"/> (mutate4java's <c>close()</c> → <c>shutdownNow()</c>) is a no-op.
/// </remarks>
public sealed class ParallelWorkerPool : IWorkerPool
{
    private readonly IReadOnlyList<string> _workerRoots;
    private readonly ITestCommandExecutor _executor;
    private readonly IProgressReporter _progressReporter;
    private readonly WorkerFutureCollector _futureCollector = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelWorkerPool"/> class.
    /// </summary>
    /// <param name="workerRoots">The per-worker copy roots; the pool runs one worker per root.</param>
    /// <param name="executor">The executor each worker uses to run its mutant's tests.</param>
    /// <param name="progressReporter">The progress reporter for live run/mutation callbacks.</param>
    public ParallelWorkerPool(
        IReadOnlyList<string> workerRoots,
        ITestCommandExecutor executor,
        IProgressReporter progressReporter)
    {
        ArgumentNullException.ThrowIfNull(workerRoots);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(progressReporter);
        _workerRoots = [.. workerRoots];
        _executor = executor;
        _progressReporter = progressReporter;
    }

    /// <inheritdoc/>
    public IReadOnlyList<MutationResult> RunAll(IReadOnlyList<MutationJob> jobs)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        _progressReporter.RunStarting(jobs.Count, _workerRoots.Count);

        Channel<MutationJob> queue = Channel.CreateUnbounded<MutationJob>(
            new UnboundedChannelOptions { SingleWriter = true, SingleReader = false });
        foreach (MutationJob job in jobs)
        {
            queue.Writer.TryWrite(job);
        }

        queue.Writer.Complete();

        List<Task<List<MutationResult>>> workerTasks = [];
        for (int index = 0; index < _workerRoots.Count; index++)
        {
            int workerIndex = index + 1;
            string workerRoot = _workerRoots[index];
            workerTasks.Add(Task.Run(() => RunWorker(workerRoot, workerIndex, queue.Reader)));
        }

        return _futureCollector.Collect(workerTasks);
    }

    /// <summary>
    /// No-op cleanup: the workers are <see cref="Task"/>s that complete when the shared queue drains,
    /// so there is no thread pool to shut down (mutate4java's <c>close()</c> → <c>shutdownNow()</c>).
    /// </summary>
    public void Dispose()
    {
    }

    private List<MutationResult> RunWorker(string workerRoot, int workerIndex, ChannelReader<MutationJob> reader)
    {
        List<MutationResult> results = [];
        using IsolatedMutationWorker worker = new(workerRoot, _executor, _progressReporter, workerIndex);
        while (reader.TryRead(out MutationJob? job))
        {
            results.Add(worker.Run(job));
        }

        return results;
    }
}
