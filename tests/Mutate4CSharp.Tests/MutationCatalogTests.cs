namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Analysis;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Faithful counterpart of mutate4java's <c>MutationCatalogTest</c>: the oracle for the AST mutation-site
/// scanner. Each case ports a Java source verbatim to its C# equivalent and asserts the same discovered
/// mutation set — count, source order, replacement-description strings, and replacement text — so the C#
/// port reproduces the Java behavior exactly. Discovery is driven through <see cref="MutationCatalog"/>,
/// exactly as the Java test drives <c>new MutationCatalog().discover(...)</c>; real DD1 scope metadata
/// now flows through every site, and these assertions confirm the site set is unchanged by it.
/// </summary>
public sealed class MutationCatalogTests : IDisposable
{
    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="MutationCatalogTests"/> class, creating a per-test
    /// temporary directory (the analog of JUnit's <c>@TempDir</c>).
    /// </summary>
    public MutationCatalogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-catalog-" + Guid.NewGuid().ToString("N"));
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
    /// Boolean literal, equality, and comparison operators each yield exactly one site, in source order.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void DiscoversBooleanEqualityAndComparisonMutations()
    {
        string file = WriteSource(
            """
            class Sample
            {
                bool Truthy()
                {
                    return true;
                }

                bool Same(int left, int right)
                {
                    return left == right;
                }

                bool Larger(int left, int right)
                {
                    return left > right;
                }

                bool Smaller(int left, int right)
                {
                    return left <= right;
                }
            }
            """);

        List<MutationSite> sites = Discover(file);

        sites.Should().HaveCount(4);
        sites[0].Description.Should().Be("replace true with false");
        sites[1].Description.Should().Be("replace == with !=");
        sites[2].Description.Should().Be("replace > with >=");
        sites[3].Description.Should().Be("replace <= with <");
    }

    /// <summary>
    /// Operators inside string literals, character literals, and comments are never sites; only the real
    /// string-valued return (as a null replacement) and the real equality operator are.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void IgnoresOperatorsInsideStringsCharsAndComments()
    {
        string file = WriteSource(
            """
            class Literals
            {
                string Text()
                {
                    return "true == false > <";
                }

                char Angle()
                {
                    return '>';
                }

                bool Same(int left, int right)
                {
                    // left == right > 0
                    /* false != true <= >= */
                    return left == right;
                }
            }
            """);

        List<MutationSite> sites = Discover(file);

        sites.Select(site => site.Description).Should().Equal(
            "replace \"true == false > <\" with null",
            "replace == with !=");
    }

    /// <summary>
    /// Generic type-argument angle brackets are never comparison sites; only the reference-valued return
    /// (as a null replacement) and the real comparison operator are.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void IgnoresGenericTypeAngleBrackets()
    {
        string file = WriteSource(
            """
            using System.Collections.Generic;

            class GenericSample
            {
                List<string> Names(List<string> source)
                {
                    return source;
                }

                bool Larger(int left, int right)
                {
                    return left > right;
                }
            }
            """);

        List<MutationSite> sites = Discover(file);

        sites.Select(site => site.Description).Should().Equal(
            "replace source with null",
            "replace > with >=");
    }

    /// <summary>
    /// Arithmetic (numeric <c>+</c>/<c>/</c>), conditional-boolean, and reference-valued null replacements
    /// on return, initializer, and assignment right-values are discovered in source order; string
    /// concatenation is excluded because <c>+</c> is numeric-only.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void DiscoversArithmeticLogicalAndNullReplacementMutations()
    {
        string file = WriteSource(
            """
            class Expanded
            {
                int Add(int left, int right)
                {
                    return left + right;
                }

                int Divide(int left, int right)
                {
                    return left / right;
                }

                bool Both(bool left, bool right)
                {
                    return left && right;
                }

                string Message()
                {
                    return "hello";
                }

                string Assign()
                {
                    string value = Helper();
                    value = Helper();
                    return value;
                }

                string Helper()
                {
                    return "x";
                }
            }
            """);

        List<MutationSite> sites = Discover(file);

        sites.Select(site => site.Description).Should().Equal(
            "replace + with -",
            "replace / with *",
            "replace && with ||",
            "replace \"hello\" with null",
            "replace Helper() with null",
            "replace Helper() with null",
            "replace value with null",
            "replace \"x\" with null");
    }

    /// <summary>
    /// Unary logical-complement and numeric unary-minus become removals (empty replacement text), and
    /// integer constants <c>0</c>/<c>1</c> swap.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void DiscoversUnaryAndConstantMutations()
    {
        string file = WriteSource(
            """
            class UnarySample
            {
                bool Invert(bool value)
                {
                    return !value;
                }

                int Negative(int value)
                {
                    return -value;
                }

                int Zero()
                {
                    return 0;
                }

                int One()
                {
                    return 1;
                }
            }
            """);

        List<MutationSite> sites = Discover(file);

        sites.Select(site => site.Description).Should().Equal(
            "replace ! with removed !",
            "replace - with removed -",
            "replace 0 with 1",
            "replace 1 with 0");

        sites[0].ReplacementText.Should().Be(string.Empty);
        sites[1].ReplacementText.Should().Be(string.Empty);
    }

    /// <summary>
    /// The <c>+</c> mutation is numeric-only: string concatenation is never rewritten to <c>-</c>. The
    /// only site on a string-valued <c>left + right</c> return is the reference-valued null replacement.
    /// This guards the numeric-only rule that the ported Java oracle does not exercise directly (Java has
    /// no string case), decided from the operand type via the semantic model.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void DoesNotMutateStringConcatenation()
    {
        string file = WriteSource(
            """
            class Concat
            {
                string Join(string left, string right)
                {
                    return left + right;
                }
            }
            """);

        List<MutationSite> sites = Discover(file);

        sites.Select(site => site.Description).Should().Equal("replace left + right with null");
    }

    private static List<MutationSite> Discover(string file)
    {
        return [.. new MutationCatalog().Discover([file])];
    }

    private string WriteSource(string source)
    {
        string file = Path.Combine(_tempDir, "Source.cs");
        File.WriteAllText(file, source);
        return file;
    }
}
