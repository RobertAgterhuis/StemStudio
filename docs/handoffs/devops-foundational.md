# Hand-off: devops / foundational (T011-T018)

## What was built

- **Images** (build context = repo root, all non-root, all with `HEALTHCHECK` on `/health`):
  - `docker/worker.Dockerfile`: python:3.12-slim, uv 0.13.5 `sync --frozen --no-dev` (torch 2.15.0+cpu
    from the lock) in a build stage, runtime with apt `ffmpeg` (ffmpeg + ffprobe) and `libsndfile1`,
    **deno 2.9.8** (yt-dlp JS runtime, from `denoland/deno:bin-2.9.8`), user `stemstudio` (uid 10001),
    writable `HOME=/home/stemstudio` (spotDL `~/.spotdl`, yt-dlp/deno caches), `STEMSTUDIO_DATA_DIR=/data`,
    `HF_HOME=/models/hf`, `TORCH_HOME=/models/torch`. Optional `TORCH_VARIANT` build arg (e.g. `cu128`)
    swaps in the CUDA wheel of the same torch version (GPU override only).
  - `docker/api.Dockerfile` / `docker/web.Dockerfile`: sdk:10.0 (10.0.404, satisfies global.json
    10.0.401 latestFeature) restore -> publish, aspnet:10.0 runtime as `app` (uid 1654), ports
    8081 / 8080. The web image builds `src/audio-engine` (`npm ci && npm run build`, node:22-slim)
    before publishing; `/js/audio-engine.js` is served (verified HTTP 200).
- `docker-compose.yml`: `worker` (internal only, `expose: 8000`), `api` (8081), `web` (8080); volumes
  `worker-data`, `api-data`, `model-cache` (`/models`), `web-keys` (data-protection key ring so a
  restart does not break open circuits / antiforgery); `depends_on: condition: service_healthy`
  worker -> api -> web; `STEMSTUDIO_SEPARATOR=${STEMSTUDIO_SEPARATOR:-demucs}`,
  `STEMSTUDIO_DEVICE=${STEMSTUDIO_DEVICE:-cpu}`; all `StemStudio__*` URLs per T014; no proxy.
- `docker-compose.gpu.yml` (NVIDIA override, best effort, not built here) and
  `docker/compose.model-cache-bind.yml` (binds `model-cache` to `$STEMSTUDIO_MODEL_CACHE_DIR`, used by e2e-real).
- `.dockerignore` (excludes .git, docs, specs, tests, bin/obj, node_modules, .venv, data, stale bundle).
- `scripts/dev.ps1` / `scripts/dev.sh`: build engine, `dotnet build` once, then worker (`uv run uvicorn`),
  api and web (`dotnet run --no-build --launch-profile http`); `-Fake`/`--fake`, `-SkipEngineBuild`,
  `-SmokeTest`/`--smoke-test` (waits for the three `/health`, then stops), Ctrl+C stops the process trees.
  Data under `<repo>/data/{worker,api}`.
- `scripts/test-all.ps1` / `scripts/test-all.sh`: every component's lint + test + coverage gate;
  components not present yet (tools/quality, tests/fixtures, e2e) are reported as `skipped`.
- `.github/workflows/ci.yml` (push/PR/dispatch, `permissions: contents: read`, actions pinned by SHA):
  `dotnet` (tool restore, build, format, test with the Directory.Build.props gate, coverage summary),
  `sonar` (only when secret `SONAR_TOKEN` exists; `vars.SONAR_HOST_URL`; `dotnet sonarscanner` from
  `dotnet tool restore`; token via `/d:sonar.token="$SONAR_TOKEN"`), `worker` (lock check, ruff,
  format, mypy, pytest `--cov-branch --cov-fail-under=90`, plus fixture-generator lint/type/tests),
  `audio-engine`, `contracts` (Redocly 2.78.0), `quality` (tools/quality lint/type/tests >= 90 %, then
  `run_all.py --report` as artifact), `e2e-lint`, `licences` (nuget-license 4.0.18, pip-licenses 5.5.5,
  license-checker 25.0.1 -> artifact `licence-report`), `e2e` (needs all; fixtures, compose with
  `fake` `--wait`, health smoke, Playwright Chromium+Firefox, report artifact, `down -v`).
