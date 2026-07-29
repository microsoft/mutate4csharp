namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Coverage;

/// <summary>
/// Unit tests for <see cref="CoverageCleaner"/>, the port of mutate4java's <c>CoverageCleaner</c>:
/// the stale coverage results directory is deleted before a fresh run, and a missing directory is a
/// no-op.
/// </summary>
public sealed class CoverageCleanerTests : IDisposable
{
    private readonly string _root;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageCleanerTests"/> class with a per-test
    /// temporary root.
    /// </summary>
    public CoverageCleanerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "m4cs-coverage-cleaner-" + Guid.NewGuid().ToString("N"));
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
    /// The results directory and everything under it — including nested subdirectories and files —
    /// is deleted.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void DeletesResultsDirectoryTree()
    {
        string resultsDirectory = Path.Combine(_root, "TestResults");
        string nested = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "coverage.cobertura.xml"), "<coverage/>");
        File.WriteAllText(Path.Combine(resultsDirectory, "run.trx"), "<TestRun/>");

        new CoverageCleaner().DeleteStaleCoverage(resultsDirectory);

        Directory.Exists(resultsDirectory).Should().BeFalse();
    }

    /// <summary>
    /// Cleaning a directory that does not exist is a no-op rather than an error.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void MissingResultsDirectoryIsANoOp()
    {
        string resultsDirectory = Path.Combine(_root, "does-not-exist");

        Action act = () => new CoverageCleaner().DeleteStaleCoverage(resultsDirectory);

        act.Should().NotThrow();
    }
}
