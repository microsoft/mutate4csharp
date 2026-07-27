# Design decisions — mutate4csharp

Locked decisions for the C# port of `mutate4java`. Source of truth alongside `docs/features/`. The
authoritative behavioral contract is the read-only `../mutate4java` (`spec.md` + source + tests).

## Product intent

- A **mutation-testing tool for C# projects** — the C# member of the `mutate4*` family (`mutate4clj`
  → `mutate4java` → `mutate4csharp`). It targets one `.cs` file, discovers AST mutation sites, runs
  the owning project's unit tests per mutant, and reports killed / survived / uncovered mutants with
  an embedded differential manifest. Ecosystem: Roslyn (parse + mutation), Coverlet → Cobertura
  (coverage), `dotnet test` / MSBuild (driver).
- **Faithful 1:1 port** of `mutate4java`: class decomposition, the mutation set, CLI contract, report
  format, and exit codes are preserved. Only the ecosystem adapters and the approved deliberate
  departures (below) change.
- **Test fidelity:** every `mutate4java` test gets a faithful C# counterpart asserting the same
  behavior — except where an approved departure changes it.
- **OSS libraries (resolved):** the only new runtime dependency is **Roslyn**
  (`Microsoft.CodeAnalysis.CSharp`). Coverage via **coverlet.collector** (`XPlat Code Coverage`);
  tests **xUnit**; assertions **FluentAssertions** pinned `[7.0.0,8.0.0)` (v8 is commercial); no JSON
  dependency; process / hashing / XML via the BCL.
- **Idiomatic (resolved):** the adopt-now baseline below. Stryker.NET is **rejected** as the engine —
  a different product that cannot reproduce the manifest / differential / scan / exact strings / exit
  codes.

## Locked choices

| Area | Decision | Notes |
|---|---|---|
| Engine | **O1** — port the bespoke engine onto Roslyn | Stryker.NET rejected |
| Analysis target | one C# `.cs` file | Roslyn syntax tree + `SemanticModel` |
| Namespace | `Microsoft.Mutate4CSharp` | `AssemblyName`/`RootNamespace` = `Microsoft.<Project>`; sub-namespaces mirror the Java packages |
| TFM | `net8.0` | manual base64url helper (net9 `Base64Url` not used) |
| Layout | **single exe** `Mutate4CSharp` + `Mutate4CSharp.Tests` | tests reference the exe assembly |
| Parser | Roslyn (`Microsoft.CodeAnalysis.CSharp`) | single-file `CSharpCompilation` + `SemanticModel` for numeric/reference typing |
| Coverage | Coverlet → **Cobertura** via `--collect:"XPlat Code Coverage"` | newest `coverage.cobertura.xml` under results dir; `hits>0` = covered |
| Test framework | **xUnit** + `Microsoft.NET.Test.Sdk` + `coverlet.collector` | already committed in `Mutate4CSharp.Tests.Common.targets` |
| Assertions | **FluentAssertions** `[7.0.0,8.0.0)` | lock file enforces the pin (v8 is commercial) |
| Module root | **`<Project>.Tests` / `<Project>.UnitTests`** convention | see below; fail-fast exit `2` if absent |
| Helper visibility | **`public`** by default (`file` where trivially single-file) | guardrail #9: never `internal` |
| Manifest marker | `/* mutate4csharp-manifest … */`, `version=1` | wider `kind` vocabulary (DD1) |

## Deliberate departures from mutate4java (approved by Mr. Das)

Default stance is **zero** behavioral departures; the CRAP-era departures do **not** apply (this is a
mutation tool, not a complexity/CRAP analyzer). The following three are approved:

1. **DD1 — C#-specific manifest scope kinds.** Java's 3 kinds (`class/method/field`) widen to a C#
   taxonomy (type flavors + `constructor/finalizer/operator/conversion-operator/local-function/
   property/accessor/indexer/event/enum-member`), with per-accessor granularity. Manifest **format
   and `version=1` are unchanged** — only the `kind` value set widens. No report-string or exit-code
   change.
