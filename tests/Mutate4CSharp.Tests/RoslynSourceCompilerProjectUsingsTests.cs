namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Analysis;

/// <summary>
/// Pins the DD5 behavior: <see cref="RoslynSourceCompiler"/> honors the target file's owning-project
/// global/implicit using context so implicit-using-dependent BCL reference types (<c>List&lt;T&gt;</c>,
/// <c>ISet&lt;T&gt;</c>, …) bind on modern (<c>ImplicitUsings=enable</c>) C# and become null-replacement
/// sites — restoring parity with mutate4java, whose imports are in-file. Each case writes a real
/// <c>.csproj</c> + <c>.cs</c> to a temp directory and drives discovery through
/// <see cref="MutationCatalog"/>, exactly as production does. The <em>synthesized-fallback</em> path is
/// exercised (no <c>obj/</c> present) so the tests are deterministic and need no build.
/// </summary>
/// <remarks>
/// Two invariants are pinned alongside the gap-closing behavior: (1) the references lever is UNTOUCHED —
/// a genuinely third-party / unresolvable type is still not a site, because honoring usings can bind
/// only a BCL type whose assembly is already referenced (via TPA); and (2) the change is monotonic — a
/// loose <c>.cs</c> file with no owning <c>.csproj</c> behaves exactly as before.
/// </remarks>
public sealed class RoslynSourceCompilerProjectUsingsTests : IDisposable
{
    private const string ImplicitUsingsEnabledProject =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
        </Project>
        """;

    private const string ImplicitUsingsDisabledProject =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <ImplicitUsings>disable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
        </Project>
        """;

    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoslynSourceCompilerProjectUsingsTests"/> class,
    /// creating a per-test temporary directory that holds the synthesized project and source files.
    /// </summary>
    public RoslynSourceCompilerProjectUsingsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-usings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// Deletes the per-test temporary directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gap closed (the point): with the owning project's <c>ImplicitUsings=enable</c> honored (via the
    /// synthesized base set), an implicit-using-dependent BCL reference-type return — <c>List&lt;T&gt;</c>
    /// and <c>ISet&lt;T&gt;</c> WITHOUT an explicit <c>using System.Collections.Generic;</c> in the file —
    /// now binds and IS a null-replacement site.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void HonorsImplicitUsingsSoBclReferenceReturnsBecomeNullSites()
    {
        WriteProject(ImplicitUsingsEnabledProject);
        string file = WriteSource(
            """
            class Sample
            {
                List<string> Names(List<string> source) => source;

                ISet<string> Keys(ISet<string> source) => source;
            }
            """);

        List<string> descriptions = Descriptions(file);

        descriptions.Should().Equal(
            "replace source with null",
            "replace source with null");
    }

    /// <summary>
    /// Bound held (can't over-resolve): honoring usings binds only BCL types whose assembly is already
    /// referenced. In one file under <c>ImplicitUsings=enable</c>, the <c>List&lt;string&gt;</c> return
    /// IS a site while a genuinely unresolvable third-party type (<c>Acme.Widget</c> — no such assembly on
    /// the TPA reference set) is still NOT a site. The mechanism only adds usings; it never touched the
    /// references lever.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void DoesNotBindThirdPartyTypesBecauseReferencesLeverIsUntouched()
    {
        WriteProject(ImplicitUsingsEnabledProject);
        string file = WriteSource(
            """
            class Sample
            {
                List<string> Ok(List<string> source) => source;

                Acme.Widget Nope(Acme.Widget widget) => widget;
            }
            """);

        List<string> descriptions = Descriptions(file);

        descriptions.Should().Equal("replace source with null");
        descriptions.Should().NotContain("replace widget with null");
    }

    /// <summary>
    /// An explicit <c>global using System.Collections.Generic;</c> in a sibling project <c>.cs</c> file
    /// (here <c>GlobalUsings.cs</c>) is discovered by the parse-only project scan and makes an unqualified
    /// <c>List&lt;string&gt;</c> return in the target file a site — even with <c>ImplicitUsings</c>
    /// disabled, isolating the explicit-global-using scan as the sole binding source.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void HonorsExplicitGlobalUsingFromSiblingProjectFile()
    {
        WriteProject(ImplicitUsingsDisabledProject);
        File.WriteAllText(
            Path.Combine(_tempDir, "GlobalUsings.cs"),
            "global using System.Collections.Generic;\n");
        string file = WriteSource(
            """
            class Sample
            {
                List<string> Names(List<string> source) => source;
            }
            """);

        List<string> descriptions = Descriptions(file);

        descriptions.Should().Equal("replace source with null");
    }

