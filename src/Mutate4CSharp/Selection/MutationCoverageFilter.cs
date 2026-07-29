namespace Microsoft.Mutate4CSharp.Selection;

using Microsoft.Mutate4CSharp.Coverage;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Partitions mutation sites into covered and uncovered against a <see cref="CoverageReport"/> —
/// faithful port of mutate4java's <c>MutationCoverageFilter</c>, re-keyed to the A4 coverage contract.
/// </summary>
/// <remarks>
/// The Java filter keyed coverage through <c>ProjectLayout.sourceSuffix(moduleRoot, file)</c> (the
/// JaCoCo package-path suffix). Per docs/decisions.md A4 that keying is replaced: this filter resolves
/// each site's file through <see cref="CoberturaLineCoverageParser.NormalizeSourcePath(string)"/>
/// <em>exactly once</em> at the boundary before calling <see cref="CoverageReport.Covers(string, int)"/>,
/// so both the coverage producer (the parser) and this querier form the identical absolute-path key.
/// The <c>moduleRoot</c> argument and the <c>ProjectLayout</c> collaborator are therefore no longer
/// needed and are intentionally not carried over.
/// </remarks>
public sealed class MutationCoverageFilter
{
    /// <summary>
    /// Splits <paramref name="sites"/> into the covered sites (mutated and tested) and the uncovered
    /// sites (reported but skipped), querying <paramref name="coverage"/> through the A4 normalized
    /// key.
    /// </summary>
    /// <param name="sites">The mutation sites to partition.</param>
    /// <param name="coverage">The coverage report to query.</param>
    /// <returns>The covered/uncovered partition.</returns>
    public CoverageSelection Filter(IReadOnlyList<MutationSite> sites, CoverageReport coverage)
    {
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(coverage);
        List<MutationSite> covered = [];
        List<MutationSite> uncovered = [];
        foreach (MutationSite site in sites)
        {
            // A4 boundary: normalize the site's file to the same absolute-path key the Cobertura
            // parser produced, exactly once, before the covered lookup.
            string normalizedPath = CoberturaLineCoverageParser.NormalizeSourcePath(site.File);
            if (coverage.Covers(normalizedPath, site.LineNumber))
            {
                covered.Add(site);
            }
            else
            {
                uncovered.Add(site);
            }
        }

        return new CoverageSelection(covered, uncovered);
    }
}
