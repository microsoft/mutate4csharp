namespace Microsoft.Mutate4CSharp.Report;

using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// A progress reporter that discards every callback. Faithful port of mutate4java's
/// <c>NoOpProgressReporter</c>, used when live progress is suppressed.
/// </summary>
public sealed class NoOpProgressReporter : IProgressReporter
{
    /// <inheritdoc/>
    public void BaselineStarting(string moduleRoot)
    {
    }

    /// <inheritdoc/>
    public void BaselineFinished(TestRun baseline)
    {
    }

    /// <inheritdoc/>
    public void RunStarting(int totalMutations, int workerCount)
    {
    }

    /// <inheritdoc/>
    public void MutationStarting(int workerIndex, MutationJob job)
    {
    }

    /// <inheritdoc/>
    public void MutationFinished(int workerIndex, MutationResult result)
    {
    }
}
