# Data Model: Style Transfer with a Style Library

Feature 009. Binding for the generator (Python), the API/Domain/Persistence (.NET) and the
contracts. Names in `code` are the contract names; DTO members follow the existing camelCase JSON
convention. Versions are assigned at feature start (contracts/DELTA.md).

## 1. Configuration (environment)

| Variable | Service | Default | Meaning |
|---|---|---|---|
| `StemStudio__GeneratorBaseUrl` | api | unset | generator base URL (`http://generator:8010`); unset = "not installed" |
| `STEMSTUDIO_GENERATOR` | generator | `acestep` | `acestep` or `fake` (CI/E2E) |
| `STEMSTUDIO_FAKE_GENERATOR_MODE` | generator | `ready` | fake only: `ready`, `no-gpu`, `oom`, `oom-twice`, `busy`, `slow` |
| `STEMSTUDIO_FAKE_GENERATOR_STEP_SECONDS` | generator | `1` | fake only: seconds per fake step |
| `STEMSTUDIO_DATA_DIR` | generator | `/data` | job JSON + scratch |
| `STEMSTUDIO_MODEL_DIR` | generator | `/models` | weights under `/models/acestep` (shared `model-cache` volume) |
| `STEMSTUDIO_GPU_LEASE_FILE` | worker (GPU), generator | unset / `/gpu-lease/gpu.lock` | shared lock file; unset in the worker = 003 behaviour |
| `STEMSTUDIO_GPU_LEASE_WAIT_SECONDS` | worker | `900` | max wait for the file lease before a CPU fallback |
| `STEMSTUDIO_GPU_MEMORY_WAIT_SECONDS` | generator | `600` | max wait for enough free VRAM |

## 2. Generator (Python, `stemstudio_generator`)

### 2.1 Health

`GeneratorHealth { contractVersion, ready: bool, reason: str | None, device: DeviceInfo,
models: [ModelStatus], queue: { queued: int, running: str | None } }`

- `DeviceInfo` has the same shape and probe reason texts as worker 1.2.0 `DeviceInfo`
  (ADR-012 D2, 003 data-model 2.1). `ready = device.kind == "gpu"`.
- `ModelStatus { id: "acestep-v15-turbo" | "acestep-vae" | "qwen3-embedding-0.6b" |
  "acestep-5hz-lm-1.7b", present: bool, bytes: int, revision: str }`.

### 2.2 Generation job (kind `restyle` in 009)

| Field | Type | Rules |
|---|---|---|
| `generationId` | str (32 hex) | server-generated |
| `kind` | `restyle` | 010 adds `song`, `voiceConversion` |
| `status` | `queued` \| `waitingForGpu` \| `downloadingModel` \| `running` \| `completed` \| `failed` \| `cancelled` | transitions in 2.4 |
| `progress` | float 0-1 | monotonic within a status |
| `stage` | str | readable stage text ("Loading model", "Generating take 1 of 2", ...) |
| `params` | `RestyleParams` | echo of the request |
| `takes` | [`GeneratedTake`] | only when completed |
| `device` | `JobDevice` | 003 shape; `cpuFallback` always false (no CPU path) |
| `error` | `{ code, message }` \| None | codes: `gpuRequired`, `gpuBusy`, `outOfMemory`, `modelDownloadFailed`, `interrupted`, `invalidInput`, `engineError` |
| `createdAt`, `updatedAt` | datetime (UTC) | |

`RestyleParams`:

| Field | Type | Rules |
|---|---|---|
| `caption` | str | 1-512 chars (built by the API) |
| `audioCoverStrength` | float | 0.0-1.0 |
| `inferenceSteps` | int | 1-20 (default 8) |
| `shift` | float | 1.0-5.0 (default 3.0) |
| `seeds` | [int] | 1-2 entries, each 0..2^31-1; length = number of takes |
| `mode` | `range` \| `inPlace` | |
| `rangeStartSeconds`, `rangeEndSeconds` | float | end - start in [10, 480]; inside the source |
| `sourceFiles` | multipart parts `stem` (1-6 FLAC/WAV files, each <= 300 MiB, same sample rate and length +- 1 frame) | summed at unity, clipped peaks reported in `stage` warning |

