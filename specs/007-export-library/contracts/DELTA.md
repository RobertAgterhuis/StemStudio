# Contract delta: Export Isolated Channels (Asset Library)

## Phase 0 outcome (architect, 2026-11-15; combined with 005)

Versions assigned (docs/ROADMAP.md): **worker-api 1.4.0**, **backend-api 1.5.0**, **ui-test-ids
1.5.0**, **render-vectors 1.0.0**; each is 005's document (worker 1.3.0, backend 1.4.0, UI ids
1.4.0) plus the 007 additions. Full documents next to this file, Redocly 2.78.0 clean (warnings:
`operation-4xx-response` on health/device/list and the idempotent render `DELETE`,
`no-unused-components` for `AudioSource`). Refinements against the delta below:

1. `RenderJob` id field is `renderJobId` (data-model 2.4 said `id`), like `detailJobId` and
   `metadataJobId`.
2. `RenderInput.path` has no schema `pattern`: the allow-list is checked by the path guard so a
   bad path is `400 invalid-input-path`, not a 422 (patterns in `ports.LIBRARY_PATH_PATTERNS`,
   helper `is_allowed_library_path`). Problem `type` values are the short codes
   (`invalid-input-path`, `input-not-found`, `render-not-completed`, backend `track-not-ready`,
   `insufficient-disk-space`, `nothing-to-export`, `asset-not-failed`, `source-unavailable`).
3. `RenderExecutor` has `validate(request)` (create time: 400/404 before queueing) and
   `render(request, out_dir, progress)`; `RenderProgress.__call__(fraction, /)` is positional-only.
4. Settings are grouped: `WorkerSettings.render.{library_dir, block_seconds}`.
5. `GET /api/assets/{assetId}/files/{role}`: `HEAD` is documented in the `GET` operation (one
   operationId); `createAudioExport`, `createStemPack`, `retryAsset` also answer `503`;
   `listAssets` answers `422` for invalid filters.
6. `Provenance.recipe` is `$ref ExportRecipe` (008 turns it into a `oneOf`); `AssetFileRole` is
   `[audio]` and `AssetOrigin` `[export]` in 1.5.0 (008 extends both). `ExportFormat.bitDepth` /
   `mp3BitrateKbps` are nullable (null = default of data-model 4.2).
7. `render-vectors.json` gate cases also carry `trackId`, the expected `path` and `contents` of
   every input; inputs are listed in mixer order (standard stems in catalogue order, then detail
   stems), explicit selections too. Generator: `generate_render_vectors.py` (`--check`).
8. Backend test staging list is named `PendingAssetOperations`. CI/test-all `CONTRACTS_DIR` ->
   this directory is devops T008 (covers 005 as well).


This file lists **what changes** in each contract. The full documents
(`worker-api.openapi.yaml`, `backend-api.openapi.yaml`, `ui-test-ids.md`, `render-vectors.json`,
`redocly.yaml`) are produced by the architect in Phase 0 (T001-T005) on top of whatever versions
are current when 007 starts (004-006 may bump them first). Version rule: every change below is
**additive** -> next **minor** of each document. At the time of writing the bases are worker-api
1.2.0, backend-api 1.3.0, ui-test-ids 1.2.0, audio-engine 2.0.0 (all from 003).

## 1. Worker API (`worker-api.openapi.yaml`, minor bump)

New tag `render`. All bodies camelCase; errors RFC 9457 problem details.

| Method | Path | operationId | Request | Responses |
|---|---|---|---|---|
| POST | `/v1/render-jobs` | `createRenderJob` | `RenderJobRequest` (data-model 2.1) | `202 RenderJob` (queued); `400 invalid-input-path` (pattern/containment), `404 input-not-found` (a path does not exist; `detail` names it), `422` validation problem (ranges, counts, formats) |
| GET | `/v1/render-jobs/{renderJobId}` | `getRenderJob` | - | `200 RenderJob`; `404` |
| GET | `/v1/render-jobs/{renderJobId}/file` | `getRenderJobFile` | - | `200` binary (`audio/flac`, `audio/wav`, `audio/mpeg`), `Content-Length`; `409 render-not-completed`; `404` |
| DELETE | `/v1/render-jobs/{renderJobId}` | `deleteRenderJob` | - | `204` (cancels a queued/running job, removes files; idempotent) |

Schemas (new): `RenderJobRequest`, `RenderInput`, `RenderRange`, `RenderOutput`
(`format`, `bitDepth`, `mp3BitrateKbps`, `dither`), `RenderMetadata`, `RenderJob`,
`RenderJobStatus`, `RenderResult`.

