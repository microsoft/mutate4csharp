namespace Microsoft.Mutate4CSharp.Model;

/// <summary>
/// The baseline test run together with its coverage report, indicating whether the coverage was
/// reused and whether a report was available.
/// </summary>
/// <param name="Baseline">The baseline test run.</param>
/// <param name="Report">The coverage report.</param>
/// <param name="Reused">Whether an existing coverage report was reused.</param>
/// <param name="ReportAvailable">Whether a coverage report was available.</param>
public sealed record CoverageRun(TestRun Baseline, CoverageReport Report, bool Reused, bool ReportAvailable);
