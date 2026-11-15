# Hand-off: devops / 009 + 010 foundational and polish (009:T009, T010, T079; 010:T008, T058)

Branch `009-010-generative` @ 6accc47 (Phase 0 merge), own worktree. Covers the devops part of
the combined wave in one run: images, compose, scripts, CI (foundational) and the compose/CI
validation (polish).

## What was built

### `docker/generator.Dockerfile` (one file, three variants, build arg `GENERATOR_VARIANT`)

| Variant | Contents | Tag used by compose | Size (disk / content) |
|---|---|---|---|
| `fake` | base install of `generator/uv.lock` (no torch, no NVIDIA wheels, OSI only); `STEMSTUDIO_GENERATOR=fake`, file lease off | `stemstudio/generator:local-fake`, `stemstudio/singer:local-fake` | **931 MB / 242 MB** |
| `acestep` | + extra `acestep` (ACE-Step 1.5 @ `ca1e85f`, torch `2.10.0+cu128`) | `stemstudio/generator:local` | **13.7 GB / 4.71 GB** |
| `soulx` | + extra `soulx` (torch `2.10.0+cu128`, transformers 4.41.2) + SoulX-Singer @ `81aeb3a` (`soulxsinger/`, `preprocess/tools/f0_extraction.py`, LICENSE, COMMIT) in `/opt/soulx-singer`, `PYTHONPATH=/opt/soulx-singer`, kinds `voiceConversion` | `stemstudio/singer:local` | **13.2 GB / 4.60 GB** |

(Docker Desktop containerd store: "disk" = unpacked + compressed layers. The naive Phase 0 spike
image was 26.2 GB / 9.1 GB.) How it stays lean: multi-stage; the venv is resolved in a build stage
with a uv cache mount (`/root/.cache/uv`, never in a layer) and apt cache mounts, copied into the
runtime stage once with `--chown` (no second chown layer); `git` only in the build stage; the
SoulX source fetched by BuildKit `ADD <repo>.git#<commit>` in its own stage and reduced to the two
paths; `FROM variant-${GENERATOR_VARIANT}` so only the chosen variant's stages are built; gradio
already dropped by the lock. Runtime: `python:3.12-slim` + ffmpeg + libsndfile1, user
`stemstudio` (uid/gid 10001, same as the worker), `/data`, `/models`, `/gpu-lease` owned by it,
`HF_HOME=/models/hf`, `STEMSTUDIO_GPU_LEASE_FILE=/gpu-lease/gpu.lock`, port 8010 (no host port),
`HEALTHCHECK` on `/health`, CMD `uvicorn --factory stemstudio_generator.main:create_app --host 0.0.0.0 --port 8010`.
Build times (cold cache, both GPU variants in parallel): about 11 min each.

### Compose

- `docker-compose.generative.yml` (with base + `docker-compose.gpu.yml`): services `generator`
  (acestep) and `singer` (soulx), both with the NVIDIA GPU reservation, `model-cache:/models`,
  `gpu-lease:/gpu-lease`, own data volumes `generator-data` / `singer-data`, env per 009/010
  data-model 1 (`STEMSTUDIO_GENERATOR`, `_KINDS`, `_DATA_DIR`, `_MODEL_DIR`, `_GPU_LEASE_FILE`,
  `_GPU_MEMORY_WAIT_SECONDS`, `_SVC_MAX_TARGET_SECONDS`), healthcheck (start period 120 s);
  api env `StemStudio__GeneratorBaseUrl=http://generator:8010`,
  `StemStudio__SingerBaseUrl=http://singer:8010`, api `depends_on` both with `service_started`
  (the API reports "unavailable" until they answer).
- **Model-cache ownership (coordinator request)**: one-shot service `model-cache-owner` (same
  image, `user: 0:0`, `network_mode: none`) that hands `/models/acestep`, `/models/soulx` and
  `/models/hf` to uid 10001 only when a file there is not owned by 10001 (`find ! -user 10001
  -print -quit`, then `chown -R`); `generator` and `singer` depend on it with
  `service_completed_successfully`. So the root-seeded Phase 0 weights become readable *and*
  writable (locks, caches, re-download of a broken file) for both services; later starts only stat.
  Verified on a throw-away volume seeded with root-owned files: first run "handed to uid 10001"
  for all three folders, second run no-op, afterwards writable as 10001. The real
  `stemstudio_model-cache` volume was **not** modified by me; it is fixed the first time the CTO
  starts the generative stack.
