---
name: build-and-test
description: Builds mutate4csharp and runs unit tests (excludes integration tests). Used for fast feedback.
---

## Commands

Run from the repository root:

    dotnet build mutate4csharp.sln --configuration Release
    dotnet test mutate4csharp.sln --configuration Release --filter "Category!=Integration"

## Pass criteria

- Build succeeds with **zero warnings and zero errors** (warnings are errors in Release).
- All unit tests pass.

Integration tests (real `git`, spawning the built CLI, spawning processes) are tagged
`[Trait("Category", "Integration")]` and excluded here; run `build-test-full` for those.
