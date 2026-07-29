namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// Faithful counterpart of mutate4java's <c>CliArgumentsParserTest</c>: the same 34 cases asserting
/// the same behaviour and the same error-message strings, verbatim. The only adaptations are the
/// sanctioned ecosystem swaps the Java test itself carries — target paths and rejection messages use
/// <c>.cs</c> / <c>C#</c> instead of <c>.java</c> / <c>Java</c>, and the tool name is
/// <c>mutate4csharp</c>.
/// </summary>
public sealed class CliArgumentsParserTests
{
    private const string Target = "src/Demo/App.cs";

    /// <summary>An empty argument vector is rejected as missing the target file.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsMissingFileArgument()
    {
        ExpectUsageError([], "mutate4csharp requires exactly one C# file");
    }

    /// <summary>A single file argument parses with every default in place.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ParsesSingleExplicitFileArgument()
    {
        CliArguments parsed = CliArgumentsParser.Parse([Target]);

        parsed.Mode.Should().Be(CliMode.ExplicitFiles);
        parsed.FileArgs.Should().Equal(Target);
        parsed.Lines.Should().BeEmpty();
        parsed.Scan.Should().BeFalse();
        parsed.UpdateManifest.Should().BeFalse();
        parsed.ReuseCoverage.Should().BeFalse();
        parsed.SinceLastRun.Should().BeFalse();
        parsed.MutateAll.Should().BeFalse();
        parsed.TimeoutFactor.Should().Be(10);
        parsed.MutationWarning.Should().Be(50);
        parsed.MaxWorkers.Should().Be(Math.Max(1, Environment.ProcessorCount / 2));
        parsed.TestCommand.Should().BeNull();
        parsed.Verbose.Should().BeFalse();
    }

    /// <summary>The line filter and timeout factor parse into their resolved values.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ParsesLineFilterAndTimeoutFactor()
    {
        CliArguments parsed = CliArgumentsParser.Parse([Target, "--lines", "12,18", "--timeout-factor", "15"]);

        int[] expectedLines = [12, 18];
        parsed.Mode.Should().Be(CliMode.ExplicitFiles);
        parsed.Lines.Should().BeEquivalentTo(expectedLines);
        parsed.TimeoutFactor.Should().Be(15);
    }

    /// <summary>The max-workers value parses.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ParsesMaxWorkers()
    {
        CliArguments parsed = CliArgumentsParser.Parse([Target, "--max-workers", "4"]);

        parsed.MaxWorkers.Should().Be(4);
    }

    /// <summary>The verbose flag parses.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ParsesVerboseFlag()
    {
        CliArguments parsed = CliArgumentsParser.Parse([Target, "--verbose"]);

        parsed.Verbose.Should().BeTrue();
    }

    /// <summary>The scan flag parses.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ParsesScanFlag()
    {
        CliArguments parsed = CliArgumentsParser.Parse([Target, "--scan"]);

        parsed.Scan.Should().BeTrue();
    }

    /// <summary>The update-manifest flag parses.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ParsesUpdateManifestFlag()
    {
        CliArguments parsed = CliArgumentsParser.Parse([Target, "--update-manifest"]);

        parsed.UpdateManifest.Should().BeTrue();
    }

    /// <summary>The reuse-coverage flag parses.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ParsesReuseCoverageFlag()
    {
        CliArguments parsed = CliArgumentsParser.Parse([Target, "--reuse-coverage"]);

        parsed.ReuseCoverage.Should().BeTrue();
    }

    /// <summary>The differential flags, warning threshold, and test command parse together.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ParsesDifferentialFlagsAndTestCommand()
    {
        CliArguments parsed = CliArgumentsParser.Parse(
        [
            Target,
            "--since-last-run",
            "--mutation-warning", "75",
            "--test-command", "dotnet test",
        ]);

        parsed.SinceLastRun.Should().BeTrue();
        parsed.MutateAll.Should().BeFalse();
        parsed.MutationWarning.Should().Be(75);
        parsed.TestCommand.Should().Be("dotnet test");
    }

    /// <summary>The mutate-all flag parses.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ParsesMutateAllFlag()
    {
        CliArguments parsed = CliArgumentsParser.Parse([Target, "--mutate-all"]);

        parsed.MutateAll.Should().BeTrue();
    }

    /// <summary>The help flag selects help mode.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ParsesHelpMode()
    {
        CliArguments parsed = CliArgumentsParser.Parse(["--help"]);

        parsed.Mode.Should().Be(CliMode.Help);
    }

    /// <summary>Two file arguments are rejected.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsMultipleFileArguments()
    {
        ExpectUsageError([Target, "src/Demo/Other.cs"], "mutate4csharp accepts exactly one C# file");
    }

    /// <summary>A line filter without a target file is rejected as missing the target file.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsLinesWithoutFileArgument()
    {
        ExpectUsageError(["--lines", "12"], "mutate4csharp requires exactly one C# file");
    }

    /// <summary>A non-<c>.cs</c> target is rejected.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsNonCSharpTarget()
    {
        ExpectUsageError(["crap4csharp"], "mutate4csharp target must be a .cs file");
    }

