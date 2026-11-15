# Implementation Plan: Export Isolated Channels (Asset Library)

**Branch**: `007-export-library` | **Date**: 2026-11-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/007-export-library/spec.md`; constitution 1.2.0;
`docs/ROADMAP.md` (007 is the input/output library of 008-011); research
`docs/research/roadmap-003-011.md` section 6.

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). Binding design decisions
are in ADR-016 (`docs/decisions/ADR-016-asset-library-and-export-rendering.md`); measurements in
[research.md](research.md). Contract changes are listed in [contracts/DELTA.md](contracts/DELTA.md);
the full contract documents are produced in Phase 0.

## Summary

A generic **asset library** owned by the API (`Assets` table, `assets/<id>/` folder) stores
saved audio (this feature) and, later, scores (008), restyled and generated songs (009/010) and
the agent's results (011). The API turns an export request (single stem or detail stem "raw",
or "what I hear" / "selected channels" with the current, possibly unsaved mixer settings, an
optional range and a format) into a flat **render plan** using the engine's gate rule v2; the
worker renders it with numpy in a separate `render` lane (never behind a separation), reading
the stems through a **read-only mount** of the API data directory, applying the Web Audio
stereo-panner law, protecting against clipping, and encoding FLAC/WAV (libsndfile) or MP3
(lameenc). The API copies the result into the library. A new Library page lists, plays,
downloads, renames, deletes and retries items; export actions live on the track page and in the
mixer, with in-app help and journey-level gates from 004. Shared vectors keep the TypeScript
engine, the C# planner and the Python renderer equal.

Measured (research.md E-01): six 4:22 stems decode in 1.6 s, mix in 0.35 s, encode to MP3 320 in
5.7 s; a single stem to FLAC takes about 2-4 s.

## Technical Context

**Language/Version**: unchanged (C# 14 / .NET 10; Python 3.12; TypeScript 6.0).

**Primary Dependencies (new)**: none new in the images. `lameenc` 1.9.0 (LGPL-3.0-or-later,
already installed through demucs) becomes a **direct** worker dependency (pinned in
`pyproject.toml`/`uv.lock`), because our code now imports it. numpy, soundfile/libsndfile are
already direct dependencies. No new NuGet or npm packages.

**Storage**: SQLite table `Assets` (migration `AddAssets`); files `<DataDir>/assets/<id>/`;
worker `render-jobs/*.json` and outputs in its data volume.

**Testing**: worker renderer, path guard, lane scheduler and routes with pytest (tiny synthetic
FLAC fixtures written in `tmp_path`; no models). Backend: domain `RenderPlanner` (shared vectors),
persistence (file SQLite), endpoints with a stub worker handler and `FakeTimeProvider`, file
serving with Range. Frontend: bUnit for the library page, export dialog and actions. Engine:
Vitest consumer of `render-vectors.json`. qa: Playwright on the fake stack (fake stems are real
FLAC files, so exports are real), plus the browser panner check (research.md E-05).

**Target Platform**: unchanged (Docker Desktop / Linux; CPU only for this feature).

**Performance Goals**: SC-001 single stem <= 15 s, SC-002 mix of <= 22 channels <= 30 s for a
5-minute song, also while a separation runs (FR-011).

**Constraints**: the worker never writes into the library (read-only mount); paths are
API-chosen, pattern-checked and containment-checked; no clipping; raw 16-bit FLAC bit-identical;
block-wise rendering (bounded memory for 15-minute songs with 56 inputs).

**Scale/Scope**: one user, libraries of hundreds of items, files up to ~150 MB (15 min WAV 24).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Every owner pairs a failing test with each implementation task (tasks.md). The renderer, mixing law, dither, path guard, lanes and routes are pure Python tested on generated fixtures; the planner is tested against shared vectors; endpoints with stub workers; UI with bUnit; E2E on the fake stack. No model is involved, so no new `@pytest.mark.integration` test. | PASS |
| II | SIG 4 stars | New worker packages `render/` (mixing, reader, encoders, planner-free executor) and `jobs/render_*` + `jobs/lanes.py`, each of small units; backend `Assets/` feature folder (endpoints, service, planner in Domain, sync applier, file store) mirroring `Mixes/` and `Tracks/`; frontend `Library/` and `Export/` component folders. Dependency rules extended by quality-gate (render imports only ports/schemas/config). | PASS |
| III | Contracts first | Worker API and backend API minor bumps, UI test ids minor bump, new `render-vectors.json`; engine contract unchanged. Phase 0 writes the OpenAPI 3.1 documents (Redocly-valid), `schemas.py`, `ports.py` (`RenderExecutor`, `LibraryReader` protocols), `StemStudio.Contracts` DTOs and the vectors before the wave. ADR-016 records the design. | PASS |
| IV | Open source, lawful use | No new library in the images; `lameenc` (LGPL-3.0+, used unmodified as a Python extension, like libsndfile) becomes a direct dependency and is listed. No model weights. MP3 tags are skipped rather than using GPL `mutagen` in our code path. Exports are personal-practice copies (legal notice repeated on the library page). | PASS |
| V | Simplicity and honesty | One generic asset model instead of per-feature libraries (needed by 008-011, not speculative); no zip bundles, no tags for MP3, no quota (only a free-space guard), no normalisation option. Clip protection is shown to the user ("Lowered by X dB"). Limits (no MP3 tags, 44.1 kHz only) are documented in help. | PASS |
| - | Tech & security | Stack unchanged. New input validation: channel ids against the track, ranges, formats, names; worker path allow-list + real-path containment; `Content-Disposition` names sanitised (RFC 6266); read-only mount; no subprocesses (libsndfile/lameenc in-process); no secrets. | PASS |
| - | Workflow | Spec Kit flow; disjoint owner paths (ownership map); hand-off per checkpoint (ADR-008). | PASS |

**Post-design re-check (after Phase 1)**: PASS; refinements tracked in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/007-export-library/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/
    ├── DELTA.md                    # this planning round
    ├── worker-api.openapi.yaml     # Phase 0 (full document, next minor)
    ├── backend-api.openapi.yaml    # Phase 0 (full document, next minor)
    ├── ui-test-ids.md              # Phase 0 (next minor)
    ├── render-vectors.json         # Phase 0 (1.0.0) + generator script
    └── redocly.yaml                # Phase 0
```

### Source Code (additions)

```text
worker/src/stemstudio_worker/
├── ports.py          # + RenderExecutor, LibraryReader protocols, RenderError, InputPathError (architect)
├── schemas.py        # + RenderJobRequest/RenderInput/RenderOutput/RenderJob/RenderResult, Health.library/lanes (architect)
├── config.py         # + library_dir, render_block_seconds (architect)
├── render/           # audio-ingest
│   ├── __init__.py   # create_render_executor(settings)
│   ├── paths.py      # allow-list + containment -> LibraryReader
│   ├── mixing.py     # pan_matrix(), mix_block() (data-model 3)
│   ├── reader.py     # block-wise multi-input reader (pads, mono->stereo, rate check)
│   ├── dither.py     # TPDF, seeded
│   ├── encoders.py   # FLAC/WAV (soundfile, tags), MP3 (lameenc)
│   └── executor.py   # two-pass render with progress/cancel -> RenderResult
├── jobs/             # stem-separation
│   ├── lanes.py          # LaneScheduler (FIFO thread per lane)
│   ├── render_store.py   # render-jobs/*.json, restart reconciliation, 24 h sweep
│   └── render_service.py # create/get/cancel/delete, progress
├── api/render_routes.py  # stem-separation (+ /health library, lanes)
└── main.py               # composition: lane + executor + store (stem-separation)

src/StemStudio.Contracts/Assets/*.cs, ApiRoutes.Assets, ContractLimits     (architect)
src/StemStudio.Domain/Assets/{Asset,AssetFile,AudioFacts,Provenance,ExportRecipe,AssetNaming}.cs
src/StemStudio.Domain/Rendering/{RenderPlanner,RenderPlan,ChannelGate}.cs   (backend)
src/StemStudio.Persistence/ (AssetConfiguration, migration AddAssets)        (backend)
src/StemStudio.Api/Assets/{AssetEndpoints,AssetService,ExportService,AssetFileStore,
    AssetJobApplier,AssetSyncService,DiskSpaceGuard,ContentDisposition}.cs
src/StemStudio.Api/Worker/{WorkerClient.Render,WorkerRenderJob}.cs            (backend)
src/StemStudio.Web/Components/Library/{Library.razor,AssetRow.razor,RenameDialog.razor}
src/StemStudio.Web/Components/Export/{ExportDialog.razor,ChannelPicker.razor,FormatPicker.razor}
    + track page and mixer buttons, nav item, help topics (004 framework)      (frontend)
src/audio-engine/tests/render-vectors.test.ts                                  (audio-engine)
e2e/tests/export.spec.ts, library.spec.ts, panner-law.spec.ts                  (qa)
docker-compose.yml (worker: api-data:/library:ro, STEMSTUDIO_LIBRARY_DIR), scripts/dev.*,
    CI CONTRACTS_DIR                                                           (devops)
tools/quality (dependency rules for render/, Assets/)                          (quality-gate)
```

**Dependency direction**: worker `api -> jobs -> ports <- render`; `render` imports only
`ports`, `schemas`, `config`; `jobs` never imports `render` (composition in `main.py`). .NET:
`Api.Assets -> Domain.Rendering/Assets`, `Api.Assets -> Worker client`, `Api.Assets ->
Persistence`; Web -> API client. The Domain planner does not know paths of the worker, only the
library layout strings it produces (data-model 4.3 rule 7).

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/007-*/**`, ADR-016, `ports.py`, `schemas.py`, `config.py`, `pyproject.toml`/`uv.lock`, `StemStudio.Contracts/**`, project files | architect |
| `worker/src/stemstudio_worker/render/**`, `worker/tests/render/**` | audio-ingest |
| `worker/src/stemstudio_worker/{jobs,api}/**` (render parts), `main.py`, `worker/tests/{jobs,api}/**` | stem-separation |
| `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | backend |
| `src/StemStudio.Web/**` (incl. help topics), `tests/StemStudio.Web.Tests/**` | frontend |
| `src/audio-engine/tests/**` (vector consumer) | audio-engine |
| `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | qa |
| `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | devops |
| `tools/quality/**`, `docs/quality/**` | quality-gate |
| `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md` | CTO |

beat-analysis has no work in this feature.

## Key flows

1. **Export request**: UI (track page or mixer dialog) -> `POST /api/assets/audio-exports` ->
   `ExportService`: validate (track completed, channels exist, range, format, disk space) ->
   `RenderPlanner` (gate rule v2) -> `Asset(queued)` with recipe and contents -> worker
   `POST /v1/render-jobs` -> `WorkerJobId` stored -> `202 Asset`.
2. **Render** (worker `render` lane): validate paths -> pass 1 (peak, progress 0-50 %) -> pass 2
   (scale, dither, encode block-wise, progress 50-100 %) -> `RenderResult`.
3. **Sync**: `AssetSyncService` polls active assets -> on `completed` hashing copy of
   `/v1/render-jobs/{id}/file` to `assets/<id>/audio.<ext>` -> verify SHA-256 -> `ready` ->
   `DELETE /v1/render-jobs/{id}`.
4. **Library**: `GET /api/assets` (filters) -> list; play via `<audio src=".../files/audio?inline=true">`
   (Range); download via `Content-Disposition: attachment`; rename `PATCH`; delete `DELETE`
   (cancels an active worker job); retry re-plans the recipe.
5. **Track deletion**: unchanged for tracks; assets keep their snapshot; `trackAvailable` is
   computed on read.

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| Gate rule implemented twice (TS engine, C# planner) | The API must resolve "what I hear" without a browser (agent, retry from recipe) | Rendering in the browser cannot serve the agent or pipelines (research.md E-06); drift is prevented by shared vectors consumed by both test suites |
| A second scheduler (`LaneScheduler`) in the worker | Exports must not wait up to ~30 min behind a CPU detail phase (FR-011) | Putting renders into the priority queue would violate FR-011; a separate process/service is heavier than one thread |
| Read-only mount of the API data dir into the worker | Inputs are stems, detail stems and assets owned by the API | Uploading inputs per job moves up to ~1.6 GB per export; the worker cache does not hold detail stems or assets |
| Two-pass rendering | Clip protection needs the global peak before writing, and memory must stay bounded for 15-minute songs | One pass in memory needs up to ~7 GB; a limiter would change the sound |
