# Hand-off: devops / 006 foundational (006:T011)

Combined 006 + 008 devops lane (architect-006-008-phase0.md): 006:T011 -> 008:T009 -> 008:T010,
one commit on top of the Phase 0 skeleton (`62d107e`). The 008 part (font, transcriber env, font
check in CI) is in `devops-008-foundational.md`; the validation evidence below covers both.

## What was built

- **`docker/worker.Dockerfile`**
  - CPU stage unchanged in mechanism: `uv sync --frozen` installs exactly `worker/uv.lock`
    (torch 2.15.0+cpu, **torchaudio 2.11.1+cpu**, stable-ts 2.19.1, openai-whisper 20250625;
    `triton` and `matplotlib` excluded by the lock's uv overrides). Verified in the built image.
  - GPU stage (`TORCH_VARIANT=cu132`): reinstalls **torch 2.15.0 and torchaudio 2.11.1** together
    from `https://download.pytorch.org/whl/${TORCH_VARIANT}` (`--reinstall-package` for both), so
    the GPU image has `torchaudio 2.11.1+cu132` (Phase 0 P0-1).
  - Runtime creates `/models/whisper` (owned by the worker user) on the model-cache volume:
    Whisper turbo downloads to `STEMSTUDIO_MODEL_DIR/whisper` at first alignment; no weights in
    the image. Header comment documents 006/008 contents.
- **`docker-compose.yml`** worker env: `STEMSTUDIO_ALIGN_MODEL` (`turbo`), `STEMSTUDIO_ALIGN_AUTO`
  (`gpu`), `STEMSTUDIO_FAKE_LYRICS` (`word`), defaults = worker defaults; header comment documents
  them. `docker-compose.gpu.yml`: comment only (torchaudio +cu132).
- **CPU-image guard** (`scripts/cpu_image_guard.py`, wrappers `check-cpu-image.{sh,ps1}`): also
  fails on `matplotlib` and when `torchaudio` or `stable_whisper` do not import (`triton` was
  already forbidden). Help text line range in the `.sh` wrapper adjusted.
- **GPU smoke** (`scripts/gpu_smoke_checks.py` mode `runtime`): new `check_torchaudio()` -
  torchaudio is a `+cu132` build and resamples on cuda.
- **Contracts**: CI `CONTRACTS_DIR` and `scripts/test-all.{ps1,sh}` -> `specs/008-sheet-music/contracts`
  (covers 006 and 007); the render-vectors check keeps
  `specs/007-export-library/contracts/generate_render_vectors.py` (new CI env `RENDER_VECTORS`,
  `$RenderVectors` / `RENDER_VECTORS` in the scripts).
- **`scripts/dev.{ps1,sh}`**: comment only - 006/008 variables pass through from the shell.

## Files changed

`docker/worker.Dockerfile`, `docker-compose.yml`, `docker-compose.gpu.yml`,
`.github/workflows/ci.yml`, `scripts/{cpu_image_guard.py, check-cpu-image.sh, check-cpu-image.ps1,
gpu_smoke_checks.py, test-all.ps1, test-all.sh, dev.ps1, dev.sh}`,
`specs/006-synced-lyrics-karaoke/tasks.md` (T011 ticked), this note.

## Tests and coverage

devops owns no coverage-gated component; the container-side checks are exercised end to end.
Images were built with separate tags (`stemstudio/worker:devops-0068-{cpu,gpu}`) by plain
`docker build`; throwaway `docker run --rm` containers only; no `stemstudio` compose project was
touched.

```text
check-cpu-image.sh stemstudio/worker:local (old image, test-first)
  FAIL module torchaudio does not import: ModuleNotFoundError ...      exit 1
  FAIL module stable_whisper does not import: ModuleNotFoundError ...
check-cpu-image.{sh,ps1} stemstudio/worker:devops-0068-cpu
  OK   CPU-only image: no NVIDIA/CUDA distributions, CPU torch, no matplotlib; torchaudio, stable_whisper import   exit 0
import check (both images, --network none, worker user)
  CPU: torch 2.15.0+cpu (cuda None), torchaudio 2.11.1+cpu
  GPU: torch 2.15.0+cu132 (cuda 13.2), torchaudio 2.11.1+cu132
  both: stable-ts 2.19.1, openai-whisper 20250625, music21 10.5.2, verovio 6.4.0, cairosvg 2.9.1,
        pypdf 6.21.4; fonttools absent, triton absent, matplotlib absent; cairosvg.svg2pdf OK
gpu_smoke_checks.py runtime (--gpus all, RTX 5070 Ti)        8 x OK incl.
  OK   torchaudio 2.11.1+cu132 / OK torchaudio resample on cuda              exit 0
```

`scripts/test-all.ps1 -ContinueOnError`: all 35 steps passed, exit 0 (contracts lint now on the
008 documents, render vectors on the 007 generator); numbers in `devops-008-foundational.md`.

## Quality checks

- `docker compose config -q`: default and `-f docker-compose.yml -f docker-compose.gpu.yml` exit 0.
- actionlint 1.7.12 (Docker): "Found 0 errors in 2 files".
- shellcheck v0.11.0 on all eight `scripts/*.sh`: exit 0.
- PSScriptAnalyzer 1.25.0 on `scripts/`: 0 errors, 0 warnings, 6 Information (pre-existing).
- `scripts/*.py`: `ruff check` / `ruff format --check` with `--config worker/pyproject.toml` clean;
  `mypy --strict --config-file worker/pyproject.toml` clean (3 files).

## Dependencies added

None by devops for 006 (torchaudio `+cu132` is the GPU wheel of the architect's torchaudio 2.11.1,
BSD-2-Clause, https://download.pytorch.org/whl/cu132).

## Open issues / requests

- **stem-separation (006 alignment)**: pass `download_root=STEMSTUDIO_MODEL_DIR/whisper` to
  Whisper; `/models/whisper` exists and is writable by the worker user. Otherwise openai-whisper
  falls back to `~/.cache/whisper` (not on the volume -> 1.6 GB re-download per container).
- **stem-separation**: the real `/health.lyricsAlignment` states then change the CI e2e health
  smoke only via the worker; no devops change expected.
- Note: the GPU image has **no triton** either (the lock's override also applies to the GPU
  reinstall); Phase 0 showed triton is never imported, so this is fine.

## Next owner

devops 008:T009/T010 (same commit), then 006:T062 at HO:polish; CTO for the integration wave.
