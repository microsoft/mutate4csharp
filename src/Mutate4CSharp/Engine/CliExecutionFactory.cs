namespace Microsoft.Mutate4CSharp.Engine;

using Microsoft.Mutate4CSharp.Analysis;
using Microsoft.Mutate4CSharp.Coverage;
using Microsoft.Mutate4CSharp.Exec;
using Microsoft.Mutate4CSharp.Manifest;
using Microsoft.Mutate4CSharp.Project;
using Microsoft.Mutate4CSharp.Report;
using Microsoft.Mutate4CSharp.Selection;

/// <summary>
/// Assembles a <see cref="CliExecution"/> from the application-level seams (executor, coverage runner,
/// workspace manager, progress reporter) plus a shared <see cref="ManifestSupport"/>. Faithful port of
/// mutate4java's <c>CliExecutionFactory</c>, minus the <c>ProjectLayout</c> argument to the coverage
/// filter (dropped under A4 — the filter now keys by absolute path).
/// </summary>
public sealed class CliExecutionFactory
{
    /// <summary>
    /// Creates the fully wired <see cref="CliExecution"/>.
    /// </summary>
    /// <param name="workspaceRoot">The repo/workspace root.</param>
    /// <param name="output">The standard-output writer.</param>
    /// <param name="error">The standard-error writer.</param>
    /// <param name="executor">The base test executor.</param>
    /// <param name="coverageRunner">The coverage runner seam.</param>
    /// <param name="workspaceManager">The worker-workspace manager.</param>
    /// <param name="verboseProgressReporter">The reporter used when <c>--verbose</c> is set.</param>
    /// <param name="layout">The project layout.</param>
    /// <returns>The assembled execution.</returns>
    public CliExecution Create(
        string workspaceRoot,
        TextWriter output,
        TextWriter error,
        ITestCommandExecutor executor,
        ICoverageRunner coverageRunner,
        IWorkspaceManager workspaceManager,
        IProgressReporter verboseProgressReporter,
        ProjectLayout layout)
    {
        ArgumentNullException.ThrowIfNull(workspaceManager);
        ManifestSupport manifestSupport = new();
        return new CliExecution(
            workspaceRoot,
            output,
            error,
            executor,
            coverageRunner,
            verboseProgressReporter,
            new MutationCatalog(),
            new ReportFormatter(),
            manifestSupport,
            layout,
            new DifferentialSelector(manifestSupport),
            new MutationCoverageFilter(),
            new MutationExecution(workspaceManager),
            new ScanReportFormatter(workspaceRoot));
    }
}
