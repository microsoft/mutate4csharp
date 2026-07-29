namespace Microsoft.Mutate4CSharp.Manifest;

using System.Globalization;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Parses the embedded manifest body (the <c>key=value</c> lines between the footer delimiters)
/// back into a <see cref="DifferentialManifest"/>.
/// </summary>
public sealed class ManifestParser
{
    /// <summary>
    /// Parses the manifest body into a <see cref="DifferentialManifest"/>.
    /// </summary>
    /// <param name="body">The manifest body text (without the footer delimiters).</param>
    /// <returns>The parsed manifest.</returns>
    public DifferentialManifest Parse(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        Dictionary<int, Dictionary<string, string>> scopes = new();
        int version = 1;
        string moduleHash = string.Empty;
        foreach (string line in body.Split('\n'))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                ReadScopeLine(scopes, line);
                if (line.StartsWith("version=", StringComparison.Ordinal))
                {
                    version = int.Parse(line["version=".Length..], CultureInfo.InvariantCulture);
                }
                else if (line.StartsWith("moduleHash=", StringComparison.Ordinal))
                {
                    moduleHash = line["moduleHash=".Length..];
                }
            }
        }

        List<MutationScope> parsedScopes = scopes
            .OrderBy(entry => entry.Key)
            .Select(entry => ToScope(entry.Value))
            .ToList();
        return new DifferentialManifest(version, moduleHash, parsedScopes);
    }

    private void ReadScopeLine(Dictionary<int, Dictionary<string, string>> scopes, string line)
    {
        int separator = line.IndexOf('=', StringComparison.Ordinal);
        if (separator < 0)
        {
            return;
        }

        string key = line[..separator];
        if (!key.StartsWith("scope.", StringComparison.Ordinal))
        {
            return;
        }

        string[] parts = key.Split('.');
        if (parts.Length != 3)
        {
            return;
        }

        int index = int.Parse(parts[1], CultureInfo.InvariantCulture);
        if (!scopes.TryGetValue(index, out Dictionary<string, string>? values))
        {
            values = new Dictionary<string, string>(StringComparer.Ordinal);
            scopes[index] = values;
        }

        values[parts[2]] = line[(separator + 1)..];
    }

    private MutationScope ToScope(Dictionary<string, string> values)
    {
        return new MutationScope(
            ManifestValueCodec.Decode(values["id"]),
            values["kind"],
            int.Parse(values["startLine"], CultureInfo.InvariantCulture),
            int.Parse(values["endLine"], CultureInfo.InvariantCulture),
            values["semanticHash"]);
    }
}
