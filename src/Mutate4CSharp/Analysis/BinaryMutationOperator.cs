namespace Microsoft.Mutate4CSharp.Analysis;

using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// The faithful port of mutate4java's <c>BinaryMutationOperator</c>: the original operator text, its
/// replacement text, and whether the mutation is numeric-only (guarded so string concatenation is not
/// mutated). <see cref="ForKind"/> maps a Roslyn <see cref="SyntaxKind"/> to the operator, mirroring the
/// Java <c>forKind(Tree.Kind)</c> switch one-for-one; unmapped kinds yield <see langword="null"/>.
/// </summary>
/// <param name="Original">The original operator source text (for example <c>==</c>).</param>
/// <param name="Replacement">The replacement operator source text (for example <c>!=</c>).</param>
/// <param name="NumericOnly">Whether the mutation applies only to numeric operands.</param>
public sealed record BinaryMutationOperator(string Original, string Replacement, bool NumericOnly)
{
    /// <summary>
    /// Returns the mutation operator for the given binary-expression kind, or <see langword="null"/>
    /// when the kind is not a mutated operator.
    /// </summary>
    /// <param name="kind">The Roslyn binary-expression syntax kind.</param>
    /// <returns>The mutation operator, or <see langword="null"/>.</returns>
    public static BinaryMutationOperator? ForKind(SyntaxKind kind) => kind switch
    {
        SyntaxKind.AddExpression => new BinaryMutationOperator("+", "-", true),
        SyntaxKind.SubtractExpression => new BinaryMutationOperator("-", "+", false),
        SyntaxKind.MultiplyExpression => new BinaryMutationOperator("*", "/", false),
        SyntaxKind.DivideExpression => new BinaryMutationOperator("/", "*", false),
        SyntaxKind.LogicalAndExpression => new BinaryMutationOperator("&&", "||", false),
        SyntaxKind.LogicalOrExpression => new BinaryMutationOperator("||", "&&", false),
        SyntaxKind.EqualsExpression => new BinaryMutationOperator("==", "!=", false),
        SyntaxKind.NotEqualsExpression => new BinaryMutationOperator("!=", "==", false),
        SyntaxKind.GreaterThanExpression => new BinaryMutationOperator(">", ">=", false),
        SyntaxKind.GreaterThanOrEqualExpression => new BinaryMutationOperator(">=", ">", false),
        SyntaxKind.LessThanExpression => new BinaryMutationOperator("<", "<=", false),
        SyntaxKind.LessThanOrEqualExpression => new BinaryMutationOperator("<=", "<", false),
        _ => null,
    };
}
