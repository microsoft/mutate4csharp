namespace Microsoft.Mutate4CSharp.Project;

/// <summary>
/// Faithful port of mutate4java's <c>ProjectLayout</c>, the workspace-relative façade the CLI and
/// engine depend on. Two responsibilities are preserved: resolving the explicit target file
/// (<see cref="ExplicitFile(string)"/>) and locating the module for a target
/// (<see cref="ResolveModule(string)"/>). The Java class's third responsibility — the JaCoCo
/// package-path <c>sourceSuffix</c> coverage key produced via <c>SourcePathNormalizer</c> — is
/// dropped: per decisions.md A4 the coverage key is now the absolute path produced by
/// <c>CoberturaLineCoverageParser.NormalizeSourcePath</c>, so both <c>sourceSuffix</c> and
/// <c>SourcePathNormalizer</c> have no remaining use and are intentionally not ported. Module
/// resolution delegates to <see cref="ModuleResolver"/> (the DD2/DD3 replacement for the Java
/// <c>ModuleRootFinder</c>).
/// </summary>
public sealed class ProjectLayout
{
    private readonly string _workspaceRoot;
    private readonly ModuleResolver _moduleResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectLayout"/> class.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root all target arguments resolve against.</param>
    public ProjectLayout(string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(workspaceRoot);
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
        _moduleResolver = new ModuleResolver(_workspaceRoot);
    }

    /// <summary>
    /// Resolves an explicit target-file argument against the workspace root and rejects directories,
    /// the faithful analog of mutate4java's <c>explicitFile</c> (message adapted to the C# tool name
    /// and file extension).
    /// </summary>
    /// <param name="arg">The raw target-file argument.</param>
    /// <returns>The absolute path to the target file.</returns>
    /// <exception cref="ArgumentException">The argument resolves to a directory.</exception>
    public string ExplicitFile(string arg)
    {
        ArgumentNullException.ThrowIfNull(arg);
        string path = Path.GetFullPath(Path.Combine(_workspaceRoot, arg));
        if (Directory.Exists(path))
        {
            throw new ArgumentException("mutate4csharp target must be a .cs file");
        }

        return path;
    }

    /// <summary>
    /// Resolves the owning production project and its unit test project for the given target file
    /// under the DD2/DD3 convention.
    /// </summary>
    /// <param name="file">The target <c>.cs</c> file.</param>
    /// <returns>The module resolution, or a typed not-found signal.</returns>
    public ModuleResolution ResolveModule(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return _moduleResolver.Resolve(file);
    }
}
