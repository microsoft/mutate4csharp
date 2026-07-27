namespace Microsoft.Mutate4CSharp.Selection;

using System.Globalization;
using System.Text;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Renders the <c>--scan</c> report (spec §13.2): a header line plus one workspace-relative line per
/// mutation site in source order, each prefixed <c>*</c> when its scope differs from the embedded
/// manifest. Faithful port of mutate4java's <c>ScanReportFormatter</c>, including the explicit
/// backslash-to-forward-slash normalization so the report reads identically on any OS.
/// </summary>
public sealed class ScanReportFormatter
{
    private readonly string _workspaceRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScanReportFormatter"/> class.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root that site paths are rendered relative to.</param>
    public ScanReportFormatter(string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(workspaceRoot);
        _workspaceRoot = workspaceRoot;
    }

    /// <summary>
    /// Formats the scan report for the given target file and its discovered sites.
    /// </summary>
    /// <param name="sourceFile">The target source file.</param>
    /// <param name="sites">The mutation sites, in source order.</param>
    /// <param name="changedScopes">The scope ids that differ from the embedded manifest.</param>
    /// <returns>The rendered scan report.</returns>
    public string Format(string sourceFile, IReadOnlyList<MutationSite> sites, IReadOnlySet<string> changedScopes)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(changedScopes);
        StringBuilder report = new();
        report.Append("Scan: ").Append(sites.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" mutation sites in ").Append(Relative(sourceFile)).Append('\n');
        foreach (MutationSite site in sites)
        {
            report.Append(changedScopes.Contains(site.ScopeId) ? "* " : "  ");
            report.Append(Relative(site.File)).Append(':')
                .Append(site.LineNumber.ToString(CultureInfo.InvariantCulture))
                .Append(' ').Append(site.Description).Append('\n');
        }

        if (changedScopes.Count > 0)
        {
            report.Append("* indicates a scope that differs from the embedded manifest.\n");
        }

        return report.ToString();
    }

    private string Relative(string file)
    {
        return Path.GetRelativePath(_workspaceRoot, file).Replace('\\', '/');
    }
}
