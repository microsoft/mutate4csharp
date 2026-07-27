namespace Microsoft.Mutate4CSharp.Report;

using System.Globalization;
using System.Text;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Renders the final stdout report: the baseline line, any extra diagnostics block, the
/// <c>UNCOVERED</c> lines, the per-mutant <c>KILLED</c>/<c>SURVIVED</c> lines, and the coverage /
/// summary footer. Faithful port of mutate4java's <c>ReportFormatter</c> — the strings are reproduced
/// character-for-character (explicit <c>"\n"</c>, invariant-culture numbers, forward-slash relative
/// paths).
/// </summary>
public sealed class ReportFormatter
{
    /// <summary>
    /// Formats the complete mutation report.
    /// </summary>
    /// <param name="projectRoot">The root that site paths are rendered relative to.</param>
    /// <param name="baseline">The baseline test run.</param>
    /// <param name="extra">The extra diagnostics block, or <see langword="null"/>/blank for none.</param>
    /// <param name="uncovered">The uncovered mutation sites.</param>
    /// <param name="results">The per-mutant results.</param>
    /// <returns>The rendered report.</returns>
    public string Format(
        string projectRoot,
        TestRun baseline,
        string? extra,
        IReadOnlyList<MutationSite> uncovered,
        IReadOnlyList<MutationResult> results)
    {
        ArgumentNullException.ThrowIfNull(projectRoot);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(uncovered);
        ArgumentNullException.ThrowIfNull(results);
        StringBuilder output = new();
        output.Append("Baseline tests passed in ")
            .Append(baseline.DurationMillis.ToString(CultureInfo.InvariantCulture)).Append(" ms.\n");
        if (!string.IsNullOrWhiteSpace(extra))
        {
            output.Append(extra);
        }

        AppendUncovered(projectRoot, uncovered, output);
        AppendResults(projectRoot, results, output);
        AppendSummary(uncovered, results, output);
        return output.ToString();
    }

    private static string Relative(string projectRoot, string file)
    {
        return Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
    }

    private void AppendUncovered(string projectRoot, IReadOnlyList<MutationSite> uncovered, StringBuilder output)
    {
        foreach (MutationSite site in uncovered)
        {
            output.Append("UNCOVERED ");
            output.Append(Relative(projectRoot, site.File)).Append(':');
            output.Append(site.LineNumber.ToString(CultureInfo.InvariantCulture)).Append(' ');
            output.Append(site.Description).Append('\n');
        }
    }

    private void AppendResults(string projectRoot, IReadOnlyList<MutationResult> results, StringBuilder output)
    {
        foreach (MutationResult result in results)
        {
            output.Append(result.Killed ? "KILLED " : "SURVIVED ");
            output.Append(Relative(projectRoot, result.Site.File)).Append(':');
            output.Append(result.Site.LineNumber.ToString(CultureInfo.InvariantCulture)).Append(' ');
            output.Append(result.Site.Description).Append(" (");
            output.Append(result.DurationMillis.ToString(CultureInfo.InvariantCulture)).Append(" ms)\n");
            if (result.TimedOut)
            {
                output.Append("  timed out\n");
            }
        }
    }

    private void AppendSummary(
        IReadOnlyList<MutationSite> uncovered, IReadOnlyList<MutationResult> results, StringBuilder output)
    {
        int killed = results.Count(result => result.Killed);
        int survived = results.Count - killed;
        output.Append("Coverage: ").Append(uncovered.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" uncovered sites skipped.\n");
        output.Append("Summary: ").Append(killed.ToString(CultureInfo.InvariantCulture)).Append(" killed, ");
        output.Append(survived.ToString(CultureInfo.InvariantCulture)).Append(" survived, ");
        output.Append(results.Count.ToString(CultureInfo.InvariantCulture)).Append(" total.\n");
    }
}