- `docker-compose.generative-fake.yml` (base + generative + fake, no GPU override): both services
  and the ownership step use the fake variant; `deploy: !reset {}` removes the GPU reservation;
  `STEMSTUDIO_FAKE_GENERATOR_MODE` (default `ready`), `_STEP_SECONDS` (1),
  `STEMSTUDIO_GPU_MEMORY_WAIT_SECONDS` (default 5, for mode `busy`), lease file blank.
- `docker-compose.gpu.yml`: worker mounts `gpu-lease:/gpu-lease`, sets
  `STEMSTUDIO_GPU_LEASE_FILE=/gpu-lease/gpu.lock` and passes `STEMSTUDIO_GPU_LEASE_WAIT_SECONDS`
  (default 900); declares the `gpu-lease` volume.
- `docker/worker.Dockerfile`: creates `/gpu-lease` owned by 10001 (both variants; empty) so the
  shared volume gets that ownership whichever image mounts it first. The existing
  `stemstudio/worker:local-gpu` must be rebuilt (`up --build` does it) before the lease volume is
  first created, otherwise it starts root-owned.

### Scripts

- `scripts/cpu_image_guard.py` now has profiles: `worker` (unchanged rules) and `generator`
  (NVIDIA/CUDA rules + no torch/torchaudio/torchvision/ace-step/nano-vllm/transformers/diffusers;
  `stemstudio_generator.config`/`.schemas`, fastapi, uvicorn, soundfile, pynvml must import).
  `scripts/check-cpu-image.{sh,ps1}` gained `--generator` / `-Generator` (default image
  `stemstudio/generator:local-fake`, `--build` builds the fake variant).
- `scripts/generative-smoke.{sh,ps1}` + `scripts/generative_smoke_checks.py` (009 Q3, 010):
  stack mode (default; `up -d --build --wait` of base + gpu + generative unless `--no-up`, then
  in `generator` and `singer`: torch `2.10.0+cu128`, `sm_<cc>` in the arch list, bf16 matmul,
  `/health` `ready` with kind `restyle` / `voiceConversion`, one 10 s range restyle and one 10 s
  voice conversion on synthetic WAVs, takes checked as FLAC and deleted; `--skip-jobs`,
  `--project`); image mode (`--images`: throw-away `--gpus all` containers only, runtime +
  engine imports + weights `REVISION`/`SHA256SUMS`/listed files in the model volume mounted
  read-only).
- `scripts/image-licences.{sh,ps1}` + `scripts/image_licences.py`: Markdown licence table per
  image (all Python distributions, from package metadata, in a throw-away container without
  network) - for the GPU images that CI never builds (CTO 009 T083 / 010 T061).
- `scripts/test-all.{ps1,sh}`: generator steps (uv sync, ruff check, ruff format, mypy
  `src tests`, pytest with the 90 % gate) and Redocly lint of the 009 and 010 generator + backend
  documents.

### CI (`.github/workflows/ci.yml`)

- New job `generator`: `uv lock --check`, `uv sync --frozen` (base + dev only), ruff, ruff format,
  mypy, pytest `--cov-fail-under=90`, coverage artifact. `e2e` now also needs it.
- `contracts`: lints `$GENERATIVE_CONTRACTS` (009 + 010 generator and backend documents).
- New job `generator-image`: `docker compose config -q` of all four combinations, builds the fake
  image and runs the guard (profile generator), asserts the image user.
- `licences`: Python generator base install (pip-licenses 5.5.5), uv cache on both locks.
- The default images and the `cpu-image-guard` job are unchanged in behaviour.

## Files changed

- new: `docker/generator.Dockerfile`, `docker-compose.generative.yml`,
  `docker-compose.generative-fake.yml`, `scripts/generative-smoke.{sh,ps1}`,
  `scripts/generative_smoke_checks.py`, `scripts/image-licences.{sh,ps1}`,
  `scripts/image_licences.py`, this hand-off
