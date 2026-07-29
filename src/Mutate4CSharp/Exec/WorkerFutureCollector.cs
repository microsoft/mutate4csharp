namespace Microsoft.Mutate4CSharp.Exec;

using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Faithful port of mutate4java's package-private <c>WorkerFutureCollector</c>: waits for every worker
/// to finish, aggregates their results, and sorts them by <see cref="MutationResult.Order"/>.
/// </summary>
/// <remarks>
/// mutate4java collects each <c>Future</c> in turn and, on an <c>ExecutionException</c>, rethrows the
/// worker's cause when it is an <c>Exception</c> or wraps a non-<c>Exception</c> <c>Throwable</c> (e.g.
/// an <c>Error</c>). .NET has no <c>Error</c>/<c>Exception</c> split — every fault derives from
/// <see cref="Exception"/> — so that branch collapses: awaiting <see cref="Task.WhenAll{TResult}(System.Collections.Generic.IEnumerable{Task{TResult}})"/>
/// surfaces the first faulted worker's original exception unwrapped, which is the faithful analog of
/// rethrowing the cause. Waiting for the whole batch first also guarantees no worker keeps mutating
/// its copy after <see cref="ParallelWorkerPool.RunAll"/> returns.
/// </remarks>
public sealed class WorkerFutureCollector
{
    /// <summary>
    /// Waits for all <paramref name="workerTasks"/>, aggregates their results, and returns them sorted
    /// by <see cref="MutationResult.Order"/>. If any worker faulted, its original exception is
    /// rethrown once every worker has settled.
    /// </summary>
    /// <param name="workerTasks">The per-worker result tasks.</param>
    /// <returns>The aggregated results, ordered by run position.</returns>
    public IReadOnlyList<MutationResult> Collect(IReadOnlyList<Task<List<MutationResult>>> workerTasks)
    {
        ArgumentNullException.ThrowIfNull(workerTasks);

        // Block until every worker has settled, then index the aggregated array (never a per-task
        // .Result) — the CA1849-safe Task.WhenAll pattern carried from T12.
        List<MutationResult>[] perWorker = Task.WhenAll(workerTasks).GetAwaiter().GetResult();

        List<MutationResult> results = [];
        foreach (List<MutationResult> workerResults in perWorker)
        {
            results.AddRange(workerResults);
        }

        results.Sort(static (left, right) => left.Order.CompareTo(right.Order));
        return results.AsReadOnly();
    }
}
