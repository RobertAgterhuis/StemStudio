---

description: "Task list for feature 009 style transfer (Phase 0 by the architect and CTO, one parallel wave of owner agents incl. the new generative role, then a CTO integration wave)"
---

# Tasks: Style Transfer with a Style Library

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/DELTA.md](contracts/DELTA.md),
constitution (1.3.0 after T001), ADR-009, ADR-011, ADR-012, ADR-018.

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component. **No task needs a GPU** except the marked generator integration
test (skipped in CI) and the CTO validation T082: the generator is tested with `FakeMusicEngine`,
fake NVML, fake lease and fake clock; .NET with a stub generator `HttpMessageHandler` using the
contract examples; E2E with the fake generator (`STEMSTUDIO_GENERATOR=fake`).

**Status**: Phase 0 **executed** (architect, 2026-11-15) together with 010's Phase 0 in one skeleton;
see `docs/handoffs/architect-009-010-phase0.md` (combined 009 + 010 wave plan, which replaces the
"010 starts only after T084" rule below: both features run in one wave on the shared skeleton).
T001: constitution 1.3.0 by the CTO; ADR-018 Accepted and `.claude/agents/generative.md` added by
the architect on the CTO's instruction. T006's hand-off is `architect-009-010-phase0.md`.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase. Tasks of different owners are always
  parallel in the wave.
- **[owner]**: the agent from `.claude/agents/` (`generative` is new: plan.md draft, added by the CTO
  in T001); it writes only inside its owned paths plus `docs/handoffs/<owner>-009-<checkpoint>.md`.
- audio-engine, audio-ingest and beat-analysis have no tasks in this feature.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-009-<checkpoint>.md` at `foundational`, every story it takes
part in and `polish`, with red -> green commit hashes per task pair, exact coverage numbers and
lint/SIG output. Marker `HO:<checkpoint>` in the execution plan.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| generative | job, engine, gate ports | `generator/src/stemstudio_generator/{ports,schemas,config}.py` (T003) | fakes in `engines/fake.py`, `devices/fake.py` |
| stem-separation | lease file settings | worker `config.py` (T004), ADR-018 D3 | tmp-path lock files, fake clock |
| backend | generator endpoints | `contracts/generator-api.openapi.yaml` 1.0.0 + examples (T002, copies in `tests/StemStudio.Api.Tests/Generator/Examples/`) | stub `HttpMessageHandler`; fake `IGeneratedAudioLibrary` |
| frontend | styles, generations, status, track origin | backend API (T002) + Contracts DTOs (T005) | fake `IStemStudioApiClient` |
| qa | UI | `contracts/ui-test-ids.md` (T002), fake generator modes (data-model 1) | suite authored in the wave, executed in T070 |
| devops | images, compose, env | plan.md, data-model 1, research.md S-02 | skeleton |

---

## Phase 0: Decisions, contract code and skeleton (CTO + architect, BEFORE the wave)

- [x] T001 [CTO] Constitution amendment 1.3.0 (MINOR) with `/speckit-constitution`: GPU-only exception for optional generative services (text in ADR-018 D9); ADR-018 status -> Accepted; add `.claude/agents/generative.md` from the plan.md draft
- [x] T002 [architect] Bind versions on top of the merged 003/004/007 contracts and write full documents in `specs/009-style-transfer/contracts/`: `generator-api.openapi.yaml` 1.0.0 (new, examples per DELTA.md 1), `backend-api.openapi.yaml` (B+0.1), `ui-test-ids.md` (U+0.1), `redocly.yaml`; Redocly lint clean (FR-001..FR-010)
- [x] T003 [architect] Generator skeleton `generator/`: `pyproject.toml` (Python 3.12; runtime: fastapi, uvicorn, pydantic, python-multipart, soundfile, numpy, nvidia-ml-py; optional extra `acestep` = `ace-step @ git+https://github.com/ace-step/ACE-Step-1.5@ca1e85fe9430179831e6bc6be790c332190a3866`; dev: pytest, pytest-cov, ruff, mypy, httpx), `uv.lock`; verify the ACE-Step lock resolves on 3.12 and whether `gradio` can be excluded (fallback 3.11, recorded in an ADR-018 note); `ports.py` (`MusicEngine`, `GpuGate`, `GpuNeed`, `JobStore`, `Clock`, `DeviceUse`), `schemas.py` (`CONTRACT_VERSION = "1.0.0"`, all generator schemas), `config.py` (data-model 1); skeleton tests `generator/tests/test_contract_skeleton.py` (schemas validate the contract examples); mypy `ignore_missing_imports` for `acestep`, `pynvml`
- [x] T004 [P] [architect] Worker `config.py`: `gpu_lease_file` (`STEMSTUDIO_GPU_LEASE_FILE`, optional) and `gpu_lease_wait_seconds` (default 900) with tests in `worker/tests/test_config.py`
- [x] T005 [P] [architect] .NET contract code: `StemStudio.Contracts/Styles/*` (DTOs, enums), `Generation/*` (`GenerationDto`, `GenerationTakeDto`, `RestyleRequestDto`, `GenerationStatusDto`, `GenerationErrorDto`), `Tracks/TrackOriginDto` + `TrackDto.Origin`, `ApiRoutes` additions, `ContractLimits` (range 10-480 s, name 60, description 300, tags 8, caption 512); skeleton contract tests; generator example copies; new operationIds staged as pending in the backend contract test (agreed one-time edit)
- [x] T006 [architect] Verify the skeleton: `dotnet build` 0 warnings, all .NET tests + coverage gates, worker and generator `pytest --cov`/ruff/mypy, `tools/quality/run_all.py` 0 errors; hand-off `docs/handoffs/architect-009-plan.md` (dependencies + licences)

