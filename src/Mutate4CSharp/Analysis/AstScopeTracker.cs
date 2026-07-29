namespace Microsoft.Mutate4CSharp.Analysis;

using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// The faithful port of mutate4java's <c>AstScopeTracker</c>, widened to the C#-specific manifest
/// scope-kind taxonomy (deliberate departure DD1). As the scanner walks the tree it brackets each
/// scope-defining declaration — <see cref="TryEnter"/> before the children, <see cref="Exit"/> after —
/// so the innermost currently-open scope (<see cref="CurrentScope"/>) is the enclosing scope for any
/// mutation site the factory builds while inside it. This ambient push/pop model reproduces Java's
/// per-site <c>currentScope(path)</c> walk-up without threading a path through the site factory.
/// </summary>
/// <remarks>
/// <para>
/// A scope id is <c>"&lt;kind&gt;:&lt;prefix&gt;#&lt;detail&gt;:&lt;startLine&gt;"</c>. The
/// <c>prefix</c> is the enclosing <em>type</em>-name stack (outer&#8594;inner, including the type
/// itself), joined by <c>.</c>; members never push onto the prefix. Two stacks are therefore
/// maintained: the type-name stack drives the prefix, and the scope stack drives
/// <see cref="CurrentScope"/> (both types and members push onto it).
/// </para>
/// <para>
/// Type kinds (push the prefix): <c>class struct record record-struct interface enum delegate</c>.
/// Member kinds (scopes, no prefix push): <c>method constructor finalizer operator conversion-operator
/// local-function property accessor indexer field event enum-member</c>. Namespaces, lambdas /
/// anonymous methods, local variables, parameters, and statement / using / attribute nodes are never
/// scopes; a mutation site outside every declaration falls back to <c>file:&lt;filename&gt;</c>.
/// </para>
/// </remarks>
public sealed class AstScopeTracker
{
    private readonly string _file;
    private readonly SyntaxTree _tree;
    private readonly MutationScopeFactory _scopeFactory;
    private readonly List<MutationScope> _scopes = [];
    private readonly List<string> _typeNames = [];
    private readonly Stack<ScopeFrame> _openScopes = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AstScopeTracker"/> class.
    /// </summary>
    /// <param name="file">The source file path, used for the <c>file:</c> fallback scope.</param>
    /// <param name="tree">The parsed syntax tree, used for line-number mapping.</param>
    public AstScopeTracker(string file, SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(tree);
        _file = file;
        _tree = tree;
        _scopeFactory = new MutationScopeFactory(tree);
    }

    /// <summary>
    /// Gets the discovered scopes in first-seen (visit) order; the catalog sorts them by id.
    /// </summary>
    public IReadOnlyList<MutationScope> Scopes => _scopes;

    /// <summary>
    /// Gets a reference to the innermost currently-open scope, or the <c>file:</c> fallback when the walk
    /// is outside every declaration (for example, top-level statements).
    /// </summary>
    public ScopeRef CurrentScope => _openScopes.Count > 0
        ? ScopeRef.From(_openScopes.Peek().Scope)
        : new ScopeRef("file:" + Path.GetFileName(_file), "file", 1, FileEndLine());

    /// <summary>
    /// Enters the given node as a scope when it is a scope-defining declaration, recording the scope and
    /// pushing it (and, for a type, its name) so it becomes the current scope for the node's subtree.
    /// </summary>
    /// <param name="node">The node being visited.</param>
    /// <returns><see langword="true"/> when a scope was entered and a matching <see cref="Exit"/> is due.</returns>
    public bool TryEnter(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (Classify(node) is not { } classification)
        {
            return false;
        }

        bool pushedTypeName = classification.TypeName is not null;
        if (pushedTypeName)
        {
            _typeNames.Add(classification.TypeName!);
        }

        string id = BuildId(classification.Kind, classification.Detail, node);
        MutationScope scope = _scopeFactory.Create(id, classification.Kind, node);
        AddScope(scope);
        _openScopes.Push(new ScopeFrame(scope, pushedTypeName));
        return true;
    }

    /// <summary>
    /// Exits the scope most recently entered by <see cref="TryEnter"/>, restoring the enclosing scope.
    /// </summary>
    public void Exit()
    {
        ScopeFrame frame = _openScopes.Pop();
        if (frame.PushedTypeName)
        {
            _typeNames.RemoveAt(_typeNames.Count - 1);
        }
    }

    private static string MethodLikeDetail(string name, int parameterCount) =>
        name + "(" + parameterCount.ToString(CultureInfo.InvariantCulture) + ")";

    private static string MethodDetail(MethodDeclarationSyntax method) =>
        MethodLikeDetail(method.Identifier.Text, method.ParameterList.Parameters.Count);

    private static string LocalFunctionDetail(LocalFunctionStatementSyntax localFunction) =>
        MethodLikeDetail(localFunction.Identifier.Text, localFunction.ParameterList.Parameters.Count);

