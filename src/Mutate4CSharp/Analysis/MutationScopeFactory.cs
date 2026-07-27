namespace Microsoft.Mutate4CSharp.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.Mutate4CSharp.Manifest;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// The faithful port of mutate4java's <c>MutationScopeFactory</c>: builds a <see cref="MutationScope"/>
/// (identifier, kind, line range, semantic hash) from a declaration node. Where the Java factory read
/// node positions through <c>Trees.getSourcePositions()</c> and mapped them with a hand-rolled
/// <c>LineNumberTable</c>, Roslyn exposes both intrinsically: line numbers come from
/// <see cref="SyntaxTree.GetLineSpan"/> (the same 1-based mapping the site factory uses, so site and
/// scope lines share one source of truth) and the node's own source text comes from its string form
/// over its <see cref="SyntaxNode.Span"/>. The <c>LineNumberTable</c> collaborator is therefore
/// dropped as redundant.
/// </summary>
public sealed class MutationScopeFactory
{
    private readonly SyntaxTree _tree;
    private readonly ManifestSupport _manifestSupport = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MutationScopeFactory"/> class.
    /// </summary>
    /// <param name="tree">The parsed syntax tree, used for line-number mapping.</param>
    public MutationScopeFactory(SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        _tree = tree;
    }

    /// <summary>
    /// Builds the <see cref="MutationScope"/> for the given declaration node: the supplied identifier and
    /// kind, the node's 1-based start/end lines, and the SHA-256 hex digest of the node's source text.
    /// </summary>
    /// <param name="id">The scope identifier.</param>
    /// <param name="kind">The scope kind.</param>
    /// <param name="node">The declaration node the scope describes.</param>
    /// <returns>The mutation scope.</returns>
    public MutationScope Create(string id, string kind, SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(node);
        FileLinePositionSpan span = _tree.GetLineSpan(node.Span);
        int startLine = span.StartLinePosition.Line + 1;
        int endLine = span.EndLinePosition.Line + 1;
        return new MutationScope(id, kind, startLine, endLine, _manifestSupport.Hash(node.ToString()));
    }
}
