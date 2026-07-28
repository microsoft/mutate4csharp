# Feature: mutate4csharp — faithful C# port of mutate4java
**Branch:** vibe/mutate4csharp-port
**Status:** Planning

## Requirements

Convert the read-only Java tool `mutate4java` (`../mutate4java`) into its C# equivalent,
`mutate4csharp`, with **high fidelity**. The tool is a **mutation-testing tool**: it targets one C#
`.cs` file, discovers AST mutation sites, runs the owning project's unit tests per mutant, and reports
killed / survived / uncovered mutants with an embedded differential manifest. Preserve the class
decomposition, mutation set, original formulas / heuristics, CLI contract, report format, and exit
codes; adapt only the ecosystem adapters. Every Java test gets a faithful C# counterpart.

## Design Options (Ox)

### O1 — Faithful port of the bespoke engine onto Roslyn — **chosen**
- Re-implement mutate4java's class decomposition in C#; Roslyn replaces the JDK tree API (parse +
  semantic model); source-text splicing, manifest, differential selection, coverage filtering, the
  worker model, reporting, and exit codes are ported 1:1. Only new runtime OSS dependency: Roslyn.
- Pros: the only approach consistent with the locked faithful-port intent; full control of the
  mutation set and the exact output strings the tests assert. Cons: we own the (small) mutation logic;
  requires a single-file `CSharpCompilation` for typing.

### O2 — Reuse Stryker.NET as the engine — **rejected**
- A different product (own CLI, mutation set, reports, whole-project model); cannot reproduce the
  embedded-manifest differential selection, scan mode, exact `KILLED/SURVIVED/UNCOVERED` strings, or
  exit codes `0/1/2/3`.

### O3 — Text/regex mutation (no Roslyn) — **rejected**
- Cannot faithfully exclude strings / char / comments / generic `<>` or do numeric-vs-reference
  typing.

**Ruled by Mr. Das: O1.**

## Slices (Sx)

| Slice | Outcome (independently builds + tests green) | Depends on |
|-------|----------------------------------------------|------------|
| S1 | Solution + `Mutate4CSharp` exe + `Mutate4CSharp.Tests`; `model/` records; `manifest/` codec + hashing; `cli/` parser + validators + usage; `Main` exit plumbing (pure, no Roslyn/process) | – |
| S2 | Roslyn analysis: `MutationCatalog`, AST scanner, scope tracker (DD1 taxonomy), type predicates, `SourceAnalysis` + module/scope hashing | S1 |
| S3 | Selection + coverage parse + reporting (pure): differential selector, line filter, coverage filter, scan mode/formatter, report formatter, Cobertura parser, project/module resolution, progress formatters, source/changed-file finders | S1, S2 |
| S4 | Exec: process/test executor (async drain, tree-kill, `dotnet test`), coverage runner (coverlet → Cobertura), workspace copy/isolation, parallel worker pool + cleanup | S1 |
| S5 | Engine orchestration + CLI wiring: `CliExecution(+Factory)`, `ExecutionContext`, `BaselineRunner`, `MutationRunPlanner`, `MutationExecution`, `ExecutionOutcomeWriter`, `ManifestWriter`, `CliApplication` | S2, S3, S4 |
| S6 | Integration/acceptance tier (real .NET SDK + coverlet), gated `[Trait("type","IntegrationTests")]` | S5 |
| S7 | **Independent evaluation gate:** a fresh reviewer on model `gpt-5.6-sol`, briefed with **Mr. Das's original requirements only** (no design docs, decisions, or departures), assesses the finished port and reports pass / gaps | S6 |
| S8 | **Real-world dogfooding ("rubber meets the road"):** once the whole port is merged + pushed, run the finished `mutate4csharp` against three sibling C# repos — `../crap4csharp`, this repo (`mutate4csharp`, self-mutation), and `../dry4csharp` — and capture mutation results + any tool defects | S7 (post-merge) |

**Critical path:** S1 → S2 → S3 → S5 → S6 → S7 → S8. After S1, Track A (S2 → S3) runs concurrently
with Track B (S4); S5 is the join. Within S3, the Cobertura parser (T8) and project/module resolution
(T9) can start right after S1. **S7 (independent evaluation) then S8 (real-world dogfooding) run last —
S8 only once the entire port is merged and pushed.**

## Tasks (Tx)

