namespace Microsoft.Mutate4CSharp.Selection;

using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Restricts mutation sites to an explicit set of source lines (the <c>--lines</c> selection) —
/// faithful port of mutate4java's <c>LineFilter</c>. An empty line set is a pass-through.
/// </summary>
public sealed class LineFilter
{
    /// <summary>
    /// Filters <paramref name="sites"/> to those whose line number is in <paramref name="lines"/>, or
    /// returns them unchanged when the line set is empty.
    /// </summary>
    /// <param name="sites">The mutation sites to filter.</param>
    /// <param name="lines">The selected source lines; empty means all lines.</param>
    /// <returns>The filtered mutation sites.</returns>
    public IReadOnlyList<MutationSite> Filter(IReadOnlyList<MutationSite> sites, IReadOnlySet<int> lines)
    {
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            return sites;
        }

        return sites.Where(site => lines.Contains(site.LineNumber)).ToList();
    }
}
