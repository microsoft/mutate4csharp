namespace Microsoft.Mutate4CSharp.Tests.Integration;

using System.ComponentModel;
using Microsoft.Mutate4CSharp;
using Microsoft.Mutate4CSharp.Exec;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Report;

/// <summary>
/// T17 — the worker-scoping keystone. It proves end-to-end that a mutation worker tests the MUTATED
/// COPY, not the un-mutated original. It generates a tiny real .NET 8 sample (a <c>Calculator</c>
/// production project plus an xUnit test project) into a unique temp root, makes a real per-worker copy
/// with the production <see cref="CopiedWorkspaceManager"/>, splices a real
/// <see cref="MutationSite"/> into the copy through the real <see cref="IsolatedMutationWorker"/>, and
/// runs a real <c>dotnet test</c> through the real <see cref="ProcessTestCommandExecutor"/> scoped by
/// the <see cref="ITestCommandExecutor.WithTestProject(string)"/> seam — the exact executor path
/// <see cref="Engine.MutationRunPlanner"/> wires (a repo-root-relative test-project target, cwd = worker
/// root, the DD3 unit filter).
/// </summary>
/// <remarks>
/// Per Anders' T14/T16 reviews this is the guard against risk R-C: if the mutation-run executor ever
/// targeted the original repo by an absolute path, every mutant would silently SURVIVE (a catastrophic
/// false-negative). The <see cref="CoveredMutationInWorkerCopyIsKilledByScopedTestRun"/> fact is causal
/// — only the worker copy is ever mutated, so a KILLED result can come only from testing that copy; a
/// regression to an absolute-original target would flip KILLED→SURVIVED and fail the fact.
/// <para>
/// The mutation is applied through the real machinery (a real <see cref="MutationSite"/> spliced by
/// <see cref="IsolatedMutationWorker"/>), not a bespoke edit, so the port's actual rewrite + scoring
/// path is exercised; only the Roslyn discovery that would produce the site is stubbed by hand-computing
/// the span, which is out of scope for this executor keystone.
/// </para>
/// The class is tagged <c>[Trait("type", "IntegrationTests")]</c> so DD3 excludes it from the fast unit
/// run and it stays filterable via <c>--filter "type=IntegrationTests"</c>.
/// </remarks>
[Trait("type", "IntegrationTests")]
public sealed class WorkerMutationExecutorIntegrationTests : IDisposable
{
    // Unbounded run (non-positive == wait on process exit only): the keystone asserts the test
    // PASS/FAIL outcome, not duration, so no positive ceiling can flake a slow CI run into a false
    // TimedOut. There are no timing assertions anywhere in this file.
    private const long UnboundedTimeout = 0L;

    // Add is exercised by the [Fact]; Subtract is not. "a + b" and "a - b" each occur exactly once, so
    // the span for either operator is found unambiguously by IndexOf.
    private const string CalculatorSource =
        """
        namespace Sample;

        public static class Calculator
        {
            public static int Add(int a, int b)
            {
                return a + b;
            }

            public static int Subtract(int a, int b)
            {
                return a - b;
            }
        }
        """;

    private const string CalculatorTestsSource =
        """
        using Sample;
        using Xunit;

        public sealed class CalculatorTests
        {
            [Fact]
            [Trait("type", "UnitTests")]
            public void AddSumsItsOperands()
            {
                Assert.Equal(5, Calculator.Add(2, 3));
            }
        }
        """;

    // DD3 anti-fan-out: a stray sibling project that intentionally does NOT compile. Referenced by the
    // sample .sln, it is the whole-solution build target that a scoped `dotnet test <Sample.Tests.csproj>`
    // must never touch; a fan-out to the solution would try to build it and fail the compile.
    private const string StrayBrokenSource =
        """
        namespace Stray;

        public static class Broken
        {
            public static int Value()
            {
                // Intentional compile error (string is not convertible to int): this project must
                // never be built by a correctly scoped run.
                return "not an int";
            }
        }
        """;