- `.github/workflows/e2e-real.yml` (dispatch + weekly Mon 03:00): `pytest -m integration` with
  `CI=true` and cached HF_HOME/TORCH_HOME; then compose with `demucs` + bound model cache,
  model pre-warm, `npm run test:real`.

## Files changed

`.dockerignore`, `docker/worker.Dockerfile`, `docker/api.Dockerfile`, `docker/web.Dockerfile`,
`docker/compose.model-cache-bind.yml`, `docker-compose.yml`, `docker-compose.gpu.yml`,
`scripts/dev.ps1`, `scripts/dev.sh`, `scripts/test-all.ps1`, `scripts/test-all.sh`,
`.github/workflows/ci.yml`, `.github/workflows/e2e-real.yml`, `specs/001-stemstudio-mvp/tasks.md`
(T011-T018 ticked), this note.

## Tests and coverage (validation evidence, T018)

devops has no measurable code (tasks.md coverage note). Real outputs:

```
docker compose config -q                                   -> OK
docker compose -f docker-compose.yml -f docker-compose.gpu.yml config -q    -> OK
STEMSTUDIO_MODEL_CACHE_DIR=/srv/models docker compose -f docker-compose.yml -f docker/compose.model-cache-bind.yml config -q -> OK
docker compose build    -> Image stemstudio/{worker,api,web}:local Built
STEMSTUDIO_SEPARATOR=fake docker compose up -d --wait -> worker/api/web Healthy
GET :8081/health -> {"status":"ok"} HTTP 200
GET :8080/health -> {"status":"ok"} HTTP 200
worker (in-container) /health -> {"status":"ok","separator":"fake","device":"cpu","contractVersion":"1.0.1"} HTTP 200
GET :8080/js/audio-engine.js -> 200;  :8000 not reachable from host (internal only, as intended)
users: worker uid=10001(stemstudio), api/web uid=1654(app)
worker image: deno 2.9.8, /usr/bin/ffmpeg, /usr/bin/ffprobe 7.1.5, ~/.spotdl writable, torch 2.15.0+cpu
actionlint 1.7.12 (incl. shellcheck): "Found 0 errors in 2 files", exit 0
shellcheck scripts/dev.sh scripts/test-all.sh -> exit 0; PSScriptAnalyzer 1.25.0 warnings/errors: 0
./scripts/dev.ps1 -Fake -SmokeTest -> 8000/8081/8080 /health -> 200, "Stopped worker, api and web.", exit 0
./scripts/test-all.ps1 and bash scripts/test-all.sh -> exit 0: 15 passed, 9 skipped (components not yet merged)
  .NET 10 tests passed: Domain 100/100, Persistence 100/100, Api 100/100 + Contracts 90/100, Web 100/100 (line/branch)
  worker 31 passed, 100 % (192 stmts, 2 branches); engine 2 passed, 100 %; Redocly valid (3 known warnings)
```

Image sizes (`docker image inspect .Size`): worker 3.25 GB (venv 1.7 GB, of which torch 809 MB),
api 413 MB, web 392 MB.

## Quality checks

actionlint, shellcheck, PSScriptAnalyzer clean (justified suppressions: Write-Host, ShouldProcess on
private helpers, script-scope parameters). Shell/PowerShell helpers are small single-purpose functions.

## Dependencies added