    /// <summary>
    /// No-project fallback (monotonic): a loose <c>.cs</c> file with no owning <c>.csproj</c> behaves
    /// exactly as before — the implicit-using-dependent <c>List&lt;string&gt;</c> return does not bind and
    /// is NOT a site, because nothing is injected.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void LooseFileWithoutOwningProjectIsUnchanged()
    {
        string file = WriteSource(
            """
            class Sample
            {
                List<string> Names(List<string> source) => source;
            }
            """);

        List<string> descriptions = Descriptions(file);

        descriptions.Should().BeEmpty();
    }

    /// <summary>
    /// The same loose file becomes a site once its own in-file <c>using</c> resolves the type — proving
    /// the previous case's non-site is the (unchanged) resolution behavior, not an artifact of the setup.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void LooseFileWithInFileUsingIsStillASite()
    {
        string file = WriteSource(
            """
            using System.Collections.Generic;

            class Sample
            {
                List<string> Names(List<string> source) => source;
            }
            """);

        List<string> descriptions = Descriptions(file);

        descriptions.Should().Equal("replace source with null");
    }

    /// <summary>
    /// Under an honored project context the type gates are unchanged: value types (<c>int</c>, a
    /// user <c>struct</c>) and <c>void</c> are still non-sites, while a predefined <c>string</c> /
    /// <c>string[]</c> return remains a site exactly as before — the change only ADDs BCL bindings.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ValueTypesAndVoidStayNonSitesWhileStringStaysASite()
    {
        WriteProject(ImplicitUsingsEnabledProject);
        string file = WriteSource(
            """
            struct Point
            {
            }

            class Sample
            {
                int Number(int value) => value;

                Point Where(Point value) => value;

                void Nothing(string value) => Keep(value);

                string Text(string value) => value;

                string[] Many(string[] value) => value;

                void Keep(string value)
                {
                }
            }
            """);

        List<string> descriptions = Descriptions(file);

        descriptions.Should().Equal(
            "replace value with null",
            "replace value with null");
    }

    /// <summary>
    /// Generated-file precedence: when the SDK-generated <c>obj/**/&lt;Project&gt;.GlobalUsings.g.cs</c>
    /// exists it is the source of the base global usings, taking precedence over synthesis — proven here
    /// with <c>ImplicitUsings</c> disabled (so synthesis would contribute nothing), yet the generated
    /// file's <c>global using global::System.Collections.Generic;</c> still binds the <c>List&lt;string&gt;</c>
    /// return into a site.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void PrefersGeneratedGlobalUsingsFileOverSynthesis()
    {
        WriteProject(ImplicitUsingsDisabledProject);
        WriteGeneratedGlobalUsings(
            """
            // <auto-generated/>
            global using global::System.Collections.Generic;
            """);
        string file = WriteSource(
            """
            class Sample
            {
                List<string> Names(List<string> source) => source;
            }
            """);

        List<string> descriptions = Descriptions(file);

        descriptions.Should().Equal("replace source with null");
    }

    /// <summary>
    /// Defensive degradation (monotonic guarantee): because project discovery has no workspace ceiling
    /// it can land on an unrelated, malformed ancestor <c>.csproj</c> that the baseline never builds. The
    /// optional project-context read must not abort analysis — a narrow catch degrades to the no-context
    /// (loose-file-equivalent) behavior, so a <c>List&lt;string&gt;</c> return does not bind and analysis
    /// completes without throwing.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void MalformedOwningProjectDegradesToNoContext()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Sample.csproj"), "<Project><PropertyGroup></Project>");
        string file = WriteSource(
            """
            class Sample
            {
                List<string> Names(List<string> source) => source;
            }
            """);

        List<string> descriptions = Descriptions(file);

        descriptions.Should().BeEmpty();
    }

    private static List<string> Descriptions(string file)
    {
        return [.. new MutationCatalog().Discover([file]).Select(site => site.Description)];
    }

    private void WriteGeneratedGlobalUsings(string content)
    {
        string generatedDir = Path.Combine(_tempDir, "obj", "Debug", "net8.0");
        Directory.CreateDirectory(generatedDir);
        File.WriteAllText(Path.Combine(generatedDir, "Sample.GlobalUsings.g.cs"), content);
    }

    private void WriteProject(string projectXml)
    {
        File.WriteAllText(Path.Combine(_tempDir, "Sample.csproj"), projectXml);
    }

    private string WriteSource(string source)
    {
        string file = Path.Combine(_tempDir, "Sample.cs");
        File.WriteAllText(file, source);
        return file;
    }
}
