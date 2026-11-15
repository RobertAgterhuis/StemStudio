# Hand-off: architect / 003-plan

## What was built

- **Feasibility spike (measured)** on the reference machine (RTX 5070 Ti 16 GB, sm_120, driver
  610.88, Docker Desktop WSL2) with a throwaway GPU variant of the worker image
  (`stemstudio/worker:gpu-spike`: torch 2.15.0+cu132, onnxruntime-gpu 1.32.0) running the
  worker's own 002 code on the reference song "Hero of the Day" (4:22), with the cached CPU stems
  and weights mounted read-only. The running `stemstudio` stack was never touched. Results and
  commands: `specs/003-gpu-acceleration/research.md` (G-01..G-07). Both spike images removed.

  | Step (4:22) | CPU | GPU | Speed-up |
  |---|---|---|---|
  | Standard htdemucs_6s (load + inference + FLAC) | ~162 s | 23.2 s | 7.0x |
  | MDX23C DrumSep (drums) | 234.2 s | 31.5 s | 7.4x |
  | KARA 2, onnxruntime CUDA EP (vocals) | 53.1 s | 19.6 s | 2.7x |
  | Mega 53-stem (guitar) | 881.8 s | 55.7 s | 15.8x |
  | **Standard + detail, cold models** | **31.5 min** | **3.7 min** | **8.5x** (warm: 2.2 min, 14.6x) |

  Peak VRAM 13.2 GB NVML-used (worker ~12.3 GB, Mega 53 allocated 9.35 GiB / reserved 11.27 GiB).
  Per-parent outcomes identical to the CPU run (drums 5 + rest, vocals 2 + rest, guitar 2 + rest,
  bass not supported, piano/other skipped), reconstruction exact, detail stems >= 52 dB SDR vs CPU.
  Image: CPU 3.36 GB, GPU 11.31 GB on disk (11.57 GB with cuRAND).
- **Findings that shaped the design**: (1) under WSL2 CUDA never raises OOM, it spills into shared
  memory (40 GB allocated on a 16 GB card, ~20x slower) -> per-step
  `set_per_process_memory_fraction` (measured to turn the spill into `OutOfMemoryError`);
  (2) `torch.cuda.mem_get_info` over-reported free memory (15.0 vs 9.7 GiB) -> NVML via
  `nvidia-ml-py`; (3) onnxruntime-gpu silently used the CPU EP until `preload_dlls()` was called
  and `nvidia-curand` installed; (4) htdemucs differs run to run on one device (Demucs `shifts=1`),
  so FR-007 equality is defined on outcomes, not samples.
- **Design** (ADR-012): image variants via build args, start-up probe, `DeviceRunner` (GPU lease,
  NVML free-memory check vs per-model `GpuNeed`, memory fraction, single CPU retry on GPU OOM),
  idle release of GPU models, `JobDevice` records, `GET /api/device`, fake device for E2E, CI CPU-only.
- Spec Kit artefacts: `plan.md` (Constitution Check incl. ADR-011, Complexity Tracking),
  `research.md`, `data-model.md`, `quickstart.md` (G1-G10), `tasks.md` (T001-T054), contracts
  (worker 1.2.0, backend 1.3.0, UI test ids 1.2.0; engine 2.0.0 unchanged), Clarifications session
  in `spec.md` (10 autonomous decisions), ADR-012.
- **Phase 0 skeleton (T001-T005)**: worker `schemas.py` / `ports.py` / `config.py` contract code,
  `nvidia-ml-py` dependency, .NET DTOs + route, skeleton tests, contract tests switched to the 003
  documents.

Commits: `67e89b6` docs(plan) (spec artefacts, contracts, ADR-012), **`7e54809` feat(contracts)
(Phase 0 skeleton; the wave starts from here)**, plus this hand-off.

## Files changed

- `specs/003-gpu-acceleration/{plan,research,data-model,quickstart,tasks}.md` (new), `spec.md`
  (Clarifications), `contracts/{worker-api.openapi.yaml, backend-api.openapi.yaml, ui-test-ids.md, redocly.yaml}` (new, full documents)
- `docs/decisions/ADR-012-gpu-device-policy.md` (new)
- Worker: `src/stemstudio_worker/{schemas,ports,config}.py`, `pyproject.toml`, `uv.lock`,
  `tests/test_device_contract.py` (new), `tests/test_detail_contract.py`, `tests/test_smoke.py`
- .NET: `src/StemStudio.Contracts/{ApiRoutes.cs, StemStudio.Contracts.csproj, Tracks/TrackDto.cs, Tracks/DetailPhaseDto.cs}`,
  new `Tracks/JobDeviceDto.cs`, `Tracks/ProcessingDeviceKindDto.cs`, `Health/ProcessingDeviceDto.cs`;
  `tests/StemStudio.Api.Tests/StemStudio.Api.Tests.csproj` (links -> 003), new `Contract/DeviceContractTests.cs`
- **Agreed one-time edits outside the lane** (mechanical consequences of the version bump, as in
  002): `worker/tests/api/contract.py` (spec path -> 003), `worker/tests/api/test_jobs_api.py` and
  `test_detail_routes.py` (`/health` 1.2.0 body with the default `deviceInfo`);
  `tests/StemStudio.Api.Tests/Contract/BackendContractTests.cs` (operationId list +
  `getProcessingDevice`), `BackendContractTests.StemsAndMixes.cs` (`getProcessingDevice` staged in
  `PendingDetailOperations`; the backend removes it in T040), `Worker/Examples/*.json` (8 example
  copies re-synced from worker 1.2.0: `device` added)
- `docs/handoffs/architect-003-plan.md` (this note)

## Tests and coverage

`scripts/test-all.ps1` (local equivalent of CI): **29/29 steps passed** (exit 0).

