namespace Microsoft.Mutate4CSharp.Tests;

using System.Globalization;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Report;

/// <summary>
/// Tests the two <see cref="IProgressReporter"/> implementations: <see cref="NoOpProgressReporter"/>
/// discards every callback, while <see cref="PrintStreamProgressReporter"/> writes each formatted
/// line to its <see cref="TextWriter"/>.
/// </summary>
public sealed class ProgressReporterTests
{
    /// <summary>The print-stream reporter writes the formatter's baseline-finished line to its writer.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void PrintStreamReporterWritesFormattedLine()
    {
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        PrintStreamProgressReporter reporter = new(writer);

        reporter.BaselineFinished(new TestRun(0, string.Empty, 1200, false));

        writer.ToString().Should().Be("Baseline finished: exit=0 timedOut=false duration=1200 ms\n");
    }

    /// <summary>The no-op reporter writes nothing and never throws.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void NoOpReporterDoesNothing()
    {
        NoOpProgressReporter reporter = new();

        reporter.BaselineStarting("/module/root");
        reporter.BaselineFinished(new TestRun(0, string.Empty, 1, false));
        reporter.RunStarting(1, 1);

        // No observable output and no exception — the callbacks are inert.
    }
}
