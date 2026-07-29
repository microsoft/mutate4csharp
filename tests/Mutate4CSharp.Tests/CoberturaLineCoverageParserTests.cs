namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Coverage;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Faithful C# counterpart of mutate4java's <c>JacocoLineCoverageParserTest</c>, feeding coverlet
/// Cobertura fixtures instead of JaCoCo XML. Preserves the same test intents — covered versus
/// uncovered lines, the <c>hits &gt; 0</c> boundary, malformed counters, multiple files, and an empty
/// report — while asserting A4 keying: a line is covered iff its resolved absolute path (see
/// <see cref="CoberturaLineCoverageParser.NormalizeSourcePath(string)"/>) reports positive hits.
/// </summary>
public sealed class CoberturaLineCoverageParserTests : IDisposable
{
    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoberturaLineCoverageParserTests"/> class,
    /// creating a per-test temporary directory that doubles as the report's <c>&lt;source&gt;</c>
    /// base so resolved keys are real, reproducible absolute paths on any OS.
    /// </summary>
    public CoberturaLineCoverageParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-cobertura-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
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
    /// A <c>&lt;class filename&gt;</c> resolves against the report's <c>&lt;sources&gt;</c> base to an
    /// absolute key; a line with positive hits is covered and a zero-hit line is not.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ParsesCoveredLinesResolvedAgainstSourcesBase()
    {
        string body =
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
             """;
        string xml = WriteReport("coverage.cobertura.xml", body);

        CoverageReport report = CoberturaLineCoverageParser.Parse(xml);

        string sample = Key("Sample.cs");
        report.Covers(sample, 5).Should().BeTrue();
        report.Covers(sample, 9).Should().BeFalse();
    }

    /// <summary>
    /// The covered predicate is exactly <c>hits &gt; 0</c>: one hit is covered, zero hits is not, and
    /// a line absent from the report is not covered.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void TreatsPositiveHitsAsCoveredAndZeroOrAbsentAsUncovered()
    {
        string body =
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
                         <line number="1" hits="1" branch="False" />
                         <line number="2" hits="0" branch="False" />
                       </lines>
                     </class>
                   </classes>
                 </package>
               </packages>
             </coverage>
             """;
        string xml = WriteReport("boundary.cobertura.xml", body);

        CoverageReport report = CoberturaLineCoverageParser.Parse(xml);

        string sample = Key("Sample.cs");
        report.Covers(sample, 1).Should().BeTrue();
        report.Covers(sample, 2).Should().BeFalse();
        report.Covers(sample, 3).Should().BeFalse();
    }

    /// <summary>
    /// Lines whose counters cannot be parsed are ignored (they contribute no covered site), mirroring
    /// the Java parser's tolerant integer parsing.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void IgnoresLinesWithMalformedCoverageNumbers()
    {
        string body =
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
                         <line number="oops" hits="nan" branch="False" />
                       </lines>
                     </class>
                   </classes>
                 </package>
               </packages>
             </coverage>
             """;
        string xml = WriteReport("invalid.cobertura.xml", body);

        CoverageReport report = CoberturaLineCoverageParser.Parse(xml);

        report.Covers(Key("Sample.cs"), 0).Should().BeFalse();
    }

    /// <summary>
    /// Multiple <c>&lt;class&gt;</c> entries — including a nested-path filename — each resolve to their
    /// own absolute key under the shared source base.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ResolvesMultipleFilesUnderSharedSource()
    {
        string body =
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <coverage version="1.9">
               <sources>
                 <source>{_tempDir}</source>
               </sources>
               <packages>
                 <package name="Demo">
                   <classes>
                     <class name="Demo.Alpha" filename="Alpha.cs">
                       <lines>
                         <line number="7" hits="2" branch="False" />
                         <line number="8" hits="0" branch="False" />
                       </lines>
                     </class>
                     <class name="Demo.Beta" filename="nested/Beta.cs">
                       <lines>
                         <line number="4" hits="1" branch="False" />
                       </lines>
                     </class>
                   </classes>
                 </package>
               </packages>
             </coverage>
             """;
        string xml = WriteReport("multi.cobertura.xml", body);

        CoverageReport report = CoberturaLineCoverageParser.Parse(xml);

        report.Covers(Key("Alpha.cs"), 7).Should().BeTrue();
        report.Covers(Key("Alpha.cs"), 8).Should().BeFalse();
        report.Covers(Key("nested/Beta.cs"), 4).Should().BeTrue();
    }

