namespace Microsoft.Mutate4CSharp.Selection;

using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// The partition of mutation sites produced by <see cref="MutationCoverageFilter"/>: the covered
/// sites (which are mutated and tested) and the uncovered sites (which are reported but skipped).
/// Faithful port of mutate4java's <c>CoverageSelection</c> record.
/// </summary>
/// <param name="Covered">The covered mutation sites.</param>
/// <param name="Uncovered">The uncovered mutation sites.</param>
public sealed record CoverageSelection(
    IReadOnlyList<MutationSite> Covered,
    IReadOnlyList<MutationSite> Uncovered);
