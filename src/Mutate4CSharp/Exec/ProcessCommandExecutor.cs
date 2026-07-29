namespace Microsoft.Mutate4CSharp.Exec;

using System.Diagnostics;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Faithful port of mutate4java's <c>ProcessCommandExecutor</c>: spawns an external command in a
/// working directory and captures its <see cref="CommandResult"/> (exit code, merged output,
/// duration, timeout flag). It implements the T9 <see cref="ICommandExecutor"/> seam and adds the
/// timeout-bearing overload mutate4java exposes. Per Anders' T9 pin the command is a token list
/// spawned via argv (<see cref="ProcessStartInfo.FileName"/> plus
/// <see cref="ProcessStartInfo.ArgumentList"/>, never a shell string), with standard output and error
/// redirected and drained asynchronously before the wait; standard error is merged into
/// <see cref="CommandResult.Output"/>, the analog of Java's <c>redirectErrorStream(true)</c>.
/// </summary>
public sealed class ProcessCommandExecutor : ICommandExecutor
{
    private readonly ProcessRunnerSupport _support = new();

    /// <summary>
    /// Runs the command with no timeout (the <see cref="ICommandExecutor"/> contract).
    /// </summary>
    /// <param name="command">The command and its arguments as an already-split token list.</param>
    /// <param name="workingDirectory">The directory the command runs in.</param>
    /// <returns>The command's exit code, merged output, duration, and timeout flag.</returns>
    public CommandResult Run(IReadOnlyList<string> command, string workingDirectory)
    {
        return Run(command, workingDirectory, 0);
    }

    /// <summary>
    /// Runs the command, terminating it (entire process tree) once <paramref name="timeoutMillis"/>
    /// elapses; a non-positive timeout runs unbounded. On timeout the result carries the sentinel exit
    /// code <c>124</c> and <see cref="CommandResult.TimedOut"/> set.
    /// </summary>
    /// <param name="command">The command and its arguments as an already-split token list.</param>
    /// <param name="workingDirectory">The directory the command runs in.</param>
    /// <param name="timeoutMillis">The wall-clock timeout in milliseconds; non-positive means unbounded.</param>
    /// <returns>The command's exit code, merged output, duration, and timeout flag.</returns>
    public CommandResult Run(IReadOnlyList<string> command, string workingDirectory, long timeoutMillis)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(workingDirectory);

        long start = Stopwatch.GetTimestamp();
        using Process process = StartProcess(command, workingDirectory);
        Task<string> outputTask = _support.BeginReadOutput(process);
        bool timedOut = !_support.WaitFor(process, timeoutMillis);
        int exitCode = _support.ExitCode(process, timedOut);
        string output = _support.ReadOutput(outputTask);
        long durationMillis = (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return new CommandResult(exitCode, output, durationMillis, timedOut);
    }

    private Process StartProcess(IReadOnlyList<string> command, string workingDirectory)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = command[0],
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        for (int index = 1; index < command.Count; index++)
        {
            startInfo.ArgumentList.Add(command[index]);
        }

        Process process = new() { StartInfo = startInfo };
        process.Start();
        return process;
    }
}
