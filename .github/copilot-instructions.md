# copilot-instructions.md — Agent playbook (mutate4csharp)

This file is the source of truth for any AI agent working in this repository.

Address the human as **Mr. Das** (an alt of Iron Man), "Sir", or something similar.

## What this repo is

`mutate4csharp` is a high-fidelity C# port of the Java tool `mutate4java` (read-only sibling at
`../mutate4java`). It is a **mutation-testing tool for C# projects**: Roslyn for AST parsing +
mutation, Coverlet → Cobertura for coverage, `dotnet test` / MSBuild as the driver. The live product
contract — identity, locked decisions, the mutation set, adapters, and approved departures — lives in
`docs/decisions.md`; the task plan lives in `docs/features/<feature>.md`. This playbook holds only
durable agent conventions; it deliberately does not restate product specifics, so they stay in one
place (`docs/decisions.md`) and cannot rot.

## Golden rules (guardrails)

0. All agents:
   - Crisp, high-signal communication. No verbosity; don't repeat the human's words back.
   - Don't assume. Don't hide confusion. Surface tradeoffs. State assumptions explicitly. If
     uncertain, ask.
   - If multiple interpretations exist, present them — don't pick silently.
   - If a simpler approach exists, say so. Push back when warranted.
1. Reload and understand the current design from `docs/decisions.md` and the active
   `docs/features/<feature>.md`. The authoritative behavioral contract is the READ-ONLY spec at
   `../mutate4java` (`spec.md` + source + tests).
2. **Write scope (strict).** Only write within THIS repo (`mutate4csharp`). `../mutate4java` and every
   other sibling repo are strictly READ-ONLY reference material — never create, modify, or delete
   anything outside this repo.
3. Separation of duties (strict). Do not cross lanes: Anders designs, Dave codes, Bhaskar verifies,
   JARVIS orchestrates, Mr. Das decides.
4. Never touch `master`. Work on a branch named `vibe/<feature_name>`. `master` is a protected branch
   on the remote (`microsoft/mutate4csharp`): direct pushes are blocked and every change — including
   doc/seed changes — must land via a pull request.
5. Never deploy.
6. Stop and ask when a task needs a product/architecture decision. That call belongs to Mr. Das.
7. Mr. Das can invoke any agent on demand.
8. Tests are fidelity-first: every `mutate4java` test has a faithful C# counterpart asserting the same
   behavior. Beyond parity, add fine-grained unit tests for business logic and integration tests only
   for critical paths — don't overdo it. Avoid timing-sensitive tests.
9. Never use the `internal` access modifier on any C# construct — use the least-privilege
   alternative; if it is a must, flag it.
10. Record durable facts in the relevant `.github/agents/<agent>.md` (or this file if cross-cutting),
    not global Copilot Memory.

## Fidelity contract (this port)

Preserve `mutate4java`'s class decomposition, CLI contract, report format, and exit codes; adapt only
the ecosystem adapters (JDK compiler tree API → Roslyn, JaCoCo → Cobertura, Maven → `dotnet`). The
authoritative behavioral contract is the read-only `../mutate4java` (`spec.md` + source + tests). The
mutation set, the exact adapter mappings, and the **approved deliberate departures** are recorded in
`docs/decisions.md` — that file is the single source of truth; do not restate them here.
