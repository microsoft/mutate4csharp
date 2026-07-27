namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Exec;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Report;

/// <summary>
/// Fine-grained unit tests for <see cref="IsolatedMutationWorker"/> (which mutate4java exercises only
/// indirectly through <c>ParallelWorkerPoolTest</c>): the worker splices the site's replacement into
/// its private copy, runs the injected executor against the worker root, classifies the outcome, and
/// restores the original source afterwards — even when the executor throws. It also pins Anders' T13
/// invariant: the mutated file resolves at <c>Path.Combine(workerRoot, Path.GetRelativePath(copyRoot,
/// site.File))</c> with <c>copyRoot</c> == the repo root.
/// </summary>
public sealed class IsolatedMutationWorkerTests : IDisposable
{
    private const string OriginalSource = "class Sample { bool Value() { return true; } }";
    private const string MutatedSource = "class Sample { bool Value() { return false; } }";

    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="IsolatedMutationWorkerTests"/> class, creating a
    /// per-test temporary directory (the analog of JUnit's <c>@TempDir</c>).
    /// </summary>
    public IsolatedMutationWorkerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-worker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>Deletes the per-test temporary directory.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The worker mutates the copy at the repo-root-relative path for the test run — passing the worker
    /// root as the working directory and the job's timeout through — then restores the original. A
    /// passing test run scores the mutant SURVIVED (not killed).
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void MutatesFileForTestRunThenRestoresOriginal()
    {
        Fixture fixture = CreateFixture();
        CapturingExecutor executor = new(fixture.WorkerFile, new TestRun(0, string.Empty, 7, false));

        using IsolatedMutationWorker worker = new(fixture.WorkerRoot, executor, new NoOpProgressReporter(), 1);
        MutationResult result = worker.Run(fixture.Job);

        // The executor saw the MUTATED copy, at workerRoot/relative, with the job's cwd + timeout.
        executor.ObservedContent.Should().Be(MutatedSource);
        executor.ObservedProjectRoot.Should().Be(fixture.WorkerRoot);
        executor.ObservedTimeoutMillis.Should().Be(fixture.Job.TimeoutMillis);

        // Restored after the run.
        File.ReadAllText(fixture.WorkerFile).Should().Be(OriginalSource);

        result.Killed.Should().BeFalse();
        result.TimedOut.Should().BeFalse();
        result.DurationMillis.Should().Be(7);
        result.Order.Should().Be(fixture.Job.Order);
        result.TotalJobs.Should().Be(fixture.Job.TotalJobs);
    }

    /// <summary>
    /// A failing test run scores the mutant KILLED, and the original source is still restored.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FailingTestRunIsClassifiedAsKilled()
    {
        Fixture fixture = CreateFixture();
        CapturingExecutor executor = new(fixture.WorkerFile, new TestRun(1, string.Empty, 3, false));

        using IsolatedMutationWorker worker = new(fixture.WorkerRoot, executor, new NoOpProgressReporter(), 1);
        MutationResult result = worker.Run(fixture.Job);

        result.Killed.Should().BeTrue();
        result.TimedOut.Should().BeFalse();
        File.ReadAllText(fixture.WorkerFile).Should().Be(OriginalSource);
    }

    /// <summary>
    /// A timed-out test run is scored KILLED with the timeout flag preserved (the sentinel exit 124
    /// path), and the original source is restored.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void TimedOutTestRunIsClassifiedAsKilled()
    {
        Fixture fixture = CreateFixture();
        CapturingExecutor executor = new(fixture.WorkerFile, new TestRun(124, string.Empty, 9, true));

        using IsolatedMutationWorker worker = new(fixture.WorkerRoot, executor, new NoOpProgressReporter(), 1);
        MutationResult result = worker.Run(fixture.Job);

        result.Killed.Should().BeTrue();
        result.TimedOut.Should().BeTrue();
        File.ReadAllText(fixture.WorkerFile).Should().Be(OriginalSource);
    }

    /// <summary>
    /// When the executor throws, the exception propagates unchanged and the original source is still
    /// restored (mutate4java's <c>finally</c> guarantee).
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RestoresOriginalWhenExecutorThrows()
    {
        Fixture fixture = CreateFixture();
        IOException boom = new("boom");
        ThrowingExecutor executor = new(boom);

        using IsolatedMutationWorker worker = new(fixture.WorkerRoot, executor, new NoOpProgressReporter(), 1);
        Action run = () => worker.Run(fixture.Job);

        run.Should().Throw<IOException>().Which.Should().BeSameAs(boom);
        File.ReadAllText(fixture.WorkerFile).Should().Be(OriginalSource);
    }

    private Fixture CreateFixture()
    {
        // copyRoot == the repo root the worker copy was made from (Anders' invariant).
        string copyRoot = Path.Combine(_tempDir, "repo");
        string siteFile = Path.Combine(copyRoot, "src", "Sample.cs");
        string relative = Path.GetRelativePath(copyRoot, siteFile);

        string workerRoot = Path.Combine(_tempDir, "worker");
        string workerFile = Path.Combine(workerRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(workerFile)!);
        File.WriteAllText(workerFile, OriginalSource);

        int start = OriginalSource.IndexOf("true", StringComparison.Ordinal);
        MutationSite site = new(siteFile, 1, start, start + "true".Length, "true", "false", "replace true with false");
        MutationJob job = new(site, relative, 4321, 0, 1);
        return new Fixture(workerRoot, workerFile, job);
    }

    private sealed record Fixture(string WorkerRoot, string WorkerFile, MutationJob Job);

    private sealed class CapturingExecutor : ITestCommandExecutor
    {
        private readonly string _fileToRead;
        private readonly TestRun _result;

        public CapturingExecutor(string fileToRead, TestRun result)
        {
            _fileToRead = fileToRead;
            _result = result;
        }

        public string? ObservedProjectRoot { get; private set; }

        public long ObservedTimeoutMillis { get; private set; }

        public string? ObservedContent { get; private set; }

        public TestRun RunTests(string projectRoot, long timeoutMillis)
        {
            ObservedProjectRoot = projectRoot;
            ObservedTimeoutMillis = timeoutMillis;
            ObservedContent = File.ReadAllText(_fileToRead);
            return _result;
        }
    }

    private sealed class ThrowingExecutor : ITestCommandExecutor
    {
        private readonly Exception _failure;

        public ThrowingExecutor(Exception failure)
        {
            _failure = failure;
        }

        public TestRun RunTests(string projectRoot, long timeoutMillis)
        {
            throw _failure;
        }
    }
}
