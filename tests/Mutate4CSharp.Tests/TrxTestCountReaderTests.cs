namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Coverage;

/// <summary>
/// Unit tests for <see cref="TrxTestCountReader"/>, the DD2(b) executed-test counter that has no
/// mutate4java analog. They cover the primary <c>Counters/@executed</c> path (including the
/// namespaced TRX the VSTest logger emits and the zero fail-fast case), the
/// <c>UnitTestResult</c>-count fallback, and the fail-closed handling of null, missing, and malformed
/// inputs.
/// </summary>
public sealed class TrxTestCountReaderTests : IDisposable
{
    private readonly string _root;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrxTestCountReaderTests"/> class with a per-test
    /// temporary root.
    /// </summary>
    public TrxTestCountReaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "m4cs-trx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    /// <summary>
    /// Deletes the per-test temporary root.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The executed count is read from <c>ResultSummary/Counters/@executed</c> even though the TRX
    /// carries the VSTest default XML namespace.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReadsExecutedCountFromNamespacedTrx()
    {
        string trx = Write(
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <ResultSummary outcome="Completed">
                <Counters total="7" executed="7" passed="7" failed="0" />
              </ResultSummary>
            </TestRun>
            """);

        TrxTestCountReader.CountExecutedTests(trx).Should().Be(7);
    }

    /// <summary>
    /// A run that executed no tests reports a zero count (the DD2(b) fail-fast signal).
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReadsZeroWhenNoTestsExecuted()
    {
        string trx = Write(
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <ResultSummary outcome="Completed">
                <Counters total="0" executed="0" passed="0" failed="0" />
              </ResultSummary>
            </TestRun>
            """);

        TrxTestCountReader.CountExecutedTests(trx).Should().Be(0);
    }

    /// <summary>
    /// When no <c>Counters/@executed</c> attribute is present, the recorded <c>UnitTestResult</c>
    /// entries are counted instead.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FallsBackToUnitTestResultCountWhenNoExecutedAttribute()
    {
        string trx = Write(
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results>
                <UnitTestResult testName="A" outcome="Passed" />
                <UnitTestResult testName="B" outcome="Passed" />
              </Results>
            </TestRun>
            """);

        TrxTestCountReader.CountExecutedTests(trx).Should().Be(2);
    }

    /// <summary>
    /// A null or missing path fails closed to zero.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReturnsZeroForNullOrMissingPath()
    {
        TrxTestCountReader.CountExecutedTests(null).Should().Be(0);
        TrxTestCountReader.CountExecutedTests(Path.Combine(_root, "missing.trx")).Should().Be(0);
    }

    /// <summary>
    /// Malformed XML fails closed to zero rather than throwing.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReturnsZeroForMalformedXml()
    {
        string trx = Write("<TestRun><not-closed>");

        TrxTestCountReader.CountExecutedTests(trx).Should().Be(0);
    }

    private string Write(string content)
    {
        string path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".trx");
        File.WriteAllText(path, content);
        return path;
    }
}
