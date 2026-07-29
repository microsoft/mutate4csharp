namespace Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// The usage/help text. Ported faithfully from mutate4java, adapting only the ecosystem tokens
/// (tool name, <c>.cs</c> extension, C# source, Cobertura coverage). Lines are joined with an
/// explicit <c>\n</c> so the output matches Java's LF-normalized text block regardless of the
/// source file's line endings.
/// </summary>
public static class UsageText
{
    private static readonly string[] Lines =
    [
        "Usage:",
        "  mutate4csharp <file.cs>                      Mutate one C# source file",
        "  mutate4csharp <file.cs> --scan              Print mutation-site scan without running tests",
        "  mutate4csharp <file.cs> --update-manifest   Write embedded manifest without running tests",
        "  mutate4csharp <file.cs> --reuse-coverage    Reuse existing Cobertura coverage without refreshing it",
        "  mutate4csharp <file.cs> --lines 12,18       Restrict mutations to specific source lines",
        "  mutate4csharp <file.cs> --since-last-run    Mutate only scopes changed since embedded manifest",
        "  mutate4csharp <file.cs> --mutate-all        Ignore embedded manifest and mutate all covered sites",
        "  mutate4csharp <file.cs> --mutation-warning 50 Warn when selected mutation count exceeds threshold",
        "  mutate4csharp <file.cs> --max-workers 4     Limit parallel worker count",
        "  mutate4csharp <file.cs> --timeout-factor 15 Set mutant timeout as baseline multiplier",
        "  mutate4csharp <file.cs> --test-command CMD  Override the test command used for baseline and mutants",
        "  mutate4csharp <file.cs> --verbose           Print live worker progress",
        "  mutate4csharp --help                        Print this help message",
    ];

    /// <summary>
    /// Returns the usage text.
    /// </summary>
    /// <returns>The usage text, LF-separated with a trailing newline.</returns>
    public static string Text()
    {
        return string.Join("\n", Lines) + "\n";
    }
}
