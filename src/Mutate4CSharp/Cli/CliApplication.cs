namespace Microsoft.Mutate4CSharp.Cli;

using Microsoft.Mutate4CSharp.Coverage;
using Microsoft.Mutate4CSharp.Engine;
using Microsoft.Mutate4CSharp.Exec;
using Microsoft.Mutate4CSharp.Project;
using Microsoft.Mutate4CSharp.Report;

/// <summary>
/// The CLI application root: parses a request and, when it is runnable, hands it to the wired
/// <see cref="CliExecution"/>. Faithful port of mutate4java's <c>CliApplication</c>, preserving its
/// four constructor overloads so tests can inject stub seams (a stub executor, a stub coverage runner,
/// a real or fake workspace manager, and a no-op progress reporter) and run fast with no real
/// <c>dotnet test</c>. The Java <c>sourceSuffix</c>/<c>moduleRootFor</c> pass-throughs are dropped:
/// their <c>ProjectLayout</c> responsibilities do not exist in the C# layout (A4/DD3).
/// </summary>
public sealed class CliApplication
{
    private readonly CliExecution _execution;
    private readonly CliRequestParser _requestParser;

    /// <summary>
    /// Initializes an application with the production coverage runner, workspace manager, and progress
    /// reporter, injecting only the test executor.
    /// </summary>
    /// <param name="workspaceRoot">The repo/workspace root.</param>
    /// <param name="output">The standard-output writer.</param>
    /// <param name="error">The standard-error writer.</param>
    /// <param name="executor">The test executor.</param>
    public CliApplication(string workspaceRoot, TextWriter output, TextWriter error, ITestCommandExecutor executor)
        : this(
            workspaceRoot,
            output,
            error,
            executor,
            new CoverageRunner(new ProcessCommandExecutor()),
            new CopiedWorkspaceManager(),
            new PrintStreamProgressReporter(output))
    {
    }

    /// <summary>
    /// Initializes an application with an injected coverage runner, and the production workspace
    /// manager and progress reporter.
    /// </summary>
    /// <param name="workspaceRoot">The repo/workspace root.</param>
    /// <param name="output">The standard-output writer.</param>
    /// <param name="error">The standard-error writer.</param>
    /// <param name="executor">The test executor.</param>
    /// <param name="coverageRunner">The coverage runner seam.</param>
    public CliApplication(
        string workspaceRoot,
        TextWriter output,
        TextWriter error,
        ITestCommandExecutor executor,
        ICoverageRunner coverageRunner)
        : this(
            workspaceRoot,
            output,
            error,
            executor,
            coverageRunner,
            new CopiedWorkspaceManager(),
            new PrintStreamProgressReporter(output))
    {
    }

    /// <summary>
    /// Initializes an application with an injected coverage runner and workspace manager, and the
    /// production progress reporter.
    /// </summary>
    /// <param name="workspaceRoot">The repo/workspace root.</param>
    /// <param name="output">The standard-output writer.</param>
    /// <param name="error">The standard-error writer.</param>
    /// <param name="executor">The test executor.</param>
    /// <param name="coverageRunner">The coverage runner seam.</param>
    /// <param name="workspaceManager">The worker-workspace manager.</param>
    public CliApplication(
        string workspaceRoot,
        TextWriter output,
        TextWriter error,
        ITestCommandExecutor executor,
        ICoverageRunner coverageRunner,
        IWorkspaceManager workspaceManager)
        : this(
            workspaceRoot,
            output,
            error,
            executor,
            coverageRunner,
            workspaceManager,
            new PrintStreamProgressReporter(output))
    {
    }

    /// <summary>
    /// Initializes an application from every injectable seam — the constructor the tests use to run
    /// fast with stubs.
    /// </summary>
    /// <param name="workspaceRoot">The repo/workspace root.</param>
    /// <param name="output">The standard-output writer.</param>
    /// <param name="error">The standard-error writer.</param>
    /// <param name="executor">The test executor.</param>
    /// <param name="coverageRunner">The coverage runner seam.</param>
    /// <param name="workspaceManager">The worker-workspace manager.</param>
    /// <param name="verboseProgressReporter">The reporter used when <c>--verbose</c> is set.</param>
    public CliApplication(
        string workspaceRoot,
        TextWriter output,
        TextWriter error,
        ITestCommandExecutor executor,
        ICoverageRunner coverageRunner,
        IWorkspaceManager workspaceManager,
        IProgressReporter verboseProgressReporter)
    {
        ArgumentNullException.ThrowIfNull(workspaceRoot);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ProjectLayout layout = new(workspaceRoot);
        _requestParser = new CliRequestParser(output, error);
        _execution = new CliExecutionFactory().Create(
            workspaceRoot, output, error, executor, coverageRunner, workspaceManager, verboseProgressReporter, layout);
    }

    /// <summary>
    /// Parses <paramref name="args"/> and runs the request, returning the process exit code. A parse
    /// that resolves directly (help printed, or a usage error reported) returns its own exit code;
    /// otherwise the parsed arguments are executed.
    /// </summary>
    /// <param name="args">The argument vector.</param>
    /// <returns>The process exit code.</returns>
    public int Execute(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        ParseOutcome parse = _requestParser.Parse(args);
        if (parse.ExitCode >= 0)
        {
            return parse.ExitCode;
        }

        return _execution.Execute(parse.Arguments!);
    }
}
