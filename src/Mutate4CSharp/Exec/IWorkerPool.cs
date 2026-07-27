namespace Microsoft.Mutate4CSharp.Exec;

using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Faithful port of mutate4java's <c>WorkerPool</c> interface (<c>AutoCloseable</c> → <see cref="IDisposable"/>):
/// runs every <see cref="MutationJob"/> across a set of isolated workers and returns the aggregated
/// per-mutant results. The concrete implementation is <see cref="ParallelWorkerPool"/>.
/// </summary>
public interface IWorkerPool : IDisposable
{
    /// <summary>
    /// Runs all <paramref name="jobs"/> across the pool's workers and returns their aggregated results
    /// ordered by <see cref="MutationResult.Order"/> (faithful to the run's scheduling order).
    /// </summary>
    /// <param name="jobs">The mutation jobs to run.</param>
    /// <returns>The per-mutant results, ordered by their run position.</returns>
    IReadOnlyList<MutationResult> RunAll(IReadOnlyList<MutationJob> jobs);
}
