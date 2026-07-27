namespace Microsoft.Mutate4CSharp.Engine;

using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Selection;

/// <summary>
/// Plans and runs the mutation phase: differential + line + coverage selection, the pre-worker
/// diagnostics block, and the isolated mutant run over the covered sites. Faithful port of
/// mutate4java's package-private <c>MutationRunPlanner</c>, with two C# adaptations — the coverage
/// filter is keyed by absolute path (no <c>moduleRoot</c> argument, per A4), and the mutation-run
/// executor is scoped to the resolved test project by a <em>repo-root-relative</em> path so each
/// worker tests its mutated copy (contract keystone; a <c>--test-command</c> override is left as-is).
/// </summary>
public sealed class MutationRunPlanner
{
    private readonly DifferentialSelector _selector;
    private readonly MutationCoverageFilter _coverageFilter;
    private readonly MutationExecution _mutationExecution;
    private readonly ExecutionMessages _messages;
    private readonly LineFilter _lineFilter;

    /// <summary>
    /// Initializes a new instance of the <see cref="MutationRunPlanner"/> class.
    /// </summary>
    /// <param name="selector">The differential selector.</param>
    /// <param name="coverageFilter">The coverage filter partitioning covered vs uncovered sites.</param>
    /// <param name="mutationExecution">The isolated mutation runner.</param>
    /// <param name="messages">The pre-worker diagnostics builder.</param>
    /// <param name="lineFilter">The <c>--lines</c> filter.</param>
    public MutationRunPlanner(
        DifferentialSelector selector,
        MutationCoverageFilter coverageFilter,
        MutationExecution mutationExecution,
        ExecutionMessages messages,
        LineFilter lineFilter)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(coverageFilter);
        ArgumentNullException.ThrowIfNull(mutationExecution);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(lineFilter);
        _selector = selector;
        _coverageFilter = coverageFilter;
        _mutationExecution = mutationExecution;
        _messages = messages;
        _lineFilter = lineFilter;
    }

    /// <summary>
    /// Selects, filters, and runs the mutants for the target, returning the run summary. When no
    /// covered site survives the selection the summary carries an empty result set (a clean run).
    /// </summary>
    /// <param name="parsed">The parsed CLI arguments.</param>
    /// <param name="context">The resolved execution context.</param>
    /// <param name="workspaceRoot">The repo/workspace root the worker copies are made from.</param>
    /// <param name="baseline">The passing baseline run.</param>
    /// <param name="coverage">The coverage report driving covered/uncovered classification.</param>
    /// <returns>The mutation run summary.</returns>
    public MutantResultSummary Run(
        CliArguments parsed,
        ExecutionContext context,
        string workspaceRoot,
        TestRun baseline,
        CoverageReport coverage)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(workspaceRoot);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(coverage);
        DifferentialSelection differentialSelection = _selector.Select(context.SourceFile, parsed, context.Analysis);
        IReadOnlyList<MutationSite> discovered = _lineFilter.Filter(differentialSelection.Selected, parsed.Lines);
        CoverageSelection coverageSelection = _coverageFilter.Filter(discovered, coverage);
        string extra = _messages.ExtraText(parsed, differentialSelection, coverageSelection);
        if (coverageSelection.Covered.Count == 0)
        {
            return new MutantResultSummary(
                context.SourceFile, baseline, extra, coverageSelection.Uncovered, []);
        }

        long timeoutMillis = _mutationExecution.TimeoutMillis(baseline.DurationMillis, parsed.TimeoutFactor);
        IReadOnlyList<MutationResult> results = _mutationExecution.Run(
            workspaceRoot,
            coverageSelection.Covered,
            timeoutMillis,
            parsed.MaxWorkers,
            context.ProgressReporter,
            MutationExecutor(parsed, context, workspaceRoot));
        return new MutantResultSummary(
            context.SourceFile, baseline, extra, coverageSelection.Uncovered, results);
    }

    private static ITestCommandExecutor MutationExecutor(
        CliArguments parsed, ExecutionContext context, string workspaceRoot)
    {
        if (parsed.TestCommand is not null)
        {
            return context.Executor;
        }

        // Contract keystone: scope the mutation run to the resolved test project by a repo-root
        // relative path. Combined with each worker's working directory being its repo-root copy, the
        // run targets workerRoot/<relativePath> — the mutated copy — instead of the original.
        string testProjectRelativePath = Path.GetRelativePath(workspaceRoot, context.Module.TestProjectFile!);
        return context.Executor.WithTestProject(testProjectRelativePath);
    }
}
