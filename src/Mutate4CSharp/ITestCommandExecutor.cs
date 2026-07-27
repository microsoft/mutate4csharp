namespace Microsoft.Mutate4CSharp;

using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Faithful port of mutate4java's top-level <c>TestCommandExecutor</c> interface: the seam the engine
/// uses to run a project's tests and capture the resulting <see cref="TestRun"/>. It lives in the
/// root <c>Microsoft.Mutate4CSharp</c> namespace because the Java type lives in the top-level
/// <c>mutate4java</c> package (alongside <c>CliArguments</c> / <c>CliMode</c>), and its concrete
/// implementation is <see cref="Exec.ProcessTestCommandExecutor"/>.
/// </summary>
public interface ITestCommandExecutor
{
    /// <summary>
    /// Runs the project's tests from the given project root, terminating the run once
    /// <paramref name="timeoutMillis"/> elapses (a non-positive value means no timeout).
    /// </summary>
    /// <param name="projectRoot">The directory the test command runs in.</param>
    /// <param name="timeoutMillis">The wall-clock timeout in milliseconds; non-positive means unbounded.</param>
    /// <returns>The test run's exit code, merged output, duration, and timeout flag.</returns>
    TestRun RunTests(string projectRoot, long timeoutMillis);

    /// <summary>
    /// Returns an executor that runs the given verbatim command string through a shell instead of the
    /// default test command. The default implementation ignores the override and returns this
    /// executor unchanged, faithful to mutate4java's default method.
    /// </summary>
    /// <param name="command">The verbatim shell command to run in place of the default.</param>
    /// <returns>An executor bound to <paramref name="command"/>, or this executor if unsupported.</returns>
    ITestCommandExecutor WithCommand(string command)
    {
        return this;
    }
}
