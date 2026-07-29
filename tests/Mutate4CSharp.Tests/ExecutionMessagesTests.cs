namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp;
using Microsoft.Mutate4CSharp.Cli;
using Microsoft.Mutate4CSharp.Engine;
using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Selection;

/// <summary>
/// Golden-string tests for <see cref="ExecutionMessages"/>, grounded in mutate4java's
/// <c>CliApplicationTest</c> diagnostics assertions (e.g. <c>Total mutation sites: N</c>,
/// <c>Manifest exists: true</c>, <c>Module hash changed: false</c>). Booleans render lowercase and the
/// optional <c>No mutations need testing.</c> and split-module warning lines append per their guards.
/// </summary>
public sealed class ExecutionMessagesTests
{
    private readonly ExecutionMessages _messages = new();

    /// <summary>
    /// An unchanged module renders the full diagnostics block (with lowercase booleans) followed by
    /// <c>No mutations need testing.</c> and no split-module warning.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RendersUnchangedModuleDiagnosticsWithNoMutationsLine()
    {
        DifferentialSelection differential = new(
            Selected: [],
            UnchangedModule: true,
            ManifestExists: true,
            ModuleHashChanged: false,
            TotalMutationSites: 2,
            ChangedMutationSites: 0,
            DifferentialSurfaceArea: 0,
            ManifestViolatingSurfaceArea: 0);
        CoverageSelection coverage = new([], []);

        string extra = _messages.ExtraText(Args(mutationWarning: 50), differential, coverage);

        extra.Should().Be(
            "Total mutation sites: 2\n"
            + "Covered mutation sites: 0\n"
            + "Uncovered mutation sites: 0\n"
            + "Changed mutation sites: 0\n"
            + "Manifest exists: true\n"
            + "Module hash changed: false\n"
            + "Differential surface area: 0\n"
            + "Manifest-violating surface area: 0\n"
            + "No mutations need testing.\n");
    }

    /// <summary>
    /// A changed module renders <c>Module hash changed: true</c> with the surface-area counts, no
    /// <c>No mutations need testing.</c> line, and the split-module warning once the covered count
    /// exceeds the threshold.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RendersChangedModuleDiagnosticsWithSplitWarning()
    {
        DifferentialSelection differential = new(
            Selected: Sites(2),
            UnchangedModule: false,
            ManifestExists: true,
            ModuleHashChanged: true,
            TotalMutationSites: 3,
            ChangedMutationSites: 2,
            DifferentialSurfaceArea: 1,
            ManifestViolatingSurfaceArea: 1);
        CoverageSelection coverage = new(Sites(2), []);

        string extra = _messages.ExtraText(Args(mutationWarning: 1), differential, coverage);

        extra.Should().Be(
            "Total mutation sites: 3\n"
            + "Covered mutation sites: 2\n"
            + "Uncovered mutation sites: 0\n"
            + "Changed mutation sites: 2\n"
            + "Manifest exists: true\n"
            + "Module hash changed: true\n"
            + "Differential surface area: 1\n"
            + "Manifest-violating surface area: 1\n"
            + "WARNING: Found 2 mutations. Consider splitting this module.\n");
    }

    private static List<MutationSite> Sites(int count)
    {
        return Enumerable.Range(1, count)
            .Select(line => new MutationSite("Sample.cs", line, 0, 1, "a", "b", "desc"))
            .ToList();
    }

    private static CliArguments Args(int mutationWarning)
    {
        return CliArgumentsParser.Parse(["Sample.cs"]) with { MutationWarning = mutationWarning };
    }
}
