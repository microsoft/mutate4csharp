namespace Microsoft.Mutate4CSharp.Engine;

using Microsoft.Mutate4CSharp.Manifest;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Report;

/// <summary>
/// Writes the final outcome: the embedded manifest (only on a clean run), the formatted report, and
/// the process exit code. Faithful port of mutate4java's package-private
/// <c>ExecutionOutcomeWriter</c>. The manifest is written when there is nothing to run (a covered-set
/// of zero) or when every mutant was killed (exit <c>0</c>) — never when a mutant survived (exit
/// <c>3</c>), so a survivor never stamps a "clean" manifest.
/// </summary>
public sealed class ExecutionOutcomeWriter
{
    private readonly string _workspaceRoot;
    private readonly TextWriter _out;
    private readonly ReportFormatter _formatter;
    private readonly ManifestWriter _manifestWriter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutionOutcomeWriter"/> class.
    /// </summary>
    /// <param name="workspaceRoot">The root that site paths are rendered relative to.</param>
    /// <param name="output">The writer the report is printed to.</param>
    /// <param name="formatter">The report formatter.</param>
    /// <param name="manifestWriter">The manifest writer.</param>
    public ExecutionOutcomeWriter(
        string workspaceRoot, TextWriter output, ReportFormatter formatter, ManifestWriter manifestWriter)
    {
        ArgumentNullException.ThrowIfNull(workspaceRoot);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(formatter);
        ArgumentNullException.ThrowIfNull(manifestWriter);
        _workspaceRoot = workspaceRoot;
        _out = output;
        _formatter = formatter;
        _manifestWriter = manifestWriter;
    }

    /// <summary>
    /// Writes the manifest (when clean) and the report, and returns the exit code.
    /// </summary>
    /// <param name="summary">The mutation run summary.</param>
    /// <param name="analysis">The source analysis whose manifest is written on a clean run.</param>
    /// <returns>Exit code <c>0</c> when nothing survived (or nothing ran), or <c>3</c> when a mutant survived.</returns>
    public int Write(MutantResultSummary summary, SourceAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(analysis);
        if (summary.Results.Count == 0)
        {
            _manifestWriter.Write(summary.SourceFile, analysis);
            _out.Write(_formatter.Format(
                _workspaceRoot, summary.Baseline, summary.Extra, summary.Uncovered, []));
            return 0;
        }

        int exit = summary.Results.Any(result => !result.Killed) ? 3 : 0;
        if (exit == 0)
        {
            _manifestWriter.Write(summary.SourceFile, analysis);
        }

        _out.Write(_formatter.Format(
            _workspaceRoot, summary.Baseline, summary.Extra, summary.Uncovered, summary.Results));
        return exit;
    }
}
