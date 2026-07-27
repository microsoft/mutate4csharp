namespace Microsoft.Mutate4CSharp;

/// <summary>
/// The high-level mode selected by the command-line arguments.
/// </summary>
public enum CliMode
{
    /// <summary>Mutate the explicitly named source file.</summary>
    ExplicitFiles,

    /// <summary>Print the help message and exit.</summary>
    Help,
}