    /// <summary>
    /// A valid report with no classes yields an empty report in which every site reads uncovered.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReturnsEmptyReportWhenNoClassesPresent()
    {
        string body =
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <coverage version="1.9">
               <sources>
                 <source>{_tempDir}</source>
               </sources>
               <packages />
             </coverage>
             """;
        string xml = WriteReport("empty.cobertura.xml", body);

        CoverageReport report = CoberturaLineCoverageParser.Parse(xml);

        report.Covers(Key("Sample.cs"), 1).Should().BeFalse();
    }

    /// <summary>
    /// A <see langword="null"/> or missing report path yields an empty report rather than throwing.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReturnsEmptyReportForMissingOrNullPath()
    {
        CoberturaLineCoverageParser.Parse(null)
            .Covers(Key("Sample.cs"), 1).Should().BeFalse();

        CoberturaLineCoverageParser.Parse(Path.Combine(_tempDir, "does-not-exist.xml"))
            .Covers(Key("Sample.cs"), 1).Should().BeFalse();
    }

    /// <summary>
    /// The A4 guardrail: <see cref="CoberturaLineCoverageParser.NormalizeSourcePath(string)"/>
    /// canonicalizes a divergently-spelled path to the SAME file onto the parser's key, so a mutation
    /// site whose file is written differently (non-canonical <c>.</c>/<c>..</c> segments, or — on
    /// Windows — different casing) still resolves as covered. The raw spellings are asserted to miss,
    /// proving the normalization — not incidental string equality — bridges them.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void NormalizeSourcePathBridgesDivergentSitePathSpellingsToTheKey()
    {
        string body =
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <coverage version="1.9">
               <sources>
                 <source>{_tempDir}</source>
               </sources>
               <packages>
                 <package name="Demo">
                   <classes>
                     <class name="Demo.Foo" filename="Foo.cs">
                       <lines>
                         <line number="12" hits="1" branch="False" />
                       </lines>
                     </class>
                   </classes>
                 </package>
               </packages>
             </coverage>
             """;
        string xml = WriteReport("bridge.cobertura.xml", body);

        CoverageReport report = CoberturaLineCoverageParser.Parse(xml);

        // A non-canonical spelling of the same file (./sub/../Foo.cs) does not match as a raw string,
        // but normalizing it through NormalizeSourcePath collapses it onto the parser's key.
        string nonCanonical = Path.Combine(_tempDir, ".", "sub", "..", "Foo.cs");
        report.Covers(nonCanonical, 12).Should().BeFalse();
        report.Covers(CoberturaLineCoverageParser.NormalizeSourcePath(nonCanonical), 12)
            .Should().BeTrue();

        // On Windows (case-insensitive filesystem) a differently-cased spelling likewise bridges once
        // normalized; skipped on case-sensitive platforms where the two paths are genuinely distinct.
        if (OperatingSystem.IsWindows())
        {
            string differentCase = Path.Combine(_tempDir, "FOO.CS");
            report.Covers(differentCase, 12).Should().BeFalse();
            report.Covers(CoberturaLineCoverageParser.NormalizeSourcePath(differentCase), 12)
                .Should().BeTrue();
        }
    }

    private string Key(string relativePath)
    {
        return CoberturaLineCoverageParser.NormalizeSourcePath(Path.Combine(_tempDir, relativePath));
    }

    private string WriteReport(string name, string xml)
    {
        string path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, xml);
        return path;
    }
}
