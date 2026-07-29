namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Manifest;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Faithful counterpart of mutate4java's <c>ManifestSupportTest</c>: writes an embedded manifest
/// footer to a source file, then reads and strips it, asserting the round-trip preserves the
/// manifest identity and that stripping restores the original source. The one intentional string
/// adaptation is the renamed marker (<c>mutate4csharp-manifest</c>).
/// </summary>
public sealed class ManifestSupportTests : IDisposable
{
    private readonly ManifestSupport _manifestSupport = new();
    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManifestSupportTests"/> class, creating a
    /// per-test temporary directory (the analog of JUnit's <c>@TempDir</c>).
    /// </summary>
    public ManifestSupportTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-manifest-" + Guid.NewGuid().ToString("N"));
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
    /// Writing embeds the manifest footer; stripping restores the original source; reading parses
    /// the embedded manifest back to an equal value.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void WritesReadsAndStripsEmbeddedManifest()
    {
        string sourceFile = Path.Combine(_tempDir, "Sample.cs");
        string source =
            """
            namespace Demo;

            public class Sample
            {
                public bool Truthy()
                {
                    return true;
                }
            }

            """;
        File.WriteAllText(sourceFile, source);
        DifferentialManifest manifest = new(
            1,
            "module-hash",
            [new MutationScope("method:demo.Sample#truthy():4", "method", 4, 6, "scope-hash")]);

        _manifestSupport.Write(sourceFile, source, manifest);

        string withManifest = File.ReadAllText(sourceFile);
        withManifest.Should().Contain("mutate4csharp-manifest");
        _manifestSupport.StripManifest(withManifest).Should().Be(source.TrimEnd() + "\n");

        DifferentialManifest? read = _manifestSupport.Read(sourceFile);
        read.Should().NotBeNull();
        DifferentialManifest parsed = read!;
        parsed.Version.Should().Be(1);
        parsed.ModuleHash.Should().Be("module-hash");
        parsed.Scopes.Should().ContainSingle();
        parsed.Scopes[0].Id.Should().Be("method:demo.Sample#truthy():4");
        parsed.Scopes[0].Kind.Should().Be("method");
        parsed.Scopes[0].StartLine.Should().Be(4);
        parsed.Scopes[0].EndLine.Should().Be(6);
        parsed.Scopes[0].SemanticHash.Should().Be("scope-hash");
    }
}
