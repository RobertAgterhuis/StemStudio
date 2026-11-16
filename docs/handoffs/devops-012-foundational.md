# Hand-off: devops / 012 practice tempo, foundational (T010, T011)

Branch `012-lane-devops` (worktree `.claude/worktrees/012-devops`, from `012-practice-tempo` 010c3e6d).
Nothing pushed or merged. The owner's compose project `stemstudio` was not stopped, recreated,
rebuilt or inspected; no `stemstudio/*` image tag was rebuilt. All builds ran in
`docker run --rm --network none` containers or under the separate tags `stemstudio-devops012/*`
(removed afterwards); the one throwaway web container (`devops012-webcheck`, random host port on
127.0.0.1) was stopped and removed.

## What was built

### T010 `scripts/build-soundtouch.{sh,ps1}` (commit **60bff731**, the commit the audio-engine lane waits for)

- Reads `third_party/soundtouch/soundtouch.lock` exactly (plain `KEY=value`, CRLF-tolerant; every key
  the build uses is required, an invalid line fails). Bash 3.2 compatible (no associative arrays) for macOS.
- **Download + gate**: `curl --proto '=https' --tlsv1.2` into `third_party/soundtouch/upstream/`
  (skipped when the file is present), SHA-256 must equal `SOUNDTOUCH_SHA256`; a mismatching file is
  deleted and the build stops; a failed download leaves no partial file.
