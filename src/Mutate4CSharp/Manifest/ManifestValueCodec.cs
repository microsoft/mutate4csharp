namespace Microsoft.Mutate4CSharp.Manifest;

using System.Text;

/// <summary>
/// Encodes and decodes manifest scope identifiers using URL-safe, unpadded base64 — the faithful
/// analog of the Java <c>Base64.getUrlEncoder().withoutPadding()</c> codec. net8 has no in-box
/// <c>Base64Url</c>, so the translation is done manually over standard base64.
/// </summary>
public static class ManifestValueCodec
{
    /// <summary>
    /// Encodes the given value as URL-safe, unpadded base64 of its UTF-8 bytes.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <returns>The URL-safe, unpadded base64 representation.</returns>
    public static string Encode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Decodes a URL-safe, unpadded base64 value back to its UTF-8 string.
    /// </summary>
    /// <param name="value">The URL-safe, unpadded base64 value.</param>
    /// <returns>The decoded string.</returns>
    public static string Decode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string standard = value.Replace('-', '+').Replace('_', '/');
        standard = (standard.Length % 4) switch
        {
            2 => standard + "==",
            3 => standard + "=",
            _ => standard,
        };
        return Encoding.UTF8.GetString(Convert.FromBase64String(standard));
    }
}
