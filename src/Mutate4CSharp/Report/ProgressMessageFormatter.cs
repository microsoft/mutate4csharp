namespace Microsoft.Mutate4CSharp.Report;

using System.Globalization;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Builds the live progress lines emitted while a run is in flight. Faithful port of mutate4java's
/// <c>ProgressMessageFormatter</c>; mutate4java's <c>%n</c> platform newline is rendered as an
/// explicit <c>"\n"</c> and booleans as lowercase <c>true</c>/<c>false</c> to match the Java output.
/// </summary>
public sealed class ProgressMessageFormatter
{
    /// <summary>Formats the "baseline starting" line.</summary>
    /// <param name="moduleRoot">The module root under test.</param>
    /// <returns>The formatted line.</returns>
    public string BaselineStarting(string moduleRoot)
    {
        ArgumentNullException.ThrowIfNull(moduleRoot);
        return string.Format(CultureInfo.InvariantCulture, "Baseline starting for {0}\n", moduleRoot);
    }

    /// <summary>Formats the "baseline finished" line.</summary>
    /// <param name="baseline">The completed baseline run.</param>
    /// <returns>The formatted line.</returns>
    public string BaselineFinished(TestRun baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        return string.Format(
            CultureInfo.InvariantCulture,
            "Baseline finished: exit={0} timedOut={1} duration={2} ms\n",
            baseline.ExitCode,
            baseline.TimedOut ? "true" : "false",
            baseline.DurationMillis);
    }

    /// <summary>Formats the "running N mutations" line.</summary>
    /// <param name="totalMutations">The total number of mutations to run.</param>
    /// <param name="workerCount">The number of parallel workers.</param>
    /// <returns>The formatted line.</returns>
    public string RunStarting(int totalMutations, int workerCount)
    {
        return string.Format(
            CultureInfo.InvariantCulture, "Running {0} mutations with {1} workers.\n", totalMutations, workerCount);
    }

    /// <summary>Formats the "worker starting" line for a mutation job.</summary>
    /// <param name="workerIndex">The worker index.</param>
    /// <param name="job">The mutation job being started.</param>
    /// <returns>The formatted line.</returns>
    public string MutationStarting(int workerIndex, MutationJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return string.Format(
            CultureInfo.InvariantCulture,
            "Worker {0} starting {1}/{2}: {3}:{4} {5}\n",
            workerIndex,
            job.Order + 1,
            job.TotalJobs,
            job.Site.File,
            job.Site.LineNumber,
            job.Site.Description);
    }

    /// <summary>Formats the "worker finished" line for a completed mutant.</summary>
    /// <param name="workerIndex">The worker index.</param>
    /// <param name="result">The completed mutant result.</param>
    /// <returns>The formatted line.</returns>
    public string MutationFinished(int workerIndex, MutationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return string.Format(
            CultureInfo.InvariantCulture,
            "Worker {0} finished {1}/{2}: {3} {4}:{5}\n",
            workerIndex,
            result.Order + 1,
            result.TotalJobs,
            result.Killed ? "KILLED" : "SURVIVED",
            result.Site.File,
            result.Site.LineNumber);
    }
}
