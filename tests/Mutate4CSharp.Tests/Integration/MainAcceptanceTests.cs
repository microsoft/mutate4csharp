namespace Microsoft.Mutate4CSharp.Tests.Integration;

using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// T18 end-to-end acceptance suite: the whole-tool black-box proof. Each fact generates a hermetic
/// .NET 8 sample via <see cref="TestProjectFactory"/>, spawns the REAL built <c>mutate4csharp</c>
/// executable as a child process with its working directory set to the sample root (the workspace root
/// <c>Program</c> reads through <see cref="Directory.GetCurrentDirectory"/>), and asserts the full CLI
/// contract: the §13 stdout report byte-verbatim (explicit <c>\n</c>, invariant-culture numbers,
/// forward-slash paths — only the volatile elapsed-millisecond digits are canonicalized), the stderr
/// diagnostics separately, and the §14 exit codes 0/1/2/3 including the three DD2 fail-fast exit-2 arms.
/// The exit-3 survivor fact uses a MULTI-mutant sample so the byte-verbatim report also pins result
/// ordering (job order flows source-span top-to-bottom through the worker collector into the outcome
/// writer); a single-mutant sample could not catch an ordering flake. Nothing is stubbed — these run
/// the real nested pipeline (baseline <c>dotnet test</c> + coverlet + per-mutant worker copies), so the
/// samples are kept minimal and the fast fail-fast/usage arms carry no <c>dotnet</c> at all. The class
/// is tagged <c>[Trait("type", "IntegrationTests")]</c> so DD3 excludes it from the fast unit run.
/// </summary>
[Trait("type", "IntegrationTests")]
[Collection("Integration")]
public sealed class MainAcceptanceTests : IDisposable
{
    private const int ProcessTimeoutMilliseconds = 600_000;

    private const string TwoOperatorSource =
        """
        namespace Sample;

        public static class Calculator
        {
            public static int Add(int a, int b)
            {
                return a + b;
            }

            public static int Product(int a, int b)
            {
                return a * b;
            }
        }
        """;

    private const string TwoOperatorTestSource =
        """
        using Sample;
        using Xunit;

        public sealed class CalculatorTests
        {
            [Fact]
            public void AddIsExact()
            {
                Assert.Equal(5, Calculator.Add(2, 3));
            }

            [Fact]
            public void ProductIsNonNegative()
            {
                Assert.True(Calculator.Product(2, 3) >= 0);
            }
        }
        """;

    private const string SingleAddSource =
        """
        namespace Sample;

        public static class Calculator
        {
            public static int Add(int a, int b)
            {
                return a + b;
            }
        }
        """;

    private const string SmokeTestSource =
        """
        using Xunit;

        public sealed class SmokeTests
        {
            [Fact]
            public void Runs()
            {
                Assert.Equal(2, 1 + 1);
            }
        }
        """;

    private const string SingleAddKillingTestSource =
        """
        using Sample;
        using Xunit;

        public sealed class CalculatorTests
        {
            [Fact]
            public void AddIsExact()
            {
                Assert.Equal(5, Calculator.Add(2, 3));
            }
        }
        """;

    private const string IntegrationOnlyTestSource =
        """
        using Xunit;

        public sealed class IntegrationOnlyTests
        {
            [Fact]
            [Trait("type", "IntegrationTests")]
            public void OnlyIntegration()
            {
                Assert.Equal(2, 1 + 1);
            }
        }
        """;

    private const string OrphanSource =
        """
        namespace Orphan;

        public static class Orphan
        {
            public static int Value()
            {
                return 1;
            }
        }
        """;

    // A1: one boolean literal the test exercises + kills (return true, line 7) and one the test never
    // reaches so its site is uncovered and skipped (return false, line 12).
    private const string BooleanCoveredAndUncoveredSource =
        """
        namespace Sample;

        public static class Calculator
        {
            public static bool Exercised()
            {
                return true;
            }

            public static bool NotExercised()
            {
                return false;
            }
        }
        """;

    private const string BooleanExercisedTestSource =
        """
        using Sample;
        using Xunit;

        public sealed class CalculatorTests
        {
            [Fact]
            public void ExercisedIsTrue()
            {
                Assert.True(Calculator.Exercised());
            }
        }
        """;

    // A2/A3: a red test — Add(2, 3) is 5, so asserting 6 fails the baseline.
    private const string SingleAddFailingTestSource =
        """
        using Sample;
        using Xunit;

        public sealed class CalculatorTests
        {
            [Fact]
            public void AddIsWrong()
            {
                Assert.Equal(6, Calculator.Add(2, 3));
            }
        }
        """;

