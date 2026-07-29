namespace Microsoft.Mutate4CSharp.Model;

using System.Globalization;

/// <summary>
/// A single mutation site: the source location and text span to replace, the replacement text, a
/// human-readable description, and the enclosing scope metadata.
/// </summary>
/// <param name="File">The source file path.</param>
/// <param name="LineNumber">The 1-based line number of the site.</param>
/// <param name="Start">The inclusive start offset of the span.</param>
/// <param name="End">The exclusive end offset of the span.</param>
/// <param name="OriginalText">The original source text at the span.</param>
/// <param name="ReplacementText">The replacement source text.</param>
/// <param name="Description">A human-readable description of the mutation.</param>
/// <param name="ScopeId">The enclosing scope identifier.</param>
/// <param name="ScopeKind">The enclosing scope kind.</param>
/// <param name="ScopeStartLine">The 1-based start line of the enclosing scope.</param>
/// <param name="ScopeEndLine">The 1-based end line of the enclosing scope.</param>
public sealed record MutationSite(
    string File,
    int LineNumber,
    int Start,
    int End,
    string OriginalText,
    string ReplacementText,
    string Description,
    string ScopeId,
    string ScopeKind,
    int ScopeStartLine,
    int ScopeEndLine)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MutationSite"/> class without explicit scope
    /// metadata, defaulting the scope to the site's own line.
    /// </summary>
    /// <param name="file">The source file path.</param>
    /// <param name="lineNumber">The 1-based line number of the site.</param>
    /// <param name="start">The inclusive start offset of the span.</param>
    /// <param name="end">The exclusive end offset of the span.</param>
    /// <param name="originalText">The original source text at the span.</param>
    /// <param name="replacementText">The replacement source text.</param>
    /// <param name="description">A human-readable description of the mutation.</param>
    public MutationSite(
        string file,
        int lineNumber,
        int start,
        int end,
        string originalText,
        string replacementText,
        string description)
        : this(
            file,
            lineNumber,
            start,
            end,
            originalText,
            replacementText,
            description,
            "scope:" + lineNumber.ToString(CultureInfo.InvariantCulture),
            "unknown",
            lineNumber,
            lineNumber)
    {
    }
}
