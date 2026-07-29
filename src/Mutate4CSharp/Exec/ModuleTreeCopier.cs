namespace Microsoft.Mutate4CSharp.Exec;

/// <summary>
/// Faithful port of mutate4java's package-private <c>ModuleTreeCopier</c>: the recursive, filtered
/// copy of a project tree into a fresh worker root. mutate4java walks the module root and skips the
/// single <c>target/</c> subtree (build output); the C# port walks the <em>copy root</em> and skips
/// the C# build/VCS/test output that must never be mirrored into a worker —
/// <c>bin/ obj/ .git/ .vs/ TestResults/</c> — plus the <em>worker base</em> itself (the temp directory
/// the run roots live under), the direct analog of mutate4java skipping its own worker base, which it
/// nested under the excluded <c>target/</c>. Excluded directory names are matched case-insensitively on
/// Windows and ordinally elsewhere, mirroring the T8 coverage-key path comparison.
/// </summary>
public sealed class ModuleTreeCopier
{
    // Filesystem name/path comparison: case-insensitive on Windows (like the T8 coverage-key
    // NormalizeSourcePath and ModuleResolver), ordinal elsewhere. This compares real on-disk names,
    // not hash-affecting sort keys, so it is deliberately NOT the fidelity-landmine ordinal comparer.
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static readonly HashSet<string> ExcludedDirectoryNames = new(PathComparer)
    {
        "bin",
        "obj",
        ".git",
        ".vs",
        "TestResults",
    };

    /// <summary>
    /// Recursively copies <paramref name="copyRoot"/> into <paramref name="workerRoot"/>, creating each
    /// directory and copying each file, but skipping the excluded build/VCS/test output directories and
    /// any directory at or beneath <paramref name="excludedBase"/> (the worker base) — the faithful
    /// analog of mutate4java's <c>SKIP_SUBTREE</c> on its excluded root.
    /// </summary>
    /// <param name="copyRoot">The source tree to copy.</param>
    /// <param name="workerRoot">The destination worker root (created if absent).</param>
    /// <param name="excludedBase">The worker base directory to never copy into a worker (avoids
    /// copy recursion when it happens to live under <paramref name="copyRoot"/>).</param>
    public void Copy(string copyRoot, string workerRoot, string excludedBase)
    {
        ArgumentNullException.ThrowIfNull(copyRoot);
        ArgumentNullException.ThrowIfNull(workerRoot);
        ArgumentNullException.ThrowIfNull(excludedBase);

        string sourceRoot = Path.GetFullPath(copyRoot);
        string destinationRoot = Path.GetFullPath(workerRoot);
        string excludedRoot = Path.GetFullPath(excludedBase);

        CopyTree(sourceRoot, destinationRoot, sourceRoot, excludedRoot);
    }

    private static void CopyTree(string sourceRoot, string workerRoot, string currentDirectory, string excludedRoot)
    {
        string relative = Path.GetRelativePath(sourceRoot, currentDirectory);
        string destinationDirectory =
            string.Equals(relative, ".", StringComparison.Ordinal) ? workerRoot : Path.Combine(workerRoot, relative);
        Directory.CreateDirectory(destinationDirectory);

        foreach (string file in Directory.EnumerateFiles(currentDirectory))
        {
            File.Copy(file, Path.Combine(workerRoot, Path.GetRelativePath(sourceRoot, file)));
        }

        foreach (string subdirectory in Directory.EnumerateDirectories(currentDirectory))
        {
            if (IsExcluded(subdirectory, excludedRoot))
            {
                continue;
            }

            CopyTree(sourceRoot, workerRoot, subdirectory, excludedRoot);
        }
    }

    private static bool IsExcluded(string directory, string excludedRoot)
    {
        return ExcludedDirectoryNames.Contains(Path.GetFileName(directory)) || IsWithin(directory, excludedRoot);
    }

    private static bool IsWithin(string path, string ancestor)
    {
        string full = Path.GetFullPath(path);
        if (string.Equals(full, ancestor, PathComparison))
        {
            return true;
        }

        string prefix = ancestor.EndsWith(Path.DirectorySeparatorChar) ? ancestor : ancestor + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, PathComparison);
    }
}
