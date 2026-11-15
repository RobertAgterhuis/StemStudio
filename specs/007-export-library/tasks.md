---

description: "Task list for feature 007 Export isolated channels / asset library (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Export Isolated Channels (Asset Library)

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/DELTA.md](contracts/DELTA.md),
constitution 1.2.0, ADR-016.

**Status**: Phase 0 **done** (2026-11-15, combined with 005 Phase 0; hand-off
`docs/handoffs/architect-005-007-phase0.md`): worker 1.4.0, backend 1.5.0, UI test ids 1.5.0,
render-vectors 1.0.0, each = 005's version plus the 007 additions. Original note: (it runs when 007 starts, on top of the
contract versions current at that time; 004-006 may bump them first).

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component in its hand-off. No task needs a model or a GPU.

**Organization**: grouped by user story (US1..US5); the
[execution plan](#execution-plan-per-owner-parallel-wave) regroups the IDs per agent.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on
  an incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: the agent from `.claude/agents/`; it writes only inside its owned paths (plan.md
  ownership map) plus `docs/handoffs/<owner>-007-<checkpoint>.md`.
- Paths are relative to the repository root. **CTO** = orchestrator.
- beat-analysis has no tasks in this feature.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-007-<checkpoint>.md` at `foundational` (if it has
foundational tasks), every story it takes part in (`us1`..`us5`) and `polish`, with red -> green
commit hashes per task pair, exact coverage numbers and lint/SIG output. Marker `HO:<checkpoint>`
in the execution plan.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| stem-separation (`jobs/`, `api/`) | the renderer | `ports.py` `RenderExecutor`, `LibraryReader`, `RenderError`, `InputPathError` (T001) | a fake executor (fixed result, raises, records cancel) |
| audio-ingest (`render/`) | request/result shapes, mixing law | `schemas.py` render models (T001), data-model 2-3, `render-vectors.json` (T004) | n/a (pure functions + tmp FLAC fixtures) |
| backend | worker render jobs | worker-api (T003; examples copied to `tests/StemStudio.Api.Tests/Worker/Examples/`) | stub `HttpMessageHandler` |
| frontend | assets API | backend-api (T003) + Contracts DTOs (T002) | fake `IStemStudioApiClient` |
| audio-engine | gate vectors | `render-vectors.json` (T004) | n/a |
| qa | UI | `ui-test-ids.md` (T003), fake stack | suite authored in the wave, executed in T071 |
| devops | mount, env vars | data-model 1, plan.md | skeleton |

---

## Phase 0: Contract code and skeleton (architect, BEFORE the wave)

- [x] T001 [architect] Worker contract code: `schemas.py` (`CONTRACT_VERSION` next minor; `RenderInput`, `RenderRange`, `RenderOutput`, `RenderMetadata`, `RenderJobRequest`, `RenderJobStatus`, `RenderJob`, `RenderResult`, `Health.library`, `Health.lanes` with defaults); `ports.py` (`LibraryReader`, `RenderExecutor` protocols, `RenderProgress`, `RenderError`, `InputPathError`, `InputNotFoundError`); `config.py` (`library_dir` from `STEMSTUDIO_LIBRARY_DIR`, `render_block_seconds`); `lameenc==1.9.0` as a direct dependency in `worker/pyproject.toml` + `uv.lock`; skeleton tests `worker/tests/test_render_contract.py`; worker contract tests switched to the 007 document (agreed one-time edit of the `/health` assertions in stem-separation tests) (FR-004, FR-012)
- [x] T002 [P] [architect] .NET contract code: `src/StemStudio.Contracts/Assets/*` (data-model 4, DELTA 2), `ApiRoutes.Assets`, `ContractLimits` additions (data-model 6); skeleton tests `tests/StemStudio.Api.Tests/Contract/AssetContractTests.cs`; `StemStudio.Api.Tests.csproj` links -> 007 contracts; new operationIds staged in `PendingAssetOperations` (agreed one-time edit in backend tests, un-staged by the backend per story) (FR-012)
- [x] T003 [P] [architect] Contracts: full `worker-api.openapi.yaml`, `backend-api.openapi.yaml` (OpenAPI 3.1, Redocly lint clean, examples per DELTA 1-2), `ui-test-ids.md` (DELTA 5), `redocly.yaml`; ADR-016 status -> Accepted
- [x] T004 [P] [architect] `contracts/render-vectors.json` 1.0.0 with the cases of DELTA 4 and its generator `contracts/generate_render_vectors.py` (pure Python, documented formulas; matrices to 1e-9) (FR-003, SC-003)
- [x] T005 [architect] Verify the skeleton: `dotnet build` 0 warnings, all .NET tests + coverage gates, worker `pytest --cov`/ruff/mypy, engine tests, `tools/quality/run_all.py` 0 errors; hand-off `docs/handoffs/architect-005-007-phase0.md` (combined with 005) (contract versions assigned, dependency record for lameenc)

**Checkpoint**: skeleton green; wave may start.

---

## Phase 1: Setup (per owner, no story)

- [x] T006 [P] [quality-gate] Failing tests in `tools/quality/tests/`: `worker/src/stemstudio_worker/render` may import only `ports`, `schemas`, `config`; `jobs` and `api` must not import `render` (only `main.py` composes it); `StemStudio.Domain.Rendering` must not reference `StemStudio.Api` or `StemStudio.Persistence`
- [x] T007 [quality-gate] Implement the rules in `tools/quality/dependency_check.py`; re-run `run_all.py`; update `docs/quality/SIG-REPORT.md`
- [x] T008 [P] [devops] `docker-compose.yml`: worker gets `api-data:/library:ro` and `STEMSTUDIO_LIBRARY_DIR: /library` (comment: read-only, the API is the only writer); `scripts/dev.{ps1,sh}` set `STEMSTUDIO_LIBRARY_DIR` to the API data dir; CI `CONTRACTS_DIR` and `scripts/test-all.*` -> `specs/007-export-library/contracts` (FR-012, ADR-016 D5)
- [ ] T009 [devops] Write the mount check first and show it fail on the current compose file, then make it pass: `scripts/check-library-mount.{ps1,sh}` (worker `/health` reports `library.readable: true`; a write attempt as the worker user inside `/library` fails with "Read-only file system"); run it in the CI `e2e` job after the fake stack is up
- [x] T010 [P] [qa] Update `docs/testing/TEST-STRATEGY.md`: render tested with generated FLAC fixtures, shared vectors (three consumers + browser check), fake stack exports are real files, traceability US1..US5 -> test files

---

## Phase 2: Foundational (blocking inside each owner)

### audio-ingest (`render/`, tests first)

- [x] T011 [P] [audio-ingest] Failing tests `worker/tests/render/test_mixing.py`: `pan_matrix(p)` equals every `render-vectors.json` pan case to 1e-9; `mix_block(blocks, inputs, master)` applies gain, the stereo panner law and master gain (two-sample signals, data-model 3); pan 0 is the identity (FR-003, SC-003)
- [x] T012 [audio-ingest] Implement `render/mixing.py` to pass T011
- [x] T013 [P] [audio-ingest] Failing tests `worker/tests/render/test_paths.py`: the three allow-list patterns accept valid paths and reject upper-case ids, `..`, absolute paths, backslashes, unknown stems, other extensions; a symlink that escapes the root is rejected after resolving; a missing file raises `InputNotFoundError` naming the path; an unset/unreadable root raises `RenderError("The library is not available to the worker")` (data-model 2.1, ADR-016 D5)
- [x] T014 [audio-ingest] Implement `render/paths.py` (`LibraryReader` implementation) to pass T013
- [x] T015 [P] [audio-ingest] Failing tests `worker/tests/render/test_reader.py` (FLAC fixtures written in `tmp_path`): block-wise reading of N inputs of different length pads with silence to the longest; range start/end in samples (end clamped); mono duplicated to stereo; a 48 kHz input fails with "Unsupported sample rate 48000 Hz in <path>"; concatenated blocks equal a whole-file read (research.md E-04)
- [x] T016 [audio-ingest] Implement `render/reader.py` to pass T015
- [x] T017 [P] [audio-ingest] Failing tests `worker/tests/render/test_encoders.py`: FLAC/WAV 16/24 written with the requested subtype and `title`/`artist`/`comment` tags; MP3 192/256/320 CBR decodes (soundfile/libsndfile MP3 read) to between the expected length and that length plus two MP3 frames (lameenc writes no gapless LAME/Xing header, so encoder delay and frame padding remain; reconciliation 2026-11); TPDF dither is deterministic per seed and absent when `dither=false`; a raw 16-bit input written without dither is sample-identical to the input (FR-004, SC-004)
- [x] T018 [audio-ingest] Implement `render/dither.py` and `render/encoders.py` to pass T017
- [x] T019 [P] [audio-ingest] Failing tests `worker/tests/render/test_executor.py`: two passes with progress 0..0.5 and 0.5..1; peak > 1.0 -> output peak -0.1 dBFS and `appliedGainDb` reported; peak <= 1.0 -> `appliedGainDb = 0`; all-silent render raises "Nothing to export: the selected channels are silent"; cancellation between blocks stops and removes the partial file; `RenderResult` fields (size, SHA-256, duration, peak) match the written file (FR-005, SC-005)
- [x] T020 [audio-ingest] Implement `render/executor.py` and `render/__init__.py::create_render_executor` to pass T019

### stem-separation (`jobs/`, tests first)

- [x] T021 [P] [stem-separation] Failing tests `worker/tests/jobs/test_lanes.py`: `LaneScheduler` runs jobs FIFO on its own thread, one at a time; a job in the render lane starts while a fake separation blocks the `PriorityScheduler`; exceptions of one job do not stop the lane; `pending` and `running` are reported for `/health` (FR-011)
- [x] T022 [stem-separation] Implement `jobs/lanes.py` to pass T021
- [x] T023 [P] [stem-separation] Failing tests `worker/tests/jobs/test_render_store.py`: render jobs persisted as `render-jobs/<id>.json`; after a restart `queued`/`running` become `failed` "interrupted by restart"; `delete` removes JSON + output (idempotent); the sweep removes terminal jobs older than 24 h (fake clock) (data-model 2.5)
- [x] T024 [stem-separation] Implement `jobs/render_store.py` to pass T023

### backend (tests first)

- [x] T025 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Assets/AssetTests.cs`: status transitions (queued -> rendering -> ready / failed; retry only from failed), progress clamping, `Files`/`Audio` only when ready, name rules (trim, 1..120, no control characters), default name with " (2)" suffix, contents text (data-model 4.1-4.4)
- [x] T026 [backend] Implement `src/StemStudio.Domain/Assets/*` to pass T025
- [x] T027 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Rendering/RenderPlannerTests.cs`: every gate case of `render-vectors.json` (loaded from the linked contracts folder) yields the expected plan; unknown channel ids, detail stems of non-split parents, duplicates and > 56 channels are validation errors naming `channels[i]`; empty plan -> `nothing-to-export`; paths per data-model 4.3 rule 7 (FR-002, FR-003, SC-003)
- [x] T028 [backend] Implement `src/StemStudio.Domain/Rendering/*` to pass T027
- [x] T029 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/AssetPersistenceTests.cs`: JSON round trip of `Files`, `Audio`, `Provenance` (incl. recipe); `TrackId` filter column; a database migrated from the previous schema loads with an empty `Assets` table (file SQLite)
- [x] T030 [backend] Implement the configuration and migration `AddAssets` to pass T029
- [x] T031 [P] [backend] Failing tests `tests/StemStudio.Api.Tests/Worker/WorkerRenderClientTests.cs` with the worker examples: create (202), get (each status), file download stream, delete (204, 404 tolerated), 400/404/422 problems mapped to typed exceptions with the worker `detail`; unreachable -> `WorkerUnavailableException`
- [x] T032 [backend] Implement `src/StemStudio.Api/Worker/WorkerClient.Render.cs`, `WorkerRenderJob.cs` to pass T031

**Checkpoint**: HO:foundational for audio-ingest, stem-separation and backend.

---

## Phase 3: User Story 1 - Save one stem or detail stem (P1) MVP

**Goal**: raw single stem / detail stem -> library item that plays and downloads.
**Independent test**: quickstart X2, X3; E2E `export.spec.ts` "raw stem".

- [x] T033 [P] [US1] [stem-separation] Failing API tests `worker/tests/api/test_render_routes.py` (TestClient, fake executor, real store in `tmp_path`): `POST /v1/render-jobs` -> 202 queued and the job runs in the render lane; invalid paths -> 400 `invalid-input-path`, missing input -> 404 `input-not-found`, bad ranges/formats -> 422; `GET` reflects progress and result; `GET .../file` 409 until completed then the bytes with `Content-Length`; `DELETE` cancels/removes (204); `/health` reports `library` and `lanes`; responses validate against the 007 worker contract (FR-001, FR-011)
- [x] T034 [US1] [stem-separation] Implement `jobs/render_service.py`, `api/render_routes.py`, `/health` additions and the composition in `main.py` (lane, store, `create_render_executor`, restart reconciliation, sweep timer) to pass T033
- [x] T035 [P] [US1] [backend] Failing tests `tests/StemStudio.Api.Tests/Assets/RawExportTests.cs`: `POST /api/assets/audio-exports` mode `raw` for a stem and a detail stem -> 202 `Asset` (queued, default name, contents, recipe with `bitDepth` 16 and `dither=false` sent to the worker); track not completed -> 409; free space below `MinFreeDiskBytes` (fake `IDiskSpace`) -> 409 `insufficient-disk-space`; sync: worker running -> `rendering` + progress, completed -> hashing copy to `assets/<id>/audio.flac`, SHA-256 verified, `ready`, worker job deleted; `GET /api/assets/{id}/files/audio` serves Range/ETag/`Content-Disposition` (RFC 6266 `filename*`, sanitised) and `inline=true`; un-stage `createAudioExport`, `getAsset`, `getAssetFile` (FR-001, FR-004, FR-015, SC-004)
- [x] T036 [US1] [backend] Implement `Assets/{ExportService,AssetFileStore,AssetJobApplier,AssetSyncService,DiskSpaceGuard,ContentDisposition,AssetEndpoints}.cs` (+ registration) to pass T035
- [x] T037 [P] [US1] [frontend] Failing bUnit tests: track page shows `export-stem-<stem>` for each stem and `export-detail-<parent>-<detail>` only for kept detail stems of split parents; `ExportDialog` (source fixed) shows `export-format` with FLAC preselected, hides `export-bit-depth`/`export-mp3-bitrate` below Advanced (004 level context, faked), submits the raw request through `IStemStudioApiClient`, shows `export-saved` with `export-saved-open` or `export-error` with the problem detail; help ids `export-dialog`, `export-formats` present (FR-001, FR-014)
- [x] T038 [US1] [frontend] Implement the track-page actions, `Components/Export/{ExportDialog,FormatPicker}.razor`, client methods and the help topics `export-dialog`, `export-formats` (004 framework) to pass T037
- [x] T039 [P] [US1] [frontend] Failing bUnit tests: `/library` page (`nav-library`) lists assets newest first with `asset-row-<id>`, name, contents, status (`aria-valuenow` while rendering, polled until terminal), `asset-play-<id>` (`<audio>` with the inline URL) and `asset-download-<id>` only when ready; `library-empty` when none; help id `library` (FR-007, FR-008)
- [x] T040 [US1] [frontend] Implement `Components/Library/{Library,AssetRow}.razor`, the navigation item and the help topic `library` to pass T039
- [x] T041 [US1] [qa] Author `e2e/tests/export.spec.ts` "raw stem" (fake stack): save `bass` FLAC 16 from the track page, open the item via `export-saved-open`, wait for `ready`, download it and compare its bytes' decoded samples with the stem served by `GET /api/tracks/{id}/stems/bass` (helper decodes FLAC with the engine-free Node decoder used by the fixtures, or compares via a test-only API call; SC-004); save `drums.snare` as MP3 and check that the `<audio>` element reports a duration > 0

**Checkpoint**: HO:us1.

---

## Phase 4: User Story 2 - Save what I hear, or a channel selection (P1)

**Goal**: mixer exports with the current mix; equality with the engine.
**Independent test**: quickstart X4, X5, X6; E2E "what I hear", panner law.

- [x] T042 [P] [US2] [backend] Failing tests `tests/StemStudio.Api.Tests/Assets/MixExportTests.cs`: mode `mix` with an unsaved `mix` body and with `mixId` (exactly one, else 422); `channels = null` uses the audible level; explicit channels ignore mute/solo; the worker request carries the planner's gains/pans/master and `bitDepth` 24 by default; `nothing-to-export` -> 422; mix validation errors keep their field paths (`mix.detailChannels[0].stem`); the recipe stores the mix snapshot (also for `mixId`); `appliedGainDb` from the worker result is exposed in `audio` (FR-002, FR-003, FR-005, FR-010)
- [x] T043 [US2] [backend] Implement anything missing in `ExportService`/mapping to pass T042
- [x] T044 [P] [US2] [frontend] Failing bUnit tests: `mixer-export` opens the dialog with `export-source-audible` selected and passes the mixer's current (unsaved) state; `export-source-selected` (Advanced) shows `export-channel-<id>` checkboxes prefilled with the currently audible channels; an empty selection disables `export-submit`; the request matches the selection; help id present (FR-002, FR-014)
- [x] T045 [US2] [frontend] Implement `Components/Export/ChannelPicker.razor` and the mixer integration to pass T044
- [x] T046 [P] [US2] [audio-engine] Vector consumer `src/audio-engine/tests/render-vectors.test.ts`: every gate case of `render-vectors.json` through the engine's real gate functions gives the expected per-channel gates/gains (test-only; it must pass against the existing engine; a failure is reported to the architect as a contract defect, no engine change in this feature) (FR-003, SC-003)
- [x] T047 [US2] [qa] Author `export.spec.ts` "what I hear": in the mixer set solo drums + bass, bass pan -40 %, master 70 %, export WAV 16, download and check with a fixture helper that the file's RMS per channel matches the value computed from the fake stems and the vector law (+/- 0.1 dB) and that muted/non-soloed stems are absent (FR-002, FR-003)
- [x] T048 [US2] [qa] Author `e2e/tests/panner-law.spec.ts`: in Chromium, an `OfflineAudioContext` renders a constant stereo signal (1, 0.5) through a `StereoPannerNode` at every pan of `render-vectors.json`; the outputs match the vector matrices to 1e-6 (research.md E-05, SC-003)

**Checkpoint**: HO:us2.

---

## Phase 5: User Story 3 - Manage the personal library (P1)

**Goal**: filter, rename, delete, retry, total size; items survive track deletion.
**Independent test**: quickstart X8, X9; E2E `library.spec.ts`.

- [x] T049 [P] [US3] [backend] Failing tests `tests/StemStudio.Api.Tests/Assets/LibraryTests.cs`: `GET /api/assets` filters (`kind`, `trackId`, `status`, `q`), paging, newest first, `total` and `totalSizeBytes`; `PATCH` rename (422 rules); `DELETE` removes row + folder and cancels an active worker job (late completion is ignored and its worker job deleted); `retry` only from failed (409 otherwise), re-plans the recipe, 409 `source-unavailable` when the track is gone; deleting a track keeps its assets with `trackAvailable = false`; worker 404 during sync -> failed "lost this job"; un-stage the remaining operations (FR-007..FR-010)
- [x] T050 [US3] [backend] Implement `AssetService` (list/rename/delete/retry) and the track-availability lookup to pass T049
- [x] T051 [P] [US3] [frontend] Failing bUnit tests: `library-filter-track`, `library-filter-kind`, `library-search` narrow the list; `library-total-size` formatted; rename dialog (`asset-rename-*`) with validation; delete with `confirm-dialog-ok`; `asset-retry-<id>` only for failed with `asset-error-<id>`; `asset-gain-note-<id>` only when `appliedGainDb < 0` (help id `export-clipping`); `asset-source-<id>` with `data-track-available` (FR-005, FR-007, FR-008)
- [x] T052 [US3] [frontend] Implement the library management UI and help topic `export-clipping` to pass T051
- [x] T053 [US3] [qa] Author `e2e/tests/library.spec.ts`: save three items, filter by track, rename (downloaded file name follows), delete one (gone after reload), delete the source track of another (item stays, `data-track-available="false"`, still plays)

**Checkpoint**: HO:us3.

---

## Phase 6: User Story 4 - Export only a part of the song (P2)

- [x] T054 [P] [US4] [backend] Failing tests: `range` validation (start >= 0, end <= duration, length >= 0.5 s; 422 with field `range`), range passed to the worker and included in the contents text (FR-006)
- [x] T055 [US4] [backend] Implement to pass T054
- [x] T056 [P] [US4] [frontend] Failing bUnit tests: `export-range-loop` only when the mixer has a loop region (Advanced), fills start/end; `export-range-start`/`export-range-end` validation messages; help id `export-range` (FR-006, FR-014)
- [x] T057 [US4] [frontend] Implement the range controls and help topic to pass T056
- [x] T058 [US4] [qa] Author `export.spec.ts` "loop region": loop 2.0-4.5 s on the fake track, export "Only the loop region", downloaded WAV lasts 2.500 s +/- 1 ms

**Checkpoint**: HO:us4.

---

## Phase 7: User Story 5 - Save all stems at once (P3)

- [x] T059 [P] [US5] [backend] Failing tests `POST /api/assets/stem-packs`: one raw asset per standard stem (catalog order) and, with `includeDetail`, per kept detail stem of each split parent; disk guard applies once for the whole pack; un-stage `createStemPack` (FR-013)
- [x] T060 [US5] [backend] Implement to pass T059
- [x] T061 [P] [US5] [frontend] Failing bUnit tests: `export-all-stems` (Advanced) opens the dialog with `export-include-detail` (only when the track has split parents) and submits the pack request; the confirmation names the number of items (FR-013)
- [x] T062 [US5] [frontend] Implement to pass T061
- [x] T063 [US5] [qa] Author `export.spec.ts` "stem pack": six items appear (fake track without detail), all reach `ready`

**Checkpoint**: HO:us5.

---

## Phase 8: Polish (inside the wave, per owner)

- [x] T064 [P] [audio-ingest] Refactor to SIG limits, docstrings, HO:polish with coverage of `render/`
- [x] T065 [P] [stem-separation] Refactor, docstrings, HO:polish with coverage of `jobs/`, `api/`
- [x] T066 [P] [backend] Refactor, XML docs, HO:polish with Domain/Persistence/Api coverage
- [x] T067 [P] [frontend] Accessibility (status text not colour-only, keyboard use of dialogs, `<audio>` labels), help texts final, HO:polish
- [x] T068 [P] [qa] Traceability matrix (US1..US5, FR-001..FR-015 -> tests), HO:polish
- [x] T069 [P] [devops] Validate compose/CI (`docker compose config -q` for both compose variants, `actionlint`), HO:polish
- [ ] T070 [P] [quality-gate] `run_all.py` on the whole repo; refactor requests per owner; HO:polish

---

## Integration wave (CTO, after all wave hand-offs are accepted)

- [x] T071 [CTO] Merge the owner branches; full build, lint, unit + coverage gates green; Playwright default suite (incl. export, library, panner-law specs) on the fake stack (retries 0) (2026-11-15: merged; qa integration run, retries 0: `test:export` 58 passed + 8 Chromium-only skips, `test:detail` 26/26, a11y 104/104 (106/106 at close-out), visual 80/80; HO:qa-005-007-integration)
- [ ] T072 [CTO] Reference-machine validation quickstart X2-X10 on the real stack with "Hero of the Day"; record timings (SC-001, SC-002 while a detail phase runs) and the X10 first-time-user check (SC-006) in `docs/STATUS.md` (OPEN 2026-11-15: real-stack reference-machine validation not run)
- [ ] T073 [CTO] `docs/THIRD-PARTY-LICENSES.md`: `lameenc` 1.9.0 (LGPL-3.0-or-later) moved to direct dependencies; README: "Library and exports" section (formats, raw vs mixed, clip protection, no MP3 tags, read-only mount in the non-Docker path) (PARTLY 2026-11-15: `lameenc` recorded as a direct dependency; README section still open)
- [x] T074 [CTO] `/speckit-analyze` consistency pass; update `docs/STATUS.md`; PR (2026-11-15: STATUS updated. The formal `/speckit-analyze` pass was NOT run; consistency is covered by the contract tests and the E2E suites. No PR opened: the branch is handed to the owner)
- [x] T074a [CTO] Update docs/public manuals and regenerate PDFs (constitution 1.3.1, last step of the integration wave, after `docs/STATUS.md` is updated): edit the chapters of this feature in `docs/public/src/user-manual/` and/or `docs/public/src/technical-manual/` (remove the *In development* marker, describe the delivered behaviour, setup, env vars, contracts and limits; add screenshots from the visual baselines where useful), run `cd tools/docs && npm ci && npm run browser && npm run build`, commit the sources, both PDFs and `docs/public/build-manifest.json` together; `node tools/docs/check.mjs` passes (2026-11-15: chapter 9, levels table, FAQ and tech manual updated, PDFs rebuilt, `check.mjs` passes)

---

## Execution plan per owner (parallel wave)

All owners start after Phase 0 from the Phase 0 commit, each in its own worktree. Order inside a
sequence is binding.

| Owner | Ordered task sequence | Owned paths (write) | May mock / fake |
|---|---|---|---|
| audio-ingest | T011 -> T012 -> T013 -> T014 -> T015 -> T016 -> T017 -> T018 -> T019 -> T020 -> HO:foundational -> T064 (HO:polish) | `worker/src/stemstudio_worker/render/**`, `worker/tests/render/**` | tmp FLAC fixtures, seeded RNG |
| stem-separation | T021 -> T022 -> T023 -> T024 -> HO:foundational -> T033 -> T034 -> HO:us1 -> T065 (HO:polish) | `worker/src/stemstudio_worker/{jobs,api}/**`, `main.py`, `worker/tests/{jobs,api}/**` | fake `RenderExecutor`, fake clock, blocked fake separation |
| backend | T025 -> T026 -> T027 -> T028 -> T029 -> T030 -> T031 -> T032 -> HO:foundational -> T035 -> T036 -> HO:us1 -> T042 -> T043 -> HO:us2 -> T049 -> T050 -> HO:us3 -> T054 -> T055 -> HO:us4 -> T059 -> T060 -> HO:us5 -> T066 (HO:polish) | `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | stub worker handler with 007 examples, `FakeTimeProvider`, fake `IDiskSpace`, SQLite |
| frontend | T037 -> T038 -> T039 -> T040 -> HO:us1 -> T044 -> T045 -> HO:us2 -> T051 -> T052 -> HO:us3 -> T056 -> T057 -> HO:us4 -> T061 -> T062 -> HO:us5 -> T067 (HO:polish) | `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | fake `IStemStudioApiClient`, fake 004 level context, bUnit |
| audio-engine | T046 -> HO:us2 | `src/audio-engine/tests/**` | n/a |
| qa | T010 -> T041 -> HO:us1 -> T047 -> T048 -> HO:us2 -> T053 -> HO:us3 -> T058 -> HO:us4 -> T063 -> HO:us5 -> T068 (HO:polish) | `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | suite executed in T071 |
| devops | T008 -> T009 -> HO:foundational -> T069 (HO:polish) | `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | skeleton |
| quality-gate | T006 -> T007 -> HO:foundational -> T070 (HO:polish) | `tools/quality/**`, `docs/quality/**` | sample files |
| architect | T001 -> T002 -> T003 -> T004 -> T005 (before the wave; later contract fixes on request) | `specs/**`, ADRs, skeleton files | n/a |
| CTO | T071 -> T072 -> T073 -> T074 | `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, merges | n/a |

Task count per owner: architect 5, audio-ingest 11, stem-separation 7, backend 19, frontend 13,
audio-engine 1, qa 8, devops 3, quality-gate 3, CTO 4 (total 74: T001-T074).

### Flagged cross-owner tasks (converted)

| Task | Would have needed | Converted to |
|---|---|---|
| T001 `/health` assertions in stem-separation tests | stem-separation test files | one-time edit by the architect, recorded in the hand-off |
| T002 backend contract staging | backend test files | operations staged by the architect; un-staged by the backend per story |
| T046 engine vector test | engine source change if it fails | test-only task; a failure goes back to the architect as a contract defect |
| T073 licences | owner hand-offs | consolidated by the CTO (AGENT-PROTOCOL rule 9) |

## Dependencies & Execution Order

Phase 0 -> wave (Setup + Foundational + stories per owner, parallel across owners) -> Integration
wave. Inside the wave every owner follows its sequence; stories are independent at the contract
level (fakes). Feature dependencies: 004 merged (help framework, journey levels, design system);
002/003 merged (detail stems, device policy). 008 starts only after 007's Phase 0 (it extends
the asset model and the worker lanes).

## Definition of Done checklist (mirrors the constitution)

- [ ] Red-green-refactor followed; failing test commit before implementation commit (I)
- [ ] All tests green; >= 90 % line AND branch per component, exact numbers in the hand-off (I)
- [ ] Shared vectors consumed by engine, backend and worker suites; browser panner check green (I, FR-003)
- [ ] SIG limits respected; `tools/quality/run_all.py` passes incl. the new dependency rules (II)
- [ ] Implementations match the 007 worker/backend/ui-test-id documents; contract tests pass; engine contract unchanged (III)
- [ ] No new library in the images; lameenc recorded as a direct dependency; no GPL library imported by our code (IV)
- [ ] Clip protection and limitations (no MP3 tags, 44.1 kHz) shown to the user; help topics for every new area (V, FR-014)
- [ ] No secrets; read-only mount verified; paths validated; analyzers/formatters/linters clean; public APIs documented
- [ ] docs/public user and/or technical manual updated for this feature and the PDFs regenerated; `node tools/docs/check.mjs` passes (constitution 1.3.1)
- [ ] Hand-off notes written; Conventional Commits with the Co-Authored-By trailer
