namespace Microsoft.Mutate4CSharp.Tests.Integration;

using System.ComponentModel;
using Microsoft.Mutate4CSharp.Coverage;
using Microsoft.Mutate4CSharp.Exec;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Project;

/// <summary>
/// T16 end-to-end integration test for the whole coverage path. It generates a tiny real .NET 8
/// sample (a production project plus an xUnit test project wired with <c>coverlet.collector</c>) into
/// a unique temp directory, drives the REAL <see cref="CoverageRunner"/> over a real
/// <see cref="ProcessCommandExecutor"/> — which runs <c>dotnet test --collect:"XPlat Code Coverage"
/// -p:DeterministicSourcePaths=false</c> — and asserts that the produced coverlet Cobertura report
/// reconciles against on-disk paths through <see cref="CoberturaLineCoverageParser"/>: a line the
/// single test executes resolves to an A4 key that reads covered (hits &gt; 0), a line it never
/// executes reads uncovered, and the DD2(b) executed-test count is positive. Unlike the synthetic-XML
/// unit tests (<c>CoverageRunnerTests</c>, <c>CoberturaLineCoverageParserTests</c>), this proves the
/// coverlet → Cobertura → A4-keying adapter works on a live run — the reconciliation Anders' T8 review
/// flagged as the one thing synthetic XML cannot verify. There is no Java oracle (mutate4java used
/// JaCoCo); this validates the C#/coverlet ecosystem adapter, which is the point of slice S6. The
/// class is tagged <c>[Trait("type", "IntegrationTests")]</c> so DD3 excludes it from the fast unit
/// run and it stays filterable via <c>--filter "type=IntegrationTests"</c>.
/// </summary>
[Trait("type", "IntegrationTests")]
public sealed class CoverageRunnerIntegrationTests : IDisposable
{
    private const string HitMarker = "\"hit\"";
    private const string MissMarker = "\"miss\"";

    private const string NuGetConfig =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
          </packageSources>
        </configuration>
        """;

    private const string SampleProject =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>
        </Project>
        """;

    private const string SampleTestProject =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <IsPackable>false</IsPackable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
            <PackageReference Include="xunit" Version="2.5.3" />
            <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
            <PackageReference Include="coverlet.collector" Version="6.0.0" />
          </ItemGroup>
          <ItemGroup>
            <ProjectReference Include="..\Sample\Sample.csproj" />
          </ItemGroup>
        </Project>
        """;

    private const string CalculatorTestsSource =
        """
        using Sample;
        using Xunit;

        public sealed class CalculatorTests
        {
            [Fact]
            public void ClassifyPositiveReturnsHit()
            {
                Assert.Equal("hit", Calculator.Classify(1));
            }
        }
        """;

    private readonly string _root;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageRunnerIntegrationTests"/> class, creating
    /// the unique temp root that holds the generated sample and, after the run, its coverage
    /// artifacts.
    /// </summary>
    public CoverageRunnerIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "mutate4csharp-it", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    /// <summary>
    /// Best-effort deletes the temp root, tolerating <see cref="IOException"/> and
    /// <see cref="UnauthorizedAccessException"/> because the test host may still hold a transient
    /// handle under the sample's build output when the run has only just finished.
    /// </summary>
    public void Dispose()
    {
        TryDeleteDirectory(_root);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A real coverlet run over the generated sample yields a Cobertura report whose executed line
    /// reconciles to a present, hit A4 key, whose never-executed line reads not covered, and whose
    /// baseline executed at least one test (DD2(b)).
    /// </summary>
    [Fact]
    [Trait("type", "IntegrationTests")]
    public void RealCoverletRunReconcilesCoveredAndUncoveredLines()
    {
        string calculatorSource = CalculatorSource();
        string sampleProjectDirectory = WriteSample(calculatorSource);
        string testProjectFile = Path.Combine(_root, "Sample.Tests", "Sample.Tests.csproj");
        ModuleResolution module = ModuleResolution.Resolved(
            "Sample", Path.Combine(sampleProjectDirectory, "Sample.csproj"), testProjectFile);

        CoverageRun run = RunCoverage(module);

        run.Baseline.Should().NotBeNull();
        run.Baseline!.ExitCode.Should().Be(0, "the sample baseline must pass; dotnet output was:\n{0}", run.Baseline.Output);
        run.ReportAvailable.Should().BeTrue("a real coverlet run must produce a coverage.cobertura.xml");
        run.ExecutedTestCount.Should().BePositive("the sample's single [Fact] must be executed (DD2(b))");

        string key = CoberturaLineCoverageParser.NormalizeSourcePath(
            Path.Combine(sampleProjectDirectory, "Calculator.cs"));
        run.Report.Covers(key, LineOf(calculatorSource, HitMarker))
            .Should().BeTrue("the executed line's A4 key must reconcile against the real coverlet <source> base");
        run.Report.Covers(key, LineOf(calculatorSource, MissMarker))
            .Should().BeFalse("the never-executed line must read uncovered");
    }

    private static CoverageRun RunCoverage(ModuleResolution module)
    {
        try
        {
            return new CoverageRunner(new ProcessCommandExecutor()).GenerateCoverage(module);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Could not start 'dotnet'; the .NET 8 SDK must be on PATH for this integration test.", ex);
        }
    }

    private static string CalculatorSource()
    {
        // A branch the test takes (return "hit") and one it never takes (return "miss"): coverlet
        // reports the taken return line with hits > 0 and the untaken one with hits = 0, giving one
        // known-covered and one known-uncovered line without depending on hard-coded line numbers.
        return
            """
            namespace Sample;

            public static class Calculator
            {
                public static string Classify(int value)
                {
                    if (value > 0)
                    {
                        return "hit";
                    }

                    return "miss";
                }
            }
            """;
    }

    private static int LineOf(string source, string marker)
    {
        string[] lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            if (lines[index].Contains(marker, StringComparison.Ordinal))
            {
                return index + 1;
            }
        }

        throw new InvalidOperationException($"Marker '{marker}' not found in the generated sample source.");
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
            // Best-effort: the test host may still hold a transient handle under the sample's output.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort: a file may be momentarily locked or read-only; leave it for the OS sweep.
        }
    }

    private string WriteSample(string calculatorSource)
    {
        // Hermetic guard: empty Directory.Build.{props,targets} stop MSBuild from walking above the
        // temp root and inheriting a machine-level import that could break the sample build;
        // nuget.config pins restore to nuget.org, served from the global cache the main solution's
        // restore already populated with these exact package versions.
        File.WriteAllText(Path.Combine(_root, "Directory.Build.props"), "<Project />");
        File.WriteAllText(Path.Combine(_root, "Directory.Build.targets"), "<Project />");
        File.WriteAllText(Path.Combine(_root, "nuget.config"), NuGetConfig);

        string sampleDirectory = Path.Combine(_root, "Sample");
        Directory.CreateDirectory(sampleDirectory);
        File.WriteAllText(Path.Combine(sampleDirectory, "Sample.csproj"), SampleProject);
        File.WriteAllText(Path.Combine(sampleDirectory, "Calculator.cs"), calculatorSource);

        string testDirectory = Path.Combine(_root, "Sample.Tests");
        Directory.CreateDirectory(testDirectory);
        File.WriteAllText(Path.Combine(testDirectory, "Sample.Tests.csproj"), SampleTestProject);
        File.WriteAllText(Path.Combine(testDirectory, "CalculatorTests.cs"), CalculatorTestsSource);

        return sampleDirectory;
    }
}