**Checkpoint**: skeleton green; wave may start.

---

## Phase 1: Setup (per owner, no story)

- [x] T007 [P] [quality-gate] Failing tests in `tools/quality/tests/`: generator layer rule (`engines`, `devices`, `audio` import only `ports`, `schemas`, `config`; `api -> jobs -> ports`; only `main.py` composes), generator included in `sig_check` and coverage collection; .NET rule `Api.Generation` must not reference `StemStudio.Web`
- [x] T008 [quality-gate] Implement the rules in `tools/quality/`; update `docs/quality/SIG-REPORT.md`
- [ ] T009 [P] [devops] `docker/generator.Dockerfile` (python:3.12-slim, ffmpeg + libsndfile, `uv sync --frozen`; build arg `GENERATOR_VARIANT=fake|acestep`: `fake` installs without the `acestep` extra (no torch, OSI-only), `acestep` with it; non-root user; healthcheck `/health`; no host port); `docker-compose.generative.yml` (service `generator` image `stemstudio/generator:local`, `GENERATOR_VARIANT=acestep`, GPU reservation, volumes `generator-data`, `model-cache:/models`, `gpu-lease:/gpu-lease`, env per data-model 1; api env `StemStudio__GeneratorBaseUrl=http://generator:8010`); `docker-compose.generative-fake.yml` (variant `fake`, `STEMSTUDIO_GENERATOR=fake`, no GPU reservation); `docker-compose.gpu.yml`: worker mounts `gpu-lease` and sets `STEMSTUDIO_GPU_LEASE_FILE` (FR-010, FR-011)
- [ ] T010 [devops] CI: generator job (ruff, mypy, pytest + coverage gate, contract lint), fake-variant image build + the 003 CPU-image guard applied to it (no NVIDIA wheels); `scripts/generative-smoke.{ps1,sh}` (real GPU stack: torch `2.10.0+cu128`, `sm_<cc>` in arch list, `/health ready`, one 10 s fake-free restyle; quickstart Q3); `scripts/test-all.*` include the generator
- [ ] T011 [P] [qa] `docs/testing/TEST-STRATEGY.md`: generator fakes and modes, `@generative` Playwright tag, reference-machine validation, traceability US1..US6 -> test files

