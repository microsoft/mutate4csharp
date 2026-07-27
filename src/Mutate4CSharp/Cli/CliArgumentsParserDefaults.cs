namespace Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// The default option values applied by the command-line parser before any overrides.
/// </summary>
public static class CliArgumentsParserDefaults
{
    /// <summary>The default mutant timeout multiplier.</summary>
    public const int DefaultTimeoutFactor = 10;

    /// <summary>The default mutation-count warning threshold.</summary>
    public const int DefaultMutationWarning = 50;

    /// <summary>The default maximum parallel worker count: <c>max(1, ProcessorCount / 2)</c>.</summary>
    public static readonly int DefaultMaxWorkers = Math.Max(1, Environment.ProcessorCount / 2);
}
