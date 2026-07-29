namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Project;

/// <summary>
/// New unit tests (no mutate4java oracle) for the DD2/DD3 <see cref="ModuleResolver"/> that replaces
/// mutate4java's nearest-<c>pom.xml</c> <c>ModuleRootFinder</c>. Covers <c>&lt;Project&gt;</c>
/// derivation, <c>.Tests</c> vs <c>.UnitTests</c> selection, <c>ProjectReference</c> validation
/// (a same-named test project for a different production project is rejected), the tie-break order,
/// and the typed not-found signals.
/// </summary>
public sealed class ModuleResolverTests : IDisposable
{
    private readonly string _root;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleResolverTests"/> class, creating a per-test
    /// temporary workspace root.
    /// </summary>
    public ModuleResolverTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "m4cs-module-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    /// <summary>
    /// Deletes the per-test temporary workspace root.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A sibling <c>&lt;Project&gt;.Tests.csproj</c> that references the derived project resolves,
    /// deriving <c>&lt;Project&gt;</c> from the nearest ancestor <c>.csproj</c>.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ResolvesProjectAndTestProject()
    {
        WriteProject("Foo", "Foo.csproj");
        string testProject = WriteProject("Foo.Tests", "Foo.Tests.csproj", "../Foo/Foo.csproj");
        string source = WriteSource("Foo", "Sample.cs");

        ModuleResolution result = Resolve(source);

        result.Status.Should().Be(ModuleResolutionStatus.Resolved);
        result.ProjectName.Should().Be("Foo");
        result.ProjectFile.Should().Be(FullPath("Foo", "Foo.csproj"));
        result.TestProjectFile.Should().Be(testProject);
    }

    /// <summary>
    /// When both suffixes reference the project from equally ranked (sibling) locations,
    /// <c>.Tests</c> is preferred over <c>.UnitTests</c>.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void PrefersTestsOverUnitTestsAsFinalTieBreak()
    {
        WriteProject("Foo", "Foo.csproj");
        string tests = WriteProject("Foo.Tests", "Foo.Tests.csproj", "../Foo/Foo.csproj");
        WriteProject("Foo.UnitTests", "Foo.UnitTests.csproj", "../Foo/Foo.csproj");
        string source = WriteSource("Foo", "Sample.cs");

        Resolve(source).TestProjectFile.Should().Be(tests);
    }

    /// <summary>
    /// The <c>.UnitTests</c> suffix resolves when it is the only referencing test project.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ResolvesUnitTestsWhenOnlySuffixPresent()
    {
        WriteProject("Foo", "Foo.csproj");
        string unitTests = WriteProject("Foo.UnitTests", "Foo.UnitTests.csproj", "../Foo/Foo.csproj");
        string source = WriteSource("Foo", "Sample.cs");

        ModuleResolution result = Resolve(source);

        result.Status.Should().Be(ModuleResolutionStatus.Resolved);
        result.TestProjectFile.Should().Be(unitTests);
    }

    /// <summary>
    /// A test project whose name matches but whose <c>ProjectReference</c> closure points at a
    /// different production project is rejected (mono-repo mapping validation, risk R-A/R-B).
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RejectsSameNamedTestProjectForDifferentProductionProject()
    {
        WriteProject("Foo", "Foo.csproj");
        WriteProject("Bar", "Bar.csproj");
        WriteProject("Foo.Tests", "Foo.Tests.csproj", "../Bar/Bar.csproj");
        string source = WriteSource("Foo", "Sample.cs");

        ModuleResolution result = Resolve(source);

        result.Status.Should().Be(ModuleResolutionStatus.NoTestProject);
        result.ProjectName.Should().Be("Foo");
    }

    /// <summary>
    /// A test project referencing the project transitively (through an intermediate project) is
    /// accepted.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void AcceptsTransitiveProjectReference()
    {
        WriteProject("Foo", "Foo.csproj");
        WriteProject("Mid", "Mid.csproj", "../Foo/Foo.csproj");
        string tests = WriteProject("Foo.Tests", "Foo.Tests.csproj", "../Mid/Mid.csproj");
        string source = WriteSource("Foo", "Sample.cs");

        ModuleResolution result = Resolve(source);

        result.Status.Should().Be(ModuleResolutionStatus.Resolved);
        result.TestProjectFile.Should().Be(tests);
    }

    /// <summary>
    /// A sibling test project outranks an equally named one under a <c>tests/</c> directory.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void PrefersSiblingOverTestsDirectory()
    {
        WriteProject("src/Foo", "Foo.csproj");
        string sibling = WriteProject("src/Foo.Tests", "Foo.Tests.csproj", "../Foo/Foo.csproj");
        WriteProject("tests/Foo.Tests", "Foo.Tests.csproj", "../../src/Foo/Foo.csproj");
        string source = WriteSource("src/Foo", "Sample.cs");

        Resolve(source).TestProjectFile.Should().Be(sibling);
    }

    /// <summary>
    /// No owning <c>.csproj</c> above the target yields the <see cref="ModuleResolutionStatus.NoOwningProject"/>
    /// signal.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void SignalsNoOwningProjectWhenNoCsprojFound()
    {
        string source = WriteSource("Foo", "Sample.cs");

        Resolve(source).Status.Should().Be(ModuleResolutionStatus.NoOwningProject);
    }

    /// <summary>
    /// An owning project with no referencing test project yields the
    /// <see cref="ModuleResolutionStatus.NoTestProject"/> signal, carrying the derived project.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void SignalsNoTestProjectWhenNoneReferencesProject()
    {
        WriteProject("Foo", "Foo.csproj");
        string source = WriteSource("Foo", "Sample.cs");

        ModuleResolution result = Resolve(source);

        result.Status.Should().Be(ModuleResolutionStatus.NoTestProject);
        result.ProjectFile.Should().Be(FullPath("Foo", "Foo.csproj"));
    }

    private ModuleResolution Resolve(string sourceFile)
    {
        return new ModuleResolver(_root).Resolve(sourceFile);
    }

    private string FullPath(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine(_root, Path.Combine(segments)));
    }

    private string WriteProject(string relativeDir, string fileName, params string[] projectReferences)
    {
        string directory = Path.Combine(_root, relativeDir.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        string references = string.Concat(
            projectReferences.Select(reference => $"    <ProjectReference Include=\"{reference}\" />\n"));
        string xml = $"<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup>\n{references}  </ItemGroup>\n</Project>\n";
        File.WriteAllText(path, xml);
        return path;
    }

    private string WriteSource(string relativeDir, string fileName)
    {
        string directory = Path.Combine(_root, relativeDir.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "class Sample {}");
        return path;
    }
}
