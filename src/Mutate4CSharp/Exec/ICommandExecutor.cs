namespace Microsoft.Mutate4CSharp.Exec;

using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Minimal seam for running an external command and capturing its <see cref="CommandResult"/>.
/// mutate4java's <c>ChangedFileDetector</c> spawns <c>git</c> inline via <c>ProcessBuilder</c>; the C#
/// port hoists that spawn behind this abstraction so the detector is unit-testable with a stub in
/// place of real <c>git</c>. It lives in the <c>Exec</c> namespace — alongside where the concrete
/// <c>ProcessCommandExecutor</c> (T11) will implement it — because that is the faithful analog of
/// mutate4java's <c>mutate4java.exec.ProcessCommandExecutor</c>, which returns the same
/// <see cref="CommandResult"/> shape and is the class mutate4java's <c>project</c> package already
/// imports from <c>mutate4java.exec</c>.
/// </summary>
public interface ICommandExecutor
{
    /// <summary>
    /// Runs the given command in the given working directory and returns its outcome. Standard error
    /// is expected to be merged into <see cref="CommandResult.Output"/> (faithful to mutate4java's
    /// <c>redirectErrorStream(true)</c>).
    /// </summary>
    /// <param name="command">The command and its arguments, as an already-split token list.</param>
    /// <param name="workingDirectory">The directory the command runs in.</param>
    /// <returns>The command's exit code, merged output, duration, and timeout flag.</returns>
    CommandResult Run(IReadOnlyList<string> command, string workingDirectory);
}
