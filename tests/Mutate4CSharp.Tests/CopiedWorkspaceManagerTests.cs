namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Exec;

/// <summary>
/// Faithful counterpart of mutate4java's <c>CopiedWorkspaceManagerTest</c> (adapting the fixture from
/// a Maven module to a C# tree): a worker copy is created, the build/VCS/test output that must never be
/// mirrored is excluded, the source files are copied, the mutated-file path resolves under the worker
/// root, and the run directory is cleaned up on dispose. The one adaptation is the exclusion set —
/// mutate4java excludes <c>target/</c>; the port excludes <c>bin/ obj/ .git/ .vs/ TestResults/</c> — so
/// the C# fixture seeds those directories in place of Maven's <c>target/</c>. Fast temp-dir unit test:
/// no real build runs.
/// </summary>
public sealed class CopiedWorkspaceManagerTests : IDisposable
{
    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="CopiedWorkspaceManagerTests"/> class, creating a
    /// per-test temporary directory (the analog of JUnit's <c>@TempDir</c>).
    /// </summary>
    public CopiedWorkspaceManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-workspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// Deletes the per-test temporary directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Each worker gets a copy of the source tree — with the mutated-file path resolving under the
    /// worker root — while the excluded build/VCS/test output directories are not copied. This is the
    /// port of mutate4java's <c>createsWorkerCopiesWithoutCopyingExistingTargetOutput</c>, widened from
    /// the single <c>target/</c> exclusion to the C# exclusion set.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void CreatesWorkerCopiesExcludingBuildAndVcsOutput()
    {
        string copyRoot = Path.Combine(_tempDir, "demo-module");
        string siteFile = CreateSourceModule(copyRoot);
        CreateExcludedOutput(copyRoot);

        using WorkerWorkspaces workspaces = new CopiedWorkspaceManager().CreateWorkerWorkspaces(copyRoot, 2);

        workspaces.WorkerRoots.Should().HaveCount(2);
        string workerRoot = workspaces.WorkerRoots[0];

        File.Exists(Path.Combine(workerRoot, "Demo.csproj")).Should().BeTrue();

        string mutatedFile = Path.Combine(workerRoot, Path.GetRelativePath(copyRoot, siteFile));
        mutatedFile.Should().StartWith(workerRoot);
        File.Exists(mutatedFile).Should().BeTrue();

        foreach (string excluded in ExcludedDirectoryNames)
        {
            Directory.Exists(Path.Combine(workerRoot, excluded))
                .Should().BeFalse("{0} must not be copied into a worker", excluded);
        }
    }

    /// <summary>
    /// Disposing the workspaces deletes the shared run directory, including any output written into a
    /// worker copy after it was created. Port of mutate4java's
    /// <c>cleansUpWorkerRunDirectoryOnClose</c>.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void CleansUpWorkerRunDirectoryOnDispose()
    {
        string copyRoot = Path.Combine(_tempDir, "demo-module");
        CreateSourceModule(copyRoot);

        WorkerWorkspaces workspaces = new CopiedWorkspaceManager().CreateWorkerWorkspaces(copyRoot, 1);
        string runRoot = workspaces.RunRoot;

        string nestedOutput = Path.Combine(runRoot, "worker-1", "obj", "Debug", "result.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(nestedOutput)!);
        File.WriteAllText(nestedOutput, "done");

        Directory.Exists(runRoot).Should().BeTrue();
        workspaces.Dispose();

        Directory.Exists(runRoot).Should().BeFalse();
    }

    private static readonly string[] ExcludedDirectoryNames = ["bin", "obj", ".git", ".vs", "TestResults"];

    private static string CreateSourceModule(string copyRoot)
    {
        Directory.CreateDirectory(Path.Combine(copyRoot, "src", "Demo"));
        File.WriteAllText(Path.Combine(copyRoot, "Demo.csproj"), "<Project/>");
        string siteFile = Path.Combine(copyRoot, "src", "Demo", "App.cs");
        File.WriteAllText(siteFile, "class App { }");
        return siteFile;
    }

    private static void CreateExcludedOutput(string copyRoot)
    {
        WriteNested(copyRoot, "bin", "Debug", "net8.0", "Demo.dll");
        WriteNested(copyRoot, "obj", "Demo.csproj.nuget.g.props");
        WriteNested(copyRoot, ".git", "config");
        WriteNested(copyRoot, ".vs", "Demo", "state.json");
        WriteNested(copyRoot, "TestResults", "run.trx");
    }

    private static void WriteNested(string root, params string[] segments)
    {
        string path = Path.Combine([root, .. segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "output");
    }
}
