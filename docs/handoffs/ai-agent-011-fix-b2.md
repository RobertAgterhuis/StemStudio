# Hand-off: ai-agent (backend touch) / 011 fix B2 (SC-008 GPU release race)

## What was built
- Root cause: `GpuActivity` cached its answer for 2 s. After an approved `start_detail_separation`, the detail phase is
  stored as queued at once, but the run's next turn (about 0.9 s later) could reuse an idle answer cached just before
  (a status poll or an earlier call), so `AgentModelSelector` picked `qwen3.5:9b` on the GPU. The synchronizer then
  released the models and submitted the job while 9b was still loaded, and the worker fell back to the CPU. Imports and
  generator/singer jobs had a second gap: they are stored only after the submission returns.
- Fix (API, `src/StemStudio.Api/Agent/Gpu`):
  - `GpuActivity` reads the job states on every call (no cached idle answer) and reports busy while a submission hold
    is open.
  - New `GpuSubmissions` (singleton): counts the open holds, plus a 10 s grace after the last one closes, so the stored
    job state takes over without a gap. Thread-safe; a hold closes only once.
  - `GpuJobGate.BeforeGpuJobAsync` / `BeforeGeneratorJobAsync` now open a hold **before** the release and return it
    (`Task<IDisposable>`). A cancelled release disposes it. A CPU worker opens no hold. The settings store is resolved
    through `services`, so the constructor keeps 4 parameters.
  - The worker, generator and singer decorators wrap each submission in `using (await gate.Before...(ct))`, so a failed
    submission also ends its hold.
- Agent library: no production change. `AgentModelSelector` already reads `IGpuActivity` on every turn and sends the
  fallback with `num_gpu: 0`. A regression test now proves that end to end through the Ollama request body.
- Security behaviour is unchanged (policy, taint, approvals and backends were not touched).

## Files changed
- `src/StemStudio.Api/Agent/Gpu/{GpuActivity,GpuJobGate,GpuReleasingWorkerClient,GpuReleasingGeneratorClient}.cs`,
  new `GpuSubmissions.cs`
- `tests/StemStudio.Api.Tests/Agent/GpuCoordinationTests.cs` (repro test
  `ADetailRequest_SendsTheNextTurnToTheFallback_BeforeTheJobIsSubmitted`, hold tests; the "cached for 2 s" assertion now
  expects an immediate busy answer)
- `tests/StemStudio.Agent.Tests/Backends/OllamaBackendTests.cs` (selector regression test)
- `docs/STATUS.md` (B2 note, at the caller's request)

## Tests and coverage
- Red first (commit c4d7fc66): 4 Api failures, including the repro.
- `dotnet test tests/StemStudio.Agent.Tests -c Release -p:CollectCoverage=true`: 601/601 passed; StemStudio.Agent
  line 98.8 %, branch 93.22 % (`coverage_floor.py` met 90 %).
- `dotnet test tests/StemStudio.Api.Tests -c Release -p:CollectCoverage=true`: 1440/1440 passed; StemStudio.Api line
  98.9 %, branch 91.62 %. `GpuSubmissions`, `GpuJobGate` and the decorators are at 100/100.

## Quality checks
- Release build (TreatWarningsAsErrors) clean; `dotnet format --verify-no-changes` clean on Api, Api.Tests and
  Agent.Tests.
- `sig_check.py` on the changed paths: 0 errors, 0 warnings. `duplication.py`: 0 production clones, and the
  `GpuCoordinationTests` test-only clones are the same 3 as on `main`.

## Dependencies added
none

## Docs impact
None for the manuals. The "Local qwen3.5:4b (fallback)" badge may now show for up to about 10 s after a heavy job
was submitted, even when that job is rejected.

## Open issues / requests
- Re-check A12/SC-008 on the GPU stack (owner): fresh track with 9b loaded, "Splits deze track verder", Approve. Expect
  the final turn on `qwen3.5:4b` and the detail job on the GPU.
- Residual edge case (not fixed): a turn of a *different* concurrent run that already started on 9b before the hold
  keeps 9b loaded until that turn ends (Ollama defers the `keep_alive: 0` unload). Closing it fully would need the
  release to poll `/api/ps` until no model holds VRAM. That is a contract change to the 2 s best-effort release.
- Older hand-offs (backend-011-us3/us6) mention the "cached 2 s" GPU activity. That no longer holds.

## Next owner
CTO: merge `011-fix-b2` after the stack re-check.