- changed: `docker/worker.Dockerfile`, `docker-compose.gpu.yml`, `scripts/cpu_image_guard.py`,
  `scripts/check-cpu-image.{sh,ps1}`, `scripts/test-all.{ps1,sh}`, `.github/workflows/ci.yml`

## Tests and coverage

GPU check first: `nvidia-smi` -> RTX 5070 Ti, 2,266-2,450 MiB of 16,303 MiB used, driver 610.88.
All images built under separate tags (`stemstudio/generator:devops-{fake,acestep}`,
`stemstudio/singer:devops-soulx`, `stemstudio/worker:devops-cpu`); no `stemstudio` compose
container was touched (a running `stemstudio-preseed-tmp` container belonged to someone else and
was left alone).

```text
generative-smoke.sh --images --no-build (throw-away containers, --gpus all, stemstudio_model-cache:ro)
  generator  OK torch 2.10.0+cu128 | cuda available | sm_120 in [sm_70 ... sm_100, sm_120] | 2048x2048 bf16 matmul
             OK import acestep.handler, acestep.inference, acestep.llm_inference (gradio absent)
             OK /models/acestep/checkpoints: REVISION and SHA256SUMS; 28 listed files present
  singer     OK torch 2.10.0+cu128 | cuda available | sm_120 | bf16 matmul
             OK PYTHONPATH contains /opt/soulx-singer; import soulxsinger.models.soulxsinger_svc
             OK load /opt/soulx-singer/preprocess/tools/f0_extraction.py (RMVPE class present)
             OK /models/soulx: 2 listed files; /models/soulx/whisper-base: 11 listed files present
check-cpu-image.sh --generator stemstudio/generator:devops-fake   OK (exit 0); same via .ps1 -Generator
check-cpu-image.sh --generator <acestep image>                     FAIL ace-step, cuda-bindings, ... (exit 1, negative test)
check-cpu-image.sh stemstudio/worker:devops-cpu (fresh CPU build)   OK worker profile (exit 0)
check-worker-fonts.sh stemstudio/worker:devops-cpu                  OK Leipzig, verovio/cairosvg/music21
model-cache-owner on a throw-away root-seeded volume                 handed acestep, soulx, hf to 10001; 2nd run no-op; writable as 10001
docker compose config -q: default | +gpu | +gpu+generative | +generative+generative-fake   all valid
generator (uv run --frozen): ruff check, ruff format --check, mypy src tests   clean
generator pytest --cov --cov-branch --cov-fail-under=90   73 passed; 468 stmts / 26 branches, 100 % line, 100 % branch
```

`/health ready` and the two 10 s jobs (stack mode) could **not** run yet: `main.py`
(`create_app`) is generative's 009:T033; the stack-mode script is written against the contract
and is for the CTO's reference-machine run (009 Q3 / 010 V3).

`scripts/test-all.ps1 -ContinueOnError`: **41 of 42 steps passed**; the new steps `generator *`
(5) and `contracts lint (009-style-transfer)` / `(010-lyrics-to-song)` passed. The one red step
is pre-existing and outside this lane:

```text
dotnet test + coverage gate   FAILED
  Domain 555 passed | Persistence 77 passed | Web 798 passed | Api 988 passed, 2 failed (990)
  GenerationContractTests.NewerContracts_ContainTheOlderOnesUnchangedExceptTheNamedEntries
    (components/responses): LyricsConflict, ScoreConflict outside the allowed set
    (paths): /api/tracks/{trackId}/lyrics/search, .../lyrics/align, .../align/cancel, /api/assets/scores
```

No file under `specs/`, `src/` or `tests/` is changed in this branch (`git diff HEAD -- specs
tests src` is empty): the 6accc47 merge brought be286b4 ("reconcile 005-008 contracts") into the
008 backend document, so it no longer matches the 009 copy (1.8.0) outside the named entries.

## Quality checks

