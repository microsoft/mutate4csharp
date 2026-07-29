namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Selection;

/// <summary>
/// Golden-string tests for <see cref="ScanReportFormatter"/> (spec §13.2), grounded in mutate4java's
/// <c>Scan: N mutation sites in &lt;file&gt;</c> header. Sites render in source order, each prefixed
/// <c>* </c> when its scope differs from the embedded manifest, with a trailing legend line whenever
/// any scope is marked. Paths are workspace-relative with forward slashes.
/// </summary>
public sealed class ScanReportFormatterTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "m4cs-scan-root");
    private static readonly string SourceFile = Path.Combine(Root, "src", "Demo", "Sample.cs");

    private readonly ScanReportFormatter _formatter = new(Root);

    /// <summary>
    /// Sites render in order; the changed scope is prefixed <c>* </c>, the unchanged scope is padded,
    /// and the legend line follows because a scope is marked.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void MarksChangedScopesAndAppendsLegend()
    {
        MutationSite changed = Site(5, "s1", "replace + with -");
        MutationSite unchanged = Site(9, "s2", "replace == with !=");

        string report = _formatter.Format(SourceFile, [changed, unchanged], Set("s1"));

        report.Should().Be(
            "Scan: 2 mutation sites in src/Demo/Sample.cs\n"
            + "* src/Demo/Sample.cs:5 replace + with -\n"
            + "  src/Demo/Sample.cs:9 replace == with !=\n"
            + "* indicates a scope that differs from the embedded manifest.\n");
    }

    /// <summary>
    /// With no changed scopes every site is padded and the legend line is omitted.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void OmitsLegendWhenNoScopeChanged()
    {
        MutationSite site = Site(5, "s1", "replace + with -");

        string report = _formatter.Format(SourceFile, [site], Set());

        report.Should().Be(
            "Scan: 1 mutation sites in src/Demo/Sample.cs\n"
            + "  src/Demo/Sample.cs:5 replace + with -\n");
    }

    private static MutationSite Site(int line, string scopeId, string description)
    {
        return new MutationSite(SourceFile, line, 0, 1, "a", "b", description, scopeId, "method", 1, 10);
    }

    private static HashSet<string> Set(params string[] ids)
    {
        return new HashSet<string>(ids, StringComparer.Ordinal);
    }
}
