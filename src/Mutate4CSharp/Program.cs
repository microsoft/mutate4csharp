namespace Microsoft.Mutate4CSharp;

using Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// The .NET process entry point for the mutate4csharp CLI. It delegates to <c>Cli.Main.Run</c> for
/// the parse-and-execute flow and to <see cref="Main.ExitIfNeeded"/> for process termination, keeping
/// <c>Program</c> the sole entry point (mutate4java's <c>Main.main</c> role) while the reusable
/// orchestration lives in <see cref="Main"/>.
/// </summary>
public static class Program
{
    /// <summary>
    /// Parses and runs the CLI, terminating the process for a non-zero exit code.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        int exit = Cli.Main.Run(args, Directory.GetCurrentDirectory(), Console.Out, Console.Error);
        Cli.Main.ExitIfNeeded(exit, Environment.Exit);
        return exit;
    }
}
