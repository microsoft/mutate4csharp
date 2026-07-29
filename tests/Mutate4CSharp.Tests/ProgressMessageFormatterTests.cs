namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Report;

/// <summary>
/// Golden-string tests for <see cref="ProgressMessageFormatter"/>. mutate4java's <c>%n</c> platform
/// newline is rendered as an explicit <c>"\n"</c>, its booleans lowercase, and its 0-based order
/// displayed 1-based (<c>order + 1</c>).
/// </summary>
public sealed class ProgressMessageFormatterTests
{
    private readonly ProgressMessageFormatter _formatter = new();

    /// <summary>The baseline-starting line names the module root.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FormatsBaselineStarting()
    {
        _formatter.BaselineStarting("/module/root").Should().Be("Baseline starting for /module/root\n");
    }

    /// <summary>The baseline-finished line renders the exit code, lowercase timeout flag, and duration.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FormatsBaselineFinishedWithLowercaseBoolean()
    {
        _formatter.BaselineFinished(new TestRun(0, string.Empty, 1200, false))
            .Should().Be("Baseline finished: exit=0 timedOut=false duration=1200 ms\n");
    }

    /// <summary>The run-starting line renders the mutation and worker counts.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FormatsRunStarting()
    {
        _formatter.RunStarting(3, 2).Should().Be("Running 3 mutations with 2 workers.\n");
    }

    /// <summary>The mutation-starting line renders the 1-based job position, file, line, and description.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FormatsMutationStartingWithOneBasedPosition()
    {
        MutationJob job = new(Site(5, "replace + with -"), "Sample.cs", 1000, 0, 2);

        _formatter.MutationStarting(1, job).Should().Be("Worker 1 starting 1/2: Sample.cs:5 replace + with -\n");
    }

    /// <summary>The mutation-finished line renders the 1-based position and the KILLED/SURVIVED verdict.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FormatsMutationFinishedWithVerdict()
    {
        MutationResult result = new(Site(5, "replace + with -"), true, 12, false, 1, 2);

        _formatter.MutationFinished(1, result).Should().Be("Worker 1 finished 2/2: KILLED Sample.cs:5\n");
    }

    private static MutationSite Site(int line, string description)
    {
        return new MutationSite("Sample.cs", line, 0, 1, "a", "b", description);
    }
}
