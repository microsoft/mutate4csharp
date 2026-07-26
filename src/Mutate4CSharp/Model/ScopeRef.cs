namespace Microsoft.Mutate4CSharp.Model;

/// <summary>
/// A lightweight reference to a manifest scope: its identifier, kind, and line range.
/// </summary>
/// <param name="Id">The scope identifier.</param>
/// <param name="Kind">The scope kind.</param>
/// <param name="StartLine">The 1-based start line.</param>
/// <param name="EndLine">The 1-based end line.</param>
public readonly record struct ScopeRef(string Id, string Kind, int StartLine, int EndLine)
{
    /// <summary>
    /// Creates a <see cref="ScopeRef"/> from the given <see cref="MutationScope"/>.
    /// </summary>
    /// <param name="scope">The scope to reference.</param>
    /// <returns>A reference to the given scope.</returns>
    public static ScopeRef From(MutationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return new ScopeRef(scope.Id, scope.Kind, scope.StartLine, scope.EndLine);
    }
}
