namespace Microsoft.Mutate4CSharp.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// The faithful port of mutate4java's <c>TreeTypePredicates</c>: the two semantic questions the
/// mutation-site factory asks about an expression's static type. Where the Java tool queried the JDK
/// <c>Trees.getTypeMirror(path)</c>, this queries Roslyn's <c>SemanticModel.GetTypeInfo</c> and reads the
/// expression's own <c>Type</c> (not <c>ConvertedType</c>), matching <c>getTypeMirror</c>'s
/// pre-implicit-conversion static type.
/// </summary>
public sealed class TreeTypePredicates
{
    private readonly SemanticModel _semanticModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="TreeTypePredicates"/> class.
    /// </summary>
    /// <param name="semanticModel">The resolved semantic model used to classify expression types.</param>
    public TreeTypePredicates(SemanticModel semanticModel)
    {
        ArgumentNullException.ThrowIfNull(semanticModel);
        _semanticModel = semanticModel;
    }

    /// <summary>
    /// Determines whether the expression's static type is a primitive numeric type — the analog of
    /// Java's <c>type.getKind().isPrimitive() &amp;&amp; kind != BOOLEAN</c>. Value types that are not
    /// one of the built-in numeric special types (structs, enums, <c>bool</c>) are not numeric, and an
    /// unresolved (<c>Error</c>) or absent type is not numeric.
    /// </summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> when the expression's type is a primitive numeric type.</returns>
    public bool IsNumeric(ExpressionSyntax expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ITypeSymbol? type = _semanticModel.GetTypeInfo(expression).Type;
        return type is not null && IsNumericPrimitive(type.SpecialType);
    }

    /// <summary>
    /// Determines whether the expression's static type is a reference type — the analog of Java's
    /// exclusion of <c>ERROR</c>, <c>VOID</c>, and the primitives. An unresolved (<c>Error</c>) or
    /// absent type is explicitly excluded rather than relying on <see cref="ITypeSymbol.IsReferenceType"/>
    /// alone, mirroring Java's empty-classpath <c>ERROR</c> handling.
    /// </summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> when the expression's type is a resolved reference type.</returns>
    public bool IsReference(ExpressionSyntax expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ITypeSymbol? type = _semanticModel.GetTypeInfo(expression).Type;
        return type is not null && type.TypeKind != TypeKind.Error && type.IsReferenceType;
    }

    private static bool IsNumericPrimitive(SpecialType specialType) => specialType switch
    {
        SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal
            or SpecialType.System_Char => true,
        _ => false,
    };
}
