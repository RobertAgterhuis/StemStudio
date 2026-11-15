# Data Model: Detail Separation (Phase 1)

Extends [001 data-model.md](../001-stemstudio-mvp/data-model.md); everything not mentioned here is
unchanged. Wire shapes: [contracts/](contracts/) (worker 1.1.0, backend 1.2.0, engine 2.0.0).
Decisions and thresholds: [research.md](research.md), ADR-010.

## 1. Names

### 1.1 Parent stems

The six standard stems (`vocals`, `drums`, `bass`, `guitar`, `piano`, `other`). `bass` is never
split (FR-002). With the 4-stem fallback model only `vocals`, `drums`, `other` can be split.

### 1.2 Detail stem names (`DetailStemName`, closed enum, kebab-case)

| Parent | Possible detail stems (model output -> name) | Model (ADR-010) |
|---|---|---|
| `drums` | `kick`, `snare`, `toms`, `hihat`, `cymbals` (= ride + crash), `rest` | MDX23C DrumSep 6-stem |
| `vocals` | `lead`, `backing`, `rest` | UVR MDX-Net KARA 2 |
| `other`, `guitar`, `piano` | `strings`, `brass`, `woodwinds`, `keys`, `synth`, `acoustic-guitar`, `electric-guitar`, `percussion`, `rest` | MVSep Mega 53-stem BS-RoFormer, folded into families (research.md D-05) |

`rest` exists for every split parent: `rest = parent - sum(kept sub-stems)` (FR-005). A family
that a parent can never produce (e.g. `kick` under `vocals`) is a validation error. Names are
enum values everywhere (worker, backend, URLs, file names), so they are safe path components.

**Display order** within a group: the order of the table row above, `rest` always last.

## 2. Worker entities (JSON files)

### 2.1 DetailJob (`detail-jobs/<detailJobId>.json`)

| Field | Type | Rules |
|---|---|---|
| `detailJobId` | 32 lower hex | `uuid4().hex` |
| `standardJobId` | 32 lower hex | the completed standard job whose stems are split |
| `audioSha256` | 64 lower hex | from the standard result; cache key of the detail result |
| `status` | `queued` \| `running` \| `completed` \| `failed` \| `cancelled` | state machine 4.2 |
| `stage` | string <= 200 | e.g. "Waiting for standard separations", "Splitting drums (2 of 4)" |
| `currentParent` | ParentStemName \| null | set while a parent step runs |
| `progress` | int 0..100 | never decreases within one job |
| `error` | string <= 1000 \| null | set iff `failed` |
| `cancelRequested` | bool (internal) | set by the cancel endpoint; honoured between chunks |
| `createdAt`, `updatedAt` | UTC | |

Idempotency: `POST /v1/detail-jobs` returns the existing job for the same `audioSha256` if it is
`queued`, `running` or `completed` (200); otherwise a new one (202).

### 2.2 DetailResult (`cache/by-audio/<sha>/detail/result.json`)

`detailJobId`, `audioSha256`, `separatorVersion` (string, bumps invalidate the cache),
`parents[]` (one per standard stem of the result, in display order), `completedAt`.

**ParentOutcome**: `parent`, `outcome`, `model` (model id or null), `stems[]`
(`DetailStemInfo`: `name`, `path` = `/v1/detail-jobs/{id}/stems/{parent}/{name}`, `sizeBytes`),
`metrics` (`parentLevelDbfs`, `activeShare`, `modelResidualDb` or null,
`reconstructionErrorDb` or null).

| `outcome` | Meaning (user text in the UI) | `stems` |
|---|---|---|
| `split` | "Split into kick, snare, ..." | >= 2 kept sub-stems + `rest` |
| `skipped-silent` | "Skipped: (almost) silent" (FR-003) | empty |
| `nothing-further` | "Nothing further to separate" (fewer than 2 meaningful sub-stems, FR-006) | empty |
| `not-reliable` | "Split was not reliable" (model residual or reconstruction check failed, FR-006) | empty |
| `not-supported` | "Not split" (bass; parent without a detail model) | empty |

### 2.3 Checkpoints (per parent step)

`cache/by-audio/<sha>/detail.partial/<parent>/{outcome.json, <name>.flac}` is written when a
parent step finishes (write into `<parent>.tmp/`, then rename). A retry or a resumed job skips
parents that already have a checkpoint. When all parents are done the partial directory is
renamed to `detail/` (atomic) and `result.json` is written last.

### 2.4 Model files (`STEMSTUDIO_MODEL_DIR`, compose: `/models/detail/`)

`<modelId>/<file>`; downloaded to `<file>.part`, SHA-256 verified, then renamed. The registry
(`detail/models.py`) is code: id, URL, size, SHA-256, licence status (research.md D-07).

## 3. Backend entities (SQLite via EF Core)

### 3.1 Track (extended)

New owned value `DetailPhase` (JSON column `DetailPhase`, nullable; null = never requested, shown
as status `none`). Migration `AddDetailPhase` adds the column; existing rows stay null.

