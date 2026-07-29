namespace Microsoft.Mutate4CSharp.Analysis;

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// The faithful port of mutate4java's <c>AstMutationScanner</c>: a syntax walker that visits the source
/// tree in source order and appends every discovered <see cref="MutationSite"/> to the collector. It
/// mirrors the Java scanner's visited shapes — boolean/integer literals, binary and prefix-unary
/// operators, and the reference-valued null-replacement contexts: a block <c>return</c>, a variable
/// initializer, and a <em>simple</em> assignment right-value (compound assignments such as <c>+=</c> or
/// <c>??=</c> are javac's separate <c>CompoundAssignmentTree</c> and are never null-replaced; call
/// arguments are never sites either). It additionally covers a C#-specific null-replacement context that
/// has no Java analog — the value-returning expression-bodied (<c>=&gt; expr</c>) member body (DD4).
/// </summary>
/// <remarks>
/// The walk also drives the <see cref="AstScopeTracker"/>: the <see cref="Visit(SyntaxNode)"/> override
/// enters each scope-defining declaration before its subtree and exits after, so every site the factory
/// builds is stamped with its enclosing DD1 scope.
/// </remarks>
public sealed class AstMutationScanner : CSharpSyntaxWalker
{
    private readonly List<MutationSite> _sites = [];
    private readonly AstMutationSiteFactory _siteFactory;
    private readonly AstScopeTracker _scopeTracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="AstMutationScanner"/> class.
    /// </summary>
    /// <param name="file">The source file path recorded on each emitted site.</param>
    /// <param name="root">The parsed compilation-unit root to walk.</param>
    /// <param name="semanticModel">The resolved semantic model backing the type predicates.</param>
    public AstMutationScanner(string file, CompilationUnitSyntax root, SemanticModel semanticModel)
    {
        ArgumentNullException.ThrowIfNull(root);
        _scopeTracker = new AstScopeTracker(file, root.SyntaxTree);
        _siteFactory = new AstMutationSiteFactory(file, root.SyntaxTree, semanticModel, _scopeTracker);
    }

    /// <summary>
    /// Gets the mutation sites discovered so far, in source (visit) order.
    /// </summary>
    public IReadOnlyList<MutationSite> Sites => _sites;

    /// <summary>
    /// Gets the mutation scopes discovered so far, in first-seen (visit) order.
    /// </summary>
    public IReadOnlyList<MutationScope> Scopes => _scopeTracker.Scopes;

    /// <summary>
    /// Brackets every scope-defining declaration around the walk of its subtree — entering the scope
    /// before its children are visited and exiting after — so the scope tracker always reflects the
    /// enclosing scope of the site currently being built. This single choke point layers the DD1 scope
    /// taxonomy onto the site-emitting walk without altering the per-shape visit overrides below.
    /// </summary>
    /// <param name="node">The node being visited.</param>
    public override void Visit(SyntaxNode? node)
    {
        bool entered = node is not null && _scopeTracker.TryEnter(node);
        base.Visit(node);
        if (entered)
        {
            _scopeTracker.Exit();
        }
    }

    /// <inheritdoc/>
    public override void VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Add(_siteFactory.Literal(node));
        base.VisitLiteralExpression(node);
    }

    /// <inheritdoc/>
    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Add(_siteFactory.Binary(node));
        base.VisitBinaryExpression(node);
    }

    /// <inheritdoc/>
    public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Add(_siteFactory.Unary(node));
        base.VisitPrefixUnaryExpression(node);
    }

    /// <inheritdoc/>
    public override void VisitReturnStatement(ReturnStatementSyntax node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Add(_siteFactory.NullReplacement(node.Expression));
        base.VisitReturnStatement(node);
    }

    /// <inheritdoc/>
    public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Add(_siteFactory.NullReplacement(node.Initializer?.Value));
        base.VisitVariableDeclarator(node);
    }

    /// <inheritdoc/>
    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            Add(_siteFactory.NullReplacement(node.Right));
        }

        base.VisitAssignmentExpression(node);
    }

    /// <summary>
    /// Null-replaces a value-returning expression-bodied (<c>=&gt; expr</c>) member body — the C#
    /// desugaring of <c>return expr;</c> that has no Java oracle (DD4). The emit fires only when the
    /// arrow clause's parent is a value-returning declaration (an expression-bodied method, property or
    /// indexer implicit getter, explicit <c>get</c> accessor, non-void local function, operator, or
    /// conversion operator); it must not fire for a <c>set</c>/<c>init</c>/<c>add</c>/<c>remove</c>
    /// accessor, a constructor, or a finalizer, whose <c>=&gt;</c> body is a statement rather than a
    /// return. Value-type and <c>void</c> bodies self-exclude through the factory's reference-type gate,
    /// so only the statement-bodied parents are excluded here. The base recursion stays unconditional so
    /// nested literal/operator sites inside every arrow body are still discovered.
    /// </summary>
    /// <param name="node">The arrow expression clause being visited.</param>
    public override void VisitArrowExpressionClause(ArrowExpressionClauseSyntax node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (IsValueReturningArrowBody(node.Parent))
        {
            Add(_siteFactory.NullReplacement(node.Expression));
        }

        base.VisitArrowExpressionClause(node);
    }

    private static bool IsValueReturningArrowBody(SyntaxNode? parent) => parent switch
    {
        MethodDeclarationSyntax => true,
        PropertyDeclarationSyntax => true,
        IndexerDeclarationSyntax => true,
        OperatorDeclarationSyntax => true,
        ConversionOperatorDeclarationSyntax => true,
        LocalFunctionStatementSyntax => true,
        AccessorDeclarationSyntax accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration),
        _ => false,
    };

    private void Add(MutationSite? site)
    {
        if (site is not null)
        {
            _sites.Add(site);
        }
    }
}
