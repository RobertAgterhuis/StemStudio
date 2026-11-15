# Data Model: Export Isolated Channels (Asset Library)

Builds on the 001-003 data models. Wire shapes: [contracts/DELTA.md](contracts/DELTA.md) (final
OpenAPI documents are produced in Phase 0). Design: [ADR-016](../../docs/decisions/ADR-016-asset-library-and-export-rendering.md).

## 1. Configuration

| Where | Name | Default | Meaning |
|---|---|---|---|
| worker | `STEMSTUDIO_LIBRARY_DIR` | `/library` (compose), unset otherwise | Root of the API data directory, **read-only**. Unset or unreadable -> render jobs fail with "The library is not available to the worker" and `/health` reports `library.readable = false` |
| worker | `STEMSTUDIO_RENDER_BLOCK_SECONDS` | `10` | Block length of the streaming renderer (tests use small values) |
| compose | worker volume `api-data:/library:ro` | - | devops; the API keeps `api-data:/data` read-write |
| API | `StemStudio:MinFreeDiskBytes` | `1073741824` (1 GiB) | Exports are refused (409 `insufficient-disk-space`) when the data directory's drive has less free space |
| API | `StemStudio:AssetSyncIntervalMs` | `1000` | Poll interval of active asset jobs (reuses the job-sync pattern) |

## 2. Worker entities (`schemas.py`, Phase 0)

### 2.1 RenderJobRequest (`POST /v1/render-jobs`)

| Field | Type | Rule |
|---|---|---|
| `inputs` | `RenderInput[]` | 1..64 |
| `masterGain` | number | 0..1 |
| `range` | `{startSeconds, endSeconds}` \| null | `0 <= start < end`, `end - start >= 0.5`; `end` beyond the longest input is clamped to it; null = whole length |
| `output` | `RenderOutput` | see 2.3 |
| `metadata` | `{title?, artist?, comment?}` | each <= 200 chars, control characters stripped; written as WAV/FLAC tags |

**RenderInput**: `path` (string, relative to the library root, pattern below), `gain` (0..1),
`pan` (-1..1).

Path allow-list (regex, whole string; `<uuid>` = lower-case `D` format):

```text
tracks/<uuid>/stems/(vocals|drums|bass|guitar|piano|other)\.flac
tracks/<uuid>/detail/(vocals|drums|guitar|piano|other)/[a-z][a-z-]{0,31}\.flac
assets/<uuid>/[a-z][a-z0-9-]{0,31}\.(flac|wav|mp3)
```

After the pattern check the worker resolves `Path(root, path).resolve(strict=True)` and requires
it to be inside `root.resolve()`; otherwise `400 invalid-input-path` (nothing is read).

### 2.2 Inputs of different length

Inputs are aligned at sample 0. Shorter inputs are padded with silence; the output length is the
longest input (or the range). All inputs must be 44.1 kHz; mono inputs are duplicated to stereo;
other rates fail with "Unsupported sample rate <n> Hz in <path>" (stems are always 44.1 kHz;
the rule matters for assets of later features).

### 2.3 RenderOutput

| Field | Values | Default |
|---|---|---|
| `format` | `flac`, `wav`, `mp3` | - (required) |
| `bitDepth` | `16`, `24` | `24` (ignored for `mp3`) |
| `mp3BitrateKbps` | `192`, `256`, `320` | `320` (only for `mp3`) |
| `dither` | bool | `true`; the API sends `false` for raw single-input renders (bit-identical path) |

### 2.4 RenderJob (`GET /v1/render-jobs/{id}`)

| Field | Type | Rule |
|---|---|---|
| `renderJobId` | 32 hex chars | as `Job.jobId` (Phase 0: named like `detailJobId`) |
| `status` | `queued`, `running`, `completed`, `failed`, `cancelled` | state machine 2.5 |
| `progress` | 0..1 | pass 1 = 0..0.5, pass 2 = 0.5..1 |
| `error` | string \| null (<= 500) | readable reason when `failed` |
| `result` | `RenderResult` \| null | set when `completed` |
| `createdAt`, `updatedAt` | ISO 8601 UTC | - |

**RenderResult**: `fileName` (`output.<ext>`), `contentType` (`audio/flac`, `audio/wav`,
`audio/mpeg`), `sizeBytes`, `sha256`, `durationSeconds`, `sampleRate` (44100), `channels` (2),
`peakDbfs` (of the written file, before encoding for MP3), `appliedGainDb` (0 or negative).

### 2.5 State machine and persistence

