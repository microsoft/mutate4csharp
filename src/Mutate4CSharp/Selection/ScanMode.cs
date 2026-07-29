namespace Microsoft.Mutate4CSharp.Selection;

using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Drives <c>--scan</c> mode: line-filters the discovered sites and renders them through
/// <see cref="ScanReportFormatter"/>, marking scopes that differ from the embedded manifest. Faithful
/// port of mutate4java's <c>ScanMode</c>.
/// </summary>
/// <remarks>
/// mutate4java's <c>render</c> took the engine's <c>ExecutionContext</c> and read its
/// <c>sourceFile</c> and <c>analysis</c>. That type lands with the engine wiring (T15); to keep this
/// pure-selection class free of the forward dependency, <see cref="Render"/> takes those two values
/// directly. The engine will call it as <c>Render(parsed, context.SourceFile, context.Analysis)</c>.
/// </remarks>
public sealed class ScanMode
{
    private readonly DifferentialSelector _selector;
    private readonly ScanReportFormatter _formatter;
    private readonly LineFilter _lineFilter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScanMode"/> class.
    /// </summary>
    /// <param name="selector">The differential selector supplying changed-scope markers.</param>
    /// <param name="formatter">The scan report formatter.</param>
    /// <param name="lineFilter">The line filter applied before rendering.</param>
    public ScanMode(DifferentialSelector selector, ScanReportFormatter formatter, LineFilter lineFilter)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(formatter);
        ArgumentNullException.ThrowIfNull(lineFilter);
        _selector = selector;
        _formatter = formatter;
        _lineFilter = lineFilter;
    }

    /// <summary>
    /// Renders the scan report for the given target file and analysis.
    /// </summary>
    /// <param name="parsed">The parsed CLI arguments (supplying the <c>--lines</c> selection).</param>
    /// <param name="sourceFile">The target source file.</param>
    /// <param name="analysis">The current source analysis.</param>
    /// <returns>The rendered scan report.</returns>
    public string Render(CliArguments parsed, string sourceFile, SourceAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(analysis);
        return _formatter.Format(
            sourceFile,
            _lineFilter.Filter(analysis.Sites, parsed.Lines),
            _selector.ChangedScopeIds(sourceFile, analysis));
    }
}
