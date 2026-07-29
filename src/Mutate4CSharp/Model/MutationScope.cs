namespace Microsoft.Mutate4CSharp.Model;

/// <summary>
/// A manifest scope entry: its identifier, kind, line range, and semantic hash.
/// </summary>
/// <param name="Id">The scope identifier.</param>
/// <param name="Kind">The scope kind.</param>
/// <param name="StartLine">The 1-based start line.</param>
/// <param name="EndLine">The 1-based end line.</param>
/// <param name="SemanticHash">The semantic hash of the scope's source.</param>
public sealed record MutationScope(string Id, string Kind, int StartLine, int EndLine, string SemanticHash);