| # | Slice | Task | Faithful test counterpart(s) |
|---|-------|------|------------------------------|
| T1 | S1 | Create exe + test projects, reference `Mutate4CSharp.*.targets`, add Roslyn pkg, lock files; empty build + `dotnet test` run | build green |
| T2 | S1 | Port `model/` records (`IReadOnlyList`, nullable) | (compile; exercised later) |
| T3 | S1 | Port `manifest/` (boundary, parser, serializer, base64url, SHA-256, `ManifestSupport`) | `ManifestSupportTest` |
| T4 | S1 | Port `cli/` parser + `SelectionFlagValidator` + `UsageText` + `Main` exit plumbing | `CliArgumentsParserTest` (34), `MainTest` |
| T5 | S2 | Roslyn single-file compiler + `SemanticModel` (default refs) | (via T7) |
| T6 | S2 | AST scanner + site factory + binary operator + type predicates (Roslyn) | `MutationCatalogTest` (5) |
| T7 | S2 | Scope tracker + scope factory (**DD1 taxonomy**) + `MutationCatalog.analyze/discover` | `MutationCatalogTest`, manifest round-trip |
| T8 | S3 | Cobertura line-coverage parser (+ empty report) + `CoverageReport.covers/allCovered` | `CoberturaLineCoverageParserTest` (← `JacocoLineCoverageParserTest`) |
| T9 | S3 | Project/module resolution: `<Project>.Tests\|.UnitTests` discovery + `<Project>` derivation + path normalization | `SourceFileFinderTest`, `ChangedFileDetectorTest` |
| T10 | S3 | Differential selector + line filter + coverage filter + scan mode/formatter + report formatter + execution messages | (asserted via S5 `CliApplicationTest`) |
| T11 | S4 | Process/test executor: async drain, tree-kill, shell override, unit filter | `ProcessCommandExecutorTest` |
| T12 | S4 | Coverage runner (coverlet → Cobertura): reuse path, newest-file-under-results-dir, TRX count for DD2(b) | (injected executor) |
| T13 | S4 | Workspace copy/isolation (repo-root copy; `bin/obj/.git/.vs/TestResults` excluded; temp worker base) | `CopiedWorkspaceManagerTest` |
| T14 | S4 | Parallel worker pool + isolated worker + workspaces + retry cleanup | `ParallelWorkerPoolTest`, `WorkerWorkspacesTest` |
| T15 | S5 | Wire engine + `CliApplication`; **DD2 fail-fast** (no test project / zero tests → exit 2) | `CliApplicationTest` (25) |
| T16 | S6 | Coverage runner IT on a real `dotnet` + coverlet sample | integration |
| T17 | S6 | Process/test executor IT on real `dotnet test` | integration |
| T18 | S6 | `MainAcceptanceTest` — end-to-end .NET sample projects; `TestProjectFactory` (csproj + xUnit + `type`/`no-mutate` traits) | acceptance |
| T19 | S7 | **Independent evaluation:** launch a `gpt-5.6-sol` agent whose entire brief is the **`## Requirements` section of this feature doc, verbatim** — and nothing else (not the rest of this file, not `decisions.md`, not DD1–DD3, not any design rationale). It independently inspects `mutate4csharp` (and may consult the read-only `mutate4java` the requirements name) and reports whether the port meets the requirements. | independent report |
| T20 | S8 | **Dogfood on real code:** run the built `mutate4csharp` against representative covered `.cs` files in `../crap4csharp`, `mutate4csharp` (self), and `../dry4csharp` (each needs a `<Project>.Tests\|.UnitTests` with unit tests); capture killed / survived / uncovered + any tool defects and report to Mr. Das. | dogfood results |

## Risks (Rx)

- **R-A (High)** — test-project discovery fragility (naming breaks for mono-repos / plural suites) →
  ProjectReference validation to `<Project>.csproj`, deterministic tie-break, **exit 2** with a
  precise message rather than running the wrong suite.
- **R-B (Med)** — project-under-test ↔ test-project mapping (a `.Tests` referencing several
  production projects) → ProjectReference validation; "target file absent from Cobertura" = all
  uncovered (exit 0), not an error.
- **R-C (High)** — worker-copy scope masks survivors (a missing `ProjectReference`/`Directory.Build.*`/
  `global.json` → mutant build fails → falsely scored KILLED) → default `copyRoot` = repo root with
  `bin/obj/.git/.vs/TestResults` excluded; a validated ProjectReference-closure copy is deferred (D1).
