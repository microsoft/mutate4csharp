namespace Microsoft.Mutate4CSharp.Project;

using System.Xml;
using System.Xml.Linq;

/// <summary>
/// Resolves a target <c>.cs</c> file to its owning production project and unit test project under the
/// C# DD2/DD3 convention (see <c>docs/decisions.md</c>). This is the faithful replacement for
/// mutate4java's <c>ModuleRootFinder</c>, which walked up to the nearest <c>pom.xml</c>: Maven's
/// module root has no C# analog, so the port instead (1) derives <c>&lt;Project&gt;</c> as the file
/// name of the nearest ancestor <c>.csproj</c>, and (2) discovers the <c>&lt;Project&gt;.Tests</c> /
/// <c>&lt;Project&gt;.UnitTests</c> project whose <c>&lt;ProjectReference&gt;</c> closure (transitively)
/// includes that <c>.csproj</c> — validating the mapping in mono-repos. Not-found is a typed signal
/// (<see cref="ModuleResolution"/>), never a process exit; the CLI layer (T15) maps it to exit
/// <c>2</c>.
/// </summary>
public sealed class ModuleResolver
{
    private static readonly char[] SeparatorChars = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    // Filesystem name/path comparison: case-insensitive on Windows (like the T8 coverage-key
    // NormalizeSourcePath), ordinal elsewhere. Deliberately NOT the fidelity-landmine ordinal used
    // for hash-affecting ordering — this compares real on-disk paths, not sort keys.
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly string _workspaceRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleResolver"/> class.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root; the ceiling for the <c>.csproj</c> ascent and
    /// the subtree searched for test projects.</param>
    public ModuleResolver(string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(workspaceRoot);
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
    }

    /// <summary>
    /// Resolves the owning production project and its unit test project for the given target file.
    /// </summary>
    /// <param name="file">The target <c>.cs</c> file.</param>
    /// <returns>A resolved result, or a typed not-found signal.</returns>
    public ModuleResolution Resolve(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        string target = Path.GetFullPath(file);

        string? projectFile = FindOwningProject(target);
        if (projectFile is null)
        {
            return ModuleResolution.NoOwningProject();
        }

        string projectName = Path.GetFileNameWithoutExtension(projectFile);
        string? testProjectFile = FindTestProject(projectName, projectFile);
        return testProjectFile is null
            ? ModuleResolution.NoTestProject(projectName, projectFile)
            : ModuleResolution.Resolved(projectName, projectFile, testProjectFile);
    }

