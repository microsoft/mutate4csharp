namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Manifest;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Fine-grained tests for the manifest SHA-256 hashing helper: the primitive text hash pins
/// lowercase hex fidelity against known SHA-256 vectors, and the scope aggregation pins the sort
/// order and the <c>id|semanticHash</c> line format.
/// </summary>
public class ManifestHashingTests
{
    private readonly ManifestHashing _hashing = new();

    /// <summary>
    /// The primitive hash is the lowercase SHA-256 hex of the UTF-8 text.
    /// </summary>
    /// <param name="text">The text to hash.</param>
    /// <param name="expected">The expected lowercase SHA-256 hex digest.</param>
    [Theory]
    [Trait("type", "UnitTests")]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    public void HashMatchesKnownSha256Hex(string text, string expected)
    {
        _hashing.Hash(text).Should().Be(expected);
    }

    /// <summary>
    /// Scope hashing sorts the scopes by id, joins each as <c>id|semanticHash</c> newline-terminated
    /// lines, and hashes the aggregate.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void HashScopesSortsByIdThenAggregatesAndHashes()
    {
        MutationScope[] scopes =
        [
            new MutationScope("bbb", "method", 2, 2, "h2"),
            new MutationScope("aaa", "class", 1, 1, "h1"),
        ];

        _hashing.HashScopes(scopes).Should().Be(_hashing.Hash("aaa|h1\nbbb|h2\n"));
    }
}
