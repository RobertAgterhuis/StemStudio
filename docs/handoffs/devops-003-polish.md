# Hand-off: devops / 003 polish (T049)

## What was built
Final validation of all devops changes for 003. Code commits: T008 `a444800`, T009 `46f68c6` (red)
-> `9fbadb2` (green), T039 `8c0771d`. Hand-offs: `devops-003-foundational.md` and
`devops-003-us2.md`.

## Files changed
`specs/003-gpu-acceleration/tasks.md` (T049 ticked) and this note.

## Tests and coverage
devops owns no unit-tested component. Validation on the final tree:

| Check | Result |
|---|---|
| `docker compose config -q` | exit 0 |
| `docker compose -f docker-compose.yml -f docker-compose.gpu.yml config -q` | exit 0 (worker args cu132/gpu/auto, image `local-gpu`, `STEMSTUDIO_DEVICE=auto`, GPU reservation) |
| `docker compose -p stemstudio-e2e config` with `STEMSTUDIO_FAKE_DEVICE=gpu-oom` | passes through to the worker |
| actionlint 1.7.12 (ci.yml, e2e-real.yml) | clean |
| shellcheck 0.11.0 (`scripts/*.sh`) | clean |
| PSScriptAnalyzer (Warning/Error, `scripts/`) | 0 |
| ruff check, ruff format, mypy --strict (`scripts/*.py`) | clean |
| `tools/quality/sig_check.py scripts` | 18 units, 0 errors, 0 warnings |
| CPU image build (separate tag) | OK, **3.36 GB**; guard green |
| GPU image build (separate tag) | OK, **11.5 GB**; guard red (21 distributions + CUDA torch) |
| GPU runtime smoke (`--gpus all`, RTX 5070 Ti) | torch 2.15.0+cu132, sm_120, matmul, onnxruntime CUDAExecutionProvider active after `preload_dlls()`: all OK |
| GPU smoke `/health` part | pending the stem-separation start-up probe; the Phase 0 placeholder returns `kind=cpu`, `reason=null` |

Cleanup: both test images (`stemstudio/worker:devops-cpu-check`, `:devops-gpu-check`) and every
throwaway container are removed. The running `stemstudio` stack was never stopped or recreated;
its images were not retagged. I did not prune the shared build cache or uv cache.

## Quality checks
See the table.

## Dependencies added
None beyond the GPU-variant-only packages listed in `devops-003-foundational.md`.

## Open issues / requests
- CTO T052: run `scripts/gpu-smoke.ps1` after the stem-separation merge (the `/health` gpu/cpu
  checks need the probe), and the `--gpu-e2e` / `-Gpu` passes, or rely on CI.
- qa: `test:gpu` script and `@gpu` excluded from `npm test` (see `devops-003-us2.md`).

## Next owner
CTO (merge, T052).
