namespace Microsoft.Mutate4CSharp.Model;

/// <summary>
/// A single source line identified by its file path and 1-based line number, used as a
/// coverage-lookup key.
/// </summary>
/// <param name="SourcePath">The source file path.</param>
/// <param name="LineNumber">The 1-based line number.</param>
public readonly record struct CoverageSite(string SourcePath, int LineNumber);
