namespace Microsoft.Mutate4CSharp.Project;

/// <summary>
/// The outcome kind of resolving a target <c>.cs</c> file to its owning production project and unit
/// test project under the DD2/DD3 convention. Both not-found kinds map to CLI exit <c>2</c> in T15,
/// but are kept distinct so the CLI layer can emit a precise stderr message (risk R-A).
/// </summary>
public enum ModuleResolutionStatus
{
    /// <summary>Both the owning <c>&lt;Project&gt;.csproj</c> and a validated test project were found.</summary>
    Resolved,

    /// <summary>No owning <c>.csproj</c> exists above the target file up to the workspace root.</summary>
    NoOwningProject,

    /// <summary>
    /// The owning project was found, but no <c>&lt;Project&gt;.Tests</c>/<c>.UnitTests</c> project whose
    /// project-reference closure includes it could be discovered.
    /// </summary>
    NoTestProject,
}

/// <summary>
/// The result of DD2/DD3 module resolution: the derived production project and the unit test project
/// that validates it, or a typed not-found signal. This is the faithful C# replacement for the
/// module root that mutate4java's <c>ModuleRootFinder</c> produced from the nearest <c>pom.xml</c>.
/// The resolver never terminates the process; the CLI layer (T15) decides what a not-found status
/// means (exit <c>2</c>).
/// </summary>
public sealed record ModuleResolution
{
    private ModuleResolution(
        ModuleResolutionStatus status, string? projectName, string? projectFile, string? testProjectFile)
    {
        Status = status;
        ProjectName = projectName;
        ProjectFile = projectFile;
        TestProjectFile = testProjectFile;
    }

    /// <summary>Gets the resolution outcome kind.</summary>
    public ModuleResolutionStatus Status { get; }

    /// <summary>
    /// Gets the derived production project name (the owning <c>.csproj</c> file name without its
    /// extension, i.e. <c>MSBuildProjectName</c>), or <see langword="null"/> when no owning project
    /// was found.
    /// </summary>
    public string? ProjectName { get; }

    /// <summary>
    /// Gets the absolute path to the owning <c>&lt;Project&gt;.csproj</c>, or <see langword="null"/>
    /// when no owning project was found.
    /// </summary>
    public string? ProjectFile { get; }

    /// <summary>
    /// Gets the absolute path to the resolved <c>&lt;Project&gt;.Tests.csproj</c> or
    /// <c>&lt;Project&gt;.UnitTests.csproj</c>, or <see langword="null"/> unless the status is
    /// <see cref="ModuleResolutionStatus.Resolved"/>.
    /// </summary>
    public string? TestProjectFile { get; }

    /// <summary>Creates a resolved outcome.</summary>
    /// <param name="projectName">The derived production project name.</param>
    /// <param name="projectFile">The absolute path to the owning <c>.csproj</c>.</param>
    /// <param name="testProjectFile">The absolute path to the validated test project.</param>
    /// <returns>A <see cref="ModuleResolutionStatus.Resolved"/> result.</returns>
    public static ModuleResolution Resolved(string projectName, string projectFile, string testProjectFile) =>
        new(ModuleResolutionStatus.Resolved, projectName, projectFile, testProjectFile);

    /// <summary>Creates a not-found outcome for a missing owning project.</summary>
    /// <returns>A <see cref="ModuleResolutionStatus.NoOwningProject"/> result.</returns>
    public static ModuleResolution NoOwningProject() =>
        new(ModuleResolutionStatus.NoOwningProject, null, null, null);

    /// <summary>Creates a not-found outcome for a missing test project.</summary>
    /// <param name="projectName">The derived production project name.</param>
    /// <param name="projectFile">The absolute path to the owning <c>.csproj</c>.</param>
    /// <returns>A <see cref="ModuleResolutionStatus.NoTestProject"/> result.</returns>
    public static ModuleResolution NoTestProject(string projectName, string projectFile) =>
        new(ModuleResolutionStatus.NoTestProject, projectName, projectFile, null);
}
