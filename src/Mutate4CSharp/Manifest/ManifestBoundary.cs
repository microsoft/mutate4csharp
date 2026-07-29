namespace Microsoft.Mutate4CSharp.Manifest;

/// <summary>
/// Detects the embedded manifest footer comment so it can be stripped before source analysis and
/// located for parsing. The marker string is the C# port's <c>mutate4csharp-manifest</c> (the one
/// intentional string adaptation of the Java <c>mutate4java-manifest</c> marker).
/// </summary>
public sealed class ManifestBoundary
{
    /// <summary>The opening delimiter of the embedded manifest footer comment.</summary>
    public const string Start = "/* mutate4csharp-manifest\n";

    /// <summary>The closing delimiter of the embedded manifest footer comment.</summary>
    public const string End = "*/";

    /// <summary>
    /// Returns the index of the last embedded manifest footer comment, or -1 when none is present
    /// or the trailing comment is not properly closed.
    /// </summary>
    /// <param name="raw">The raw source text.</param>
    /// <returns>The start index of the manifest footer, or -1.</returns>
    public int StartIndex(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        int start = raw.LastIndexOf(Start, StringComparison.Ordinal);
        if (start < 0)
        {
            return -1;
        }

        string tail = raw[start..];
        return tail.Trim().EndsWith(End, StringComparison.Ordinal) ? start : -1;
    }
}