```text
queued -> running -> completed
   |         |-----> failed      (error)
   |-------- cancelled <- running (DELETE while running: stops after the current block)
```

Persisted as `<data>/render-jobs/<id>.json`; output in `<data>/render-jobs/<id>/output.<ext>`.
On start-up, `queued`/`running` jobs become `failed` with "interrupted by restart" (ADR-003
rule). `DELETE /v1/render-jobs/{id}` removes the JSON and the output (any status; idempotent,
`204`). A sweep removes terminal render jobs older than 24 h.

## 3. Mixing law (binding; worker `render/mixing.py`, vectors in `render-vectors.json`)

For each block of `n` frames: `acc = zeros(n, 2)`; for each input `i` with block `x_i` (n x 2,
float32): `acc += pan(x_i * gain_i, pan_i)`; then `y = acc * masterGain`.

`pan(x, p)` (Web Audio StereoPannerNode, stereo input):

| Pan | `θ` | Output |
|---|---|---|
| `p <= 0` | `(p + 1)·π/2` | `L' = L + R·cos θ`, `R' = R·sin θ` |
| `p > 0` | `p·π/2` | `L' = L·cos θ`, `R' = R + L·sin θ` |

Pass 1 computes `peak = max |y|` over the range. Pass 2 writes `y * s` with `s = 1` when
`peak <= 1.0`, else `s = 10^(-0.1/20) / peak`; `appliedGainDb = 20·log10(s)`. Integer formats
quantise with `round(y·(2^(b-1) - 1))` after TPDF dither (when `dither`), clamped to the integer
range. A render whose peak is 0 (all silent) fails with "Nothing to export: the selected channels
are silent".

## 4. Backend entities

### 4.1 Asset (table `Assets`)

| Column / field | Type | Rule |
|---|---|---|
| `Id` | GUID | - |
| `Kind` | `audio` (007), `score` (008) | string enum, extensible |
| `Name` | string 1..120 (trimmed) | display name; not unique; default per 4.4 |
| `Status` | `queued`, `rendering`, `ready`, `failed` | 4.5 |
| `Progress` | 0..100 | from the worker job |
| `Error` | string \| null (<= 500) | readable; set iff `failed` |
| `WorkerJobId` | string \| null | the worker render job id while not `ready` |
| `Files` | JSON `AssetFile[]` | empty until `ready` |
| `Audio` | JSON `AudioFacts` \| null | set when `ready` for `kind = audio` |
| `Provenance` | JSON `Provenance` | set at creation |
| `SizeBytes` | long | sum of the files |
| `CreatedAt`, `UpdatedAt` | UTC | - |

Index on `(CreatedAt DESC)`, on `TrackId` (shadow column copied from the provenance for the
filter) and on `Status`.

**AssetFile**: `role` (`audio` in 007; `pdf`, `musicxml`, `midi` in 008), `fileName` (on disk,
`<role>.<ext>`), `contentType`, `sizeBytes`, `sha256`.

**AudioFacts**: `format` (`flac`/`wav`/`mp3`), `bitDepth` (16/24 or null), `mp3BitrateKbps`
(or null), `sampleRate` 44100, `channels` 2, `durationSeconds`, `peakDbfs`, `appliedGainDb`.

**Provenance**: `origin` (`export` in 007; `transcription` in 008; later `restyle`,
`generation`), `trackId` (GUID or null), `trackTitle` (snapshot, <= 300), `trackArtist`
(snapshot or null), `sourceAssetIds` (GUID[]; empty in 007), `recipe` (JSON, 4.2),
`contents` (short human text, 4.3). `trackAvailable` is computed on read (track exists).

### 4.2 ExportRecipe (normalised request; stored and returned)

| Field | Type | Rule |
|---|---|---|
| `trackId` | GUID | track must be `completed` |
| `mode` | `raw`, `mix` | `raw`: gain 1, pan 0, master 1, mute/solo ignored |
| `channels` | `ChannelId[]` \| null | explicit selection (1..56, unique, each exists on the track: a standard stem, or a kept detail stem of a parent whose outcome is `split`) or null = every audible channel (mode `mix` only) |
| `mix` | `MixSettings` snapshot \| null | the mix used (mode `mix`): from the request body, or loaded from `mixId` at creation and copied; validated with the existing mix rules (002 `MixDetailRules`) except the name |
| `range` | `{startSeconds, endSeconds}` \| null | 0 <= start < end <= track duration, length >= 0.5 s |
| `format` | `RenderOutput` (2.3) | `bitDepth` default: 16 for `raw` with one channel, else 24 |

### 4.3 RenderPlanner (domain, binding rules)

