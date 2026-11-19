# 017 Split IWorkerClient into job-family ports
- Status: proposed · Date: 2026-11-19 · Area: Api / Worker client and GPU gate (StemStudio.Api/Worker, Agent/Gpu) · Priority: P2

## Problem statement
`IWorkerClient` is one interface for every worker capability: separation, detail, metadata, artwork, render, lyrics, transcription,
storage and rehydration. It has 33 members (`src/StemStudio.Api/Worker/IWorkerClient.cs:24-276`, 277 lines) and 30 consumer files
outside `Worker/`. Each consumer depends on all of it, while most use two or three members.

Evidence:
- `src/StemStudio.Api/Agent/Gpu/GpuReleasingWorkerClient.cs` (154 lines) gates 5 members and only forwards the other 28, one by
  one. Each new worker endpoint has to be added there by hand. If it is a GPU job and someone forgets the gate, it is silently not
  gated.
- The practice lane already worked around this with a separate interface. `src/StemStudio.Api/Worker/WorkerClient.Practice.cs:7-10`
  says the separate interface exists "so the 001..013 IWorkerClient and its decorators stay unchanged". The design is now split
  between one fat interface and one per-lane interface.
- `tests/StemStudio.Api.Tests/TestSupport/FakeWorkerClient.cs` is 511 lines. Every test that needs a worker carries the fake for
  all 33 members.
- Churn: `IWorkerClient.cs` changed in 8 commits and `WorkerClient.cs` in 9 (`git log --name-only`). Parallel feature lanes edit the
  same file and get merge conflicts.

## Impact
- Every new worker capability touches the interface, the real client, the GPU decorator and the 511-line fake. That is a cost in
  every sprint with a worker change (most of them).
- GPU gating is opt-in per member, and that is a correctness risk. A decorator that only wrapped the "create job" ports would make
  gating visible at the type level.
- SIG interfacing and coupling: consumers depend on far more than they use, so the component dependency graph is denser than the
  real dependencies.

## Risk
- Leaving it: low immediate risk, growing cost.
- The change: mechanical, with compiler support. The real `WorkerClient` keeps implementing all the new interfaces (it is already a
  partial class per family), so the HTTP behaviour does not change. The main risk is DI registration: the decorator must be applied
  to the right interface. Rollback is per family.

## Effort
M (2-3 days). One family per pull request, about 2-4 hours each, for 8 families.

## Step-by-step plan
1. Safety net: `GpuCoordinationTests` and `GpuJobEndpointsTests` already pin which calls pass the gate. Add one test that lists
   every interface member creating a GPU job and asserts that it is gated (reflection over a `[GpuJob]` marker, or an explicit
   list).
2. Define family interfaces that follow the existing partial files: `IWorkerSeparation` (URL, upload, job, result, stem),
   `IWorkerDetail`, `IWorkerMetadata` (and artwork), `IWorkerRender`, `IWorkerLyrics`, `IWorkerTranscription`, `IWorkerStorage`
   (storage, cleanup, rehydrate), `IWorkerHealth`. Keep `IWorkerClient : all of them` for now, so nothing breaks.
3. Register each family interface to the same singleton. Move the GPU decorator to only the families with GPU jobs (separation,
   detail, lyrics, transcription) and delete the forwarding members. Check: the step-1 test and the GPU tests pass.
4. Move consumers to the narrowest interface, one feature folder per commit. Split `FakeWorkerClient` into per-family fakes that
   share a job-state helper. Check: build plus Api.Tests per commit.
5. Fold `IWorkerPracticeClient` into the same scheme (`IWorkerSections`, `IWorkerScoreViews`, `IWorkerSetlistDocuments`), then
   remove the aggregate `IWorkerClient` once it has no consumers.
6. Verify: no file depends on more than two worker interfaces (a grep count), the decorator has no forwarding members, and E2E
   passes. Roll back by restoring the aggregate interface (it can stay as a compatibility interface until the end).

## Dependencies and related proposals
The same treatment fits `IGeneratorClient` / `ISingerClient` (`Agent/Gpu/GpuReleasingGeneratorClient.cs`), but it is smaller and
can follow later. The worker HTTP contract stays the same, so no ADR is needed. ADR-003 stays valid.
