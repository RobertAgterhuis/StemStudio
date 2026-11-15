# ADR-016: Asset library and server-side export rendering (feature 007)

- **Status**: Accepted (architect, 2026-11-15, 007 Phase 0 T003; contracts worker 1.4.0, backend 1.5.0, UI test ids 1.5.0, render-vectors 1.0.0 in `specs/007-export-library/contracts/`)
- **Context documents**: `specs/007-export-library/{spec.md, research.md, data-model.md,
  contracts/DELTA.md}`, ADR-003 (worker contract and polling), ADR-004 (stem storage and
  serving), ADR-005/ADR-010 (audio engine, gate rule v2), ADR-012 (device policy),
  `docs/ROADMAP.md` (007 is the input/output library of 008, 009, 010 and 011)

## Context

The owner wants to save isolated channels "so we can use them later" (spec 007). The roadmap
makes the same library the place where sheet music (008), restyled songs (009), generated songs
(010) and the AI agent's results (011) land, and where those features take their audio inputs
from. Constraints:

1. The API is the system of record for stems (`<DataDir>/tracks/<id>/stems|detail/...`,
   ADR-004); the worker cache may be wiped.
2. "As currently mixed" must sound like the mixer: the engine applies `volume x gate`, a Web
   Audio `StereoPannerNode` on stereo input, and the master gain (audio-engine contract 2.0.0).
3. The agent (011) runs without a browser, so rendering cannot depend on the browser.
4. Measured (research.md E-01..E-04): decoding six 4:22 stems takes 1.6 s, mixing 0.35 s,
   FLAC-24 write 1.3 s, MP3-320 encode 5.7 s; a single float32 stereo stem is 92 MB in memory.

## Decisions

### D1 One generic `Asset` entity, owned by the API

The export library is an **asset library**: one table `Assets` and one folder per asset
`<DataDir>/assets/<assetId>/`. An asset has a `kind` (`audio` in 007; `score` added by 008;
later kinds are additive), a renamable display `name`, a `status` (`queued`, `rendering`,
`ready`, `failed`), one or more typed **files** (`role` + file name + content type + size +
SHA-256), and a **provenance** record: the origin (`export` in 007), the source track (id plus a
title snapshot), the source assets, and the normalised **recipe** (the request that produced
it). The recipe makes every asset reproducible (retry) and explainable (agent, help).
Assets are independent of tracks and mixes: deleting a track keeps its assets
(`provenance.trackAvailable = false`).

### D2 Rendering runs in the worker (numpy), not in the browser and not in .NET

The API resolves a request into a flat **render plan** and the worker executes it. The browser
(`OfflineAudioContext`) is rejected (no agent use, all stems decoded in the page); .NET is
rejected (no OSI FLAC/MP3 encoder in the API image; the worker already has libsndfile and
lameenc).

### D3 The API resolves channels; the worker only sums

- The API owns the mixer semantics in the domain (`RenderPlanner`): channel ids
  (`<stem>` or `<parent>.<detail>`), the audible-level gate rule v2 (collapsed parents plus the
  detail channels of expanded parents; `anySolo`; mute), per-channel volume and pan, master
  volume. Two modes: `raw` (unity gain, centre, no master) and `mix` (the given or saved mix).
  The selection is either explicit channel ids or "everything audible" (`channels = null`).
- The worker receives only `inputs[] = {path, gain, pan}`, `masterGain`, an optional range and
  the output format. It knows nothing about mixes, solo or detail groups, so 008-011 reuse it.
- **Equality with the engine** is guarded by one shared vector file
  `contracts/render-vectors.json` (gate cases and pan matrices) consumed by the engine's Vitest
  suite, the backend's xUnit suite and the worker's pytest suite, plus one Playwright check that
  the browser's own `StereoPannerNode` produces the vector values (research.md E-05).

### D4 Mixing law (binding text in data-model.md 3)

