---
name: MafAgent .NET Guidelines
description: "Use when editing C# source, .NET project files, tests, or build configuration in the MafAgent repository. Covers project structure, nullable code, testing, and validation."
applyTo: ["**/*.cs", "**/*.csproj", "Directory.Build.props"]
---

# MafAgent .NET Guidelines

- Target .NET 9 and preserve nullable reference types and implicit usings unless the task requires a deliberate change.
- Keep reusable application logic in `src/MafAgent.Core`; keep console-host concerns in `src/MafAgent.Console`; place behavior tests in `tests/MafAgent.Tests`.
- Treat warnings as errors. Do not suppress warnings or weaken nullable analysis to make a build pass; fix the underlying issue.
- Prefer small, focused changes that preserve existing public APIs and project references.
- Add or update focused xUnit tests for behavior changes in shared logic.
- Before finishing a code change, run `dotnet build --configuration Release` and `dotnet test --configuration Release --no-build` from the repository root.