namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Project;

/// <summary>
/// Faithful counterpart of mutate4java's <c>SourceFileFinderTest</c> (adapting <c>.java</c> →
/// <c>.cs</c>): an empty result when <c>src</c> is missing, and finding plus ordinally sorting the
/// <c>.cs</c> files under <c>src</c> only. Adds one C#-specific case for the <c>bin</c>/<c>obj</c>
/// build-output exclusion the port introduces.
/// </summary>
public sealed class SourceFileFinderTests : IDisposable
{
    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceFileFinderTests"/> class, creating a
    /// per-test temporary directory (the analog of JUnit's <c>@TempDir</c>).
    /// </summary>
    public SourceFileFinderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-srcfinder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// Deletes the per-test temporary directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A missing <c>src</c> directory yields an empty list.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ReturnsEmptyListWhenSrcDirectoryIsMissing()
    {
        SourceFileFinder.FindAllCSharpFilesUnderSrc(_tempDir).Should().BeEmpty();
    }

    /// <summary>
    /// Only <c>.cs</c> files under <c>src</c> are returned, ordinally sorted; non-<c>.cs</c> files and
    /// files outside <c>src</c> are excluded.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FindsAndSortsCSharpFilesUnderSrcOnly()
    {
        string src = Path.Combine(_tempDir, "src", "Mutate4CSharp");
        Directory.CreateDirectory(src);
        string first = Path.Combine(src, "A.cs");
        string second = Path.Combine(src, "nested", "B.cs");
        string ignored = Path.Combine(_tempDir, "test", "Mutate4CSharp", "C.cs");
        string notCSharp = Path.Combine(src, "notes.txt");

        Directory.CreateDirectory(Path.GetDirectoryName(second)!);
        Directory.CreateDirectory(Path.GetDirectoryName(ignored)!);
        File.WriteAllText(second, "class B {}");
        File.WriteAllText(first, "class A {}");
        File.WriteAllText(ignored, "class C {}");
        File.WriteAllText(notCSharp, "ignore");

        SourceFileFinder.FindAllCSharpFilesUnderSrc(_tempDir).Should().Equal(first, second);
    }

    /// <summary>
    /// <c>.cs</c> files under <c>bin</c>/<c>obj</c> build-output directories are excluded (the C#
    /// ecosystem adaptation).
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ExcludesBinAndObjBuildOutput()
    {
        string src = Path.Combine(_tempDir, "src", "Mutate4CSharp");
        Directory.CreateDirectory(src);
        string kept = Path.Combine(src, "Keep.cs");
        string binFile = Path.Combine(src, "bin", "Release", "net8.0", "Generated.cs");
        string objFile = Path.Combine(src, "obj", "Debug", "Generated.cs");

        Directory.CreateDirectory(Path.GetDirectoryName(binFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(objFile)!);
        File.WriteAllText(kept, "class Keep {}");
        File.WriteAllText(binFile, "class Generated {}");
        File.WriteAllText(objFile, "class Generated {}");

        SourceFileFinder.FindAllCSharpFilesUnderSrc(_tempDir).Should().Equal(kept);
    }
}
