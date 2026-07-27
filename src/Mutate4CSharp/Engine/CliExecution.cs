namespace Microsoft.Mutate4CSharp.Engine;

using System.Globalization;
using Microsoft.Mutate4CSharp.Analysis;
using Microsoft.Mutate4CSharp.Coverage;
using Microsoft.Mutate4CSharp.Manifest;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Project;
using Microsoft.Mutate4CSharp.Report;
using Microsoft.Mutate4CSharp.Selection;

/// <summary>
/// The engine's top-level orchestration: resolve the context, short-circuit the scan / update-manifest
/// modes, run the baseline + coverage, and drive the mutation run and its outcome. Faithful port of
/// mutate4java's <c>CliExecution</c> with the approved DD2 fail-fast departures folded in — a missing
/// owning project, a missing unit test project, a failed baseline, or (on the normal fresh coverage
/// path only) a baseline that executed zero unit tests each return exit <c>2</c> with a distinct
/// stderr message instead of mutate4java's warn-and-continue.
/// </summary>
public sealed class CliExecution
{
    private readonly string _workspaceRoot;
    private readonly TextWriter _out;
    private readonly TextWriter _err;
    private readonly ITestCommandExecutor _testExecutor;
    private readonly IProgressReporter _verboseProgressReporter;
    private readonly MutationCatalog _catalog;
    private readonly ProjectLayout _layout;
    private readonly BaselineRunner _baselineRunner;
    private readonly MutationRunPlanner _mutationRunPlanner;
    private readonly ScanMode _scanMode;
    private readonly ExecutionOutcomeWriter _outcomeWriter;
    private readonly ManifestWriter _manifestWriter;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliExecution"/> class from its fully injected
    /// collaborators (assembled by <see cref="CliExecutionFactory"/>).
    /// </summary>
    /// <param name="workspaceRoot">The repo/workspace root.</param>
    /// <param name="output">The standard-output writer.</param>
    /// <param name="error">The standard-error writer.</param>
    /// <param name="testExecutor">The base test executor.</param>
    /// <param name="coverageRunner">The coverage runner seam.</param>
    /// <param name="verboseProgressReporter">The reporter used when <c>--verbose</c> is set.</param>
    /// <param name="catalog">The mutation catalog.</param>
    /// <param name="formatter">The report formatter.</param>
    /// <param name="manifestSupport">The manifest support.</param>
    /// <param name="layout">The project layout.</param>
    /// <param name="selector">The differential selector.</param>
    /// <param name="coverageFilter">The coverage filter.</param>
    /// <param name="mutationExecution">The isolated mutation runner.</param>
    /// <param name="scanReportFormatter">The scan report formatter.</param>
    public CliExecution(
        string workspaceRoot,
        TextWriter output,
        TextWriter error,
        ITestCommandExecutor testExecutor,
        ICoverageRunner coverageRunner,
        IProgressReporter verboseProgressReporter,
        MutationCatalog catalog,
        ReportFormatter formatter,
        ManifestSupport manifestSupport,
        ProjectLayout layout,
        DifferentialSelector selector,
        MutationCoverageFilter coverageFilter,
        MutationExecution mutationExecution,
        ScanReportFormatter scanReportFormatter)
    {
        ArgumentNullException.ThrowIfNull(workspaceRoot);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(testExecutor);
        ArgumentNullException.ThrowIfNull(coverageRunner);
        ArgumentNullException.ThrowIfNull(verboseProgressReporter);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(formatter);
        ArgumentNullException.ThrowIfNull(manifestSupport);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(coverageFilter);
        ArgumentNullException.ThrowIfNull(mutationExecution);
        ArgumentNullException.ThrowIfNull(scanReportFormatter);
        _workspaceRoot = workspaceRoot;
        _out = output;
        _err = error;
        _testExecutor = testExecutor;
        _verboseProgressReporter = verboseProgressReporter;
        _catalog = catalog;
        _layout = layout;
        _baselineRunner = new BaselineRunner(coverageRunner, error);
        LineFilter lineFilter = new();
        _mutationRunPlanner = new MutationRunPlanner(
            selector, coverageFilter, mutationExecution, new ExecutionMessages(), lineFilter);
        _scanMode = new ScanMode(selector, scanReportFormatter, lineFilter);
        _manifestWriter = new ManifestWriter(manifestSupport);
        _outcomeWriter = new ExecutionOutcomeWriter(workspaceRoot, output, formatter, _manifestWriter);
    }

    /// <summary>
    /// Executes a parsed request end to end and returns the process exit code.
    /// </summary>
    /// <param name="parsed">The parsed CLI arguments.</param>
    /// <returns>
    /// <c>0</c> on success (or scan / update-manifest / all-killed / all-uncovered); <c>2</c> for a
    /// DD2 fail-fast (no owning project, no test project, failed baseline, or zero executed tests);
    /// <c>3</c> when at least one mutant survived.
    /// </returns>
    public int Execute(CliArguments parsed)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ExecutionContext context = ExecutionContext.Create(
            parsed, _testExecutor, _verboseProgressReporter, _layout, _catalog);
        if (parsed.Scan)
        {
            _out.Write(_scanMode.Render(parsed, context.SourceFile, context.Analysis));
            return 0;
        }

        if (parsed.UpdateManifest)
        {
            _manifestWriter.Write(context.SourceFile, context.Analysis);
            _out.Write(string.Format(
                CultureInfo.InvariantCulture, "Updated manifest for {0}\n", Relative(context.SourceFile)));
            return 0;
        }

        int moduleFailure = CheckModule(context);
        if (moduleFailure != 0)
        {
            return moduleFailure;
        }

        CoverageRun coverageRun = _baselineRunner.Run(
            parsed, context.Executor, context.Module, context.ProgressReporter);
        TestRun baseline = coverageRun.Baseline!;
        if (!baseline.Passed())
        {
            return _baselineRunner.Fail(baseline);
        }

        if (RequiresFreshTests(parsed) && coverageRun.ExecutedTestCount == 0)
        {
            _err.Write(
                "Baseline executed no unit tests. mutate4csharp requires the test project to run at "
                + "least one unit test.\n");
            return 2;
        }

        MutantResultSummary summary = _mutationRunPlanner.Run(
            parsed, context, _workspaceRoot, baseline, coverageRun.Report);
        return _outcomeWriter.Write(summary, context.Analysis);
    }

    private static bool RequiresFreshTests(CliArguments parsed)
    {
        return parsed.TestCommand is null && !parsed.ReuseCoverage;
    }

    private int CheckModule(ExecutionContext context)
    {
        switch (context.Module.Status)
        {
            case ModuleResolutionStatus.NoOwningProject:
                _err.Write(
                    "No owning C# project found for "
                    + Relative(context.SourceFile)
                    + ". mutate4csharp requires the target file to belong to a project (.csproj) "
                    + "under the workspace.\n");
                return 2;
            case ModuleResolutionStatus.NoTestProject:
                _err.Write(
                    "No unit test project found for '"
                    + context.Module.ProjectName
                    + "'. mutate4csharp requires a matching '.Tests'/'.UnitTests' project that "
                    + "references it.\n");
                return 2;
            default:
                return 0;
        }
    }

    private string Relative(string file)
    {
        return Path.GetRelativePath(_workspaceRoot, file).Replace('\\', '/');
    }
}
