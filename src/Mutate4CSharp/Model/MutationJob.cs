namespace Microsoft.Mutate4CSharp.Model;

/// <summary>
/// A unit of mutation work: the site to mutate, the source path relative to the workspace root,
/// the per-mutant timeout, and this job's position within the run.
/// </summary>
/// <param name="Site">The mutation site.</param>
/// <param name="SourceRelativePath">The source path relative to the workspace root.</param>
/// <param name="TimeoutMillis">The per-mutant timeout in milliseconds.</param>
/// <param name="Order">This job's 1-based order within the run.</param>
/// <param name="TotalJobs">The total number of jobs in the run.</param>
public sealed record MutationJob(
    MutationSite Site,
    string SourceRelativePath,
    long TimeoutMillis,
    int Order,
    int TotalJobs);
