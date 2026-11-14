# ADR-002: Component architecture and layering

- **Status**: Accepted
- **Date**: 2026-11-14
- **Decider**: architect agent, on the CTO brief

## Context

The constitution asks for a small number of well-sized components with one-way dependencies, and
for contracts between them. Eight agents build in parallel and need disjoint folders.

## Decision

Six components: **Web** (Blazor Web App, Interactive Server, Radzen), **Api** (ASP.NET Core
Minimal API), **Domain** (pure C#), **Persistence** (EF Core + SQLite), **worker** (Python 3.12,
FastAPI) and **audio engine** (TypeScript, runs in the browser). A dependency-free
**Contracts** project holds the backend HTTP DTOs used by Api and Web.

```text
Web -> Contracts <- Api -> Domain <- Persistence ; Api -> Persistence
Api --HTTP--> worker ; browser engine --HTTP--> Api (stems) ; Web --JS interop--> engine
worker: api -> jobs -> ports <- ingest | separation | analysis ; all -> schemas, config
```

- Web never references Domain or Persistence; Domain references nothing.
- Worker adapters implement the `typing.Protocol` ports in `worker/src/stemstudio_worker/ports.py`
  and never import each other; the job runner composes them.
- Folder ownership per agent is listed in `specs/001-stemstudio-mvp/plan.md`.

## Consequences

- Project references make illegal dependencies a compile error; the quality gate also checks
  Python imports.
- Mapping code between Domain and Contracts lives in Api (small, explicit duplication by design).
- Changing a contract requires an ADR and a version bump (constitution III).
