namespace Microsoft.Mutate4CSharp.Tests.Integration;

/// <summary>
/// The small shared surface both integration tests use to generate a hermetic .NET 8 sample under a
/// temp root: the package-version pins (kept byte-identical to
/// <c>Mutate4CSharp.Tests.Common.targets</c> so every generated sample restores the exact versions
/// the main solution's restore already populated in the global cache — Anders' cheap exception against
/// cache drift between the two integration samples) and <see cref="WriteHermeticGuards(string)"/>, the
/// guard-writing helper that stops MSBuild / NuGet from walking above the temp root. This is
/// deliberately NOT the T18 <c>TestProjectFactory</c>: it factors out only the version constants and
/// the guard helper, leaving each test to author its own project layout.
/// </summary>
public static class HermeticSample
{
    /// <summary>The target framework every generated sample project builds against.</summary>
    public const string TargetFramework = "net8.0";

    /// <summary>The pinned <c>Microsoft.NET.Test.Sdk</c> version (matches the tests' common targets).</summary>
    public const string TestSdkVersion = "17.8.0";

    /// <summary>The pinned <c>xunit</c> / <c>xunit.runner.visualstudio</c> version.</summary>
    public const string XunitVersion = "2.5.3";

    /// <summary>The pinned <c>coverlet.collector</c> version.</summary>
    public const string CoverletCollectorVersion = "6.0.0";

    /// <summary>
    /// The <c>nuget.config</c> that clears inherited sources and pins restore to nuget.org, so a
    /// generated sample resolves exactly the pinned versions from the shared global package cache.
    /// </summary>
    public const string NuGetConfig =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
          </packageSources>
        </configuration>
        """;

    /// <summary>
    /// Writes the hermetic guards at <paramref name="root"/>: empty <c>Directory.Build.props</c> and
    /// <c>Directory.Build.targets</c> that stop MSBuild walking above the temp root into a machine-level
    /// import that could break the sample build, plus the pinned <see cref="NuGetConfig"/> served from
    /// the global cache the main solution's restore already populated.
    /// </summary>
    /// <param name="root">The temp root the generated sample (and its worker copies) live under.</param>
    public static void WriteHermeticGuards(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        File.WriteAllText(Path.Combine(root, "Directory.Build.props"), "<Project />");
        File.WriteAllText(Path.Combine(root, "Directory.Build.targets"), "<Project />");
        File.WriteAllText(Path.Combine(root, "nuget.config"), NuGetConfig);
    }
}
