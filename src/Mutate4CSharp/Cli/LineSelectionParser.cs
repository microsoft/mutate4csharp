namespace Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// Parses the comma-separated <c>--lines</c> value into a set of positive line numbers, skipping
/// blank entries and rejecting an all-blank value.
/// </summary>
public sealed class LineSelectionParser
{
    private readonly IntegerArgumentParser _integers = new();

    /// <summary>
    /// Parses the <c>--lines</c> value.
    /// </summary>
    /// <param name="text">The raw comma-separated value.</param>
    /// <returns>The set of selected positive line numbers.</returns>
    public IReadOnlySet<int> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        HashSet<int> lines = new();
        foreach (string part in text.Split(','))
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                lines.Add(_integers.ParsePositiveInt(part.Trim(), "--lines"));
            }
        }

        if (lines.Count == 0)
        {
            throw new ArgumentException("--lines requires at least one line number");
        }

        return lines;
    }
}
