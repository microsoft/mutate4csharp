namespace Microsoft.Mutate4CSharp.Tests;

using System.Collections.Concurrent;
using Microsoft.Mutate4CSharp.Exec;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Report;

/// <summary>
/// Faithful counterpart of mutate4java's <c>ParallelWorkerPoolTest</c>, plus a deterministic check
/// that N workers drain the shared queue concurrently and that their results are aggregated in
/// <see cref="MutationResult.Order"/> order regardless of input order.
/// </summary>
/// <remarks>
/// mutate4java's <c>wrapsNonExceptionCauseFromWorkerFailure</c> is intentionally not ported: it asserts
/// the JVM's <c>Error</c> vs <c>Exception</c> split (a worker <c>AssertionError</c> rewrapped as
/// <c>IllegalStateException</c>). .NET has no such split — every fault derives from
/// <see cref="System.Exception"/>, so the pool surfaces the worker's original exception unwrapped, which
/// <see cref="RethrowsWorkerFailure"/> already covers.
/// </remarks>
public sealed class ParallelWorkerPoolTests : IDisposable
{
    private const string SampleSource = "class Sample { bool Value() { return true; } }";

    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelWorkerPoolTests"/> class, creating a
    /// per-test temporary directory (the analog of JUnit's <c>@TempDir</c>).
    /// </summary>
    public ParallelWorkerPoolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-pool-" + Guid.NewGuid().ToString("N"));
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
    /// Faithful counterpart of mutate4java's <c>rethrowsCheckedCauseFromWorkerFailure</c>: when a
    /// worker's executor throws, <see cref="ParallelWorkerPool.RunAll"/> rethrows that exact exception.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RethrowsWorkerFailure()
    {
        string workerRoot = Path.Combine(_tempDir, "worker");
        string relative = Path.Combine("src", "Sample.cs");
        WriteSource(Path.Combine(workerRoot, relative));

        MutationJob job = BuildJob(relative, order: 0, totalJobs: 1);
        IOException expected = new("boom");

        using ParallelWorkerPool pool = new(
            [workerRoot],
            new FailingExecutor(expected),
            new NoOpProgressReporter());

        Action runAll = () => pool.RunAll([job]);

        runAll.Should().Throw<IOException>().Which.Should().BeSameAs(expected);
    }

    /// <summary>
    /// Two workers drain a single shared queue of six jobs concurrently, and the pool returns their
    /// results ordered by <see cref="MutationResult.Order"/> even though the jobs are enqueued in
    /// reverse order. Concurrency is proven deterministically: each worker's first job blocks inside the
    /// executor on a two-party <see cref="Barrier"/>, so neither worker can finish until both have
    /// started — a naive single-threaded drain would deadlock rather than pass.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RunsWorkersInParallelAndAggregatesResultsInOrder()
    {
        const int workerCount = 2;
        const int jobCount = 6;

        string relative = Path.Combine("src", "Sample.cs");
        List<string> workerRoots = [];
        for (int index = 0; index < workerCount; index++)
        {
            string workerRoot = Path.Combine(_tempDir, "worker-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            WriteSource(Path.Combine(workerRoot, relative));
            workerRoots.Add(workerRoot);
        }

        // Jobs enqueued in reverse Order (5..0) to prove the collector sorts the aggregate.
        List<MutationJob> jobs = [];
        for (int order = jobCount - 1; order >= 0; order--)
        {
            jobs.Add(BuildJob(relative, order, jobCount));
        }

        using Barrier barrier = new(workerCount);
        BarrierExecutor executor = new(barrier);

        using ParallelWorkerPool pool = new(workerRoots, executor, new NoOpProgressReporter());
        IReadOnlyList<MutationResult> results = pool.RunAll(jobs);

        results.Select(result => result.Order).Should().Equal(Enumerable.Range(0, jobCount));
        executor.SeenRoots.Should().Be(workerCount, "both workers must have drained at least one job");
        executor.TimedOutWaiting.Should().BeFalse("the workers must rendezvous, proving real parallelism");
    }

    private static MutationJob BuildJob(string relative, int order, int totalJobs)
    {
        int start = SampleSource.IndexOf("true", StringComparison.Ordinal);
        MutationSite site = new(
            Path.Combine("repo", relative),
            1,
            start,
            start + "true".Length,
            "true",
            "false",
            "replace true with false");
        return new MutationJob(site, relative, 1000L, order, totalJobs);
    }

    private static void WriteSource(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, SampleSource);
    }

    private sealed class FailingExecutor : ITestCommandExecutor
    {
        private readonly Exception _failure;

        public FailingExecutor(Exception failure)
        {
            _failure = failure;
        }

        public TestRun RunTests(string projectRoot, long timeoutMillis)
        {
            throw _failure;
        }
    }

    private sealed class BarrierExecutor : ITestCommandExecutor
    {
        private readonly Barrier _barrier;
        private readonly ConcurrentDictionary<string, byte> _seenRoots = new(StringComparer.Ordinal);
        private volatile bool _timedOutWaiting;

        public BarrierExecutor(Barrier barrier)
        {
            _barrier = barrier;
        }

        public int SeenRoots => _seenRoots.Count;

        public bool TimedOutWaiting => _timedOutWaiting;

        public TestRun RunTests(string projectRoot, long timeoutMillis)
        {
            // Only the first job seen for a given worker root rendezvous with the peer worker. Because
            // this blocks synchronously inside the worker's Run loop, the worker cannot drain further
            // jobs until every worker has arrived — so a single worker can never grab the whole queue.
            if (_seenRoots.TryAdd(projectRoot, 0) && !_barrier.SignalAndWait(TimeSpan.FromSeconds(30)))
            {
                _timedOutWaiting = true;
            }

            return new TestRun(0, string.Empty, 1, false);
        }
    }
}
