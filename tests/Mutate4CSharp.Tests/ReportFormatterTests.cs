namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Report;

/// <summary>
/// Golden-string tests for <see cref="ReportFormatter"/>, grounded in mutate4java's
/// <c>CliApplicationTest</c> assertions (e.g. <c>UNCOVERED …</c>, <c>KILLED …</c>, <c>Coverage: N
/// uncovered sites skipped.</c>, <c>Summary: k killed, s survived, n total.</c>). Paths are rendered
/// workspace-relative with forward slashes and every number uses the invariant culture.
/// </summary>
public sealed class ReportFormatterTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "m4cs-report-root");

    private readonly ReportFormatter _formatter = new();

    /// <summary>
    /// A full report renders the baseline line, one <c>UNCOVERED</c> line, the per-mutant
    /// <c>KILLED</c>/<c>SURVIVED</c> lines with their durations, and the coverage/summary footer.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FormatsBaselineUncoveredResultsAndSummary()
    {
        TestRun baseline = new(0, string.Empty, 1234, false);
        MutationSite uncovered = Site(9, "replace == with !=");
        MutationResult killed = Result(Site(5, "replace + with -"), killed: true, durationMillis: 12, timedOut: false);
        MutationResult survived =
            Result(Site(7, "replace * with /"), killed: false, durationMillis: 8, timedOut: false);

        string report = _formatter.Format(Root, baseline, extra: string.Empty, [uncovered], [killed, survived]);

        report.Should().Be(
            "Baseline tests passed in 1234 ms.\n"
            + "UNCOVERED src/Demo/Sample.cs:9 replace == with !=\n"
            + "KILLED src/Demo/Sample.cs:5 replace + with - (12 ms)\n"
            + "SURVIVED src/Demo/Sample.cs:7 replace * with / (8 ms)\n"
            + "Coverage: 1 uncovered sites skipped.\n"
            + "Summary: 1 killed, 1 survived, 2 total.\n");
    }

    /// <summary>A timed-out mutant appends the indented <c>timed out</c> line beneath its result.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RendersTimedOutResultWithTimedOutLine()
    {
        TestRun baseline = new(0, string.Empty, 500, false);
        MutationResult timedOut =
            Result(Site(5, "replace + with -"), killed: true, durationMillis: 1000, timedOut: true);

        string report = _formatter.Format(Root, baseline, extra: null, [], [timedOut]);

        report.Should().Be(
            "Baseline tests passed in 500 ms.\n"
            + "KILLED src/Demo/Sample.cs:5 replace + with - (1000 ms)\n"
            + "  timed out\n"
            + "Coverage: 0 uncovered sites skipped.\n"
            + "Summary: 1 killed, 0 survived, 1 total.\n");
    }

    /// <summary>A non-blank extra block is inserted verbatim after the baseline line; a blank one is not.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void InsertsNonBlankExtraAfterBaselineLine()
    {
        TestRun baseline = new(0, string.Empty, 42, false);

        string withExtra = _formatter.Format(Root, baseline, "No mutations need testing.\n", [], []);
        string withBlank = _formatter.Format(Root, baseline, "   \n", [], []);

        withExtra.Should().Be(
            "Baseline tests passed in 42 ms.\n"
            + "No mutations need testing.\n"
            + "Coverage: 0 uncovered sites skipped.\n"
            + "Summary: 0 killed, 0 survived, 0 total.\n");
        withBlank.Should().Be(
            "Baseline tests passed in 42 ms.\n"
            + "Coverage: 0 uncovered sites skipped.\n"
            + "Summary: 0 killed, 0 survived, 0 total.\n");
    }

    private static MutationSite Site(int line, string description)
    {
        string file = Path.Combine(Root, "src", "Demo", "Sample.cs");
        return new MutationSite(file, line, 0, 1, "a", "b", description);
    }

    private static MutationResult Result(MutationSite site, bool killed, long durationMillis, bool timedOut)
    {
        return new MutationResult(site, killed, durationMillis, timedOut, 0, 1);
    }
}