`Health` (additive): `library: { readable: boolean, reason: string | null }` - `readable` is
true when `STEMSTUDIO_LIBRARY_DIR` exists and is listable; `lanes: { render: { queued: int,
running: bool } }`.

Examples to include: a raw single-stem request (`dither: false`, `bitDepth: 16`), a six-input
mix to MP3 with a range, a `running` job, a `completed` job with `appliedGainDb: -1.8`, a
`failed` job "interrupted by restart", a 400 path problem.

## 2. Backend API (`backend-api.openapi.yaml`, minor bump)

New tag `assets`. All new operations are agent-ready: closed schemas
(`additionalProperties: false`), explicit limits, examples, readable problem `detail`s.

| Method | Path | operationId | Request | Responses |
|---|---|---|---|---|
| GET | `/api/assets` | `listAssets` | query `kind` (`audio`\|`score`), `trackId`, `status`, `q` (name contains, <= 100), `limit` (1..200, default 100), `offset` | `200 AssetList { items: Asset[], total: int, totalSizeBytes: long }` (newest first) |
| POST | `/api/assets/audio-exports` | `createAudioExport` | `CreateAudioExportRequest` | `202 Asset` (+ `Location`); `404` track/mix; `409 track-not-ready`, `409 insufficient-disk-space`; `422` validation (`channels[i]`, `range`, `format`), `422 nothing-to-export` |
| POST | `/api/assets/stem-packs` | `createStemPack` | `CreateStemPackRequest { trackId, includeDetail: bool = false, format: ExportFormat }` | `202 { items: Asset[] }`; same errors |
| GET | `/api/assets/{assetId}` | `getAsset` | - | `200 Asset`; `404` |
| PATCH | `/api/assets/{assetId}` | `renameAsset` | `RenameAssetRequest { name }` | `200 Asset`; `422` |
| DELETE | `/api/assets/{assetId}` | `deleteAsset` | - | `204`; `404` |
| POST | `/api/assets/{assetId}/retry` | `retryAsset` | - | `202 Asset`; `409 asset-not-failed`, `409 source-unavailable` |
| GET, HEAD | `/api/assets/{assetId}/files/{role}` | `getAssetFile` | query `inline` (bool) | `200`/`206` with Range, ETag, Last-Modified, `Content-Disposition`; `404` (unknown role or not ready) |

`CreateAudioExportRequest`:

```yaml
trackId: uuid                      # required
mode: raw | mix                    # required
channels: ChannelId[] | null       # 1..56 unique; null = every audible channel (mode mix only)
mix: MixSettingsInput | null       # current mixer state (MixSettings without name); mode mix
mixId: uuid | null                 # alternative to mix (saved mix); exactly one of mix/mixId in mode mix
range: { startSeconds, endSeconds } | null
format: { format: flac|wav|mp3, bitDepth?: 16|24, mp3BitrateKbps?: 192|256|320 }
name: string | null                # 1..120; default per data-model 4.4
```

