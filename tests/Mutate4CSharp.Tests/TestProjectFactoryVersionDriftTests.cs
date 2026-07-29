namespace Microsoft.Mutate4CSharp.Tests;

using System.Xml.Linq;
using Microsoft.Mutate4CSharp.Tests.Integration;

/// <summary>
/// A deterministic guard (no <c>dotnet</c>, no file generation) that the four package-version pins the
/// <see cref="TestProjectFactory"/> bakes into every generated sample stay byte-identical to the ones
/// <c>Mutate4CSharp.Tests.Common.targets</c> feeds the real test projects. Both must resolve the exact
/// same versions from the shared global package cache the main solution's restore already populated;
/// were they to drift (a common-targets bump not mirrored in the factory), a generated sample would
/// force a cold restore of a different version — a warm-cache miss that would silently slow, or on an
/// offline agent break, every S6 integration/acceptance test. This test fails fast at unit-lane speed
/// so the drift can never reach the integration lane. It is a plain unit test (no
/// <c>[Trait("type","IntegrationTests")]</c>) so it runs in the fast gate.
/// </summary>
public sealed class TestProjectFactoryVersionDriftTests
{
    private const string CommonTargetsFileName = "Mutate4CSharp.Tests.Common.targets";

    /// <summary>
    /// Each package the factory pins carries the same version the common targets declare.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FactoryPinsMatchCommonTargets()
    {
        IReadOnlyDictionary<string, string> declared = ReadCommonTargetsVersions();

        declared.Should().Contain("Microsoft.NET.Test.Sdk", TestProjectFactory.TestSdkVersion);
        declared.Should().Contain("xunit", TestProjectFactory.XunitVersion);
        declared.Should().Contain("xunit.runner.visualstudio", TestProjectFactory.XunitVersion);
        declared.Should().Contain("coverlet.collector", TestProjectFactory.CoverletCollectorVersion);
    }

    private static Dictionary<string, string> ReadCommonTargetsVersions()
    {
        string targetsPath = LocateCommonTargets();
        XDocument document = XDocument.Load(targetsPath);
        return document.Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "PackageReference", StringComparison.Ordinal))
            .Where(element => element.Attribute("Include") is not null && element.Attribute("Version") is not null)
            .ToDictionary(
                element => element.Attribute("Include")!.Value,
                element => element.Attribute("Version")!.Value,
                StringComparer.Ordinal);
    }

    private static string LocateCommonTargets()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, CommonTargetsFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate '{CommonTargetsFileName}' above '{AppContext.BaseDirectory}'.");
    }
}
