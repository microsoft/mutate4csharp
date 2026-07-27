namespace Microsoft.Mutate4CSharp.Engine;

using System.Globalization;
using System.Text;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Selection;

/// <summary>
/// Builds the pre-worker diagnostics block that precedes the mutation report: the differential
/// counts, whether the manifest exists / changed, and the optional "no mutations" and split-module
/// warnings. Faithful port of mutate4java's <c>ExecutionMessages</c>; booleans render lowercase and
/// numbers use the invariant culture to match the Java strings character-for-character.
/// </summary>
public sealed class ExecutionMessages
{
    /// <summary>
    /// Renders the extra diagnostics block for the given selection.
    /// </summary>
    /// <param name="parsed">The parsed CLI arguments (supplying the mutation-count warning threshold).</param>
    /// <param name="differentialSelection">The differential selection result.</param>
    /// <param name="coverageSelection">The coverage split of covered vs uncovered sites.</param>
    /// <returns>The rendered diagnostics block.</returns>
    public string ExtraText(
        CliArguments parsed,
        DifferentialSelection differentialSelection,
        CoverageSelection coverageSelection)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(differentialSelection);
        ArgumentNullException.ThrowIfNull(coverageSelection);
        StringBuilder extra = new();
        AppendDifferentialDiagnostics(differentialSelection, coverageSelection, extra);
        if (differentialSelection.UnchangedModule)
        {
            extra.Append("No mutations need testing.\n");
        }

        if (coverageSelection.Covered.Count > parsed.MutationWarning)
        {
            extra.Append("WARNING: Found ")
                .Append(coverageSelection.Covered.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" mutations. Consider splitting this module.\n");
        }

        return extra.ToString();
    }

    private void AppendDifferentialDiagnostics(
        DifferentialSelection differentialSelection, CoverageSelection coverageSelection, StringBuilder extra)
    {
        extra.Append("Total mutation sites: ")
            .Append(differentialSelection.TotalMutationSites.ToString(CultureInfo.InvariantCulture)).Append('\n');
        extra.Append("Covered mutation sites: ")
            .Append(coverageSelection.Covered.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        extra.Append("Uncovered mutation sites: ")
            .Append(coverageSelection.Uncovered.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        extra.Append("Changed mutation sites: ")
            .Append(differentialSelection.ChangedMutationSites.ToString(CultureInfo.InvariantCulture)).Append('\n');
        extra.Append("Manifest exists: ")
            .Append(differentialSelection.ManifestExists ? "true" : "false").Append('\n');
        extra.Append("Module hash changed: ")
            .Append(differentialSelection.ModuleHashChanged ? "true" : "false").Append('\n');
        extra.Append("Differential surface area: ")
            .Append(differentialSelection.DifferentialSurfaceArea.ToString(CultureInfo.InvariantCulture)).Append('\n');
        extra.Append("Manifest-violating surface area: ")
            .Append(differentialSelection.ManifestViolatingSurfaceArea.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
    }
}
