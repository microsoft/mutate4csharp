namespace Microsoft.Mutate4CSharp.Manifest;

using System.Text;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// The public entry point for the embedded differential manifest: reading it from a source file,
/// stripping it so it does not perturb source analysis, writing an updated one back, and hashing
/// scopes. Delegates to the boundary, codec, and hashing collaborators.
/// </summary>
public sealed class ManifestSupport
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly ManifestBoundary _boundary = new();
    private readonly ManifestCodec _codec = new();
    private readonly ManifestHashing _hashing = new();

    /// <summary>
    /// Reads and parses the embedded manifest from the given source file, if present.
    /// </summary>
    /// <param name="sourceFile">The source file to read.</param>
    /// <returns>The parsed manifest, or <see langword="null"/> when none is embedded.</returns>
    public DifferentialManifest? Read(string sourceFile)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        string raw = File.ReadAllText(sourceFile);
        int start = _boundary.StartIndex(raw);
        if (start < 0)
        {
            return null;
        }

        int end = raw.IndexOf(ManifestBoundary.End, start, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        string body = raw[(start + ManifestBoundary.Start.Length)..end].Trim();
        return _codec.Parse(body);
    }

    /// <summary>
    /// Returns the source text with any embedded manifest footer removed.
    /// </summary>
    /// <param name="rawSource">The raw source text.</param>
    /// <returns>The source text without the manifest footer.</returns>
    public string StripManifest(string rawSource)
    {
        ArgumentNullException.ThrowIfNull(rawSource);
        int start = _boundary.StartIndex(rawSource);
        if (start < 0)
        {
            return rawSource;
        }

        return rawSource[..start].TrimEnd() + "\n";
    }

    /// <summary>
    /// Writes the source with a freshly serialized manifest footer appended to the given file.
    /// </summary>
    /// <param name="sourceFile">The source file to write.</param>
    /// <param name="sourceWithoutManifest">The source text without any manifest footer.</param>
    /// <param name="manifest">The manifest to embed.</param>
    public void Write(string sourceFile, string sourceWithoutManifest, DifferentialManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(sourceWithoutManifest);
        ArgumentNullException.ThrowIfNull(manifest);
        string updated = sourceWithoutManifest.TrimEnd() + "\n\n"
            + _codec.Serialize(manifest, ManifestBoundary.Start, ManifestBoundary.End);
        File.WriteAllText(sourceFile, updated, Utf8NoBom);
    }

    /// <summary>
    /// Hashes the given scopes into the aggregate module scope hash.
    /// </summary>
    /// <param name="scopes">The scopes to hash.</param>
    /// <returns>The lowercase SHA-256 hex digest of the aggregated scope lines.</returns>
    public string HashScopes(IReadOnlyList<MutationScope> scopes)
    {
        return _hashing.HashScopes(scopes);
    }

    /// <summary>
    /// Computes the lowercase SHA-256 hex digest of the given text.
    /// </summary>
    /// <param name="text">The text to hash.</param>
    /// <returns>The lowercase SHA-256 hex digest.</returns>
    public string Hash(string text)
    {
        return _hashing.Hash(text);
    }
}
