namespace Microsoft.Mutate4CSharp.Tests.Integration;

/// <summary>
/// The shared hermetic-sample generator for slice S6's integration and acceptance tests (T16, T17, and
/// the T18 acceptance suite). It materializes a self-contained .NET 8 repository under a unique temp
/// root — a production <c>&lt;Project&gt;.csproj</c> with caller-supplied source files, a matching
/// <c>&lt;Project&gt;.Tests.csproj</c> (xUnit + <c>coverlet.collector</c>) with a
/// <c>ProjectReference</c> back to it, an optional stray project and root <c>.sln</c>, and the hermetic
/// guards (empty <c>Directory.Build.props</c>/<c>.targets</c> plus a nuget.org-pinned
/// <c>nuget.config</c>) that stop MSBuild / NuGet from walking above the temp root. The package-version
/// pins are held here (folded in from the former <c>HermeticSample</c>) and kept byte-identical to
/// <c>Mutate4CSharp.Tests.Common.targets</c> so every generated sample restores the exact versions the
/// main solution's restore already populated in the global cache; the
/// <c>TestProjectFactoryVersionDriftTests</c> unit test guards that they stay in sync.
/// </summary>
/// <remarks>
/// The production and test <c>.csproj</c> files are written only when the caller supplies at least one
/// matching source file, so a sample can deliberately omit the test project (a production project with
/// no <c>&lt;Project&gt;.Tests</c>) or omit both projects entirely (a loose <c>.cs</c> with no owning
/// project) — the two DD2 fail-fast shapes the acceptance suite exercises — while a full sample simply
/// supplies both. Each <see cref="Create"/> call returns a fresh <see cref="TestProject"/> handle that
/// owns its temp root and deletes it best-effort on disposal.
/// </remarks>
public sealed class TestProjectFactory
{
    /// <summary>The target framework every generated project builds against.</summary>
    public const string TargetFramework = "net8.0";

    /// <summary>The pinned <c>Microsoft.NET.Test.Sdk</c> version (matches the tests' common targets).</summary>
    public const string TestSdkVersion = "17.8.0";

    /// <summary>The pinned <c>xunit</c> / <c>xunit.runner.visualstudio</c> version.</summary>
    public const string XunitVersion = "2.5.3";

    /// <summary>The pinned <c>coverlet.collector</c> version.</summary>
    public const string CoverletCollectorVersion = "6.0.0";

