namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Coverage;
using Microsoft.Mutate4CSharp.Exec;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Project;

/// <summary>
/// Counterpart of mutate4java's <c>CoverageRunnerTest</c>, re-expressed as a fast unit test: where
/// mutate4java drove a real Maven+JaCoCo run through a live <c>ProcessCommandExecutor</c>, the port
/// injects a stub <see cref="ICommandExecutor"/> that writes coverlet-shaped fixtures (a
/// <c>coverage.cobertura.xml</c> under a GUID subdirectory and a <c>.trx</c>) instead of spawning
/// <c>dotnet test</c>. It asserts the same behaviors — fresh refresh, reuse, and missing-reuse — plus
/// the C#/coverlet adaptations: the <c>--collect</c> command shape, the DD3 working directory and
/// explicit test-project target, the deterministic-source-paths control, newest-report selection,
/// stale-results cleaning, and the DD2(b) executed-test count. The real-coverlet reconciliation is the
/// T16 integration test.
/// </summary>
public sealed class CoverageRunnerTests : IDisposable
{
    private readonly string _testProjectDirectory;
    private readonly string _testProjectFile;
    private readonly string _resultsDirectory;
    private readonly ModuleResolution _module;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageRunnerTests"/> class, creating a
    /// per-test temporary test-project directory that backs the resolved module and doubles as the
    /// coverage report's <c>&lt;source&gt;</c> base.
    /// </summary>
    public CoverageRunnerTests()
    {
        _testProjectDirectory = Path.Combine(Path.GetTempPath(), "m4cs-coverage-runner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testProjectDirectory);
        _testProjectFile = Path.Combine(_testProjectDirectory, "Demo.Tests.csproj");
        _resultsDirectory = Path.Combine(_testProjectDirectory, "TestResults");
        _module = ModuleResolution.Resolved(
            "Demo", Path.Combine(_testProjectDirectory, "Demo.csproj"), _testProjectFile);
    }

    /// <summary>
    /// Deletes the per-test temporary directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_testProjectDirectory))
        {
            Directory.Delete(_testProjectDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The fresh run invokes <c>dotnet test</c> with the <c>--collect</c> coverage collector, the
    /// explicit test-project target, the DD3 working directory (the test project's own directory), the
    /// deterministic-source-paths control, the TRX logger, and the coverage timeout; it captures the
    /// baseline, parses the produced report, and reads the executed-test count.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RefreshInvokesCollectRunWithDd3WorkingDirectoryAndParsesReport()
    {
        RecordingCommandExecutor executor = new(exitCode: 0, onRun: results =>
        {
            WriteCoberturaReport(results, coveredLine: 5, hits: 3);
            WriteTrx(results, executed: 3);
        });

        CoverageRun run = new CoverageRunner(executor).GenerateCoverage(_module);

        executor.Invocations.Should().Be(1);
        executor.LastWorkingDirectory.Should().Be(_testProjectDirectory);
        executor.LastTimeoutMillis.Should().Be(300_000L);
        executor.LastCommand.Should().ContainInOrder("dotnet", "test", _testProjectFile);
        executor.LastCommand.Should().Contain("--collect:XPlat Code Coverage");
        executor.LastCommand.Should().Contain("-p:DeterministicSourcePaths=false");
        executor.LastCommand.Should().ContainInOrder("--results-directory", _resultsDirectory);
        executor.LastCommand.Should().ContainInOrder("--logger", "trx");
        executor.LastCommand.Should().ContainInOrder("--filter", "type!=IntegrationTests&Category!=no-mutate");

        run.Reused.Should().BeFalse();
        run.ReportAvailable.Should().BeTrue();
        run.Baseline.Should().NotBeNull();
        run.Baseline!.ExitCode.Should().Be(0);
        run.Report.Covers(Key("Sample.cs"), 5).Should().BeTrue();
        run.ExecutedTestCount.Should().Be(3);
    }

    /// <summary>
    /// When coverlet writes several <c>coverage.cobertura.xml</c> files (one per GUID subdirectory),
    /// the newest by last-write time is the one parsed.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RefreshParsesTheNewestCoberturaReport()
    {
        DateTime baseTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        RecordingCommandExecutor executor = new(exitCode: 0, onRun: results =>
        {
            // Older report says line 5 is covered; newer report says it is not. Newest must win.
            WriteCoberturaReport(results, coveredLine: 5, hits: 3, writeTimeUtc: baseTime);
            WriteCoberturaReport(results, coveredLine: 5, hits: 0, writeTimeUtc: baseTime.AddMinutes(5));
        });

        CoverageRun run = new CoverageRunner(executor).GenerateCoverage(_module);

        run.Report.Covers(Key("Sample.cs"), 5).Should().BeFalse();
    }

    /// <summary>
    /// The stale results directory is deleted before the fresh run, so artifacts a previous run left
    /// behind cannot leak into the new report.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RefreshDeletesStaleResultsDirectoryBeforeRunning()
    {
        Directory.CreateDirectory(_resultsDirectory);
        string staleArtifact = Path.Combine(_resultsDirectory, "stale.txt");
        File.WriteAllText(staleArtifact, "stale");
        RecordingCommandExecutor executor = new(exitCode: 0, onRun: results =>
            WriteCoberturaReport(results, coveredLine: 5, hits: 1));

        CoverageRun run = new CoverageRunner(executor).GenerateCoverage(_module);

        File.Exists(staleArtifact).Should().BeFalse();
        run.ReportAvailable.Should().BeTrue();
    }

    /// <summary>
    /// The DD2(b) executed-test count is read from the produced TRX, including the fail-fast case of
    /// zero executed tests.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RefreshReadsZeroExecutedTestCountFromTrx()
    {
        RecordingCommandExecutor executor = new(exitCode: 0, onRun: results =>
        {
            WriteCoberturaReport(results, coveredLine: 5, hits: 1);
            WriteTrx(results, executed: 0);
        });

        CoverageRun run = new CoverageRunner(executor).GenerateCoverage(_module);

        run.ExecutedTestCount.Should().Be(0);
    }

    /// <summary>
    /// A fresh run that produces no TRX fails closed to a zero executed-test count (which escalates to
    /// DD2(b) downstream) rather than throwing.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RefreshWithoutTrxFailsClosedToZeroExecutedTestCount()
    {
        RecordingCommandExecutor executor = new(exitCode: 0, onRun: results =>
            WriteCoberturaReport(results, coveredLine: 5, hits: 1));

        CoverageRun run = new CoverageRunner(executor).GenerateCoverage(_module);

        run.ExecutedTestCount.Should().Be(0);
    }

    /// <summary>
    /// A failing coverage command yields the baseline exit code, no available report, and an empty
    /// report — faithful to mutate4java parsing coverage only when the run succeeds.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RefreshWithFailingRunReportsEmptyCoverageAndBaselineExitCode()
    {
        RecordingCommandExecutor executor = new(exitCode: 1, onRun: results =>
            WriteCoberturaReport(results, coveredLine: 5, hits: 3));

        CoverageRun run = new CoverageRunner(executor).GenerateCoverage(_module);

        run.Baseline.Should().NotBeNull();
        run.Baseline!.ExitCode.Should().Be(1);
        run.ReportAvailable.Should().BeFalse();
        run.Report.Covers(Key("Sample.cs"), 5).Should().BeFalse();
    }

    /// <summary>
    /// The reuse path reuses the newest existing report without invoking the executor, returning a
    /// null baseline and the reused-report flags — the port of mutate4java's
    /// <c>reusesExistingJacocoXmlWhenRequested</c>.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReuseReusesExistingReportWithoutRunning()
    {
        WriteCoberturaReport(_resultsDirectory, coveredLine: 5, hits: 3);
        RecordingCommandExecutor executor = new(exitCode: 0, onRun: _ =>
            throw new InvalidOperationException("Executor must not run on the reuse path."));

        CoverageRun run = new CoverageRunner(executor).GenerateCoverage(_module, reuseCoverage: true);

        executor.Invocations.Should().Be(0);
        run.Baseline.Should().BeNull();
        run.Reused.Should().BeTrue();
        run.ReportAvailable.Should().BeTrue();
        run.ExecutedTestCount.Should().Be(0);
        run.Report.Covers(Key("Sample.cs"), 5).Should().BeTrue();
    }

    /// <summary>
    /// The reuse path with no existing report returns an empty report in which every site reads
    /// uncovered — the port of mutate4java's <c>reportsMissingCoverageWhenReuseRequestedWithoutJacocoXml</c>
    /// (mutate4java returns an empty report here, not an all-covered one).
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReuseWithoutExistingReportReportsEmptyCoverage()
    {
        RecordingCommandExecutor executor = new(exitCode: 0, onRun: _ =>
            throw new InvalidOperationException("Executor must not run on the reuse path."));

        CoverageRun run = new CoverageRunner(executor).GenerateCoverage(_module, reuseCoverage: true);

        executor.Invocations.Should().Be(0);
        run.Baseline.Should().BeNull();
        run.Reused.Should().BeTrue();
        run.ReportAvailable.Should().BeFalse();
        run.Report.Covers(Key("Sample.cs"), 5).Should().BeFalse();
    }

    private string Key(string relativePath)
    {
        return CoberturaLineCoverageParser.NormalizeSourcePath(Path.Combine(_testProjectDirectory, relativePath));
    }

    private void WriteCoberturaReport(string resultsDirectory, int coveredLine, int hits, DateTime? writeTimeUtc = null)
    {
        string reportDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(reportDirectory);
        string reportFile = Path.Combine(reportDirectory, "coverage.cobertura.xml");
        File.WriteAllText(reportFile, CoberturaXml(_testProjectDirectory, coveredLine, hits));
        if (writeTimeUtc is not null)
        {
            File.SetLastWriteTimeUtc(reportFile, writeTimeUtc.Value);
        }
    }

    private static void WriteTrx(string resultsDirectory, int executed)
    {
        Directory.CreateDirectory(resultsDirectory);
        File.WriteAllText(Path.Combine(resultsDirectory, "run.trx"), TrxXml(executed));
    }

    private static string CoberturaXml(string sourceBase, int line, int hits)
    {
        string xml =
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <coverage version="1.9">
               <sources>
                 <source>{sourceBase}</source>
               </sources>
               <packages>
                 <package name="Demo">
                   <classes>
                     <class name="Demo.Sample" filename="Sample.cs">
                       <lines>
                         <line number="{line}" hits="{hits}" branch="False" />
                       </lines>
                     </class>
                   </classes>
                 </package>
               </packages>
             </coverage>
             """;
        return xml;
    }

    private static string TrxXml(int executed)
    {
        string xml =
            $"""
             <?xml version="1.0" encoding="UTF-8"?>
             <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
               <ResultSummary outcome="Completed">
                 <Counters total="{executed}" executed="{executed}" passed="{executed}" failed="0" />
               </ResultSummary>
             </TestRun>
             """;
        return xml;
    }

    private sealed class RecordingCommandExecutor : ICommandExecutor
    {
        private readonly int _exitCode;
        private readonly Action<string> _onRun;

        public RecordingCommandExecutor(int exitCode, Action<string> onRun)
        {
            _exitCode = exitCode;
            _onRun = onRun;
        }

        public int Invocations { get; private set; }

        public IReadOnlyList<string> LastCommand { get; private set; } = [];

        public string LastWorkingDirectory { get; private set; } = string.Empty;

        public long LastTimeoutMillis { get; private set; }

        public CommandResult Run(IReadOnlyList<string> command, string workingDirectory)
        {
            return Run(command, workingDirectory, 0L);
        }

        public CommandResult Run(IReadOnlyList<string> command, string workingDirectory, long timeoutMillis)
        {
            Invocations++;
            LastCommand = command;
            LastWorkingDirectory = workingDirectory;
            LastTimeoutMillis = timeoutMillis;
            _onRun(ResultsDirectoryOf(command));
            return new CommandResult(_exitCode, "baseline output", 4321, false);
        }

        private static string ResultsDirectoryOf(IReadOnlyList<string> command)
        {
            for (int index = 0; index < command.Count - 1; index++)
            {
                if (string.Equals(command[index], "--results-directory", StringComparison.Ordinal))
                {
                    return command[index + 1];
                }
            }

            throw new InvalidOperationException("Coverage command is missing --results-directory.");
        }
    }
}
