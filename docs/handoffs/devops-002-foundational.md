# Hand-off: devops / 002 foundational (T008)

## What was built
Runtime and delivery wiring for feature 002 (detail separation):

- **Compose** (`docker-compose.yml`): worker gets `STEMSTUDIO_MODEL_DIR=/models` (the existing
  `model-cache` volume, next to `hf/` and `torch/`) and
  `STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=${STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS:-1}`. Header comment
  documents the ~1.9 GB first detail download (DrumSep, KARA 2, Mega 53-stem), that weights never
  go into the image or repo, and the >= 20 GB Docker Desktop memory need (~16 GB for a 4-minute
  song).
- **Worker image** (`docker/worker.Dockerfile`): `ENV STEMSTUDIO_MODEL_DIR=/models` so the image
  works on its own too (`docker run`); header comment names the new runtimes. No other change
  was needed: onnxruntime 1.32.0 and rotary-embedding-torch 0.9.2 come in through the frozen
  `uv sync` of the lock from T002 (manylinux wheels, no extra apt packages).
- **Non-Docker path** (`scripts/dev.ps1`, `scripts/dev.sh`): export `STEMSTUDIO_MODEL_DIR`
  (default `<repo>/data/models`, git-ignored) and `STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS`
  (default `1`); existing values in the environment win. The start-up line prints the model dir.
- **Contracts**: CI `CONTRACTS_DIR` and `scripts/test-all.{ps1,sh}` now point at
  `specs/002-detail-separation/contracts`.
- **e2e-real** (`.github/workflows/e2e-real.yml`):
  - `STEMSTUDIO_MODEL_DIR=$GITHUB_WORKSPACE/.model-cache` in the integration job, so the host
    cache has the same layout as the container's `/models` (the e2e-real compose job binds
    `.model-cache` to `/models` via `docker/compose.model-cache-bind.yml`): weights downloaded by
    the integration test are reused by the compose stack.
  - Cache key bumped to `models-htdemucs_6s-htdemucs-detail-drumsep-kara2-mega53-v2` (old key held
    only Demucs weights; ~2.2 GB total, under the 10 GB repo cache limit).
  - New step: generate the detail fixtures (`tests/fixtures/generate_detail_fixtures.py`, T010).
  - The integration step is split: Demucs and the other marked tests (`--ignore=tests/detail`,
    step timeout 45 min, pytest `--timeout=2400` as before) and the detail integration test
    (`tests/detail`, step `timeout-minutes: 60`, pytest `--timeout=3600`). Job timeout 60 -> 110 min.

## Files changed
- `docker-compose.yml`
- `docker/worker.Dockerfile`
- `scripts/dev.ps1`, `scripts/dev.sh`, `scripts/test-all.ps1`, `scripts/test-all.sh`
- `.github/workflows/ci.yml`, `.github/workflows/e2e-real.yml`
- `specs/002-detail-separation/tasks.md` (T008 ticked)

## Tests and coverage
devops owns no unit-tested code (YAML, Dockerfiles, scripts); the coverage gate does not apply.
Verification run:

```
docker compose config -q                                                  -> exit 0
docker compose -f docker-compose.yml -f docker-compose.gpu.yml config -q  -> exit 0
STEMSTUDIO_MODEL_CACHE_DIR=/tmp/mc docker compose -f docker-compose.yml -f docker/compose.model-cache-bind.yml config -q -> exit 0
docker compose config (worker env): STEMSTUDIO_MODEL_DIR: /models, STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS: "1"
docker compose build worker -> "Image stemstudio/worker:local Built", exit 0 (3.36 GB on disk, 790 MB content)
docker run --rm --entrypoint python stemstudio/worker:local -c "import onnxruntime, rotary_embedding_torch, torch ..."
  onnxruntime 1.32.0 ['AzureExecutionProvider', 'CPUExecutionProvider']
  rotary-embedding-torch 0.9.2
  torch 2.15.0+cpu
  MODEL_DIR /models
npx @redocly/cli@2.78.0 lint (002 contracts) -> "Your API descriptions are valid", 3 warnings, exit 0
```

The running stack containers were not stopped or recreated (only the image was rebuilt/retagged).

## Quality checks
```
actionlint 1.7.12 (docker rhysd/actionlint:1.7.12, -verbose) ci.yml e2e-real.yml -> "Found 0 errors in 2 files", exit 0
shellcheck v0.11.0 (docker koalaman/shellcheck:v0.11.0) scripts/dev.sh scripts/test-all.sh -> exit 0
PSScriptAnalyzer 1.25.0 scripts/ -> 0 warnings, 0 errors (6 pre-existing Information-level
  PSAvoidUsingPositionalParameters on unchanged lines of dev.ps1)
```
No secrets: `SONAR_TOKEN` remains a `secrets.` reference only.

## Dependencies added
None by devops (onnxruntime / rotary-embedding-torch were added by the architect in T002 and are
listed in `docs/handoffs/architect-002-plan.md`).

## Open issues / requests
1. **qa (T010)**: e2e-real now runs `tests/fixtures/generate_detail_fixtures.py` before the
   integration tests; keep that path (as in quickstart.md). If the detail integration test builds
   its 10 s input itself, the step is harmless.
2. **stem-separation (T050)**: e2e-real runs the detail test as `pytest -m integration tests/detail`
   with `STEMSTUDIO_MODEL_DIR` set and `CI=true`; keep the test under `worker/tests/detail/`. A
   per-test `@pytest.mark.timeout` must stay <= 3600 s.
3. **Runner memory**: GitHub-hosted `ubuntu-24.04` runners have 16 GB RAM. The 10 s fixture of the
   integration test is fine; the @real Playwright flow on compose would run a real detail phase
   too if the flow triggers it, which may hit memory limits for long fixtures. Keep @real fixtures
   short, or move the job to a larger runner later.
4. **GPU override**: `docker-compose.gpu.yml` swaps only torch for the CUDA wheel; onnxruntime stays
   the CPU package (KARA 2 runs on CPU there). Acceptable for the CPU-first target; noted for the CTO.

## Next owner
CTO (HO:foundational review); devops continues with T087 (HO:polish).
