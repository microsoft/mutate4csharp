namespace Microsoft.Mutate4CSharp.Exec;

using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Faithful port of mutate4java's package-private <c>MutationWorker</c> interface: runs a single
/// <see cref="MutationJob"/> and returns its result. mutate4java's <c>AutoCloseable</c> carries a
/// <c>default</c> no-op <c>close()</c> so implementations need not override it; the C# port keeps the
/// no-op on the (sealed, resource-free) <see cref="IsolatedMutationWorker"/> instead, because a
/// default-interface <see cref="IDisposable.Dispose"/> would trip CA1816 (a derived finalizer could
/// need to re-implement it).
/// </summary>
public interface IMutationWorker : IDisposable
{
    /// <summary>
    /// Runs the given <paramref name="job"/> against this worker's isolated copy and returns its
    /// result.
    /// </summary>
    /// <param name="job">The mutation job to run.</param>
    /// <returns>The mutant's result.</returns>
    MutationResult Run(MutationJob job);
}
