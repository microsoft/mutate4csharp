namespace Microsoft.Mutate4CSharp.Engine;

using Microsoft.Mutate4CSharp.Analysis;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Project;
using Microsoft.Mutate4CSharp.Report;

/// <summary>
/// The resolved per-invocation context shared across the engine steps: the target source file, its
/// resolved module, the selected test executor, the progress reporter, and the source analysis.
/// Faithful port of mutate4java's <c>ExecutionContext</c> record, with one adaptation — mutate4java
/// carried a Maven <c>moduleRoot</c> <see cref="System.IO.Path"/>; the C# port carries the richer
/// <see cref="ModuleResolution"/> (DD2/DD3) so the baseline, coverage, and mutation steps can reach
/// the resolved test project. The repo/workspace root the mutation run copies is held by
/// <see cref="CliExecution"/>, not here.
/// </summary>
/// <param name="SourceFile">The absolute path to the target source file.</param>
/// <param name="Module">The resolved owning production project and its unit test project.</param>
/// <param name="Executor">The test executor, already bound to any <c>--test-command</c> override.</param>
/// <param name="ProgressReporter">The progress reporter (verbose or no-op per <c>--verbose</c>).</param>
/// <param name="Analysis">The completed source analysis of the target file.</param>
public sealed record ExecutionContext(
    string SourceFile,
    ModuleResolution Module,
    ITestCommandExecutor Executor,
    IProgressReporter ProgressReporter,
    SourceAnalysis Analysis)
{
    /// <summary>
    /// Builds the execution context for a parsed request: resolves the explicit target file and its
    /// module, selects the test executor (honoring a <c>--test-command</c> override) and the progress
    /// reporter (honoring <c>--verbose</c>), and analyzes the target.
    /// </summary>
    /// <param name="parsed">The parsed CLI arguments.</param>
    /// <param name="executor">The base test executor.</param>
    /// <param name="verboseProgressReporter">The reporter used when <c>--verbose</c> is set.</param>
    /// <param name="layout">The project layout used to resolve the target file and module.</param>
    /// <param name="catalog">The mutation catalog used to analyze the target.</param>
    /// <returns>The resolved execution context.</returns>
    public static ExecutionContext Create(
        CliArguments parsed,
        ITestCommandExecutor executor,
        IProgressReporter verboseProgressReporter,
        ProjectLayout layout,
        MutationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(verboseProgressReporter);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(catalog);
        ITestCommandExecutor selectedExecutor =
            parsed.TestCommand is null ? executor : executor.WithCommand(parsed.TestCommand);
        string sourceFile = layout.ExplicitFile(parsed.FileArgs[0]);
        ModuleResolution module = layout.ResolveModule(sourceFile);
        IProgressReporter progressReporter =
            parsed.Verbose ? verboseProgressReporter : new NoOpProgressReporter();
        return new ExecutionContext(
            sourceFile, module, selectedExecutor, progressReporter, catalog.Analyze(sourceFile));
    }
}