---

## Phase 2: Foundational (blocking inside each owner)

### generative (tests first)

- [ ] T012 [P] [generative] Failing tests `generator/tests/devices/test_probe.py`, `test_nvml.py`: probe outcomes and reason texts identical to 003 data-model 2.1 (fake torch/NVML adapters); `ready` only for kind gpu (FR-010)
- [ ] T013 [generative] Implement `devices/{probe,nvml}.py` to pass T012
- [ ] T014 [P] [generative] Failing tests `generator/tests/devices/test_gate.py` (tmp-path lock file, fake NVML, fake clock): `hold()` takes the `flock` lease (a second process/thread blocks; released on exception); NVML free >= standard need -> standard profile; else >= low-memory need -> low-memory profile; else release lease, report `waitingForGpu`, re-check every 10 s, fail `gpuBusy` "GPU busy: needs X GB, Y GB free" after the wait limit; memory fraction `(free - 256 MiB) / total` set before and reset after; `is_gpu_oom()` (FR-011, FR-012, ADR-018 D3/D4)
- [ ] T015 [generative] Implement `devices/{lease,gate,memory,oom,fake}.py` to pass T014
- [ ] T016 [P] [generative] Failing tests `generator/tests/jobs/`: JSON job store; single-thread scheduler FIFO; state machine of data-model 2.4 incl. cancel while queued/waiting/running (cancel flag polled by the progress callback), restart reconciliation (`running`/`waitingForGpu`/`downloadingModel` -> `failed interrupted`), idle hook calls `engine.unload()` when the queue is empty, 24 h retention (FR-007, FR-013, SC-006)
- [ ] T017 [generative] Implement `jobs/{store,scheduler,runner,progress,reconcile}.py` to pass T016
- [ ] T018 [P] [generative] Failing tests `generator/tests/audio/`: sum 1-6 stems at unity (length tolerance 1 frame, sample-rate mismatch -> `invalidInput`), clip detection, range cut, FLAC writer 24-bit 48 kHz with Vorbis tags `AI_GENERATED`, `GENERATOR`, `STEMSTUDIO_GENERATION`, `STEMSTUDIO_TAKE`, `COMMENT` (written through ffmpeg with an argument list), SHA-256 (FR-008)
- [ ] T019 [generative] Implement `audio/{mix,flac}.py` to pass T018
- [ ] T020 [P] [generative] Failing tests `generator/tests/engines/test_fake.py`: `FakeMusicEngine` deterministic takes (seeded noise + tone of the requested length), modes `ready`, `no-gpu`, `oom` (first attempt raises a GPU OOM, low-memory retry succeeds), `oom-twice`, `busy` (fake NVML reports too little memory), `slow` (data-model 1)
- [ ] T021 [generative] Implement `engines/fake.py` and `engines/profiles.py` (`GpuNeed` constants from research.md S-03) to pass T020

### stem-separation (tests first)

- [ ] T022 [P] [stem-separation] Failing tests `worker/tests/devices/test_file_lease.py`: with `gpu_lease_file` set, the GPU runner takes the `flock` lease after its thread lease for every GPU step; a held lock makes it wait; after `gpu_lease_wait_seconds` the step runs on the CPU with the reason "GPU busy with another StemStudio job"; without the setting the 003 behaviour is unchanged; CPU runner never touches the file (FR-011)
- [ ] T023 [stem-separation] Implement in `worker/src/stemstudio_worker/devices/{lease,runner}.py` to pass T022

### backend (tests first)

