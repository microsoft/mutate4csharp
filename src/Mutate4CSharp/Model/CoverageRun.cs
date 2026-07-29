namespace Microsoft.Mutate4CSharp.Model;

/// <summary>
/// The baseline test run together with its coverage report, indicating whether the coverage was
/// reused and whether a report was available. <paramref name="Baseline"/> is <see langword="null"/>
/// on the reuse path (no fresh baseline was executed), faithful to mutate4java's nullable
/// <c>baseline</c>.
/// </summary>
/// <param name="Baseline">The baseline test run, or <see langword="null"/> when coverage was reused.</param>
/// <param name="Report">The coverage report.</param>
/// <param name="Reused">Whether an existing coverage report was reused.</param>
/// <param name="ReportAvailable">Whether a coverage report was available.</param>
/// <param name="ExecutedTestCount">
/// The number of tests the fresh baseline run executed, read from the produced TRX (DD2(b)); it lets
/// the engine fail fast with exit <c>2</c> when the baseline executes zero unit tests. It is <c>0</c>
/// whenever no fresh baseline+coverage run was performed (the reuse path returns a <c>0</c> count with
/// a <see langword="null"/> <paramref name="Baseline"/>).
/// </param>
public sealed record CoverageRun(
    TestRun? Baseline, CoverageReport Report, bool Reused, bool ReportAvailable, int ExecutedTestCount);
