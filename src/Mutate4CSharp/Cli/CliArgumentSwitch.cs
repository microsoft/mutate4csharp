namespace Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// Dispatches a single argument to the correct handler: mode flags, selection flags, value flags,
/// or a bare file argument. Returns the index the outer loop should resume from (advanced past a
/// consumed flag value where applicable).
/// </summary>
public static class CliArgumentSwitch
{
    /// <summary>
    /// Parses the argument at <paramref name="index"/>, mutating <paramref name="state"/>.
    /// </summary>
    /// <param name="args">The full argument vector.</param>
    /// <param name="index">The index of the argument to parse.</param>
    /// <param name="state">The accumulator to mutate.</param>
    /// <param name="validators">The validators used for value flags.</param>
    /// <returns>The index the outer loop should resume from.</returns>
    public static int Parse(string[] args, int index, CliArgumentParseState state, CliArgumentValidators validators)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(state);
        string arg = args[index];
        int? parsedIndex = ParseModeFlag(arg, index, state);
        if (parsedIndex is not null)
        {
            return parsedIndex.Value;
        }

        parsedIndex = ParseSelectionFlag(arg, index, state);
        if (parsedIndex is not null)
        {
            return parsedIndex.Value;
        }

        parsedIndex = ParseValueFlag(args, index, state, validators, arg);
        if (parsedIndex is not null)
        {
            return parsedIndex.Value;
        }

        return AddFileArgument(arg, index, state);
    }

    private static int? ParseModeFlag(string arg, int index, CliArgumentParseState state)
    {
        return arg switch
        {
            "--help" => Set(index, () => state.Help = true),
            "--verbose" => Set(index, () => state.Verbose = true),
            _ => null,
        };
    }

    private static int? ParseSelectionFlag(string arg, int index, CliArgumentParseState state)
    {
        return arg switch
        {
            "--scan" => Set(index, () => state.Scan = true),
            "--update-manifest" => Set(index, () => state.UpdateManifest = true),
            "--reuse-coverage" => Set(index, () => state.ReuseCoverage = true),
            "--since-last-run" => Set(index, () => state.SinceLastRun = true),
            "--mutate-all" => Set(index, () => state.MutateAll = true),
            _ => null,
        };
    }

    private static int? ParseValueFlag(string[] args, int index, CliArgumentParseState state, CliArgumentValidators validators, string arg)
    {
        return arg switch
        {
            "--lines" => ParseFlagValue(args, index, "--lines", value => state.Lines = validators.ParseLines(value), validators),
            "--timeout-factor" => ParseFlagValue(args, index, "--timeout-factor", value => state.TimeoutFactor = validators.ParsePositiveInt(value, "--timeout-factor"), validators),
            "--mutation-warning" => ParseFlagValue(args, index, "--mutation-warning", value => state.MutationWarning = validators.ParsePositiveInt(value, "--mutation-warning"), validators),
            "--max-workers" => ParseFlagValue(args, index, "--max-workers", value => state.MaxWorkers = validators.ParsePositiveInt(value, "--max-workers"), validators),
            "--test-command" => ParseFlagValue(args, index, "--test-command", value => SetTestCommand(state, value), validators),
            _ => null,
        };
    }

    private static int ParseFlagValue(string[] args, int index, string flag, Action<string> consumer, CliArgumentValidators validators)
    {
        int valueIndex = index + 1;
        validators.EnsureHasValue(args, valueIndex, flag);
        consumer(args[valueIndex]);
        return valueIndex;
    }

    private static int AddFileArgument(string arg, int index, CliArgumentParseState state)
    {
        if (arg.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Unknown option: " + arg);
        }

        state.Values.Add(arg);
        return index;
    }

    private static int Set(int index, Action action)
    {
        action();
        return index;
    }

    private static void SetTestCommand(CliArgumentParseState state, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("--test-command must not be blank");
        }

        state.TestCommand = value;
    }
}
