namespace Microsoft.Mutate4CSharp.Cli;

using Microsoft.Mutate4CSharp.Exec;

/// <summary>
/// The command-line exit plumbing ported from mutate4java's <c>Main</c>: wiring an argument vector to
/// a <see cref="CliApplication"/> run, exposing the usage text, and providing the conditional-exit
/// helper. The .NET process entry point stays <see cref="Program.Main(string[])"/>, which delegates
/// here; this type is not itself an entry point.
/// </summary>
public static class Main
{
    /// <summary>
    /// Runs the CLI for the given argument vector using the default process test executor.
    /// </summary>
    /// <param name="args">The argument vector.</param>
    /// <param name="projectRoot">The repo/workspace root.</param>
    /// <param name="output">The standard-output writer.</param>
    /// <param name="error">The standard-error writer.</param>
    /// <returns>The process exit code.</returns>
    public static int Run(string[] args, string projectRoot, TextWriter output, TextWriter error)
    {
        return Run(args, projectRoot, output, error, new ProcessTestCommandExecutor());
    }

    /// <summary>
    /// Runs the CLI for the given argument vector using the supplied test executor.
    /// </summary>
    /// <param name="args">The argument vector.</param>
    /// <param name="projectRoot">The repo/workspace root.</param>
    /// <param name="output">The standard-output writer.</param>
    /// <param name="error">The standard-error writer.</param>
    /// <param name="executor">The test executor to run with.</param>
    /// <returns>The process exit code.</returns>
    public static int Run(
        string[] args, string projectRoot, TextWriter output, TextWriter error, ITestCommandExecutor executor)
    {
        return new CliApplication(projectRoot, output, error, executor).Execute(args);
    }

    /// <summary>
    /// Returns the usage/help text.
    /// </summary>
    /// <returns>The usage text.</returns>
    public static string Usage()
    {
        return UsageText.Text();
    }

    /// <summary>
    /// Invokes <paramref name="exiter"/> with <paramref name="exit"/> only when it is non-zero, so a
    /// success code never forces process termination.
    /// </summary>
    /// <param name="exit">The exit code.</param>
    /// <param name="exiter">The action that terminates the process with a given code.</param>
    public static void ExitIfNeeded(int exit, Action<int> exiter)
    {
        ArgumentNullException.ThrowIfNull(exiter);
        if (exit != 0)
        {
            exiter(exit);
        }
    }
}
