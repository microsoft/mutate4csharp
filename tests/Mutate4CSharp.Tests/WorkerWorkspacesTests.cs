namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Exec;

/// <summary>
/// Faithful counterpart of mutate4java's <c>WorkerWorkspacesTest</c>: exercises the retrying cleanup
/// through the injected <see cref="WorkerWorkspaces.DeleteAttempt"/> / <see cref="WorkerWorkspaces.RetrySleeper"/>
/// / <see cref="WorkerWorkspaces.DeleteTree"/> seams, so the Windows file-lock retry is deterministic
/// (no real sleeps or locks). It also covers the <see cref="WorkerWorkspaces.Dispose"/> tree deletion.
/// </summary>
/// <remarks>
/// mutate4java's <c>deleteWithRetriesThrowsForNonRetryableFailure</c> asserts a fail-fast on a
/// non-<c>DirectoryNotEmptyException</c> <c>IOException</c>. .NET has no such subtype — a still-locked
/// worker tree surfaces as a plain <see cref="IOException"/>, so every captured <see cref="IOException"/>
/// is the retryable transient failure (see <see cref="WorkerCleanup"/>). That branch therefore has no
/// ecosystem analog and is intentionally not ported; the retry-then-succeed and give-up paths are.
/// </remarks>
public sealed class WorkerWorkspacesTests : IDisposable
{
    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerWorkspacesTests"/> class, creating a
    /// per-test temporary directory (the analog of JUnit's <c>@TempDir</c>).
    /// </summary>
    public WorkerWorkspacesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-workspaces-" + Guid.NewGuid().ToString("N"));
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
    /// Disposing a handle whose run root never existed is a no-op. Port of mutate4java's
    /// <c>closeDoesNothingWhenRunRootDoesNotExist</c>.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void CloseDoesNothingWhenRunRootDoesNotExist()
    {
        using WorkerWorkspaces workspaces = new(Path.Combine(_tempDir, "missing"), []);

        Action dispose = workspaces.Dispose;

        dispose.Should().NotThrow();
    }

    /// <summary>
    /// Disposing deletes the whole run-root tree, including nested output. Port of mutate4java's
    /// <c>closeDeletesRunRootTree</c>.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void CloseDeletesRunRootTree()
    {
        string runRoot = Path.Combine(_tempDir, "run");
        string nestedFile = Path.Combine(runRoot, "worker-1", "obj", "Debug", "result.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(nestedFile)!);
        File.WriteAllText(nestedFile, "done");
        WorkerWorkspaces workspaces = new(runRoot, [Path.Combine(runRoot, "worker-1")]);

        workspaces.Dispose();

        Directory.Exists(runRoot).Should().BeFalse();
    }

    /// <summary>
    /// A transient failure is retried, sleeping between attempts, until a later attempt succeeds:
    /// the delete seam runs one more time than it fails, and the sleeper is invoked once per failure.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void DeleteWithRetriesRetriesThenSucceeds()
    {
        const int failuresBeforeSuccess = 2;
        int attempts = 0;
        int sleeps = 0;
        IOException busy = new("busy");

        IOException? result = WorkerWorkspaces.DeleteWithRetries(
            _tempDir,
            _ => ++attempts <= failuresBeforeSuccess ? busy : null,
            () => sleeps++);

        result.Should().BeNull();
        attempts.Should().Be(failuresBeforeSuccess + 1);
        sleeps.Should().Be(failuresBeforeSuccess);
    }

    /// <summary>
    /// When every attempt keeps failing, the last failure is returned after the retry limit and the
    /// sleeper was invoked once per attempt. Port of mutate4java's
    /// <c>deleteWithRetriesReturnsLastDirectoryNotEmptyFailureAfterRetryLimit</c>.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void DeleteWithRetriesReturnsFailureAfterRetryLimit()
    {
        int sleeps = 0;
        IOException busy = new("busy");

        IOException? result = WorkerWorkspaces.DeleteWithRetries(_tempDir, _ => busy, () => sleeps++);

        result.Should().BeSameAs(busy);
        sleeps.Should().Be(WorkerCleanup.DeleteRetries);
    }

    /// <summary>
    /// <see cref="WorkerWorkspaces.TryDelete(string, WorkerWorkspaces.DeleteTree)"/> captures and
    /// returns the <see cref="IOException"/> the delete seam throws. Port of mutate4java's
    /// <c>tryDeleteReturnsIOExceptionFromDeleteTree</c>.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void TryDeleteReturnsIOExceptionFromDeleteTree()
    {
        IOException failure = new("boom");

        IOException? result = WorkerWorkspaces.TryDelete(_tempDir, _ => throw failure);

        result.Should().BeSameAs(failure);
    }
}
