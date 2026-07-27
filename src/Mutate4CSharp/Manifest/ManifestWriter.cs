namespace Microsoft.Mutate4CSharp.Manifest;

using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Writes the embedded differential manifest for a completed source analysis, delegating the
/// serialization and file update to <see cref="ManifestSupport"/>.
/// </summary>
public sealed class ManifestWriter
{
    private readonly ManifestSupport _manifestSupport;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManifestWriter"/> class.
    /// </summary>
    /// <param name="manifestSupport">The manifest support used to serialize and write.</param>
    public ManifestWriter(ManifestSupport manifestSupport)
    {
        _manifestSupport = manifestSupport;
    }

    /// <summary>
    /// Writes the manifest for the given analysis into the source file.
    /// </summary>
    /// <param name="sourceFile">The source file to write.</param>
    /// <param name="analysis">The completed source analysis.</param>
    public void Write(string sourceFile, SourceAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        _manifestSupport.Write(
            sourceFile,
            analysis.SourceWithoutManifest,
            new DifferentialManifest(1, analysis.ModuleHash, analysis.Scopes));
    }
}
