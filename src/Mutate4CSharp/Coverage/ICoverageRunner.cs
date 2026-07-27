namespace Microsoft.Mutate4CSharp.Coverage;

using Microsoft.Mutate4CSharp.Model;
using Microsoft.Mutate4CSharp.Project;

/// <summary>
/// The coverage-generation seam the engine depends on, extracted so the orchestration layer (T15) can
/// inject a stub in tests without spawning a real <c>dotnet test --collect</c> run. mutate4java's
/// engine referenced the concrete <c>CoverageRunner</c> directly and its tests subclassed it; the C#
/// <see cref="CoverageRunner"/> is <see langword="sealed"/>, so an interface takes the place of that
/// subclassing seam. The sole production implementation is <see cref="CoverageRunner"/>.
/// </summary>
public interface ICoverageRunner
{
    /// <summary>
    /// Generates fresh coverage for the module under test — the seam for mutate4java's single-argument
    /// <c>generateCoverage(projectRoot)</c>.
    /// </summary>
    /// <param name="module">The resolved production/test project pair to generate coverage for.</param>
    /// <returns>The baseline run and its parsed coverage report.</returns>
    CoverageRun GenerateCoverage(ModuleResolution module);

    /// <summary>
    /// Generates or reuses coverage for the module under test — the seam for mutate4java's
    /// <c>generateCoverage(projectRoot, reuseCoverage)</c>.
    /// </summary>
    /// <param name="module">The resolved production/test project pair to generate coverage for.</param>
    /// <param name="reuseCoverage">When <see langword="true"/>, reuses an existing report instead of refreshing.</param>
    /// <returns>The baseline run (or <see langword="null"/> on the reuse path) and the coverage report.</returns>
    CoverageRun GenerateCoverage(ModuleResolution module, bool reuseCoverage);
}