    // A5: one site per operator family — boolean (true), comparison (>), reference null-replacement
    // ("hello"), and unary (!) — consolidating mutate4java's four per-operator acceptance tests.
    private const string MultiOperatorSource =
        """
        namespace Sample;

        public static class Operators
        {
            public static bool Enabled()
            {
                return true;
            }

            public static bool Above(int value)
            {
                return value > 10;
            }

            public static string Greet()
            {
                return "hello";
            }

            public static bool Allowed(bool blocked)
            {
                return !blocked;
            }
        }
        """;

    private const string MultiOperatorKillingTestSource =
        """
        using Sample;
        using Xunit;

        public sealed class OperatorsTests
        {
            [Fact]
            public void EnabledIsTrue()
            {
                Assert.True(Operators.Enabled());
            }

            [Fact]
            public void AboveTenIsFalseAtBoundary()
            {
                Assert.False(Operators.Above(10));
            }

            [Fact]
            public void GreetReturnsHello()
            {
                Assert.Equal("hello", Operators.Greet());
            }

            [Fact]
            public void AllowedNegatesBlocked()
            {
                Assert.True(Operators.Allowed(false));
            }
        }
        """;

    // A7: removing the `!` in `while (!done)` turns the loop into `while (done)` — an infinite loop
    // when the test calls SpinUntil(true), so the single mutant is killed by the per-mutant timeout.
    private const string InfiniteLoopSource =
        """
        namespace Sample;

        public static class Looping
        {
            public static bool SpinUntil(bool done)
            {
                while (!done)
                {
                }

                return done;
            }
        }
        """;

    private const string InfiniteLoopKillingTestSource =
        """
        using Sample;
        using Xunit;

        public sealed class LoopingTests
        {
            [Fact]
            public void SpinUntilDoneReturnsTrue()
            {
                Assert.True(Looping.SpinUntil(true));
            }
        }
        """;

    // The expected §13 reports are built with explicit "\n" concatenation (never a multi-line raw
    // string, whose newlines would inherit this source file's line endings) so the byte-verbatim
    // assertion pins the "\n"-only contract on Windows and the Linux CI runner alike.
    private const string ExpectedSurvivorReport =
        "Baseline tests passed in <N> ms.\n"
        + "Total mutation sites: 2\n"
        + "Covered mutation sites: 2\n"
        + "Uncovered mutation sites: 0\n"
        + "Changed mutation sites: 0\n"
        + "Manifest exists: false\n"
        + "Module hash changed: false\n"
        + "Differential surface area: 0\n"
        + "Manifest-violating surface area: 0\n"
        + "KILLED Sample/Calculator.cs:7 replace + with - (<N> ms)\n"
        + "SURVIVED Sample/Calculator.cs:12 replace * with / (<N> ms)\n"
        + "Coverage: 0 uncovered sites skipped.\n"
        + "Summary: 1 killed, 1 survived, 2 total.\n";

    private const string ExpectedUncoveredReport =
        "Baseline tests passed in <N> ms.\n"
        + "Total mutation sites: 1\n"
        + "Covered mutation sites: 0\n"
        + "Uncovered mutation sites: 1\n"
        + "Changed mutation sites: 0\n"
        + "Manifest exists: false\n"
        + "Module hash changed: false\n"
        + "Differential surface area: 0\n"
        + "Manifest-violating surface area: 0\n"
        + "UNCOVERED Sample/Calculator.cs:7 replace + with -\n"
        + "Coverage: 1 uncovered sites skipped.\n"
        + "Summary: 0 killed, 0 survived, 0 total.\n";

    // The --test-command path treats every site as covered (no coverage run), so the single Add
    // mutant is covered and killed by the custom command.
    private const string ExpectedCustomCommandKilledReport =
        "Baseline tests passed in <N> ms.\n"
        + "Total mutation sites: 1\n"
        + "Covered mutation sites: 1\n"
        + "Uncovered mutation sites: 0\n"
        + "Changed mutation sites: 0\n"
        + "Manifest exists: false\n"
        + "Module hash changed: false\n"
        + "Differential surface area: 0\n"
        + "Manifest-violating surface area: 0\n"
        + "KILLED Sample/Calculator.cs:7 replace + with - (<N> ms)\n"
        + "Coverage: 0 uncovered sites skipped.\n"
        + "Summary: 1 killed, 0 survived, 1 total.\n";