Per input: `x * gain`, then the Web Audio stereo-input panner (pan <= 0: `L' = L + R*cos(θ)`,
`R' = R*sin(θ)` with `θ = (pan+1)·π/2`; pan > 0: `L' = L*cos(θ)`, `R' = R + L*sin(θ)` with
`θ = pan·π/2`), summed in float32, times `masterGain`. No ramps (static settings). If the peak
of the rendered range exceeds 1.0, the whole render is scaled to a peak of -0.1 dBFS and the
applied gain is recorded on the asset and shown (never silent clipping). 16-bit output of a
processed render gets TPDF dither (seeded per job); a `raw` single-input render with the source's
16-bit depth is written without dither and is bit-identical to the stored stem.

### D5 Inputs: read-only library mount, relative paths

The API's data directory is mounted **read-only** into the worker (`/library`,
`STEMSTUDIO_LIBRARY_DIR`); requests carry paths **relative** to it, chosen by the API
(`tracks/<id>/stems/<stem>.flac`, `tracks/<id>/detail/<parent>/<detail>.flac`,
`assets/<id>/<file>`). The worker accepts a path only when it matches the allow-list pattern,
contains no `..`, and its resolved real path stays inside the library root. Uploading stems per
request was rejected (up to 56 stems x ~30 MB per export). The worker never writes to the
library: it writes its output to its own data dir and the API copies it (hashing copy, as for
stems) into `assets/<id>/`, then deletes the worker job.

### D6 A separate light lane in the worker

Render jobs (and 008's transcription jobs, in their own lane) do not wait behind a 30-minute
detail separation: the worker gets a small `LaneScheduler` (one FIFO thread per lane) next to
the existing `PriorityScheduler`. Rendering is CPU-light and streams in 10 s blocks (measured
bit-equal to a whole-file render), so peak memory stays below ~200 MB regardless of the number
of inputs. Worker restart: unfinished render jobs become `failed` with "interrupted by restart"
(as ADR-003); the asset can be retried from its recipe.

### D7 API surface built for people and for the agent

`/api/assets` (list with `kind`, `trackId`, `status`, `q` filters and the library's total size;
get; rename; delete; retry; file download with Range/ETag and `Content-Disposition`),
`POST /api/assets/audio-exports` (one asset) and `POST /api/assets/stem-packs` (one asset per
stem of a track). Every request body is a closed JSON schema with explicit limits, so 011 can
expose them as agent tools 1:1; asset ids and the `AudioSource` union (`{trackId, channel}` or
`{assetId}`) are the input type of 008 and later features. Status is polled
(`GET /api/assets/{id}`), as for tracks.

### D8 Formats

`flac` (16/24-bit, default 24 for `mix`, 16 for `raw`), `wav` (16/24-bit PCM), `mp3` (CBR
192/256/320 kbps via lameenc, default 320). 44.1 kHz stereo always (stems are 44.1 kHz). WAV and
FLAC carry title/artist/comment tags via libsndfile; MP3 has no tags (no non-GPL tag writer in
the stack; mutagen is GPL). The download file name is derived from the asset name (sanitised,
RFC 6266 `filename*`).

## Consequences

- One mount and one environment variable more in compose and in the non-Docker path; the
  library layout (three path patterns) becomes part of the worker contract.
- The gate rule exists twice (TypeScript engine, C# domain); the shared vectors keep them equal.
- Rendering is deterministic and fully unit-testable without a browser or models; the fake stack
  renders real files from the fake stems, so E2E tests download real audio.
- Later features add asset kinds, origins and roles without changing 007's endpoints.
- Disk use grows with the library; the library page shows the total size and exports fail with
  a readable message when free disk space is below 1 GiB.

## Alternatives rejected

| Alternative | Why not |
|---|---|
| Browser `OfflineAudioContext` render | needs an open page (agent, 008/009 pipelines), decodes every stem in the page, untestable beyond mocks |
| Render in the API (.NET) | no OSI-licensed FLAC/MP3 encoder in .NET without shipping ffmpeg into the API image |
| Upload stems to the worker per job | up to ~1.6 GB transfer per export; the read-only mount is free |
| Worker reads its own cache | the cache may be wiped (ADR-004); detail stems and assets are not in it |
| Separate "exports" table per feature | 008-011 would each invent their own library; one asset model keeps one library page and one agent tool set |
