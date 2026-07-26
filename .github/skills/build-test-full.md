---
name: build-and-test-full
description: Builds mutate4csharp and runs the full test suite including integration tests.
---

## Commands

Run from the repository root:

    dotnet build mutate4csharp.sln --configuration Release
    dotnet test mutate4csharp.sln --configuration Release

## Notes

Integration tests spawn real processes — `git`, the built mutate4csharp CLI, and `dotnet`. They require
`git` and the .NET SDK on `PATH`. There are **no secrets** to inject.

## Pass criteria

- Build succeeds with **zero warnings and zero errors** (warnings are errors in Release).
- All tests pass (unit + integration).
