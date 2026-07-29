# mutate4csharp

## Attribution

`mutate4csharp` continues the lineage of Robert C. ("Uncle Bob") Martin's original **mutate4clj**, and
is a faithful C# port of its Java sibling **mutate4java** (the read-only reference tool at
`../mutate4java`). It preserves that tool's class decomposition, CLI contract, report format, and exit
codes; only the ecosystem adapters change — Roslyn replaces the JDK compiler tree API, Coverlet →
Cobertura replaces JaCoCo, and `dotnet test` / MSBuild replaces Maven.

---

`mutate4csharp` is a standalone mutation-testing tool for C# projects.

It targets one C# source file at a time, discovers mutation sites in that file, runs the owning
project's tests, and reports which mutants were killed, survived, timed out, or were skipped because
the target line was uncovered.

It also supports differential mutation through an embedded manifest comment at the end of the source
file. When a manifest is present, `mutate4csharp` can skip unchanged declaration scopes instead of
rerunning the entire file.

## What It Does

For a requested C# source file, `mutate4csharp`:

- runs the owning project's tests with Coverlet (Cobertura) coverage enabled
- fails fast if the unmodified baseline is red
- discovers supported mutation sites from the Roslyn syntax tree
- fingerprints declaration scopes for differential mutation
- filters out uncovered mutation sites using the Cobertura XML report
- applies each covered mutation
- reruns `dotnet test` for each mutant
- prints a differential diagnostics block before running workers
- reports killed and survived mutants in source order
- writes an embedded manifest footer after successful clean runs

Mutation runs can be isolated across multiple worker copies of the project so parallel mutants do not
overwrite each other.

## Usage

```bash
# Mutate one C# source file
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/Flag.cs

# Print a mutation-site scan without running tests
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/Flag.cs --scan

# Write or refresh the embedded manifest without running tests
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/Flag.cs --update-manifest

# Reuse existing coverage data instead of refreshing it
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/Flag.cs --reuse-coverage

# Restrict mutation to specific lines
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/Flag.cs --lines 12,18

# Mutate only scopes changed since the embedded manifest
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/Flag.cs --since-last-run

# Ignore the embedded manifest and mutate all covered sites
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/Flag.cs --mutate-all

# Warn when the selected mutation count is large
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/Flag.cs --mutation-warning 50

# Limit parallel worker count
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/Flag.cs --max-workers 4

# Adjust the mutant timeout multiplier
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/Flag.cs --timeout-factor 15

# Override the test command
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/Flag.cs --test-command "dotnet test --filter Category!=no-mutate"

# Print live worker progress
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/Flag.cs --verbose

# Show usage
dotnet run --project src/Mutate4CSharp -c Release -- --help
```

## Command-Line Options

- `--lines 12,18`
  Restricts mutation to the listed source lines in the requested file.

- `--scan`
  Bypasses baseline, coverage, and mutant execution. It prints every discovered mutation site and
  marks changed scopes from the embedded manifest with `*`.

- `--update-manifest`
  Rewrites the embedded manifest for the requested file without running baseline tests, coverage, or
  mutants.

- `--reuse-coverage`
  Reuses the existing Cobertura XML coverage report instead of refreshing coverage before mutation
  starts.

- `--since-last-run`
  Restricts mutation to covered sites in declaration scopes that changed since the embedded manifest.

- `--mutate-all`
  Ignores the embedded manifest and runs all covered mutation sites.

- `--mutation-warning N`
  Prints a warning when the selected covered mutation count exceeds `N`. The default is `50`.

- `--max-workers N`
  Caps the number of isolated parallel workers. The default is half the available processors, with a
  minimum of `1`.

- `--timeout-factor N`
  Sets the timeout multiplier for each mutant test run, relative to the baseline duration. The
  default is `10`.

- `--test-command CMD`
  Overrides the baseline and mutant test command. When this is set, `mutate4csharp` falls back to
  treating all discovered sites as covered unless external coverage data is already available.

- `--verbose`
  Prints live mutation progress, including worker start and finish lines.

- `--help`
  Prints usage text.

## Targeting Rules

- The tool accepts exactly one `.cs` file target.
- Directory-wide mutation is not supported.
- Test sources are executed, but they are not mutation targets.
- `--update-manifest` may not be combined with `--scan`, `--reuse-coverage`, `--lines`,
  `--since-last-run`, or `--mutate-all`.
- `--lines` may not be combined with `--since-last-run` or `--mutate-all`.
- `--scan` may not be combined with `--since-last-run`, `--mutate-all`, or `--reuse-coverage`.
- `--since-last-run` may not be combined with `--mutate-all`.

## Coverage Filtering

`mutate4csharp` generates Coverlet (Cobertura) coverage during the baseline run and uses line coverage
to skip uncovered mutation sites.

When `--reuse-coverage` is used, the tool skips the coverage refresh and reuses the existing
`coverage.cobertura.xml` report if it exists. The run prints a warning because covered/uncovered
classification may be stale. If the report does not exist, the run continues without coverage
filtering.

When `--test-command` is used, the tool does not attempt to wrap that custom command in Coverlet. In
that mode, mutation sites are treated as covered.

Uncovered sites are reported as:

```text
UNCOVERED path/to/File.cs:42 replace true with false
```

If every discovered site is uncovered, no mutants are executed.

## Parallel Workers

When `--max-workers` is greater than `1`, `mutate4csharp` creates isolated worker copies of the owning
project under:

```text
mutation-workers/run-<uuid>/worker-N/
```

Each worker:

- owns its own copied project tree
- mutates only files inside that private copy
- runs `dotnet test` inside its own workspace
- restores the mutated file before taking the next job