| Field | Type | Rules |
|---|---|---|
| `Status` | `Queued` \| `Running` \| `Completed` \| `Failed` \| `Cancelled` | state machine 4.1 |
| `Stage` | string? <= 200 | from the worker |
| `CurrentParent` | ParentStemName? | from the worker |
| `Progress` | int 0..100 | never decreases within one detail job |
| `Error` | string? <= 1000 | set iff `Failed` |
| `WorkerDetailJobId` | string? (32 hex) | null while the submission is pending (worker unreachable) |
| `Parents` | list of `ParentOutcome` | empty until `Completed` |
| `RequestedAt`, `UpdatedAt` | UTC | |

**ParentOutcome** (backend): `Parent`, `Outcome`, `DetailStems` (list of `DetailStem`:
`Name`, `SizeBytes`, `Order`). File path derived, never stored:
`<DataDir>/tracks/<TrackId>/detail/<parent>/<name>.flac`.

Rules:
- A detail phase can only be requested when `Track.Status == Completed`.
- Start ("Split further") is allowed when `DetailPhase` is null, `Failed` or `Cancelled`; it
  creates a fresh phase (`Queued`, progress 0, new worker job). `Completed` cannot be restarted.
- `Completed` is set only after every detail stem file was written (temp file + rename).
- Deleting a track is refused (409) while `DetailPhase.Status` is `Queued` or `Running`
  (FR-016); otherwise the `detail/` folder is removed with the track.
- Detail failures never change `Track.Status`, `Stems` or mixes (FR-008).

### 3.2 Mix (extended, backwards compatible)

| Field | Type | Rules |
|---|---|---|
| `Expanded` | set of ParentStemName (JSON) | default empty; each parent must have outcome `split` on the track |
| `DetailChannels` | list of `DetailChannelSettings` (JSON) | default empty; at most one per (parent, stem); the detail stem must exist on the track |

**DetailChannelSettings**: `Parent`, `Stem` (DetailStemName), `Volume` 0..1 (default 0.8), `Pan`
-1..1 (default 0), `Mute`, `Solo` (default false). Detail stems without an entry use defaults.

Old mixes: both columns null/absent -> empty -> every parent collapsed, sound unchanged (FR-015,
SC-005). Migration `AddMixDetailSettings` adds the two JSON columns with default `[]`.

## 4. State machines

### 4.1 Detail phase (backend)

```text
  [none] ──(standard completed: auto) or POST .../detail──> queued ──> running ──> completed
                                                          │   │         │
                                                          │   └─────────┴──> failed   (worker failed | worker 404
                                                          │                            "interrupted by restart" |
                                                          │                            stem download failed)
                                                          └── POST .../detail/cancel ──> cancelled
  failed | cancelled ──POST .../detail──> queued (new WorkerDetailJobId)
```

- Forward only (queued < running < terminal). `queued` with `WorkerDetailJobId == null` means
  "submission pending": the sync service retries `POST /v1/detail-jobs` each poll.
- Cancel is allowed in `queued` and `running`; the API calls the worker cancel endpoint (503 if
  the worker is unreachable and a worker job exists).

### 4.2 Detail job (worker)

```text
 queued ──> running ──(per parent: gate -> model -> sub-stem gate -> checks -> checkpoint)──> completed
   │          │
   ├──────────┴──> failed     (DetailModelUnavailableError, DetailSeparationError, out of memory/disk,
   │                           "interrupted by restart" at start-up, "Unexpected error")
   └──────────┴──> cancelled  (cancel request; honoured before the next chunk)
```

| Status | Progress | Stage text |
|---|---|---|
| queued | 0 | "Queued" / "Waiting for standard separations" |
| running | 1-99 | "Downloading detail model" (first use), "Splitting <parent> (i of n)" |
| completed | 100 | "Completed" |
| failed | unchanged | "Failed" |
| cancelled | unchanged | "Cancelled" |

Progress = `floor(100 x (done parent steps + fraction of current step) / steps)`, where each
step is weighted by its measured cost (research.md D-03).

Fixed error texts:

| Cause | `error` |
|---|---|
| Model download failed (offline) | "Detail model could not be downloaded; try again when online." |
| SHA-256 mismatch | "Detail model download was corrupt; try again." |
| `MemoryError` / allocation failure | "Not enough memory for detail separation." |
| `OSError` ENOSPC | "Not enough disk space for detail separation." |
| Standard stems missing in the worker cache | "The standard stems of this track are no longer available in the worker; re-import the track." |
| Restart | "interrupted by restart" |
| Bug | "Unexpected error" |

No partial detail stems are ever offered: a failed or cancelled job leaves only checkpoints in
`detail.partial/` (reused by a retry), never a `detail/result.json`.

## 5. Storage layout (additions)

```text
<StemStudio:DataDir>/tracks/<trackId>/detail/<parent>/<name>.flac      # backend copy (served)

<STEMSTUDIO_DATA_DIR>/
├── detail-jobs/<detailJobId>.json
└── cache/by-audio/<sha>/
    ├── detail.partial/<parent>/{outcome.json, <name>.flac}   # checkpoints
    └── detail/{result.json, <parent>/<name>.flac}            # final, cache hit for SC-007

<STEMSTUDIO_MODEL_DIR>/detail/<modelId>/<file>                 # model-cache volume, never in git
```
