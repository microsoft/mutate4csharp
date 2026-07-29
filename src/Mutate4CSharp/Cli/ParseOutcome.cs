namespace Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// The outcome of a top-level parse attempt: either parsed <see cref="CliArguments"/> to run, or an
/// exit code to return directly (help printed, or a usage error reported).
/// </summary>
/// <param name="Arguments">The parsed arguments, or <see langword="null"/> when exiting.</param>
/// <param name="ExitCode">The exit code to return, or <c>-1</c> when arguments are present.</param>
public sealed record ParseOutcome(CliArguments? Arguments, int ExitCode)
{
    /// <summary>
    /// Creates an outcome carrying parsed arguments to run.
    /// </summary>
    /// <param name="arguments">The parsed arguments.</param>
    /// <returns>An outcome with the given arguments and an exit code of <c>-1</c>.</returns>
    public static ParseOutcome Ok(CliArguments arguments)
    {
        return new ParseOutcome(arguments, -1);
    }

    /// <summary>
    /// Creates an outcome carrying an exit code to return directly.
    /// </summary>
    /// <param name="code">The exit code.</param>
    /// <returns>An outcome with no arguments and the given exit code.</returns>
    public static ParseOutcome Exit(int code)
    {
        return new ParseOutcome(null, code);
    }
}