2. **DD2 — Fail-fast `exit 2`** when (a) no `<Project>.Tests`/`<Project>.UnitTests` (or owning
   `.csproj`) resolves, or (b) the baseline executes **zero** unit tests. This is **in addition to** —
   not a replacement for — mutate4java's faithful "green baseline + all sites uncovered → exit 0",
   which is **kept**. ("All uncovered but tests ran" → 0; "no test project / zero tests" → 2.)
3. **DD3 — Unit-only, single-project test scoping.** Run only `<Project>.Tests|.UnitTests` with the
   filter `type!=IntegrationTests&Category!=no-mutate`, replacing mutate4java's whole-owning-module
   `mvn test -DexcludeTags=no-mutate`. `--test-command` still fully overrides (coverage → allCovered
   per spec §9).

Exit code `2` is therefore **broadened** to "baseline failed **OR** no unit-test project **OR** zero
unit tests executed" — three sub-reasons documented under one code, keeping the `0/1/2/3` contract.
All stdout report strings are unchanged; DD2 adds **stderr** lines only.

## Supported mutation set (faithful — spec §6.1)

One mutation site per (AST-based; comments, string/char literals, generic `<>`, and manifest content
are excluded):

- boolean literals `true` ↔ `false`
- equality / comparison `== != < <= > >=`
- arithmetic `+` ↔ `-`, `*` ↔ `/` (`+` is numeric-only — no string-concat mutation)
- conditional boolean `&&` ↔ `||`
- unary removal `!expr → expr`, `-expr → expr`
- integer constants `0` ↔ `1`
- reference-valued rvalues → `null` (return / initializer / assignment RHS; not call arguments)

Numeric-vs-reference decisions use the resolved `SemanticModel` (single-file `CSharpCompilation` with
default framework references); C# value types (structs/enums) are non-reference — the faithful analog
of Java primitives.

## Manifest scope-kind taxonomy (DD1)

Scope id = `"<kind>:<prefix>#<detail>:<startLine>"`. `prefix` = the enclosing **type**-name stack
(outer→inner, including the type itself) joined by `.` — members do **not** push onto the prefix
(faithful to Java, which stacks only class names). `semanticHash` = SHA-256 hex of the declaration
node's source text; `startLine`/`endLine` from Roslyn line mapping. `addScope` de-dups by id.

- **Type kinds (push prefix):** `class struct record record-struct interface enum delegate`.
- **Member kinds (scopes, no prefix push):** `method constructor finalizer operator
  conversion-operator local-function property accessor indexer field event enum-member`.
- **Details:** method `Name(paramCount)`; ctor `ctor(n)` / static `cctor(0)`; finalizer
  `finalizer(0)`; operator `operator<Op>(n)`; conversion `implicit <T>(1)` / `explicit <T>(1)`;
  accessor `<Owner>.get|set|init|add|remove` (only when the accessor has a body); indexer `this[](n)`;
  field one scope per declarator (`int a, b;` → two); enum member `Name`.
- **Not scopes:** namespaces (and never a prefix component), lambdas / anonymous methods, local
  variables / parameters, using / attribute / statement nodes, compiler-generated members.
- **Fallback:** `file:<filename>` (top-level statements / outside any declaration).

## Module-root + test-selection convention

- **`<Project>` derivation:** ascend from the target `.cs` file to the nearest `.csproj`; `<Project>`
  = its file name without extension (= `MSBuildProjectName`). No owning `.csproj` up to the workspace
  root → **exit 2**.
- **Test-project discovery:** find `<Project>.Tests.csproj` or `<Project>.UnitTests.csproj` whose
  `<ProjectReference>` closure includes `<Project>.csproj` (validates the mapping in mono-repos).
  Tie-break: sibling → under a `tests/` dir → nearest by path; `.Tests` over `.UnitTests`. None found
  → **exit 2**.
- **Default test command:** `dotnet test <Project>.Tests.csproj --collect:"XPlat Code Coverage"
  --filter "type!=IntegrationTests&Category!=no-mutate" --results-directory <dir> --logger trx`.
  Unit = `type` ∈ {`UnitTests`, `Unit`} or no `type` trait; `IntegrationTests` excluded (VSTest treats
  an absent property as `!=` any value); `Category!=no-mutate` is the faithful port of
  `-DexcludeTags=no-mutate`. `--test-command` overrides entirely (then coverage = allCovered).