```text
dotnet build StemStudio.slnx -c Release            0 Warning(s), 0 Error(s)
dotnet format --verify-no-changes                  passed
dotnet test -p:CollectCoverage=true (gates on):
  StemStudio.Domain.Tests       239 passed   Domain        line 98.46 %  branch 97.54 %
  StemStudio.Persistence.Tests   40 passed   Persistence   line 98.59 %  branch 100 %
  StemStudio.Web.Tests          284 passed   Web           line 99.87 %  branch 94.84 %
  StemStudio.Api.Tests          381 passed   Api           line 99.6 %   branch 97.92 %
                                (+12 new)    Contracts     line 100 %    branch 100 %
worker: uv run pytest --cov --cov-branch           669 passed, 3 deselected (integration)
  TOTAL 3100 stmts, 0 missed, 350 branches, 1 partial (pre-existing) -> line 100 %, branch 99.71 %
  ports.py, schemas.py, config.py 100 % (new skeleton tests: tests/test_device_contract.py, 19 tests)
worker ruff check / ruff format --check / mypy --strict (src, tests: 130 files)   clean
engine (untouched): lint, typecheck, test:coverage 100 % stmt/branch/line, build    passed
quality tools: pytest 100 %; fixtures: pytest 100 %, generate passed
e2e: npm ci, lint, typecheck, playwright --list                                     passed
npx @redocly/cli@2.78.0 lint --config specs/003-gpu-acceleration/contracts/redocly.yaml   specs/003-gpu-acceleration/contracts/{worker,backend}-api.openapi.yaml
  -> both valid (OpenAPI 3.1), 4 warnings: operation-4xx-response on worker GET /health and
     backend GET /health, GET /api/device (new, no client errors by design), GET /api/tracks
```

(`scripts/test-all.ps1` still lints the 002 contracts until devops T008; the 003 documents were
linted separately above.)

## Quality checks

- `tools/quality/run_all.py`: 495 files, 3,888 units, 24 clones, 1 exemption, **0 errors**,
  135 warnings (all pre-existing; the one new warning, `merge_device` complexity 6, was fixed by
  extracting `_merged_kind`) -> exit 0.
- `python tools/quality/sig_check.py worker/src/stemstudio_worker/ports.py`: 13 units, 0 errors,
  0 warnings.
- Public API documented: docstrings on every new schema/port/setting, XML docs on every new DTO
  member (TreatWarningsAsErrors passes).
- CPU image guard rule checked against the current `stemstudio/worker:local`: no NVIDIA/CUDA
  distribution, `torch.version.cuda` None (`2.15.0+cpu`).

## Dependencies added

| Name | Version | Licence | URL | Where |
|---|---|---|---|---|
| nvidia-ml-py | 13.615.71 | BSD-3-Clause (OSI) | https://pypi.org/project/nvidia-ml-py/ | worker, locked, both images |

Planned for the GPU image only (devops T008; not in `uv.lock`; CTO lists them in T053):

| Name | Version | Licence |
|---|---|---|
| torch (cu132 build) | 2.15.0+cu132 | BSD-3-Clause |
| triton | 3.9.x (as resolved by torch) | MIT |
| onnxruntime-gpu | 1.32.0 | MIT |
| NVIDIA CUDA runtime wheels: cuda-toolkit 13.2.2 (cuBLAS, CUDA runtime, CCCL, CUPTI, NVRTC, cuFFT, cuFile, cuSOLVER, cuSPARSE, nvJitLink, NVTX), nvidia-cudnn-cu13 9.26.0.51, nvidia-cusparselt-cu13, nvidia-nccl-cu13 2.30.x, nvidia-nvshmem-cu13, nvidia-curand 10.4.2.66 | as resolved | NVIDIA proprietary (free redistributable), system dependency per ADR-011 |

## Open issues / requests

- **devops (T008)**: the GPU image needs `cuda-toolkit[curand]==13.2.2` next to `onnxruntime-gpu`
  (research.md G-01); without it KARA 2 silently runs on the CPU. CI `CONTRACTS_DIR` and
  `scripts/test-all.*` still point to the 002 contracts until T008 (both lint clean).
- **stem-separation**: the GPU OOM check must inspect the original exception before
  `demix.inference_error` turns "alloc" errors into `MemoryError` (ADR-012 D5); KARA 2 must call
  `onnxruntime.preload_dlls()` and verify `session.get_providers()[0]`.
- **Risk: VRAM headroom.** Mega 53 needs 10.6 GB free; with a local LLM loaded (Ollama ~7 GB) the
  step runs on the CPU (~15 min). Correct per spec, but users with games/LLMs open will see it.
- **Risk: cold model load.** Mega 53 took 84 s to load under heavy CPU contention (the owner's
  CPU job ran during the spike); lever in research.md G-07 if T052 misses SC-001 (unlikely: 3.7 min
  vs 6.3 min budget).
- **Not verified**: other GPUs/drivers (only the reference machine), Linux + NVIDIA Container
  Toolkit, `expandable_segments`.
- **Build cache residue**: the spike's Docker build-cache layer records (~5.9 GB) and the grown uv
  download cache (7.4 GB incl. CUDA wheels) were left because the build cache is shared with other
  projects; `docker builder prune` reclaims them if wanted.
- Spec assumption "about 3 GB used by the desktop": measured 0.55-0.9 GB idle on the reference
  machine (the earlier 6 GB was an Ollama model); the design uses NVML at run time, so no change.

## Next owner

Wave owners per `specs/003-gpu-acceleration/tasks.md` execution plan: stem-separation (15 tasks),
backend (13), frontend (5), qa (5), devops (4), quality-gate (3); then the CTO integration wave
(T051-T054, including the real GPU measurement T052 and the licence/README work T053).
