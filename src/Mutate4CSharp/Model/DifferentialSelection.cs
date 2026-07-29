namespace Microsoft.Mutate4CSharp.Model;

/// <summary>
/// The result of differential selection: the selected mutation sites together with the
/// diagnostic counts describing why they were chosen.
/// </summary>
/// <param name="Selected">The selected mutation sites.</param>
/// <param name="UnchangedModule">Whether the module is unchanged since the manifest.</param>
/// <param name="ManifestExists">Whether a manifest already existed.</param>
/// <param name="ModuleHashChanged">Whether the module hash changed.</param>
/// <param name="TotalMutationSites">The total number of mutation sites.</param>
/// <param name="ChangedMutationSites">The number of changed mutation sites.</param>
/// <param name="DifferentialSurfaceArea">The differential surface area.</param>
/// <param name="ManifestViolatingSurfaceArea">The manifest-violating surface area.</param>
public sealed record DifferentialSelection(
    IReadOnlyList<MutationSite> Selected,
    bool UnchangedModule,
    bool ManifestExists,
    bool ModuleHashChanged,
    int TotalMutationSites,
    int ChangedMutationSites,
    int DifferentialSurfaceArea,
    int ManifestViolatingSurfaceArea);
