namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Unit tests for the small behaviors carried by the model types beyond their auto-generated
/// value semantics: <see cref="TestRun.Passed"/>, <see cref="CoverageReport"/> lookups,
/// <see cref="ScopeRef.From"/>, and the <see cref="MutationSite"/> convenience constructor.
/// </summary>
public class ModelTests
{
    /// <summary>
    /// A run passes only when it exits with code zero and did not time out.
    /// </summary>
    /// <param name="exitCode">The exit code.</param>
    /// <param name="timedOut">Whether the run timed out.</param>
    /// <param name="expected">The expected <see cref="TestRun.Passed"/> result.</param>
    [Theory]
    [Trait("type", "UnitTests")]
    [InlineData(0, false, true)]
    [InlineData(0, true, false)]
    [InlineData(1, false, false)]
    [InlineData(1, true, false)]
    public void PassedReflectsExitCodeAndTimeout(int exitCode, bool timedOut, bool expected)
    {
        new TestRun(exitCode, string.Empty, 0, timedOut).Passed().Should().Be(expected);
    }

    /// <summary>
    /// A report covers exactly the sites in its covered set, matched by value.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void CoversMatchesCoveredSitesByValue()
    {
        var report = new CoverageReport(new HashSet<CoverageSite> { new CoverageSite("A.cs", 5) });

        report.Covers("A.cs", 5).Should().BeTrue();
        report.Covers("A.cs", 6).Should().BeFalse();
        report.Covers("B.cs", 5).Should().BeFalse();
    }

    /// <summary>
    /// An all-covered report treats every site as covered.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void AllCoveredTreatsEverySiteAsCovered()
    {
        CoverageReport.AllCovered().Covers("anything.cs", 999).Should().BeTrue();
    }

    /// <summary>
    /// <see cref="ScopeRef.From"/> copies the scope's identity and line range.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ScopeRefFromCopiesScopeIdentity()
    {
        var scope = new MutationScope("the-id", "method", 3, 9, "hash");

        ScopeRef.From(scope).Should().Be(new ScopeRef("the-id", "method", 3, 9));
    }

    /// <summary>
    /// The convenience constructor defaults the scope metadata to the site's own line.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void MutationSiteConvenienceConstructorDefaultsScopeToLine()
    {
        var site = new MutationSite("F.cs", 16, 10, 12, "a", "b", "desc");

        site.ScopeId.Should().Be("scope:16");
        site.ScopeKind.Should().Be("unknown");
        site.ScopeStartLine.Should().Be(16);
        site.ScopeEndLine.Should().Be(16);
    }
}
