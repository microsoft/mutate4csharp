namespace Microsoft.Mutate4CSharp.Project;

using Microsoft.Mutate4CSharp.Exec;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Faithful port of mutate4java's <c>ChangedFileDetector</c>: runs <c>git status --porcelain</c>
/// scoped to a project root and returns the changed <c>.cs</c> files under its <c>src</c> directory,
/// ordinally sorted. The one C# adaptation is the <c>git</c> spawn: mutate4java uses
/// <c>ProcessBuilder</c> inline, whereas the port threads the spawn through the injected
/// <see cref="ICommandExecutor"/> seam so the parsing/filtering logic is unit-testable with a stub in
/// place of real <c>git</c>. The class stays a static utility (mirroring the Java <c>static</c>
/// method) — the executor is a parameter rather than a constructor-injected field.
/// </summary>
public static class ChangedFileDetector
{
    private static readonly string[] LineSeparators = ["\r\n", "\n", "\r"];

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Detects changed <c>.cs</c> files under <c>&lt;projectRoot&gt;/src</c> via
    /// <c>git status --porcelain</c>.
    /// </summary>
    /// <param name="executor">The command executor used to run <c>git</c>.</param>
    /// <param name="projectRoot">The repository/project root to scope <c>git</c> to.</param>
    /// <returns>The ordinally sorted absolute paths of changed <c>.cs</c> files under <c>src</c>.</returns>
    /// <exception cref="InvalidOperationException"><c>git status</c> exited non-zero.</exception>
    public static IReadOnlyList<string> ChangedCSharpFilesUnderSrc(ICommandExecutor executor, string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(projectRoot);

        CommandResult result = executor.Run(
            ["git", "-C", projectRoot, "status", "--porcelain"], projectRoot);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("git status failed: " + result.Output);
        }

        string src = Path.GetFullPath(Path.Combine(projectRoot, "src"));
        List<string> files = [];
        foreach (string line in result.Output.Split(LineSeparators, StringSplitOptions.None))
        {
            string? path = ParseLine(projectRoot, line);
            if (path is not null && IsUnder(src, path))
            {
                files.Add(path);
            }
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private static string? ParseLine(string root, string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length < 4)
        {
            return null;
        }

        string pathText = line[3..].Trim();
        int renameMarker = pathText.IndexOf(" -> ", StringComparison.Ordinal);
        string finalPath = renameMarker >= 0 ? pathText[(renameMarker + 4)..] : pathText;
        if (!finalPath.EndsWith(".cs", PathComparison))
        {
            return null;
        }

        return Path.GetFullPath(Path.Combine(root, finalPath));
    }

    private static bool IsUnder(string src, string path)
    {
        if (string.Equals(src, path, PathComparison))
        {
            return true;
        }

        string prefix = src.EndsWith(Path.DirectorySeparatorChar) ? src : src + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, PathComparison);
    }
}
