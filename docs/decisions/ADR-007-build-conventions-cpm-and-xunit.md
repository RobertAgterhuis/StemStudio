# ADR-007: .NET build conventions, Central Package Management and xUnit v3 on VSTest

- **Status**: Accepted
- **Date**: 2026-11-14
- **Decider**: architect agent, on the CTO brief

## Context

Five src and four test projects must share settings and versions; the brief asks for CPM, zero
warnings and coverlet. xUnit v3 4.x defaults to Microsoft.Testing.Platform (MTP), and the .NET 10
SDK refuses the VSTest target for MTP projects ("Testing with VSTest target is no longer supported").

## Decision

- `global.json`: SDK 10.0.401, `rollForward: latestFeature`.
- `Directory.Build.props`: `net10.0`, nullable, implicit usings, `TreatWarningsAsErrors`,
  `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, XML docs required in src (CS1591
  is an error); test projects (name ends with `.Tests`) get xUnit v3, Shouldly, NSubstitute and
  coverlet automatically, `OutputType=Exe` and **`IsTestingPlatformApplication=false`** so they run
  on VSTest through `xunit.runner.visualstudio`.
- `Directory.Packages.props` holds every NuGet version (EF Core/ASP.NET 10.0.15 line, not 11.x,
  which targets net11). Projects never specify versions.
- `.editorconfig` defines style and naming (file-scoped namespaces, braces, `_camelCase` private
  fields, PascalCase constants/static readonly) and relaxes CA1707/CA1515/CA1861 in tests.

## Consequences

- `dotnet test`, `--collect "XPlat Code Coverage"` and `-p:CollectCoverage=true` all work.
- Moving to MTP later means: drop `IsTestingPlatformApplication=false`, replace coverlet
  collector/msbuild with `coverlet.MTP`, set `"test": {"runner": "Microsoft.Testing.Platform"}`
  in `global.json`, and update CI commands (new ADR).
