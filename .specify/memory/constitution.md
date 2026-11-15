# StemStudio Constitution

## Core Principles

### I. Test-First, Measured Coverage (NON-NEGOTIABLE)

- Every task follows Red-Green-Refactor: a failing test is committed or shown before the
  implementation that makes it pass.
- Line AND branch coverage MUST be >= 90% per component (.NET API, .NET UI, Python worker,
  JS audio engine). CI fails below the threshold.
- Tooling: .NET uses xUnit + coverlet (Cobertura) and bUnit for Blazor components; Python uses
  pytest + pytest-cov; the JS engine uses Vitest with a mocked `AudioContext`; the main flow
  (import -> separate -> mix -> save -> reload) is covered by Playwright end-to-end tests.
- Heavy models (Demucs, librosa analysis, yt-dlp, spotDL, ffmpeg) sit behind interfaces so unit
  tests use fakes. Exactly one marked integration test (`@pytest.mark.integration`) runs the real
  separation model on a short audio fixture.
- A task is never "done" without green tests and coverage evidence in its hand-off note.

### II. Maintainability by Construction (SIG 4 stars or higher)

- Unit size: <= 15 lines of code per method/function where possible, never > 30.
- Unit complexity: cyclomatic complexity <= 5 per unit, never > 10.
- Duplication: no duplicated blocks of 6 or more lines.
- Unit interfacing: <= 4 parameters per unit (use parameter objects/records beyond that).
- Module coupling: clear layers, dependencies flow one way (UI -> API client -> API -> domain;
  API -> persistence; API -> worker contract), no cycles.
- Component balance: a small number of well-sized components (UI, API, domain, persistence,
  worker, audio engine).
- These limits are enforced by automated checks in CI (`tools/quality/`), not by convention.

### III. Contracts First

- Inter-component communication is defined by versioned contracts before implementation:
  OpenAPI 3.1 for the worker HTTP API and for the backend HTTP API, plus a typed JS interop
  surface for the audio engine.
- Implementations are verified against the contract (contract tests); breaking changes require an
  ADR and a contract version bump.
- Every public API (HTTP endpoint, public C# type/member, exported TS function, public Python
  function) is documented.

### IV. Open Source Only, Lawful Use

- Only open-source dependencies (code and libraries) with OSI-approved licences; no paid software
  or paid tiers. Every dependency and its licence is recorded in `docs/THIRD-PARTY-LICENSES.md`.
- **Model weights exception** (owner decision 2026-11-15, ADR-009): pretrained model weights MAY
  be used when their licence is non-commercial (e.g. CC BY-NC), unclear or not stated, because
  StemStudio is a local, single-user, non-commercial practice tool. Conditions:
  1. every such model is listed in `docs/THIRD-PARTY-LICENSES.md` with its exact licence status,
     source URL and SHA-256;
  2. weights are downloaded at first use into the model cache volume and are never committed to
     the repository;
  3. the README and `docs/THIRD-PARTY-LICENSES.md` state that commercial use and redistributing
     images that contain these weights are not covered;
  4. weights whose licence explicitly forbids this use remain excluded.
- DRM MUST never be circumvented. Spotify input is resolved to metadata via spotDL, and the audio
  comes from a matching public YouTube source.
- The UI shows a notice that imports are for personal practice only and the user is responsible
  for complying with copyright and platform terms of service.

### V. Simplicity and Honesty

- YAGNI: build the MVP scope only; post-MVP ideas go to the backlog.
- Limitations are documented, not hidden. In particular, separating instruments beyond the
  available models' stems is limited and quality varies; limitations are shown to the user.
- Audio timing uses sample-accurate Web Audio scheduling (look-ahead scheduler on
  `AudioContext.currentTime`); `setInterval`/`setTimeout` MUST NOT determine audible timing.

## Technology & Security Constraints

- Frontend: Blazor Web App (.NET 10) + Radzen Blazor Components (MIT); audio engine in
  TypeScript via JS interop (Web Audio API).
- Backend: ASP.NET Core (.NET 10) Minimal API, EF Core + SQLite, file storage on local disk.
- Worker: Python 3.12, FastAPI, wrapping Demucs (`htdemucs_6s`, fallback `htdemucs`), librosa,
  yt-dlp, spotDL and ffmpeg; job status by polling.
- Runtime: `docker compose up` starts everything with one command; a non-Docker path is
  documented.
- Static analysis: .NET analyzers with `TreatWarningsAsErrors` and `dotnet format`; ruff + mypy
  (strict) for Python; ESLint for TypeScript; a SonarQube-compatible `sonar-project.properties`.
- Secrets are never stored in files or command lines; configuration comes from environment
  variables. User-supplied URLs and file names are validated (allow-listed hosts, no path
  traversal, size limits) before being passed to external tools; subprocesses are invoked with
  argument arrays, never through a shell.
- Deviations from the preferred stack require an ADR in `docs/decisions/`.

## Development Workflow & Quality Gates

- Spec-Driven Development with GitHub Spec Kit: constitution -> specify -> clarify -> plan ->
  tasks -> analyze -> implement. All artefacts live in the repo under `specs/`.
- Work is executed by specialised agents (`.claude/agents/`) in clearly separated folders; each
  agent ends a task with a hand-off note in `docs/handoffs/<agent>-<task>.md` (what was built,
  files changed, tests and coverage, open issues, next owner).
- The orchestrator (CTO) reviews every hand-off against the Definition of Done and rejects
  incomplete work with concrete feedback.
- Definition of Done (per task and for the MVP): tests green; coverage >= 90% line and branch;
  analyzers, formatters and linters clean; SIG limits respected; public API documented;
  hand-off note written; `docs/STATUS.md` updated; Conventional Commit created.
- CI (GitHub Actions) builds, lints, tests and enforces coverage on every push and pull request.

## Governance

- This constitution supersedes other practices. Plans and tasks MUST include a Constitution Check
  and justify any deviation in a Complexity Tracking table.
- Amendments are made through `/speckit-constitution`, recorded with a semantic version bump
  (MAJOR: principle removed or redefined; MINOR: principle or section added; PATCH: wording),
  and accompanied by an ADR when they change technical direction.
- Compliance is reviewed at every hand-off and by the `quality-gate` agent before the MVP is
  declared complete.

**Version**: 1.1.0 | **Ratified**: 2026-11-14 | **Last Amended**: 2026-11-15
