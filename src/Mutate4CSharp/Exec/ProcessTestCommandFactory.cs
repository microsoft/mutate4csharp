namespace Microsoft.Mutate4CSharp.Exec;

using System.Diagnostics;

/// <summary>
/// Faithful port of mutate4java's package-private <c>ProcessTestCommandFactory</c>: spawns the test
/// process for <see cref="ProcessTestCommandExecutor"/>, either from an explicit argv token list or
/// from a verbatim command string run through a shell. Both variants redirect standard output and
/// error (drained by <see cref="TimedProcessRun"/>) with <c>UseShellExecute = false</c>.
/// </summary>
/// <remarks>
/// <see cref="StartShellProcess(string, string)"/> is the single sanctioned non-1:1 shell path (A9):
/// mutate4java uses <c>/bin/sh -lc &lt;command&gt;</c> unconditionally, whereas the port OS-detects and
/// uses <c>cmd.exe /c &lt;command&gt;</c> on Windows so a user's <c>--test-command</c> override runs in
/// the platform shell. The user command is passed as a single argument so the shell — not the port —
/// parses it; on Windows, <c>cmd.exe</c>'s own quoting rules apply to that argument. Every other spawn
/// in the port uses argv, never a shell string.
/// </remarks>
public static class ProcessTestCommandFactory
{
    /// <summary>
    /// Starts the test process from an explicit argv token list (the default command path).
    /// </summary>
    /// <param name="projectRoot">The directory the command runs in.</param>
    /// <param name="command">The command and its arguments as an already-split token list.</param>
    /// <returns>The started, output-redirected process.</returns>
    public static Process StartProcess(string projectRoot, IReadOnlyList<string> command)
    {
        ArgumentNullException.ThrowIfNull(projectRoot);
        ArgumentNullException.ThrowIfNull(command);

        ProcessStartInfo startInfo = CreateStartInfo(projectRoot);
        startInfo.FileName = command[0];
        for (int index = 1; index < command.Count; index++)
        {
            startInfo.ArgumentList.Add(command[index]);
        }

        return Start(startInfo);
    }

    /// <summary>
    /// Starts the test process by running a verbatim command string through the platform shell
    /// (<c>cmd.exe /c</c> on Windows, <c>/bin/sh -lc</c> otherwise) — the A9 <c>--test-command</c>
    /// override path.
    /// </summary>
    /// <param name="projectRoot">The directory the command runs in.</param>
    /// <param name="commandText">The verbatim command string handed to the shell.</param>
    /// <returns>The started, output-redirected process.</returns>
    public static Process StartShellProcess(string projectRoot, string commandText)
    {
        ArgumentNullException.ThrowIfNull(projectRoot);
        ArgumentNullException.ThrowIfNull(commandText);

        return StartProcess(projectRoot, ShellCommand(commandText));
    }

    /// <summary>
    /// Builds the platform-shell argv that runs <paramref name="commandText"/> as a single argument
    /// (<c>cmd.exe /c &lt;command&gt;</c> on Windows, <c>/bin/sh -lc &lt;command&gt;</c> otherwise).
    /// The single source of truth for the A9 shell override, shared by
    /// <see cref="StartShellProcess(string, string)"/> and the command
    /// <see cref="ProcessTestCommandExecutor"/> records for a <c>--test-command</c> override.
    /// </summary>
    /// <param name="commandText">The verbatim command string handed to the shell.</param>
    /// <returns>The shell argv token list, shell launcher first and the user command last.</returns>
    public static IReadOnlyList<string> ShellCommand(string commandText)
    {
        ArgumentNullException.ThrowIfNull(commandText);

        return OperatingSystem.IsWindows()
            ? ["cmd.exe", "/c", commandText]
            : ["/bin/sh", "-lc", commandText];
    }

    private static ProcessStartInfo CreateStartInfo(string projectRoot)
    {
        return new ProcessStartInfo
        {
            WorkingDirectory = projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
    }

    private static Process Start(ProcessStartInfo startInfo)
    {
        Process process = new() { StartInfo = startInfo };
        process.Start();
        return process;
    }
}
