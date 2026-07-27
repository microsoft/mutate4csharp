namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Analysis;
using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// Grounds the C#-specific manifest scope-kind taxonomy (deliberate departure DD1) in
/// <c>docs/decisions.md</c>. Because DD1 widens Java's <c>class/method/field</c> kinds to a C#
/// taxonomy, there is no Java oracle for the new kinds; each case therefore asserts the scope id, kind,
/// and line that decisions.md prescribes — the id shape
/// <c>"&lt;kind&gt;:&lt;prefix&gt;#&lt;detail&gt;:&lt;startLine&gt;"</c>, the type-name prefix
/// (outer&#8594;inner, including the type itself; members never push onto it), and the member/type kind
/// details. Scopes flow through the public <see cref="MutationCatalog.Analyze"/> seam, exercising the
/// whole scanner &#8594; tracker &#8594; factory pipeline.
/// </summary>
public sealed class MutationScopeTaxonomyTests : IDisposable
{
    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="MutationScopeTaxonomyTests"/> class, creating a
    /// per-test temporary directory.
    /// </summary>
    public MutationScopeTaxonomyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "m4cs-scopes-" + Guid.NewGuid().ToString("N"));
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
    /// A nested type's prefix is the enclosing type stack including the type itself; a member inside it
    /// carries the type prefix but does not push its own name.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void NestedTypePrefixIncludesTypeAndMemberDoesNot()
    {
        string source =
            """
            class Outer
            {
                class Inner
                {
                    int Foo(int a)
                    {
                        return a;
                    }
                }
            }
            """;

        IReadOnlyList<MutationScope> scopes = Analyze(source);

        Ids(scopes).Should().Contain(
        [
            $"class:Outer#Outer:{LineOf(source, "class Outer")}",
            $"class:Outer.Inner#Inner:{LineOf(source, "class Inner")}",
            $"method:Outer.Inner#Foo(1):{LineOf(source, "int Foo")}",
        ]);
    }

    /// <summary>
    /// A namespace is never a prefix component: a type inside <c>namespace Demo</c> keeps the type-only
    /// prefix (<c>class:Foo#Foo:L</c>), and no scope id carries the namespace name.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void NamespaceIsNeverAPrefixComponent()
    {
        string source =
            """
            namespace Demo
            {
                class Foo
                {
                    int Bar()
                    {
                        return 0;
                    }
                }
            }
            """;

        IReadOnlyList<MutationScope> scopes = Analyze(source);

        Ids(scopes).Should().Contain(
        [
            $"class:Foo#Foo:{LineOf(source, "class Foo")}",
            $"method:Foo#Bar(0):{LineOf(source, "int Bar")}",
        ]);
        Ids(scopes).Should().NotContain(id => id.Contains("Demo", StringComparison.Ordinal));
    }

    /// <summary>
    /// A multi-line scope records both its 1-based start and end lines: <c>EndLine</c> is computed
    /// independently (<c>span.EndLinePosition.Line + 1</c>) and must land on the declaration's closing
    /// brace, guarding against off-by-one and Span-vs-FullSpan / start-vs-end confusion.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void MultiLineScopeRecordsStartAndEndLines()
    {
        string source =
            """
            class Calc
            {
                int Sum(int a, int b)
                {
                    int total = a;
                    total = total + b;
                    return total;
                } // sum-end
            }
            """;

        IReadOnlyList<MutationScope> scopes = Analyze(source);

        MutationScope method = scopes.Single(scope => scope.Id.StartsWith("method:Calc#Sum(2):", StringComparison.Ordinal));
        method.StartLine.Should().Be(LineOf(source, "int Sum(int a, int b)"));
        method.EndLine.Should().Be(LineOf(source, "// sum-end"));
        method.EndLine.Should().BeGreaterThan(method.StartLine);
    }

    /// <summary>
    /// An accessor is a scope only when it has a body; an auto-property's bodiless accessors are not
    /// scopes, though the property itself is.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void AccessorWithBodyIsScopeButAutoPropertyAccessorsAreNot()
    {
        string source =
            """
            class Props
            {
                int _n;

                int WithBody
                {
                    get { return _n; }
                }

                int Auto { get; set; }
            }
            """;

        IReadOnlyList<MutationScope> scopes = Analyze(source);

        scopes.Where(scope => scope.Kind == "accessor").Select(scope => scope.Id)
            .Should().ContainSingle().Which.Should().Be($"accessor:Props#WithBody.get:{LineOf(source, "get { return _n; }")}");
        Ids(scopes).Should().Contain(
        [
            $"property:Props#WithBody:{LineOf(source, "int WithBody")}",
            $"property:Props#Auto:{LineOf(source, "int Auto")}",
        ]);
    }

    /// <summary>
    /// A <c>VariableDeclarator</c> under a field declaration is a <c>field</c> scope; the same node shape
    /// under a local declaration is not a scope, so the only field scope is the real field.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void FieldDeclaratorIsScopeButLocalIsNot()
    {
        string source =
            """
            class Fields
            {
                int _f = 0;

                int Compute()
                {
                    int local = 0;
                    return local;
                }
            }
            """;

        IReadOnlyList<MutationScope> scopes = Analyze(source);

        scopes.Where(scope => scope.Kind == "field").Select(scope => scope.Id)
            .Should().ContainSingle().Which.Should().Be($"field:Fields#_f:{LineOf(source, "int _f")}");
        Ids(scopes).Should().Contain($"method:Fields#Compute(0):{LineOf(source, "int Compute")}");
    }

    /// <summary>
    /// A static constructor uses the <c>cctor(0)</c> detail; an instance constructor uses <c>ctor(n)</c>.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void StaticAndInstanceConstructorsUseDistinctDetails()
    {
        string source =
            """
            class WithCtors
            {
                static WithCtors()
                {
                }

                WithCtors(int a)
                {
                }
            }
            """;

        IReadOnlyList<MutationScope> scopes = Analyze(source);

        Ids(scopes).Should().Contain(
        [
            $"constructor:WithCtors#cctor(0):{LineOf(source, "static WithCtors")}",
            $"constructor:WithCtors#ctor(1):{LineOf(source, "WithCtors(int a)")}",
        ]);
    }

    /// <summary>
    /// An operator uses the <c>operator&lt;Op&gt;(n)</c> detail; a conversion operator uses
    /// <c>implicit &lt;T&gt;(1)</c>.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void OperatorAndConversionUseTheirDetails()
    {
        string source =
            """
            struct Vec
            {
                public static Vec operator +(Vec a, Vec b)
                {
                    return a;
                }

                public static implicit operator Vec(int a)
                {
                    return new Vec();
                }
            }
            """;

        IReadOnlyList<MutationScope> scopes = Analyze(source);

        Ids(scopes).Should().Contain(
        [
            $"struct:Vec#Vec:{LineOf(source, "struct Vec")}",
            $"operator:Vec#operator+(2):{LineOf(source, "operator +")}",
            $"conversion-operator:Vec#implicit Vec(1):{LineOf(source, "implicit operator")}",
        ]);
    }

    /// <summary>
    /// A record and a record struct use distinct type kinds and each pushes its own name as prefix.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void RecordAndRecordStructUseDistinctKinds()
    {
        string source =
            """
            record Point(int X, int Y);

            record struct Size(int W, int H);
            """;

        IReadOnlyList<MutationScope> scopes = Analyze(source);

        Ids(scopes).Should().Contain(
        [
            $"record:Point#Point:{LineOf(source, "record Point")}",
            $"record-struct:Size#Size:{LineOf(source, "record struct Size")}",
        ]);
    }

    /// <summary>
    /// The remaining C#-specific member kinds — indexer, finalizer, enum-member — and the delegate type
    /// kind produce scopes with their prescribed details.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void IndexerFinalizerEnumMemberAndDelegateScopes()
    {
        string source =
            """
            delegate int Op(int a);

            enum Color
            {
                Red,
                Green
            }

            class Box
            {
                ~Box()
                {
                }

                int this[int i]
                {
                    get { return i; }
                }
            }
            """;

        IReadOnlyList<MutationScope> scopes = Analyze(source);

        Ids(scopes).Should().Contain(
        [
            $"delegate:Op#Op:{LineOf(source, "delegate int Op")}",
            $"enum:Color#Color:{LineOf(source, "enum Color")}",
            $"enum-member:Color#Red:{LineOf(source, "Red,")}",
            $"enum-member:Color#Green:{LineOf(source, "Green")}",
            $"finalizer:Box#finalizer(0):{LineOf(source, "~Box")}",
            $"indexer:Box#this[](1):{LineOf(source, "int this[int i]")}",
            $"accessor:Box#this[].get:{LineOf(source, "get { return i; }")}",
        ]);
    }

    private static IEnumerable<string> Ids(IReadOnlyList<MutationScope> scopes) =>
        scopes.Select(scope => scope.Id);

    private static int LineOf(string source, string marker)
    {
        int index = source.IndexOf(marker, StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0, "the marker '{0}' must exist in the source", marker);
        return source[..index].Count(character => character == '\n') + 1;
    }

    private IReadOnlyList<MutationScope> Analyze(string source)
    {
        string file = Path.Combine(_tempDir, "Source.cs");
        File.WriteAllText(file, source);
        return new MutationCatalog().Analyze(file).Scopes;
    }
}