    /// <summary>
    /// The <c>nuget.config</c> that clears inherited sources and pins restore to nuget.org, so a
    /// generated sample resolves exactly the pinned versions from the shared global package cache.
    /// </summary>
    public const string NuGetConfig =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
          </packageSources>
        </configuration>
        """;

    private const string StrayProjectName = "Stray";

    private readonly List<SourceFile> _productionFiles = [];
    private readonly List<SourceFile> _testFiles = [];
    private readonly List<SourceFile> _looseFiles = [];
    private string _projectName = "Sample";
    private bool _includeStrayProject;
    private bool _includeSolution;

    /// <summary>
    /// Sets the production project name (default <c>Sample</c>); the test project is named
    /// <c>&lt;name&gt;.Tests</c>.
    /// </summary>
    /// <param name="name">The production project name.</param>
    /// <returns>This factory, for chaining.</returns>
    public TestProjectFactory WithProjectName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _projectName = name;
        return this;
    }

    /// <summary>
    /// Adds a production source file under the <c>&lt;Project&gt;/</c> directory. Supplying any
    /// production file causes the production <c>.csproj</c> to be written.
    /// </summary>
    /// <param name="fileName">The file name (for example <c>Calculator.cs</c>).</param>
    /// <param name="content">The file contents.</param>
    /// <returns>This factory, for chaining.</returns>
    public TestProjectFactory WithProductionFile(string fileName, string content)
    {
        _productionFiles.Add(new SourceFile(fileName, content));
        return this;
    }

    /// <summary>
    /// Adds a test source file under the <c>&lt;Project&gt;.Tests/</c> directory. Supplying any test
    /// file causes the test <c>.csproj</c> (with its <c>ProjectReference</c> to the production project)
    /// to be written.
    /// </summary>
    /// <param name="fileName">The file name (for example <c>CalculatorTests.cs</c>).</param>
    /// <param name="content">The file contents.</param>
    /// <returns>This factory, for chaining.</returns>
    public TestProjectFactory WithTestFile(string fileName, string content)
    {
        _testFiles.Add(new SourceFile(fileName, content));
        return this;
    }

    /// <summary>
    /// Adds a file at an arbitrary path relative to the sample root, outside any project — for example
    /// a loose <c>.cs</c> file with no owning project.
    /// </summary>
    /// <param name="relativePath">The path relative to the sample root (forward or back slashes).</param>
    /// <param name="content">The file contents.</param>
    /// <returns>This factory, for chaining.</returns>
    public TestProjectFactory WithLooseFile(string relativePath, string content)
    {
        _looseFiles.Add(new SourceFile(relativePath, content));
        return this;
    }

    /// <summary>
    /// Adds a deliberately non-compiling stray sibling project (referenced by the generated
    /// <c>.sln</c>) that a correctly scoped run must never build.
    /// </summary>
    /// <returns>This factory, for chaining.</returns>
    public TestProjectFactory WithStrayProject()
    {
        _includeStrayProject = true;
        return this;
    }

    /// <summary>
    /// Writes a root <c>&lt;Project&gt;.sln</c> referencing the generated projects — the anti-fan-out
    /// trap a scoped <c>dotnet test &lt;project&gt;</c> must ignore.
    /// </summary>
    /// <returns>This factory, for chaining.</returns>
    public TestProjectFactory WithSolution()
    {
        _includeSolution = true;
        return this;
    }

    /// <summary>
    /// Materializes the configured sample under a fresh unique temp root and returns its handle.
    /// </summary>
    /// <returns>The generated sample handle.</returns>
    public TestProject Create()
    {
        string root = Path.Combine(Path.GetTempPath(), "mutate4csharp-sample", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        WriteHermeticGuards(root);

        string projectDirectory = Path.Combine(root, _projectName);
        string projectFile = Path.Combine(projectDirectory, _projectName + ".csproj");
        if (_productionFiles.Count > 0)
        {
            WriteFiles(projectDirectory, ProductionProject(), _projectName + ".csproj", _productionFiles);
        }

        string testProjectName = _projectName + ".Tests";
        string testProjectDirectory = Path.Combine(root, testProjectName);
        string testProjectFile = Path.Combine(testProjectDirectory, testProjectName + ".csproj");
        if (_testFiles.Count > 0)
        {
            WriteFiles(testProjectDirectory, TestProject(), testProjectName + ".csproj", _testFiles);
        }

        if (_includeStrayProject)
        {
            WriteStrayProject(root);
        }

        if (_includeSolution)
        {
            File.WriteAllText(Path.Combine(root, _projectName + ".sln"), SolutionContent());
        }

        foreach (SourceFile loose in _looseFiles)
        {
            WriteLooseFile(root, loose);
        }

        return new TestProject(root, _projectName, projectDirectory, projectFile, testProjectDirectory, testProjectFile);
    }

    /// <summary>
    /// Writes the hermetic guards at <paramref name="root"/>: empty <c>Directory.Build.props</c> and
    /// <c>Directory.Build.targets</c> so MSBuild never walks above the temp root into a machine-level
    /// import, plus the nuget.org-pinned <see cref="NuGetConfig"/> served from the global cache the
    /// main solution's restore already populated with these exact package versions.
    /// </summary>
    /// <param name="root">The temp root the generated sample (and its worker copies) live under.</param>
    public static void WriteHermeticGuards(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        File.WriteAllText(Path.Combine(root, "Directory.Build.props"), "<Project />");
        File.WriteAllText(Path.Combine(root, "Directory.Build.targets"), "<Project />");
        File.WriteAllText(Path.Combine(root, "nuget.config"), NuGetConfig);
    }

    private static void WriteFiles(
        string directory, string projectContent, string projectFileName, IReadOnlyList<SourceFile> files)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, projectFileName), projectContent);
        foreach (SourceFile file in files)
        {
            File.WriteAllText(Path.Combine(directory, file.RelativePath), file.Content);
        }
    }

    private static void WriteLooseFile(string root, SourceFile loose)
    {
        string path = Path.Combine(root, loose.RelativePath.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, loose.Content);
    }

    private static string ProductionProject()
    {
        return
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{TargetFramework}</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """;
    }

    private void WriteStrayProject(string root)
    {
        string strayDirectory = Path.Combine(root, StrayProjectName);
        Directory.CreateDirectory(strayDirectory);
        string strayProject =
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{TargetFramework}</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """;
        const string brokenSource =
            """
            namespace Stray;

            public static class Broken
            {
                public static int Value()
                {
                    // Intentional compile error (string is not convertible to int): this project must
                    // never be built by a correctly scoped run.
                    return "not an int";
                }
            }
            """;
        File.WriteAllText(Path.Combine(strayDirectory, StrayProjectName + ".csproj"), strayProject);
        File.WriteAllText(Path.Combine(strayDirectory, "Broken.cs"), brokenSource);
    }

    private string TestProject()
    {
        return
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{TargetFramework}</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <IsPackable>false</IsPackable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="{TestSdkVersion}" />
                <PackageReference Include="xunit" Version="{XunitVersion}" />
                <PackageReference Include="xunit.runner.visualstudio" Version="{XunitVersion}" />
                <PackageReference Include="coverlet.collector" Version="{CoverletCollectorVersion}" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include="../{_projectName}/{_projectName}.csproj" />
              </ItemGroup>
            </Project>
            """;
    }

    private string SolutionContent()
    {
        // A plain (non-interpolated) template so the literal single-brace project/GUID braces stay
        // verbatim; the project names are substituted afterwards. The exact GUIDs are irrelevant — the
        // .sln is only an anti-fan-out trap a scoped `dotnet test <project>` never reads.
        const string projectEntry =
            "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"__NAME__\", \"__PATH__\", \"__GUID__\"\nEndProject\n";
        string testProjectName = _projectName + ".Tests";
        string entries =
            ProjectEntry(projectEntry, _projectName, _projectName + "/" + _projectName + ".csproj", "{A1111111-1111-1111-1111-111111111111}")
            + ProjectEntry(projectEntry, testProjectName, testProjectName + "/" + testProjectName + ".csproj", "{B2222222-2222-2222-2222-222222222222}");
        if (_includeStrayProject)
        {
            entries += ProjectEntry(
                projectEntry,
                StrayProjectName,
                StrayProjectName + "/" + StrayProjectName + ".csproj",
                "{C3333333-3333-3333-3333-333333333333}");
        }

        return
            "Microsoft Visual Studio Solution File, Format Version 12.00\n"
            + "# Visual Studio Version 17\n"
            + entries
            + "Global\n"
            + "  GlobalSection(SolutionConfigurationPlatforms) = preSolution\n"
            + "    Debug|Any CPU = Debug|Any CPU\n"
            + "    Release|Any CPU = Release|Any CPU\n"
            + "  EndGlobalSection\n"
            + "EndGlobal\n";
    }

    private static string ProjectEntry(string template, string name, string path, string guid)
    {
        return template
            .Replace("__NAME__", name, StringComparison.Ordinal)
            .Replace("__PATH__", path, StringComparison.Ordinal)
            .Replace("__GUID__", guid, StringComparison.Ordinal);
    }

    private sealed record SourceFile(string RelativePath, string Content);
}