`GeneratedTake { index: int, seed: int, durationSeconds: float, sampleRate: 48000,
sha256: str, bytes: int, model: { id, revision } }`; audio at
`GET /v1/generations/{id}/takes/{index}` (`audio/flac`, range requests). FLAC 24-bit stereo 48 kHz
with Vorbis comments `AI_GENERATED=true`, `GENERATOR=ACE-Step 1.5 (<revision>)`,
`STEMSTUDIO_GENERATION=<id>`, `STEMSTUDIO_TAKE=<index>`, `COMMENT=AI-generated with StemStudio`.

### 2.3 Ports (`ports.py`)

| Port | Methods | Implementations |
|---|---|---|
| `MusicEngine` | `load(profile, on_progress) -> None`, `restyle(request: EngineRestyle, on_progress) -> list[EngineTake]`, `unload() -> None`, `required_models() -> list[ModelStatus]` | `AceStepEngine` (lazy `acestep` import), `FakeMusicEngine` |
| `GpuGate` | `hold(need: GpuNeed, on_wait) -> ContextManager[DeviceUse]` (lease + NVML + memory cap) | `FileLeaseGpuGate`, `FakeGpuGate` |
| `GpuNeed` | dataclass `(name, memory_mib)` | constants per profile (research.md S-03): `COVER_TURBO`, `COVER_TURBO_LOWMEM` |
| `JobStore` | `save(job)`, `get(id)`, `list_active()`, `delete(id)` | JSON files (`jobs/<id>.json`) |
| `Clock` | `now()`, `sleep(s)` | system / fake |

### 2.4 Job state machine

```
queued -> waitingForGpu -> (downloadingModel ->) running -> completed
   |            |                  |                 |
   +------------+------------------+-----------------+--> failed | cancelled
running --(OOM, first time)--> running (low-memory profile)   # stage text says "retrying with low memory"
```

- Restart reconciliation: `running`/`waitingForGpu`/`downloadingModel` -> `failed(interrupted)`;
  `queued` stays queued.
- Cancel: `queued`/`waitingForGpu` -> `cancelled` immediately; `running` -> cancel flag checked
  between diffusion steps (progress callback) and between takes -> `cancelled`; lease released.
- Completed jobs are kept until the API deletes them (`DELETE /v1/generations/{id}`) or 24 h.

## 3. Backend (.NET)

### 3.1 Style (`StemStudio.Domain.Styles`)

| Member | Type | Rules |
|---|---|---|
| `Id` | Guid | presets: stable GUID from the JSON resource |
| `Kind` | `Preset` \| `User` | presets read-only |
| `PresetKey` | string? | e.g. `jazz-trio`; presets only, unique |
| `Name` | string | 1-60, unique (case-insensitive) across the library |
| `GenreFamily` | enum `GenreFamily` | `Rock`, `Pop`, `Jazz`, `Blues`, `Electronic`, `HipHop`, `Latin`, `Folk`, `Classical`, `World`, `Country`, `Metal`, `Soul`, `Ambient` |
| `SubGenre` | string? | <= 40 |
| `TempoFeel` | enum | `Keep` (follow the source), `Laidback`, `Driving`, `HalfTime`, `Swing` |
| `Instrumentation` | list<string> | 0-8 tags, each 1-30 |
| `Moods` | list<string> | 0-8 tags, each 1-30 |
| `VocalHandling` | enum | `Keep` (vocals stay in the source mix), `Instrumental` (vocal stem excluded, caption "instrumental") |
| `DefaultStrength` | enum `Strength` | `Subtle`, `Light`, `Balanced`, `Strong`, `Free` -> `audioCoverStrength` 0.85 / 0.70 / 0.55 / 0.40 / 0.25 |
| `Description` | string? | <= 300; shown in the library; part of the caption for user styles only |
| `CreatedAt`, `UpdatedAt` | DateTimeOffset | |

