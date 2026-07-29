namespace Microsoft.Mutate4CSharp.Analysis;

using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// The faithful port of mutate4java's <c>AstMutationSiteFactory</c>: builds a <see cref="MutationSite"/>
/// from a candidate expression, or returns <see langword="null"/> when the expression is not a mutation
/// site. It reproduces the Java factory's per-shape logic (boolean/integer literals, the binary and unary
/// operators, and reference-valued null replacement) and the exact replacement-description strings.
/// </summary>
/// <remarks>
/// Where Java located operator text with a substring scan between the operands, Roslyn exposes the
/// operator token span directly, so this reads <see cref="BinaryExpressionSyntax.OperatorToken"/> and
/// <see cref="PrefixUnaryExpressionSyntax.OperatorToken"/> spans — cleaner, same result. As in Java,
/// each site's enclosing scope is resolved through the injected <see cref="AstScopeTracker"/>: the
/// single <c>BuildSite</c> construction point stamps every site with the tracker's current scope
/// (id, kind, and line range).
/// </remarks>
public sealed class AstMutationSiteFactory
{
    private readonly string _file;
    private readonly SyntaxTree _tree;
    private readonly TreeTypePredicates _typePredicates;
    private readonly AstScopeTracker _scopeTracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="AstMutationSiteFactory"/> class.
    /// </summary>
    /// <param name="file">The source file path recorded on each emitted site.</param>
    /// <param name="tree">The parsed syntax tree, used for line-number mapping.</param>
    /// <param name="semanticModel">The resolved semantic model backing the type predicates.</param>
    /// <param name="scopeTracker">The scope tracker supplying each site's enclosing scope metadata.</param>
    public AstMutationSiteFactory(string file, SyntaxTree tree, SemanticModel semanticModel, AstScopeTracker scopeTracker)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(semanticModel);
        ArgumentNullException.ThrowIfNull(scopeTracker);
        _file = file;
        _tree = tree;
        _typePredicates = new TreeTypePredicates(semanticModel);
        _scopeTracker = scopeTracker;
    }

    /// <summary>
    /// Builds the mutation site for a boolean literal (<c>true</c>&#8596;<c>false</c>) or an integer
    /// constant (<c>0</c>&#8596;<c>1</c>), or returns <see langword="null"/> for any other literal.
    /// </summary>
    /// <param name="node">The literal expression.</param>
    /// <returns>The mutation site, or <see langword="null"/>.</returns>
    public MutationSite? Literal(LiteralExpressionSyntax node)
    {
        ArgumentNullException.ThrowIfNull(node);
        object? value = node.Token.Value;
        if (value is bool boolean)
        {
            return LiteralSite(node, boolean ? "true" : "false", boolean ? "false" : "true");
        }

        if (value is int number && (number == 0 || number == 1))
        {
            return LiteralSite(node, number.ToString(CultureInfo.InvariantCulture), number == 0 ? "1" : "0");
        }

        return null;
    }

    /// <summary>
    /// Builds the mutation site for a mutated binary operator, or returns <see langword="null"/> when the
    /// operator is not mutated (or is numeric-only and the operand type is not numeric — the guard that
    /// excludes string concatenation from the <c>+</c> mutation).
    /// </summary>
    /// <param name="node">The binary expression.</param>
    /// <returns>The mutation site, or <see langword="null"/>.</returns>
    public MutationSite? Binary(BinaryExpressionSyntax node)
    {
        ArgumentNullException.ThrowIfNull(node);
        BinaryMutationOperator? operation = BinaryMutationOperator.ForKind(node.Kind());
        if (operation is null || (operation.NumericOnly && !_typePredicates.IsNumeric(node)))
        {
            return null;
        }

        SyntaxToken token = node.OperatorToken;
        return Site(token.SpanStart, token.Span.End, operation.Original, operation.Replacement);
    }

    /// <summary>
    /// Builds the removal mutation site for a logical-complement (<c>!expr</c>&#8594;<c>expr</c>) or a
    /// numeric unary-minus (<c>-expr</c>&#8594;<c>expr</c>), or returns <see langword="null"/> otherwise.
    /// </summary>
    /// <param name="node">The prefix-unary expression.</param>
    /// <returns>The mutation site, or <see langword="null"/>.</returns>
    public MutationSite? Unary(PrefixUnaryExpressionSyntax node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Kind() switch
        {
            SyntaxKind.LogicalNotExpression => RemovablePrefix(node, "!"),
            SyntaxKind.UnaryMinusExpression => _typePredicates.IsNumeric(node) ? RemovablePrefix(node, "-") : null,
            _ => null,
        };
    }

    /// <summary>
    /// Builds the null-replacement mutation site for a reference-valued expression, or returns
    /// <see langword="null"/> when the expression is absent, is not a reference type, or is already
    /// <c>null</c>.
    /// </summary>
    /// <param name="expression">The candidate right-value expression.</param>
    /// <returns>The mutation site, or <see langword="null"/>.</returns>
    public MutationSite? NullReplacement(ExpressionSyntax? expression)
    {
        if (expression is null || !_typePredicates.IsReference(expression))
        {
            return null;
        }

        string original = expression.ToString();
        return "null".Equals(original, StringComparison.Ordinal)
            ? null
            : Site(expression.Span.Start, expression.Span.End, original, "null");
    }

    private MutationSite LiteralSite(LiteralExpressionSyntax node, string original, string replacement)
    {
        int start = node.Token.SpanStart;
        return Site(start, start + original.Length, original, replacement);
    }

    private MutationSite RemovablePrefix(PrefixUnaryExpressionSyntax node, string @operator)
    {
        SyntaxToken token = node.OperatorToken;
        string description = "replace " + @operator + " with removed " + @operator;
        return BuildSite(token.SpanStart, token.Span.End, @operator, string.Empty, description);
    }

    private MutationSite Site(int start, int end, string original, string replacement) =>
        BuildSite(start, end, original, replacement, "replace " + original + " with " + replacement);

    private MutationSite BuildSite(int start, int end, string original, string replacement, string description)
    {
        int lineNumber = _tree.GetLineSpan(TextSpan.FromBounds(start, start)).StartLinePosition.Line + 1;
        ScopeRef scope = _scopeTracker.CurrentScope;
        return new MutationSite(_file, lineNumber, start, end, original, replacement, description, scope.Id, scope.Kind, scope.StartLine, scope.EndLine);
    }
}
