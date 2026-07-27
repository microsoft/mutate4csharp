namespace Microsoft.Mutate4CSharp.Engine;

using Microsoft.Mutate4CSharp.Coverage;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Project;
using Microsoft.Mutate4CSharp.Report;

/// <summary>
/// Runs (or reuses) the baseline test + coverage step and reports a failed baseline. Faithful port of
/// mutate4java's package-private <c>BaselineRunner</c>, adapted to the C# ecosystem: the baseline runs
/// with the working directory pinned to the resolved test project's own directory (DD3), the coverage
/// producer is the <see cref="ICoverageRunner"/> seam, and the reuse diagnostics no longer name a
/// fixed JaCoCo report path.
/// </summary>
public sealed class BaselineRunner
{
    private readonly ICoverageRunner _coverageRunner;
    private readonly TextWriter _err;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaselineRunner"/> class.
    /// </summary>
    /// <param name="coverageRunner">The coverage runner used on the fresh and reuse paths.</param>
    /// <param name="error">The writer baseline-failure and reuse diagnostics are printed to.</param>
    public BaselineRunner(ICoverageRunner coverageRunner, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(coverageRunner);
        ArgumentNullException.ThrowIfNull(error);
        _coverageRunner = coverageRunner;
        _err = error;
    }

    /// <summary>
    /// Produces the baseline run and coverage report for the module, choosing the fresh, reuse, or
    /// custom-<c>--test-command</c> path per the parsed arguments.
    /// </summary>
    /// <param name="parsed">The parsed CLI arguments.</param>
    /// <param name="executor">The test executor (already bound to any <c>--test-command</c> override).</param>
    /// <param name="module">The resolved module under test.</param>
    /// <param name="progressReporter">The progress reporter for baseline start/finish callbacks.</param>
    /// <returns>The baseline run and its coverage report.</returns>
    public CoverageRun Run(
        CliArguments parsed,
        ITestCommandExecutor executor,
        ModuleResolution module,
        IProgressReporter progressReporter)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(progressReporter);
        string testProjectDirectory = TestProjectDirectory(module);
        progressReporter.BaselineStarting(testProjectDirectory);
        CoverageRun coverageRun;
        if (parsed.TestCommand is null)
        {
            if (parsed.ReuseCoverage)
            {
                TestRun baseline = executor.RunTests(testProjectDirectory, 0L);
                CoverageRun reusedCoverage = _coverageRunner.GenerateCoverage(module, true);
                coverageRun = new CoverageRun(
                    baseline, reusedCoverage.Report, true, reusedCoverage.ReportAvailable, 0);
                PrintReuseMessage(coverageRun.ReportAvailable);
            }
            else
            {
                coverageRun = _coverageRunner.GenerateCoverage(module, false);
            }
        }
        else
        {
            coverageRun = new CoverageRun(
                executor.RunTests(testProjectDirectory, 0L), CoverageReport.AllCovered(), false, false, 0);
        }

        progressReporter.BaselineFinished(coverageRun.Baseline!);
        return coverageRun;
    }

    /// <summary>
    /// Reports a failed baseline to standard error and returns the fail-fast exit code.
    /// </summary>
    /// <param name="baseline">The failed baseline run.</param>
    /// <returns>Exit code <c>2</c>.</returns>
    public int Fail(TestRun baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (baseline.TimedOut)
        {
            _err.Write("Baseline tests timed out.\n");
        }

        _err.Write("Baseline tests failed.\n");
        _err.Write(baseline.Output);
        return 2;
    }

    private static string TestProjectDirectory(ModuleResolution module)
    {
        string testProjectFile = module.TestProjectFile
            ?? throw new ArgumentException("Module resolution has no test project file.", nameof(module));
        return Path.GetDirectoryName(testProjectFile)!;
    }

    private void PrintReuseMessage(bool reportAvailable)
    {
        if (reportAvailable)
        {
            _err.Write("Reusing existing coverage data.\n");
            _err.Write(
                "Warning: coverage may be stale; covered/uncovered site classification may be inaccurate.\n");
            return;
        }

        _err.Write(
            "Coverage reuse requested, but no existing coverage report was found. "
            + "Continuing without coverage filtering.\n");
    }
}
