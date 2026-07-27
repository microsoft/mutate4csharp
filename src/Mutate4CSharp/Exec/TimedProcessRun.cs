namespace Microsoft.Mutate4CSharp.Exec;

using System.Diagnostics;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Faithful port of mutate4java's package-private static <c>TimedProcessRun</c>: waits for an
/// already-started test process, applies the timeout, and produces a <see cref="TestRun"/>. The wait
/// / exit-code / drain helpers are intentionally duplicated from <see cref="ProcessRunnerSupport"/> —
/// faithful to mutate4java, whose <c>TimedProcessRun</c> likewise carries its own private copies
/// rather than sharing <c>ProcessRunnerSupport</c>. As in <see cref="ProcessRunnerSupport"/>, standard
/// output and error are drained asynchronously (started before the wait, avoiding the pipe-buffer
/// deadlock) and merged; a timeout kills the entire process tree and yields the sentinel exit
/// code <c>124</c>.
/// </summary>
public static class TimedProcessRun
{
    /// <summary>
    /// Finishes a running test process: drains its output, waits with the given timeout, and returns
    /// the resulting <see cref="TestRun"/> measured from <paramref name="startTimestamp"/>.
    /// </summary>
    /// <param name="process">The already-started test process.</param>
    /// <param name="timeoutMillis">The wall-clock timeout in milliseconds; non-positive means unbounded.</param>
    /// <param name="startTimestamp">The <see cref="Stopwatch.GetTimestamp()"/> taken when the process started.</param>
    /// <returns>The test run's exit code, merged output, duration, and timeout flag.</returns>
    public static TestRun Finish(Process process, long timeoutMillis, long startTimestamp)
    {
        ArgumentNullException.ThrowIfNull(process);
        Task<string> outputTask = DrainAsync(process);
        bool timedOut = !WaitFor(process, timeoutMillis);
        int exitCode = ExitCode(process, timedOut);
        long durationMillis = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        return new TestRun(exitCode, outputTask.GetAwaiter().GetResult(), durationMillis, timedOut);
    }

    private static bool WaitFor(Process process, long timeoutMillis)
    {
        if (timeoutMillis <= 0)
        {
            process.WaitForExit();
            return true;
        }

        return process.WaitForExit(ClampTimeout(timeoutMillis));
    }

    private static int ExitCode(Process process, bool timedOut)
    {
        if (!timedOut)
        {
            return process.ExitCode;
        }

        process.Kill(entireProcessTree: true);
        process.WaitForExit();
        return 124;
    }

    private static int ClampTimeout(long timeoutMillis)
    {
        return timeoutMillis > int.MaxValue ? int.MaxValue : (int)timeoutMillis;
    }

    private static async Task<string> DrainAsync(Process process)
    {
        try
        {
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            string[] streams = await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
            return streams[0] + streams[1];
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }
}
