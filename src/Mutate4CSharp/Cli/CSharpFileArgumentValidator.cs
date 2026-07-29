namespace Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// Enforces the targeting rule: exactly one explicit <c>.cs</c> file. This is the one sanctioned
/// ecosystem adaptation of mutate4java's Java-file validator.
/// </summary>
public sealed class CSharpFileArgumentValidator
{
    /// <summary>
    /// Validates that <paramref name="values"/> holds exactly one <c>.cs</c> file argument.
    /// </summary>
    /// <param name="values">The accumulated file arguments.</param>
    public void Validate(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("mutate4csharp requires exactly one C# file");
        }

        if (values.Count != 1)
        {
            throw new ArgumentException("mutate4csharp accepts exactly one C# file");
        }

        if (!values[0].EndsWith(".cs", StringComparison.Ordinal))
        {
            throw new ArgumentException("mutate4csharp target must be a .cs file");
        }
    }
}