Validation: `StyleTextGuard.Check(text)` on `Name`, `SubGenre`, tags and `Description`;
`StylePromptBuilder.Build(style, options) -> PromptResult { Caption (<= 512), Strength, Seeds }`.
Caption template (deterministic, test vectors in tests): `"{genre family} {sub-genre}, {tempo feel
phrase}, {instrumentation joined}, {moods joined}{, instrumental}{, user description}"`, trimmed to
512 characters at a word boundary.

Preset resource `Styles/presets.v1.json`: `{ version: 1, presets: [ { id, key, name, genreFamily,
subGenre, tempoFeel, instrumentation[], moods[], vocalHandling, defaultStrength, description } ] }`;
24 presets in v1 (research.md R-04 list). Seeding: upsert by `id` on start-up; presets removed from
the resource are kept if referenced by a take snapshot (they are never referenced by id after
snapshotting, so they are simply deleted).

### 3.2 Generation (`StemStudio.Domain.Generation`)

| Member | Type | Rules |
|---|---|---|
| `Id` | Guid | |
| `Kind` | `Restyle` (010: `Song`, `VoiceConversion`) | |
| `Status` | `Queued`, `WaitingForGpu`, `DownloadingModel`, `Running`, `Completed`, `Failed`, `Cancelled` | mirrors 2.4; terminal: last three |
| `Progress` | double 0-1 | |
| `Stage` | string? | <= 200 |
| `SourceTrackId` | Guid? | null after the source track is deleted (`SourceTrackTitle` kept) |
| `SourceTrackTitle` | string | snapshot |
| `Range` | `SourceRange(StartSeconds, EndSeconds, Mode)` | |
| `IncludedStems` | list<string> | subset of the six standard stems, >= 1 |
| `StyleSnapshot` | `StyleSnapshot` (JSON) | all style fields + `StyleId` + `Kind` at creation |
| `Caption`, `Strength`, `Seeds` | | builder output (stored for reproducibility) |
| `WorkerJobId` (`GeneratorJobId`) | string? | set when submitted |
| `Device` | `JobDevice?` | 003 value object |
| `Error` | `GenerationError?(Code, Message)` | |
| `Takes` | list<`GenerationTake`> | owned |
| `CreatedAt`, `UpdatedAt`, `CompletedAt` | | |

`GenerationTake`: `Index`, `Seed`, `DurationSeconds`, `Sha256`, `FilePath` (relative to data dir),
`ImportFilePath` (44.1 kHz copy), `LibraryAssetId` (string?, from 007), `ImportedTrackId` (Guid?),
`AiGenerated` (always true).

Rules: create only for a `Completed` track; `Retry` creates a new generation with the same
parameters and new seeds; delete removes files, the library entries (via the port) and keeps
imported tracks (their `Origin` keeps the generation id).

### 3.3 Track origin (`StemStudio.Domain.Tracks`)

`Track.Origin`: `Imported` (default, existing rows) | `Generated(GenerationId, TakeIndex, Kind)`;
`TrackDto.origin { kind: "imported" | "generated", generationId?, takeIndex?, aiGenerated: bool }`.

### 3.4 Generation status (API)

`GenerationStatusDto { state: "notInstalled" | "unavailable" | "ready", reason?: string,
device?: ProcessingDeviceDto, models: [ { id, present, bytes } ], downloadBytesNeeded: long }`.

### 3.5 Persistence

Tables `Styles` (tags as JSON columns), `Generations` (snapshot, range, stems, seeds, device, error
as JSON columns), `GenerationTakes` (owned, FK cascade), `Tracks.Origin` JSON column (null =
imported). Migration `AddGenerativeStyles`. Indices: `Styles(Name)` unique NOCASE,
`Generations(SourceTrackId)`, `Generations(Status)`.

### 3.6 Library port (007 binding)

```csharp
public interface IGeneratedAudioLibrary
{
    Task<string> RegisterAsync(GeneratedAudioEntry entry, CancellationToken ct);
    Task RemoveAsync(string assetId, CancellationToken ct);
}
public sealed record GeneratedAudioEntry(string FilePath, string Title, GenerationKind Kind,
    Guid GenerationId, int TakeIndex, string Sha256, bool AiGenerated);
```

Bound at feature start to the 007 export/asset library service (adapter owned by backend).
Title: `"<source title> - <style name> (take <n>)"`.
