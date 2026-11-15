---

description: "Task list for feature 008 Sheet music per instrument (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Sheet Music per Instrument

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/DELTA.md](contracts/DELTA.md),
constitution 1.2.0, ADR-016, ADR-017.

**Status**: **Phase 0 done** (2026-11-15, combined with 006 Phase 0; hand-off
`docs/handoffs/architect-006-008-phase0.md`): worker-api 1.6.0, backend-api 1.7.0, ui-test-ids
1.7.0 (on top of 006's documents); matplotlib excluded; piano `GpuNeed` 1280 MiB; Leipzig ->
`Leipzig.ttf`. The wave may start; frontend tasks wait for the 004 merge.

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component in its hand-off (vendored code under `transcription/vendor/`
excluded, as `detail/vendor/msst` in 002). Real models run only in tests marked
`@pytest.mark.integration` (T040, T051), excluded from the default CI run.

**Organization**: grouped by user story (US1..US5); the
[execution plan](#execution-plan-per-owner-parallel-wave) regroups the IDs per agent.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on
  an incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: the agent from `.claude/agents/`; it writes only inside its owned paths (plan.md
  ownership map) plus `docs/handoffs/<owner>-008-<checkpoint>.md`.
- Paths are relative to the repository root. **CTO** = orchestrator.
- audio-engine has no tasks in this feature.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-008-<checkpoint>.md` at `foundational` (if it has
foundational tasks), every story it takes part in (`us1`..`us5`) and `polish`, with red -> green
commit hashes per task pair, exact coverage numbers and lint/SIG output. Marker `HO:<checkpoint>`
in the execution plan.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| stem-separation (`jobs/`, `api/`, composition) | notation, engraving, drum onsets | `ports.py` `PitchTranscriber`, `DrumTranscriber`, `Notator`, `ScoreEngraver`, `NoteEvent`, `DrumHit`, `TempoGrid`, `ScoreDocument` (T001) | fakes returning fixed `ScoreDocument`/PDF bytes |
| beat-analysis (`notation/`, `analysis/drum_onsets.py`) | note/hit shapes, profiles | `ports.py` (T001), data-model 2.1-2.7 | synthetic note lists, generated click audio |
| audio-ingest (`engraving/`) | MusicXML in, PDF/SVG out | `ports.py` `ScoreEngraver` (T001), data-model 3 | MusicXML fixtures written by music21 in the test |
| backend | worker transcription jobs | worker-api (T004; examples copied to `tests/StemStudio.Api.Tests/Worker/Examples/`) | stub `HttpMessageHandler` |
| frontend | scores API | backend-api (T004) + Contracts DTOs (T003) | fake `IStemStudioApiClient` |
| qa | UI | `ui-test-ids.md` (T004), fake stack with fake transcriber | suite authored in the wave, executed in T079 |
| devops | font, env vars | data-model 1, `worker/scripts/install_music_font.py` (T002) | skeleton |

---

## Phase 0: Contract code and skeleton (architect, BEFORE the wave)

- [x] T001 [architect] Worker contract code: `schemas.py` (next minor; `ScoreProfile`, `ScoreQuality`, `ScoreResolution`, `TempoGrid` variants, `DrumInputs`, `ScoreTitles`, `TranscriptionJobRequest`, `TranscriptionStage`, `TranscriptionJob`, `TranscriptionResult`, `ScoreSummary`, `KeyInfo`, `ScoreWarning`, `Health.transcription`, `Health.lanes.transcribe`); `ports.py` (`NoteEvent`, `DrumHit`, `TempoGrid`, `ScoreDocument`, `PitchTranscriber`, `DrumTranscriber`, `Notator`, `ScoreEngraver`, `TranscriptionError`, `NoNotesError`); `config.py` (`transcriber`, `transcribe_threads`); dependencies in `pyproject.toml` + `uv.lock` (music21 10.5.2, verovio 6.4.1, cairosvg 2.9.1, pypdf, torchlibrosa 0.1.0; decide matplotlib: lock it or exclude it with a uv override, record why); mypy overrides for untyped packages; skeleton tests `worker/tests/test_transcription_contract.py` (FR-003, FR-012)
- [x] T002 [P] [architect] `worker/scripts/install_music_font.py` (build-time: extract the Leipzig WOFF2 from the installed Verovio wheel's `data/Leipzig.css`, convert to OTF with fontTools, write to a given directory, print its SHA-256) with tests `worker/tests/scripts/test_install_music_font.py` (fixture CSS; fontTools mocked when absent) (SC-004)
- [x] T003 [P] [architect] .NET contract code: `src/StemStudio.Contracts/Scores/*`, `AssetDto.Score`, roles and origin enums extended, `ApiRoutes.Scores`, `ContractLimits` additions; skeleton tests `tests/StemStudio.Api.Tests/Contract/ScoreContractTests.cs`; csproj links -> 008 contracts; operations staged in `PendingScoreOperations` (agreed one-time edit in backend tests) (FR-012)
- [x] T004 [P] [architect] Contracts: full `worker-api.openapi.yaml`, `backend-api.openapi.yaml` (Redocly lint clean, examples per DELTA), `ui-test-ids.md`, `redocly.yaml`; ADR-017 status -> Accepted
- [x] T005 [architect] Phase 0 measurements (throw-away containers, read-only volumes): piano CRNN on the GPU image (time for 60 s, peak VRAM via NVML -> `GpuNeed`), verify the basic-pitch download URL at tag v0.4.0 matches SHA-256 `2c3c1d14...`; record in research.md (S-04, S-01) and data-model 2.10/4.5
- [x] T006 [architect] Verify the skeleton: `dotnet build` 0 warnings, all .NET tests + coverage gates, worker `pytest --cov`/ruff/mypy, `tools/quality/run_all.py` 0 errors; hand-off `docs/handoffs/architect-008-plan.md` (versions, dependency + licence records, matplotlib decision)

**Checkpoint**: skeleton green; wave may start.

---

## Phase 1: Setup (per owner, no story)

- [x] T007 [P] [quality-gate] Failing tests in `tools/quality/tests/`: `notation` imports only `ports`, `schemas` and music21; `engraving` only `ports`, verovio, cairosvg, pypdf; `transcription` must not import `notation`, `engraving`, `jobs`, `api`; `jobs`/`api` import none of them (composition in `main.py`); `transcription/vendor/**` excluded from SIG unit checks but its LICENSE/NOTICE files must exist
- [x] T008 [quality-gate] Implement the rules in `tools/quality/`; re-run `run_all.py`; update `docs/quality/SIG-REPORT.md`
- [ ] T009 [P] [devops] Write the font check first and show it fail on the current image: `scripts/check-worker-fonts.{ps1,sh}` (`fc-list` inside `stemstudio/worker:local` lists "Leipzig"; `python -c "import verovio, cairosvg, music21"` succeeds as the worker user) (SC-004)
- [ ] T010 [devops] `docker/worker.Dockerfile`: build stage runs `worker/scripts/install_music_font.py` (fontTools only in that stage) with `fontTools[woff]` (brotli) and copies the printed file (`Leipzig.ttf`, TrueType outlines) to `/usr/local/share/fonts/verovio/` + `fc-cache`; `fontconfig` present; both image variants; `docker-compose.yml` passes `STEMSTUDIO_TRANSCRIBER` (default empty = automatic); CI `CONTRACTS_DIR`/`scripts/test-all.*` -> `specs/008-sheet-music/contracts` (once for 006 + 008; the render-vectors check keeps the 007 path); wire T009 into CI (image job) to pass
- [ ] T011 [P] [qa] Update `docs/testing/TEST-STRATEGY.md`: notation/engraving tested on synthetic notes and MusicXML assertions; integration tests with real models on synthetic audio (SC-002); fake transcriber for E2E; traceability US1..US5 -> test files

---

## Phase 2: Foundational (blocking inside each owner)

### beat-analysis (`notation/`, tests first)

- [ ] T012 [P] [beat-analysis] Failing tests `worker/tests/notation/test_profiles_cleaning.py`: profile table and suggestions (data-model 2.1); cleaning steps 1-6 in order (range, 60 ms monophonic reduction keeps the louder note, next-onset ends, one-grid-step minimum, duplicates merged, per-bar cap with counts) and the warnings they raise (data-model 2.7) (FR-002, FR-010)
- [ ] T013 [beat-analysis] Implement `notation/profiles.py`, `notation/cleaning.py` to pass T012
- [ ] T014 [P] [beat-analysis] Failing tests `worker/tests/notation/test_timebase.py`: interpolation over uneven beats (a note at a beat lands exactly on it despite tempo drift), extrapolation before/after the grid with the median period, bar 1 at the first downbeat, pickup bar length, manual grid generation, grid shift for a range/asset start, 1/16 and 1/8 snapping, time signature and rounded tempo (data-model 2.4, FR-004, FR-006, FR-007, SC-003)
- [ ] T015 [beat-analysis] Implement `notation/timebase.py` to pass T014
- [ ] T016 [P] [beat-analysis] Failing tests `worker/tests/notation/test_keys.py`: estimate on a synthetic G-major line returns G major with confidence > 0.6; < 8 notes -> C major, confidence 0, `few-notes`; override names parsed (30 keys); spelling: F# in G major, G-flat in D-flat major, A-flat (not G#) in A-flat major (research.md S-08) (FR-005)
- [ ] T017 [beat-analysis] Implement `notation/keys.py` to pass T016
- [ ] T018 [P] [beat-analysis] Failing tests `worker/tests/notation/test_builders.py`: from synthetic notes and a grid, the MusicXML (parsed back with music21) has the profile's clefs (treble, bass 8vb, treble 8vb, grand staff split at 60, percussion), the time signature, the tempo mark, the key signature, the expected number of measures (+ pickup), rests filling gaps, ties across bar lines, no duration below the resolution; percussion: two voices (hands up, kick down), x noteheads for hi-hat/cymbals, chords of simultaneous hand hits; MIDI bytes parse with the expected note count; titles/part name set (FR-002, FR-003, FR-004, FR-009, SC-003)
- [ ] T019 [beat-analysis] Implement `notation/builders.py`, `notation/writer.py` and the `Notator` (clean -> quantise -> key -> build -> MusicXML/MIDI) to pass T018

### audio-ingest (`engraving/`, tests first)

- [ ] T020 [P] [audio-ingest] Failing tests `worker/tests/engraving/test_engraver.py`: a music21-written MusicXML fixture renders to N A4 SVG pages with the data-model 3 options; the PDF has N pages, title/producer/subject metadata; `fonts.leipzig_available()` reflects fontconfig (patched); when the font is missing the engraver raises `TranscriptionError("The music font is not installed")` instead of producing boxes; with the font present, the PDF's font list contains Leipzig whenever the SVG has a `font-family="Leipzig"` text (SC-004)
- [ ] T021 [audio-ingest] Implement `engraving/{verovio_svg,pdf,fonts}.py` and the `ScoreEngraver` to pass T020

### stem-separation (`transcription/`, `jobs/`, tests first)

- [ ] T022 [P] [stem-separation] Failing tests `worker/tests/transcription/test_basic_pitch.py` with a fake ONNX session (fixed onset/frame/contour arrays): audio is resampled to 22,050 Hz mono and windowed with the original overlap; outputs are stitched and turned into `NoteEvent`s with thresholds 0.6/0.4 and 127.7 ms; the session is created on the CPU provider with `transcribe_threads`; the model is fetched through the model manager with the pinned id/URL/SHA-256 (data-model 2.10); progress callbacks per window (FR-001)
- [ ] T023 [stem-separation] Vendor the needed basic-pitch post-processing into `transcription/vendor/basic_pitch/` (LICENSE, NOTICE, header naming the source commit; no TF/resampy/pretty_midi imports) and implement `transcription/basic_pitch.py` to pass T022
- [ ] T024 [P] [stem-separation] Failing tests `worker/tests/transcription/test_piano.py` with a fake torch module: the CRNN runs through the injected `DeviceRunner` with the piano `GpuNeed`, on 10 s segments with progress per segment; CPU fallback reason propagates to the job device; the regression post-processor output maps to `NoteEvent`s; no `os.system`, matplotlib or audioread import (import guard test); weights fetched by the model manager (FR-008, FR-011)
- [ ] T025 [stem-separation] Vendor the piano model definition and post-processor into `transcription/vendor/piano_transcription/` (MIT LICENSE kept; drop the matplotlib, audioread **and mido** imports, Phase 0) and implement `transcription/piano.py` to pass T024
- [ ] T026 [P] [stem-separation] Failing tests `worker/tests/transcription/test_fake.py`: the fake transcriber's notes/hits follow data-model 2.6 for every profile and respect ranges and the grid
- [ ] T027 [stem-separation] Implement `transcription/fake.py` and `transcription/__init__.py` factories (real/fake by settings) to pass T026
- [ ] T028 [P] [stem-separation] Failing tests `worker/tests/jobs/test_transcription_store.py`: jobs persisted as `transcription-jobs/<id>.json` with three result files; restart -> `failed` "interrupted by restart"; delete removes everything; 24 h sweep (fake clock)
- [ ] T029 [stem-separation] Implement `jobs/transcription_store.py` to pass T028

### backend (tests first)

- [ ] T030 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Scores/ScoreRecipeTests.cs`: profile suggestion per channel (data-model 2.1), allowed profiles (pitched vs drums), drums only for `drums` with drums split, quality default by device, resolution default per profile, key override names, tempo ranges, range >= 2 s, default name (FR-002, FR-006, FR-008, FR-009)
- [ ] T031 [backend] Implement `src/StemStudio.Domain/Scores/{ScoreRecipe,ScoreFacts,ProfileCatalog}.cs` to pass T030
- [ ] T032 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Scores/TempoGridResolverTests.cs`: every row of data-model 4.3 (reliable grid, shifted by a range, asset with provenance track and range, manual tempo overrides, none -> `tempo-required`) (FR-004, FR-007)
- [ ] T033 [backend] Implement `TempoGridResolver` to pass T032
- [ ] T034 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/ScorePersistenceTests.cs`: `Score` JSON round trip, `kind = score` filter, assets created by 007 load with `Score = null`
- [ ] T035 [backend] Implement the configuration and migration `AddScoreFacts` to pass T034
- [ ] T036 [P] [backend] Failing tests `tests/StemStudio.Api.Tests/Worker/WorkerTranscriptionClientTests.cs` with the worker examples: create/get/files per role/delete, problems mapped, `device` parsed
- [ ] T037 [backend] Implement `Worker/WorkerClient.Transcription.cs`, `WorkerTranscriptionJob.cs` to pass T036

**Checkpoint**: HO:foundational for beat-analysis, audio-ingest, stem-separation and backend.

---

## Phase 3: User Story 1 - Sheet music for a melodic instrument (P1) MVP

**Goal**: stem -> PDF + MusicXML + MIDI library item with limits shown.
**Independent test**: quickstart M3, M6; E2E "bass".

- [ ] T038 [P] [US1] [stem-separation] Failing API tests `worker/tests/api/test_transcription_routes.py` (TestClient; fake transcriber, fake notator/engraver for most cases, one case with the real notation + engraving on fake notes): `POST /v1/transcription-jobs` 202 in the `transcribe` lane (does not wait for a blocked render lane or separation); profile/input mismatches 422; stages and progress ranges (data-model 2.9); result files per role with content types; `409` before completion; "No notes were detected" failure; `/health` `transcription` and `lanes.transcribe`; responses validate against the 008 worker contract (FR-001, FR-003, FR-011)
- [ ] T039 [US1] [stem-separation] Implement `jobs/transcription_service.py`, `api/transcription_routes.py`, the pipeline composition and the lane in `main.py` (threads capped by `transcribe_threads`) to pass T038
- [ ] T040 [US1] [stem-separation] Integration test `worker/tests/transcription/test_accuracy_integration.py` (`@pytest.mark.integration`, downloads basic-pitch): the synthetic single-voice fixture (generator in the test, as research.md S-03) through the real basic-pitch + the beat-analysis cleaning/quantisation reaches note F1 >= 0.90 (SC-002); a failure becomes a tuning request to beat-analysis/stem-separation in the hand-off
- [ ] T041 [P] [US1] [backend] Failing tests `tests/StemStudio.Api.Tests/Scores/CreateScoreTests.cs`: `GET /api/scores/options` (suggested profile, grid status, estimates, limits); `POST /api/assets/scores` for `bass` -> 202 `Asset(kind score)` with recipe, the worker request carries the input path, the grid and titles; sync copies `pdf`, `musicxml`, `midi` with SHA-256 checks and fills `score`; `getAssetFile` serves PDF inline by default and MusicXML/MIDI as attachments; un-stage `getScoreOptions`, `createScore` (FR-001, FR-003, FR-010, FR-012)
- [ ] T042 [US1] [backend] Implement `Scores/{ScoreEndpoints,ScoreService,ScoreOptionsService,ScoreJobApplier}.cs` and the job-kind switch in the 007 `AssetSyncService` to pass T041
- [ ] T043 [P] [US1] [frontend] Failing bUnit tests: `score-stem-<stem>` and `score-detail-<parent>-<detail>` (pitched only) on the track page; `ScoreDialog` loads options, preselects the suggested `score-profile`, shows `score-limits` and `score-estimate`, submits, shows `score-saved`/`score-error`; library rows of kind score show `asset-preview-<id>` (PDF), the three download links, key (with confidence), tempo, warnings, limits; help ids `sheet-music`, `sheet-music-profiles` (FR-002, FR-003, FR-010, FR-014)
- [ ] T044 [US1] [frontend] Implement `Components/Scores/{ScoreDialog,ScoreFactsPanel,PdfPreview}.razor`, client methods, actions and help topics to pass T043
- [ ] T045 [US1] [qa] Author `e2e/tests/sheet-music.spec.ts` "bass": fake stack, generate from the track page, wait for `ready`, the PDF preview is visible, download the three files: the PDF starts with `%PDF`, the MusicXML has the fake grid's measure count, `<fifths>`, `<beats>`/`<beat-type>`, a bass clef with octave change; the MIDI starts with `MThd`

**Checkpoint**: HO:us1.

---

## Phase 4: User Story 2 - Key, tempo and time signature that fit the song (P1)

**Goal**: key override, simplified rhythm, manual tempo for unreliable grids.
**Independent test**: quickstart M4, M5.

- [ ] T046 [P] [US2] [backend] Failing tests: `keyOverride` validation (enum) and pass-through; `resolution` pass-through; unreliable grid without `tempo` -> 422 `tempo-required`, with `tempo` -> manual grid sent; options report `gridStatus` (FR-005, FR-006, FR-007)
- [ ] T047 [US2] [backend] Implement to pass T046
- [ ] T048 [P] [US2] [frontend] Failing bUnit tests: `score-resolution` ("simplified", Beginner); `score-key-override` (Advanced) listing keys with the estimate shown after generation; `score-tempo-required` notice and the three tempo fields required for unreliable grids, prefilled from the mixer's metronome override when available; help id `sheet-music-key-tempo` (FR-005..FR-007, FR-014)
- [ ] T049 [US2] [frontend] Implement to pass T048
- [ ] T050 [US2] [qa] Author `sheet-music.spec.ts` "key and rhythm": regenerate with key "E minor" and simplified; the MusicXML has `<fifths>1</fifths>` and no `<type>16th</type>`

**Checkpoint**: HO:us2.

---

## Phase 5: User Story 3 - Piano on two staves (P2)

- [ ] T051 [US3] [stem-separation] Integration test (`@pytest.mark.integration`, downloads the piano checkpoint): the synthetic polyphonic fixture (research.md S-04) through the real piano CRNN + cleaning reaches note F1 >= 0.85 (SC-002); records the time per audio second on the test machine in the output
- [ ] T052 [P] [US3] [backend] Failing tests: piano options list `accurate` and `fast` with estimates from `StemStudio:ScoreEstimates` and the device (`GET /api/device` faked); default `accurate` on a GPU worker, `fast` on CPU; `attribution` set for the piano engine; the `cpu-piano` warning surfaces in `score.warnings` (FR-008)
- [ ] T053 [US3] [backend] Implement to pass T052
- [ ] T054 [P] [US3] [frontend] Failing bUnit tests: `score-quality` (Advanced) for piano with both estimates; `asset-attribution-<id>` for piano items; help id `sheet-music-piano` (FR-008, FR-014)
- [ ] T055 [US3] [frontend] Implement to pass T054
- [ ] T056 [US3] [qa] Author `sheet-music.spec.ts` "piano": the fake piano score's MusicXML has two staves (`<staves>2</staves>`) with treble and bass clefs

**Checkpoint**: HO:us3.

---

## Phase 6: User Story 4 - Drum notation (P2)

- [ ] T057 [P] [US4] [beat-analysis] Failing tests `worker/tests/analysis/test_drum_onsets.py`: generated click tracks per piece (known times) give hits within 20 ms; the bleed gate (10 % of the 99th percentile strength) removes a quieter copy of another piece's clicks; missing piece inputs produce no hits for that piece; strengths in 0..1 (FR-009)
- [ ] T058 [US4] [beat-analysis] Implement `analysis/drum_onsets.py` (`DrumTranscriber`) to pass T057
- [ ] T059 [P] [US4] [stem-separation] Failing tests: a `drums` job requires `drumInputs` (422 otherwise), runs the `DrumTranscriber` and the percussion notation, reports progress per piece, and adds a warning when a piece input is absent (FR-009)
- [ ] T060 [US4] [stem-separation] Implement the drums path in the service/composition to pass T059
- [ ] T061 [P] [US4] [backend] Failing tests: `drums` without a split drums parent -> 409 `drums-not-split` (options: `drumsRequireSplit = true`); with the split, the five detail paths are resolved (FR-009)
- [ ] T062 [US4] [backend] Implement to pass T061
- [ ] T063 [P] [US4] [frontend] Failing bUnit tests: `score-drums-split-required` notice with `score-drums-split-start` starting the detail phase (existing client call); help id `sheet-music-drums` (FR-009, FR-014)
- [ ] T064 [US4] [frontend] Implement to pass T063
- [ ] T065 [US4] [qa] Author `sheet-music.spec.ts` "drums": on the fake stack split the drums (fake detail), generate drums; the MusicXML has a percussion clef, two voices and `<notehead>x</notehead>` elements

**Checkpoint**: HO:us4.

---

## Phase 7: User Story 5 - A part of the song or a library item (P3)

- [ ] T066 [P] [US5] [backend] Failing tests: `source = {assetId}` for a ready audio asset uses its path and the provenance track's grid shifted by the asset's range start; an asset without a track requires `tempo`; `range` (>= 2 s) shifts the grid; provenance records `sourceAssetIds` and `trackId` (FR-001, FR-012, FR-013)
- [ ] T067 [US5] [backend] Implement to pass T066
- [ ] T068 [P] [US5] [frontend] Failing bUnit tests: `asset-score-<id>` on audio library items opens the dialog with that source; `score-range-start`/`score-range-end` (Advanced) with validation (FR-013, FR-014)
- [ ] T069 [US5] [frontend] Implement to pass T068
- [ ] T070 [US5] [qa] Author `sheet-music.spec.ts` "from a library item": export a loop region (007), generate sheet music from that item; the score item's source shows both the audio item and the track

**Checkpoint**: HO:us5.

---

## Phase 8: Polish (inside the wave, per owner)

- [ ] T071 [P] [beat-analysis] Refactor to SIG limits, docstrings, HO:polish with coverage of `notation/`, `analysis/drum_onsets.py`
- [ ] T072 [P] [audio-ingest] Refactor, docstrings, HO:polish with coverage of `engraving/`
- [ ] T073 [P] [stem-separation] Refactor, docstrings, HO:polish with coverage of `transcription/` (vendor excluded), `jobs/`, `api/`; dependency records (vendored sources with commit ids)
- [ ] T074 [P] [backend] Refactor, XML docs, HO:polish with Domain/Persistence/Api coverage
- [ ] T075 [P] [frontend] Accessibility (PDF preview has a download alternative and a title; warnings as text), help texts final, HO:polish
- [ ] T076 [P] [qa] Traceability matrix (US1..US5, FR-001..FR-014 -> tests), HO:polish
- [ ] T077 [P] [devops] Validate compose/CI, image size delta recorded (CPU and GPU variants), HO:polish
- [ ] T078 [P] [quality-gate] `run_all.py` on the whole repo; refactor requests per owner; HO:polish

---

## Integration wave (CTO, after all wave hand-offs are accepted)

- [ ] T079 [CTO] Merge the owner branches; full build, lint, unit + coverage gates green; Playwright default suite incl. `sheet-music.spec.ts` on the fake stack (retries 0); `pytest -m integration worker/tests/transcription` once (T040, T051)
- [ ] T080 [CTO] Reference-machine validation quickstart M2-M10 with "Hero of the Day" (bass, vocals.lead, drums) and one song with a real piano part (GPU and CPU); record times vs SC-001 and the estimates, update `StemStudio:ScoreEstimates` defaults if they are off by more than 50 %, and do the SC-006 first-time-user check; record in `docs/STATUS.md`
- [ ] T081 [CTO] `docs/THIRD-PARTY-LICENSES.md`: music21, verovio (LGPL-3.0-only, unmodified), cairosvg (LGPL-3.0-or-later, unmodified), cairocffi, cssselect2, tinycss2, webencodings, defusedxml, pypdf, torchlibrosa, music21's dependencies, vendored basic-pitch subset (Apache-2.0 + NOTICE) and piano_transcription_inference subset (MIT), weights basic-pitch ICASSP 2022 (Apache-2.0, SHA-256) and piano CRNN (CC BY 4.0, Kong et al., Zenodo 4034264, SHA-256), Leipzig font (OFL-1.1); README: "Sheet music" section (profiles, limits, drums need detail stems, CPU piano time)
- [ ] T082 [CTO] `/speckit-analyze` consistency pass; update `docs/STATUS.md`; PR

---

## Execution plan per owner (parallel wave)

All owners start after Phase 0 from the Phase 0 commit, each in its own worktree. Order inside a
sequence is binding.

| Owner | Ordered task sequence | Owned paths (write) | May mock / fake |
|---|---|---|---|
| beat-analysis | T012 -> T013 -> T014 -> T015 -> T016 -> T017 -> T018 -> T019 -> HO:foundational -> T057 -> T058 -> HO:us4 -> T071 (HO:polish) | `worker/src/stemstudio_worker/notation/**`, `analysis/drum_onsets.py`, `worker/tests/{notation,analysis}/**` | synthetic notes, generated clicks |
| audio-ingest | T020 -> T021 -> HO:foundational -> T072 (HO:polish) | `worker/src/stemstudio_worker/engraving/**`, `worker/tests/engraving/**` | MusicXML fixtures, patched fontconfig check |
| stem-separation | T022 -> T023 -> T024 -> T025 -> T026 -> T027 -> T028 -> T029 -> HO:foundational -> T038 -> T039 -> T040 -> HO:us1 -> T051 -> HO:us3 -> T059 -> T060 -> HO:us4 -> T073 (HO:polish) | `worker/src/stemstudio_worker/{transcription,jobs,api}/**`, `main.py`, `worker/tests/{transcription,jobs,api}/**` | fake ONNX session, fake torch, fake `DeviceRunner`, fake notator/engraver, fake clock |
| backend | T030 -> T031 -> T032 -> T033 -> T034 -> T035 -> T036 -> T037 -> HO:foundational -> T041 -> T042 -> HO:us1 -> T046 -> T047 -> HO:us2 -> T052 -> T053 -> HO:us3 -> T061 -> T062 -> HO:us4 -> T066 -> T067 -> HO:us5 -> T074 (HO:polish) | `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | stub worker handler with 008 examples, fake device client, SQLite |
| frontend | T043 -> T044 -> HO:us1 -> T048 -> T049 -> HO:us2 -> T054 -> T055 -> HO:us3 -> T063 -> T064 -> HO:us4 -> T068 -> T069 -> HO:us5 -> T075 (HO:polish) | `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | fake `IStemStudioApiClient`, fake 004 level context, bUnit |
| qa | T011 -> T045 -> HO:us1 -> T050 -> HO:us2 -> T056 -> HO:us3 -> T065 -> HO:us4 -> T070 -> HO:us5 -> T076 (HO:polish) | `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | suite executed in T079 |
| devops | T009 -> T010 -> HO:foundational -> T077 (HO:polish) | `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | skeleton |
| quality-gate | T007 -> T008 -> HO:foundational -> T078 (HO:polish) | `tools/quality/**`, `docs/quality/**` | sample files |
| architect | T001 -> T002 -> T003 -> T004 -> T005 -> T006 (before the wave; later contract fixes on request) | `specs/**`, ADRs, skeleton files, `worker/scripts/install_music_font.py` | n/a |
| CTO | T079 -> T080 -> T081 -> T082 | `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, merges | n/a |

Task count per owner: architect 6, beat-analysis 11, audio-ingest 3, stem-separation 15,
backend 19, frontend 11, qa 7, devops 3, quality-gate 3, CTO 4 (total 82: T001-T082).

### Flagged cross-owner tasks (converted)

| Task | Would have needed | Converted to |
|---|---|---|
| T002 font script used by the Dockerfile | devops writing worker code | architect writes the script (skeleton), devops calls it (T010) |
| T003 backend contract staging | backend test files | staged by the architect; un-staged by the backend per story |
| T040/T051 accuracy below target | tuning in two owners' code | reported in the hand-off as a tuning request (constants in `notation/profiles.py` and `transcription/basic_pitch.py`) |
| T042 job-kind switch in the 007 `AssetSyncService` | 007 code | same owner (backend), additive change |
| T081 licences | owner hand-offs | consolidated by the CTO (AGENT-PROTOCOL rule 9) |

## Dependencies & Execution Order

Phase 0 (after 007's Phase 0, because it extends `Asset`, the lanes and the library mount) ->
wave -> integration wave. 008's wave may run in parallel with 007's wave only if 007's Phase 0 is
merged; the CTO merges 007 first. Inside the wave every owner follows its sequence; stories are
independent at the contract level (fakes).

## Definition of Done checklist (mirrors the constitution)

- [ ] Red-green-refactor followed; failing test commit before implementation commit (I)
- [ ] All tests green; >= 90 % line AND branch per component (vendor excluded), exact numbers in the hand-off; integration accuracy tests pass on the reference machine (I, SC-002)
- [ ] SIG limits respected; `tools/quality/run_all.py` passes incl. the new dependency rules (II)
- [ ] Implementations match the 008 worker/backend/ui-test-id documents; contract tests pass; engine unchanged (III)
- [ ] Only OSI code; LGPL libraries unmodified; no GPL import; vendored code with LICENSE/NOTICE; weights downloaded with SHA-256 and attributed (CC BY 4.0) (IV)
- [ ] Quality limits and warnings shown per profile and item; CPU piano time estimated and shown (V)
- [ ] No secrets; no shell/subprocess for downloads; inputs validated; analyzers/formatters/linters clean; public APIs documented
- [ ] Hand-off notes written; Conventional Commits with the Co-Authored-By trailer
