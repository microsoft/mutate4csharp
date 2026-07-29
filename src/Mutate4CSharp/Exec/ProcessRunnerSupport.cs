namespace Microsoft.Mutate4CSharp.Exec;

using System.Diagnostics;

/// <summary>
/// Faithful port of mutate4java's package-private <c>ProcessRunnerSupport</c>: the wait /
/// exit-code / output-capture helpers that <see cref="ProcessCommandExecutor"/> composes. The method
/// decomposition mirrors the Java class (instance members, per the CA1822 mirror-Java policy); the
/// one ecosystem adaptation is the output drain. Java relies on <c>redirectErrorStream(true)</c> plus
/// a post-wait <c>readAllBytes()</c>; .NET cannot merge the two OS pipes, so the port redirects
/// standard output and error separately and drains both asynchronously — started via
/// <see cref="BeginReadOutput(Process)"/> before the wait — to avoid the pipe-buffer deadlock on large
/// <c>dotnet test</c> output, then concatenates them (stderr after stdout) as the merged output. The
/// private drain / clamp helpers are duplicated in <see cref="TimedProcessRun"/>, faithful to
/// mutate4java, which likewise duplicates its wait / exit-code / read helpers across the two classes.
/// </summary>
public sealed class ProcessRunnerSupport
{
    /// <summary>
    /// Waits for the process to exit. A non-positive timeout waits indefinitely; otherwise it waits at
    /// most <paramref name="timeoutMillis"/> milliseconds.
    /// </summary>
    /// <param name="process">The running process.</param>
    /// <param name="timeoutMillis">The wait timeout in milliseconds; non-positive means unbounded.</param>
    /// <returns><see langword="true"/> if the process exited in time; <see langword="false"/> on timeout.</returns>
    public bool WaitFor(Process process, long timeoutMillis)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (timeoutMillis <= 0)
        {
            process.WaitForExit();
            return true;
        }

        return process.WaitForExit(ClampTimeout(timeoutMillis));
    }

    /// <summary>
    /// Returns the process exit code. When <paramref name="timedOut"/> is set the process is killed —
    /// entire tree (the faithful <c>destroyForcibly</c> analog, extended to the child processes
    /// <c>dotnet</c> spawns) — and the sentinel exit code <c>124</c> is returned.
    /// </summary>
    /// <param name="process">The process.</param>
    /// <param name="timedOut">Whether the wait timed out.</param>
    /// <returns>The process exit code, or <c>124</c> on timeout.</returns>
    public int ExitCode(Process process, bool timedOut)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (!timedOut)
        {
            return process.ExitCode;
        }

        process.Kill(entireProcessTree: true);
        process.WaitForExit();
        return 124;
    }

    /// <summary>
    /// Begins draining the process's standard output and error concurrently. Call this immediately
    /// after the process starts, before <see cref="WaitFor(Process, long)"/>, so a full pipe cannot
    /// block the process (and thus the wait) on large output.
    /// </summary>
    /// <param name="process">The process whose streams to drain.</param>
    /// <returns>A task producing the merged output (standard error appended to standard output).</returns>
    public Task<string> BeginReadOutput(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        return DrainAsync(process);
    }

    /// <summary>
    /// Completes the drain started by <see cref="BeginReadOutput(Process)"/> and returns the merged
    /// output. Safe to block on because the process has already exited (or been killed) by the time it
    /// is called, so the streams are at end-of-file.
    /// </summary>
    /// <param name="outputTask">The task returned by <see cref="BeginReadOutput(Process)"/>.</param>
    /// <returns>The merged standard output and error.</returns>
    public string ReadOutput(Task<string> outputTask)
    {
        ArgumentNullException.ThrowIfNull(outputTask);
        return outputTask.GetAwaiter().GetResult();
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
