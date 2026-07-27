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
/// operators, and the three reference-valued null-replacement contexts (return, variable initializer,
/// and assignment right-value; never call arguments).
/// </summary>
/// <remarks>
/// The Java scanner also drove the scope tracker (entering/exiting types and recording member scopes) as
/// it walked; that scope wiring is owned by the scope-tracker task and layered on top of this
/// site-emitting walk.
/// </remarks>
public sealed class AstMutationScanner : CSharpSyntaxWalker
{
    private readonly List<MutationSite> _sites = [];
    private readonly AstMutationSiteFactory _siteFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AstMutationScanner"/> class.
    /// </summary>
    /// <param name="file">The source file path recorded on each emitted site.</param>
    /// <param name="root">The parsed compilation-unit root to walk.</param>
    /// <param name="semanticModel">The resolved semantic model backing the type predicates.</param>
    public AstMutationScanner(string file, CompilationUnitSyntax root, SemanticModel semanticModel)
    {
        ArgumentNullException.ThrowIfNull(root);
        _siteFactory = new AstMutationSiteFactory(file, root.SyntaxTree, semanticModel);
    }

    /// <summary>
    /// Gets the mutation sites discovered so far, in source (visit) order.
    /// </summary>
    public IReadOnlyList<MutationSite> Sites => _sites;

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
        Add(_siteFactory.NullReplacement(node.Right));
        base.VisitAssignmentExpression(node);
    }

    private void Add(MutationSite? site)
    {
        if (site is not null)
        {
            _sites.Add(site);
        }
    }
}
