namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Analysis;
using Microsoft.Mutate4CSharp.Manifest;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Proves the DD1 scopes produced by <see cref="MutationCatalog.Analyze"/> integrate with the T3
/// embedded manifest: analyzing a source, embedding the resulting scopes and module hash via
/// <see cref="ManifestSupport.Write"/>, then reading them back preserves every scope's id, kind, line
/// range, and semantic hash (across the base64url id encoding), plus the module hash.
/// </summary>
public sealed class MutationCatalogManifestRoundTripTests : IDisposable
{
    private readonly ManifestSupport _manifestSupport = new();
    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="MutationCatalogManifestRoundTripTests"/> class,
    /// creating a per-test temporary directory.
    /// </summary>
    public MutationCatalogManifestRoundTripTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-roundtrip-" + Guid.NewGuid().ToString("N"));
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
    /// Analyzing, embedding, and re-reading the manifest preserves the module hash and every scope,
    /// including scope ids that carry operator symbols and parentheses.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void AnalyzeWriteAndReadPreservesScopesAndModuleHash()
    {
        string file = Path.Combine(_tempDir, "Sample.cs");
        string source =
            """
            namespace Demo;

            struct Vec
            {
                int _x = 0;

                public Vec()
                {
                }

                public static Vec operator +(Vec a, Vec b)
                {
                    return a;
                }
            }
            """;
        File.WriteAllText(file, source);

        SourceAnalysis analysis = new MutationCatalog().Analyze(file);
        analysis.Scopes.Should().NotBeEmpty();
        DifferentialManifest manifest = new(1, analysis.ModuleHash, analysis.Scopes);

        _manifestSupport.Write(file, analysis.SourceWithoutManifest, manifest);
        DifferentialManifest? read = _manifestSupport.Read(file);

        read.Should().NotBeNull();
        read!.Version.Should().Be(1);
        read.ModuleHash.Should().Be(analysis.ModuleHash);
        read.Scopes.Should().Equal(analysis.Scopes);
    }
}
