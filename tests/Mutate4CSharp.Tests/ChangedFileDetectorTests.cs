namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Exec;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Project;

/// <summary>
/// Counterpart of mutate4java's <c>ChangedFileDetectorTest</c>, re-expressed as a pure unit test:
/// mutate4java drives real <c>git</c>, whereas the port injects a stub <see cref="ICommandExecutor"/>
/// so the parsing/filtering/sorting logic is exercised deterministically without a repository. Same
/// intent — modified and untracked <c>.cs</c> files under <c>src</c> are returned, ordinally sorted —
/// plus coverage of rename targets, the exact <c>git</c> command, and the failure path.
/// </summary>
public sealed class ChangedFileDetectorTests
{
    /// <summary>
    /// Modified and untracked <c>.cs</c> files under <c>src</c> are returned ordinally sorted; a
    /// non-<c>.cs</c> file and a <c>.cs</c> file outside <c>src</c> are excluded.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FindsModifiedAndUntrackedCSharpFilesUnderSrc()
    {
        string root = TempRoot();
        string porcelain = string.Join(
            "\n",
            " M src/Demo/Tracked.cs",
            "?? src/Demo/NewFile.cs",
            " M README.md",
            " M docs/Notes.cs");
        StubCommandExecutor stub = new(new CommandResult(0, porcelain, 0, false));

        IReadOnlyList<string> changed = ChangedFileDetector.ChangedCSharpFilesUnderSrc(stub, root);

        changed.Should().Equal(
            Path.GetFullPath(Path.Combine(root, "src/Demo/NewFile.cs")),
            Path.GetFullPath(Path.Combine(root, "src/Demo/Tracked.cs")));
    }

    /// <summary>
    /// The executor is invoked with <c>git -C &lt;root&gt; status --porcelain</c> scoped to the
    /// project root.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RunsGitStatusPorcelainScopedToProjectRoot()
    {
        string root = TempRoot();
        StubCommandExecutor stub = new(new CommandResult(0, string.Empty, 0, false));

        ChangedFileDetector.ChangedCSharpFilesUnderSrc(stub, root);

        stub.LastCommand.Should().Equal("git", "-C", root, "status", "--porcelain");
        stub.LastWorkingDirectory.Should().Be(root);
    }

    /// <summary>
    /// A porcelain rename entry resolves to the rename target path.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ResolvesRenameTargetPath()
    {
        string root = TempRoot();
        StubCommandExecutor stub = new(new CommandResult(0, "R  src/Demo/Old.cs -> src/Demo/Renamed.cs", 0, false));

        ChangedFileDetector.ChangedCSharpFilesUnderSrc(stub, root)
            .Should().Equal(Path.GetFullPath(Path.Combine(root, "src/Demo/Renamed.cs")));
    }

    /// <summary>
    /// A non-zero <c>git</c> exit throws, surfacing the merged output.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ThrowsWhenGitStatusFails()
    {
        string root = TempRoot();
        StubCommandExecutor stub = new(new CommandResult(128, "fatal: not a git repository", 0, false));

        Action act = () => ChangedFileDetector.ChangedCSharpFilesUnderSrc(stub, root);

        act.Should().Throw<InvalidOperationException>().WithMessage("git status failed: *");
    }

    private static string TempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "m4cs-changed-" + Guid.NewGuid().ToString("N"));
    }

    private sealed class StubCommandExecutor : ICommandExecutor
    {
        private readonly CommandResult _result;

        public StubCommandExecutor(CommandResult result)
        {
            _result = result;
        }

        public IReadOnlyList<string>? LastCommand { get; private set; }

        public string? LastWorkingDirectory { get; private set; }

        public CommandResult Run(IReadOnlyList<string> command, string workingDirectory)
        {
            LastCommand = command;
            LastWorkingDirectory = workingDirectory;
            return _result;
        }
    }
}
