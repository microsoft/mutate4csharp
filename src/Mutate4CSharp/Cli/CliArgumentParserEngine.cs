namespace Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// The core argument-parsing loop: scans the argument vector into a
/// <see cref="CliArgumentParseState"/>, then validates and materializes the
/// <see cref="CliArguments"/>.
/// </summary>
public sealed class CliArgumentParserEngine
{
    private readonly CliArgumentValidators _validators;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliArgumentParserEngine"/> class.
    /// </summary>
    /// <param name="validators">The validators used during parsing.</param>
    public CliArgumentParserEngine(CliArgumentValidators validators)
    {
        _validators = validators;
    }

    /// <summary>
    /// Parses the argument vector into a validated <see cref="CliArguments"/>.
    /// </summary>
    /// <param name="args">The argument vector.</param>
    /// <returns>The parsed arguments.</returns>
    public CliArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        CliArgumentParseState state = new();
        for (int i = 0; i < args.Length; i++)
        {
            i = CliArgumentSwitch.Parse(args, i, state, _validators);
        }

        if (state.Help)
        {
            return state.HelpArguments();
        }

        _validators.ValidateSelectionFlags(state);
        _validators.EnsureExactlyOneCSharpFile(state.Values);
        return state.ToCliArguments();
    }
}