    private string? FindOwningProject(string target)
    {
        string? directory = Directory.Exists(target) ? target : Path.GetDirectoryName(target);
        while (directory is not null && IsWithinWorkspace(directory))
        {
            string? projectFile = NearestProjectInDirectory(directory);
            if (projectFile is not null)
            {
                return projectFile;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    private static string? NearestProjectInDirectory(string directory)
    {
        return Directory.EnumerateFiles(directory)
            .Where(path => path.EndsWith(".csproj", PathComparison))
            .OrderBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private string? FindTestProject(string projectName, string projectFile)
    {
        string testName = projectName + ".Tests.csproj";
        string unitTestName = projectName + ".UnitTests.csproj";

        List<string> candidates = EnumerateProjectFiles()
            .Where(path =>
            {
                string name = Path.GetFileName(path);
                return string.Equals(name, testName, PathComparison)
                    || string.Equals(name, unitTestName, PathComparison);
            })
            .Where(path => ReferencesTransitively(path, projectFile))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        candidates.Sort((left, right) => CompareCandidates(left, right, projectFile));
        return candidates[0];
    }

    private IEnumerable<string> EnumerateProjectFiles()
    {
        EnumerationOptions options = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };
        return Directory.EnumerateFiles(_workspaceRoot, "*", options)
            .Where(path => path.EndsWith(".csproj", PathComparison))
            .Where(path => !IsUnderBuildOutput(path));
    }

    private bool ReferencesTransitively(string testProject, string projectFile)
    {
        string target = Path.GetFullPath(projectFile);
        HashSet<string> visited = new(PathComparer) { Path.GetFullPath(testProject) };
        Queue<string> pending = new();
        foreach (string reference in ProjectReferences(testProject))
        {
            pending.Enqueue(reference);
        }

        while (pending.Count > 0)
        {
            string current = Path.GetFullPath(pending.Dequeue());
            if (!visited.Add(current))
            {
                continue;
            }

            if (string.Equals(current, target, PathComparison))
            {
                return true;
            }

            if (File.Exists(current))
            {
                foreach (string reference in ProjectReferences(current))
                {
                    pending.Enqueue(reference);
                }
            }
        }

        return false;
    }

    private static List<string> ProjectReferences(string projectFile)
    {
        if (!File.Exists(projectFile))
        {
            return [];
        }

        string directory = Path.GetDirectoryName(projectFile)!;
        XDocument document = LoadSecure(projectFile);
        List<string> references = [];
        foreach (XElement element in document.Descendants()
                     .Where(node => string.Equals(node.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase)))
        {
            string? include = element.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            string relative = include
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            references.Add(Path.GetFullPath(Path.Combine(directory, relative)));
        }

        return references;
    }

    private int CompareCandidates(string left, string right, string projectFile)
    {
        string projectDirectory = Path.GetDirectoryName(projectFile)!;

        // Tie-break priority (docs/decisions.md): sibling → under a tests/ dir → nearest by path;
        // the .Tests-over-.UnitTests suffix preference is the final discriminator.
        int siblingRank = Rank(IsSibling(left, projectDirectory), IsSibling(right, projectDirectory));
        if (siblingRank != 0)
        {
            return siblingRank;
        }

        int testsDirRank = Rank(IsUnderTestsDirectory(left), IsUnderTestsDirectory(right));
        if (testsDirRank != 0)
        {
            return testsDirRank;
        }

        int distance = PathDistance(projectDirectory, left).CompareTo(PathDistance(projectDirectory, right));
        if (distance != 0)
        {
            return distance;
        }

        int suffix = SuffixRank(left).CompareTo(SuffixRank(right));
        return suffix != 0 ? suffix : string.Compare(left, right, StringComparison.Ordinal);
    }

    private static int Rank(bool left, bool right)
    {
        // A preferred (true) candidate sorts first.
        if (left == right)
        {
            return 0;
        }

        return left ? -1 : 1;
    }

    private static bool IsSibling(string candidate, string projectDirectory)
    {
        string? candidateParent = Path.GetDirectoryName(Path.GetDirectoryName(candidate));
        string? projectParent = Path.GetDirectoryName(projectDirectory);
        return candidateParent is not null
            && projectParent is not null
            && string.Equals(candidateParent, projectParent, PathComparison);
    }

    private bool IsUnderTestsDirectory(string candidate)
    {
        string relative = Path.GetRelativePath(_workspaceRoot, candidate);
        foreach (string segment in relative.Split(SeparatorChars, StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(segment, "tests", PathComparison))
            {
                return true;
            }
        }

        return false;
    }

    private static int PathDistance(string projectDirectory, string candidate)
    {
        string candidateDirectory = Path.GetDirectoryName(candidate)!;
        string relative = Path.GetRelativePath(projectDirectory, candidateDirectory);
        if (string.Equals(relative, ".", StringComparison.Ordinal))
        {
            return 0;
        }

        return relative.Split(SeparatorChars, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static int SuffixRank(string candidate)
    {
        // .Tests (0) is preferred over .UnitTests (1).
        return Path.GetFileName(candidate).EndsWith(".UnitTests.csproj", PathComparison) ? 1 : 0;
    }

    private bool IsUnderBuildOutput(string path)
    {
        string relative = Path.GetRelativePath(_workspaceRoot, path);
        foreach (string segment in relative.Split(SeparatorChars, StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(segment, "bin", PathComparison) || string.Equals(segment, "obj", PathComparison))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsWithinWorkspace(string directory)
    {
        if (string.Equals(directory, _workspaceRoot, PathComparison))
        {
            return true;
        }

        string prefix = _workspaceRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _workspaceRoot
            : _workspaceRoot + Path.DirectorySeparatorChar;
        return directory.StartsWith(prefix, PathComparison);
    }

    private static XDocument LoadSecure(string projectFile)
    {
        // Secure reader: prohibit DTD processing and disable external entity resolution (XXE), the
        // same posture as the T8 Cobertura parser.
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        using FileStream stream = File.OpenRead(projectFile);
        using XmlReader reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader);
    }
}
