# Hand-off: architect / 006 + 008 Phase 0 (spikes, contracts, contract code, skeleton)

006 T001-T008 and 008 T001-T006, executed as **one skeleton** on top of `005-007-artwork-export`
(f246ddc: 004 Phase 0 + devops/qa/quality, 005 + 007 Phase 0 and their worker waves), so 006 and
008 can be built in one combined wave, in parallel with the 004 UI wave and the remaining 005/007
backend/frontend work.

## What was built

### Contract versions (docs/ROADMAP.md, binding)

| Document | 006 (`specs/006-synced-lyrics-karaoke/contracts/`) | 008 (`specs/008-sheet-music/contracts/`) |
|---|---|---|
| worker-api | **1.5.0** = 007's 1.4.0 + lyrics jobs, `Health.lyricsAlignment` | **1.6.0** = 1.5.0 + transcription jobs, `Health.transcription`, `Health.lanes.transcribe` |
| backend-api | **1.6.0** = 007's 1.5.0 + `Track.lyricsStatus`, `/api/tracks/{id}/lyrics/**` | **1.7.0** = 1.6.0 + `/api/scores/options`, `POST /api/assets/scores`, score assets |
| audio-engine | **2.1.0** (`audio-engine.md`, `types.ts`) | unchanged (2.1.0) |
| ui-test-ids | **1.6.0** (007's 1.5.0 + karaoke/lyrics) | **1.7.0** (1.6.0 + sheet music) |
| redocly.yaml | yes | yes |

Code and test links use the **008** documents (worker `CONTRACT_VERSION = "1.6.0"`, Contracts DTOs =
backend 1.7.0, engine 2.1.0). Subset rules are tested in both stacks: 007 -> 006 changes only
`/health`/`Health` (worker) and `Track` + 6 track examples (backend); 006 -> 008 changes only
`/health`, `Health`, `LanesHealth` (worker) and the asset entries (backend)
(`worker/tests/contract/test_lyrics_contract.py`, `LyricsContractTests.NewerContracts_...`).
ADR-015 and ADR-017 are **Accepted** with a "Phase 0 outcome" section. Each `contracts/DELTA.md`
starts with "Phase 0 outcome" (all refinements); the ones wave agents must know:

- **006**: `CreateLyricsJobRequest` = `oneOf` Lookup/Align by `mode` (`schemas.LYRICS_REQUEST`);
  align has `refine` (line-synced document) for the P3 refine; instrumental results carry an empty
  document; `WorkerSettings.lyrics.{align_model, align_auto, fake_mode}`; `Aligner.align(request,
  device, progress)`; `WHISPER_GPU_NEED` 5120 MiB; `estimate_alignment_seconds`; collapse check
  constants `COLLAPSE_*` (see measurements); backend lyrics responses share the `LyricsDocument`
  response with `Cache-Control: no-store`; help topic ids `karaoke`, `karaoke-offset`,
  `lyrics-sources`, `lyrics-align` (no `help-` prefix).
- **008**: `TempoGrid` = `oneOf` BeatGrid/Manual by `kind`, beats may be negative after the shift;
  new warning `missing-drum-piece`; `TranscriptionJob.status` reuses `RenderJobStatus`;
  `WorkerSettings.transcription.{transcriber, threads}`; ports `PitchTranscriber`,
  `DrumTranscriber`, `Notator`, `ScoreEngraver` + value types; `PIANO_GPU_NEED` 1280 MiB;
  **`Provenance.scoreRecipe`** (new) and `Provenance.recipe = null` for score items
  (`ProvenanceDto.Recipe` is now nullable); `getAssetFile` keeps attachment by default for every
  role (preview uses `inline=true`); Leipzig is a TrueType font -> `Leipzig.ttf`.

### Phase 0 spikes and measurements (RTX 5070 Ti, throw-away containers, read-only volumes, `stemstudio` untouched, 13.6-14.1 GB GPU free before each run)

| Item | Result |
|---|---|
| 006 P0-1 torchaudio 2.11.1 next to torch 2.15.0 | imports (0.02 s) and runs resample/biquads in the CPU (`+cpu`) and GPU (`+cu132`, cu132 index only) image; stable-ts/whisper import; triton never imported -> **same worker image, no aligner image** (fallback not needed) |
| Whisper turbo weights | 1,617,941,637 B, SHA-256 `aff26ae4...a19a` verified; first load 136.4 s incl. download; cached load CPU 12-13.5 s, GPU 17-19 s |
| 006 P0-2 GPU align, 4:22 song | `vocals.lead` 6.7 s cold / 5.0 s warm; `vocals` 4.8 / 2.7 s; NVML peak **4,118 MiB** -> `WHISPER_GPU_NEED` **5120 MiB** |
| 006 P0-2 CPU align (8 threads) | 207.8 s for the whole song (0.79 s per audio s); 60 s clip 76.2 s |
| Alignment quality | `vocals.lead`: median line-start error 415 ms, 29/48 lines within 500 ms; plain `vocals`: **collapsed** (median 83.6 s); no stable-ts option fixes it (vad 43.7 s, only_voice_freq 52 s, fast_mode 104 s); words < 20 ms: good 9-16 %, collapsed 69-98 % -> collapse check + one `vad=True` retry + fail text (research.md P0-2, ADR-015 outcome) |
| 008 piano CRNN on GPU, 60 s | load 4.0 s, 4.46 s cold / 4.47 s warm (0.075 s per audio s), NVML peak **952 MiB** -> `PIANO_GPU_NEED` **1280 MiB**; checkpoint SHA-256 verified; vendoring must also drop **mido** |
| 008 basic-pitch v0.4.0 URL | verified (230,444 B, SHA-256 `2c3c1d14...`), redirects to raw.githubusercontent.com |
| 008 matplotlib | music21 10.5.2 works without it (key analysis, MusicXML, MIDI) -> **excluded by uv override** |
| 008 Leipzig font | verovio 6.4.0 and 6.4.1 ship the identical `Leipzig.css` (one WOFF2 face); fontTools + brotli -> 119,416 B TrueType, SHA-256 `9606f129...ef08`, deterministic; no OFL text in the wheel (CTO records the upstream attribution) |

### Worker contract code (006 T003/T007, 008 T001/T002)

- `schemas.py` (1.6.0): lyrics (`TimedLyrics` with invariant validator, `LyricsLine/Word`,
  `Lookup/AlignLyricsRequest`, `LYRICS_REQUEST`, `LyricsJob(State)`, `LyricsResult`,
  `LyricsAlignmentHealth`, fixed texts and stages) and transcription (`TranscriptionJobRequest`,
  `DrumInputs`, `BeatGridTempo`/`ManualTempo`/`TempoGrid`, `ScoreTitles`, `TranscriptionFile`,
  `KeyInfo`, `ScoreWarning`, `ScoreSummary`, `TranscriptionResult`, `TranscriptionJob`,
  `TranscriptionHealth`, `SCORE_FILES`, `SCORE_WARNING_TEXTS`, `PIANO_ATTRIBUTION`, ...);
  `Health.lyrics_alignment`, `Health.transcription`, `LanesHealth.transcribe` with defaults.
- `ports.py`: `WeightsFile`; 006 `LyricsQuery`, `LyricsLookup`, `AlignRequest`, `Aligner`,
  errors, `WHISPER_GPU_NEED`, `WHISPER_TURBO_WEIGHTS`, `COLLAPSE_*`, `estimate_alignment_seconds`;
  008 `NoteEvent`, `DrumHit`, `Transcription`, `PitchTranscriber`, `DrumTranscriber`,
  `NotationSettings`, `ScoreDocument`, `Notator`, `EngravedScore`, `ScoreEngraver`, errors,
  `PIANO_GPU_NEED`, `BASIC_PITCH_WEIGHTS`, `PIANO_WEIGHTS`.
- `config.py`: `LyricsSettings` (`STEMSTUDIO_ALIGN_MODEL`, `_ALIGN_AUTO`, `_FAKE_LYRICS`),
  `TranscriptionSettings` (`STEMSTUDIO_TRANSCRIBER`, `_TRANSCRIBE_THREADS`).
- `worker/scripts/install_music_font.py` + `worker/tests/scripts/test_install_music_font.py`.
- Skeleton tests in a new package `worker/tests/contract/` (`docs.py` helper,
  `test_lyrics_contract.py`, `test_transcription_contract.py`).

### .NET (006 T004, 008 T003)

- `Contracts/Lyrics/*` (11 files), `Contracts/Scores/*` (17 files), `TrackDto.LyricsStatus`,
  `AssetDto.Score` (`ScoreFactsDto?`), `ProvenanceDto.{Recipe?, ScoreRecipe}`,
  `AssetFileRoleDto.{Pdf, MusicXml, Midi}`, `AssetOriginDto.Transcription`, `ApiRoutes` (lyrics,
  scores, score file roles), `ContractLimits` (lyrics, scores, `KeyNames`).
- Tests: `Contract/LyricsContractTests.cs`, `Contract/ScoreContractTests.cs`, shared helpers in
  `ContractJson.cs` (`Backend006/007`, `Sections`, `ChangedEntries`, `RoundTrip`, `WithoutTimes`);
  25 new worker example copies in `Worker/Examples/` (lyrics jobs/results, transcription
  requests/jobs, three problems).

### Engine (006 T005)

`types.ts` 2.1.0: `KaraokeWord/Line/Lyrics/Options/State`, `AudioEngine.attachKaraoke`,
`setKaraokeOffset`, `detachKaraoke`; `index.ts` `ENGINE_CONTRACT_VERSION = '2.1.0'`; `engine.ts`
stubs that throw `KARAOKE_NOT_IMPLEMENTED` (after the disposed check); `tests/karaoke-contract.test.ts`.

### Agreed one-time edits outside the lane (mechanical consequences of the version bumps)

- Worker: `tests/api/contract.py`, `tests/test_detail_contract.py`, `tests/test_device_contract.py`
  (document -> 008, version 1.6.0); `tests/api/test_detail_routes.py` (1.6.0);
  `tests/api/test_jobs_api.py`, `tests/test_smoke.py` (`/health` literals with the new defaults);
  `tests/api/test_render_routes.py` (compare `lanes.render` only); `tests/test_render_contract.py`
  (architect skeleton: 007 document stays 1.4.0, health checks reduced to the 007 members).
- Backend tests: `BackendContractTests.cs` (+9 operation ids), `BackendContractTests.StemsAndMixes.cs`
  (`PendingLyricsOperations` 7 ids - backend 006 T033/T044/T049; `PendingScoreOperations` 2 ids -
  backend 008 T041), `Worker/WorkerDeviceClientTests.cs` (health version 1.6.0),
  `ArtworkContractTests`/`AssetContractTests` (compare against the linked 007 document),
  csproj links (008 worker/backend, 006 and 007 backend documents).
- Web: **one constant** `src/StemStudio.Web/Audio/EngineMethods.cs` `ContractVersion = "2.1.0"`
  (the drift test compares it with `types.ts`). No page or component was touched.
- Engine: `tests/engine.test.ts` version literal 2.1.0.

## Files changed

- `specs/006-synced-lyrics-karaoke/{contracts/(worker-api.openapi.yaml, backend-api.openapi.yaml, audio-engine.md, ui-test-ids.md, redocly.yaml) new, contracts/DELTA.md, plan.md, research.md, tasks.md}`
- `specs/008-sheet-music/{contracts/(worker-api.openapi.yaml, backend-api.openapi.yaml, ui-test-ids.md, redocly.yaml) new, contracts/DELTA.md, plan.md, research.md, data-model.md, tasks.md}`
- `docs/decisions/ADR-015-synced-lyrics-karaoke.md`, `docs/decisions/ADR-017-sheet-music-transcription-and-engraving.md`
- `worker/{pyproject.toml, uv.lock, src/stemstudio_worker/(schemas, ports, config).py, scripts/install_music_font.py, tests/contract/**, tests/scripts/**}` + the one-time edits above
- `src/StemStudio.Contracts/**`, `tests/StemStudio.Api.Tests/**`, `src/StemStudio.Web/Audio/EngineMethods.cs`
- `src/audio-engine/src/{types,index,engine}.ts`, `src/audio-engine/tests/{engine,karaoke-contract}.test.ts`
- `docs/handoffs/architect-006-008-phase0.md`

## Tests and coverage

`scripts/test-all.ps1 -ContinueOnError`: **all 34 steps passed** (exit 0). Coverage:

```text
StemStudio.Domain.Tests       261 passed   Domain        line 98.54 %  branch 97.67 %
StemStudio.Persistence.Tests   48 passed   Persistence   line 98.56 %  branch 100 %
StemStudio.Web.Tests          332 passed   Web           line 99.88 %  branch 95.12 %
StemStudio.Api.Tests          535 passed   Api           line 99.63 %  branch 98.02 %
                                           Contracts     line 100 %    branch 100 %
worker pytest                 1736 passed, 7 deselected; 6668 stmts, 754 branches, 100 %
engine vitest                 207 passed, 100 %;  web-interop 11 passed
quality tools pytest          314 passed, 100 %; fixtures 100 %
e2e lint, typecheck, playwright --list (355 tests in 29 files)   passed
quality gate run_all          784 files, 0 errors, 163 warnings (none in a Phase 0 file)
```

Individual runs during the work:

```text
dotnet build StemStudio.slnx -c Release                          0 Warning(s), 0 Error(s)
dotnet format --verify-no-changes                                passed
StemStudio.Api.Tests                                             535 passed (was 473)
worker pytest --cov --cov-branch                                 1736 passed, 7 deselected; TOTAL 6662 stmts / 754 branches, 100 %
  schemas.py 714/714, ports.py 277+/277+, config.py 108/108      100 % line and branch
worker ruff check / format --check / mypy --strict (236 files)   clean
worker/scripts/install_music_font.py (21 tests)                  100 % line and branch (measured separately; not in the package)
engine lint / typecheck / test:coverage                          207 passed, 100 % statements/branches/functions/lines
Redocly 2.78.0 lint 006 and 008 documents                        valid, 6 warnings each (known 4xx warnings + idempotent DELETEs)
tools/quality/run_all.py                                         0 errors (164 warnings; none in a Phase 0 file)
```

## Quality checks

- SIG: every new unit <= 15 LOC, CC <= 5, <= 4 parameters (dataclass constructors excepted), no
  new clone (contract test helpers shared in `worker/tests/contract/docs.py` and `ContractJson.cs`).
- Public API documented (docstrings, XML docs; TreatWarningsAsErrors passes; TSDoc on the engine types).

## Dependencies added (worker, all >= 2 weeks old on 2026-11-15, PyPI `upload_time`)

| Name | Version (published) | Licence | URL | Note |
|---|---|---|---|---|
| stable-ts | 2.19.1 (2025-08-16) | MIT | https://pypi.org/project/stable-ts/ | 006 |
| openai-whisper | 20250625 (2025-06-26) | MIT | https://pypi.org/project/openai-whisper/ | 006; triton excluded by override |
| torchaudio | 2.11.1+cpu (2026-10-28) | BSD-2-Clause | https://download.pytorch.org/whl/cpu | 006; GPU image: `+cu132` (devops) |
| tiktoken | 0.14.0 (2026-08-17) | MIT | https://pypi.org/project/tiktoken/ | 006 |
| more-itertools | 11.2.1 (2026-10-14) | MIT | https://pypi.org/project/more-itertools/ | 006/008 |
| regex | 2026.9.29 (constraint; 2026.11.x too new) | Apache-2.0 AND CNRI-Python | https://pypi.org/project/regex/ | via tiktoken |
| PyYAML | 6.0.3 (2025-09-25) | MIT | https://pypi.org/project/PyYAML/ | dev -> runtime |
| music21 | 10.5.2 (2026-10-28) | BSD-3-Clause | https://pypi.org/project/music21/ | 008; matplotlib excluded by override |
| chardet / jsonpickle / webcolors | 7.7.0 / 4.1.4 / 25.10.0 | 0BSD / BSD-3 / BSD-3 | PyPI | music21 deps |
| verovio | 6.4.0 (2026-10-30) | LGPL-3.0-only (unmodified) | https://pypi.org/project/verovio/ | 008; 6.4.1 too new |
| cairosvg | 2.9.1 (2026-09-07) | LGPL-3.0-or-later (unmodified) | https://pypi.org/project/CairoSVG/ | 008 |
| cairocffi / cssselect2 / tinycss2 / webencodings / defusedxml | 1.7.1 / 0.10.1 / 1.5.1 / 0.6.1 / 0.7.1 | BSD-3 / BSD / BSD / BSD / PSF-2.0 | PyPI | cairosvg deps |
| pypdf | 6.21.4 (2026-10-28) | BSD-3-Clause | https://pypi.org/project/pypdf/ | 008; 7.x too new |
| torchlibrosa | 0.1.0 (2023-02-21) | MIT | https://pypi.org/project/torchlibrosa/ | 008 |

Weights (first-use downloads, never committed): Whisper large-v3-turbo (MIT), basic-pitch ICASSP
2022 ONNX (Apache-2.0), piano CRNN (CC BY 4.0). Font: Leipzig (OFL-1.1, from the Verovio wheel at
build time; fontTools[woff] MIT + brotli MIT in the build stage only). No NuGet or npm change.

## Combined 006 + 008 wave (execution plan)

Start every agent from the Phase 0 commit. Task ids are written `006:T0nn` / `008:T0nn`;
hand-offs `docs/handoffs/<owner>-006-<checkpoint>.md` / `<owner>-008-<checkpoint>.md`.

| Owner | Agents | Sequence | Why |
|---|---|---|---|
| audio-ingest | **2 in parallel** (safe) | A (006): 006:T013 -> T014 -> T015 -> T016 -> HO:foundational -> T027 -> T028 -> HO:us1 -> T056 (HO:polish). B (008): 008:T020 -> T021 -> HO:foundational -> T072 (HO:polish) | disjoint `lyrics/**` vs `engraving/**`; both import only ports/schemas/config (+ 005 `metadata.http` for A). B: engraving tests need libcairo; on Windows patch `cairosvg` or run in the Linux CI/container |
| beat-analysis | 1 (008 only) | 008:T012 -> T013 -> T014 -> T015 -> T016 -> T017 -> T018 -> T019 -> HO:foundational -> T057 -> T058 -> HO:us4 -> T071 (HO:polish) | `notation/**`, `analysis/drum_onsets.py` |
| stem-separation | **2, partly parallel** | A (006): 006:T017 -> T018 -> HO:foundational -> T029 -> T030 -> HO:us1 -> T040 -> T041 -> T042 -> T043 -> HO:us2 -> T057 (HO:polish). B (008): 008:T022 -> T023 -> T024 -> T025 -> T026 -> T027 -> T028 -> T029 -> HO:foundational **in parallel with A** (new files only: `transcription/**`, `jobs/transcription_store.py` + tests); then **wait for A's HO:us1** and continue from it: T038 -> T039 -> T040 -> HO:us1 -> T051 -> HO:us3 -> T059 -> T060 -> HO:us4 -> T073 (HO:polish) | both later edit `main.py`, `api/routes.py` (`/health`), `jobs/lanes.py`/`scheduler.py` and the runtime; A adds the align priority to the scheduler, B adds the transcribe lane on the 007 `LaneScheduler` |
| backend | 1, sequential (+1 optional for pure domain) | 006:T019 -> T020 -> T021 -> T022 -> T023 -> T024 -> HO:foundational -> T031 -> T032 -> T033 -> T034 -> HO:us1 -> T044 -> T045 -> HO:us2 -> T049 -> T050 -> HO:us3 -> T058 (HO:polish) -> 008:T030 -> ... -> T074 (HO:polish). Optional second agent: 008:T030 -> T031 -> T032 -> T033 (`Domain/Scores/**` only) in parallel | two migrations (`AddTrackLyrics`, `AddScoreFacts`) share the DbContext/snapshot; both extend `IWorkerClient`, `JobSyncService`/`AssetSyncService`, the composition root and `ApiFactory`. **Persistence/Api tasks (006:T021+, 008:T034+) start only after the 005/007 backend branch is merged** (this branch has no 005/007 backend code yet: `AddArtworkAndArtists`, `AddAssets`, `AssetSyncService` are prerequisites); 006:T019/T020 and 008:T030-T033 (Domain only) may start now |
| audio-engine | 1 (006 only) | 006:T025 -> T026 -> HO:foundational -> T035 -> T036 -> HO:us1 -> T051 -> T052 -> HO:us3 -> T059 (HO:polish) | independent of 004; may start now (replaces the stubs in `engine.ts`) |
| frontend | 1, sequential, **after the 004 merge** (008 also after the 007 frontend merge) | 006:T037 -> T038 -> HO:us1 -> T046 -> T047 -> HO:us2 -> T053 -> T054 -> HO:us3 -> T060 (HO:polish) -> 008:T043 -> T044 -> HO:us1 -> T048 -> T049 -> HO:us2 -> T054 -> T055 -> HO:us3 -> T063 -> T064 -> HO:us4 -> T068 -> T069 -> HO:us5 -> T075 (HO:polish) | both edit `IStemStudioApiClient` + fake, the track detail page, the help topics and levels; 008 builds on the 007 library page |
| qa | 1, sequential | 006:T012 -> T039 -> HO:us1 -> T048 -> HO:us2 -> T055 -> HO:us3 -> T061 (HO:polish) -> 008:T011 -> T045 -> HO:us1 -> T050 -> HO:us2 -> T056 -> HO:us3 -> T065 -> HO:us4 -> T070 -> HO:us5 -> T076 (HO:polish) | shared `TEST-STRATEGY.md`, e2e support/page objects. New specs/page objects/fixtures (006:T012 incl. the CC0 alignment clip annotated on a **lead-vocal** recording) may be written before the 004 merge; edits to existing page objects wait for it |
| devops | 1, sequential | 006:T011 -> 008:T009 -> T010 -> HO:foundational -> 006:T062 + 008:T077 (HO:polish) | both edit `docker/worker.Dockerfile` (GPU torchaudio `+cu132`, Leipzig build stage with `fontTools[woff]`, `Leipzig.ttf`), compose env, CI. Point `CONTRACTS_DIR` and `scripts/test-all.*` at **`specs/008-sheet-music/contracts`** once (covers 006); keep the render-vectors check on `specs/007-export-library/contracts/generate_render_vectors.py` (008 has no vectors file) |
| quality-gate | 1, sequential | 006:T009 -> T010 -> 008:T007 -> T008 -> HO:foundational -> 006:T063 + 008:T078 (HO:polish) | both extend `dependency_check.py` and its tests; add the `transcription/vendor/**` exclusion |
| CTO | - | merge order: 004 -> 005/007 backend+frontend -> this Phase 0 -> 006/008 owner branches; 006:T064 + 008:T079 (merge, gates) -> 006:T065 (karaoke/lyrics e2e) -> 006:T066 + 008:T080 (reference machine) -> 006:T067 + 008:T081 (licences) -> 006:T068 (README) -> 006:T069 + 008:T082 (analyze, STATUS, PR) | |

**Frontend tasks that must wait for the 004 merge**: 006:T037, T038, T046, T047, T053, T054, T060;
008:T043, T044, T048, T049, T054, T055, T063, T064, T068, T069, T075 (mixer layout, track detail,
library, dialogs, help/levels are being rebuilt by 004; the 008 ones also need the 007 library
page). qa may author new spec files (`karaoke.spec.ts`, `lyrics.spec.ts`, `sheet-music.spec.ts`)
and fixtures earlier.

## Open issues / requests

- **devops**: GPU image needs `torchaudio==2.11.1+cu132` from `https://download.pytorch.org/whl/cu132`
  (the CPU lock has `+cpu`); CPU-image guard: assert no `triton`/`matplotlib`; font stage per
  008:T010 with `fontTools[woff]` and `Leipzig.ttf`; `CONTRACTS_DIR` -> 008 (see table).
- **stem-separation**: alignment collapse rule (`ports.COLLAPSE_*`, research.md P0-2) is binding
  for 006:T040/T041; report the real `/health.lyricsAlignment` and `transcription` states; the
  health literals in `test_jobs_api.py`/`test_smoke.py` then change again (owner edit).
- **backend**: un-stage `PendingLyricsOperations`/`PendingScoreOperations` per story;
  `ProvenanceDto.Recipe` is nullable now (null-check in 007 readers when merging the 007 branch);
  backend 007 code is not on this branch - rebase 006/008 persistence work on the 005/007 backend
  merge.
- **audio-ingest (008)**: `cairosvg` needs libcairo at import (absent on Windows dev machines);
  import it lazily and fake it in unit tests.
- **CTO**: THIRD-PARTY-LICENSES (table above), OFL attribution for Leipzig from upstream
  (no licence text in the wheel); README: alignment of unsplit vocals is unreliable (split vocals
  first); constitution header in plan/tasks still cites 1.2.0 (1.3.0 does not affect 006/008).
- Owner question O-2 (006 alignment clip) remains decided by CTO-DECISIONS (qa picks CC0 material);
  use an isolated lead vocal, not a full vocal stem.

## Next owner

The combined wave above (start from the Phase 0 commit); CTO for the integration wave.