- actionlint 1.7.12 (Docker image, with shellcheck on `run:` blocks): 0 errors (ci.yml, e2e-real.yml).
- shellcheck v0.11.0 on all ten `scripts/*.sh`: exit 0.
- PSScriptAnalyzer on `scripts/`: 0 errors, 0 warnings (6 pre-existing Information in `dev.ps1`).
- ruff check + ruff format (generator config) on the four `scripts/*.py`: clean;
  `sig_check` on `cpu_image_guard.py` + `generative_smoke_checks.py`: 30 units, 0 errors, 0 warnings.
- `docker compose config -q`: all four combinations valid.

## Dependencies added

No new Python/NuGet/npm dependency. Build/runtime inputs (all already pinned by Phase 0):
`python:3.12-slim`, `ghcr.io/astral-sh/uv:0.13.5`, Debian `ffmpeg`, `libsndfile1`, `git`
(build stage only), SoulX-Singer source @ `81aeb3ae772c70093c3de74dc23c92d983801ae4`
(Apache-2.0, https://github.com/Soul-AILab/SoulX-Singer).

Licence report of the images (`scripts/image-licences.sh`, for THIRD-PARTY-LICENSES):

| Image | Distributions | Notes |
|---|---|---|
| fake | 21 | all MIT / BSD / Apache-2.0 / PSF; no NVIDIA |
| generator (acestep) | 125 | 22x "NVIDIA Proprietary Software" / `LicenseRef-NVIDIA-*` (CUDA 12.8 wheels incl. cudnn, nvshmem, cusparselt, cufile; `cuda-bindings` 12.9.4) = ADR-011 system dependency; `torchao` 0.16.0 has no licence metadata (BSD-3-Clause upstream); `frozendict` 2.5.1 LGPL-3.0-only; `pytorch-wavelets` 1.3.0 "Free To Use" (MIT upstream - CTO to confirm) |
| singer (soulx) | 82 | NVIDIA wheels as above; `soxr` 1.1.0 LGPL-2.1-or-later (librosa); `certifi` MPL-2.0 |

## Open issues / requests

- **architect / CTO**: the pre-existing red `GenerationContractTests` subset test above (008 backend
  document after be286b4 vs. the 009 copy): either re-derive the 009/010 backend documents from the
  reconciled 008 document or widen the named entries.
- **generative**: the image starts `uvicorn --factory stemstudio_generator.main:create_app` on
  0.0.0.0:8010; please provide `create_app()` in `main.py` (the `stemstudio-generator =
  main:run` script entry stays valid). `/health` must answer 200 for the healthcheck. ffmpeg and
  libsndfile are in every variant; no C compiler (if a triton JIT path is ever enabled, it needs
  gcc). In the image, `HF_HOME=/models/hf`; whisper-base must still load from
  `/models/soulx/whisper-base` (the coordinator's check: `REVISION` + `SHA256SUMS` present ->
  `present: true`, no download). Use `sys.path`-independent RMVPE loading by file path from
  `/opt/soulx-singer/preprocess/tools/f0_extraction.py`.
- **stem-separation**: compose now passes `STEMSTUDIO_GPU_LEASE_FILE` / `_WAIT_SECONDS` to the GPU
  worker; the lease implementation (009:T022/T023) is yours.
- **qa / CTO**: fake stack for `@generative`:
  `docker compose -f docker-compose.yml -f docker-compose.generative.yml -f docker-compose.generative-fake.yml up -d --build --wait`
  (with `STEMSTUDIO_SEPARATOR=fake`); the CI `e2e` job does not start it yet (CTO integration
  wave 009:T081 / 010:T059, once `main.py` exists).
- **CTO**: run `scripts/generative-smoke.ps1` (stack mode) on the reference machine after the
  generative merge; record the image sizes (above) in STATUS.
- An invalid `GENERATOR_VARIANT` fails at `FROM variant-<x>` with a "pull access denied" message
  (BuildKit resolves the stage name as an image); documented in the Dockerfile header.
- Build cache: the GPU builds added BuildKit cache entries; `docker builder prune` was not run
  (shared cache). All test images were removed.

## Next owner

CTO (integration wave 009:T081-T084 / 010:T059-T062); generative for `main.py` (`create_app`).
