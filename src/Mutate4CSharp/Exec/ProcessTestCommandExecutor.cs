namespace Microsoft.Mutate4CSharp.Exec;

using System.Diagnostics;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Faithful port of mutate4java's <c>ProcessTestCommandExecutor</c>: the concrete
/// <see cref="ITestCommandExecutor"/> that spawns a test process (via
/// <see cref="ProcessTestCommandFactory"/>) and turns it into a <see cref="TestRun"/> through
/// <see cref="TimedProcessRun"/>. The default command is the DD3 unit-only filter — the faithful port
/// of Java's <c>mvn test -DexcludeTags=no-mutate</c> — carrying no <c>--collect</c> coverage wrapping;
/// coverage composition (and the resolved <c>&lt;Project&gt;.Tests.csproj</c>) is the coverage
/// runner's concern (T12), injected through the argv constructor. A <c>--test-command</c> override is
/// run through a shell via <see cref="WithCommand(string)"/> (A9).
/// </summary>
/// <remarks>
/// mutate4java stores the override <c>commandText</c> in a private field that is written but never
/// read; the port omits that dead field (it would trip <c>CS0414</c> under warnings-as-errors) while
/// preserving identical behavior — the override is still applied by building a shell-launching
/// executor.
/// </remarks>
public sealed class ProcessTestCommandExecutor : ITestCommandExecutor
{
    private static readonly IReadOnlyList<string> DefaultCommand =
        ["dotnet", "test", "--filter", "type!=IntegrationTests&Category!=no-mutate"];

    private readonly ProcessLauncher _launcher;

    /// <summary>
    /// Initializes an executor that runs the DD3 default test command.
    /// </summary>
    public ProcessTestCommandExecutor()
        : this(DefaultCommand)
    {
    }

    /// <summary>
    /// Initializes an executor that runs the given argv token list as the test command.
    /// </summary>
    /// <param name="command">The command and its arguments as an already-split token list.</param>
    public ProcessTestCommandExecutor(IReadOnlyList<string> command)
        : this(projectRoot => ProcessTestCommandFactory.StartProcess(projectRoot, command))
    {
    }

    /// <summary>
    /// Initializes an executor that runs the given verbatim command string through a shell (A9).
    /// </summary>
    /// <param name="commandText">The verbatim shell command to run.</param>
    public ProcessTestCommandExecutor(string commandText)
        : this(projectRoot => ProcessTestCommandFactory.StartShellProcess(projectRoot, commandText))
    {
    }

    /// <summary>
    /// Initializes an executor from a launcher — the seam that keeps the run/timeout logic testable
    /// without a real test process (the port of mutate4java's <c>ProcessLauncher</c> constructor).
    /// </summary>
    /// <param name="launcher">The launcher that starts the test process for a project root.</param>
    public ProcessTestCommandExecutor(ProcessLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        _launcher = launcher;
    }

    /// <summary>
    /// Starts a test process for a project root; the port of mutate4java's nested
    /// <c>ProcessLauncher</c> functional interface, expressed as a delegate so constructor lambdas
    /// bind to it directly.
    /// </summary>
    /// <param name="projectRoot">The directory the test process runs in.</param>
    /// <returns>The started, output-redirected process.</returns>
    public delegate Process ProcessLauncher(string projectRoot);

    /// <inheritdoc/>
    public TestRun RunTests(string projectRoot, long timeoutMillis)
    {
        ArgumentNullException.ThrowIfNull(projectRoot);
        long start = Stopwatch.GetTimestamp();
        using Process process = _launcher(projectRoot);
        return TimedProcessRun.Finish(process, timeoutMillis, start);
    }

    /// <inheritdoc/>
    public ITestCommandExecutor WithCommand(string command)
    {
        return new ProcessTestCommandExecutor(command);
    }
}
