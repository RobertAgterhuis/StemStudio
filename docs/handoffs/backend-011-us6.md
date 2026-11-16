# Hand-off: backend / 011 US6 (T078)

Branch `011-lane-backend`. Previous: `backend-011-us5.md`.

## What was built

- **GPU release before the 006-010 GPU job endpoints** (DELTA.md section 5, FR-022, SC-008), all through decorators so every
  code path that reaches a GPU service is covered (endpoints and the background synchronisers):
  - `GpuReleasingWorkerClient` now also gates `CreateLyricsAlignJobAsync` (`POST /api/tracks/{id}/lyrics/align` and the
    automatic alignment rule of the lyrics synchroniser) and `CreateTranscriptionJobAsync` (`POST /api/assets/scores`).
  - New `GpuReleasingGeneratorClient` (`IGeneratorClient`) and `GpuReleasingSingerClient` (`ISingerClient`) gate
    `CreateRestyleAsync` (`POST /api/tracks/{id}/restyles`), `CreateSongAsync` (`POST /api/songs`), both via
    `POST /api/generations/{id}/retry`, and `CreateVoiceConversionAsync` (singer, submitted by `ConversionStemWaiter` once
    the take of `.../takes/{index}/voice-conversions` is separated). Reads, downloads, cancels, deletes and sample checks are
    forwarded unchanged.
  - `.../takes/{index}/import` and the take separation of a voice conversion go through `ImportService` ->
    `CreateUploadJobAsync`, gated since T053.
  - `GpuRegistration.DecorateGpuJobClients()` (was `DecorateWorkerClient()`) wraps all three clients; `ApiFactory` calls it
    after the fakes are registered, so the tests exercise the production decorators.
- **Generator jobs release regardless of the worker device.** `GpuJobGate.BeforeGeneratorJobAsync` skips only for a non-Ollama
  backend; the generator and the singer run on the GPU only, so a CPU-only worker says nothing about them. Worker jobs keep
  the DELTA.md rule (skip on a CPU worker).
- **`IGpuActivity`** additionally counts: active generations (restyle, song, voice conversion) except a conversion in
  `waitingForStems` (its take's track counts instead); tracks whose lyrics are `aligning` and active score assets
  (queued/rendering transcriptions) unless the worker is CPU-only. Still cached 2 s.
- No 009 GPU scheduler with a pre-job hook exists in the API, so the decorator approach applies.

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T078 | `f30628b` | `f03a73d` |

The red commit references the new decorator types, so it compiles only with them; red was verified with pass-through
decorators and a no-op generator gate (10 of 11 tests failed on the missing release / registration / activity).

## Tests and coverage

`dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true`: **1430 passed**; StemStudio.Api **98.79 % line,
91.55 % branch**. New `Agent/GpuJobEndpointsTests.cs` (11): restyle, score and lyrics-alignment end to end (release before the
submission), decorator registration, generator/singer and worker decorators gate exactly the job submissions, generator
release on a CPU worker, other backends skip, activity for generations, alignments and scores.

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean; new units <= 4 parameters.

## Dependencies added

None.

## Docs impact

Technical manual: with the Ollama backend, every GPU job (separation, detail, lyrics alignment, scores, restyles, songs, voice
conversions) first asks Ollama to unload its models (best effort, 2 s).

## Open issues / requests

- architect: DELTA.md section 5 says "skipped when ... the worker device is CPU"; for generator and singer jobs the backend
  releases regardless of the worker device (they are GPU-only services). Please reword section 5 accordingly or tell me to
  follow the literal text.

## Next owner

backend (T079c, HO:polish).
