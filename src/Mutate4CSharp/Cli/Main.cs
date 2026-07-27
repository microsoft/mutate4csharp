namespace Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// The command-line exit plumbing ported from mutate4java's <c>Main</c>: exposing the usage text and
/// the conditional-exit helper. The mutation orchestration that mutate4java's <c>Main.run</c> wires
/// up arrives with the engine in a later task.
/// </summary>
public static class Main
{
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