    private const string ProductionProject =
        $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>{HermeticSample.TargetFramework}</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>
        </Project>
        """;

    private const string TestProject =
        $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>{HermeticSample.TargetFramework}</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <IsPackable>false</IsPackable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="{HermeticSample.TestSdkVersion}" />
            <PackageReference Include="xunit" Version="{HermeticSample.XunitVersion}" />
            <PackageReference Include="xunit.runner.visualstudio" Version="{HermeticSample.XunitVersion}" />
            <PackageReference Include="coverlet.collector" Version="{HermeticSample.CoverletCollectorVersion}" />
          </ItemGroup>
          <ItemGroup>
            <ProjectReference Include="../Sample/Sample.csproj" />
          </ItemGroup>
        </Project>
        """;

    private const string StrayProject =
        $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>{HermeticSample.TargetFramework}</TargetFramework>
            <Nullable>enable</Nullable>
          </PropertyGroup>
        </Project>
        """;

    private const string SolutionFile =
        """
        Microsoft Visual Studio Solution File, Format Version 12.00
        # Visual Studio Version 17
        Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Sample", "Sample/Sample.csproj", "{A1111111-1111-1111-1111-111111111111}"
        EndProject
        Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Sample.Tests", "Sample.Tests/Sample.Tests.csproj", "{B2222222-2222-2222-2222-222222222222}"
        EndProject
        Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Stray", "Stray/Stray.csproj", "{C3333333-3333-3333-3333-333333333333}"
        EndProject
        Global
          GlobalSection(SolutionConfigurationPlatforms) = preSolution
            Debug|Any CPU = Debug|Any CPU
            Release|Any CPU = Release|Any CPU
          EndGlobalSection
          GlobalSection(ProjectConfigurationPlatforms) = postSolution
            {A1111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            {A1111111-1111-1111-1111-111111111111}.Debug|Any CPU.Build.0 = Debug|Any CPU
            {A1111111-1111-1111-1111-111111111111}.Release|Any CPU.ActiveCfg = Release|Any CPU
            {A1111111-1111-1111-1111-111111111111}.Release|Any CPU.Build.0 = Release|Any CPU
            {B2222222-2222-2222-2222-222222222222}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            {B2222222-2222-2222-2222-222222222222}.Debug|Any CPU.Build.0 = Debug|Any CPU
            {B2222222-2222-2222-2222-222222222222}.Release|Any CPU.ActiveCfg = Release|Any CPU
            {B2222222-2222-2222-2222-222222222222}.Release|Any CPU.Build.0 = Release|Any CPU
            {C3333333-3333-3333-3333-333333333333}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            {C3333333-3333-3333-3333-333333333333}.Debug|Any CPU.Build.0 = Debug|Any CPU
            {C3333333-3333-3333-3333-333333333333}.Release|Any CPU.ActiveCfg = Release|Any CPU
            {C3333333-3333-3333-3333-333333333333}.Release|Any CPU.Build.0 = Release|Any CPU
          EndGlobalSection
        EndGlobal
        """;

    private readonly string _root;
    private WorkerWorkspaces? _workspaces;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerMutationExecutorIntegrationTests"/> class,
    /// creating the unique temp root that holds the generated sample the worker copies are made from.
    /// </summary>
    public WorkerMutationExecutorIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "m4cs-exec-it", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    /// <summary>
    /// Best-effort teardown: disposes the real worker copies first (they hold the just-built test output
    /// a real testhost / VBCSCompiler handle can lock briefly), then deletes the source root.
    /// </summary>
    public void Dispose()
    {
        BestEffortDisposeWorkspaces();
        TryDeleteDirectory(_root);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Splicing the covered <c>a + b</c> in the worker copy to <c>a - b</c> makes the [Fact] that
    /// asserts <c>Add(2, 3) == 5</c> fail, so the scoped run exits non-zero and the mutant is KILLED.
    /// This is the leak guard, made causal: only the worker copy was ever mutated (the original on disk
    /// is byte-identical to what was written), so a KILLED result can arise only from testing that copy.
    /// A regression pointing the executor at an absolute original-repo path would test the un-mutated
    /// original, the [Fact] would PASS, and <c>Killed</c> would be false — flipping and failing this fact.
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void CoveredMutationInWorkerCopyIsKilledByScopedTestRun()
    {
        Sample sample = WriteSample();
        string workerRoot = CreateWorkerCopy();
        MutationJob job = MutationJobFor(sample, "a + b", "a - b");

        MutationResult result = RunMutant(workerRoot, sample.TestProjectRelativePath, job);

        result.Killed.Should().BeTrue(
            "flipping the covered `a + b` to `a - b` in the worker copy must fail the [Fact] and score KILLED");
        result.TimedOut.Should().BeFalse("the mutant is killed by a real test failure, not by a timeout");
        File.ReadAllText(sample.OriginalCalculatorFile).Should().Be(
            CalculatorSource, "only the worker copy is mutated; the original source is never touched (leak guard)");
    }

    /// <summary>
    /// Splicing the never-exercised <c>Subtract</c> (<c>a - b</c> → <c>a + b</c>) leaves the covered
    /// [Fact] passing, so the same real scoped run exits zero and the mutant SURVIVES. This also proves
    /// the DD3 unit filter includes the sample test (were it excluded, <c>dotnet test</c> would exit
    /// non-zero and this would read KILLED).
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void UncoveredMutationInWorkerCopySurvivesScopedTestRun()
    {
        Sample sample = WriteSample();
        string workerRoot = CreateWorkerCopy();
        MutationJob job = MutationJobFor(sample, "a - b", "a + b");

        MutationResult result = RunMutant(workerRoot, sample.TestProjectRelativePath, job);

        result.Killed.Should().BeFalse(
            "mutating the never-exercised Subtract keeps the covered [Fact] green, so the run exits 0 and SURVIVES");
        result.TimedOut.Should().BeFalse();
    }

    /// <summary>
    /// DD3 anti-fan-out: with a competing <c>Sample.sln</c> and a deliberately non-compiling
    /// <c>Stray</c> project present at the worker root, the scoped run against the un-mutated copy still
    /// passes — proving the executor targets the single resolved test project and never fans out to the
    /// whole solution (which would try to build <c>Stray</c> and fail the compile).
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void ScopedTestRunDoesNotFanOutToCompetingSolution()
    {
        Sample sample = WriteSample();
        string workerRoot = CreateWorkerCopy();

        TestRun run = RunScopedTests(workerRoot, sample.TestProjectRelativePath);

        run.Passed().Should().BeTrue(
            "the run targets Sample.Tests by its repo-relative path (cwd == worker root); the broken Stray and the "
            + "Sample.sln that references it are never built, so a fan-out to the solution — which would fail the "
            + "compile — did not happen");
        run.TimedOut.Should().BeFalse();
    }

    private static ITestCommandExecutor ScopedExecutor(string testProjectRelativePath)
    {
        // The keystone seam: the real ProcessTestCommandExecutor scoped to the resolved test project by
        // a REPO-RELATIVE path (carrying the DD3 unit filter). Run with cwd == worker root it resolves
        // to workerRoot/<relative> — the mutated copy — exactly as MutationRunPlanner wires it.
        return new ProcessTestCommandExecutor().WithTestProject(testProjectRelativePath);
    }

    private static MutationJob MutationJobFor(Sample sample, string original, string replacement)
    {
        // A real MutationSite whose span is the operator token, spliced by the real IsolatedMutationWorker
        // (source[..Start] + Replacement + source[End..]). Offsets are computed against the exact source
        // written to disk, so they match the byte-identical File.Copy worker copy regardless of newline
        // style.
        int start = CalculatorSource.IndexOf(original, StringComparison.Ordinal);
        int lineNumber = 1 + CalculatorSource[..start].Count(character => character == '\n');
        MutationSite site = new(
            sample.OriginalCalculatorFile,
            lineNumber,
            start,
            start + original.Length,
            original,
            replacement,
            $"replace '{original}' with '{replacement}'");
        return new MutationJob(site, sample.CalculatorRelativePath, UnboundedTimeout, 0, 1);
    }

    private static T GuardDotnet<T>(Func<T> run)
    {
        try
        {
            return run();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Could not start 'dotnet'; the .NET 8 SDK must be on PATH for this integration test.", ex);
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort: a test-host handle may still hold a transient lock; leave it for the OS sweep.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort: a file may be momentarily locked or read-only; leave it for the OS sweep.
        }
    }

    private MutationResult RunMutant(string workerRoot, string testProjectRelativePath, MutationJob job)
    {
        using IsolatedMutationWorker worker = new(
            workerRoot, ScopedExecutor(testProjectRelativePath), new NoOpProgressReporter(), 1);
        return GuardDotnet(() => worker.Run(job));
    }

    private TestRun RunScopedTests(string workerRoot, string testProjectRelativePath)
    {
        ITestCommandExecutor executor = ScopedExecutor(testProjectRelativePath);
        return GuardDotnet(() => executor.RunTests(workerRoot, UnboundedTimeout));
    }

    private string CreateWorkerCopy()
    {
        // The real production copy path: CopiedWorkspaceManager copies the repo/workspace root into
        // %TEMP%/mutate4csharp/run-<guid>/worker-1 (excluding bin/obj/.git/.vs/TestResults), carrying the
        // hermetic guards. The handle is disposed best-effort in teardown.
        _workspaces = new CopiedWorkspaceManager().CreateWorkerWorkspaces(_root, 1);
        return _workspaces.WorkerRoots[0];
    }

    private Sample WriteSample()
    {
        HermeticSample.WriteHermeticGuards(_root);

        string sampleDirectory = Path.Combine(_root, "Sample");
        Directory.CreateDirectory(sampleDirectory);
        File.WriteAllText(Path.Combine(sampleDirectory, "Sample.csproj"), ProductionProject);
        string calculatorFile = Path.Combine(sampleDirectory, "Calculator.cs");
        File.WriteAllText(calculatorFile, CalculatorSource);

        string testDirectory = Path.Combine(_root, "Sample.Tests");
        Directory.CreateDirectory(testDirectory);
        string testProjectFile = Path.Combine(testDirectory, "Sample.Tests.csproj");
        File.WriteAllText(testProjectFile, TestProject);
        File.WriteAllText(Path.Combine(testDirectory, "CalculatorTests.cs"), CalculatorTestsSource);

        string strayDirectory = Path.Combine(_root, "Stray");
        Directory.CreateDirectory(strayDirectory);
        File.WriteAllText(Path.Combine(strayDirectory, "Stray.csproj"), StrayProject);
        File.WriteAllText(Path.Combine(strayDirectory, "Broken.cs"), StrayBrokenSource);
        File.WriteAllText(Path.Combine(_root, "Sample.sln"), SolutionFile);

        return new Sample(
            calculatorFile,
            Path.GetRelativePath(_root, calculatorFile),
            Path.GetRelativePath(_root, testProjectFile));
    }

    private void BestEffortDisposeWorkspaces()
    {
        if (_workspaces is null)
        {
            return;
        }

        try
        {
            _workspaces.Dispose();
        }
        catch (InvalidOperationException)
        {
            // WorkerWorkspaceCloser wraps a give-up-after-retries delete failure (a lingering testhost /
            // VBCSCompiler handle on the worker's build output). Best-effort here; leave it for the OS
            // sweep so cleanup never fails the run.
        }
    }

    private sealed record Sample(
        string OriginalCalculatorFile,
        string CalculatorRelativePath,
        string TestProjectRelativePath);
}