- **R-D (High)** — A4 false-uncovered from Cobertura `filename` variance (relative/absolute,
  deterministic remap, `<sources>` bases, casing) → resolve against `<sources>`, absolute
  case-insensitive compare, plus an integration test asserting a known-covered line is `covered`.
- **R-E (Med)** — "zero tests executed" detection (`dotnet test` exits 0 on no match) → `--logger trx`,
  read counters, fail-closed to exit 2.
- **R-F (Low)** — legitimate all-uncovered surprise (a `.Tests` that doesn't exercise the target) →
  faithful exit 0 with `Coverage: N uncovered sites skipped.`; documentation note, not a bug.

## Assumptions (Ax)

- **A1** — Namespace `Microsoft.Mutate4CSharp`; sub-namespaces mirror the Java packages.
- **A2** — Test tiering: unit tests by default; integration/acceptance gated
  `[Trait("type","IntegrationTests")]`; CI runs both.
- **A3** — `dotnet test` is the driver; `coverlet.collector` is available (committed); the target
  project's tests wire coverage via `--collect`.
- **A4** — Single-file target: exactly one `.cs`; reject dir / non-`.cs` / zero / multiple (spec §4).
- **A5** — Exit codes `0/1/2/3` (with `2` broadened) and all report strings ported verbatim.
- **A6** — Helper tier `public` by default (`file` where trivially single-file); guardrail #9 honored.

## Deferrals (Dx)

- **D1** — ProjectReference-closure worker copy (vs the repo-root copy default).
- **D2** — Whole-directory / whole-solution mutation (spec §15 non-goal).
- **D3** — Stable machine-readable / JSON report (spec §15).
- **D4** — Non-`dotnet` build systems (spec §15).
- **D5** — External coverage handling under `--test-command` (spec §9: sites treated as covered).
- **D6** — C#-only construct mutations beyond the ported set (null-coalescing, pattern ops, switch
  arms) — flagged superset only if Mr. Das requests it.

## Notes & Decisions

- Full locked decisions, the DD1 scope-kind taxonomy, the module/test convention, and the exit-code
  semantics are in `docs/decisions.md`.
- Namespace `Microsoft.Mutate4CSharp`; **single-exe** layout; `net8.0`; manifest marker
  `mutate4csharp-manifest`.
- Seeded on `master` **via PR** (master is remote-protected). Implementation runs on
  `vibe/mutate4csharp-port`; JARVIS creates that branch and drives T1 → T18 via Dave/Bhaskar, with
  Anders review per task.
- **Independent evaluation (S7/T19):** at Mr. Das's instruction, once the entire port is complete it is
  evaluated by an independent `gpt-5.6-sol` agent whose entire brief is the **`## Requirements` section
  of this feature doc, verbatim, and nothing else** — deliberately withheld are the rest of this file,
  `docs/decisions.md`, the DD1–DD3 departures, and all design rationale — so the judgment is unbiased.
  Intended consequence: the evaluator is unaware the departures were sanctioned, so it may report
  DD1–DD3 as deviations; that raw signal goes to Mr. Das.
- **Real-world dogfooding (S8/T20):** once the entire port is merged and pushed, the finished tool is
  run against three sibling C# repos — `../crap4csharp`, this repo (`mutate4csharp`, self-mutation),
  and `../dry4csharp` — the actual rubber-meets-the-road validation. Each target must be a buildable C#
  project with unit tests (`<Project>.Tests`/`.UnitTests`). ⚠️ Write-scope: the tool writes an embedded
  manifest into a target's source on clean runs and mutates worker copies — dogfooding the **external**
  siblings should run against copies (or with explicit authorization) to respect the "write only within
  this repo" guardrail; self-mutation of `mutate4csharp` is in scope. Surviving mutants + tool defects
  report back to Mr. Das.
- **Carry-forward — T15 wiring (Anders, T4 review):** keep `Program` as the .NET entry point — wire
  `Program.Main(args)` → `Cli.Main.run(...)` → `CliApplication.execute` + `ExitIfNeeded`; do **not**
  make `Cli.Main` an entry point. Treat `ParseOutcome.ExitCode == -1` as "continue with `Arguments`",
  distinct from `0` (help success) — the faithful magic-number contract. Also `CliExecution` calls
  `ScanMode.Render(parsed, context.SourceFile, context.Analysis)` (pure `ScanMode` takes DTOs, not
  `ExecutionContext`, to avoid a Selection→Engine upward dependency).
