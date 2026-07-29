namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp;
using Microsoft.Mutate4CSharp.Cli;
using Microsoft.Mutate4CSharp.Manifest;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Selection;

/// <summary>
/// Exercises <see cref="DifferentialSelector"/> across the spec §8 selection states — no manifest,
/// an unchanged module, a changed module, <c>--since-last-run</c>, <c>--mutate-all</c>, and
/// <c>--lines</c> — plus the S3 <c>file:</c> fallback conservative-select adaptation. There is no
/// dedicated Java oracle (these strings surface through S5/S6), so the expectations are grounded in
/// mutate4java's <c>DifferentialSelector.select</c> control flow.
/// </summary>
public sealed class DifferentialSelectorTests : IDisposable
{
    private readonly ManifestSupport _manifestSupport = new();
    private readonly string _tempDir;
    private readonly string _sourceFile;
    private readonly DifferentialSelector _selector;

    /// <summary>
    /// Initializes a new instance of the <see cref="DifferentialSelectorTests"/> class, creating a
    /// per-test temporary directory and a selector over a real <see cref="ManifestSupport"/>.
    /// </summary>
    public DifferentialSelectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-diffsel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _sourceFile = Path.Combine(_tempDir, "Sample.cs");
        _selector = new DifferentialSelector(_manifestSupport);
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

    /// <summary>No embedded manifest selects every site and is reported non-differential.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void NoManifestSelectsAllSites()
    {
        WriteSourceWithoutManifest();
        SourceAnalysis analysis = Analysis(
            "MOD",
            [Scope("s1", "h1")],
            SiteIn(5, "s1"),
            SiteIn(9, "s1"));

        DifferentialSelection selection = _selector.Select(_sourceFile, Args(), analysis);

        selection.Selected.Should().Equal(analysis.Sites);
        selection.UnchangedModule.Should().BeFalse();
        selection.ManifestExists.Should().BeFalse();
        selection.ModuleHashChanged.Should().BeFalse();
        selection.TotalMutationSites.Should().Be(2);
        selection.ChangedMutationSites.Should().Be(0);
        selection.DifferentialSurfaceArea.Should().Be(0);
        selection.ManifestViolatingSurfaceArea.Should().Be(0);
    }

    /// <summary>A manifest with an unchanged module hash selects nothing.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ManifestUnchangedModuleSelectsNothing()
    {
        WriteManifest("MOD", Scope("s1", "h1"));
        SourceAnalysis analysis = Analysis(
            "MOD",
            [Scope("s1", "h1")],
            SiteIn(5, "s1"),
            SiteIn(9, "s1"));

        DifferentialSelection selection = _selector.Select(_sourceFile, Args(), analysis);

        selection.Selected.Should().BeEmpty();
        selection.UnchangedModule.Should().BeTrue();
        selection.ManifestExists.Should().BeTrue();
        selection.ModuleHashChanged.Should().BeFalse();
        selection.TotalMutationSites.Should().Be(2);
        selection.ChangedMutationSites.Should().Be(0);
    }

    /// <summary>
    /// A changed module hash selects only the sites in changed scopes — the union of unregistered
    /// (new) and manifest-violating (registered but re-hashed) scopes — and reports each surface area.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ManifestChangedHashSelectsOnlyChangedScopeSites()
    {
        WriteManifest("OLD", Scope("s1", "h1-old"), Scope("s2", "h2"));
        MutationSite inViolation = SiteIn(5, "s1");
        MutationSite inUnchanged = SiteIn(8, "s2");
        MutationSite inUnregistered = SiteIn(12, "s3");
        SourceAnalysis analysis = Analysis(
            "NEW",
            [Scope("s1", "h1-new"), Scope("s2", "h2"), Scope("s3", "h3")],
            inViolation,
            inUnchanged,
            inUnregistered);

        DifferentialSelection selection = _selector.Select(_sourceFile, Args(), analysis);

        selection.Selected.Should().Equal(inViolation, inUnregistered);
        selection.UnchangedModule.Should().BeFalse();
        selection.ManifestExists.Should().BeTrue();
        selection.ModuleHashChanged.Should().BeTrue();
        selection.TotalMutationSites.Should().Be(3);
        selection.ChangedMutationSites.Should().Be(2);
        selection.DifferentialSurfaceArea.Should().Be(1);
        selection.ManifestViolatingSurfaceArea.Should().Be(1);
    }

    /// <summary>
    /// <c>--lines</c> without <c>--since-last-run</c> short-circuits to non-differential selection
    /// (every site), deferring the line narrowing to <see cref="LineFilter"/>.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void LinesWithoutSinceLastRunSelectsAllSites()
    {
        WriteManifest("OLD", Scope("s1", "h1-old"));
        SourceAnalysis analysis = Analysis(
            "NEW",
            [Scope("s1", "h1-new")],
            SiteIn(5, "s1"),
            SiteIn(9, "s1"));

        DifferentialSelection selection =
            _selector.Select(_sourceFile, Args(lines: new HashSet<int> { 5 }), analysis);

        selection.Selected.Should().Equal(analysis.Sites);
        selection.UnchangedModule.Should().BeFalse();
        selection.ManifestExists.Should().BeFalse();
    }

