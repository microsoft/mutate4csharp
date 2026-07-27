namespace Microsoft.Mutate4CSharp.Coverage;

using Microsoft.Mutate4CSharp.Exec;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Project;

/// <summary>
/// Faithful port of mutate4java's <c>CoverageRunner</c>, adapting its JaCoCo-via-Maven flow to
/// coverlet's <c>XPlat Code Coverage</c> collector run through <c>dotnet test</c>. The role is
/// preserved 1:1 — generate (or reuse) coverage for the module under test, returning the baseline
/// <see cref="TestRun"/> alongside a parsed <see cref="CoverageReport"/> — while the ecosystem adapter
/// changes: the single <c>dotnet test --collect</c> invocation is both the baseline run and the
/// coverage generator (decisions.md), coverlet writes a <c>coverage.cobertura.xml</c> under a GUID
/// subdirectory of the results directory (so the newest one is located rather than a fixed path), and
/// the report is parsed by <see cref="CoberturaLineCoverageParser"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>DD3 working directory (Anders, CRITICAL).</b> mutate4java's coverage command is project-less
/// (faithful to <c>mvn test</c>), but <c>dotnet test</c> binds to whatever project or <b>solution</b>
/// sits in its working directory — a stray <c>.sln</c> would fan the run out across the whole solution,
/// silently violating DD3 (wrong baseline + wrong coverage). The port pins the run to exactly one
/// project two ways at once: the working directory is the test project's own directory
/// (<see cref="Path.GetDirectoryName(string)"/> of <see cref="ModuleResolution.TestProjectFile"/>) and
/// the resolved <c>&lt;Project&gt;.Tests.csproj</c> is passed explicitly as the <c>dotnet test</c>
/// target.
/// </para>
/// <para>
/// <b>Deterministic source paths (R-D).</b> The run passes <c>-p:DeterministicSourcePaths=false</c> so
/// the PDB sequence points coverlet reads keep real on-disk source paths. Under the deterministic-build
/// remap (auto-enabled by <c>ContinuousIntegrationBuild=true</c> on CI) coverlet's Cobertura
/// <c>&lt;source&gt;</c> becomes the <c>/_/</c> placeholder, which cannot reconcile with the on-disk
/// mutation-site paths — every site would read uncovered. Explicitly disabling it is the direct switch
/// for that remap and wins even when a repo or CI turns the CI build flag on. The end-to-end
/// reconciliation against a real coverlet report is the T16 integration test.
/// </para>
/// </remarks>
public sealed class CoverageRunner : ICoverageRunner
{
    private const long CoverageTimeoutMillis = 300_000L;
    private const string ResultsDirectoryName = "TestResults";
    private const string CoberturaReportName = "coverage.cobertura.xml";
    private const string TrxSearchPattern = "*.trx";

    private readonly ICommandExecutor _executor;
    private readonly CoverageCleaner _cleaner = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageRunner"/> class over the given command
    /// executor (the port of mutate4java's <c>ProcessCommandExecutor</c> dependency).
    /// </summary>
    /// <param name="executor">The command executor used to run <c>dotnet test</c>.</param>
    public CoverageRunner(ICommandExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        _executor = executor;
    }

    /// <summary>
    /// Generates fresh coverage for the module under test — the port of mutate4java's single-argument
    /// <c>generateCoverage(projectRoot)</c>.
    /// </summary>
    /// <param name="module">The resolved production/test project pair to generate coverage for.</param>
    /// <returns>The baseline run and its parsed coverage report.</returns>
    public CoverageRun GenerateCoverage(ModuleResolution module)
    {
        return GenerateCoverage(module, false);
    }

    /// <summary>
    /// Generates or reuses coverage for the module under test — the port of mutate4java's
    /// <c>generateCoverage(projectRoot, reuseCoverage)</c>.
    /// </summary>
    /// <param name="module">The resolved production/test project pair to generate coverage for.</param>
    /// <param name="reuseCoverage">
    /// When <see langword="true"/>, skips the fresh run and reuses the newest existing
    /// <c>coverage.cobertura.xml</c> under the results directory if present; otherwise refreshes
    /// coverage. When no existing report is found the returned report is empty (every site reads
    /// uncovered), faithful to mutate4java.
    /// </param>
    /// <returns>The baseline run (or <see langword="null"/> on the reuse path) and the coverage report.</returns>
    public CoverageRun GenerateCoverage(ModuleResolution module, bool reuseCoverage)
    {
        ArgumentNullException.ThrowIfNull(module);
        string testProjectFile = TestProjectFileOf(module);
        string testProjectDirectory = Path.GetDirectoryName(testProjectFile)!;
        string resultsDirectory = Path.Combine(testProjectDirectory, ResultsDirectoryName);

        if (reuseCoverage)
        {
            string? existingReport = NewestCoberturaReport(resultsDirectory);
            return new CoverageRun(
                null,
                CoberturaLineCoverageParser.Parse(existingReport),
                true,
                existingReport is not null,
                0);
        }

        _cleaner.DeleteStaleCoverage(resultsDirectory);
        CommandResult result = _executor.Run(
            CoverageCommand(testProjectFile, resultsDirectory), testProjectDirectory, CoverageTimeoutMillis);
        TestRun baseline = new(result.ExitCode, result.Output, result.DurationMillis, result.TimedOut);
        string? reportPath = result.ExitCode == 0 ? NewestCoberturaReport(resultsDirectory) : null;
        return new CoverageRun(
            baseline,
            CoberturaLineCoverageParser.Parse(reportPath),
            false,
            reportPath is not null,
            ExecutedTestCount(resultsDirectory));
    }

    private static IReadOnlyList<string> CoverageCommand(string testProjectFile, string resultsDirectory)
    {
        return
        [
            "dotnet",
            "test",
            testProjectFile,
            "--collect:XPlat Code Coverage",
            "--filter",
            "type!=IntegrationTests&Category!=no-mutate",
            "--results-directory",
            resultsDirectory,
            "--logger",
            "trx",
            "-p:DeterministicSourcePaths=false",
        ];
    }

    private static string TestProjectFileOf(ModuleResolution module)
    {
        return module.TestProjectFile
            ?? throw new ArgumentException("Module resolution has no test project file.", nameof(module));
    }

    private static int ExecutedTestCount(string resultsDirectory)
    {
        return TrxTestCountReader.CountExecutedTests(NewestFile(resultsDirectory, TrxSearchPattern));
    }

    private static string? NewestCoberturaReport(string resultsDirectory)
    {
        return NewestFile(resultsDirectory, CoberturaReportName);
    }

    private static string? NewestFile(string directory, string searchPattern)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        string? newest = null;
        DateTime newestWrite = DateTime.MinValue;
        foreach (string file in Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories))
        {
            DateTime write = File.GetLastWriteTimeUtc(file);
            if (newest is null || write > newestWrite)
            {
                newest = file;
                newestWrite = write;
            }
        }

        return newest;
    }
}
