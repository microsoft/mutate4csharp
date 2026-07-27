namespace Microsoft.Mutate4CSharp.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// The result of compiling a single C# source file: the parsed compilation-unit root and the
/// resolved semantic model. The faithful port of mutate4java's <c>CompiledSource</c> record — its
/// <c>units</c> (parsed compilation-unit roots) map to <see cref="Root"/> and its <c>trees</c> (the
/// JDK <c>Trees</c> service) map to <see cref="SemanticModel"/>. Java read node positions through
/// <c>Trees.getSourcePositions()</c>; in Roslyn those are intrinsic, so no separate accessor is
/// needed — every node carries its <see cref="Microsoft.CodeAnalysis.Text.TextSpan"/> and line
/// spans come from <c>Root.SyntaxTree.GetLineSpan(...)</c>.
/// </summary>
/// <param name="Root">The parsed compilation-unit root of the single source file.</param>
/// <param name="SemanticModel">The resolved semantic model used for numeric/reference type queries.</param>
public sealed record CompiledSource(CompilationUnitSyntax Root, SemanticModel SemanticModel);
