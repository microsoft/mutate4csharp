namespace Microsoft.Mutate4CSharp.Project;

/// <summary>
/// Faithful port of mutate4java's <c>SourceFileFinder</c>: enumerates every source file under the
/// <c>src</c> directory of a root, returning an empty list when <c>src</c> is absent. The only
/// ecosystem adaptations are <c>.java</c> → <c>.cs</c> and excluding the <c>bin</c>/<c>obj</c> build
/// output that C# projects nest under source directories (Java has no such output under <c>src</c>).
/// Ordering preserves Java's natural-order sort as an ordinal sort (the fidelity-landmine rule).
/// </summary>
public static class SourceFileFinder
{
    private static readonly char[] SeparatorChars = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Finds every <c>.cs</c> file under <c>&lt;root&gt;/src</c>, excluding <c>bin</c>/<c>obj</c> build
    /// output, sorted ordinally.
    /// </summary>
    /// <param name="root">The directory whose <c>src</c> subtree is searched.</param>
    /// <returns>The ordinally sorted absolute paths, or an empty list when <c>src</c> is missing.</returns>
    public static IReadOnlyList<string> FindAllCSharpFilesUnderSrc(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        string src = Path.Combine(root, "src");
        if (!Directory.Exists(src))
        {
            return [];
        }

        EnumerationOptions options = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };
        List<string> files = Directory.EnumerateFiles(src, "*", options)
            .Where(path => path.EndsWith(".cs", PathComparison))
            .Where(path => !IsUnderBuildOutput(src, path))
            .ToList();
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private static bool IsUnderBuildOutput(string src, string path)
    {
        string relative = Path.GetRelativePath(src, path);
        foreach (string segment in relative.Split(SeparatorChars, StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(segment, "bin", PathComparison) || string.Equals(segment, "obj", PathComparison))
            {
                return true;
            }
        }

        return false;
    }
}
