---

description: "Task list for feature 010 own lyrics to song (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Own Lyrics to Song (Music + Voice)

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/DELTA.md](contracts/DELTA.md),
the merged 009 artefacts, constitution 1.3.0, ADR-018, ADR-019.

**Tests**: REQUIRED (constitution I), red -> green per owner sequence, >= 90 % line AND branch per
component. No task needs a GPU except the marked generator integration test (CI-skipped) and the
CTO validation T060: SVC, RMVPE and the song engine are faked (`FakeVoiceConverter`,
`FakePitchTracker`, `FakeMusicEngine.song`); E2E uses the fake generator and Chromium's fake
microphone (`--use-fake-device-for-media-stream --use-file-for-fake-audio-capture=<fixture>`).

**Status**: Phase 0 **executed** (architect, 2026-11-15) together with 009's Phase 0; the CTO runs
009 and 010 in one combined wave (`docs/handoffs/architect-009-010-phase0.md`), so "009 complete
(T084) -> Phase 0" below no longer applies. SoulX-Singer packaging: path install in the singer
image (ADR-019 Phase 0 outcome); `SONG_PLANNED` re-measured (research.md S-07). T005's hand-off is
`architect-009-010-phase0.md`.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

As in 009. Owners: architect, generative, backend, frontend, qa, devops, quality-gate, CTO.
stem-separation, audio-engine, audio-ingest and beat-analysis have no tasks (the worker is used
unchanged through the 009 import flow).

## Checkpoint hand-offs (binding, ADR-008)

`docs/handoffs/<owner>-010-<checkpoint>.md` at `foundational`, every story and `polish`.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| generative | song/conversion/sample-check ports | `ports.py`, `schemas.py` 1.1.0 (T003) | fakes |
| backend | generator 1.1.0 endpoints | `generator-api.openapi.yaml` 1.1.0 + examples (T002) | stub handler |
| frontend | lyrics, songs, voices, conversions | backend contract (T002), DTOs (T004) | fake API client; bUnit JS-interop mock for the recorder |
| qa | UI | `ui-test-ids.md` (T002), fake generator, fake microphone fixture (T009) | suite executed in T059 |
| devops | image layer for SoulX-Singer | plan.md, research.md S-05 | skeleton |

---

## Phase 0: Contract code and skeleton (architect, BEFORE the wave)

- [x] T001 [architect] ADR-019 -> Accepted; confirm the consent statement `consent.v1` (EN + NL text, data-model 3.3) with the CTO (owner wording check, not blocking the plan)
- [x] T002 [architect] Full contracts in `specs/010-lyrics-to-song/contracts/`: `generator-api.openapi.yaml` 1.1.0, `backend-api.openapi.yaml` (B9+0.1), `ui-test-ids.md` (U9+0.1), `redocly.yaml`; Redocly lint clean (DELTA.md)
- [x] T003 [architect] Generator skeleton: `pyproject.toml` new extra `soulx` (conflicting with `acestep` via `[tool.uv] conflicts`: torch 2.10.0+cu128, transformers 4.41.2, tokenizers 0.19.1) with the SoulX-Singer code pin (`81aeb3ae772c70093c3de74dc23c92d983801ae4`; upstream has no packaging, so Phase 0 decides between a git dependency with a minimal build shim kept in `generator/third_party/soulx-singer-shim/` or a path install in the Dockerfile; the choice is recorded in ADR-019), `librosa`, `omegaconf`; `uv.lock`; `ports.py` (`VoiceConverter`, `PitchTracker`, `MusicEngine.song`, new `GpuNeed`s), `schemas.py` 1.1.0, `config.py` (`STEMSTUDIO_GENERATOR_KINDS`, `STEMSTUDIO_SVC_MAX_TARGET_SECONDS`, fake mode `unvoiced-sample`); re-measure the planned song profile with an explicit bf16 LM dtype and update `GpuNeed`; skeleton tests
- [x] T004 [P] [architect] .NET contract code: `Contracts/Lyrics/*`, `Contracts/Voices/*` (incl. `VoiceConsentDto`, `ConsentStatementDto`), `Generation/*` additions (`SongRequestDto`, `VoiceConversionRequestDto`, `VocalLabelDto`), `TrackOriginDto` additions, `ApiRoutes`, `ContractLimits` (lyrics 4,096, title 80, sample 10-60 s, sample upload 20 MiB, lyrics upload 64 KiB); skeleton tests; example copies; operationIds staged
- [x] T005 [architect] Verify the skeleton (build 0 warnings, all tests + coverage gates, ruff/mypy, `run_all.py`); hand-off `docs/handoffs/architect-010-plan.md` (dependencies + licences)