- **Carry-forward — S3/selection (Anders, T7 review):** the synthetic `file:` fallback scope is
  stamped on `CurrentScope` but has **no** manifest entry; when the differential selector consumes
  `ScopeId`, a `file:`-scoped site (no matching manifest scope) must degrade gracefully
  (conservative-select), mirroring Java's tolerance of its `"unknown"` default.
- **Carry-forward — T12/T16 coverage reconciliation (Anders, T8 review):** `NormalizeSourcePath`
  (`GetFullPath`) does NOT undo deterministic-build path remapping (`DeterministicSourcePaths`/`PathMap`/
  SourceLink → `/_/…` placeholders), symlinks, or a relative-vs-CWD basis — any of which makes
  coverlet's `<source>` fail to reconcile with on-disk site paths → all-uncovered. T12's coverage run
  should control `DeterministicSourcePaths` (a real on-disk `<source>`); **T16 must be an end-to-end
  reconciliation test on a REAL coverlet report** (not synthetic XML), asserting a known-covered line's
  key is present. T10 must call `NormalizeSourcePath(MutationSite.File)` exactly once at the boundary.
- **Carry-forward — T12/T15 DD3 cwd (Anders, T11 review, CRITICAL):** the default test command is
  project-less (faithful to Java `mvn test`), but `dotnet test` binds to the directory's project/
  **solution** — a `.sln` runs the WHOLE solution, silently violating DD3 (wrong baseline + coverage).
  T12/T15 MUST invoke the executor with **working directory = `Path.GetDirectoryName(TestProjectFile)`**
  (or append the explicit `<Project>.Tests.csproj`) so `dotnet test` binds to exactly that one project.
  Also carry the CA1849 pattern (`await Task.WhenAll(...)` + index, not `.Result`) into T12/T14.
- **Carry-forward — T15 coverage gating (Anders, T12 review, CRITICAL):** pass `CoverageRun.Report`
  (even the empty reuse-missing report) **straight** to the planner (like Java `CliExecution`); do
  **NOT** gate the coverage filter on `ReportAvailable`. Java runs **zero** mutants on reuse-missing
  (empty report → covered=∅ → all `UNCOVERED`, summary `0 total`); `ReportAvailable` drives **only**
  `printReuseMessage`. Apply the DD2b `executed==0 → exit 2` gate **only** on the normal coverage path
  (reuse + `--test-command` are exempt — no fresh TRX). In the reuse / `--test-command` baseline paths,
  run the project-less executor with **cwd = test-project dir** so it still binds to `<Project>.Tests`.
