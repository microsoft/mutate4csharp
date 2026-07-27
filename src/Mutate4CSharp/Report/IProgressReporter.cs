namespace Microsoft.Mutate4CSharp.Report;

using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Receives live progress callbacks as a run proceeds. Faithful port of mutate4java's
/// <c>ProgressReporter</c> interface (I-prefixed per this repository's interface convention).
/// </summary>
public interface IProgressReporter
{
    /// <summary>Signals that the baseline test run is starting.</summary>
    /// <param name="moduleRoot">The module root under test.</param>
    void BaselineStarting(string moduleRoot);

    /// <summary>Signals that the baseline test run has finished.</summary>
    /// <param name="baseline">The completed baseline run.</param>
    void BaselineFinished(TestRun baseline);

    /// <summary>Signals that the mutation run is starting.</summary>
    /// <param name="totalMutations">The total number of mutations to run.</param>
    /// <param name="workerCount">The number of parallel workers.</param>
    void RunStarting(int totalMutations, int workerCount);

    /// <summary>Signals that a worker is starting a mutation job.</summary>
    /// <param name="workerIndex">The worker index.</param>
    /// <param name="job">The mutation job being started.</param>
    void MutationStarting(int workerIndex, MutationJob job);

    /// <summary>Signals that a worker has finished a mutant.</summary>
    /// <param name="workerIndex">The worker index.</param>
    /// <param name="result">The completed mutant result.</param>
    void MutationFinished(int workerIndex, MutationResult result);
}