    /// <summary>
    /// <c>--since-last-run</c> overrides the <c>--lines</c> short-circuit and applies differential
    /// selection, narrowing to the changed-scope sites.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void SinceLastRunWithLinesAppliesDifferentialSelection()
    {
        WriteManifest("OLD", Scope("s1", "h1-old"), Scope("s2", "h2"));
        MutationSite changed = SiteIn(5, "s1");
        MutationSite unchanged = SiteIn(8, "s2");
        SourceAnalysis analysis = Analysis(
            "NEW",
            [Scope("s1", "h1-new"), Scope("s2", "h2")],
            changed,
            unchanged);

        DifferentialSelection selection =
            _selector.Select(_sourceFile, Args(sinceLastRun: true, lines: new HashSet<int> { 5 }), analysis);

        selection.Selected.Should().Equal(changed);
        selection.ManifestExists.Should().BeTrue();
        selection.ModuleHashChanged.Should().BeTrue();
    }

    /// <summary>
    /// <c>--mutate-all</c> selects every site and reports the run as non-differential even when a
    /// manifest exists — mutate4java's <c>notDifferential</c> hardcodes the manifest flags to
    /// <see langword="false"/>.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void MutateAllSelectsAllSitesAndReportsManifestAbsent()
    {
        WriteManifest("OLD", Scope("s1", "h1-old"));
        SourceAnalysis analysis = Analysis(
            "NEW",
            [Scope("s1", "h1-new")],
            SiteIn(5, "s1"),
            SiteIn(9, "s1"));

        DifferentialSelection selection = _selector.Select(_sourceFile, Args(mutateAll: true), analysis);

        selection.Selected.Should().Equal(analysis.Sites);
        selection.UnchangedModule.Should().BeFalse();
        selection.ManifestExists.Should().BeFalse();
        selection.ModuleHashChanged.Should().BeFalse();
        selection.ChangedMutationSites.Should().Be(0);
    }

    /// <summary>
    /// The S3 adaptation: a synthetic <c>file:</c> fallback site carries no manifest scope, so it is
    /// conservative-selected (treated as changed) alongside the genuinely changed scope rather than
    /// silently dropped.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FileFallbackSiteIsConservativeSelectedAlongsideChangedScope()
    {
        WriteManifest("OLD", Scope("s1", "h1-old"));
        MutationSite inChangedScope = SiteIn(5, "s1");
        MutationSite topLevel = SiteIn(2, "file:Sample.cs");
        SourceAnalysis analysis = Analysis(
            "NEW",
            [Scope("s1", "h1-new")],
            inChangedScope,
            topLevel);

        DifferentialSelection selection = _selector.Select(_sourceFile, Args(), analysis);

        selection.Selected.Should().Contain(topLevel);
        selection.Selected.Should().Contain(inChangedScope);
        selection.UnchangedModule.Should().BeFalse();
    }

    /// <summary>
    /// The <c>file:</c> fallback site is conservative-selected even when the module hash is otherwise
    /// unchanged — the module hash excludes top-level statements, so the selector cannot prove they
    /// are unchanged and never reports the module unchanged while such a site is outstanding.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FileFallbackSiteIsConservativeSelectedEvenWhenModuleUnchanged()
    {
        WriteManifest("MOD", Scope("s1", "h1"));
        MutationSite topLevel = SiteIn(2, "file:Sample.cs");
        SourceAnalysis analysis = Analysis(
            "MOD",
            [Scope("s1", "h1")],
            topLevel);

        DifferentialSelection selection = _selector.Select(_sourceFile, Args(), analysis);

        selection.Selected.Should().Equal(topLevel);
        selection.UnchangedModule.Should().BeFalse();
        selection.ManifestExists.Should().BeTrue();
    }

    /// <summary>
    /// <see cref="DifferentialSelector.ChangedScopeIds"/> returns the union of the unregistered and
    /// manifest-violating scope ids — the scan mode's <c>*</c> markers.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ChangedScopeIdsReturnsUnionOfUnregisteredAndViolations()
    {
        WriteManifest("OLD", Scope("s1", "h1-old"), Scope("s2", "h2"));
        SourceAnalysis analysis = Analysis(
            "NEW",
            [Scope("s1", "h1-new"), Scope("s2", "h2"), Scope("s3", "h3")],
            SiteIn(5, "s1"));

        IReadOnlySet<string> changed = _selector.ChangedScopeIds(_sourceFile, analysis);

        changed.Should().BeEquivalentTo(["s1", "s3"]);
    }

    private static MutationScope Scope(string id, string semanticHash)
    {
        return new MutationScope(id, "method", 1, 10, semanticHash);
    }

    private MutationSite SiteIn(int line, string scopeId)
    {
        return new MutationSite(_sourceFile, line, 0, 1, "==", "!=", "replace == with !=", scopeId, "method", 1, 10);
    }

    private static SourceAnalysis Analysis(
        string moduleHash, IReadOnlyList<MutationScope> scopes, params MutationSite[] sites)
    {
        return new SourceAnalysis("// generated test source\n", sites, scopes, moduleHash);
    }

    private static CliArguments Args(bool mutateAll = false, bool sinceLastRun = false, IReadOnlySet<int>? lines = null)
    {
        return CliArgumentsParser.Parse(["Sample.cs"]) with
        {
            MutateAll = mutateAll,
            SinceLastRun = sinceLastRun,
            Lines = lines ?? new HashSet<int>(),
        };
    }

    private void WriteSourceWithoutManifest()
    {
        File.WriteAllText(_sourceFile, "// generated test source\n");
    }

    private void WriteManifest(string moduleHash, params MutationScope[] scopes)
    {
        DifferentialManifest manifest = new(1, moduleHash, scopes);
        _manifestSupport.Write(_sourceFile, "// generated test source\n", manifest);
    }
}
