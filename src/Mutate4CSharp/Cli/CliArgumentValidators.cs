namespace Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// The composition of the individual validators used by the parser: selection-flag conflicts, the
/// single-target-file rule, line-list parsing, positive-integer parsing, and the "flag requires a
/// value" check.
/// </summary>
public sealed class CliArgumentValidators
{
    private readonly SelectionFlagValidator _selection = new();
    private readonly CSharpFileArgumentValidator _cSharpFile = new();
    private readonly LineSelectionParser _lines = new();
    private readonly IntegerArgumentParser _integers = new();

    /// <summary>
    /// Rejects invalid combinations of selection flags.
    /// </summary>
    /// <param name="state">The accumulated parse state.</param>
    public void ValidateSelectionFlags(CliArgumentParseState state)
    {
        _selection.Validate(state);
    }

    /// <summary>
    /// Ensures exactly one C# target file was supplied.
    /// </summary>
    /// <param name="values">The accumulated file arguments.</param>
    public void EnsureExactlyOneCSharpFile(IReadOnlyList<string> values)
    {
        _cSharpFile.Validate(values);
    }

    /// <summary>
    /// Parses a comma-separated line list into a set of positive line numbers.
    /// </summary>
    /// <param name="text">The raw line-list value.</param>
    /// <returns>The parsed set of line numbers.</returns>
    public IReadOnlySet<int> ParseLines(string text)
    {
        return _lines.Parse(text);
    }

    /// <summary>
    /// Parses a positive integer flag value.
    /// </summary>
    /// <param name="text">The raw value.</param>
    /// <param name="flag">The originating flag name, used in the error message.</param>
    /// <returns>The parsed positive integer.</returns>
    public int ParsePositiveInt(string text, string flag)
    {
        return _integers.ParsePositiveInt(text, flag);
    }

    /// <summary>
    /// Ensures a flag has a value at <paramref name="index"/> (present and not another flag).
    /// </summary>
    /// <param name="args">The argument vector.</param>
    /// <param name="index">The index the value is expected at.</param>
    /// <param name="flag">The originating flag name, used in the error message.</param>
    public void EnsureHasValue(string[] args, int index, string flag)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException(flag + " requires a value");
        }
    }
}
