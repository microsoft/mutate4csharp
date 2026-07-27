namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp;
using Microsoft.Mutate4CSharp.Cli;
using Microsoft.Mutate4CSharp.Manifest;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Selection;

/// <summary>
/// Integration test for <see cref="ScanMode"/>, which composes the line filter, differential
/// selector, and scan formatter. Confirms the adapted signature (source file + analysis instead of
/// the engine's execution context) wires the pieces so line filtering narrows the sites and changed
/// scopes flow through as <c>* </c> markers.
/// </summary>
public sealed class ScanModeTests : IDisposable
{
    private readonly ManifestSupport _manifestSupport = new();
    private readonly string _tempDir;
    private readonly string _sourceFile;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScanModeTests"/> class, creating a per-test
    /// temporary directory used as both the workspace root and the target file's location.
    /// </summary>
    public ScanModeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-scanmode-" + Guid.NewGuid().ToString("N"));
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
    /// With <c>--lines 5</c> the scan renders only the line-5 site, marked <c>* </c> because its scope
    /// differs from the embedded manifest.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RendersLineFilteredScanWithChangedScopeMarker()
    {
        DifferentialManifest manifest = new(1, "OLD", [new MutationScope("s1", "method", 1, 10, "h1-old")]);
        _manifestSupport.Write(_sourceFile, "// generated test source\n", manifest);
        SourceAnalysis analysis = new(
            "// generated test source\n",
            [Site(5, "replace + with -"), Site(9, "replace == with !=")],
            [new MutationScope("s1", "method", 1, 10, "h1-new")],
            "NEW");
        ScanMode scanMode = new(
            new DifferentialSelector(_manifestSupport), new ScanReportFormatter(_tempDir), new LineFilter());
        CliArguments parsed = CliArgumentsParser.Parse(["Sample.cs"]) with { Lines = new HashSet<int> { 5 } };

        string report = scanMode.Render(parsed, _sourceFile, analysis);

        report.Should().Be(
            "Scan: 1 mutation sites in Sample.cs\n"
            + "* Sample.cs:5 replace + with -\n"
            + "* indicates a scope that differs from the embedded manifest.\n");
    }

    private MutationSite Site(int line, string description)
    {
        return new MutationSite(_sourceFile, line, 0, 1, "a", "b", description, "s1", "method", 1, 10);
    }
}