    /// <summary>A non-positive timeout factor is rejected.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsNonPositiveTimeoutFactor()
    {
        ExpectUsageError([Target, "--timeout-factor", "0"], "--timeout-factor must be a positive integer");
    }

    /// <summary>A non-positive max-workers value is rejected.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsNonPositiveMaxWorkers()
    {
        ExpectUsageError([Target, "--max-workers", "0"], "--max-workers must be a positive integer");
    }

    /// <summary>An unknown option is rejected, naming the option.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsUnknownOption()
    {
        ExpectUsageError([Target, "--bogus"], "Unknown option: --bogus");
    }

    /// <summary><c>--lines</c> may not be combined with <c>--since-last-run</c>.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsLinesCombinedWithSinceLastRun()
    {
        ExpectUsageError([Target, "--lines", "5", "--since-last-run"], "--lines may not be combined with --since-last-run");
    }

    /// <summary><c>--lines</c> may not be combined with <c>--mutate-all</c>.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsLinesCombinedWithMutateAll()
    {
        ExpectUsageError([Target, "--lines", "5", "--mutate-all"], "--lines may not be combined with --mutate-all");
    }

    /// <summary><c>--since-last-run</c> may not be combined with <c>--mutate-all</c>.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsSinceLastRunCombinedWithMutateAll()
    {
        ExpectUsageError([Target, "--since-last-run", "--mutate-all"], "--since-last-run may not be combined with --mutate-all");
    }

    /// <summary><c>--scan</c> may not be combined with <c>--since-last-run</c>.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsScanCombinedWithSinceLastRun()
    {
        ExpectUsageError([Target, "--scan", "--since-last-run"], "--scan may not be combined with --since-last-run");
    }

    /// <summary><c>--scan</c> may not be combined with <c>--update-manifest</c>.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsScanCombinedWithUpdateManifest()
    {
        ExpectUsageError([Target, "--scan", "--update-manifest"], "--scan may not be combined with --update-manifest");
    }

    /// <summary><c>--scan</c> may not be combined with <c>--reuse-coverage</c>.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsScanCombinedWithReuseCoverage()
    {
        ExpectUsageError([Target, "--scan", "--reuse-coverage"], "--scan may not be combined with --reuse-coverage");
    }

    /// <summary><c>--update-manifest</c> may not be combined with <c>--since-last-run</c>.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsUpdateManifestCombinedWithSinceLastRun()
    {
        ExpectUsageError([Target, "--update-manifest", "--since-last-run"], "--update-manifest may not be combined with --since-last-run");
    }

    /// <summary><c>--update-manifest</c> may not be combined with <c>--mutate-all</c>.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsUpdateManifestCombinedWithMutateAll()
    {
        ExpectUsageError([Target, "--update-manifest", "--mutate-all"], "--update-manifest may not be combined with --mutate-all");
    }

    /// <summary><c>--update-manifest</c> may not be combined with <c>--reuse-coverage</c>.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsUpdateManifestCombinedWithReuseCoverage()
    {
        ExpectUsageError([Target, "--update-manifest", "--reuse-coverage"], "--update-manifest may not be combined with --reuse-coverage");
    }

    /// <summary><c>--update-manifest</c> may not be combined with <c>--lines</c>.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsUpdateManifestCombinedWithLines()
    {
        ExpectUsageError([Target, "--update-manifest", "--lines", "5"], "--update-manifest may not be combined with --lines");
    }

    /// <summary><c>--scan</c> may not be combined with <c>--mutate-all</c>.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsScanCombinedWithMutateAll()
    {
        ExpectUsageError([Target, "--scan", "--mutate-all"], "--scan may not be combined with --mutate-all");
    }

    /// <summary>A <c>--lines</c> flag with no value is rejected.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsMissingLinesValue()
    {
        ExpectUsageError([Target, "--lines"], "--lines requires a value");
    }

    /// <summary>An all-blank <c>--lines</c> value is rejected.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsBlankLinesValue()
    {
        ExpectUsageError([Target, "--lines", ",,"], "--lines requires at least one line number");
    }

    /// <summary>A non-numeric <c>--lines</c> value is rejected.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsNonNumericLinesValue()
    {
        ExpectUsageError([Target, "--lines", "a"], "--lines must be a positive integer");
    }

    /// <summary>A <c>--timeout-factor</c> flag with no value is rejected.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsMissingTimeoutFactorValue()
    {
        ExpectUsageError([Target, "--timeout-factor"], "--timeout-factor requires a value");
    }

    /// <summary>A <c>--max-workers</c> flag with no value is rejected.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsMissingMaxWorkersValue()
    {
        ExpectUsageError([Target, "--max-workers"], "--max-workers requires a value");
    }

    /// <summary>A blank <c>--test-command</c> value is rejected.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsBlankTestCommand()
    {
        ExpectUsageError([Target, "--test-command", "   "], "--test-command must not be blank");
    }

    private static void ExpectUsageError(string[] args, string message)
    {
        Action act = () => CliArgumentsParser.Parse(args);

        act.Should().Throw<ArgumentException>().Which.Message.Should().Be(message);
    }
}
