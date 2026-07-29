namespace Microsoft.Mutate4CSharp.Model;

/// <summary>
/// The outcome for a single mutant: whether it was killed, how long it took, whether it timed
/// out, and its position within the run.
/// </summary>
/// <param name="Site">The mutation site.</param>
/// <param name="Killed">Whether the mutant was killed by the tests.</param>
/// <param name="DurationMillis">The wall-clock duration in milliseconds.</param>
/// <param name="TimedOut">Whether the mutant run timed out.</param>
/// <param name="Order">This mutant's 0-based position within the run (progress display renders it as <c>Order + 1</c>).</param>
/// <param name="TotalJobs">The total number of jobs in the run.</param>
public sealed record MutationResult(
    MutationSite Site,
    bool Killed,
    long DurationMillis,
    bool TimedOut,
    int Order,
    int TotalJobs);