| Name | Version | Licence | URL |
|---|---|---|---|
| python (base image) | 3.12-slim (3.12.16, Debian 13) | PSF-2.0 (+ Debian packages) | https://hub.docker.com/_/python |
| ffmpeg, libsndfile1 (Debian apt) | ffmpeg 7.1.5 | LGPL-2.1+/GPL-2.0+ (Debian build), LGPL-2.1 | https://ffmpeg.org |
| deno | 2.9.8 | MIT | https://github.com/denoland/deno |
| uv (build stage only) | 0.13.5 | MIT/Apache-2.0 | https://github.com/astral-sh/uv |
| mcr.microsoft.com/dotnet/sdk, aspnet | 10.0 (SDK 10.0.404, Ubuntu 24.04) | MIT | https://github.com/dotnet/dotnet-docker |
| curl (apt, api/web runtime, health check) | Ubuntu 24.04 | curl (MIT-style) | https://curl.se |
| node (web build stage only) | 22-slim (22.23.5) | MIT | https://hub.docker.com/_/node |
| CI only: nuget-license | 4.0.18 | Apache-2.0 | https://github.com/sensslen/nuget-license |
| CI only: pip-licenses | 5.5.5 | MIT | https://github.com/raimon49/pip-licenses |
| CI only: license-checker | 25.0.1 | BSD-3-Clause | https://github.com/davglass/license-checker |
| CI only: actions/checkout v7.0.1, setup-dotnet v6.0.0, setup-node v7.1.0, setup-python v7.1.0, upload-artifact v7.0.2, cache v6.1.0; astral-sh/setup-uv v10.3.3 | SHA-pinned | MIT | https://github.com/actions |

setup-dotnet v6.1.0 was skipped (published 2026-11-03, under two weeks). FYI from CTO: lizard's
dependency pathspec is MPL-2.0 (accepted).

## Open issues / requests

1. **CTO/architect**: `.gitignore` lacks `coverage.xml` (worker pytest-cov XML); the CI writes it, local
   scripts no longer do. Please add it.
2. **quality-gate**: CI assumes `tools/quality/requirements.txt` + `tools/quality/pyproject.toml`
   (ruff/mypy/pytest/coverage config) and `run_all.py --report <path>`; tools are run from the repo
   root (`mypy --config-file tools/quality/pyproject.toml`, `--cov-config=tools/quality/pyproject.toml`).
   Confirm or tell me the exact commands.
3. **qa**: CI runs the fixture generator tests with the worker env from the repo root
   (`uv run --project worker pytest tests/fixtures/tests --cov=tests/fixtures`) and expects
   `e2e/package-lock.json`, scripts `lint`, `test`, `test:real`, and Playwright output in
   `e2e/playwright-report/` + `e2e/test-results/`.
4. The CI jobs for other owners' components are guarded by `hashFiles(...)` and emit a `::warning::`
   when the component is missing; after the integration merge the CTO may drop the guards so a
   missing component fails hard.
5. **CTO/architect**: `dotnet-sonarscanner` must be in `dotnet-tools.json`; the repository needs the
   `SONAR_TOKEN` secret and optionally the `SONAR_HOST_URL` variable (default sonarcloud.io). Python and
   TS coverage are not sent by the .NET scanner; `sonar-project.properties` (quality-gate) covers them
   only with a separate `sonar-scanner` run, which is not wired yet.
6. Data protection logs `No XML encryptor configured` in the web container (keys stored unencrypted in
   the `web-keys` volume). Acceptable for a local single-user app; frontend may set an app name.
7. The GPU override (`docker-compose.gpu.yml`) is validated with `config -q` only, not built or run.
8. First real separation downloads the Demucs weights into `model-cache`. Pre-warm:
   `docker compose run --rm worker python -c "from demucs.pretrained import get_model; get_model('htdemucs_6s'); get_model('htdemucs')"`.
9. The worker image is large (3.25 GB), mostly torch + librosa/numba/scipy; reduce only by dropping deps.

## Next owner

CTO: review and merge (order quality-gate, devops, ...). devops resumes at T117 (HO:polish) after the
integration wave.