- **Carry-forward — T14/T15 mutated-file base (Anders, T13 review):** relativize `MutationSite.File`
  against **`copyRoot` == repo root** (the workspace-copy base) so `workerRoot / relativePath` lands on
  the copied file; T14 job construction + `CreateWorkerWorkspaces(copyRoot, …)` must share the repo
  root as the single base. T14 keeps `WorkerWorkspaces` a **`sealed class`** (resource handle, not a
  value — Anders' ruling).
- **Carry-forward — T15 worker test scoping (Anders, T14 review, CRITICAL):** the mutation-run executor
  must target the resolved test project by a **repo-relative** path (e.g. `dotnet test <TestProjRelPath>
  --filter …`) invoked with **cwd = worker root**, so each worker tests its **mutated copy**
  (`workerRoot/<relpath>`). An ABSOLUTE original-repo path would test the un-mutated original → every
  mutant silently **SURVIVES** (catastrophic false-negative). `--test-command` path exempt. Also:
  `MutationExecution` `using`s BOTH the pool AND `WorkerWorkspaces` (cleanup on fault); worker count =
  `Max(1, Min(jobs.Count, maxWorkers))`, passed to `CreateWorkerWorkspaces(copyRoot=repoRoot, count)`.
- **Carry-forward — T17 robustness (Anders, T14 review, proposal-only):** `WorkerWorkspaces.TryDelete`
  could also catch `UnauthorizedAccessException` (Java's `AccessDeniedException` is an `IOException`;
  .NET's is not) → wrap as retryable; and blocking workers could use `TaskCreationOptions.LongRunning`
  (closer to `newFixedThreadPool`). Non-blocking niceties.
- **`.gitignore` convention (Anders, T8 review):** anchor project-specific, root-scoped output dir
  names (`/coverage/`; `/artifacts/` if the .NET-8 artifacts layout is later adopted); keep genuine
  build-output names (`bin`/`obj`/`Debug`/`Release`) depth-agnostic (unanchored). No proactive sweep.

## Progress

Per-task log (Dave implements → Bhaskar verifies → Anders reviews → JARVIS commits). See `git log` for commit SHAs.

| Task | Status | Dave | Bhaskar | Anders |
|------|--------|------|---------|--------|
| T1 | Done | ✅ | ✅ | ✅ |
| T2 | Done | ✅ | ✅ | ✅ |
| T3 | Done | ✅ | ✅ | ✅ |
| T4 | Done | ✅ | ✅ | ✅ |
| T5 | Done | ✅ | ✅ | ✅ |
| T6 | Done | ✅ | ✅ | ✅ |
| T7 | Done | ✅ | ✅ | ✅ |
| T8 | Done | ✅ | ✅ | ✅ |
| T9 | Done | ✅ | ✅ | ✅ |
| T10 | Done | ✅ | ✅ | ✅ |
| T11 | Done | ✅ | ✅ | ✅ |
| T12 | Done | ✅ | ✅ | ✅ |
| T13 | Done | ✅ | ✅ | ✅ |
| T14 | Done | ✅ | ✅ | ✅ |
| T15 | Done | ✅ | ✅ | ✅ |
| T16 | Done | ✅ | ✅ | ✅ |
| T17 | Done | ✅ | ✅ | ✅ |
| T18 | Done | ✅ | ✅ | ✅ |
| T19 | Done | ✅ | ✅ | ✅ |
| T20 | Dogfood ✅ (1 fidelity finding under adjudication) | — | ✅ | 🟡 |

**Slices:** S1 ✅ · S2 ✅ · S3 ✅ · S4 ✅ · S5 ✅ · **CI split (Option B) ✅** · **S6 (integration/acceptance) ✅** · **S7 (independent eval + remediation) ✅** · **S8 (dogfooding) 🟡 (re-dogfood pending)** — the entire port implementation + full test tier (unit + integration + acceptance) is **COMPLETE and remediated**. **T16 ✅** · **T17 ✅** (worker-scoping keystone) · **T18 ✅** (whole-tool black-box acceptance + `TestProjectFactory`) · **T19 ✅** (independent `gpt-5.6-sol` eval, fed only the Requirements). **S7 remediation — all 5 findings closed:** F1 `--test-command` cwd (`8cec4db`), F2 over-gen + F3/DD4 under-gen + F4 reuse-scoping (`1c21218`), F5 test-parity ADDs A1–A7 (`64507bb`). **S8 dogfood → DD5 fix** (implicit-usings null-mutation gap) + IT-lane serialization. Suite **223 (215 unit + 16 IT — serialized)**. **Remaining: re-dogfood to confirm DD5 closed the gap on real code → then port done.**

- **S8 dogfood outcome (T20, Bhaskar-executed on temp copies — guardrail #2 respected, no source repo written):** ran the built tool against `../crap4csharp`, self (`mutate4csharp`), `../dry4csharp` — **41 covered mutants, ALL 41 KILLED, 0 survived**, 1 uncovered correctly skipped; 9 `--scan` + 3 `--update-manifest`, all exit 0. **Zero crashes, zero false fail-fasts, coverage reconciliation held on real deterministic-path repos, worker-scoping sound (genuine kills, copies cleaned).** F2 (no spurious compound-assign null) + DD4 (value-type arrows self-exclude) validated on real code. **Production-usable.** Performance is heavy (inherent to mutation testing — per-mutant = repo copy + build + test run; use targeted/`--lines` runs). **Fidelity finding → resolved as DD5 (Mr. Das ruled (b)-narrow):** null-replacement was skipping import-dependent BCL generics (`ISet`/`List`/`SortedSet`) because the single-file `RoslynSourceCompiler` lacked the project's implicit/global usings → `TypeKind.Error` → excluded, whereas mutate4java's in-file imports resolve JDK types (so it *does* mutate stdlib returns — C# was stricter than Java). **DD5 fix:** inject the owning project's global/implicit usings as a context-only tree (usings lever only, references untouched → bounded to BCL, monotonic). Also serialized the coverage-collecting ITs (xUnit non-parallel collection) to remove a pre-existing collector-under-contention flake. A **targeted re-dogfood** will confirm `ISet`/`List` returns are now mutated on a real repo, closing S8.

- **S7 independent-eval outcome + remediation (T19):** the blind `gpt-5.6-sol` evaluator (fed ONLY the `## Requirements` section) returned "mostly functional, not fully conformant." Triage: most "non-conformance" flags were the **approved DD1/DD2/DD3 departures** (the eval was intentionally blind to them) — not defects. Five genuine findings, adjudicated by Anders against the Java oracle and ruled by Mr. Das:
  1. **`--test-command` cwd inconsistency (HIGH)** — baseline ran at test-project dir, workers at repo-root copy → **FIXED** (align baseline to workspace root; +2 unit +1 causal acceptance test).
  2. **Mutation-set OVER-generation (MEDIUM):** C# null-replaced compound-assignment RHS (`+= "#"` → null); Java's `visitAssignment` is simple-`=` only → **fix:** guard emit to `SimpleAssignmentExpression`, keep `base` recursion unconditional. *(mutation-set batch)*
  3. **Mutation-set UNDER-generation → DD4 (MEDIUM, Mr. Das RATIFIED):** expression-bodied returns (`=> expr`) got no null-replacement; block-bodied equivalents did → **fix:** scoped `VisitArrowExpressionClause` on value-returning arrow contexts (method/property-get/indexer-get/get-accessor/non-void local function/operator-conversion); **exclude** set/init/add/remove/ctor/finalizer; lambdas naturally excluded. *(mutation-set batch)*
  4. **Reuse baseline not explicitly project-scoped (LOW):** reuse path binds by cwd only → **fix:** apply explicit test-project target on the reuse baseline (DD3 consistency; cwd unchanged). *(mutation-set batch)*
  5. **Test-parity ADDs (MEDIUM):** A1 all-killed→exit-0 + manifest (+ mixed killed/uncovered); A2 baseline-red→exit-2; A3 `--update-manifest` on red→exit-0 (tests not run); A4 `ProcessTestCommandExecutorTests` (5 Java cases: configured cmd, `WithCommand` shell override, `WithTestProject` argv, timeout→124, output-on-failure); A5 consolidated multi-operator KILLED; A6 `--lines` e2e; **A7 real mutant-timeout — guarded IT (Mr. Das chose add).** *(test-parity batch)*

- **Done — T18 carry-forwards:** `TestProjectFactory` extracted (folds in `HermeticSample`; T16+T17 refactored onto it, no weakened assertions) ✅; version-drift guard added (`TestProjectFactoryVersionDriftTests`, fast unit) ✅; spawn-exe separate-drain + §13 byte-verbatim + multi-mutant ordering + full §14 (incl. 3 DD2 arms) ✅. `--test-command` acceptance arm **intentionally omitted** (T15 unit oracle + T17 keystone already pin it — Bhaskar + Anders concur).
- **Carry-forward — R-D flag necessity pin (Anders, T16 review, still OPEN, optional, non-blocking):** no test yet proves `-p:DeterministicSourcePaths=false` is *necessary* (the hermetic samples never set `ContinuousIntegrationBuild=true`, so the `/_/` remap never fires → the coverlet IT would stay green even if the flag were removed). A future variant writing `<ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>` and asserting reconciliation still holds (ideally failing without the flag) would pin it. Deferred — candidate hardening, not a port gap.
- **Doc-reconciliation carry-forwards (non-blocking, doc-only — Bhaskar re-flags):** (a) T12 reuse-message wording in `decisions.md` vs the Java empty-report impl; (b) T15 "test-parity note" — RESOLVED (the `## Test-parity ledger` section is present in `decisions.md`). Neither is a code defect.
- **Resolved — CI gating (Option B):** implemented + verified + reviewed + pushed (`ea46e67`); both CI steps green on Linux. See `decisions.md` › Environment/CI.
- **Carry-forward — T11 `ProcessCommandExecutor` (Anders, T9 review):** `ICommandExecutor.Run` takes
  **argv** (`command[0]` = exe) — spawn via `Process.StartInfo.FileName` + `ArgumentList`, NOT a shell
  string; **merge stderr into `Output`** (Java `redirectErrorStream(true)` analog); set `TimedOut` /
  `DurationMillis`.
