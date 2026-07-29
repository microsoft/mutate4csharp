namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Selection;

/// <summary>
/// Tests <see cref="LineFilter"/>: an empty line set is a pass-through, otherwise only sites on the
/// selected lines survive.
/// </summary>
public sealed class LineFilterTests
{
    private readonly LineFilter _filter = new();

    /// <summary>An empty line set returns the sites unchanged.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void EmptyLineSetPassesThrough()
    {
        IReadOnlyList<MutationSite> sites = [Site(5), Site(9)];

        _filter.Filter(sites, new HashSet<int>()).Should().BeSameAs(sites);
    }

    /// <summary>A non-empty line set keeps only the sites on the selected lines.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void NonEmptyLineSetKeepsSelectedLines()
    {
        MutationSite five = Site(5);
        MutationSite nine = Site(9);

        _filter.Filter([five, nine], new HashSet<int> { 5 }).Should().Equal(five);
    }

    private static MutationSite Site(int line)
    {
        return new MutationSite("Sample.cs", line, 0, 1, "a", "b", "desc");
    }
}
