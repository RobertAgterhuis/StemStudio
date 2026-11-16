# Hand-off: backend / 011 US3 (T052-T055) + cross-lane requests

Branch `011-lane-backend`. Previous: `backend-011-us2.md`.

## What was built

- **GPU coordination (T052 -> T053)**, `src/StemStudio.Api/Agent/Gpu/`:
  - `GpuJobGate.BeforeGpuJobAsync`: calls the agent library's `ILlmGpuRelease` (resolved optionally) when the selected
    backend is `ollama` and the cached worker device (`GET /api/device` cache) is not CPU; waits at most 2 s on the injected
    `TimeProvider`; never throws (a failing or hanging release is abandoned, the request continues).
  - `GpuReleasingWorkerClient`: an `IWorkerClient` decorator, so every submission of a standard, upload or detail worker
    job passes the gate: `POST /api/imports`, `/imports/upload`, `/tracks/{id}/retry`, and the detail job that the detail
    synchroniser submits after `POST /tracks/{id}/detail`. Why a decorator: the import/track/detail services already have 4
    constructor parameters (SIG limit), and one decorator covers every code path that reaches the worker. `DecorateWorkerClient()`
    runs in `AddAgentHost`; `ApiFactory` calls it again after registering the fake worker, so tests exercise the production
    decorator. Lyrics alignment and transcription are forwarded unchanged until T078.
  - `GpuActivity : IGpuActivity`: true while a track's standard job (not terminal) or detail phase (queued/running) runs on a
    GPU device, or on a not-yet-known device unless the worker is known to be CPU-only; cached 2 s.
- **Status (T054 -> T055)**: `GET /api/agent/status` (`getAgentStatus`, un-staged) = `AgentStatusProbe`: the library's
  `IAgentStatusService.GetStatusAsync` with a 3 s cap; timeout, exception or a missing service -> 200 with `ready: false` and
  a fixed reason (never an exception text or URL). `POST messages` -> 503 `backend-unavailable` (RFC 9457 `status` 503,
  extension `agentStatus` = the status) when not ready; nothing is stored. Other endpoints never call the probe.
- **qa request (1)**: `StemStudio__Agent__Backend=fake` overrides the backend of a stored settings row
  (`EfAgentSettingsStore.GetAsync`), so the E2E stack always runs the fake. This is an **environment override for the test
  stack**; data-model 2 otherwise lets the row win. Test: `StoresTests.FakeBackendFromTheEnvironment_OverridesAStoredRow`.
- **frontend requests**: (1) SSE response headers are flushed when the stream opens (`response.StartAsync`), test
  `Events_FlushTheHeadersBeforeTheFirstEvent`; (2) `Last-Event-ID` is treated like `after`; **`after` wins when both are
  present** (`Events_AfterWinsOverLastEventId`); (3) no position = full replay from Seq 1 (`Events_Replay...`, unchanged);
  (4) the approval card's `device` in the run snapshot is `cpu` for `cpu` (any case) and `gpu` for anything else, null stays
  null (`GetRun_MapsTheApprovalDeviceToCpuOrGpu`). The live `approval.requested` event carries whatever the runner publishes,
  so **ai-agent** should publish `cpu`/`gpu` as well.
- Fix found by the full suite: `AgentRunLifecycle.FailAsync` now stores `run.failed` before the run turns terminal (a stream
  opened in between used to end without the terminal event).

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T052 -> T053 | `b1cb418` | `8e7772a` |
| fake override (qa) | `b726873` | `5dd7652` |
| T054 -> T055 | `eef6131` | `2f2066c` |
| frontend SSE/device requests | `fe428af` | `e236a19` |

## Tests and coverage

`dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true`: **1390 passed**; StemStudio.Api **98.87 % line,
91.75 % branch**; Contracts 100 / 100. Persistence unchanged since us1 (99 passed, 98.64 / 95).

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean. New units stay within 4 parameters. The SIG warnings listed in
`backend-011-us1.md` are still open for T079c.

## Dependencies added

None.

## Docs impact

Technical manual: the agent backend is checked at most every request of the status endpoint (3 s cap); GPU-heavy imports
unload the Ollama models first (best effort, 2 s) when the backend is Ollama and the worker uses the GPU.
`StemStudio__Agent__Backend=fake` always wins over the saved setting (test stack only).

## Open issues / requests

- qa request (2) is scheduled in T060/T061: `PUT /api/agent/settings` validates the Ollama models (422) only when they change.
- qa request (3), a fresh `ScriptedChatClient` per run that plays from turn 1, is client side (ai-agent, `AddStemStudioAgent` options).
  The backend creates nothing per run except the run record; it does not cache a chat client.
- ai-agent: publish `approval.requested.device` as `cpu`/`gpu`.

## Next owner

backend (US4: T060/T061).