This avoids collisions in source files, MSBuild `bin/`/`obj/` output, test-result artifacts, and
coverage output.

## Embedded Manifest

On successful clean runs, `mutate4csharp` writes an embedded footer comment at the end of the source
file. That manifest records:

- manifest version
- module hash
- declaration scopes with stable ids
- start/end lines
- scope semantic hashes

The manifest is stripped before source analysis, so it does not perturb mutation-site positions or
scope hashing.

With no explicit selection flags:

- if no manifest exists, `mutate4csharp` mutates all covered sites
- if a manifest exists and the module hash is unchanged, it runs zero mutations
- if a manifest exists and the module hash changed, it mutates only sites inside changed scopes

This makes repeated mutation runs cheaper on large files without relying on git.

`--update-manifest` is the manual version of that write step. It refreshes the embedded manifest from
the current source analysis even if the project's tests are red, because it does not run them.

`--update-manifest` should not run coverage at all. It is a manifest rewrite only.

## Scan Mode

`--scan` prints a lightweight differential inventory for a single file. It does not:

- run the baseline tests
- generate coverage
- run any mutants
- rewrite the embedded manifest

Instead it prints the discovered mutation sites in source order and, when a manifest exists, prefixes
sites in changed scopes with `*`.

Typical scan output looks like this:

```text
Scan: 2 mutation sites in src/Demo/Flag.cs
* src/Demo/Flag.cs:5 replace true with false
  src/Demo/Flag.cs:9 replace == with !=
* indicates a scope that differs from the embedded manifest.
```

## Module & Test Resolution

`mutate4csharp` resolves which tests to run for a target file by convention:

- The **owning project** is the nearest `.csproj` above the target `.cs` file; its file name without
  extension is `<Project>`.
- The **test project** is `<Project>.Tests.csproj` or `<Project>.UnitTests.csproj` whose project
  references (transitively) include `<Project>.csproj`. Only that project's tests are run.
- If no such test project (or no owning `.csproj`) is found, `mutate4csharp` **fails fast with exit
  `2`** — it never silently reports success with no coverage.
- Only **unit tests** run: a test counts as a unit test if its `[Trait("type", …)]` is `UnitTests`,
  `Unit`, or absent; tests marked `[Trait("type", "IntegrationTests")]` are excluded.

## Test Tags

The default test command is:

```text
dotnet test <Project>.Tests.csproj --collect:"XPlat Code Coverage" --filter "type!=IntegrationTests&Category!=no-mutate"
```

- `type!=IntegrationTests` keeps unit tests (`type` = `UnitTests`, `Unit`, or untagged) and excludes
  integration tests.
- `Category!=no-mutate` excludes xUnit tests tagged `[Trait("Category", "no-mutate")]` — useful for
  tests that invoke mutation tools directly, recursively start `dotnet`/coverage, or are too
  expensive to run in every mutant cycle.

Use `--test-command` to override the whole command with a different test-selection strategy.

## Current Mutation Set

The tool currently mutates:

- boolean literals: `true` <-> `false`
- equality and comparison: `==`, `!=`, `<`, `<=`, `>`, `>=`
- arithmetic: `+` <-> `-`, `*` <-> `/`
- conditional boolean operators: `&&` <-> `||`
- unary operators: `!expr` -> `expr`, `-expr` -> `expr`
- integer constants: `0` <-> `1`
- reference-valued rvalues: replace with `null`

Mutation discovery is AST-based, so comments, string literals, char literals, and generic angle
brackets are not treated as mutation sites.

## Output

Typical output looks like this:

```text
Baseline tests passed in 4666 ms.
Total mutation sites: 12
Covered mutation sites: 3
Uncovered mutation sites: 1
Changed mutation sites: 2
Manifest exists: true
Module hash changed: true
Differential surface area: 1
Manifest-violating surface area: 1
WARNING: Found 72 mutations. Consider splitting this module.
KILLED src/Demo/Flag.cs:5 replace true with false (4686 ms)
UNCOVERED src/Demo/Flag.cs:12 replace == with !=
Coverage: 1 uncovered sites skipped.
Summary: 1 killed, 0 survived, 1 total.
```

The differential diagnostics block is printed before worker execution. It reports:

- total mutation sites discovered in the file
- covered mutation sites selected for execution
- uncovered mutation sites skipped by coverage
- changed mutation sites selected by differential analysis
- whether an embedded manifest exists
- whether the module hash changed relative to the manifest
- differential surface area
- manifest-violating surface area

`Differential surface area` counts mutations in scopes that were not registered in the manifest.
`Manifest-violating surface area` counts mutations in scopes that were registered but whose semantic
hash changed.

Exit codes:

- `0`: all executed mutants were killed, or there were no covered sites to run
- `1`: command-line usage error
- `2`: baseline tests failed
- `3`: at least one mutant survived

## Build

From the repository root:

```bash
dotnet build mutate4csharp.sln --configuration Release
dotnet test mutate4csharp.sln --configuration Release
```

`dotnet build` compiles the tool. `dotnet test` runs the fast unit suite; the Maven-analog integration
tests that exercise coverage generation and real `dotnet test` execution against temporary sample
projects are tagged `[Trait("Category", "Integration")]` and run with the full suite.

## Workflow Recommendation

If you have a batch of mutation runs to execute in the same project, let the first run generate fresh
coverage and then use `--reuse-coverage` for the remaining runs.

```bash
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/First.cs
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/Second.cs --reuse-coverage
dotnet run --project src/Mutate4CSharp -c Release -- src/Demo/Third.cs --reuse-coverage
```
