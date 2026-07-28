namespace Microsoft.Mutate4CSharp.Tests.Integration;

/// <summary>
/// xUnit non-parallel collection for the integration tests. Every integration class that spawns a real
/// nested <c>dotnet test --collect</c> / coverage run is tagged <c>[Collection("Integration")]</c> so the
/// whole IT lane runs <b>serially</b> — xUnit parallelizes across test classes by default, and multiple
/// concurrent coverage collectors can contend and transiently flip a covered site to uncovered (a
/// test-harness artifact only: production collects coverage exactly once, and per-mutant workers run
/// WITHOUT <c>--collect</c>). Disabling parallelization for this collection removes the contention while
/// leaving the unit lane (whose classes are NOT in this collection) fully parallel. No fixture is needed.
/// </summary>
[CollectionDefinition("Integration", DisableParallelization = true)]
public sealed class IntegrationCollectionDefinition
{
}