    private static string ConstructorDetail(ConstructorDeclarationSyntax constructor) =>
        constructor.Modifiers.Any(SyntaxKind.StaticKeyword)
            ? "cctor(0)"
            : MethodLikeDetail("ctor", constructor.ParameterList.Parameters.Count);

    private static string OperatorDetail(OperatorDeclarationSyntax @operator) =>
        MethodLikeDetail("operator" + @operator.OperatorToken.Text, @operator.ParameterList.Parameters.Count);

    private static string ConversionDetail(ConversionOperatorDeclarationSyntax conversion) =>
        conversion.ImplicitOrExplicitKeyword.Text + " " + conversion.Type + "(1)";

    private static string IndexerDetail(IndexerDeclarationSyntax indexer) =>
        "this[](" + indexer.ParameterList.Parameters.Count.ToString(CultureInfo.InvariantCulture) + ")";

    private static Classification? AccessorClassification(AccessorDeclarationSyntax accessor)
    {
        // Per DD1, an accessor is a scope only when it has a body (a block or an expression body);
        // auto-property accessors (get; set; init;) are not scopes.
        if (accessor.Body is null && accessor.ExpressionBody is null)
        {
            return null;
        }

        string owner = accessor.Parent?.Parent switch
        {
            PropertyDeclarationSyntax property => property.Identifier.Text,
            IndexerDeclarationSyntax => "this[]",
            EventDeclarationSyntax @event => @event.Identifier.Text,
            _ => string.Empty,
        };
        return Classification.Member("accessor", owner + "." + accessor.Keyword.Text);
    }

    private static Classification? VariableClassification(VariableDeclaratorSyntax declarator)
    {
        // A VariableDeclarator is a field/event scope only when its parent chain is
        // VariableDeclarator -> VariableDeclaration -> FieldDeclaration/EventFieldDeclaration; local
        // declarators (locals, using/fixed statements) share the same node shape but are never scopes.
        if (declarator.Parent is not VariableDeclarationSyntax declaration)
        {
            return null;
        }

        return declaration.Parent switch
        {
            FieldDeclarationSyntax => Classification.Member("field", declarator.Identifier.Text),
            EventFieldDeclarationSyntax => Classification.Member("event", declarator.Identifier.Text),
            _ => null,
        };
    }

    private Classification? Classify(SyntaxNode node) => node switch
    {
        ClassDeclarationSyntax type => Classification.Type("class", type.Identifier.Text),
        StructDeclarationSyntax type => Classification.Type("struct", type.Identifier.Text),
        InterfaceDeclarationSyntax type => Classification.Type("interface", type.Identifier.Text),
        RecordDeclarationSyntax type => Classification.Type(
            type.IsKind(SyntaxKind.RecordStructDeclaration) ? "record-struct" : "record",
            type.Identifier.Text),
        EnumDeclarationSyntax type => Classification.Type("enum", type.Identifier.Text),
        DelegateDeclarationSyntax type => Classification.Type("delegate", type.Identifier.Text),
        MethodDeclarationSyntax method => Classification.Member("method", MethodDetail(method)),
        ConstructorDeclarationSyntax constructor => Classification.Member("constructor", ConstructorDetail(constructor)),
        DestructorDeclarationSyntax => Classification.Member("finalizer", "finalizer(0)"),
        OperatorDeclarationSyntax @operator => Classification.Member("operator", OperatorDetail(@operator)),
        ConversionOperatorDeclarationSyntax conversion => Classification.Member("conversion-operator", ConversionDetail(conversion)),
        LocalFunctionStatementSyntax localFunction => Classification.Member("local-function", LocalFunctionDetail(localFunction)),
        PropertyDeclarationSyntax property => Classification.Member("property", property.Identifier.Text),
        IndexerDeclarationSyntax indexer => Classification.Member("indexer", IndexerDetail(indexer)),
        EventDeclarationSyntax @event => Classification.Member("event", @event.Identifier.Text),
        EnumMemberDeclarationSyntax enumMember => Classification.Member("enum-member", enumMember.Identifier.Text),
        AccessorDeclarationSyntax accessor => AccessorClassification(accessor),
        VariableDeclaratorSyntax declarator => VariableClassification(declarator),
        _ => null,
    };

    private void AddScope(MutationScope scope)
    {
        if (!_scopes.Any(existing => existing.Id == scope.Id))
        {
            _scopes.Add(scope);
        }
    }

    private string BuildId(string kind, string detail, SyntaxNode node)
    {
        int startLine = _tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
        string prefix = string.Join(".", _typeNames);
        if (prefix.Length > 0)
        {
            prefix += "#";
        }

        return kind + ":" + prefix + detail + ":" + startLine.ToString(CultureInfo.InvariantCulture);
    }

    private int FileEndLine() =>
        _tree.GetLineSpan(_tree.GetRoot().FullSpan).EndLinePosition.Line + 1;

    private readonly record struct ScopeFrame(MutationScope Scope, bool PushedTypeName);

    private readonly record struct Classification(string Kind, string Detail, string? TypeName)
    {
        public static Classification Type(string kind, string name) => new(kind, name, name);

        public static Classification Member(string kind, string detail) => new(kind, detail, null);
    }
}
