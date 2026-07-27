namespace Microsoft.Mutate4CSharp.Tests;

using Microsoft.Mutate4CSharp.Cli;

/// <summary>
/// Faithful counterpart of mutate4java's <c>MainTest</c>: verifies the conditional-exit helper only
/// invokes the exiter for a non-zero code. The captured local mutated by the lambda stands in for
/// Java's <c>AtomicInteger</c>.
/// </summary>
public sealed class MainTests
{
    /// <summary>A zero exit code leaves the exiter uninvoked.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void DoesNotExitWhenCodeIsZero()
    {
        int exit = -1;

        Main.ExitIfNeeded(0, code => exit = code);

        exit.Should().Be(-1);
    }

    /// <summary>A non-zero exit code invokes the exiter with that code.</summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void ExitsWhenCodeIsNonZero()
    {
        int exit = -1;

        Main.ExitIfNeeded(3, code => exit = code);

        exit.Should().Be(3);
    }
}
