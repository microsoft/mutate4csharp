namespace Microsoft.Mutate4CSharp.Model;

/// <summary>
/// The outcome of running an external command: exit code, captured output, wall-clock
/// duration, and whether the command was terminated for exceeding its timeout.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="Output">The captured command output.</param>
/// <param name="DurationMillis">The wall-clock duration in milliseconds.</param>
/// <param name="TimedOut">Whether the command was killed for exceeding its timeout.</param>
public sealed record CommandResult(int ExitCode, string Output, long DurationMillis, bool TimedOut);
