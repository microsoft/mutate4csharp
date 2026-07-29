namespace Microsoft.Mutate4CSharp.Manifest;

using System.Globalization;
using System.Text;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Serializes a <see cref="DifferentialManifest"/> into the embedded footer body, wrapped by the
/// supplied start and end delimiters. Newlines are explicit <c>"\n"</c> to match the Java
/// serializer byte for byte.
/// </summary>
public sealed class ManifestSerializer
{
    /// <summary>
    /// Serializes the manifest into its embedded footer text.
    /// </summary>
    /// <param name="manifest">The manifest to serialize.</param>
    /// <param name="start">The opening delimiter.</param>
    /// <param name="end">The closing delimiter.</param>
    /// <returns>The serialized manifest text.</returns>
    public string Serialize(DifferentialManifest manifest, string start, string end)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        StringBuilder output = new();
        output.Append(start);
        output.Append("version=").Append(manifest.Version.ToString(CultureInfo.InvariantCulture)).Append('\n');
        output.Append("moduleHash=").Append(manifest.ModuleHash).Append('\n');
        for (int i = 0; i < manifest.Scopes.Count; i++)
        {
            AppendScope(output, i, manifest.Scopes[i]);
        }

        output.Append(end).Append('\n');
        return output.ToString();
    }

    private void AppendScope(StringBuilder output, int index, MutationScope scope)
    {
        string prefix = "scope." + index.ToString(CultureInfo.InvariantCulture) + ".";
        output.Append(prefix).Append("id=").Append(ManifestValueCodec.Encode(scope.Id)).Append('\n');
        output.Append(prefix).Append("kind=").Append(scope.Kind).Append('\n');
        output.Append(prefix).Append("startLine=").Append(scope.StartLine.ToString(CultureInfo.InvariantCulture)).Append('\n');
        output.Append(prefix).Append("endLine=").Append(scope.EndLine.ToString(CultureInfo.InvariantCulture)).Append('\n');
        output.Append(prefix).Append("semanticHash=").Append(scope.SemanticHash).Append('\n');
    }
}
