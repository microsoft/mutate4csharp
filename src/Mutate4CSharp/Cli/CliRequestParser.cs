namespace Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// Turns an argument vector into a <see cref="ParseOutcome"/>: prints usage and exits <c>0</c> for
/// help mode, reports the error and prints usage and exits <c>1</c> on a usage error, or yields the
/// parsed arguments to run.
/// </summary>
public sealed class CliRequestParser
{
    private readonly TextWriter _out;
    private readonly TextWriter _err;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliRequestParser"/> class.
    /// </summary>
    /// <param name="output">The writer help and usage text are printed to.</param>
    /// <param name="error">The writer usage-error messages are printed to.</param>
    public CliRequestParser(TextWriter output, TextWriter error)
    {
        _out = output;
        _err = error;
    }

    /// <summary>
    /// Parses the argument vector into a <see cref="ParseOutcome"/>.
    /// </summary>
    /// <param name="args">The argument vector.</param>
    /// <returns>The parse outcome.</returns>
    public ParseOutcome Parse(string[] args)
    {
        try
        {
            CliArguments parsed = CliArgumentsParser.Parse(args);
            if (parsed.Mode == CliMode.Help)
            {
                _out.WriteLine(Main.Usage());
                return ParseOutcome.Exit(0);
            }

            return ParseOutcome.Ok(parsed);
        }
        catch (ArgumentException ex)
        {
            _err.WriteLine(ex.Message);
            _out.WriteLine(Main.Usage());
            return ParseOutcome.Exit(1);
        }
    }
}
