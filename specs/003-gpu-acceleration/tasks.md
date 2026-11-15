---

description: "Task list for feature 003 GPU acceleration (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: GPU Acceleration

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/](contracts/),
constitution 1.2.0, ADR-011, ADR-012.

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component in its hand-off. No task needs a GPU: device code is tested with
fakes; the real GPU path is validated by the CTO on the reference machine (T052).

**Organization**: grouped by user story (US1..US3); the
[execution plan](#execution-plan-per-owner-parallel-wave) regroups the IDs per agent.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on
  an incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: the agent from `.claude/agents/`; it writes only inside its owned paths (plan.md
  ownership map) plus `docs/handoffs/<owner>-003-<checkpoint>.md`.
- Paths are relative to the repository root. **CTO** = orchestrator.
- audio-engine, audio-ingest and beat-analysis have no tasks in this feature.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-003-<checkpoint>.md` at `foundational` (if it has
foundational tasks), every story it takes part in (`us1`..`us3`) and `polish`, with red -> green
commit hashes per task pair, exact coverage numbers and lint/SIG output. Marker `HO:<checkpoint>`
in the execution plan.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| stem-separation (`separation/`, `detail/`, `jobs/`) | the device policy | `ports.py` `DeviceRunner`, `GpuNeed`, `DeviceUse`, `merge_device` (T001) | a fake runner returning `DeviceUse`s / raising |
| backend | worker device fields, worker health | `contracts/worker-api.openapi.yaml` 1.2.0 (examples copied in `tests/StemStudio.Api.Tests/Worker/Examples/`, T003) | stub `HttpMessageHandler` |
| frontend | `Track.device`, `DetailPhase.device`, `GET /api/device` | `backend-api.openapi.yaml` 1.3.0 + Contracts DTOs (T003) | fake `IStemStudioApiClient` |
| qa | UI | `contracts/ui-test-ids.md` 1.2.0, `STEMSTUDIO_FAKE_DEVICE` (T001) | suite authored in the wave, executed in T051 |
| devops | build args, env vars | plan.md, data-model.md 1, research.md G-01/G-05 | skeleton |

---

## Phase 0: Contract code and skeleton (architect, BEFORE the wave)

- [x] T001 [architect] Worker contract code: `schemas.py` (`CONTRACT_VERSION = "1.2.0"`; `ProcessingDeviceKind`, `DeviceInfo` (MiB aliases), `CPU_DEVICE`, `JobDevice`, `Job.device`, `DetailJob.device`, `Health.device_info` defaulting to `CPU_DEVICE`); `ports.py` (`GpuNeed`, `DeviceUse`, `DeviceRunner` protocol, `merge_device()` implemented per data-model 2.2, `SeparationResult.device`); `config.py` (`fake_device` from `STEMSTUDIO_FAKE_DEVICE`, `cpu`/`gpu`/`gpu-oom`); skeleton tests `worker/tests/test_device_contract.py`; worker contract tests switched to the 003 document (`tests/test_detail_contract.py`, `tests/api/contract.py`) and the `/health` assertions updated to 1.2.0 (agreed one-time edit in stem-separation tests) (FR-003, FR-006)
- [x] T002 [P] [architect] Worker dependency `nvidia-ml-py==13.615.71` (BSD-3, pure Python) in `worker/pyproject.toml` + `uv.lock`; mypy `ignore_missing_imports` for `pynvml`; licence in `docs/handoffs/architect-003-plan.md`
- [x] T003 [P] [architect] .NET contract code: `Tracks/{JobDeviceDto (+ DisplayText), ProcessingDeviceKindDto}.cs`, `TrackDto.Device`, `DetailPhaseDto.Device`, `Health/ProcessingDeviceDto.cs` (+ `Unreachable`), `ApiRoutes.Device`; skeleton tests `tests/StemStudio.Api.Tests/Contract/DeviceContractTests.cs`; `StemStudio.Api.Tests.csproj` links -> 003 contracts; worker example copies re-synced (`Worker/Examples/*.json` + `device`); `getProcessingDevice` staged in `PendingDetailOperations` and in the operationId list (agreed one-time edits in backend tests) (FR-006)
- [x] T004 [P] [architect] Contracts: worker-api 1.2.0, backend-api 1.3.0, ui-test-ids 1.2.0, redocly config (Redocly lint valid); ADR-012
- [x] T005 [architect] Verify the skeleton: `dotnet build` 0 warnings, all .NET tests + coverage gates, worker `pytest --cov`/ruff/mypy, engine untouched, `tools/quality/run_all.py` 0 errors; hand-off `docs/handoffs/architect-003-plan.md`

**Checkpoint**: skeleton green; wave may start.

---

## Phase 1: Setup (per owner, no story)

- [ ] T006 [P] [quality-gate] Failing tests in `tools/quality/tests/`: the dependency rule lets `worker/src/stemstudio_worker/devices` import only `ports`, `schemas`, `config`, and forbids `separation`, `detail`, `jobs`, `api` from importing `devices` (only `main.py` composes it) (plan.md dependency direction)
- [ ] T007 [quality-gate] Implement the rule in `tools/quality/dependency_check.py`; re-run `tools/quality/run_all.py`; update `docs/quality/SIG-REPORT.md`
- [ ] T008 [P] [devops] `docker/worker.Dockerfile`: build arg `ORT_VARIANT` (`cpu` default; `gpu` uninstalls `onnxruntime` and installs `onnxruntime-gpu==1.32.0` without extras plus `cuda-toolkit[curand]==13.2.2`, research.md G-01) next to `TORCH_VARIANT`; header comment updated. `docker-compose.gpu.yml`: `TORCH_VARIANT` default **`cu132`** (fixes the broken `cu128`, FR-009), `ORT_VARIANT: gpu`, image `stemstudio/worker:local-gpu`, `STEMSTUDIO_DEVICE` default `auto`, keep the `deploy.resources.reservations.devices` block, comments (prerequisites, ~11.3 GB image, no Linux driver inside WSL). `docker-compose.yml`: comment line for the GPU option; pass `STEMSTUDIO_FAKE_DEVICE` (default `cpu`) to the worker. CI `CONTRACTS_DIR` and `scripts/test-all.{ps1,sh}` -> `specs/003-gpu-acceleration/contracts`; `scripts/dev.{ps1,sh}` pass `STEMSTUDIO_FAKE_DEVICE` (FR-001, FR-009)
- [ ] T009 [devops] Write the CPU-image guard first and show it fail on a GPU-variant image, then wire it: `scripts/check-cpu-image.{ps1,sh}` (no distribution named `nvidia-*` other than `nvidia-ml-py`, no `cuda-*`, `triton` or `onnxruntime-gpu`, and `torch.version.cuda is None`; quickstart G2) and CI job `cpu-image-guard`; `scripts/gpu-smoke.{ps1,sh}` (build `stemstudio/worker:local-gpu`, run with `--gpus all`: torch `+cu132`, `sm_<cc>` in arch list, matmul, onnxruntime CUDA EP after `preload_dlls()`, `/health` `deviceInfo.kind == gpu`; and without `--gpus`: `kind == cpu` with a reason; quickstart G3/G4) (FR-003, FR-008)
- [ ] T010 [P] [qa] Update `docs/testing/TEST-STRATEGY.md`: GPU path tested with fakes (device runner, probe, NVML), `STEMSTUDIO_FAKE_DEVICE` for E2E, reference-machine validation by the CTO, traceability US1..US3 -> test files

---

## Phase 2: Foundational (blocking inside each owner)

### stem-separation (tests first)

- [x] T011 [P] [stem-separation] Failing tests `worker/tests/devices/test_probe.py` with fake torch/NVML adapters: `cpu` preference never imports torch; each probe step of data-model 2.1 fails with its exact reason (CPU build, CUDA unavailable, architecture missing, test kernel error truncated to 200 chars, NVML unavailable); success yields `DeviceInfo(kind=gpu, name, computeCapability "12.0", total/free MiB)`; `cuda` and `auto` behave the same (FR-003)
- [x] T012 [stem-separation] Implement `devices/probe.py` and `devices/nvml.py` (lazy `torch`/`pynvml` imports, adapters injectable) to pass T011
- [x] T013 [P] [stem-separation] Failing tests `worker/tests/devices/test_runner.py` (fake NVML reader, fake torch memory hooks, fake steps): CPU runner calls `work("cpu")` and reports `DeviceUse(cpu)`; GPU runner holds the lease (a second thread waits; released on error); NVML free < need -> `work("cpu")` with the exact "Not enough free GPU memory ..." reason and no CUDA call; fits -> memory fraction `(free - 256 MiB) / total` set before and reset after the step; `torch.cuda.OutOfMemoryError` or an onnxruntime "out of memory"/"Failed to allocate memory" error on the GPU attempt -> `empty_cache()` and exactly one `work("cpu")` with the "ran out of memory" reason; the CPU attempt's error propagates; a non-OOM GPU error propagates without retry (FR-004, FR-005, SC-004)
- [x] T014 [stem-separation] Implement `devices/{lease,oom,runner}.py` to pass T013
- [x] T015 [P] [stem-separation] Failing tests `worker/tests/devices/test_factory.py`, `test_fake.py`: `create_device_runner(settings)` returns the CPU runner for `cpu`, the probed runner for `cuda`/`auto` (CPU runner + reason when the probe fails), the fake runner for `separator=fake`; fake `gpu` reports `DeviceInfo(gpu, "Fake GPU")` and GPU uses; fake `gpu-oom` makes the first `run` of each job context fall back with the OOM reason (data-model 1)
- [x] T016 [stem-separation] Implement `devices/fake.py` and `devices/__init__.py::create_device_runner` to pass T015

### backend (tests first)

- [ ] T017 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Tracks/JobDeviceTests.cs`: `JobDevice` value rules (data-model 3.1: name <= 200 and null for CPU, reason <= 500 and non-null iff fallback); `Track.ApplyDevice` while not terminal, kept after completion, reset by retry; `DetailPhase` device set by progress/completion, reset by (re)start; invalid worker data rejected without failing the track (FR-006)
- [ ] T018 [backend] Implement `src/StemStudio.Domain/Tracks/JobDevice.cs`, `Track.Device`, `DetailPhase.Device` to pass T017
- [ ] T019 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/JobDevicePersistenceTests.cs`: JSON round trip of `Track.Device` and `DetailPhase.Device`; a database migrated from the 002 schema loads with null devices (file-based SQLite)
- [ ] T020 [backend] Implement the configuration and migration `AddJobDevice` (`dotnet ef migrations add AddJobDevice`) to pass T019
- [ ] T021 [P] [backend] Failing tests `tests/StemStudio.Api.Tests/Worker/WorkerDeviceClientTests.cs` with the 1.2.0 examples: `WorkerJob.Device`, `WorkerDetailJob.Device` parsed (null when absent, 1.1.0 bodies still parse); `GetHealthAsync` parses `deviceInfo` (cpu, gpu, gpu-unavailable examples); unreachable -> `WorkerUnavailableException` within `WorkerTimeouts.Health` (2 s)
- [ ] T022 [backend] Implement `WorkerJob`/`WorkerDetailJob` device members, `WorkerHealth`, `IWorkerClient.GetHealthAsync` to pass T021

**Checkpoint**: HO:foundational for stem-separation and backend.

---

## Phase 3: User Story 1 - Faster separation on a GPU machine (P1) MVP

**Goal**: in GPU mode standard and detail separation run on the GPU and the track page shows it.
**Independent test**: quickstart G3, G5, G6, G7 (CTO, T052); fakes in the wave.

- [x] T023 [P] [US1] [stem-separation] Failing tests `worker/tests/separation/test_demucs_device.py`: `DemucsSeparator` runs the separation through the injected `DeviceRunner` with `GpuNeed("htdemucs_6s", <research.md G-04>)`, passes the runner's device string to `apply`, returns `SeparationResult.device = merge_device(None, use)`; a CPU retry calls `apply` again with `cpu` (FR-002, FR-004)
- [x] T024 [US1] [stem-separation] Implement in `separation/` (factory `create_separator(settings, devices)`) to pass T023
- [x] T025 [P] [US1] [stem-separation] Failing tests `worker/tests/detail/test_device_models.py`: `RealModelProvider` caches per `(model_id, device)`; KARA 2 on `cuda` calls `onnxruntime.preload_dlls()` and creates the session with `["CUDAExecutionProvider", "CPUExecutionProvider"]`, and when the session's first provider is not CUDA the step reports the "ONNX Runtime has no usable CUDA provider" fallback; every parent step runs through the runner with its model's `GpuNeed` (research.md G-04); `release_gpu()` drops only GPU instances (FR-002, ADR-012 D6)
- [x] T026 [US1] [stem-separation] Implement in `detail/{runtimes,steps,registry,__init__}.py` (factories receive the runner) to pass T025
- [x] T027 [P] [US1] [stem-separation] Failing tests `worker/tests/jobs/test_job_device.py` (fake runner, fake clock): `Job.device` is set when the separation starts and replaced by the result's device; cached results keep `device = null`; `DetailJob.device` is folded after every parent step and persisted in `detail-jobs/*.json`; the scheduler calls the idle hook (GPU release) when both queues are empty; `/health` returns the runner's `deviceInfo` (FR-006)
- [x] T028 [US1] [stem-separation] Implement in `jobs/{runner,progress,detail_runner,detail_progress,scheduler,runtime}.py`, `api/routes.py`, `main.py` (probe once, one runner shared) to pass T027
- [ ] T029 [P] [US1] [backend] Failing tests `tests/StemStudio.Api.Tests/Sync/DeviceSyncTests.cs`: every standard poll copies `device` to `Track.Device`; every detail poll copies it to `DetailPhase.Device`; `TrackDto.Device`/`DetailPhaseDto.Device` mapped; served `GET /api/tracks/{id}` validates against backend 1.3.0 `Track` with a device (FR-006)
- [ ] T030 [US1] [backend] Implement in `Sync/TrackJobApplier.cs`, `Detail/DetailJobApplier.cs`, track/detail mappers to pass T029
- [ ] T031 [P] [US1] [frontend] Failing bUnit tests `tests/StemStudio.Web.Tests/Components/DeviceBadgeTests.cs` and track-page tests: `DeviceBadge` renders `JobDeviceDto.DisplayText()` with `data-device` and `data-fallback`; `track-device` / `detail-device` exist only when the device is set; `track-device-fallback` / `detail-device-fallback` show the reason only when `cpuFallback` (ui-test-ids 1.2.0) (US1-1, FR-006)
- [ ] T032 [US1] [frontend] Implement `src/StemStudio.Web/Components/Shared/DeviceBadge.razor` and use it in `TrackDetail.razor` and `DetailSection.razor` to pass T031
- [ ] T033 [US1] [qa] Author `e2e/tests/gpu.spec.ts` "device on the track page" (`@gpu`, fake stack with `STEMSTUDIO_FAKE_DEVICE=gpu`): `track-device` and `detail-device` have `data-device="gpu"` and text "GPU (Fake GPU)"; on the default fake stack the existing suite sees `data-device="cpu"` (one assertion added to the main-flow spec)

**Checkpoint**: HO:us1.

---

## Phase 4: User Story 2 - Works everywhere, falls back automatically (P1)

**Goal**: CPU default unchanged; GPU unavailable -> CPU with reason; OOM -> step retried on the CPU.
**Independent test**: quickstart G1, G2, G4, G8, G9.

- [x] T034 [P] [US2] [stem-separation] Failing API-level tests `worker/tests/api/test_device_fallback.py` (TestClient, fake runner `gpu-oom`, injected failing probe): a standard and a detail job complete with `device.cpuFallback = true` and the OOM reason; `/health` reports `kind = cpu` and the probe reason; with `STEMSTUDIO_DEVICE=cpu` no probe runs and every job reports `CPU` without fallback (FR-003, FR-004, SC-004)
- [x] T035 [US2] [stem-separation] Implement anything missing to pass T034
- [ ] T036 [P] [US2] [backend] Failing tests: the fallback reason from the worker `RunningDetailJob` example reaches `DetailPhaseDto.Device.FallbackReason`; a 1.1.0-shaped worker job (no `device`) keeps `Track.Device = null`; retrying a failed track clears the device (FR-004, FR-006)
- [ ] T037 [US2] [backend] Implement anything missing to pass T036
- [ ] T038 [US2] [qa] Author `gpu.spec.ts` "fallback notice" (`@gpu`, `STEMSTUDIO_FAKE_DEVICE=gpu-oom`): the job completes, `track-device-fallback` and `detail-device-fallback` contain "ran out of memory"; mark `@gpu` specs so the default run excludes them (`grepInvert`) (SC-004)
- [ ] T039 [US2] [devops] CI `e2e` job: after the default fake-stack run, restart the fake stack with `STEMSTUDIO_FAKE_DEVICE=gpu-oom` and run `--grep @gpu`; same in `scripts/test-all.*` (optional flag `-Gpu`/`--gpu-e2e`)

**Checkpoint**: HO:us2.

---

## Phase 5: User Story 3 - See which device is used (P3)

**Goal**: the processing device is visible before importing (worker health) and per job.
**Independent test**: quickstart G7.

- [ ] T040 [P] [US3] [backend] Failing tests `tests/StemStudio.Api.Tests/Device/DeviceEndpointTests.cs`: `GET /api/device` maps worker `deviceInfo` (gpu, cpu with reason); worker unreachable or > 2 s -> `200 {reachable: false}`; answers cached 10 s (`FakeTimeProvider`); response validates against `ProcessingDevice`; remove `getProcessingDevice` from `PendingDetailOperations` (FR-006)
- [ ] T041 [US3] [backend] Implement `src/StemStudio.Api/Device/DeviceEndpoints.cs` (+ registration) to pass T040
- [ ] T042 [P] [US3] [frontend] Failing tests: `IStemStudioApiClient.GetDeviceAsync`; import page shows `worker-device` with `data-device` `gpu`/`cpu`/`unknown` and `worker-device-reason` only when a reason is set (FR-006, US3-1)
- [ ] T043 [US3] [frontend] Implement the client method and the import-page display (reusing `DeviceBadge`) to pass T042
- [ ] T044 [US3] [qa] Author `gpu.spec.ts` "worker device on the import page" (`@gpu`: `data-device="gpu"`) and a default-suite assertion `data-device="cpu"` on the import page

**Checkpoint**: HO:us3.

---

## Phase 6: Polish (inside the wave, per owner)

- [x] T045 [P] [stem-separation] Refactor to SIG limits, docstrings, HO:polish with coverage of `devices`, `separation`, `detail`, `jobs`, `api`
- [ ] T046 [P] [backend] Refactor, XML docs, HO:polish with Domain/Persistence/Api coverage
- [ ] T047 [P] [frontend] Accessibility of the device badge (text, not colour only), HO:polish
- [ ] T048 [P] [qa] Traceability matrix update, HO:polish
- [ ] T049 [P] [devops] Validate compose/CI changes (`docker compose -f docker-compose.yml -f docker-compose.gpu.yml config -q`, `actionlint`), HO:polish
- [ ] T050 [P] [quality-gate] `run_all.py` on the whole repo; refactor requests per owner; HO:polish

---

## Integration wave (CTO, after all wave hand-offs are accepted)

- [ ] T051 [CTO] Merge the owner branches; full build, lint, unit + coverage gates green; Playwright default suite + `@gpu` suite on the fake stacks (retries 0)
- [ ] T052 [CTO] Real GPU validation on the reference machine (quickstart G3-G7, G9, G10): build the GPU stack, import "Hero of the Day" as a fresh track, record standard and detail wall times, peak VRAM (`nvidia-smi --query-gpu=memory.used -lms 500`), per-parent outcomes vs the 002 CPU run, reconstruction check; also run the default CPU stack once (G1/G2). Record in `docs/STATUS.md`; if SC-001 fails, see research.md G-07 levers
- [ ] T053 [CTO] `docs/THIRD-PARTY-LICENSES.md`: new heading "System dependencies (GPU variant only)" listing the NVIDIA CUDA runtime wheels with exact versions from the GPU image (`uv pip list` inside it; research.md G-01 list) under the NVIDIA licence, plus `triton` (MIT), `onnxruntime-gpu` 1.32.0 (MIT), `torch` 2.15.0+cu132 (BSD-3); `nvidia-ml-py` 13.615.71 (BSD-3) in the normal list. README: GPU section (prerequisites, start command, first build size/time, how to read the device indication and fallback notices, "no Linux driver inside WSL", troubleshooting by health reason) (FR-001, FR-008)
- [ ] T054 [CTO] `/speckit-analyze` consistency pass; update `docs/STATUS.md`; PR

---

## Execution plan per owner (parallel wave)

All owners start after Phase 0 from the Phase 0 commit, each in its own worktree. Order inside a
sequence is binding.

| Owner | Ordered task sequence | Owned paths (write) | May mock / fake |
|---|---|---|---|
| stem-separation | T011 -> T012 -> T013 -> T014 -> T015 -> T016 -> HO:foundational -> T023 -> T024 -> T025 -> T026 -> T027 -> T028 -> HO:us1 -> T034 -> T035 -> HO:us2 -> T045 (HO:polish) | `worker/src/stemstudio_worker/{devices,separation,detail,jobs,api}/**`, `main.py`, `worker/scripts/**`, `worker/tests/{devices,separation,detail,jobs,api}/**` | fake torch/NVML adapters, fake `DeviceRunner`, fake models, fake clock |
| backend | T017 -> T018 -> T019 -> T020 -> T021 -> T022 -> HO:foundational -> T029 -> T030 -> HO:us1 -> T036 -> T037 -> HO:us2 -> T040 -> T041 -> HO:us3 -> T046 (HO:polish) | `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | stub `HttpMessageHandler` with worker 1.2.0 examples, `FakeTimeProvider`, SQLite |
| frontend | T031 -> T032 -> HO:us1 -> T042 -> T043 -> HO:us3 -> T047 (HO:polish) | `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | fake `IStemStudioApiClient`, bUnit |
| qa | T010 -> T033 -> HO:us1 -> T038 -> HO:us2 -> T044 -> HO:us3 -> T048 (HO:polish) | `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | suite executed in T051 |
| devops | T008 -> T009 -> HO:foundational -> T039 -> HO:us2 -> T049 (HO:polish) | `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | skeleton; a locally built GPU-variant image for the guard's red run |
| quality-gate | T006 -> T007 -> HO:foundational -> T050 (HO:polish) | `tools/quality/**`, `docs/quality/**` | sample files |
| architect | T001 -> T002 -> T003 -> T004 -> T005 (before the wave; later contract fixes on request) | `specs/**`, ADRs, skeleton files | n/a |
| CTO | T051 -> T052 -> T053 -> T054 | `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, merges | n/a |

Task count per owner: architect 5, stem-separation 15, backend 13, frontend 5, qa 5, devops 4,
quality-gate 3, CTO 4 (total 54: T001-T054).

### Flagged cross-owner tasks (converted)

| Task | Would have needed | Converted to |
|---|---|---|
| T001 `/health` assertions in stem-separation tests | stem-separation test files | one-time edit by the architect (version + default `deviceInfo`), recorded in the hand-off |
| T003 backend contract staging | backend test files | `getProcessingDevice` staged by the architect; un-staged by the backend in T040 |
| T008/T039 contract path and e2e env | devops files | devops tasks; Phase 0 keeps CI on the 002 path until T008 (both documents lint clean) |
| T053 licences | owner hand-offs | consolidated by the CTO (AGENT-PROTOCOL rule 9) |

## Dependencies & Execution Order

Phase 0 -> wave (Setup + Foundational + stories per owner, parallel across owners) -> Integration
wave. Inside the wave every owner follows its sequence; stories are independent at the contract
level (fakes).

## Definition of Done checklist (mirrors the constitution)

- [ ] Red-green-refactor followed; failing test commit before implementation commit (I)
- [ ] All tests green; >= 90 % line AND branch per component, exact numbers in the hand-off (I)
- [ ] Device code behind `DeviceRunner` and injected torch/NVML/onnxruntime adapters; no test needs a GPU; no new real-model integration test (I)
- [ ] SIG limits respected; `tools/quality/run_all.py` passes incl. the `devices` dependency rule (II)
- [ ] Implementations match worker 1.2.0, backend 1.3.0, UI ids 1.2.0 (engine 2.0.0 unchanged); contract tests pass (III)
- [ ] CPU image and CI free of NVIDIA runtime wheels (guard green); GPU-variant system dependencies listed in THIRD-PARTY-LICENSES; everything works on the CPU (IV, ADR-011)
- [ ] Fallbacks and the device are shown to the user with their reason; GPU speed and VRAM measured on the reference machine (V)
- [ ] No secrets; analyzers/formatters/linters clean; public APIs documented
- [ ] Hand-off notes written; Conventional Commits with the Co-Authored-By trailer