**Checkpoint**: skeleton green; wave may start.

---

## Phase 1: Setup

- [x] T006 [P] [quality-gate] Failing tests + rule: `Api.Voices` must not depend on `Api.Generation` (one-way `Generation -> Voices`); generator `engines/soulx_svc.py` and `engines/rmvpe.py` follow the engine layer rule
- [x] T007 [quality-gate] Implement in `tools/quality/`; update `docs/quality/SIG-REPORT.md`
- [ ] T008 [P] [devops] `docker/generator.Dockerfile`: variant `GENERATOR_VARIANT=soulx` (extra `soulx`, no ACE-Step) tagged `stemstudio/singer:local`; `docker-compose.generative.yml` adds service `singer` (GPU reservation, `gpu-lease`, `model-cache`, `singer-data`, `STEMSTUDIO_GENERATOR_KINDS=voiceConversion`) and api env `StemStudio__SingerBaseUrl`; fake compose adds a fake singer; `scripts/generative-smoke.*` adds a 10 s voice conversion; CI unchanged otherwise (fake variants have no torch)
- [x] T009 [P] [qa] Fixtures: `tests/fixtures/voice/own-voice-20s.wav` (synthetic sung vowels, generated by a committed script, no real person), `unvoiced-20s.wav` (noise), `lyrics/sample-en.txt`, `sample-nl.txt`, `sample.lrc`; TEST-STRATEGY update (fake microphone, SC-006 disk walk)

---

## Phase 2: Foundational

### generative (tests first)

- [x] T010 [P] [generative] Failing tests `generator/tests/engines/test_fake_song_svc.py`: `FakeMusicEngine.song` (length, `vocal` label, instrumental), `FakeVoiceConverter` (deterministic output length = target length), `FakePitchTracker` (voiced ratio from the fixture: tone -> high, noise -> 0); fake mode `unvoiced-sample`
- [x] T011 [generative] Implement the fakes to pass T010
- [x] T012 [P] [generative] Failing tests `generator/tests/audio/test_remix.py`, `test_normalise.py`: converted vocal 24 kHz mono -> 48 kHz stereo, length aligned to the accompaniment, sum with 0-5 stems at unity + `vocalGainDb`, peak limit -1 dBFS; sample normalisation to 48 kHz mono FLAC; PCM SHA-256 (FR-008)
- [x] T013 [generative] Implement `audio/{remix,normalise}.py` to pass T012

### backend (tests first)

