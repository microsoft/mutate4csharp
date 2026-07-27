namespace Microsoft.Mutate4CSharp.Manifest;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Computes the SHA-256 hex digests used by the manifest: the aggregate hash over the sorted
/// scope identity/semantic-hash pairs, and the primitive hash of arbitrary text. Hex encoding is
/// lowercase to match the Java <c>String.format("%02x", ...)</c> output byte for byte.
/// </summary>
public sealed class ManifestHashing
{
    /// <summary>
    /// Hashes the scopes by concatenating each scope's <c>id|semanticHash</c> line, sorted by id,
    /// and hashing the result.
    /// </summary>
    /// <param name="scopes">The scopes to hash.</param>
    /// <returns>The lowercase SHA-256 hex digest of the aggregated scope lines.</returns>
    public string HashScopes(IReadOnlyList<MutationScope> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        StringBuilder output = new();
        foreach (MutationScope scope in scopes.OrderBy(scope => scope.Id, StringComparer.Ordinal))
        {
            output.Append(scope.Id).Append('|').Append(scope.SemanticHash).Append('\n');
        }

        return Hash(output.ToString());
    }

    /// <summary>
    /// Computes the lowercase SHA-256 hex digest of the UTF-8 encoding of the given text.
    /// </summary>
    /// <param name="text">The text to hash.</param>
    /// <returns>The lowercase SHA-256 hex digest.</returns>
    public string Hash(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        StringBuilder hex = new(bytes.Length * 2);
        foreach (byte value in bytes)
        {
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return hex.ToString();
    }
}
