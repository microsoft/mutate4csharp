namespace Microsoft.Mutate4CSharp.Model;

/// <summary>
/// The result of a test run: exit code, captured output, wall-clock duration, and whether the
/// run was terminated for exceeding its timeout.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="Output">The captured test output.</param>
/// <param name="DurationMillis">The wall-clock duration in milliseconds.</param>
/// <param name="TimedOut">Whether the run was killed for exceeding its timeout.</param>
public sealed record TestRun(int ExitCode, string Output, long DurationMillis, bool TimedOut)
{
    /// <summary>
    /// Determines whether the run passed: it exited with code zero and did not time out.
    /// </summary>
    /// <returns><see langword="true"/> if the run passed; otherwise <see langword="false"/>.</returns>
    public bool Passed()
    {
        return ExitCode == 0 && !TimedOut;
    }
}
