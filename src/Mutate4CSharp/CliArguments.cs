namespace Microsoft.Mutate4CSharp;

/// <summary>
/// The fully parsed command-line request: the selected mode, the target file arguments, and every
/// resolved option value or flag.
/// </summary>
/// <param name="Mode">The high-level CLI mode.</param>
/// <param name="FileArgs">The explicit file arguments.</param>
/// <param name="Lines">The source lines to restrict mutation to (empty means all).</param>
/// <param name="Scan">Whether to print a mutation-site scan without running tests.</param>
/// <param name="UpdateManifest">Whether to write the embedded manifest without running tests.</param>
/// <param name="ReuseCoverage">Whether to reuse existing coverage without refreshing it.</param>
/// <param name="SinceLastRun">Whether to mutate only scopes changed since the embedded manifest.</param>
/// <param name="MutateAll">Whether to ignore the embedded manifest and mutate all covered sites.</param>
/// <param name="TimeoutFactor">The mutant timeout expressed as a baseline multiplier.</param>
/// <param name="MutationWarning">The mutation-count threshold above which a warning is printed.</param>
/// <param name="MaxWorkers">The maximum parallel worker count.</param>
/// <param name="TestCommand">The overriding test command, or <see langword="null"/> for the default.</param>
/// <param name="Verbose">Whether to print live worker progress.</param>
public sealed record CliArguments(
    CliMode Mode,
    IReadOnlyList<string> FileArgs,
    IReadOnlySet<int> Lines,
    bool Scan,
    bool UpdateManifest,
    bool ReuseCoverage,
    bool SinceLastRun,
    bool MutateAll,
    int TimeoutFactor,
    int MutationWarning,
    int MaxWorkers,
    string? TestCommand,
    bool Verbose);