`ChannelId` = `^(vocals|drums|bass|guitar|piano|other)(\.[a-z][a-z-]{0,31})?$` (same as the
engine's `ChannelId`). `MixSettingsInput` reuses `MixSettings` minus `name`, `metronome` and
`loop` (they never affect an export).

`Asset` (response):

```yaml
id: uuid
kind: audio | score
name: string
status: queued | rendering | ready | failed
progress: 0..100
error: string | null
files: [{ role, contentType, sizeBytes, sha256, url }]   # url = /api/assets/{id}/files/{role}
audio: AudioFacts | null           # data-model 4.1
score: null                        # filled by 008
provenance: { origin: export, trackId, trackTitle, trackArtist, trackAvailable, sourceAssetIds, contents, recipe }
sizeBytes: long
createdAt, updatedAt
```

`AudioSource` (new shared schema, used by 008 and later; not used by a 007 endpoint):
`oneOf [{ trackId: uuid, channel: ChannelId }, { assetId: uuid }]`.

`StemStudio.Contracts` (Phase 0): `Assets/{AssetDto, AssetKindDto, AssetStatusDto, AssetFileDto,
AudioFactsDto, ProvenanceDto, AssetListDto, CreateAudioExportRequest, CreateStemPackRequest,
RenameAssetRequest, ExportFormatDto, ExportModeDto, RangeDto, AudioSourceDto}.cs`,
`ApiRoutes.Assets`, `ContractLimits` additions (data-model 6).

## 3. Audio engine (`audio-engine.md`)

**No interface change** (2.0.0 stays). The engine's test suite gains a consumer of
`render-vectors.json` (gate cases through `levelGate`/`anyAudibleSolo`), so the C# planner and
the engine cannot drift. If Phase 0 finds the gate helpers are not reachable from a test without
an export, the delta is a test-only re-export, not a contract change.

## 4. Shared vectors (`contracts/render-vectors.json`, new, version 1.0.0)

```json
{
  "version": "1.0.0",
  "gate": [ { "name": "...", "track": { "stems": [...], "split": { "drums": ["kick", "snare", "rest"] } },
              "mix": { "masterVolume": 0.7, "channels": [...], "detailChannels": [...], "expanded": ["drums"] },
              "mode": "mix", "channels": null,
              "expected": { "masterGain": 0.7, "inputs": [ { "channelId": "drums.kick", "gain": 0.9, "pan": 0 } ] } } ],
  "pan": [ { "pan": -0.4, "matrix": [[1.0, 0.5878], [0.0, 0.809]] } ]
}
```

At least: no solo; solo on one standard stem; solo on a hidden channel (ignored); mute + solo on
the same channel; expanded parent with a muted detail; explicit selection ignoring mute/solo;
raw mode; gain 0 dropped; empty -> `nothing-to-export`; pans -1, -0.5, -0.4, 0, 0.25, 0.5, 1.
Matrices are written with 1e-9 precision by a generator script in Phase 0 and checked by qa
against Chromium (research.md E-05).

## 5. UI test ids (`ui-test-ids.md`, minor bump)

| Screen | New `data-testid` |
|---|---|
| Navigation | `nav-library` |
| Track detail (`/tracks/{id}`) | `export-stem-<stem>` (button per standard stem), `export-detail-<parent>-<detail>` (per kept detail stem), `export-all-stems` (P3) |
| Mixer | `mixer-export` (opens the dialog) |
| Export dialog (track page and mixer) | `export-dialog`, `export-source-audible`, `export-source-selected` (mixer only), `export-channel-<channelId-with-dot-as-dash>` (checkbox, mixer "selected"), `export-format` (select `flac`/`wav`/`mp3`), `export-bit-depth`, `export-mp3-bitrate`, `export-range-loop` (checkbox, only with a loop region), `export-range-start`, `export-range-end`, `export-name`, `export-include-detail` (stem pack), `export-submit`, `export-cancel`, `export-error`, `export-saved` (confirmation with a link to the item: `export-saved-open`) |
| Library (`/library`) | `library-total-size`, `library-filter-track`, `library-filter-kind`, `library-search`, `library-empty`, `asset-row-<assetId>`, `asset-name-<assetId>`, `asset-status-<assetId>` (text = status; `aria-valuenow` = progress while rendering), `asset-contents-<assetId>`, `asset-source-<assetId>` (`data-track-available` true/false), `asset-gain-note-<assetId>` (only when `appliedGainDb < 0`), `asset-play-<assetId>` (an `<audio>` element), `asset-download-<assetId>` (link), `asset-rename-<assetId>`, `asset-rename-input`, `asset-rename-confirm`, `asset-delete-<assetId>`, `asset-retry-<assetId>` (only `failed`), `asset-error-<assetId>`, `confirm-dialog-ok`, `confirm-dialog-cancel` (existing) |

## 6. In-app help (004 framework)

New help topics (ids are the `data-help-id` values; text by the frontend, reviewed by the CTO):
`library` (what the library is, later use, sizes, copyright line), `export-dialog` (what I hear vs
selected channels, raw vs mixed), `export-formats` (FLAC/WAV/MP3, bit depth, bit rate; Advanced),
`export-range` (Advanced), `export-clipping` (why an item was lowered). Minimum journey levels:
format select = Beginner (default FLAC preselected), bit depth / bit rate / range / selected
channels / stem pack = Advanced.

## Reconciliation (architect, 2026-11, after the 007 implementation wave)

- data-model 3: quantisation `clamp(round(y·2^(b-1)), -2^(b-1), 2^(b-1)-1)` (libsndfile's read
  scale; keeps raw renders bit-identical), pan table rows `p < 0` / `p >= 0`, TPDF dither on every
  integer output (16/24-bit and the PCM fed to LAME) seeded from `sha256(<render job id>)`;
  data-model 2.4: `peakDbfs` floored at -150 when every sample quantises to zero, four more
  readable `error` texts. ADR-016 D4 amended for 24-bit dither. tasks T017: MP3 decoded length
  `n .. n + 2 frames` (no gapless header from lameenc). Wire contracts unchanged.