- [ ] T024 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Styles/`: `Style` rules (data-model 3.1); `StyleTextGuard` (imitation phrases EN/NL, deny-list and library-artist matches, case/diacritics-insensitive whole words, no false positive on genre words such as "rock" or "soul"); `StylePromptBuilder` caption test vectors and the strength map; `PresetCatalog` loads `presets.v1.json`: >= 24 presets, >= 8 genre families, unique keys/names, every preset passes the guard (FR-003, FR-005, SC-003)
- [ ] T025 [backend] Implement `src/StemStudio.Domain/Styles/*` + embedded resources (`presets.v1.json` with the research.md R-04 list, `artist-denylist.v1.txt`, `imitation-phrases.v1.txt`) to pass T024
- [ ] T026 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Generation/`: `Generation` state machine mirroring generator states, terminal states, retry creates a new generation with new seeds, source-track deletion keeps the title snapshot; `TrackOrigin` (FR-006, FR-007)
- [ ] T027 [backend] Implement `src/StemStudio.Domain/Generation/*`, `Tracks/TrackOrigin.cs` to pass T026
- [ ] T028 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/GenerativePersistenceTests.cs`: round trips of styles (JSON tags), generations with snapshots and takes, `Tracks.Origin`; database migrated from the previous schema loads with `Origin = null`; preset seeding upserts idempotently
- [ ] T029 [backend] Implement configurations, seeding and migration `AddGenerativeStyles` to pass T028
- [ ] T030 [P] [backend] Failing tests `tests/StemStudio.Api.Tests/Generator/GeneratorClientTests.cs` with the 1.0.0 examples: `GetHealthAsync` (ready, no GPU, unreachable within 2 s), `CreateRestyleAsync` streams 1-6 stem files as multipart with the `params` part, `GetAsync`, `CancelAsync`, `DownloadTakeAsync` (SHA-256 verified), `DeleteAsync`; problem bodies mapped to typed errors
- [ ] T031 [backend] Implement `src/StemStudio.Api/Generator/*` to pass T030

**Checkpoint**: HO:foundational for generative, stem-separation, backend.

---

## Phase 3: User Story 1 - Restyle an imported song with a preset style (P1) MVP

**Goal**: restyle a completed track with a style; take playable and in the library.
**Independent test**: quickstart Q2 (fake) and Q4/Q5 (GPU, CTO).

- [ ] T032 [P] [US1] [generative] Failing API tests `generator/tests/api/test_generations.py` (TestClient, fake engine/gate): create (multipart validation: 1-6 stems, sizes, types, range limits 10-480 s, seeds 1-2, `409 gpuRequired` when not ready), get, cancel, take audio with range requests, delete; responses validate against `generator-api.openapi.yaml`; fault injection `oom`, `oom-twice`, `busy`, restart -> defined terminal states with codes and the lease released (FR-001, FR-007, FR-012, SC-006)
- [ ] T033 [US1] [generative] Implement `api/{routes,uploads,problems}.py`, `main.py` composition to pass T032
- [ ] T034 [P] [US1] [generative] Failing tests `generator/tests/engines/test_acestep_engine.py` with a monkeypatched fake `acestep` module: parameter mapping (`task_type="cover"`, `src_audio`, caption, `audio_cover_strength`, `inference_steps`, `shift`, seeds, `batch_size` = takes, `audio_format="flac"`), LM never initialised for restyle, progress callback mapping, model download on first use with byte progress (`downloadingModel`), revision pin, low-memory profile = `offload_to_cpu` + `offload_dit_to_cpu`, `unload()` drops handlers and empties the cache (FR-013, FR-017, ADR-018 D5/D6)
- [ ] T035 [US1] [generative] Implement `engines/acestep_engine.py` (lazy import) to pass T034
- [ ] T036 [US1] [generative] One `@pytest.mark.integration @pytest.mark.gpu` test `generator/tests/integration/test_acestep_real.py`: real ACE-Step cover of a 10 s fixture (`tests/fixtures/` short mix) on the GPU produces a 10 s 48 kHz FLAC with the tags; skipped without CUDA (never in CI)
- [ ] T037 [P] [US1] [backend] Failing tests `tests/StemStudio.Api.Tests/Generation/RestyleEndpointTests.cs`: `POST /api/tracks/{id}/restyles` validation (track completed, range, stems, takes, style exists), style snapshot + builder output stored, stems uploaded via the stub generator, `202` + `Location`; `409` when not completed (FR-001, FR-002, FR-006)
- [ ] T038 [US1] [backend] Implement `Generation/GenerationEndpoints.cs` (create restyle) and `RestyleService` to pass T037
- [ ] T039 [P] [US1] [backend] Failing tests `GenerationSyncServiceTests.cs` (`FakeTimeProvider`, stub generator): polls active generations every 2 s, maps status/progress/stage/device/error, on completion downloads takes to `generations/<id>/take-<n>.flac` (48 kHz; the import path resamples in the worker as for any upload), verifies SHA-256, registers each take through a fake `IGeneratedAudioLibrary` (title rule data-model 3.6), deletes the generator copy; generator restart (`interrupted`) and unreachable generator handled (FR-007, FR-008)
- [ ] T040 [US1] [backend] Implement `Generation/{GenerationSyncService,TakeStorage}.cs` and the 007 adapter `GeneratedAudioLibraryAdapter` to pass T039
- [ ] T041 [P] [US1] [backend] Failing tests for `GET /api/generations[?kind&sourceTrackId]`, `GET/{id}`, `cancel`, `retry`, `DELETE` (files + library entries removed, imported tracks kept), take audio with range requests; served bodies validate against the backend contract
- [ ] T042 [US1] [backend] Implement the remaining generation endpoints to pass T041
- [ ] T043 [P] [US1] [frontend] Failing bUnit tests: `RestyleDialog` (style select with description, range inputs with limits, stem checkboxes, 5-step strength, takes 1-2, personal-use notice, error display), track page generation section (`generation-row-*` with status/progress/stage, cancel, retry, delete), `TakePlayer` (`<audio>` with the take URL), `AiBadge`, take details panel (style, strength, seed, source, range, model, device), help links (ui-test-ids, FR-015, FR-016)
- [ ] T044 [US1] [frontend] Implement `Shared/{RestyleDialog,TakeList,TakePlayer,AiBadge}.razor`, the track-page section, `Pages/Generations.razor`, API client methods, help entries (`help.restyle.*`, `help.generation.ai-label`, `help.generation.personal-use`, `help.generation.limits`) to pass T043
- [ ] T045 [US1] [qa] Author `e2e/tests/generative.spec.ts` "restyle a track" (`@generative`, fake stack): restyle with "Jazz trio" -> progress -> completed -> take plays (audio element `readyState >= 2`) -> AI badge -> take details; cancel flow; `oom-twice` mode shows the readable failure and retry

**Checkpoint**: HO:us1.

---

## Phase 4: User Story 2 - Browse and choose from the style library (P1)

- [ ] T046 [P] [US2] [backend] Failing tests `StyleEndpointTests.cs`: `GET /api/styles` order (presets first, then name), filters `kind`, `genreFamily`, `mood`, `instrument`, `q` (name/description/tags, case-insensitive); `GET /api/styles/{id}`; contract validation (FR-004)
- [ ] T047 [US2] [backend] Implement `Styles/StyleEndpoints.cs` (read side) to pass T046
- [ ] T048 [P] [US2] [frontend] Failing bUnit tests `StylesPageTests.cs`: list with genre-family grouping, preset marker (`data-kind`), filters and search, empty state; restyle dialog shows the picked style's description and default strength (US2-1..3)
- [ ] T049 [US2] [frontend] Implement `Pages/Styles.razor` and help entry `help.styles.library` to pass T048
- [ ] T050 [US2] [qa] Author "style library" spec (`@generative` not required: runs on the default fake stack): >= 24 presets, filter by mood narrows the list, search by name (SC-003, SC-007)

**Checkpoint**: HO:us2.

---

## Phase 5: User Story 6 - Clear behaviour without a GPU (P1)

- [ ] T051 [P] [US6] [generative] Failing tests: `/health` for ready, no-GPU (probe reasons) and fake `no-gpu`; every create returns `409 gpuRequired` with the reason when not ready (FR-010)
- [ ] T052 [US6] [generative] Implement anything missing to pass T051
- [ ] T053 [P] [US6] [backend] Failing tests `GenerationStatusTests.cs`: `GET /api/generation/status` -> `notInstalled` (no base URL, or unreachable > 2 s), `unavailable` + reason, `ready` + models + `downloadBytesNeeded`; cached 10 s (`FakeTimeProvider`); `POST .../restyles` -> `409` with `code` `notInstalled`/`gpuRequired` (FR-010)
- [ ] T054 [US6] [backend] Implement `Generation/GenerationStatusService.cs` + endpoint to pass T053
- [ ] T055 [P] [US6] [frontend] Failing bUnit tests: `GenerationStatusBanner` (`generation-status` `data-state`, reason, help link); `track-restyle` disabled with `aria-describedby` reason when not ready; style library still editable (US6-1/2)
- [ ] T056 [US6] [frontend] Implement the banner, disabled states and help entry `help.generation.gpu-required` to pass T055
- [ ] T057 [US6] [qa] Author specs: default fake stack without generator -> `data-state="notInstalled"`, restyle disabled; fake generator `no-gpu` -> `unavailable` with the reason; the existing suite passes unchanged (SC-002)

**Checkpoint**: HO:us6.

---

## Phase 6: User Story 3 - Create and edit own styles (P2)

- [ ] T058 [P] [US3] [backend] Failing tests: `POST /api/styles`, `PUT`, `DELETE`, `POST .../duplicate` ("<name> (copy)", numbered when taken); presets `403`; validation `400` with field + reason incl. guard `code: "imitation"` (library artist names from the user's tracks are included); duplicate name `409`; earlier generations keep their snapshot (US3-1..4, FR-004, FR-005)
- [ ] T059 [US3] [backend] Implement the write side of `StyleEndpoints.cs` to pass T058
- [ ] T060 [P] [US3] [frontend] Failing bUnit tests `StyleEditorTests.cs`: create/edit form with all fields, tag inputs (max 8), field errors from the API, duplicate/edit/delete actions only on user styles, delete confirm
- [ ] T061 [US3] [frontend] Implement `Pages/StyleEditor.razor` and help entry `help.styles.editor` to pass T060
- [ ] T062 [US3] [qa] Author "own style" spec: duplicate a preset, edit, restyle with it (fake), take details show the user style; an artist name is refused with the reason

**Checkpoint**: HO:us3.

---

## Phase 7: User Story 4 - Import a restyled take as a track (P2)

- [ ] T063 [P] [US4] [backend] Failing tests `TakeImportTests.cs`: `POST .../takes/{n}/import` runs the existing upload import with the take file (stub worker), sets `Track.Origin = generated`, returns `202` new / `200` existing (idempotent by SHA-256), `409` when the generation is not completed; `TrackDto.origin` mapped (FR-009)
- [ ] T064 [US4] [backend] Implement `Generation/TakeImporter.cs` to pass T063
- [ ] T065 [P] [US4] [frontend] Failing bUnit tests: `take-import-*` button and resulting navigation; `track-origin-badge` + `track-origin-source-link` on the track page and an AI badge in the track list (US4-2)
- [ ] T066 [US4] [frontend] Implement to pass T065
- [ ] T067 [US4] [qa] Author "import take" spec: import -> fake separation -> mixer opens; origin badge; second import returns the same track

**Checkpoint**: HO:us4.

---

## Phase 8: User Story 5 - Restyle only a section (P3)

- [ ] T068 [P] [US5] [generative] Failing tests: `mode=inPlace` sends the whole summed source with `task_type="repaint"`, `repainting_start/end` = range, `chunk_mask_mode="explicit"`; take length = source length; fake engine keeps samples outside the range identical (FR-014)
- [ ] T069 [US5] [generative] Implement in `engines/acestep_engine.py`, `engines/fake.py`, `jobs/runner.py` to pass T068
- [ ] T070 [P] [US5] [backend] Failing tests: `mode=inPlace` validation (range inside the track, whole track <= 480 s), stored mode, take duration = track duration (FR-014)
- [ ] T071 [US5] [backend] Implement to pass T070
- [ ] T072 [P] [US5] [frontend] Failing bUnit test: in-place option `restyle-mode-inplace` in `RestyleDialog` with its explanation and range inputs (US5-1)
- [ ] T073 [US5] [frontend] Implement to pass T072

**Checkpoint**: HO:us5 (generative, backend, frontend).

---

## Phase 9: Polish (inside the wave, per owner)

- [ ] T074 [P] [generative] Refactor to SIG limits, docstrings, HO:polish with coverage of `api`, `jobs`, `devices`, `engines`, `audio`
- [ ] T075 [P] [stem-separation] HO:polish for the lease change (coverage of `devices`)
- [ ] T076 [P] [backend] Refactor, XML docs, HO:polish
- [ ] T077 [P] [frontend] Accessibility (disabled reasons readable by screen readers, badges as text, not colour only), HO:polish
- [ ] T078 [P] [qa] Traceability matrix, HO:polish
- [ ] T079 [P] [devops] `docker compose -f docker-compose.yml -f docker-compose.gpu.yml -f docker-compose.generative.yml config -q`, actionlint, HO:polish
- [ ] T080 [P] [quality-gate] `run_all.py` on the whole repo; refactor requests; HO:polish

---

## Integration wave (CTO, after all wave hand-offs are accepted)

- [ ] T081 [CTO] Merge the owner branches; full build, lint, unit + coverage gates green; Playwright default suite + `@generative` suite on the fake stacks (all fake modes), retries 0
- [ ] T082 [CTO] Real GPU validation on the reference machine (quickstart Q3-Q8): image size, first download time, cold/warm 45 s restyle, full-song restyle, VRAM peaks, labels, import, GPU sharing with a running detail separation, free-memory wait; record in `docs/STATUS.md`; if SC-001 fails see research.md S-08
- [ ] T083 [CTO] `docs/THIRD-PARTY-LICENSES.md`: ACE-Step 1.5 code (MIT, commit) and weights (MIT, revision + SHA-256 per file, research.md S-01), the generator image's Python packages (`uv pip list` in the image) incl. torch 2.10.0+cu128 and its NVIDIA CUDA 12.8 wheels under "System dependencies (generator image, GPU only)"; README: generative section (start command, GPU requirement, disk/VRAM needs, labels, ethics rules)
- [ ] T084 [CTO] Owner listening check (quickstart Q9, SC-004); `/speckit-analyze`; `docs/STATUS.md`; PR

---

## Execution plan per owner (parallel wave)

All owners start after Phase 0 from the Phase 0 commit, each in its own worktree. Order inside a
sequence is binding.

| Owner | Ordered task sequence | Owned paths (write) | May mock / fake |
|---|---|---|---|
| generative | T012 -> T013 -> T014 -> T015 -> T016 -> T017 -> T018 -> T019 -> T020 -> T021 -> HO:foundational -> T032 -> T033 -> T034 -> T035 -> T036 -> HO:us1 -> T051 -> T052 -> HO:us6 -> T068 -> T069 -> HO:us5 -> T074 (HO:polish) | `generator/src/stemstudio_generator/{api,jobs,devices,engines,audio}/**`, `main.py`, `generator/tests/**` | fake engine, fake NVML/torch adapters, tmp lock files, fake clock, fake `acestep` module |
| stem-separation | T022 -> T023 -> HO:foundational -> T075 (HO:polish) | `worker/src/stemstudio_worker/devices/**`, `worker/tests/devices/**` | tmp lock files, fake clock |
| backend | T024 -> T025 -> T026 -> T027 -> T028 -> T029 -> T030 -> T031 -> HO:foundational -> T037 -> T038 -> T039 -> T040 -> T041 -> T042 -> HO:us1 -> T046 -> T047 -> HO:us2 -> T053 -> T054 -> HO:us6 -> T058 -> T059 -> HO:us3 -> T063 -> T064 -> HO:us4 -> T070 -> T071 -> HO:us5 -> T076 (HO:polish) | `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | stub generator handler, fake `IGeneratedAudioLibrary`, `FakeTimeProvider`, SQLite |
| frontend | T043 -> T044 -> HO:us1 -> T048 -> T049 -> HO:us2 -> T055 -> T056 -> HO:us6 -> T060 -> T061 -> HO:us3 -> T065 -> T066 -> HO:us4 -> T072 -> T073 -> HO:us5 -> T077 (HO:polish) | `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | fake API client, bUnit |
| qa | T011 -> T045 -> HO:us1 -> T050 -> HO:us2 -> T057 -> HO:us6 -> T062 -> HO:us3 -> T067 -> HO:us4 -> T078 (HO:polish) | `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | suite executed in T081 |
| devops | T009 -> T010 -> HO:foundational -> T079 (HO:polish) | `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | fake generator image |
| quality-gate | T007 -> T008 -> HO:foundational -> T080 (HO:polish) | `tools/quality/**`, `docs/quality/**` | sample files |
| architect | T002 -> T003 -> T004 -> T005 -> T006 (before the wave; contract fixes on request) | `specs/**`, ADRs, skeleton files | n/a |
| CTO | T001 (before the wave) -> T081 -> T082 -> T083 -> T084 | constitution, `.claude/agents/generative.md`, `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, merges | n/a |

Task count per owner: CTO 5 (T001, T081-T084), architect 5 (T002-T006), generative 20
(T012-T021, T032-T036, T051-T052, T068-T069, T074), stem-separation 3 (T022, T023, T075),
backend 25 (T024-T031, T037-T042, T046-T047, T053-T054, T058-T059, T063-T064, T070-T071, T076),
frontend 13 (T043-T044, T048-T049, T055-T056, T060-T061, T065-T066, T072-T073, T077), qa 7 (T011,
T045, T050, T057, T062, T067, T078), devops 3 (T009, T010, T079), quality-gate 3 (T007, T008,
T080). **Total 84** (T001-T084).

### Flagged cross-owner tasks (converted)

| Task | Would have needed | Converted to |
|---|---|---|
| T005 backend contract staging | backend test files | new operationIds staged by the architect; un-staged by the backend as each endpoint lands |
| T009 api env var in compose | api configuration | devops sets `StemStudio__GeneratorBaseUrl`; the backend reads it through `StemStudioApiOptions` (T053) |
| T040 007 library adapter | 007 library code | backend owns the adapter in `Api/Generation/`; it calls 007's public service only |
| T083 licences | owner hand-offs | consolidated by the CTO (AGENT-PROTOCOL rule 9) |

## Dependencies & Execution Order

T001 (amendment, agent role) -> Phase 0 -> wave (Setup + Foundational + stories per owner, parallel
across owners) -> Integration wave. Stories are independent at the contract level (fakes). 010
starts only after T084.

## Definition of Done checklist (mirrors the constitution)

- [ ] Red-green-refactor followed; failing test commit before implementation commit (I)
- [ ] All tests green; >= 90 % line AND branch per component incl. the generator, exact numbers in the hand-off (I)
- [ ] Models behind `MusicEngine`/`GpuGate`; no test needs a GPU except the marked, CI-skipped integration test (I)
- [ ] SIG limits respected; `tools/quality/run_all.py` passes incl. the generator layer rule (II)
- [ ] Implementations match generator 1.0.0, backend and UI ids versions of T002; contract tests pass (III)
- [ ] Constitution 1.3.0 amendment in place; default stack, CI and the fake generator image free of NVIDIA wheels; licences recorded with SHA-256 (IV, ADR-009/011/018)
- [ ] No artist names in presets (automated); AI labels on 100 % of takes (automated); personal-use notice shown (IV)
- [ ] GPU-only behaviour visible with reasons; timings and VRAM measured on the reference machine (V)
- [ ] Help entries present and linked; no secrets; analyzers/formatters/linters clean; public APIs documented
- [ ] Hand-off notes written; Conventional Commits with the Co-Authored-By trailer
