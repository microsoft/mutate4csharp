namespace Microsoft.Mutate4CSharp.Manifest;

using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Bridges the manifest parser and serializer: parses an embedded footer body into a
/// <see cref="DifferentialManifest"/> and serializes one back out.
/// </summary>
public sealed class ManifestCodec
{
    private readonly ManifestParser _parser = new();
    private readonly ManifestSerializer _serializer = new();

    /// <summary>
    /// Parses the manifest body into a <see cref="DifferentialManifest"/>.
    /// </summary>
    /// <param name="body">The manifest body text (without the footer delimiters).</param>
    /// <returns>The parsed manifest.</returns>
    public DifferentialManifest Parse(string body)
    {
        return _parser.Parse(body);
    }

    /// <summary>
    /// Serializes the manifest into its embedded footer text.
    /// </summary>
    /// <param name="manifest">The manifest to serialize.</param>
    /// <param name="start">The opening delimiter.</param>
    /// <param name="end">The closing delimiter.</param>
    /// <returns>The serialized manifest text.</returns>
    public string Serialize(DifferentialManifest manifest, string start, string end)
    {
        return _serializer.Serialize(manifest, start, end);
    }
}
