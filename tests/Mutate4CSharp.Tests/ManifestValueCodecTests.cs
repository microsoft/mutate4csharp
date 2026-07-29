namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Manifest;

/// <summary>
/// Fine-grained tests for the bespoke, hand-rolled URL-safe/unpadded base64 codec (net8 has no
/// in-box <c>Base64Url</c>). The vectors pin byte-for-byte fidelity with the Java
/// <c>Base64.getUrlEncoder().withoutPadding()</c> output, including the <c>+</c>→<c>-</c> and
/// <c>/</c>→<c>_</c> substitutions and padding removal.
/// </summary>
public class ManifestValueCodecTests
{
    /// <summary>
    /// Encoding yields URL-safe, unpadded base64 that matches the Java encoder byte for byte.
    /// </summary>
    /// <param name="plain">The plaintext to encode.</param>
    /// <param name="encoded">The expected URL-safe, unpadded base64.</param>
    [Theory]
    [Trait("type", "UnitTests")]
    [InlineData("", "")]
    [InlineData("method:ManifestBoundary#startIndex(1):15", "bWV0aG9kOk1hbmlmZXN0Qm91bmRhcnkjc3RhcnRJbmRleCgxKToxNQ")]
    [InlineData("\u00ff>", "w78-")]
    [InlineData("\u00ff?", "w78_")]
    [InlineData("\u00fb\u00ff", "w7vDvw")]
    public void EncodeMatchesUrlSafeUnpaddedBase64(string plain, string encoded)
    {
        ManifestValueCodec.Encode(plain).Should().Be(encoded);
    }

    /// <summary>
    /// Decoding reverses the encoding, re-padding and translating the URL-safe alphabet.
    /// </summary>
    /// <param name="plain">The expected decoded plaintext.</param>
    /// <param name="encoded">The URL-safe, unpadded base64 to decode.</param>
    [Theory]
    [Trait("type", "UnitTests")]
    [InlineData("", "")]
    [InlineData("method:ManifestBoundary#startIndex(1):15", "bWV0aG9kOk1hbmlmZXN0Qm91bmRhcnkjc3RhcnRJbmRleCgxKToxNQ")]
    [InlineData("\u00ff>", "w78-")]
    [InlineData("\u00ff?", "w78_")]
    [InlineData("\u00fb\u00ff", "w7vDvw")]
    public void DecodeReversesEncoding(string plain, string encoded)
    {
        ManifestValueCodec.Decode(encoded).Should().Be(plain);
    }

    /// <summary>
    /// Encoding then decoding round-trips arbitrary strings, including Unicode and manifest ids.
    /// </summary>
    /// <param name="value">The value to round-trip.</param>
    [Theory]
    [Trait("type", "UnitTests")]
    [InlineData("")]
    [InlineData("class:Demo.Sample#Sample:10")]
    [InlineData("operator:Vector#operator+(2):42")]
    [InlineData("naïve:ünïcödë#hash:1")]
    public void EncodeDecodeRoundTrips(string value)
    {
        ManifestValueCodec.Decode(ManifestValueCodec.Encode(value)).Should().Be(value);
    }
}
