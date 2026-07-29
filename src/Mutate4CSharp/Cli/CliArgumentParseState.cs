namespace Microsoft.Mutate4CSharp.Cli;

using System.Collections.ObjectModel;

/// <summary>
/// The mutable accumulator the parser fills in while scanning the argument vector, and which
/// produces the final <see cref="CliArguments"/> once parsing succeeds.
/// </summary>
public sealed class CliArgumentParseState
{
    /// <summary>Gets or sets a value indicating whether help mode was requested.</summary>
    public bool Help { get; set; }

    /// <summary>Gets or sets a value indicating whether verbose output was requested.</summary>
    public bool Verbose { get; set; }

    /// <summary>Gets or sets the selected source lines (empty means all).</summary>
    public IReadOnlySet<int> Lines { get; set; } = new HashSet<int>();

    /// <summary>Gets or sets a value indicating whether scan mode was requested.</summary>
    public bool Scan { get; set; }

    /// <summary>Gets or sets a value indicating whether manifest-update mode was requested.</summary>
    public bool UpdateManifest { get; set; }

    /// <summary>Gets or sets a value indicating whether existing coverage should be reused.</summary>
    public bool ReuseCoverage { get; set; }

    /// <summary>Gets or sets a value indicating whether differential-since-last-run mode was requested.</summary>
    public bool SinceLastRun { get; set; }

    /// <summary>Gets or sets a value indicating whether all covered sites should be mutated.</summary>
    public bool MutateAll { get; set; }

    /// <summary>Gets or sets the mutant timeout multiplier.</summary>
    public int TimeoutFactor { get; set; } = CliArgumentsParserDefaults.DefaultTimeoutFactor;

    /// <summary>Gets or sets the mutation-count warning threshold.</summary>
    public int MutationWarning { get; set; } = CliArgumentsParserDefaults.DefaultMutationWarning;

    /// <summary>Gets or sets the maximum parallel worker count.</summary>
    public int MaxWorkers { get; set; } = CliArgumentsParserDefaults.DefaultMaxWorkers;

    /// <summary>Gets or sets the overriding test command, or <see langword="null"/> for the default.</summary>
    public string? TestCommand { get; set; }

    /// <summary>Gets the accumulated non-flag file arguments.</summary>
    public Collection<string> Values { get; } = new();

    /// <summary>
    /// Builds the arguments for help mode, ignoring every non-help option.
    /// </summary>
    /// <returns>The help-mode arguments.</returns>
    public CliArguments HelpArguments()
    {
        return new CliArguments(CliMode.Help, [], new HashSet<int>(), false, false, false, false, false, TimeoutFactor, MutationWarning, MaxWorkers, null, Verbose);
    }

    /// <summary>
    /// Builds the arguments for explicit-file mode from the accumulated state.
    /// </summary>
    /// <returns>The explicit-file arguments.</returns>
    public CliArguments ToCliArguments()
    {
        return new CliArguments(
            CliMode.ExplicitFiles,
            [.. Values],
            Lines,
            Scan,
            UpdateManifest,
            ReuseCoverage,
            SinceLastRun,
            MutateAll,
            TimeoutFactor,
            MutationWarning,
            MaxWorkers,
            TestCommand,
            Verbose);
    }
}