    // A1: the canonical mixed success path — one covered mutant the tests kill and one uncovered
    // mutant that is reported (BEFORE the killed line) and skipped; all sites resolved so the run
    // exits 0 and stamps the manifest.
    private const string ExpectedGreenKilledWithUncoveredReport =
        "Baseline tests passed in <N> ms.\n"
        + "Total mutation sites: 2\n"
        + "Covered mutation sites: 1\n"
        + "Uncovered mutation sites: 1\n"
        + "Changed mutation sites: 0\n"
        + "Manifest exists: false\n"
        + "Module hash changed: false\n"
        + "Differential surface area: 0\n"
        + "Manifest-violating surface area: 0\n"
        + "UNCOVERED Sample/Calculator.cs:12 replace false with true\n"
        + "KILLED Sample/Calculator.cs:7 replace true with false (<N> ms)\n"
        + "Coverage: 1 uncovered sites skipped.\n"
        + "Summary: 1 killed, 0 survived, 1 total.\n";

    private TestProject? _project;

    /// <summary>
    /// Best-effort deletes the generated sample, tolerating <see cref="IOException"/> and
    /// <see cref="UnauthorizedAccessException"/> because the just-finished child process may still hold
    /// a transient handle under the sample's build output.
    /// </summary>
    public void Dispose()
    {
        _project?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A covered mutant the tests do not catch survives: the tool exits 3 with an empty stderr and the
    /// byte-verbatim §13 report, whose fixed KILLED-then-SURVIVED order pins deterministic result
    /// ordering across the two mutants.
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void SurvivingMutantExitsThreeWithVerbatimOrderedReport()
    {
        _project = new TestProjectFactory()
            .WithProductionFile("Calculator.cs", TwoOperatorSource)
            .WithTestFile("CalculatorTests.cs", TwoOperatorTestSource)
            .Create();

        ToolResult result = RunTool(_project.Root, ProductionArgument(_project, "Calculator.cs"));

        result.ExitCode.Should().Be(
            3, "a covered mutant the tests do not catch survives; stderr was:\n{0}", result.StandardError);
        result.StandardError.Should().BeEmpty();
        NormalizeDurations(result.StandardOutput).Should().Be(ExpectedSurvivorReport);
    }

    /// <summary>
    /// A run whose only mutation site is uncovered is a clean pass: the tool exits 0 with the
    /// byte-verbatim all-uncovered report and stamps the module manifest into the source file.
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void AllUncoveredExitsZeroAndWritesManifest()
    {
        _project = new TestProjectFactory()
            .WithProductionFile("Calculator.cs", SingleAddSource)
            .WithTestFile("SmokeTests.cs", SmokeTestSource)
            .Create();

        ToolResult result = RunTool(_project.Root, ProductionArgument(_project, "Calculator.cs"));

        result.ExitCode.Should().Be(
            0, "an all-uncovered run has nothing to kill and passes; stderr was:\n{0}", result.StandardError);
        result.StandardError.Should().BeEmpty();
        NormalizeDurations(result.StandardOutput).Should().Be(ExpectedUncoveredReport);
        File.ReadAllText(_project.ProductionFile("Calculator.cs"))
            .Should().Contain("mutate4csharp-manifest", "an exit-0 run stamps the module manifest into the source");
    }

    /// <summary>
    /// Locks Mr. Das' baseline/worker cwd-alignment ruling end-to-end: a <c>--test-command</c> whose
    /// test-project path is relative to the workspace/repo root drives a clean all-killed run (exit 0
    /// with the byte-verbatim §13 report). This is CAUSAL to the fix — the custom command
    /// <c>dotnet test Sample.Tests/Sample.Tests.csproj</c> only resolves when it runs from the
    /// workspace root. With the fix the baseline runs at the workspace root (aligned with the
    /// per-mutant workers, which run from their repo-root copies), so the relative project path
    /// resolves, the baseline passes, and the worker kills the mutant. Under the OLD bug the baseline
    /// ran from the resolved test-project directory, where <c>Sample.Tests/Sample.Tests.csproj</c>
    /// does NOT resolve, so <c>dotnet test</c> would fail and the tool would exit 2 (failed baseline).
    /// A clean exit-0 all-killed run therefore proves both the baseline and the workers ran the
    /// command from the aligned workspace root.
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void CustomTestCommandRunsFromWorkspaceRootAndKillsMutant()
    {
        _project = new TestProjectFactory()
            .WithProductionFile("Calculator.cs", SingleAddSource)
            .WithTestFile("CalculatorTests.cs", SingleAddKillingTestSource)
            .Create();

        // Repo-root-relative test-project path: this is what makes the test causal — it resolves only
        // from the workspace root (the aligned cwd the fix pins for the baseline), never from the
        // resolved test-project directory the old baseline used.
        string relativeTestProject = _project.RelativeToRoot(_project.TestProjectFile).Replace('\\', '/');
        string testCommand = "dotnet test " + relativeTestProject;

        ToolResult result = RunTool(
            _project.Root, ProductionArgument(_project, "Calculator.cs"), "--test-command", testCommand);

        string because =
            "the repo-root-relative --test-command resolves only when the baseline runs from the aligned "
            + "workspace root; a failed baseline would exit 2. stderr was:\n" + result.StandardError;
        result.ExitCode.Should().Be(0, because);
        result.StandardError.Should().BeEmpty();
        NormalizeDurations(result.StandardOutput).Should().Be(ExpectedCustomCommandKilledReport);
    }

    /// <summary>
    /// A target file that belongs to no <c>.csproj</c> under the workspace fails fast (DD2): the tool
    /// exits 2 before any <c>dotnet</c> run, with an empty stdout and the exact no-owning-project stderr.
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void MissingOwningProjectExitsTwo()
    {
        _project = new TestProjectFactory()
            .WithLooseFile("Orphan.cs", OrphanSource)
            .Create();

        ToolResult result = RunTool(_project.Root, "Orphan.cs");

        result.ExitCode.Should().Be(2);
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().Be(
            "No owning C# project found for Orphan.cs. mutate4csharp requires the target file to belong to "
            + "a project (.csproj) under the workspace.\n");
    }

    /// <summary>
    /// A production project with no matching <c>.Tests</c>/<c>.UnitTests</c> project fails fast (DD2):
    /// the tool exits 2 before any <c>dotnet</c> run, with an empty stdout and the exact
    /// no-test-project stderr.
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void MissingTestProjectExitsTwo()
    {
        _project = new TestProjectFactory()
            .WithProductionFile("Calculator.cs", SingleAddSource)
            .Create();

        ToolResult result = RunTool(_project.Root, ProductionArgument(_project, "Calculator.cs"));

        result.ExitCode.Should().Be(2);
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().Be(
            "No unit test project found for 'Sample'. mutate4csharp requires a matching '.Tests'/'.UnitTests' "
            + "project that references it.\n");
    }

    /// <summary>
    /// A test project whose only test is <c>[Trait("type","IntegrationTests")]</c> runs zero unit tests
    /// under the DD3 unit filter: the baseline executes no tests and the tool fails fast (DD2b), exiting
    /// 2 with an empty stdout and the exact zero-unit-tests stderr.
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void ZeroUnitTestsExitsTwo()
    {
        _project = new TestProjectFactory()
            .WithProductionFile("Calculator.cs", SingleAddSource)
            .WithTestFile("IntegrationOnlyTests.cs", IntegrationOnlyTestSource)
            .Create();

        ToolResult result = RunTool(_project.Root, ProductionArgument(_project, "Calculator.cs"));

        result.ExitCode.Should().Be(
            2, "a baseline that runs zero unit tests fails fast (DD2b); stderr was:\n{0}", result.StandardError);
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().Be(
            "Baseline executed no unit tests. mutate4csharp requires the test project to run at least "
            + "one unit test.\n");
    }

    /// <summary>
    /// Conflicting selection flags (<c>--scan</c> with <c>--update-manifest</c>) are a usage error: the
    /// tool exits 1 and reports the conflict on stderr. The stdout usage text is written with
    /// <c>WriteLine</c> (platform newline), so only the exit code and the stderr message are asserted.
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void ConflictingSelectionFlagsExitOne()
    {
        _project = new TestProjectFactory().Create();

        ToolResult result = RunTool(_project.Root, "Sample/Calculator.cs", "--scan", "--update-manifest");

        result.ExitCode.Should().Be(1);
        result.StandardError.Should().Contain("--scan may not be combined with --update-manifest");
    }

    /// <summary>
    /// A1 — the canonical green success path: a covered boolean mutant the test kills and a second,
    /// uncovered boolean mutant. The tool exits 0 with the byte-verbatim §13 report — the UNCOVERED
    /// line ordered before the KILLED line — and stamps the module manifest into the source.
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void GreenAllKilledWithUncoveredSiteExitsZeroAndWritesManifest()
    {
        _project = new TestProjectFactory()
            .WithProductionFile("Calculator.cs", BooleanCoveredAndUncoveredSource)
            .WithTestFile("CalculatorTests.cs", BooleanExercisedTestSource)
            .Create();

        ToolResult result = RunTool(_project.Root, ProductionArgument(_project, "Calculator.cs"));

        result.ExitCode.Should().Be(
            0,
            "the only covered mutant is killed and the uncovered one is skipped; stderr was:\n{0}",
            result.StandardError);
        result.StandardError.Should().BeEmpty();
        NormalizeDurations(result.StandardOutput).Should().Be(ExpectedGreenKilledWithUncoveredReport);
        File.ReadAllText(_project.ProductionFile("Calculator.cs"))
            .Should().Contain("mutate4csharp-manifest", "an exit-0 run stamps the module manifest into the source");
    }

    /// <summary>
    /// A2 — a baseline whose test is red fails fast (§14 exit 2 via <c>BaselineRunner.Fail</c>): the
    /// tool writes nothing to stdout and its stderr starts with the byte-verbatim <c>Baseline tests
    /// failed.\n</c> line (the failing <c>dotnet test</c> output follows, so only the prefix is pinned).
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void RedBaselineExitsTwo()
    {
        _project = new TestProjectFactory()
            .WithProductionFile("Calculator.cs", SingleAddSource)
            .WithTestFile("CalculatorTests.cs", SingleAddFailingTestSource)
            .Create();

        ToolResult result = RunTool(_project.Root, ProductionArgument(_project, "Calculator.cs"));

        result.ExitCode.Should().Be(
            2, "a red baseline fails fast before any mutant runs; stderr was:\n{0}", result.StandardError);
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().StartWith("Baseline tests failed.\n");
    }

    /// <summary>
    /// A3 — <c>--update-manifest</c> short-circuits before the module check and baseline (T15
    /// ordering): run against a project whose test would fail the baseline, it still exits 0, writes
    /// only the <c>Updated manifest for …</c> line to stdout, stamps the manifest into the source, and
    /// never touches stderr — proving the tests were not run.
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void UpdateManifestOnRedProjectExitsZeroWithoutRunningTests()
    {
        _project = new TestProjectFactory()
            .WithProductionFile("Calculator.cs", SingleAddSource)
            .WithTestFile("CalculatorTests.cs", SingleAddFailingTestSource)
            .Create();

        ToolResult result = RunTool(
            _project.Root, ProductionArgument(_project, "Calculator.cs"), "--update-manifest");

        result.ExitCode.Should().Be(
            0,
            "update-manifest short-circuits before the baseline, so the red test never runs; stderr was:\n{0}",
            result.StandardError);
        result.StandardOutput.Should().Be("Updated manifest for Sample/Calculator.cs\n");
        result.StandardError.Should().BeEmpty();
        File.ReadAllText(_project.ProductionFile("Calculator.cs"))
            .Should().Contain("mutate4csharp-manifest", "update-manifest stamps the module manifest into the source");
    }

    /// <summary>
    /// A5 — one consolidated run whose production code has a boolean, a comparison, a reference
    /// null-replacement, and a unary mutation site, all covered and killed by the tests. Asserts the
    /// four operator-family descriptions and the all-killed summary via <c>Contains</c> (so the run is
    /// robust to per-mutant ordering), exiting 0. Replaces mutate4java's four separate per-operator
    /// acceptance tests with a single compile-and-kill run.
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void MultiOperatorFamilyAllKilledExitsZero()
    {
        _project = new TestProjectFactory()
            .WithProductionFile("Operators.cs", MultiOperatorSource)
            .WithTestFile("OperatorsTests.cs", MultiOperatorKillingTestSource)
            .Create();

        ToolResult result = RunTool(_project.Root, ProductionArgument(_project, "Operators.cs"));

        result.ExitCode.Should().Be(
            0,
            "every covered mutant across the four operator families is killed; stderr was:\n{0}",
            result.StandardError);
        result.StandardError.Should().BeEmpty();
        result.StandardOutput.Should().Contain("replace true with false")
            .And.Contain("replace > with >=")
            .And.Contain("replace \"hello\" with null")
            .And.Contain("replace ! with removed !")
            .And.Contain("Coverage: 0 uncovered sites skipped.")
            .And.Contain("Summary: 4 killed, 0 survived, 4 total.");
    }

    /// <summary>
    /// A7 — the only end-to-end proof of the per-mutant timeout path: removing the <c>!</c> in
    /// <c>while (!done)</c> makes the sole mutant loop forever, so the tool's own per-mutant timeout
    /// (bounded to one baseline duration via <c>--timeout-factor 1</c>) fires and scores it KILLED
    /// (timed out); the tool exits 0. Guarded by the single infinite-loop mutant, the test needs no
    /// <c>Thread.Sleep</c> and relies solely on the tool's timeout.
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void InfiniteLoopMutantIsKilledByTimeout()
    {
        _project = new TestProjectFactory()
            .WithProductionFile("Looping.cs", InfiniteLoopSource)
            .WithTestFile("LoopingTests.cs", InfiniteLoopKillingTestSource)
            .Create();

        ToolResult result = RunTool(
            _project.Root, ProductionArgument(_project, "Looping.cs"), "--timeout-factor", "1");

        result.ExitCode.Should().Be(
            0,
            "the sole mutant hangs and is killed by the per-mutant timeout; stderr was:\n{0}",
            result.StandardError);
        result.StandardError.Should().BeEmpty();
        result.StandardOutput.Should().Contain("replace ! with removed !")
            .And.Contain("timed out")
            .And.Contain("Summary: 1 killed, 0 survived, 1 total.");
    }

    private static string ProductionArgument(TestProject project, string fileName)
    {
        // Forward slashes keep the single argument valid on Windows and the Linux CI runner alike; the
        // tool renders the report path with forward slashes regardless of the argument's separators.
        return project.RelativeToRoot(project.ProductionFile(fileName)).Replace('\\', '/');
    }

    private static string NormalizeDurations(string report)
    {
        // The elapsed-millisecond digits are the only non-deterministic part of the §13 contract;
        // canonicalizing both duration forms lets the rest of the block — ordering, tokens,
        // forward-slash paths, counts and every "\n" — be asserted byte-verbatim.
        report = Regex.Replace(report, @"passed in \d+ ms", "passed in <N> ms");
        report = Regex.Replace(report, @"\(\d+ ms\)", "(<N> ms)");
        return report;
    }

    private static ToolResult RunTool(string workingDirectory, params string[] toolArguments)
    {
        string applicationDll = LocateApplication();
        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(applicationDll);
        foreach (string argument in toolArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Could not start 'dotnet'; the .NET 8 SDK must be on PATH for this acceptance test.", ex);
        }

        // Drain stdout AND stderr on separate reads STARTED BEFORE the wait (the T11 drain-before-wait
        // lesson applied to our own capture): a chatty nested `dotnet test` fills one pipe's buffer, so
        // waiting on exit before reading the other stream would deadlock the child. Reading both
        // concurrently lets it flush freely.
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(ProcessTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException(
                $"The mutate4csharp process did not exit within {ProcessTimeoutMilliseconds} ms.");
        }

        process.WaitForExit(); // let the async readers reach EOF now that the process has exited
        return new ToolResult(
            process.ExitCode, standardOutput.GetAwaiter().GetResult(), standardError.GetAwaiter().GetResult());
    }

    private static string LocateApplication()
    {
        // Derive the running configuration/TFM from the test output directory
        // (…/tests/Mutate4CSharp.Tests/bin/<Config>/<TFM>/) and read the sibling application project's
        // own build output for the same pair — never a hard-coded Debug/Release — so the same real exe
        // the current run built is the one spawned.
        string assemblyName = typeof(Main).Assembly.GetName().Name!;
        DirectoryInfo testOutput = new(AppContext.BaseDirectory);
        string targetFramework = testOutput.Name;
        string configuration = testOutput.Parent!.Name;
        string repositoryRoot = RepositoryRoot(testOutput);
        string applicationDll = Path.Combine(
            repositoryRoot, "src", "Mutate4CSharp", "bin", configuration, targetFramework, assemblyName + ".dll");
        if (!File.Exists(applicationDll))
        {
            throw new InvalidOperationException(
                $"The built mutate4csharp application was not found at '{applicationDll}'. Build the "
                + $"'{assemblyName}' project ({configuration}/{targetFramework}) before running the acceptance tests.");
        }

        return applicationDll;
    }

    private static string RepositoryRoot(DirectoryInfo start)
    {
        DirectoryInfo? directory = start;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "mutate4csharp.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (mutate4csharp.sln) above '{start.FullName}'.");
    }

    private sealed record ToolResult(int ExitCode, string StandardOutput, string StandardError);
}
