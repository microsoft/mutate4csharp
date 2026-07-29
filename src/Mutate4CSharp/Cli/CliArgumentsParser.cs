namespace Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// The public entry point for command-line parsing: turns an argument vector into a validated
/// <see cref="CliArguments"/>, throwing <see cref="ArgumentException"/> on any usage error.
/// </summary>
public static class CliArgumentsParser
{
    private static readonly CliArgumentParserEngine Engine = new(new CliArgumentValidators());

    /// <summary>
    /// Parses the argument vector into a validated <see cref="CliArguments"/>.
    /// </summary>
    /// <param name="args">The argument vector.</param>
    /// <returns>The parsed arguments.</returns>
    public static CliArguments Parse(string[] args)
    {
        return Engine.Parse(args);
    }
}