- **Compile**: fresh extraction into `third_party/soundtouch/build/src/` on every build, then **`em++`**
  with `EMCC_FLAGS` over `SOUNDTOUCH_SOURCES` + `shim/stemstudio_soundtouch.cpp`, include dirs
  `<archive>/include` and `third_party/soundtouch/include`, plus `-ffile-prefix-map=<source dir>=.`
  and `-ffile-prefix-map=<shim dir>=.`. Default: inside `EMSDK_IMAGE@EMSDK_DIGEST` with
  `--network none` (the container runs the same script with `--local`; on Linux/macOS as the
  caller's uid:gid). `--local` = the non-Docker path (em++ on PATH).
- **Module check** (node from emsdk): no imports, all 13 exports (`memory`, `_initialize`, ABI v1),
  `_initialize()` then `st_abi_version() == ABI_VERSION`.
- Output: `OUTPUT` of the lock (`src/StemStudio.Web/wwwroot/js/soundtouch/soundtouch.wasm`, git-ignored)
  or `--out FILE`; prints size and SHA-256.
- Modes: `--verify-reproducible` (two fresh containers, SHA-256 compared), `--fetch-only`,
  `--package DIR` (LGPL distribution folder, below), `--check-pins` (web image pin == lock),
  `--print-config`, `--lock`, `--shim-dir` (until T020 lands: point it at any shim), `--out`, `--help`.
  The ps1 twin has the same modes except `--package` (only the Linux image build needs it); its
  Docker mode runs the bash script in the container, so the compile is a single implementation.

### T010 web image (commit c5119c66)

- `docker/web.Dockerfile`: global `ARG EMSDK_IMAGE=emscripten/emsdk:6.0.13@sha256:fb7864e3...a68a`;
  stage `soundtouch`: `--fetch-only` (only step with network), then
  `RUN --network=none ... --local --out /out/js/soundtouch/soundtouch.wasm && ... --package /out/third-party/soundtouch`;
  the build stage copies `/out/` into `src/StemStudio.Web/wwwroot/` **before `dotnet publish`**
  (MapStaticAssets only serves files known at publish time).
- Distribution folder (LGPL-2.1 section 6, ADR-021 D7) at `/third-party/soundtouch/`: `COPYING.TXT`,
  `NOTICE.md`, `README.md`, `soundtouch.lock`, the exact verified `soundtouch-2.4.1.tar.gz`,
  `include/stemstudio_soundtouch.h`, `shim/**`, `scripts/build-soundtouch.{sh,ps1}`, `SHA256SUMS`,
  `index.html` (links + hashes). Together with the ABI and the lock flags this is the complete
  corresponding source and everything needed to relink/replace the module.
- `.dockerignore`: re-includes only `scripts/build-soundtouch.{sh,ps1}`; excludes
  `third_party/soundtouch/{upstream,build}`, `wwwroot/js/soundtouch`, `wwwroot/third-party`,
  `wwwroot/js/time-stretch-processor.js(.map)` (never copy a stale local build into the image).

### T011 CI and test-all (commit 176a5b77)

- New job **`soundtouch`**: `--check-pins`; `actions/cache` of `third_party/soundtouch/upstream`
  keyed by `hashFiles('third_party/soundtouch/soundtouch.lock')` (the script re-verifies the hash on
  every use); `--verify-reproducible`; module SHA-256 in the job summary; `npm ci` +
  `npm run test:wasm` with `SOUNDTOUCH_WASM_REQUIRED=1`; artifact `soundtouch-wasm`. `e2e` needs it.
- `contracts` job: Redocly lint of the 012 backend 1.11.0 document and `check_superset.py`
  (PyYAML 6.0.3 via `uv run --no-project --with`), env `PRACTICE_CONTRACTS`.
- `quality` job step renamed; it already runs all of `scripts/tests` (now incl. the soundtouch tests).
- `scripts/test-all.{sh,ps1}`: 012 contract lint + superset check, the pin check, and
  `engine test:wasm` only when `soundtouch.wasm` exists (otherwise a "skipped" line).

## Deviations (owner unavailable; decided for the best result)

1. **`CONTRACTS_DIR` stays at 008.** It feeds the worker + backend Redocly step and 012 has no worker
   document; the .NET contract tests read the 012 document through their csproj link (architect,
   Phase 0), not through an env var. The 012 document is linted by a separate step instead.
2. **"Build once per run, cache keyed by the lock"**: the *source archive* is cached by the lock hash;
   the module itself is built in every run (twice, compared), because a cached binary would never
   re-prove reproducibility. Downstream jobs use the job's artifact.
3. **CI requires `test:wasm`** (`SOUNDTOUCH_WASM_REQUIRED=1`, no `--if-present`): a missing suite or
   module is red in CI, never silently skipped. Locally `test-all` skips it when the module is not built.
4. **No `--package` in the ps1 twin**; the image build (Linux) is its only user.

## Files changed

- `scripts/build-soundtouch.sh` (new, +x), `scripts/build-soundtouch.ps1` (new)
- `scripts/tests/test_build_soundtouch.py` (new)
- `docker/web.Dockerfile`, `.dockerignore`
- `.github/workflows/ci.yml`, `scripts/test-all.sh`, `scripts/test-all.ps1`
- `specs/012-practice-tempo/tasks.md` (T010, T011 ticked; only the checkboxes + a note)

## Tests and coverage

Stand-in shim: until audio-engine T020 lands, all builds used a throw-away ABI v1 shim kept in the
session scratchpad (never committed; the real one replaces it; hashes below change then).

```text
bash scripts/build-soundtouch.sh --shim-dir <stand-in>                 module ok: no imports, ABI 1, 13 exports
                                                                      wrote .../soundtouch.wasm (30090 bytes) sha256=07c00b3a67ee0cf8d52e5e1a190b4e781be7438148a989e4a7fa4f3eb4bf819c
bash scripts/build-soundtouch.sh --verify-reproducible ...            reproducible: both builds sha256=07c00b3a...819c
pwsh scripts/build-soundtouch.ps1 -VerifyReproducible -ShimDir ...    reproducible: both builds sha256=07c00b3a...819c, exit 0
--local as uid 1001 in /tmp/w (other path, twice)                     07c00b3a...819c twice (path-independent: prefix maps work)
docker build -f docker/web.Dockerfile (temp context + stand-in shim)  exit 0; image /app/wwwroot/js/soundtouch/soundtouch.wasm = 07c00b3a...819c
docker build --no-cache --target soundtouch (second build)            07c00b3a...819c; SHA256SUMS a61e34c2... identical in both
served (throwaway container): js/soundtouch/soundtouch.wasm 200 application/wasm (br when accepted);
  every /third-party/soundtouch/* file 200, index.html text/html; directory URL /third-party/soundtouch/ -> 404 (see requests)
uv run --project tools/quality --frozen pytest -c scripts/pyproject.toml --cov --cov-branch --cov-config=scripts/pyproject.toml scripts/tests
                                                                      73 passed (20 build-soundtouch, 8 served-check, 45 key volume)
                                                                      key_volume_checks.py line 100 % branch 100 % (36/36); the shell
                                                                      scripts are black-box tested (no line coverage tool for bash/ps1)
mutation check: SHA-256 comparison disabled -> 2 failed, 18 passed (gate tests bite)
uv run --no-project --with pyyaml==6.0.3 python -I specs/012-practice-tempo/contracts/check_superset.py   superset: yes
npx @redocly/cli@2.78.0 lint (012 backend 1.11.0)                     valid, 9 warnings (inherited from 1.21.0)
docker compose config -q (default; default+gpu+generative)            ok
```

## Quality checks

```text
shellcheck v0.11.0 scripts/build-soundtouch.sh scripts/check-soundtouch-served.sh scripts/test-all.sh   exit 0
actionlint 1.7.12 -no-color (ci.yml, e2e-real.yml, pages.yml; bundled shellcheck on run blocks)     0 errors
PSScriptAnalyzer 1.25.0 (Warning, Error) build-soundtouch.ps1, check-soundtouch-served.ps1, test-all.ps1   0
ruff check / ruff format --check / mypy --strict (scripts/pyproject.toml) on scripts/tests          clean
scripts/build-soundtouch.sh --check-pins                                                            pins ok
```

SIG: Python test units small (largest test ~13 LOC); bash/ps1 functions mostly <= 15 lines (the
inline node ABI check is the longest at ~17 lines incl. the JS string); tools/quality does not scan shell.

## Dependencies added

| Name | Version | Licence | URL | Note |
|---|---|---|---|---|
| actions/download-artifact | v8.0.2 (`9000827c...`) | MIT | https://github.com/actions/download-artifact | CI only (T067 step in e2e) |
| PyYAML | 6.0.3 | MIT | https://pyyaml.org | CI/test-all only, ephemeral `uv --with` (already in worker/uv.lock) |
| Emscripten SDK image | 6.0.13 by digest | MIT / NCSA | https://hub.docker.com/r/emscripten/emsdk | already listed by the architect; build tool, not distributed |

No runtime dependency added. SoundTouch 2.4.1 (LGPL-2.1) is the architect's row.

## Docs impact

Technical manual, setup chapter (CTO): building the module - `scripts/build-soundtouch.sh` /
`pwsh scripts/build-soundtouch.ps1` (Docker, pinned emsdk 6.0.13 by digest, offline compile),
`--local` / `-Local` with an activated emsdk 6.0.13 (non-Docker path), `--verify-reproducible`;
the web image builds it automatically; `/third-party/soundtouch/index.html` lists the LGPL source
package; replace the module by mounting a file over `/app/wwwroot/js/soundtouch/soundtouch.wasm`
(README "Replace the module"). No user-visible change.

## Open issues / requests

- **audio-engine (T019/T020)**: build with `scripts/build-soundtouch.sh` (or `--shim-dir` while
  iterating); the shim goes to `third_party/soundtouch/shim/stemstudio_soundtouch.cpp` - until it
  exists the web image build and the CI job `soundtouch` fail with "shim not found ... (012 T020)"
  (intended). `npm run test:wasm` must **fail instead of skip when `SOUNDTOUCH_WASM_REQUIRED=1`**
  (CI sets it); the module path is the lock's `OUTPUT`. Anything else in `shim/` (e.g. patch files)
  is shipped in `/third-party/soundtouch/shim/`.
- **frontend (`Program.cs`)**: `/third-party/soundtouch/` (directory URL) is 404 under
  `MapStaticAssets`; `/third-party/soundtouch/index.html` works. Please link the about/licence UI to
  `index.html`, or map the directory URL to it. Optional: `.tar.gz` is served as `application/x-tar`
  without `Content-Encoding` (bytes are correct).
- **architect**: NOTICE.md says the app "serves it at `/third-party/soundtouch/`" - suggest
  `/third-party/soundtouch/index.html`; NOTICE lists "the build script `build-soundtouch.sh`" - both
  scripts ship under `scripts/` in the folder; README "Rebuild" could mention `--verify-reproducible` and `--local`.
- **quality-gate (T009)**: in addition to the architect's list, exclude `third_party/soundtouch/build/**`
  and `src/StemStudio.Web/wwwroot/third-party/**` (local build/package outputs) from Sonar/SIG.
- **CTO**: the CI `e2e` job now `needs: soundtouch`; partial merges without the shim and `test:wasm`
  are red by design. Rows above for THIRD-PARTY-LICENSES.

## Next owner

audio-engine (T019/T020 on this script), then the CTO in the integration wave.
