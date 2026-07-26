namespace Microsoft.Mutate4CSharp.Model;

/// <summary>
/// A parsed differential manifest: the format version, the module hash, and the recorded
/// mutation scopes.
/// </summary>
/// <param name="Version">The manifest format version.</param>
/// <param name="ModuleHash">The module hash.</param>
/// <param name="Scopes">The recorded mutation scopes.</param>
public sealed record DifferentialManifest(int Version, string ModuleHash, IReadOnlyList<MutationScope> Scopes);
