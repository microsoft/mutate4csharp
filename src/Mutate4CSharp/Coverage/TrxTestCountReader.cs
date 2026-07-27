namespace Microsoft.Mutate4CSharp.Coverage;

using System.Globalization;
using System.Xml;
using System.Xml.Linq;

/// <summary>
/// Reads the number of executed tests from a VSTest <c>.trx</c> log. This has no mutate4java analog —
/// JaCoCo's Maven flow surfaced the baseline test result through Maven's own exit code — but coverlet
/// runs under <c>dotnet test</c>, which exits <c>0</c> even when its <c>--filter</c> matches nothing.
/// The DD2(b) fail-fast (exit <c>2</c> on a baseline that executed zero unit tests) therefore needs an
/// explicit executed-test count, obtained by adding <c>--logger trx</c> to the coverage run and
/// reading the TRX the logger writes (risk R-E). Parsing is fail-closed: a missing, unreadable, or
/// malformed TRX yields <c>0</c>, so an unclassifiable baseline is treated as "no tests executed" and
/// escalates to DD2(b) rather than silently passing.
/// </summary>
public static class TrxTestCountReader
{
    /// <summary>
    /// Returns the number of executed tests recorded in the given TRX, or <c>0</c> when the file is
    /// <see langword="null"/>, missing, or cannot be parsed. The count is read from the
    /// <c>ResultSummary/Counters/@executed</c> attribute (matched by local name so the TRX default
    /// namespace is tolerated); when that attribute is absent the recorded <c>UnitTestResult</c>
    /// entries are counted instead.
    /// </summary>
    /// <param name="trxPath">The path to the <c>.trx</c> log, or <see langword="null"/>.</param>
    /// <returns>The executed-test count, or <c>0</c> when it cannot be determined.</returns>
    public static int CountExecutedTests(string? trxPath)
    {
        if (trxPath is null || !File.Exists(trxPath))
        {
            return 0;
        }

        try
        {
            XDocument document = LoadSecure(trxPath);
            XElement? counters = document.Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "Counters", StringComparison.Ordinal));
            XAttribute? executed = counters?.Attribute("executed");
            if (executed is not null)
            {
                return ParseCount(executed.Value);
            }

            return document.Descendants()
                .Count(element => string.Equals(element.Name.LocalName, "UnitTestResult", StringComparison.Ordinal));
        }
        catch (XmlException)
        {
            return 0;
        }
    }

    private static XDocument LoadSecure(string trxPath)
    {
        // Secure reader: prohibit DTD processing and disable external entity resolution so a hostile
        // TRX cannot mount an XXE attack (the VSTest logger never emits a DOCTYPE).
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        using FileStream stream = File.OpenRead(trxPath);
        using XmlReader reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader);
    }

    private static int ParseCount(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) && result > 0
            ? result
            : 0;
    }
}
