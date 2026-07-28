namespace Microsoft.Mutate4CSharp.Tests;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Mutate4CSharp.Analysis;
using Microsoft.Mutate4CSharp.Cli;
using Microsoft.Mutate4CSharp.Coverage;
using Microsoft.Mutate4CSharp.Exec;
using Microsoft.Mutate4CSharp.Manifest;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Project;
using Microsoft.Mutate4CSharp.Report;

/// <summary>
/// Faithful port of mutate4java's <c>CliApplicationTest</c> — the big end-to-end oracle for the wired
/// engine. Each case drives a real <see cref="CliApplication"/> with stubbed seams (a stub test
/// executor and a stub coverage runner, plus the real <see cref="CopiedWorkspaceManager"/>) so the run
/// paths exercise the actual selection, coverage-gating, worker-isolation, and reporting wiring
/// <em>without</em> spawning a real <c>dotnet test</c>. Twenty-one cases are direct ports of the Java
/// oracle (adapted to the C# target: <c>src/Demo/Sample.cs</c> paths, the <c>.cs</c> message, and the
/// A4 absolute-path coverage key); four additional cases cover the approved DD2 fail-fast departures
/// (no owning project, no test project, and the DD2(b) zero-executed-tests gate — plus its reuse-path
/// exemption), and two more pin Mr. Das' baseline/worker cwd-alignment ruling (the custom
/// <c>--test-command</c> baseline runs at the workspace root; the default path keeps the DD3
/// test-project directory). mutate4java's <c>moduleRootFor</c>/<c>sourceSuffix</c> pass-through tests
/// are dropped: those methods do not exist in the C# layout (their responsibilities moved to
/// <c>ModuleResolver</c> and <c>CoberturaLineCoverageParser.NormalizeSourcePath</c>, each covered by
/// their own unit tests).
/// </summary>
public sealed class CliApplicationTests : IDisposable
{
    private const string OriginalSource =
        "namespace Demo;\n" +
        "\n" +
        "class Sample {\n" +
        "    bool Truthy() {\n" +
        "        return true;\n" +
        "    }\n" +
        "\n" +
        "    bool Same(int left, int right) {\n" +
        "        return left == right;\n" +
        "    }\n" +
        "}\n";

    private const string UnarySource =
        "namespace Demo;\n" +
        "\n" +
        "class Guard {\n" +
        "    bool Allows(bool blocked) {\n" +
        "        return !blocked;\n" +
        "    }\n" +
        "}\n";

    private const string ChangedSource =
        "namespace Demo;\n" +
        "\n" +
        "class Sample {\n" +
        "    bool Truthy() {\n" +
        "        return false;\n" +
        "    }\n" +
        "\n" +
        "    bool Same(int left, int right) {\n" +
        "        return left == right;\n" +
        "    }\n" +
        "}\n";

    private const string ChangedSourceWithExtraScope =
        "namespace Demo;\n" +
        "\n" +
        "class Sample {\n" +
        "    bool Truthy() {\n" +
        "        return false;\n" +
        "    }\n" +
        "\n" +
        "    bool Same(int left, int right) {\n" +
        "        return left == right;\n" +
        "    }\n" +
        "\n" +
        "    bool BrandNew() {\n" +
        "        return true;\n" +
        "    }\n" +
        "}\n";

    private const string CustomTestCommand = "custom-test-command --flag";