- [ ] T014 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Lyrics/`: `LyricsNormaliser` (LRC timestamps stripped, CRLF, trailing spaces, blank-line collapse, 4,096 limit after normalisation), `LyricsSections.Parse/Propose` test vectors (repeated block -> chorus, numbered markers, unknown brackets kept), `LyricsDensity` warning > 4 words/s (FR-001, FR-002)
- [ ] T015 [backend] Implement `src/StemStudio.Domain/Lyrics/*` to pass T014
- [ ] T016 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Voices/`: `VoiceProfile` rules, `VoiceConsent` (statement version must be current, statement hash matches the embedded text, person name 1-80, confirmed true), display-name uniqueness; `SongCaptionBuilder` (009 builder + lead voice + delivery, guarded, <= 512) (FR-006, FR-010)
- [ ] T017 [backend] Implement `Domain/Voices/*`, embedded `consent.v1.en.txt`/`consent.v1.nl.txt`, `Domain/Generation/{SongRequest,VoiceConversionRequest,VocalLabel,SongCaptionBuilder}.cs` to pass T016
- [ ] T018 [P] [backend] Failing tests `LyricsVoicesPersistenceTests.cs`: round trips of `LyricsSheets`, `VoiceProfiles` (consent columns), `AudioHashes`, song/conversion generation columns; migration from the 009 schema; `AudioHashes` backfill for existing stems/takes is idempotent
- [ ] T019 [backend] Implement configurations, migration `AddLyricsAndVoices` and the backfill to pass T018
- [ ] T020 [P] [backend] Failing tests `GeneratorClientSongVoiceTests.cs` with 1.1.0 examples: two named clients (generator, singer) from `GeneratorBaseUrl`/`SingerBaseUrl`, routing by kind, `kinds` from `/health`, `409 kindNotServed` mapped; create song (JSON only), create voice conversion (multipart `target`, `prompt`, `accompaniment` x5), `CheckVoiceSampleAsync` + `GetCheckedSampleAsync`, `DownloadTakeVocalAsync`
- [ ] T021 [backend] Implement the client additions to pass T020

**Checkpoint**: HO:foundational for generative and backend.

---

## Phase 3: User Story 1 - Turn my lyrics into a song with an AI voice (P1) MVP

- [ ] T022 [P] [US1] [generative] Failing tests `generator/tests/engines/test_acestep_song.py` (fake `acestep` module): `task_type="text2music"`, lyrics, caption, `vocal_language`, `bpm`, `keyscale`, `timesignature`, `duration`, `thinking=True`, seeds; LM initialised with the **pt** backend (research.md S-04: vLLM does not release VRAM and refuses 180 s on 16 GB) on the first song job and released with the DiT when idle; profile chosen by free memory: planned (DiT + LM) -> direct (DiT only, `thinking=False`, BPM/key/time signature from the request or 120 BPM 4/4) -> direct low-memory (offload) -> wait; LM on the CPU is never used (> 10 min, research.md S-04); take reports `profile`; progress stage "Planning song structure" while the LM runs (FR-001, FR-012)
- [ ] T023 [US1] [generative] Implement `engines/acestep_engine.py` song path and `engines/profiles.py` song needs to pass T022
- [ ] T024 [P] [US1] [generative] Failing API tests: `kind=song` validation (lyrics 1-4,096, duration 10-480, bpm 40-220, key pattern, language list), takes carry `vocal=synthetic` or `none` (instrumental), FLAC tags `AI_VOICE`; contract validation
- [ ] T025 [US1] [generative] Implement the song kind in `api/` and `jobs/runner.py` to pass T024
- [ ] T026 [US1] [generative] `@pytest.mark.integration @pytest.mark.gpu` test: real 10 s song from two lyric lines on the GPU (skipped without CUDA)
- [ ] T027 [P] [US1] [backend] Failing tests `SongEndpointTests.cs`: `POST /api/songs` with `lyricsId` or inline text (normalised), style or free caption (guard -> `400 imitation`), lead voice, instrumental, bpm/key/duration/takes validation, snapshot stored, `202`; `409` not ready; density warning returned in the response `warnings[]`, not an error (FR-001, FR-010, FR-012)
- [ ] T028 [US1] [backend] Implement `Generation/SongService.cs` + endpoint to pass T027
- [ ] T029 [P] [US1] [backend] Failing tests: `POST /api/lyrics/sections` helper endpoint; sync maps `vocal` to `GenerationTake.Vocal`; library title rule for songs ("<lyrics title> - <style> (take n)") (FR-002, FR-011)
- [ ] T030 [US1] [backend] Implement to pass T029
- [x] T031 [P] [US1] [frontend] Failing bUnit tests `NewSongPageTests.cs`: lyrics editor (paste, upload, char counter, "Add sections"), language, style select (009 library) or free caption, lead voice, delivery, instrumental, bpm/key/duration/takes, density warning, generate, errors, personal-use + responsibility notice, help links; take list reuses 009 components with `take-vocal-badge-*` (FR-001, FR-002, FR-014, FR-016)
- [x] T032 [US1] [frontend] Implement `Pages/NewSong.razor`, `Shared/{LyricsEditor,VocalBadge}.razor`, help entries `help.lyrics.*`, `help.song.*` to pass T031
- [x] T033 [US1] [qa] Author `e2e/tests/song.spec.ts` (`@generative`, fake stack): paste sample lyrics -> add sections -> pick "Classic soul" -> generate -> take with `data-vocal="synthetic"` plays; invalid lyrics (empty, too long) refused

**Checkpoint**: HO:us1.

---

## Phase 4: User Story 2 - Keep and reuse my lyrics (P2)

- [ ] T034 [P] [US2] [backend] Failing tests `LyricsEndpointTests.cs`: CRUD, upload (`.txt`, `.lrc`, `.md`, UTF-8, <= 64 KiB; `415` other types; invalid UTF-8 `400`), search; takes keep their snapshot after edit/delete (FR-003)
- [ ] T035 [US2] [backend] Implement `Lyrics/LyricsEndpoints.cs` to pass T034
- [x] T036 [P] [US2] [frontend] Failing bUnit tests `LyricsListTests.cs`: list, edit, delete confirm, "new song from these lyrics"
- [x] T037 [US2] [frontend] Implement `Pages/LyricsList.razor` to pass T036
- [x] T038 [US2] [qa] Author "lyrics list" spec: save, edit, generate twice, both takes show their own text

**Checkpoint**: HO:us2.

---

## Phase 5: User Story 3 - Sing it with my own voice (P2)

- [ ] T039 [P] [US3] [generative] Failing tests `generator/tests/api/test_voice_samples.py`: `POST /v1/voice-samples/check` (duration 10-60 s, voiced ratio >= 0.5 via `PitchTracker` on the CPU, `ok`/reason, `checkId`, PCM SHA-256), `GET .../{checkId}/audio` once within 10 minutes, then `404`; unreadable `400`; `413` (FR-005, FR-007)
- [ ] T040 [US3] [generative] Implement `api/voice_samples.py` and `engines/rmvpe.py` (lazy import, CPU for checks, CUDA under the gate for conversions) to pass T039
- [ ] T041 [P] [US3] [generative] Failing tests `generator/tests/engines/test_soulx_svc.py` (fake `soulxsinger` modules): model built once with fp16 on CUDA, `infer(pt_wav, gt_wav, pt_f0, gt_f0, auto_shift, pitch_shift, n_steps, cfg, use_fp16)` mapping, target longer than the limit -> `invalidInput`, target voiced ratio < 0.1 -> `unvoicedTarget`, unload; voice-conversion job: gate with `SVC_FP16` need, remix, take + `/vocal` file, tags `AI_VOICE=converted`, `AI_VOICE_PROFILE`; job copies of prompt/target deleted at the end (FR-008, FR-011, FR-015)
- [ ] T042 [US3] [generative] Implement `engines/soulx_svc.py`, the `voiceConversion` kind in `api/` and `jobs/runner.py` to pass T041
- [ ] T043 [US3] [generative] `@pytest.mark.integration @pytest.mark.gpu` test: real SVC of a 10 s sung fixture with the own-voice fixture as prompt (skipped without CUDA)
- [ ] T044 [P] [US3] [backend] Failing tests `VoiceProfileEndpointTests.cs`: `generation/status.voice` (`notInstalled`/`unavailable`/`ready` from the singer health); creation `409` when the singer is not ready; consent missing/outdated -> `400` before any file is written; exact-copy guard (bytes equal to a stored stem/take/export/upload -> `422 libraryAudio`); generator check results mapped to `422 tooShort|tooLong|unvoiced`; stored sample + consent record (version, hash, name, time, sample SHA-256); `GET` list/one/sample; `GET /api/consent/voice` (FR-005, FR-006, FR-007, SC-004)
- [ ] T045 [US3] [backend] Implement `Voices/VoiceProfileEndpoints.cs`, `Audio/AudioHashIndex.cs` (+ writes from stem/take/export/upload storage) to pass T044
- [ ] T046 [P] [US3] [backend] Failing tests `VoiceConversionTests.cs`: `POST .../voice-conversions` for a completed song take with vocals (`409` otherwise); take not imported -> 009 import + `waitingForStems` until the track completes; then the vocal stem as `target`, the other five stems as `accompaniment`, the profile sample as `prompt` sent to the stub generator; result take `vocal=converted` with profile name and the vocal file stored (FR-008, FR-011)
- [ ] T047 [US3] [backend] Implement `Generation/{VoiceConversionService,ConversionStemWaiter}.cs` to pass T046
- [ ] T048 [P] [US3] [backend] Failing tests `VoiceProfileDeletionTests.cs`: delete cancels queued/running conversions with the profile (stub generator `DELETE`), removes `voices/<id>/`, optionally deletes conversion takes (files + library entries), nulls the profile id and keeps the name snapshot "(deleted)"; a walk over the API data directory finds no file derived from the sample (SC-006, FR-009)
- [ ] T049 [US3] [backend] Implement `Voices/VoiceProfileService.DeleteAsync` to pass T048
- [x] T050 [P] [US3] [frontend] Failing bUnit tests: `VoiceRecorder` (JS interop mocked: start/stop, timer, 60 s auto-stop, permission denied -> `voice-record-unavailable` with upload still possible), `ConsentPanel` (statement from `GET /api/consent/voice` in EN + NL, checkbox + person name required before `voice-save` enables), `VoiceProfiles` page (list, play, consent details, delete with "also delete converted takes" checkbox), `UseMyVoiceDialog` on song takes with vocals only (FR-005, FR-006, FR-009, FR-014)
- [x] T051 [US3] [frontend] Implement `wwwroot/js/voice-recorder.js` (MediaRecorder, WebM/Opus, typed JSDoc), `Shared/{VoiceRecorder,ConsentPanel,UseMyVoiceDialog}.razor`, `Pages/VoiceProfiles.razor`, help entries `help.voice.*` to pass T050
- [x] T052 [US3] [qa] Author `e2e/tests/voice.spec.ts` (`@generative`, fake stack, fake microphone with the own-voice fixture): record 20 s -> consent -> save; save without consent impossible; upload of a downloaded stem refused (`libraryAudio`); "Use my voice" on a song take -> waiting for stems -> converted take with `data-vocal="converted"`; delete profile with "delete takes" -> takes gone

**Checkpoint**: HO:us3.

---

## Phase 6: User Story 4 - Use the song for practice (P2)

- [ ] T053 [P] [US4] [backend] Failing tests: importing song and conversion takes sets `Track.Origin` with `vocal`, `voiceProfileName`, `lyricsId`; `TrackDto.origin` mapped (FR-013)
- [ ] T054 [US4] [backend] Implement to pass T053
- [x] T055 [P] [US4] [frontend] Failing bUnit test + implementation of `track-lyrics-link` and the vocal badge on generated tracks (combined red -> green pair inside the frontend sequence: test commit first)
- [x] T056 [US4] [qa] Author "song to practice" spec: import a song take -> fake separation -> mixer; lyrics link present

**Checkpoint**: HO:us4.

---

## Phase 7: User Story 5 - Instrumental backing (P3)

- [x] T057 [US5] [qa] Author "instrumental" spec (`@generative`): generate with `song-instrumental` -> take `data-vocal="none"`; "Use my voice" not offered. (Generator, backend and frontend support is already delivered by T022-T032; this story only adds the E2E proof.)

**Checkpoint**: HO:us5 (qa).

---

## Phase 8: Polish (inside the wave, per owner)

- [ ] T058 [P] [generative] [backend] [frontend] [qa] [devops] [quality-gate] Each owner: refactor to SIG limits, docs, accessibility (frontend: consent panel and recorder fully keyboard- and screen-reader-usable), traceability (qa), compose/CI validation (devops), `run_all.py` (quality-gate); HO:polish per owner (one task id, one hand-off per owner)

---

## Integration wave (CTO)

- [ ] T059 [CTO] Merge; full build, lint, unit + coverage gates; Playwright default + `@generative` suites on the fake stacks (incl. fake microphone), retries 0
- [ ] T060 [CTO] Real GPU validation (quickstart V3-V8): 60 s and 3 min songs (cold/warm), Dutch and English, VRAM peaks, own-voice profile from a real recording of the owner (with consent), conversion of a 60 s take, labels, deletion walk; record in `docs/STATUS.md`
- [ ] T061 [CTO] `docs/THIRD-PARTY-LICENSES.md`: SoulX-Singer code (Apache-2.0, commit), weights `model-svc.pt`, `rmvpe.pt` (Apache-2.0, revisions + SHA-256 from research.md S-01), whisper-base (Apache-2.0, revision + SHA-256), librosa (ISC), omegaconf (BSD-3); note "VoiceStudio (AGPL-3.0) not used"; README: songs, voice profiles, consent and ethics rules
- [ ] T062 [CTO] Owner listening checks (SC-003); `/speckit-analyze`; `docs/STATUS.md`; PR
- [ ] T062a [CTO] Update docs/public manuals and regenerate PDFs (constitution 1.3.1, last step of the integration wave, after `docs/STATUS.md` is updated): edit the chapters of this feature in `docs/public/src/user-manual/` and/or `docs/public/src/technical-manual/` (remove the *In development* marker, describe the delivered behaviour, setup, env vars, contracts and limits; add screenshots from the visual baselines where useful), run `cd tools/docs && npm ci && npm run browser && npm run build`, commit the sources, both PDFs and `docs/public/build-manifest.json` together; `node tools/docs/check.mjs` passes

---

## Execution plan per owner (parallel wave)

| Owner | Ordered task sequence | Owned paths (write) | May mock / fake |
|---|---|---|---|
| generative | T010 -> T011 -> T012 -> T013 -> HO:foundational -> T022 -> T023 -> T024 -> T025 -> T026 -> HO:us1 -> T039 -> T040 -> T041 -> T042 -> T043 -> HO:us3 -> T058 (HO:polish) | as 009 | fake engines, fake `acestep`/`soulxsinger` modules, fake clock |
| backend | T014 -> T015 -> T016 -> T017 -> T018 -> T019 -> T020 -> T021 -> HO:foundational -> T027 -> T028 -> T029 -> T030 -> HO:us1 -> T034 -> T035 -> HO:us2 -> T044 -> T045 -> T046 -> T047 -> T048 -> T049 -> HO:us3 -> T053 -> T054 -> HO:us4 -> T058 (HO:polish) | as 009 | stub generator/worker handlers, `FakeTimeProvider`, SQLite, temp data dir |
| frontend | T031 -> T032 -> HO:us1 -> T036 -> T037 -> HO:us2 -> T050 -> T051 -> HO:us3 -> T055 -> HO:us4 -> T058 (HO:polish) | as 009 | fake API client, bUnit JS-interop mock |
| qa | T009 -> T033 -> HO:us1 -> T038 -> HO:us2 -> T052 -> HO:us3 -> T056 -> HO:us4 -> T057 -> HO:us5 -> T058 (HO:polish) | as 009 | fake stack, fake microphone |
| devops | T008 -> HO:foundational -> T058 (HO:polish) | as 009 | fake generator image |
| quality-gate | T006 -> T007 -> HO:foundational -> T058 (HO:polish) | as 009 | sample files |
| architect | T001 -> T002 -> T003 -> T004 -> T005 | as 009 | n/a |
| CTO | T059 -> T060 -> T061 -> T062 | as 009 | n/a |

Task count per owner (T058 counted once per owner): architect 5 (T001-T005), generative 15
(T010-T013, T022-T026, T039-T043, T058), backend 23 (T014-T021, T027-T030, T034-T035,
T044-T049, T053-T054, T058), frontend 8 (T031-T032, T036-T037, T050-T051, T055, T058), qa 8
(T009, T033, T038, T052, T056, T057, T058), devops 2 (T008, T058), quality-gate 3 (T006, T007,
T058), CTO 4 (T059-T062). **62 task ids** (T001-T062); 68 owner assignments.

## Dependencies & Execution Order

009 complete (T084) -> Phase 0 -> wave -> Integration wave.

## Definition of Done checklist (mirrors the constitution)

- [ ] Red-green-refactor followed; failing test commit before implementation commit (I)
- [ ] All tests green; >= 90 % line AND branch per component incl. the generator (I)
- [ ] SVC, F0 and song engine behind ports; only the marked integration tests touch a GPU (I)
- [ ] SIG limits; `run_all.py` passes (II)
- [ ] Generator 1.1.0, backend and UI ids versions of T002 implemented; contract tests pass (III)
- [ ] SoulX-Singer and whisper-base licences recorded with SHA-256; no VoiceStudio code; consent stored and enforced; no library audio as a voice sample; labels on 100 % of takes; voice data deletion verified (IV)
- [ ] Limits of conversion quality, languages and the exact-copy guard documented in help (V)
- [ ] Help entries present and linked; no secrets; linters clean; public APIs documented
- [ ] docs/public user and/or technical manual updated for this feature and the PDFs regenerated; `node tools/docs/check.mjs` passes (constitution 1.3.1)
- [ ] Hand-off notes written; Conventional Commits with the Co-Authored-By trailer
