namespace Microsoft.Mutate4CSharp.Model;

/// <summary>
/// A summary of a mutation run: the target source file, the baseline test run, extra report
/// text, the uncovered mutation sites, and the per-mutant results.
/// </summary>
/// <param name="SourceFile">The target source file path.</param>
/// <param name="Baseline">The baseline test run.</param>
/// <param name="Extra">Extra diagnostic text appended to the report.</param>
/// <param name="Uncovered">The uncovered mutation sites.</param>
/// <param name="Results">The per-mutant results.</param>
public sealed record MutantResultSummary(
    string SourceFile,
    TestRun Baseline,
    string Extra,
    IReadOnlyList<MutationSite> Uncovered,
    IReadOnlyList<MutationResult> Results);