/// <summary>
/// A generated hermetic sample: the temp root and the resolved paths of the production and test
/// projects. Disposal deletes the temp root best-effort, tolerating <see cref="IOException"/> and
/// <see cref="UnauthorizedAccessException"/> because a just-finished test host may still hold a
/// transient handle under the sample's build output.
/// </summary>
public sealed class TestProject : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestProject"/> class.
    /// </summary>
    /// <param name="root">The sample's temp root (and the workspace root the tool runs against).</param>
    /// <param name="projectName">The production project name.</param>
    /// <param name="projectDirectory">The production project directory.</param>
    /// <param name="projectFile">The production <c>.csproj</c> path.</param>
    /// <param name="testProjectDirectory">The test project directory.</param>
    /// <param name="testProjectFile">The test <c>.csproj</c> path.</param>
    public TestProject(
        string root,
        string projectName,
        string projectDirectory,
        string projectFile,
        string testProjectDirectory,
        string testProjectFile)
    {
        Root = root;
        ProjectName = projectName;
        ProjectDirectory = projectDirectory;
        ProjectFile = projectFile;
        TestProjectDirectory = testProjectDirectory;
        TestProjectFile = testProjectFile;
    }

    /// <summary>Gets the sample's temp root (the workspace root the tool runs against).</summary>
    public string Root { get; }

    /// <summary>Gets the production project name.</summary>
    public string ProjectName { get; }

    /// <summary>Gets the production project directory (<c>Root/&lt;Project&gt;</c>).</summary>
    public string ProjectDirectory { get; }

    /// <summary>Gets the production <c>&lt;Project&gt;.csproj</c> path.</summary>
    public string ProjectFile { get; }

    /// <summary>Gets the test project directory (<c>Root/&lt;Project&gt;.Tests</c>).</summary>
    public string TestProjectDirectory { get; }

    /// <summary>Gets the test <c>&lt;Project&gt;.Tests.csproj</c> path.</summary>
    public string TestProjectFile { get; }

    /// <summary>Returns the absolute path of a production source file by name.</summary>
    /// <param name="fileName">The production file name.</param>
    /// <returns>The absolute path under the production project directory.</returns>
    public string ProductionFile(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return Path.Combine(ProjectDirectory, fileName);
    }

    /// <summary>Returns the path of <paramref name="absolutePath"/> relative to the sample root.</summary>
    /// <param name="absolutePath">An absolute path under the sample root.</param>
    /// <returns>The sample-root-relative path.</returns>
    public string RelativeToRoot(string absolutePath)
    {
        ArgumentNullException.ThrowIfNull(absolutePath);
        return Path.GetRelativePath(Root, absolutePath);
    }

    /// <summary>Best-effort deletes the sample's temp root.</summary>
    public void Dispose()
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort: the test host may still hold a transient handle under the sample's output.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort: a file may be momentarily locked or read-only; leave it for the OS sweep.
        }
    }
}