    private readonly ManifestSupport _manifestSupport = new();
    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliApplicationTests"/> class, laying down a
    /// resolvable DD2/DD3 module (a <c>src/Demo/Demo.csproj</c> production project and a
    /// <c>tests/Demo.Tests/Demo.Tests.csproj</c> unit test project that references it) under a fresh
    /// per-test temporary workspace root.
    /// </summary>
    public CliApplicationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        WriteProject("src/Demo", "Demo.csproj");
        WriteProject("tests/Demo.Tests", "Demo.Tests.csproj", "../../src/Demo/Demo.csproj");
    }

    /// <summary>Deletes the per-test temporary workspace root.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Verbose mode emits the live baseline, run, and per-worker progress lines.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReportsMutationProgress()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(Coverage(file, 5));
        StubExecutor executor = new(new TestRun(1, "killed", 5, false));
        StringWriter progress = new();

        int exit = new CliApplication(
            _tempDir,
            new StringWriter(),
            new StringWriter(),
            executor,
            coverageRunner,
            new CopiedWorkspaceManager(),
            new PrintStreamProgressReporter(progress)).Execute([Relative(file), "--verbose"]);

        exit.Should().Be(0);
        string progressText = progress.ToString();
        progressText.Should().Contain("Baseline starting for");
        progressText.Should().Contain("Baseline finished: exit=0 timedOut=false duration=10 ms");
        progressText.Should().Contain("Running 1 mutations with 1 workers.");
        progressText.Should().Contain("Worker 1 starting 1/1:");
        progressText.Should().Contain("Worker 1 finished 1/1: KILLED");
    }

    /// <summary><c>--help</c> prints usage and exits zero without running anything.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void PrintsHelpAndExitsZero()
    {
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), new StubExecutor(), new StubCoverageRunner(EmptyCoverage()))
            .Execute(["--help"]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("Usage:");
    }

    /// <summary><c>--scan</c> lists the mutation sites without running coverage or mutants.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ScansMutationSitesWithoutRunningCoverageOrMutants()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(EmptyCoverage());
        StubExecutor executor = new();
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file), "--scan"]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("Scan: 2 mutation sites in src/Demo/Sample.cs");
        output.ToString().Should().Contain("src/Demo/Sample.cs:5 replace true with false");
        output.ToString().Should().Contain("src/Demo/Sample.cs:9 replace == with !=");
        executor.Invocations.Should().Be(0);
        coverageRunner.Invocations.Should().Be(0);
        StrippedSource(file).Should().Be(OriginalSource);
    }

    /// <summary><c>--update-manifest</c> embeds the manifest without running coverage or mutants.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void UpdatesManifestWithoutRunningCoverageOrMutants()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(EmptyCoverage());
        StubExecutor executor = new();
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file), "--update-manifest"]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("Updated manifest for src/Demo/Sample.cs");
        executor.Invocations.Should().Be(0);
        coverageRunner.Invocations.Should().Be(0);
        StrippedSource(file).Should().Be(OriginalSource);
        _manifestSupport.Read(file).Should().NotBeNull();
    }

    /// <summary><c>--reuse-coverage</c> reuses the existing report instead of refreshing coverage.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReusesCoverageWithoutRunningBaselineCoverageCommand()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(true, Coverage(file, 5, 9));
        StubExecutor executor = new(
            new TestRun(0, "baseline ok", 10, false),
            new TestRun(1, "killed", 5, false),
            new TestRun(1, "killed", 6, false));
        StringWriter error = new();

        int exit = Application(new StringWriter(), error, executor, coverageRunner)
            .Execute([Relative(file), "--reuse-coverage"]);

        exit.Should().Be(0);
        coverageRunner.Invocations.Should().Be(0);
        executor.Invocations.Should().Be(3);
        error.ToString().Should().Contain("Reusing existing coverage data");
        error.ToString().Should().Contain("coverage may be stale");
    }

    /// <summary>A reuse request with no existing report warns and continues without filtering.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void WarnsWhenCoverageReuseIsRequestedButNoCoverageExists()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(false, EmptyCoverage());
        StubExecutor executor = new(new TestRun(0, "baseline ok", 10, false));
        StringWriter error = new();

        int exit = Application(new StringWriter(), error, executor, coverageRunner)
            .Execute([Relative(file), "--reuse-coverage"]);

        exit.Should().Be(0);
        coverageRunner.Invocations.Should().Be(0);
        executor.Invocations.Should().Be(1);
        error.ToString().Should().Contain("Coverage reuse requested, but");
        error.ToString().Should().Contain("Continuing without coverage filtering.");
    }

    /// <summary>Scan marks scopes whose content differs from the embedded manifest.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void MarksChangedScopesDuringScanWhenManifestDiffers()
    {
        string file = WriteSourceFile();
        SourceAnalysis manifestBaseline = new MutationCatalog().Analyze(file);
        WriteMatchingManifest(file);
        _manifestSupport.Write(
            file,
            ChangedSource,
            new DifferentialManifest(1, manifestBaseline.ModuleHash, manifestBaseline.Scopes));
        StubCoverageRunner coverageRunner = new(EmptyCoverage());
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), new StubExecutor(), coverageRunner)
            .Execute([Relative(file), "--scan"]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("* src/Demo/Sample.cs:5 replace false with true");
        output.ToString().Should().Contain("  src/Demo/Sample.cs:9 replace == with !=");
        output.ToString().Should().Contain("* indicates a scope that differs from the embedded manifest.");
        coverageRunner.Invocations.Should().Be(0);
    }

    /// <summary>An invalid target argument prints usage to stdout, the error to stderr, and exits one.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void PrintsUsageForInvalidArgumentsAndExitsOne()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exit = Application(output, error, new StubExecutor(), new StubCoverageRunner(EmptyCoverage()))
            .Execute(["bogus"]);

        exit.Should().Be(1);
        output.ToString().Should().Contain("Usage:");
        error.ToString().Should().Contain("mutate4csharp target must be a .cs file");
    }

    /// <summary>A failing baseline stops the run with exit two and never mutates the file.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void StopsWhenBaselineTestsFail()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(new TestRun(1, "failing baseline", 10, false), EmptyCoverage());
        StubExecutor executor = new();
        StringWriter error = new();

        int exit = Application(new StringWriter(), error, executor, coverageRunner)
            .Execute([Relative(file)]);

        exit.Should().Be(2);
        error.ToString().Should().Contain("Baseline tests failed.");
        StrippedSource(file).Should().Be(OriginalSource);
        executor.Invocations.Should().Be(0);
    }

    /// <summary>A surviving mutant yields exit three and both KILLED and SURVIVED lines.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReturnsNonZeroWhenAnyMutationSurvives()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(Coverage(file, 5, 9));
        StubExecutor executor = new(
            new TestRun(1, "killed", 5, false),
            new TestRun(0, "survived", 6, false));
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file)]);

        exit.Should().Be(3);
        output.ToString().Should().Contain("KILLED");
        output.ToString().Should().Contain("SURVIVED");
        StrippedSource(file).Should().Be(OriginalSource);
    }

    /// <summary>Every mutant killed yields exit zero and leaves the source unmutated.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReturnsZeroWhenAllMutationsAreKilled()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(Coverage(file, 5, 9));
        StubExecutor executor = new(
            new TestRun(1, "killed", 5, false),
            new TestRun(1, "killed", 6, false));

        int exit = Application(new StringWriter(), new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file)]);

        exit.Should().Be(0);
        StrippedSource(file).Should().Be(OriginalSource);
    }

    /// <summary>All discovered sites uncovered runs no mutants and still exits zero.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReturnsZeroWhenAllDiscoveredSitesAreUncovered()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(EmptyCoverage());
        StubExecutor executor = new();
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file)]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("Coverage: 2 uncovered sites skipped.");
        executor.Invocations.Should().Be(0);
    }

    /// <summary><c>--lines</c> restricts the run to the requested lines with the floor timeout.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FiltersMutationsByRequestedLines()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(Coverage(file, 5));
        StubExecutor executor = new(new TestRun(1, "killed", 5, false));
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file), "--lines", "5"]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("Summary: 1 killed, 0 survived, 1 total.");
        executor.Invocations.Should().Be(1);
        executor.Timeouts.Should().ContainSingle().Which.Should().Be(1000L);
    }

    /// <summary>
    /// A6 — <c>--lines</c> restricts the run to the requested line range through the full
    /// CLI→parse→filter→selection wiring. With BOTH sites covered, requesting only line 5 mutates and
    /// kills the line-5 boolean site while the line-9 comparison site is removed entirely (it is
    /// neither run nor reported UNCOVERED), so exactly one mutant runs and the run exits 0. Distinct
    /// from <see cref="FiltersMutationsByRequestedLines"/>, whose line-9 site was already uncovered.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RestrictsMutationsToRequestedLineRange()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(Coverage(file, 5, 9));
        StubExecutor executor = new(new TestRun(1, "killed", 5, false));
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file), "--lines", "5"]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("KILLED src/Demo/Sample.cs:5 replace true with false")
            .And.NotContain("src/Demo/Sample.cs:9")
            .And.Contain("Coverage: 0 uncovered sites skipped.")
            .And.Contain("Summary: 1 killed, 0 survived, 1 total.");
        executor.Invocations.Should().Be(1);
    }

    /// <summary>A timed-out mutant counts as killed and prints the timeout marker.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void CountsTimedOutMutantsAsKilled()
    {
        string file = WriteUnarySourceFile();
        StubCoverageRunner coverageRunner = new(Coverage(file, 5));
        StubExecutor executor = new(new TestRun(124, "timed out mutant", 1000, true));
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file), "--lines", "5"]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("timed out");
        StrippedSource(file).Should().Be(UnarySource);
    }

    /// <summary>Uncovered sites are reported and skipped while covered ones run.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReportsUncoveredSitesAndSkipsThem()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(Coverage(file, 5));
        StubExecutor executor = new(new TestRun(1, "killed", 5, false));
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file)]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("UNCOVERED src/Demo/Sample.cs:9 replace == with !=");
        output.ToString().Should().Contain("Coverage: 1 uncovered sites skipped.");
        executor.Invocations.Should().Be(1);
    }

    /// <summary><c>--max-workers</c> runs the covered mutants across the requested worker count.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void AcceptsMaxWorkersDuringMutationRun()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(Coverage(file, 5, 9));
        StubExecutor executor = new(
            new TestRun(1, "killed", 5, false),
            new TestRun(1, "killed", 6, false));
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file), "--max-workers", "2"]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("Summary: 2 killed, 0 survived, 2 total.");
        executor.Invocations.Should().Be(2);
        StrippedSource(file).Should().Be(OriginalSource);
    }

    /// <summary>Exceeding the mutation-warning threshold prints the split-module warning.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void PrintsWarningWhenSelectedMutationCountExceedsThreshold()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(Coverage(file, 5, 9));
        StubExecutor executor = new(
            new TestRun(1, "killed", 5, false),
            new TestRun(1, "killed", 5, false));
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file), "--mutation-warning", "1"]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("WARNING: Found 2 mutations. Consider splitting this module.");
    }

    /// <summary><c>--since-last-run</c> with a matching manifest selects nothing to test.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void SkipsMutationsWhenManifestMatchesCurrentModuleHash()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(Coverage(file, 5, 9));
        StubExecutor executor = new();
        WriteMatchingManifest(file);
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file), "--since-last-run"]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("Total mutation sites: 2");
        output.ToString().Should().Contain("Covered mutation sites: 0");
        output.ToString().Should().Contain("Uncovered mutation sites: 0");
        output.ToString().Should().Contain("Changed mutation sites: 0");
        output.ToString().Should().Contain("Manifest exists: true");
        output.ToString().Should().Contain("Module hash changed: false");
        output.ToString().Should().Contain("Differential surface area: 0");
        output.ToString().Should().Contain("Manifest-violating surface area: 0");
        output.ToString().Should().Contain("No mutations need testing.");
        executor.Invocations.Should().Be(0);
    }

    /// <summary><c>--since-last-run</c> reports surface area for new and manifest-violating scopes.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReportsSurfaceAreaForUnregisteredAndManifestViolatingScopes()
    {
        string file = WriteSourceFile();
        SourceAnalysis manifestBaseline = new MutationCatalog().Analyze(file);
        _manifestSupport.Write(
            file,
            ChangedSourceWithExtraScope,
            new DifferentialManifest(1, manifestBaseline.ModuleHash, manifestBaseline.Scopes));
        StubCoverageRunner coverageRunner = new(Coverage(file, 5, 13));
        StubExecutor executor = new(
            new TestRun(1, "killed", 5, false),
            new TestRun(1, "killed", 6, false));
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file), "--since-last-run"]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("Total mutation sites: 3");
        output.ToString().Should().Contain("Covered mutation sites: 2");
        output.ToString().Should().Contain("Uncovered mutation sites: 0");
        output.ToString().Should().Contain("Changed mutation sites: 2");
        output.ToString().Should().Contain("Manifest exists: true");
        output.ToString().Should().Contain("Module hash changed: true");
        output.ToString().Should().Contain("Differential surface area: 1");
        output.ToString().Should().Contain("Manifest-violating surface area: 1");
        output.ToString().Should().Contain("Summary: 2 killed, 0 survived, 2 total.");
    }

    /// <summary><c>--mutate-all</c> ignores a matching manifest and runs every covered site.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void MutateAllIgnoresMatchingManifest()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(Coverage(file, 5, 9));
        StubExecutor executor = new(
            new TestRun(1, "killed", 5, false),
            new TestRun(1, "killed", 5, false));
        WriteMatchingManifest(file);
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file), "--mutate-all"]);

        exit.Should().Be(0);
        output.ToString().Should().NotContain("No mutations need testing.");
        executor.Invocations.Should().Be(2);
    }

    /// <summary>A custom test command runs verbatim and treats every site as covered.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void UsesCustomTestCommandAndTreatsSitesAsCovered()
    {
        string file = WriteSourceFile();
        StubExecutor executor = new(
            new TestRun(0, "baseline ok", 10, false),
            new TestRun(1, "killed", 5, false),
            new TestRun(1, "killed", 6, false));
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, new StubCoverageRunner(EmptyCoverage()))
            .Execute([Relative(file), "--test-command", CustomTestCommand]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("Summary: 2 killed, 0 survived, 2 total.");
        executor.Invocations.Should().Be(3);
        executor.Commands.Should().Equal(CustomTestCommand, CustomTestCommand, CustomTestCommand);
    }

    /// <summary>
    /// Mr. Das' ruling: on the <c>--test-command</c> path the baseline runs at the workspace/repo root
    /// — the same root the per-mutant workers copy and run in — so a cwd-relative user command resolves
    /// identically for the baseline and every mutant (not the resolved test-project directory).
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RunsCustomTestCommandBaselineFromWorkspaceRoot()
    {
        string file = WriteSourceFile();
        StubExecutor executor = new(
            new TestRun(0, "baseline ok", 10, false),
            new TestRun(1, "killed", 5, false),
            new TestRun(1, "killed", 6, false));

        int exit = Application(new StringWriter(), new StringWriter(), executor, new StubCoverageRunner(EmptyCoverage()))
            .Execute([Relative(file), "--test-command", CustomTestCommand]);

        exit.Should().Be(0);
        executor.Directories.TryPeek(out string? baselineDirectory).Should().BeTrue();
        baselineDirectory.Should().Be(_tempDir);
    }

    /// <summary>
    /// Guard: the default (<c>TestCommand is null</c>) path keeps the DD3 binding — the baseline runs
    /// from the resolved test project's own directory, unchanged by the <c>--test-command</c> fix. The
    /// reuse path is used because it drives the baseline through the executor (the fresh path produces
    /// the baseline via the coverage runner).
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RunsDefaultBaselineFromTestProjectDirectory()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(true, Coverage(file, 5, 9));
        StubExecutor executor = new(
            new TestRun(0, "baseline ok", 10, false),
            new TestRun(1, "killed", 5, false),
            new TestRun(1, "killed", 6, false));

        int exit = Application(new StringWriter(), new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file), "--reuse-coverage"]);

        exit.Should().Be(0);
        executor.Directories.TryPeek(out string? baselineDirectory).Should().BeTrue();
        baselineDirectory.Should().Be(Path.Combine(_tempDir, "tests", "Demo.Tests"));
    }

    /// <summary>
    /// Finding 4 (DD3 consistency): the reuse baseline is scoped to exactly one project two ways at once
    /// — its working directory is the resolved test project's own directory (pinned above) <em>and</em>
    /// that project is passed as the explicit <c>dotnet test</c> target — so a stray <c>.sln</c>/second
    /// <c>.csproj</c> in that directory cannot fan the run out. The stub records the applied target via
    /// <see cref="ITestCommandExecutor.WithTestProject"/>; the reuse path drives the baseline through the
    /// executor, so the applied target is the resolved test project file.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReuseBaselineIsScopedToExplicitTestProject()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(true, Coverage(file, 5, 9));
        StubExecutor executor = new(
            new TestRun(0, "baseline ok", 10, false),
            new TestRun(1, "killed", 5, false),
            new TestRun(1, "killed", 6, false));

        int exit = Application(new StringWriter(), new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file), "--reuse-coverage"]);

        exit.Should().Be(0);
        executor.TestProjects.TryPeek(out string? appliedTestProject).Should().BeTrue();
        appliedTestProject.Should().Be(Path.Combine(_tempDir, "tests", "Demo.Tests", "Demo.Tests.csproj"));
    }

    /// <summary>DD2: no owning C# project above the target fails fast with exit two.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FailsFastWithExitTwoWhenNoOwningProjectExists()
    {
        string file = WriteSource("orphan", "Orphan.cs", OriginalSource);
        StringWriter error = new();

        int exit = Application(new StringWriter(), error, new StubExecutor(), new StubCoverageRunner(EmptyCoverage()))
            .Execute([Relative(file)]);

        exit.Should().Be(2);
        error.ToString().Should().Contain("No owning C# project found");
        StrippedSource(file).Should().Be(OriginalSource);
    }

    /// <summary>DD2: an owning project with no referencing test project fails fast with exit two.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FailsFastWithExitTwoWhenNoTestProjectExists()
    {
        WriteProject("src/Solo", "Solo.csproj");
        string file = WriteSource("src/Solo", "Solo.cs", OriginalSource);
        StringWriter error = new();

        int exit = Application(new StringWriter(), error, new StubExecutor(), new StubCoverageRunner(EmptyCoverage()))
            .Execute([Relative(file)]);

        exit.Should().Be(2);
        error.ToString().Should().Contain("No unit test project found for 'Solo'");
    }

    /// <summary>DD2(b): a green baseline that executed zero unit tests fails fast with exit two.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FailsFastWithExitTwoWhenBaselineExecutesZeroUnitTests()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(Coverage(file, 5, 9), executedTestCount: 0);
        StubExecutor executor = new();
        StringWriter error = new();

        int exit = Application(new StringWriter(), error, executor, coverageRunner)
            .Execute([Relative(file)]);

        exit.Should().Be(2);
        error.ToString().Should().Contain("Baseline executed no unit tests");
        executor.Invocations.Should().Be(0);
        StrippedSource(file).Should().Be(OriginalSource);
    }

    /// <summary>
    /// DD2(b) exemption: the reuse path (which never produces a fresh TRX and so reports zero executed
    /// tests) is exempt from the zero-executed-tests gate and completes normally with exit zero.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReuseCoveragePathIsExemptFromZeroUnitTestFailFast()
    {
        string file = WriteSourceFile();
        StubCoverageRunner coverageRunner = new(true, Coverage(file, 5, 9));
        StubExecutor executor = new(
            new TestRun(0, "baseline ok", 10, false),
            new TestRun(1, "killed", 5, false),
            new TestRun(1, "killed", 6, false));
        StringWriter output = new();

        int exit = Application(output, new StringWriter(), executor, coverageRunner)
            .Execute([Relative(file), "--reuse-coverage"]);

        exit.Should().Be(0);
        output.ToString().Should().Contain("Summary: 2 killed, 0 survived, 2 total.");
        executor.Invocations.Should().Be(3);
    }

    private static CoverageReport Coverage(string sourceFile, params int[] lines)
    {
        string key = CoberturaLineCoverageParser.NormalizeSourcePath(sourceFile);
        HashSet<CoverageSite> sites = [];
        foreach (int line in lines)
        {
            sites.Add(new CoverageSite(key, line));
        }

        return new CoverageReport(sites);
    }

    private static CoverageReport EmptyCoverage()
    {
        return new CoverageReport(new HashSet<CoverageSite>());
    }

    private CliApplication Application(
        TextWriter output, TextWriter error, StubExecutor executor, StubCoverageRunner coverageRunner)
    {
        return new CliApplication(
            _tempDir,
            output,
            error,
            executor,
            coverageRunner,
            new CopiedWorkspaceManager(),
            new NoOpProgressReporter());
    }

    private string WriteSourceFile()
    {
        return WriteSource("src/Demo", "Sample.cs", OriginalSource);
    }

    private string WriteUnarySourceFile()
    {
        return WriteSource("src/Demo", "Guard.cs", UnarySource);
    }

    private string WriteSource(string relativeDir, string fileName, string content)
    {
        string directory = Path.Combine(_tempDir, relativeDir.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private string WriteProject(string relativeDir, string fileName, params string[] projectReferences)
    {
        string directory = Path.Combine(_tempDir, relativeDir.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        string references = string.Concat(
            projectReferences.Select(reference => $"    <ProjectReference Include=\"{reference}\" />\n"));
        string xml = $"<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup>\n{references}  </ItemGroup>\n</Project>\n";
        File.WriteAllText(path, xml);
        return path;
    }

    private string Relative(string file)
    {
        return Path.GetRelativePath(_tempDir, file);
    }

    private string StrippedSource(string file)
    {
        return _manifestSupport.StripManifest(File.ReadAllText(file));
    }

    private void WriteMatchingManifest(string file)
    {
        SourceAnalysis analysis = new MutationCatalog().Analyze(file);
        _manifestSupport.Write(
            file, analysis.SourceWithoutManifest, new DifferentialManifest(1, analysis.ModuleHash, analysis.Scopes));
    }

    /// <summary>
    /// A stub <see cref="ITestCommandExecutor"/> that returns queued <see cref="TestRun"/>s and records
    /// its invocation count, per-call timeouts, per-call working directories, (when bound via
    /// <see cref="WithCommand"/>) the verbatim command, and (when bound via
    /// <see cref="WithTestProject"/>) the applied test-project target. Faithful analog of the Java
    /// oracle's stub: the counter and queues are shared by reference so a
    /// <see cref="WithCommand"/>/<see cref="WithTestProject"/>-derived instance keeps recording into the
    /// original.
    /// </summary>
    private sealed class StubExecutor : ITestCommandExecutor
    {
        private readonly ConcurrentQueue<TestRun> _runs;
        private readonly StrongBox<int> _invocations;
        private readonly string? _command;
        private readonly string? _testProject;

        public StubExecutor(params TestRun[] values)
            : this(
                new ConcurrentQueue<TestRun>(),
                new ConcurrentQueue<long>(),
                new ConcurrentQueue<string>(),
                new ConcurrentQueue<string>(),
                new ConcurrentQueue<string>(),
                new StrongBox<int>(0),
                null,
                null,
                values)
        {
        }

        private StubExecutor(
            ConcurrentQueue<TestRun> runs,
            ConcurrentQueue<long> timeouts,
            ConcurrentQueue<string> commands,
            ConcurrentQueue<string> directories,
            ConcurrentQueue<string> testProjects,
            StrongBox<int> invocations,
            string? command,
            string? testProject,
            params TestRun[] values)
        {
            _runs = runs;
            Timeouts = timeouts;
            Commands = commands;
            Directories = directories;
            TestProjects = testProjects;
            _invocations = invocations;
            _command = command;
            _testProject = testProject;
            foreach (TestRun value in values)
            {
                _runs.Enqueue(value);
            }
        }

        public ConcurrentQueue<long> Timeouts { get; }

        public ConcurrentQueue<string> Commands { get; }

        public ConcurrentQueue<string> Directories { get; }

        public ConcurrentQueue<string> TestProjects { get; }

        public int Invocations => _invocations.Value;

        public TestRun RunTests(string projectRoot, long timeoutMillis)
        {
            Interlocked.Increment(ref _invocations.Value);
            Timeouts.Enqueue(timeoutMillis);
            Directories.Enqueue(projectRoot);
            if (_command is not null)
            {
                Commands.Enqueue(_command);
            }

            if (_testProject is not null)
            {
                TestProjects.Enqueue(_testProject);
            }

            if (!_runs.TryDequeue(out TestRun? run))
            {
                throw new InvalidOperationException("StubExecutor exhausted its queued runs.");
            }

            return run;
        }

        public ITestCommandExecutor WithCommand(string command)
        {
            return new StubExecutor(
                _runs, Timeouts, Commands, Directories, TestProjects, _invocations, command, _testProject);
        }

        public ITestCommandExecutor WithTestProject(string testProjectPath)
        {
            return new StubExecutor(
                _runs, Timeouts, Commands, Directories, TestProjects, _invocations, _command, testProjectPath);
        }
    }

    /// <summary>
    /// A stub <see cref="ICoverageRunner"/> mirroring the Java oracle's <c>StubCoverageRunner</c>: it
    /// returns a fixed baseline and report on the fresh path (recording its invocation count) and, on
    /// the reuse path, the report when a reusable one is "available" or an empty report otherwise. The
    /// C# <see cref="CoverageRun"/> carries the extra DD2(b) executed-test count; it defaults above zero
    /// so the fresh path never trips the zero-tests gate, and a dedicated overload injects zero for the
    /// gate test.
    /// </summary>
    private sealed class StubCoverageRunner : ICoverageRunner
    {
        private readonly TestRun _baseline;
        private readonly CoverageReport _report;
        private readonly bool _reusableReportAvailable;
        private readonly int _executedTestCount;

        public StubCoverageRunner(CoverageReport report)
            : this(false, new TestRun(0, "baseline ok", 10, false), report, 1)
        {
        }

        public StubCoverageRunner(TestRun baseline, CoverageReport report)
            : this(false, baseline, report, 1)
        {
        }

        public StubCoverageRunner(bool reusableReportAvailable, CoverageReport report)
            : this(reusableReportAvailable, new TestRun(0, "baseline ok", 10, false), report, 1)
        {
        }

        public StubCoverageRunner(CoverageReport report, int executedTestCount)
            : this(false, new TestRun(0, "baseline ok", 10, false), report, executedTestCount)
        {
        }

        private StubCoverageRunner(
            bool reusableReportAvailable, TestRun baseline, CoverageReport report, int executedTestCount)
        {
            _reusableReportAvailable = reusableReportAvailable;
            _baseline = baseline;
            _report = report;
            _executedTestCount = executedTestCount;
        }

        public int Invocations { get; private set; }

        public CoverageRun GenerateCoverage(ModuleResolution module)
        {
            Invocations++;
            return new CoverageRun(_baseline, _report, false, true, _executedTestCount);
        }

        public CoverageRun GenerateCoverage(ModuleResolution module, bool reuseCoverage)
        {
            if (reuseCoverage)
            {
                return new CoverageRun(
                    null,
                    _reusableReportAvailable ? _report : new CoverageReport(new HashSet<CoverageSite>()),
                    true,
                    _reusableReportAvailable,
                    0);
            }

            return GenerateCoverage(module);
        }
    }
}
