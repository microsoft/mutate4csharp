namespace Microsoft.Mutate4CSharp.Tests;

/// <summary>
/// Scaffolding sanity test: proves the solution builds, the test project references the
/// executable assembly, and the unit-test tier runs. Real behavior tests arrive in later tasks.
/// </summary>
public class ProgramTests
{
    /// <summary>
    /// The stub entry point returns the success exit code.
    /// </summary>
    [Fact]
    [Trait("type", "UnitTests")]
    public void MainReturnsSuccessExitCode()
    {
        Program.Main().Should().Be(0);
    }
}
