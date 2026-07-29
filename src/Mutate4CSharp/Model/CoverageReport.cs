namespace Microsoft.Mutate4CSharp.Model;

/// <summary>
/// A line-coverage lookup over a set of covered <see cref="CoverageSite"/> entries, or a report
/// that treats every source line as covered.
/// </summary>
public sealed class CoverageReport
{
    private readonly IReadOnlySet<CoverageSite> _coveredLines;
    private readonly bool _treatAllAsCovered;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageReport"/> class over the given
    /// covered lines.
    /// </summary>
    /// <param name="coveredLines">The set of covered source sites.</param>
    public CoverageReport(IReadOnlySet<CoverageSite> coveredLines)
        : this(coveredLines, false)
    {
    }

    private CoverageReport(IReadOnlySet<CoverageSite> coveredLines, bool treatAllAsCovered)
    {
        _coveredLines = coveredLines;
        _treatAllAsCovered = treatAllAsCovered;
    }

    /// <summary>
    /// Creates a report that treats every source line as covered.
    /// </summary>
    /// <returns>A report whose <see cref="Covers"/> always returns <see langword="true"/>.</returns>
    public static CoverageReport AllCovered()
    {
        return new CoverageReport(new HashSet<CoverageSite>(), true);
    }

    /// <summary>
    /// Determines whether the given source line is covered.
    /// </summary>
    /// <param name="sourcePath">The source file path.</param>
    /// <param name="lineNumber">The 1-based line number.</param>
    /// <returns><see langword="true"/> if the line is covered; otherwise <see langword="false"/>.</returns>
    public bool Covers(string sourcePath, int lineNumber)
    {
        return _treatAllAsCovered || _coveredLines.Contains(new CoverageSite(sourcePath, lineNumber));
    }
}
