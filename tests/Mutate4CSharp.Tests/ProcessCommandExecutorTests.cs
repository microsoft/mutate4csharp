namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Exec;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Counterpart of mutate4java's <c>ProcessCommandExecutorTest</c>. mutate4java drives a real process
/// via <c>sh -c</c>; the port spawns the platform shell that always exists on both this repo's
/// Windows dev box and the Linux CI (<c>cmd.exe</c> vs <c>/bin/sh</c>), so the tests stay fast,
/// deterministic, and cross-platform — hence <c>UnitTests</c>. They assert the same behaviors:
/// successful output capture and the timeout sentinel, plus the C# additions the T11 executor is
/// responsible for — merging standard error into the output and propagating a non-zero exit code. The
/// heavier real-<c>dotnet test</c> exercise of the test executor is T17.
/// </summary>
public sealed class ProcessCommandExecutorTests
{
    /// <summary>
    /// A command that exits zero has its standard output captured and is not flagged as timed out.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void CapturesSuccessfulCommandOutput()
    {
        CommandResult result = new ProcessCommandExecutor().Run(Shell("echo ok"), WorkingDirectory);

        result.ExitCode.Should().Be(0);
        result.Output.Trim().Should().Be("ok");
        result.TimedOut.Should().BeFalse();
    }

    /// <summary>
    /// A command that outlives its timeout is killed (entire tree) and reported with the sentinel exit
    /// code <c>124</c> and <see cref="CommandResult.TimedOut"/> set.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReturnsTimeoutExitCodeWhenCommandTakesTooLong()
    {
        CommandResult result = new ProcessCommandExecutor().Run(SleepCommand(), WorkingDirectory, 200);

        result.ExitCode.Should().Be(124);
        result.TimedOut.Should().BeTrue();
    }

    /// <summary>
    /// Standard error is merged into the captured output (the analog of Java's
    /// <c>redirectErrorStream(true)</c>), so a failing command stays informative.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void MergesStandardErrorIntoOutput()
    {
        CommandResult result = new ProcessCommandExecutor().Run(Shell("echo err 1>&2"), WorkingDirectory);

        result.ExitCode.Should().Be(0);
        result.Output.Should().Contain("err");
        result.TimedOut.Should().BeFalse();
    }

    /// <summary>
    /// A non-zero exit code is propagated verbatim and the run is not flagged as timed out.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReturnsNonZeroExitCode()
    {
        CommandResult result = new ProcessCommandExecutor().Run(Shell("exit 3"), WorkingDirectory);

        result.ExitCode.Should().Be(3);
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
}