Input: track, recipe. Output: `RenderPlan { inputs: [{channelId, path, gain, pan}], masterGain }`.

1. **Audible level** (gate rule v2, audio-engine contract 2.0.0): a standard stem is *hidden*
   when its parent is in `mix.expanded`; a detail channel `<p>.<d>` is *hidden* when `p` is not
   expanded. `anySolo` = any non-hidden channel with `solo`. `gate(c) = hidden || mute ? 0 :
   (anySolo && !solo ? 0 : 1)`.
2. `channels = null` (mode `mix`): every channel with `gate = 1`, `gain = volume`, `pan = pan`
   from the mix (defaults `volume 0.8`, `pan 0` for channels without an entry, as in the mixer).
3. Explicit `channels`, mode `mix`: every listed channel, `gain = volume`, `pan = pan`; mute,
   solo and hidden are ignored (the user chose them deliberately).
4. Mode `raw`: every listed channel with `gain = 1`, `pan = 0`, `masterGain = 1`.
5. `masterGain = mix.masterVolume` in mode `mix`.
6. Channels with `gain = 0` are dropped; an empty plan is rejected with 422
   `nothing-to-export` ("Nothing to export: no channel is audible").
7. Paths: `tracks/<trackId>/stems/<stem>.flac` or `tracks/<trackId>/detail/<p>/<d>.flac`.

**Contents text** (`Provenance.contents`, <= 200 chars): `"<channel list>, <raw|mixed>"`
with channel ids joined by " + " in mixer order (standard stems in catalog order, then detail
stems), shortened to "N channels, mixed" above 4 channels; plus ", <m:ss>-<m:ss>" with a range.

### 4.4 Names and file names

- Default name: `"<track title> - <contents without mode>"`, truncated to 120 characters; when
  an item with the same name exists, append " (2)", " (3)", ... (case-insensitive).
- Rename: trimmed, 1..120 characters, no control characters.
- Download file name: name with `<>:"/\|?*` and control characters replaced by `_`, trimmed,
  max 150 characters, plus the extension of the file; sent as `Content-Disposition:
  attachment; filename="<ascii fallback>"; filename*=UTF-8''<percent-encoded>` (RFC 6266/5987).
  `?inline=true` sends `inline` instead (player).

### 4.5 Asset status mapping (sync from the worker)

| Worker render job | Asset |
|---|---|
| `queued` | `queued` |
| `running` | `rendering`, `Progress = round(progress·100)` |
| `completed` | API downloads the file with a hashing copy to `assets/<id>/audio.<ext>`, verifies the SHA-256, fills `Files` and `Audio`, sets `ready`, then `DELETE`s the worker job |
| `failed` / `cancelled` | `failed` with the worker error |
| `404` (worker lost the job, e.g. restart) | `failed`, "The processing service lost this job; try again" |
| worker unreachable | unchanged; retried on the next poll |

`POST /api/assets/{id}/retry` (only `failed`): re-plans the stored recipe (the track and its
stems must still exist, else 409 `source-unavailable`) and submits a new worker job.

`DELETE /api/assets/{id}`: removes the row and `assets/<id>/`; when a worker job is active it is
cancelled with `DELETE /v1/render-jobs/{id}` (best effort; a late completion finds no asset and
the job is deleted).

### 4.6 Stem pack (P3)

`POST /api/assets/stem-packs {trackId, includeDetail, format}` creates one raw asset per standard
stem and (with `includeDetail`) per kept detail stem of every `split` parent, in catalog order,
and returns them; each runs as its own render job.

## 5. Storage layout (API data directory)

```text
<DataDir>/
├── stemstudio.db
├── tracks/<trackId>/stems/<stem>.flac                 (001)
├── tracks/<trackId>/detail/<parent>/<detail>.flac     (002)
└── assets/<assetId>/<role>.<ext>                      (007: audio.flac|wav|mp3)
```

Deleting a track removes `tracks/<trackId>/` only. Asset directories are written only by the API
(temp file + atomic move, existing `AtomicFile`), read by the worker through the read-only mount.

## 6. Limits (Contracts `ContractLimits` additions, Phase 0)

| Constant | Value |
|---|---|
| `AssetNameMaxLength` | 120 |
| `MaxExportChannels` | 56 |
| `MinExportRangeSeconds` | 0.5 |
| `MinFreeDiskBytesDefault` | 1 GiB |
| `ExportFormats` | `flac`, `wav`, `mp3` |
| `ExportBitDepths` | 16, 24 |
| `Mp3Bitrates` | 192, 256, 320 |
