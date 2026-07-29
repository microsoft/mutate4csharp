namespace Microsoft.Mutate4CSharp.Coverage;

using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Parses a coverlet-produced Cobertura XML report into a <see cref="CoverageReport"/> keyed by
/// absolute-path <see cref="CoverageSite"/> entries — the ecosystem-adapter port of mutate4java's
/// <c>JacocoLineCoverageParser</c> (JaCoCo XML → Cobertura XML). The role and algorithm are
/// preserved: build a set of covered lines where a line is covered iff its counter is positive; only
/// the XML schema and the key resolution change. JaCoCo's <c>&lt;line ci="N"&gt;</c> becomes
/// Cobertura's <c>&lt;line hits="N"&gt;</c> (covered iff <c>N &gt; 0</c>), and JaCoCo's package/source
/// path keying becomes A4 keying: each <c>&lt;class filename&gt;</c> is resolved against the report's
/// <c>&lt;sources&gt;</c> base(s) to a reproducible absolute path (see
/// <see cref="NormalizeSourcePath(string)"/>).
/// </summary>
public static class CoberturaLineCoverageParser
{
    /// <summary>
    /// Parses the given Cobertura XML file into a <see cref="CoverageReport"/>. A <see langword="null"/>
    /// or missing path — and a valid but empty report (no classes / no covered lines) — yields an
    /// empty report in which every site reads uncovered, the faithful analog of the Java parser's
    /// empty-report handling.
    /// </summary>
    /// <param name="coberturaXmlPath">The path to the Cobertura <c>coverage.cobertura.xml</c> file.</param>
    /// <returns>A coverage report over the covered lines discovered in the report.</returns>
    public static CoverageReport Parse(string? coberturaXmlPath)
    {
        if (coberturaXmlPath is null || !File.Exists(coberturaXmlPath))
        {
            return new CoverageReport(new HashSet<CoverageSite>());
        }

        try
        {
            XDocument document = LoadSecure(coberturaXmlPath);
            List<string> sources = ReadSources(document);
            HashSet<CoverageSite> coveredLines = [];
            foreach (XElement classElement in document.Descendants("class"))
            {
                ReadCoveredLines(classElement, sources, coveredLines);
            }

            return new CoverageReport(coveredLines);
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException($"Unable to parse Cobertura XML: {coberturaXmlPath}", ex);
        }
    }

    /// <summary>
    /// Normalizes a source path to the reproducible absolute-path form used as the coverage key
    /// (A4). <see cref="Path.GetFullPath(string)"/> resolves the path to absolute and canonicalizes
    /// directory separators; on Windows the result is upper-cased with the invariant culture so that
    /// the ordinal <see cref="CoverageSite"/> equality matches case-insensitively (Windows file paths
    /// are case-insensitive), mirroring the T5 <c>.dll</c> filter's <c>OrdinalIgnoreCase</c> intent.
    /// The T10 coverage filter MUST resolve a mutation site's file through this same method before
    /// calling <see cref="CoverageReport.Covers(string, int)"/> so that both sides produce the
    /// identical key form and match.
    /// </summary>
    /// <param name="path">The source path to normalize.</param>
    /// <returns>The canonical absolute-path key form.</returns>
    public static string NormalizeSourcePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string absolute = Path.GetFullPath(path);
        return OperatingSystem.IsWindows() ? absolute.ToUpperInvariant() : absolute;
    }

    private static XDocument LoadSecure(string coberturaXmlPath)
    {
        // Secure reader: prohibit DTD processing and disable any external entity resolution so a
        // hostile report cannot mount an XXE attack (coverlet never emits a DOCTYPE).
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        using FileStream stream = File.OpenRead(coberturaXmlPath);
        using XmlReader reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader);
    }

    private static List<string> ReadSources(XDocument document)
    {
        List<string> sources = [];
        XElement? sourcesElement = document.Root?.Element("sources");
        if (sourcesElement is not null)
        {
            foreach (XElement source in sourcesElement.Elements("source"))
            {
                string value = source.Value.Trim();
                if (value.Length > 0)
                {
                    sources.Add(value);
                }
            }
        }

        return sources;
    }

    private static void ReadCoveredLines(
        XElement classElement, List<string> sources, HashSet<CoverageSite> coveredLines)
    {
        string filename = classElement.Attribute("filename")?.Value ?? string.Empty;
        if (filename.Length == 0)
        {
            return;
        }

        foreach (XElement line in classElement.Elements("lines").Elements("line"))
        {
            int hits = ParseInt(line.Attribute("hits")?.Value);
            if (hits <= 0)
            {
                continue;
            }

            int number = ParseInt(line.Attribute("number")?.Value);
            foreach (string absolutePath in ResolveAbsolutePaths(filename, sources))
            {
                coveredLines.Add(new CoverageSite(absolutePath, number));
            }
        }
    }

    private static IEnumerable<string> ResolveAbsolutePaths(string filename, List<string> sources)
    {
        if (sources.Count == 0)
        {
            yield return NormalizeSourcePath(filename);
            yield break;
        }

        // Resolve against every declared source base: coverlet normally emits a single base, but a
        // deterministic multi-project report can carry several. Keying under each base is a safe
        // superset — the T10 filter queries one specific absolute path, so unmatched keys are inert.
        foreach (string source in sources)
        {
            yield return NormalizeSourcePath(Path.Combine(source, filename));
        }
    }

    private static int ParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result
            : 0;
    }
}
