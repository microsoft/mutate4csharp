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

**Critical path:** S1 → S2 → S3 → S5 → S6 → S7. After S1, Track A (S2 → S3) runs concurrently with
Track B (S4); S5 is the join. Within S3, the Cobertura parser (T8) and project/module resolution (T9)
can start right after S1. **S7 runs last — only once the entire port is complete.**

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