- **Baseline + coverage** come from one `dotnet test --collect` call; the runner reads the newest
  `coverage.cobertura.xml` under the results dir. `--reuse-coverage` reuses it; missing → continue
  without filtering (spec §9).
- **Coverage key (A4):** resolve each Cobertura `<class filename>` against the report `<sources>` to
  an absolute path and compare case-insensitively to the target site's absolute path; covered iff the
  `<line … hits=H>` has `H>0`. (Replaces JaCoCo package-path keying / `SourcePathNormalizer`.)
- **Worker isolation:** copy the **repo root** (workspace root) excluding `bin/ obj/ .git/ .vs/
  TestResults/` and the worker base; the worker base lives under
  `%TEMP%/mutate4csharp/run-<guid>/worker-N`; the mutated file lives at the copy-root-relative path;
  `dotnet test` runs with cwd = worker root, targeting the copy-relative test project. (A
  ProjectReference-closure copy is a deferred optimization — see the feature file.)

## Idiomatic policy

- **Adopt-now baseline:** nullable enable; `record` value types for `model/`; `InvariantCulture` for
  all rendered numbers; explicit `"\n"` in report / scan / manifest output; async stdout/stderr drain
  + `Process.Kill(entireProcessTree: true)`; `IReadOnlyList<T>` returns; `Environment.ProcessorCount`
  for default max-workers (`max(1, N/2)`); ordinal string comparisons; single-file `CSharpCompilation`
  for the semantic model; file-scoped namespaces + `_camelCase` privates + `I`-prefixed interfaces.
- **Static vs instance (CA1822):** `CA1822` is globally disabled in `.editorconfig` (alongside
  `CA1515`/`CA2007`) — a purity/perf rule that fights deliberate app-level DI composition and never
  flags a bug. **Mirror `mutate4java` per member:** port Java `static` members as `static` (e.g.
  `ManifestValueCodec.encode/decode`), and keep Java instance-composed helpers as **instances**
  (preserving the ctor/field-injection composition graph). Never staticize a stateless helper merely
  to satisfy the analyzer.
- **Ordinal sorting (fidelity landmine):** any Java `String.compareTo` / natural-order sort maps to
  `StringComparer.Ordinal` (UTF-16 ordinal) — never a culture/invariant comparer. A culture comparer
  would silently reorder and change hash-affecting order (manifest module hash, and later
  selection/report ordering). Applies to all string ordering across the port.
- **Greenlit engineering (behavior-neutral):** P1 single Roslyn walk (sites + scopes together); P2
  `record struct` for the tiny hot keys (`CoverageSite`, `ScopeRef`); P3 `Channel<MutationJob>` worker
  pool with identical scheduling semantics.
- **Declined:** `System.CommandLine` (the exact error strings + conflict rules are asserted verbatim
  by `CliArgumentsParserTest`); Stryker.NET as the engine.

## Timeouts, workers, exit codes (faithful)

- Mutant timeout = `max(1000ms, max(1, baselineDuration) * timeoutFactor)`; default factor `10`; a
  timeout → **KILLED (timeout)**, sentinel exit `124`.
- Default max-workers = `max(1, ProcessorCount/2)`; `--max-workers` caps it.
- Exit codes: `0` success / all killed / all-uncovered / scan / manifest-update; `1` usage error; `2`
  baseline failed **or** no unit-test project **or** zero unit tests executed; `3` ≥1 survivor.

## Environment / CI

- CI is GitHub Actions (`.github/workflows/ci.yml`): restore → build (Release, warnings-as-errors) →
  test with coverage. `master` is **remote-protected** — every change lands via PR.
- Kept from the scaffold: `.editorconfig`; analyzers (NetAnalyzers / StyleCop / BannedApi);
  warnings-as-errors in Release; `global.json`; `nuget.config` (nuget.org only); the agentic-loop
  files; `meta-design` + feature template; the `build-test` skills.
