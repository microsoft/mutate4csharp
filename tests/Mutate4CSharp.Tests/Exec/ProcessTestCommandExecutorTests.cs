namespace Microsoft.Mutate4CSharp.Tests.Exec;

using Microsoft.Mutate4CSharp.Exec;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Counterpart of mutate4java's <c>ProcessTestCommandExecutorTest</c>. C#'s
/// <see cref="System.Diagnostics.Process"/> members are non-virtual, so the port cannot subclass a
/// throwing/fake process the way the Java oracle does; the run-behavior cases therefore drive a real,
/// trivial platform-shell process through the executor's argv/launcher seam (fast, deterministic,
/// cross-platform — hence <c>UnitTests</c>). The argv-construction cases assert the recorded command
/// with no spawn at all: the A9 <c>--test-command</c> override and the DD3 <c>WithTestProject</c>
/// argv. mutate4java's <c>returnsEmptyOutputWhenReadingProcessOutputFails</c> case is not portable
/// (it needs a process whose output stream throws on read, impossible without a fake process); the
/// executor's own <c>IOException</c> drain guard covers that path in <see cref="TimedProcessRun"/>.
/// </summary>
public sealed class ProcessTestCommandExecutorTests
{
    /// <summary>
    /// A configured argv command runs in the target directory, exits zero, and has its output
    /// captured without being flagged as timed out (the port of Java's
    /// <c>startsConfiguredCommandInTargetDirectory</c> / <c>capturesSuccessfulTestRunOutput</c>).
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RunsConfiguredCommandAndCapturesOutput()
    {
        TestRun result = new ProcessTestCommandExecutor(Shell("echo m4c-run")).RunTests(WorkingDirectory, 0);

        result.ExitCode.Should().Be(0);
        result.Output.Trim().Should().Be("m4c-run");
        result.TimedOut.Should().BeFalse();
    }

    /// <summary>
    /// The A9 <c>WithCommand</c> override records the user command as the final argv token (the port
    /// of Java's <c>startsShellCommandOverrideInTargetDirectory</c>). The assertion inspects only the
    /// last token so it stays cross-platform, independent of the <c>cmd.exe /c</c> vs <c>/bin/sh -lc</c>
    /// shell prefix.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void WithCommandRecordsUserCommandAsFinalArgvToken()
    {
        ProcessTestCommandExecutor executor =
            (ProcessTestCommandExecutor)new ProcessTestCommandExecutor().WithCommand("echo m4c-override");

        executor.Command.Should().NotBeNull();
        executor.Command![^1].Should().Be("echo m4c-override");
    }

    /// <summary>
    /// The DD3 <c>WithTestProject</c> seam builds the <c>dotnet test</c> argv with the unit-only filter
    /// (the faithful port of Java's <c>mvn test -DexcludeTags=no-mutate</c>), using a forward-slash
    /// project path so the argv is identical on Windows and the Linux CI runner.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void WithTestProjectBuildsDotnetTestUnitFilterArgv()
    {
        ProcessTestCommandExecutor executor = (ProcessTestCommandExecutor)
            new ProcessTestCommandExecutor().WithTestProject("Sample.Tests/Sample.Tests.csproj");

        executor.Command.Should().Equal(
            "dotnet",
            "test",
            "Sample.Tests/Sample.Tests.csproj",
            "--filter",
            "type!=IntegrationTests&Category!=no-mutate");
    }

    /// <summary>
    /// A test run that outlives its timeout is killed (entire tree) and reported with the sentinel exit
    /// code <c>124</c> and <see cref="TestRun.TimedOut"/> set (the port of Java's
    /// <c>returnsTimeoutExitCodeWhenTestRunTakesTooLong</c>). The five-second sleep against a 200 ms
    /// timeout keeps the outcome deterministic rather than timing-sensitive.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReturnsTimeoutSentinelWhenTestRunExceedsTimeout()
    {
        TestRun result = new ProcessTestCommandExecutor(SleepCommand()).RunTests(WorkingDirectory, 200);

        result.ExitCode.Should().Be(124);
        result.TimedOut.Should().BeTrue();
    }

    /// <summary>
    /// A failing test run still captures its output and propagates the non-zero exit code verbatim,
    /// without being flagged as timed out — the T11 output-capture responsibility.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void CapturesOutputAndPropagatesExitCodeWhenTestRunFails()
    {
        TestRun result = new ProcessTestCommandExecutor(FailingCommand()).RunTests(WorkingDirectory, 0);

        result.ExitCode.Should().Be(3);
        result.Output.Should().Contain("m4c-fail");
        result.TimedOut.Should().BeFalse();
    }

    private static string WorkingDirectory => Path.GetTempPath();

    private static IReadOnlyList<string> Shell(string script)
    {
        return OperatingSystem.IsWindows()
            ? ["cmd.exe", "/c", script]
            : ["/bin/sh", "-c", script];
    }

    private static IReadOnlyList<string> SleepCommand()
    {
        return Shell(OperatingSystem.IsWindows() ? "ping -n 5 127.0.0.1" : "sleep 5");
    }

    private static IReadOnlyList<string> FailingCommand()
    {
        return Shell(OperatingSystem.IsWindows() ? "echo m4c-fail& exit 3" : "echo m4c-fail; exit 3");
    }
}
