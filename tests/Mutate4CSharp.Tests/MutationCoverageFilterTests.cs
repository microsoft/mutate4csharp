namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Coverage;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Selection;

/// <summary>
/// Exercises <see cref="MutationCoverageFilter"/> through the A4 coverage-key boundary: a site is
/// covered iff its file — resolved once through
/// <see cref="CoberturaLineCoverageParser.NormalizeSourcePath(string)"/> — reports positive hits in
/// the parsed report. Reuses the T8 temp-dir-as-source-base fixture so both the producer (parser) and
/// the querier (filter) form the identical absolute-path key.
/// </summary>
public sealed class MutationCoverageFilterTests : IDisposable
{
    private readonly MutationCoverageFilter _filter = new();
    private readonly string _tempDir;
    private readonly string _sourceFile;

    /// <summary>
    /// Initializes a new instance of the <see cref="MutationCoverageFilterTests"/> class, creating a
    /// per-test temporary directory that doubles as the report's <c>&lt;source&gt;</c> base.
    /// </summary>
    public MutationCoverageFilterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-covfilter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _sourceFile = Path.Combine(_tempDir, "Sample.cs");
    }

    /// <summary>
    /// Deletes the per-test temporary directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A covered line partitions to <c>covered</c> and a zero-hit line to <c>uncovered</c>, keyed by
    /// the site file resolved through <see cref="CoberturaLineCoverageParser.NormalizeSourcePath"/>.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void PartitionsCoveredAndUncoveredThroughNormalizedKey()
    {
        CoverageReport report = ParseReport(
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <coverage version="1.9">
               <sources>
                 <source>{_tempDir}</source>
               </sources>
               <packages>
                 <package name="Demo">
                   <classes>
                     <class name="Demo.Sample" filename="Sample.cs">
                       <lines>
                         <line number="5" hits="3" branch="False" />
                         <line number="9" hits="0" branch="False" />
                       </lines>
                     </class>
                   </classes>
                 </package>
               </packages>
             </coverage>
             """);
        MutationSite covered = Site(_sourceFile, 5);
        MutationSite uncovered = Site(_sourceFile, 9);

        CoverageSelection selection = _filter.Filter([covered, uncovered], report);

        selection.Covered.Should().Equal(covered);
        selection.Uncovered.Should().Equal(uncovered);
    }

    /// <summary>
    /// The A4 boundary is honored by the filter itself: a site whose file is spelled non-canonically
    /// (redundant <c>.</c>/<c>..</c> segments) still resolves as covered because the filter normalizes
    /// it onto the parser's key — string equality alone would miss.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void NormalizesDivergentSitePathSpellingOntoTheKey()
    {
        CoverageReport report = ParseReport(
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <coverage version="1.9">
               <sources>
                 <source>{_tempDir}</source>
               </sources>
               <packages>
                 <package name="Demo">
                   <classes>
                     <class name="Demo.Sample" filename="Sample.cs">
                       <lines>
                         <line number="5" hits="1" branch="False" />
                       </lines>
                     </class>
                   </classes>
                 </package>
               </packages>
             </coverage>
             """);
        string nonCanonical = Path.Combine(_tempDir, ".", "sub", "..", "Sample.cs");
        MutationSite site = Site(nonCanonical, 5);

        CoverageSelection selection = _filter.Filter([site], report);

        selection.Covered.Should().Equal(site);
        selection.Uncovered.Should().BeEmpty();
    }

    /// <summary>An all-covered report leaves no site uncovered.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void AllCoveredReportLeavesNothingUncovered()
    {
        MutationSite a = Site(_sourceFile, 1);
        MutationSite b = Site(_sourceFile, 2);

        CoverageSelection selection = _filter.Filter([a, b], CoverageReport.AllCovered());

        selection.Covered.Should().Equal(a, b);
        selection.Uncovered.Should().BeEmpty();
    }

    private static MutationSite Site(string file, int line)
    {
        return new MutationSite(file, line, 0, 1, "==", "!=", "replace == with !=");
    }

    private CoverageReport ParseReport(string xml)
    {
        string path = Path.Combine(_tempDir, "coverage.cobertura.xml");
        File.WriteAllText(path, xml);
        return CoberturaLineCoverageParser.Parse(path);
    }
}
