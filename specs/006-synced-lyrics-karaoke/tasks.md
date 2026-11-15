---

description: "Task list for feature 006 synced lyrics with karaoke ball (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Synced Lyrics with Karaoke Ball

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/DELTA.md](contracts/DELTA.md),
constitution 1.2.0, ADR-015 (and ADR-005, ADR-012, ADR-014).

**Status**: planned ahead; **Phase 0 is not executed yet**. It starts after 005 is merged; the
architect then assigns the contract versions (DELTA.md), runs the install spike and the
measurements (T001, T002) and writes the contract code.

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component in its hand-off. No unit test needs the network, a GPU or the
Whisper weights: LRCLIB uses recorded response shapes with self-written text, alignment sits
behind the `Aligner` port with a fake, the engine uses the mocked `AudioContext` and a fake
`requestAnimationFrame`. Exactly one new `@pytest.mark.integration` test runs real alignment on
the self-made clip (run by the CTO, T066). No third-party lyrics are committed.

**Organization**: grouped by user story (US1..US3); the
[execution plan](#execution-plan-per-owner-parallel-wave) regroups the IDs per agent.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on
  an incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: the agent from `.claude/agents/`; it writes only inside its owned paths (plan.md
  ownership map) plus `docs/handoffs/<owner>-006-<checkpoint>.md`.
- Paths are relative to the repository root. **CTO** = orchestrator.
- beat-analysis has no tasks in this feature.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-006-<checkpoint>.md` at `foundational` (if it has
foundational tasks), every story it takes part in (`us1`..`us3`) and `polish`, with red -> green
commit hashes per task pair, exact coverage numbers and lint/SIG output. Marker `HO:<checkpoint>`.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| stem-separation (`alignment/`, `jobs/`, `api/`) | lookup, aligner port, timed lyrics model | `ports.py` `LyricsLookup`, `Aligner`, `AlignRequest`, `WHISPER_GPU_NEED`; `schemas.py` `TimedLyrics` (T003) | fake lookup / fake aligner |
| audio-ingest (`lyrics/`) | result models, 005 egress | `schemas.py` (T003), 005 `metadata.{http,ratelimit,user_agent}` | `httpx.MockTransport` |
| audio-engine | karaoke types | `types.ts` (T005) + engine contract text (T006) | mocked `AudioContext`, fake rAF |
| backend | lyrics jobs | worker contract (T006; examples copied to `tests/StemStudio.Api.Tests/Worker/Examples/`, T004) | stub `HttpMessageHandler` |
| frontend | lyrics endpoints, `lyricsStatus`, engine karaoke methods | backend contract + DTOs (T004, T006), `types.ts` (T005) | fake `IStemStudioApiClient`, fake `IAudioEngine` |
| qa | UI ids, fake lyrics modes | `ui-test-ids.md` (T006), `STEMSTUDIO_FAKE_LYRICS` (T003) | suite authored in the wave, executed in T065 |
| devops | env vars, image rules | data-model.md 1, research.md F-04 | skeleton |

---

## Phase 0: Spike, contract code and skeleton (architect, BEFORE the wave; not executed yet)

- [ ] T001 [architect] Install spike (research P0-1) in a scratch venv/container only: `torch 2.15.0` (CPU and cu132) + `stable-ts 2.19.1` + `torchaudio 2.11.1` + `openai-whisper 20250625` with the triton override; import, `torchaudio.functional.resample`, `stable_whisper.load_model("turbo", download_root=<scratch>)`, `align(..., original_split=True)` on a 30 s clip. Decision recorded in research.md and ADR-015 D3: same worker image (expected) or separate `aligner` image (fallback, then plan.md and tasks are amended before the wave)
- [ ] T002 [architect] Measurements on the reference machine (research P0-2): GPU wall time cold/warm and NVML peak for aligning the reference song's `vocals` stem; CPU wall time; set `WHISPER_GPU_NEED` (MiB) and the CPU/GPU estimate formula (seconds per audio minute) in research.md (SC-005)
- [ ] T003 [architect] Worker contract code: `schemas.py` (version bump; `TimedLyrics`, `LyricsLine`, `LyricsWord`, `WordOrigin`, `SyncLevel`, `LyricsJobMode`, `LyricsJobState`, `CreateLyricsJobRequest`, `LyricsJob`, `LyricsResult`, `LyricsOutcome`, `Health.lyrics_alignment`); `ports.py` (`LyricsQuery`, `LyricsLookup`, `AlignRequest`, `Aligner`, `WHISPER_GPU_NEED` from T002); `config.py` (`align_model`, `align_auto`, `fake_lyrics`); skeleton tests `worker/tests/test_lyrics_contract.py` incl. the TimedLyrics invariants (data-model 2.1); worker contract tests switched to the 006 document (agreed one-time edit) (FR-002)
- [ ] T004 [P] [architect] .NET contract code (DELTA.md 2.3); skeleton tests `tests/StemStudio.Api.Tests/Contract/LyricsContractTests.cs`; test project links -> 006 contracts; worker example copies (lyrics job, results with self-written text); operationIds staged (agreed one-time edit) (FR-002)
- [ ] T005 [P] [architect] `src/audio-engine/src/types.ts`: karaoke types and `AudioEngine` methods (DELTA.md 3.1), `ENGINE_CONTRACT_VERSION` bump; skeleton test that the version and the stub methods exist (stubs throw "not implemented" until T026)
- [ ] T006 [P] [architect] Full contract documents: worker-api, backend-api (OpenAPI 3.1, Redocly lint clean), audio-engine.md (karaoke section from DELTA.md 3.2), ui-test-ids, redocly config; ADR-015 status Accepted
- [ ] T007 [P] [architect] Worker dependencies: `stable-ts==2.19.1`, `openai-whisper==20250625`, `torchaudio==2.11.1` (PyTorch CPU index), PyYAML direct; `[tool.uv] override-dependencies = ["triton ; sys_platform == 'never'"]`; `uv lock`; mypy overrides (`stable_whisper.*`, `whisper.*`, `torchaudio.*`); licences in `docs/handoffs/architect-006-plan.md`
- [ ] T008 [architect] Verify the skeleton: `dotnet build` 0 warnings, all .NET tests + coverage gates, worker `pytest --cov`/ruff/mypy, engine `npm test`/lint/tsc, `tools/quality/run_all.py` 0 errors, CPU-image guard (003) passes on a locally built CPU image; hand-off `docs/handoffs/architect-006-plan.md`

**Checkpoint**: skeleton green; wave may start.

---

## Phase 1: Setup (per owner, no story)

- [ ] T009 [P] [quality-gate] Failing tests in `tools/quality/tests/`: `lyrics` imports only `ports`, `schemas`, `config`, `metadata.{http,ratelimit,user_agent}`; `alignment` imports only `ports`, `schemas`, `config`; `jobs`/`api` do not import `lyrics`/`alignment` (only `main.py` composes); engine `transport.ts`/`mixer.ts` do not import `karaoke/`
- [ ] T010 [quality-gate] Implement the rules in `tools/quality/dependency_check.py`; re-run `run_all.py`; update `docs/quality/SIG-REPORT.md`
- [ ] T011 [P] [devops] `docker/worker.Dockerfile`: confirm the CPU stage installs exactly `uv.lock` (no triton), Whisper weights go to `STEMSTUDIO_MODEL_DIR/whisper` on the model-cache volume; compose env `STEMSTUDIO_ALIGN_MODEL`, `STEMSTUDIO_ALIGN_AUTO`, `STEMSTUDIO_FAKE_LYRICS`; extend `scripts/check-cpu-image.*` to assert `stable_whisper` and `torchaudio` import in the CPU image and that `triton` is absent; CI `CONTRACTS_DIR` and `scripts/test-all.*` -> 006
- [ ] T012 [P] [qa] `tests/fixtures/lyrics/`: self-written lyrics as LRC (incl. multi-stamp lines, `[offset:]`, empty lines, a CJK and a right-to-left line), `lyricsfile` YAML (shape of research F-03), LRCLIB JSON shapes for get/search (records of F-01/F-02 with replaced text), README with provenance; alignment clip + manual word annotation (research R-07, owner question O-2) for the integration test; `docs/testing/TEST-STRATEGY.md` traceability US1..US3

---

## Phase 2: Foundational (blocking inside each owner)

### audio-ingest (tests first)

- [ ] T013 [P] [audio-ingest] Failing tests `worker/tests/lyrics/test_lrc.py`, `test_lyricsfile.py`, `test_normalize.py`: LRC stamps `[mm:ss]`/`.xx`/`.xxx`, several stamps per line, `[offset:]` sign, metadata tags ignored, empty lines as breaks; `lyricsfile` via `safe_load`, words with start only -> ends per research R-03 (3 s cap, line end); limits (400 lines, 40 words, 500 chars, 1 MB) -> typed "unusable" error; invariants of data-model 2.1 (FR-002, FR-003)
- [ ] T014 [audio-ingest] Implement `lyrics/{lrc,lyricsfile,normalize}.py` to pass T013
- [ ] T015 [P] [audio-ingest] Failing tests `worker/tests/lyrics/test_interpolate.py`: vowel-group weights (Unicode, minimum 1), CJK per character, line span capped at `60 ms * chars + 1500 ms`, last line, words contiguous and proportional, origin `interpolated` (FR-003)
- [ ] T016 [audio-ingest] Implement `lyrics/interpolate.py` to pass T015

### stem-separation (tests first)

- [ ] T017 [P] [stem-separation] Failing tests `worker/tests/jobs/test_lyrics_jobs.py` (fake lookup, fake aligner, fake clock): `lookup` jobs on the 005 network lane, `align` jobs as scheduler steps with priority standard > align > detail parent step (a queued align runs before the next detail parent, after a queued standard job; never pre-empts); persisted in `lyrics-jobs/*.json`; restart re-runs; `DELETE` removes job + result; 24 h sweep; cancel of queued/running align (FR-014)
- [ ] T018 [stem-separation] Implement `jobs/{lyrics_store,lyrics_lane}.py` and the scheduler priority in `jobs/scheduler.py` to pass T017

### backend (tests first)

- [ ] T019 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Lyrics/TrackLyricsTests.cs`: state machine data-model 3.2 incl. removed (no automatic lookup), paste from every allowed state, auto-align rule (GPU worker and standard completed), offset limits (±5000, multiple of 10), document invariants re-validated (invalid worker data -> `failed` with a reason), credit text per source/origin (FR-002, FR-007)
- [ ] T020 [backend] Implement `Domain/Lyrics/*` to pass T019
- [ ] T021 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/LyricsPersistenceTests.cs`: JSON document round trip, cascade delete with the track, `Tracks.LyricsStatus` column, a 005-schema database loads with `none`
- [ ] T022 [backend] Implement configuration and migration `AddTrackLyrics` to pass T021
- [ ] T023 [P] [backend] Failing tests `tests/StemStudio.Api.Tests/Worker/WorkerLyricsClientTests.cs` with the worker examples: create (lookup/align), get, result (`no-store`), cancel, delete; 404/409/422 mapping; `Health.lyricsAlignment` parsed
- [ ] T024 [backend] Implement the worker client additions to pass T023

### audio-engine (tests first)

- [ ] T025 [P] [audio-engine] Failing Vitest tests `tests/frame-loop.test.ts`, `tests/karaoke/clock.test.ts`: one shared rAF loop for meters and karaoke (starts with the first subscriber, stops with the last, pauses while `document.visibilityState` is hidden; existing meter tests unchanged); karaoke clock = transport position from `getOutputTimestamp()` extrapolated with `performance.now()`, fallback `currentTime`, minus `outputLatency` when defined, minus the offset; wrapped into the loop; paused/stopped -> stored position; no accumulation (1,000 simulated frames with jittered rAF intervals give zero drift against the mock clock) (FR-006, SC-002)
- [ ] T026 [audio-engine] Implement `src/frame-loop.ts` (meters migrated onto it) and `src/karaoke/clock.ts` to pass T025

**Checkpoint**: HO:foundational for audio-ingest, stem-separation, backend and audio-engine.

---

## Phase 3: User Story 1 - Sing along with downloaded synced lyrics (P1) MVP

**Goal**: LRCLIB lookup at import, banner with the ball in the mixer, offset.
**Independent test**: quickstart L2, L4, L5, L6, L7.

- [ ] T027 [P] [US1] [audio-ingest] Failing tests `worker/tests/lyrics/test_lrclib.py`, `test_select.py`, `test_lookup.py`: request order get+album -> get -> search, at most 3 requests, >= 500 ms between them (fake clock), `Retry-After` via the 005 client, UA from 005; selection rule of data-model 2.3 over the F-01/F-02 record sets (262 s -> word-synced record; 270 s -> line-synced; 200 s -> not found; plain-only within 15 s -> `plain`); instrumental -> outcome `instrumental`; non-JSON body -> not found; lookups off -> `disabled` without requests; fake provider modes (FR-001, FR-003, FR-012)
- [ ] T028 [US1] [audio-ingest] Implement `lyrics/{lrclib,select,lookup,fake}.py` to pass T027
- [ ] T029 [P] [US1] [stem-separation] Failing tests `worker/tests/api/test_lyrics_routes.py`: create/get/result/cancel/delete per contract (202, 404, 409, 422), `Cache-Control: no-store` on results, composition with the real lookup or the fake provider (FR-001, FR-010)
- [ ] T030 [US1] [stem-separation] Implement `api/lyrics_routes.py`, `main.py` wiring to pass T029
- [ ] T031 [P] [US1] [backend] Failing tests `tests/StemStudio.Api.Tests/Lyrics/LyricsSyncTests.cs`: `none -> searching` when the standard poll reports `durationSeconds` (names from the 005 lookup fields), and on first mixer data request of an old track (FR-013); result applied (found/instrumental/notFound/disabled), worker job deleted after copy, worker 404 -> failed, unreachable keeps state (FR-001, FR-002)
- [ ] T032 [US1] [backend] Implement `Api/Lyrics/{LyricsSynchronizer,LyricsJobApplier,LyricsService}.cs` (registered in `JobSyncService`) to pass T031
- [ ] T033 [P] [US1] [backend] Failing tests `LyricsEndpointTests.cs`: `GET /api/tracks/{id}/lyrics` per status with `no-store`, `PUT .../lyrics/offset` (limits, 409 without lyrics), `TrackDto.lyricsStatus`; lyrics absent from `GET /api/tracks` and `GET /api/tracks/{id}`; a full fake run's log output contains none of the fixture words (FR-007, FR-010, SC-006)
- [ ] T034 [US1] [backend] Implement `Api/Lyrics/LyricsEndpoints.cs` and a logging guard (lyrics DTOs excluded from request/response logging) to pass T033
- [ ] T035 [P] [US1] [audio-engine] Failing tests `tests/karaoke/{cursor,layout,ball,dom,attach}.test.ts`: cursor O(1) forward and binary search after seek/loop wrap/offset change; DOM built with `textContent`, slots previous/current/next, word `data-state`, host `data-state`/`data-line-index`/`data-word-index`/`data-position-ms`; layout measured once and on resize (stubbed `getBoundingClientRect`, `ResizeObserver`), no layout reads inside the frame callback; ball parabola (lands exactly at each word start; short-word height scaling; arc to the next line's first word); countdown before the first line and in gaps > 5 s; `ended`; one frame after seek/pause/stop/loop/offset; `attachKaraoke` replaces, `detachKaraoke` removes, errors per contract; landing error 0 ms on the mock clock and <= 1 frame at 60 Hz with jittered frames (FR-005, FR-006, FR-008, SC-002, SC-004)
- [ ] T036 [US1] [audio-engine] Implement `src/karaoke/{cursor,layout,ball,dom,index}.ts` and the engine methods to pass T035
- [ ] T037 [P] [US1] [frontend] Failing bUnit tests: `KaraokeBanner` renders the empty host `karaoke-banner` with `@key` and no child content, `data-sync`, `karaoke-credit`; calls `IAudioEngine.AttachKaraokeAsync` after the track and lyrics are loaded, `DetachKaraokeAsync` on dispose; `karaoke-offset` (±5000 ms, 10 ms steps, ±50 ms keys) calls `SetKaraokeOffsetAsync` immediately and saves debounced (500 ms); `karaoke-offset-reset`; `karaoke-unavailable` texts per status; layout at the bottom of the mixer in the 004 design (banner does not cover the transport); journey levels (beginner: banner + toggle; advanced: offset, size; expert: timing origin) (FR-005, FR-007, FR-015)
- [ ] T038 [US1] [frontend] Implement `Components/Mixer/Karaoke/*`, `Audio/{IAudioEngine,AudioEngineInterop,EngineMethods}` karaoke members, `IStemStudioApiClient.{GetLyricsAsync, SetLyricsOffsetAsync}`, banner styles (004 tokens), help topics `help-karaoke`, `help-karaoke-offset` to pass T037
- [ ] T039 [US1] [qa] Author `e2e/tests/karaoke.spec.ts` (fake stack `STEMSTUDIO_FAKE_LYRICS=word|line|instrumental`): states countdown -> singing -> ended, landing accuracy against the engine clock (quickstart L4: 30 s, 50 loops, 20 seeks), frame-rate/perf trace (L5), offset changes the landing time, instrumental has no banner (SC-002, SC-004)

**Checkpoint**: HO:us1.

---

## Phase 4: User Story 2 - Lyrics without timing are aligned automatically (P2)

**Goal**: forced alignment of plain or pasted lyrics on the vocal stem.
**Independent test**: quickstart L3, L8, L9.

- [ ] T040 [P] [US2] [stem-separation] Failing tests `worker/tests/alignment/test_audio_prep.py`, `test_mapping.py`, `test_aligner.py` (fake stable-ts module, fake runner): stem choice `vocals.lead` from the detail cache, else `vocals`; missing stem / silent stem -> fixed texts; mono 16 kHz float32 via soxr; the adapter runs through `DeviceRunner` with `WHISPER_GPU_NEED`, passes the device, `download_root` = model dir, `original_split=True`, language hint; model download stage + progress; mapping of stable-ts words back to our tokens by order (mismatch -> error), `lowConfidence` below 0.2, word caps; CPU retry on GPU OOM via the runner (FR-004, FR-014)
- [ ] T041 [US2] [stem-separation] Implement `alignment/{audio_prep,mapping,stable_ts_aligner,fake}.py` (lazy imports) and the `align` job handler to pass T040
- [ ] T042 [P] [US2] [stem-separation] Failing tests: `estimatedSeconds` from the T002 formula per device; `Health.lyricsAlignment` (`modelCached` true when the weights file exists with the right size); `@pytest.mark.integration` test `worker/tests/alignment/test_align_clip.py` on the T012 clip asserting >= 80 % of word starts within 300 ms (SC-003, SC-005)
- [ ] T043 [US2] [stem-separation] Implement the estimate and health additions to pass T042 (the integration test is run in T066)
- [ ] T044 [P] [US2] [backend] Failing tests `LyricsAlignTests.cs`: plain result -> `alignPending`; auto rule starts alignment when the worker device is GPU (from `/api/device`) and the standard job is completed, respecting `STEMSTUDIO_ALIGN_AUTO` reported by the worker; `PUT .../lyrics` (paste; limits; control characters rejected) -> plain document with source user; `POST .../lyrics/align` and `/align/cancel` (409 rules); alignment progress/device/estimate mapped; completion -> `found` origin `aligned` (FR-004)
- [ ] T045 [US2] [backend] Implement in `Api/Lyrics/*` to pass T044
- [ ] T046 [P] [US2] [frontend] Failing bUnit tests: track detail `lyrics-section` per status; `lyrics-preview` static text for alignPending/failed with text; `lyrics-align` with `lyrics-align-estimate`; `lyrics-align-progress`, `lyrics-align-device` (003 device badge), `lyrics-align-cancel`; paste dialog (`lyrics-paste-*`, client-side length check); `lyrics-error`; `lyrics-copyright-notice` (FR-004, FR-010)
- [ ] T047 [US2] [frontend] Implement the track-detail lyrics section, paste dialog, API client methods, help topics `help-lyrics-sources`, `help-lyrics-align`, legal-notice sentence on lyrics to pass T046
- [ ] T048 [US2] [qa] Author `e2e/tests/lyrics.spec.ts` (fake stack `plain`, `not-found`, `failed`; fake CPU and fake GPU devices): auto alignment on fake GPU, click-to-align on fake CPU with estimate, paste flow, failure + retry, banner appears with `data-sync="aligned"`

**Checkpoint**: HO:us2.

---

## Phase 5: User Story 3 - Control the banner (P3)

**Goal**: show/hide, text size, search again, remove, reduced motion.
**Independent test**: spec US3.

- [ ] T049 [P] [US3] [backend] Failing tests: `DELETE .../lyrics` -> `removed` (document cleared, no automatic lookup on later polls or opens), `POST .../lyrics/search` from removed/notFound/failed/disabled (409 while searching/aligning) (FR-009)
- [ ] T050 [US3] [backend] Implement to pass T049
- [ ] T051 [P] [US3] [audio-engine] Failing tests: reduced motion (option and `matchMedia` change at runtime) hides the ball and highlights the current word; text-size class change triggers a re-measure (FR-011)
- [ ] T052 [US3] [audio-engine] Implement to pass T051
- [ ] T053 [P] [US3] [frontend] Failing bUnit tests: `karaoke-toggle` (`aria-pressed`, banner removed and engine detached when hidden, remembered via `localStorage` with try/catch defaults), `karaoke-size` s/m/l, `lyrics-search`, `lyrics-remove` with confirm; contrast tokens for both 004 themes (FR-009, FR-011)
- [ ] T054 [US3] [frontend] Implement to pass T053
- [ ] T055 [US3] [qa] Author `karaoke.spec.ts` "controls": toggle persists across reload, size change keeps landing accuracy, remove + search again, `reducedMotion: 'reduce'` emulation shows no ball

**Checkpoint**: HO:us3.

---

## Phase 6: Polish (inside the wave, per owner)

- [ ] T056 [P] [audio-ingest] Refactor to SIG limits, docstrings, HO:polish with coverage of `lyrics`
- [ ] T057 [P] [stem-separation] Refactor, docstrings, HO:polish with coverage of `alignment`, `jobs`, `api`
- [ ] T058 [P] [backend] Refactor, XML docs, HO:polish with Domain/Persistence/Api coverage
- [ ] T059 [P] [audio-engine] Refactor, TSDoc, `npm run test:coverage` >= 90 %, HO:polish
- [ ] T060 [P] [frontend] Accessibility (banner `aria-live="off"` with a "show full lyrics" alternative, keyboard access to controls, focus order, contrast >= 4.5:1), help topic review, HO:polish
- [ ] T061 [P] [qa] Traceability matrix update, HO:polish
- [ ] T062 [P] [devops] Validate compose/CI changes, CPU-image guard run, HO:polish
- [ ] T063 [P] [quality-gate] `run_all.py` on the whole repo; refactor requests per owner; HO:polish

---

## Integration wave (CTO, after all wave hand-offs are accepted)

- [ ] T064 [CTO] Merge the owner branches; full build, lint, unit + coverage gates green
- [ ] T065 [CTO] Playwright default suite + `karaoke`/`lyrics` specs on the fake stack (retries 0), Chromium and Firefox
- [ ] T066 [CTO] Reference-machine validation (quickstart L6-L10): reference song word sync and ball spot-check, instrumental song, GPU alignment time and device, CPU estimate, `pytest -m integration worker/tests/alignment/test_align_clip.py`; record in `docs/STATUS.md`
- [ ] T067 [CTO] `docs/THIRD-PARTY-LICENSES.md`: stable-ts 2.19.1 (MIT), openai-whisper 20250625 (MIT), torchaudio 2.11.1 (BSD-2-Clause), tiktoken 0.14.0 (MIT), more-itertools 11.2.1 (MIT), regex (Apache-2.0 / CNRI-Python), PyYAML (MIT, direct); model weights Whisper large-v3-turbo (MIT, SHA-256, source URL, first-use download); "Online data services": LRCLIB (no stated data licence; lyrics copyrighted, local display only)
- [ ] T068 [CTO] README: karaoke banner, lyrics sources, what is sent, alignment (GPU automatic, CPU on click, 1.6 GB model), copyright and personal-practice notice incl. lyrics
- [ ] T069 [CTO] `/speckit-analyze` consistency pass; update `docs/STATUS.md`; PR

---

## Execution plan per owner (parallel wave)

All owners start after Phase 0 from the Phase 0 commit, each in its own worktree. Order inside a
sequence is binding.

| Owner | Ordered task sequence | Owned paths (write) | May mock / fake |
|---|---|---|---|
| audio-ingest | T013 -> T014 -> T015 -> T016 -> HO:foundational -> T027 -> T028 -> HO:us1 -> T056 (HO:polish) | `worker/src/stemstudio_worker/lyrics/**`, `worker/tests/lyrics/**` | `httpx.MockTransport` with recorded shapes (self-written text), fake clock |
| stem-separation | T017 -> T018 -> HO:foundational -> T029 -> T030 -> HO:us1 -> T040 -> T041 -> T042 -> T043 -> HO:us2 -> T057 (HO:polish) | `worker/src/stemstudio_worker/{alignment,jobs,api}/**`, `main.py`, their tests | fake lookup, fake stable-ts module, fake `DeviceRunner`, fake clock |
| audio-engine | T025 -> T026 -> HO:foundational -> T035 -> T036 -> HO:us1 -> T051 -> T052 -> HO:us3 -> T059 (HO:polish) | `src/audio-engine/**` except `types.ts` | mocked `AudioContext` (+ `getOutputTimestamp`, `outputLatency`), fake rAF, stubbed layout |
| backend | T019 -> T020 -> T021 -> T022 -> T023 -> T024 -> HO:foundational -> T031 -> T032 -> T033 -> T034 -> HO:us1 -> T044 -> T045 -> HO:us2 -> T049 -> T050 -> HO:us3 -> T058 (HO:polish) | `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | stub `HttpMessageHandler`, `FakeTimeProvider`, SQLite |
| frontend | T037 -> T038 -> HO:us1 -> T046 -> T047 -> HO:us2 -> T053 -> T054 -> HO:us3 -> T060 (HO:polish) | `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | fake `IStemStudioApiClient`, fake `IAudioEngine`, bUnit JS interop mock |
| qa | T012 -> T039 -> HO:us1 -> T048 -> HO:us2 -> T055 -> HO:us3 -> T061 (HO:polish) | `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | fake stack (`STEMSTUDIO_FAKE_LYRICS`, `STEMSTUDIO_FAKE_DEVICE`) |
| devops | T011 -> HO:foundational -> T062 (HO:polish) | `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | locally built CPU image |
| quality-gate | T009 -> T010 -> HO:foundational -> T063 (HO:polish) | `tools/quality/**`, `docs/quality/**` | sample files |
| architect | T001 -> T002 -> T003 -> T004 -> T005 -> T006 -> T007 -> T008 (before the wave; later contract fixes on request) | `specs/**`, ADRs, skeleton files, `types.ts` | scratch venv/container for the spike |
| CTO | T064 -> T065 -> T066 -> T067 -> T068 -> T069 | `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, merges | n/a |

Task count per owner: architect 8, audio-ingest 7, stem-separation 9, audio-engine 7, backend 15,
frontend 7, qa 5, devops 2, quality-gate 3, CTO 6 (total 69: T001-T069).

### Flagged cross-owner tasks (converted)

| Task | Would have needed | Converted to |
|---|---|---|
| T003 worker contract test switch | stem-separation test files | one-time edit by the architect, recorded in the hand-off |
| T004 backend contract staging | backend test files | operationIds staged by the architect; un-staged by the backend in T033/T044/T049 |
| T005 `types.ts` | audio-engine package | architect-owned per the skeleton rule (as in 001/002) |
| T001/T002 spike on the reference machine | GPU access, worker cache | architect runs read-only against the worker cache in a scratch container (as in 003) |
| T067/T068 licences and README | CTO-owned files | consolidated by the CTO (AGENT-PROTOCOL rule 9) |

## Dependencies & Execution Order

Phase 0 (incl. the spike; a negative T001 result changes the plan before the wave) -> wave
(Setup + Foundational + stories per owner, parallel across owners) -> Integration wave. US1 is the
MVP and can ship alone (word- and line-synced lyrics cover the reference song); US2 adds the
alignment stack.

## Definition of Done checklist (mirrors the constitution)

- [ ] Red-green-refactor followed; failing test commit before implementation commit (I)
- [ ] All tests green; >= 90 % line AND branch per component (worker, API, Web, engine), exact numbers in the hand-off (I)
- [ ] LRCLIB, stable-ts/Whisper and the clock behind ports/fakes; exactly one new marked integration test (I)
- [ ] SIG limits respected; `run_all.py` passes incl. the `lyrics`/`alignment`/`karaoke` dependency rules (II)
- [ ] Implementations match the 006 worker, backend, engine and UI-id contracts; contract tests pass (III)
- [ ] New dependencies OSI-licensed and recorded; Whisper weights with SHA-256, never committed; CPU image without triton/NVIDIA wheels; lyrics in-app only, never exported/logged; credit shown; no third-party lyrics in the repo (IV)
- [ ] Ball driven by `requestAnimationFrame` on the audio clock, no timers; landing accuracy and frame rate measured; estimated/aligned timing labelled (V)
- [ ] No secrets; analyzers/formatters/linters clean; public APIs documented
- [ ] Help topics `help-karaoke`, `help-lyrics-sources`, `help-lyrics-align`, `help-karaoke-offset` present (004 framework)
- [ ] Hand-off notes written; Conventional Commits with the Co-Authored-By trailer
