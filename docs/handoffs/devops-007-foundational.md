# Hand-off: devops / 007 foundational (007:T008, 007:T009; includes 005:T008)

Combined 005 + 007 devops lane (architect-005-007-phase0.md): 005:T008 -> 007:T008 -> 007:T009,
one commit `e5b8a17`. The 005 part is summarised in `devops-005-foundational.md`.

## What was built

- **`docker-compose.yml`** (worker):
  - `api-data:/library:ro` and `STEMSTUDIO_LIBRARY_DIR: /library` (comment: read-only, the API is
    the only writer; ADR-016 D5, FR-012).
  - Render lane setting `STEMSTUDIO_RENDER_BLOCK_SECONDS: ${STEMSTUDIO_RENDER_BLOCK_SECONDS:-10}`
    (worker default 10).
  - 005 variables (pass-through, worker defaults): `STEMSTUDIO_ONLINE_LOOKUPS` (`on`),
    `STEMSTUDIO_CONTACT_URL` (empty -> generic User-Agent token; no value baked in),
    `STEMSTUDIO_WIKIPEDIA_LANGUAGES` (`en`), `STEMSTUDIO_FAKE_ONLINE` (`found`).
  - Header comment documents all of them. The GPU override needs no change (it only replaces
    build args, image, `STEMSTUDIO_DEVICE` and the device reservation; env/volumes merge).
- **`scripts/dev.{ps1,sh}`**: `STEMSTUDIO_LIBRARY_DIR` = `<repo>/data/api` (the API data dir) in
  a new helper (`Initialize-LibraryEnvironment` / `init_library_environment`); the 005 variables
  are passed through from the shell unchanged (not set), the start-up line prints the library
  dir and the lookups state.
- **`scripts/check-library-mount.{sh,ps1}`** + **`scripts/library_mount_checks.py`** (007:T009):
  the wrappers pipe the Python file into `docker compose [-p ..] [-f ..] exec -T worker python -I -`
  (same pattern as `check-cpu-image.*`). Three checks, one line each, exit 1 when any fails:
  `STEMSTUDIO_LIBRARY_DIR == /library`; `/health` `library.readable == true`; writing
  `/library/.stemstudio-write-probe` as the worker user fails with `EROFS` ("Read-only file system").
- **CI** (`.github/workflows/ci.yml`):
  - `CONTRACTS_DIR: specs/007-export-library/contracts` (done once; covers 005).
  - `contracts` job: new step `python3 "$CONTRACTS_DIR/generate_render_vectors.py" --check`.
  - `cpu-image-guard` job: new step fails when `stemstudio/worker:ci-cpu` sets
    `STEMSTUDIO_CONTACT_URL` in its image config (005 FR-010).
  - `e2e` job: `scripts/check-library-mount.sh` right after the health smoke test.
- **`scripts/test-all.{ps1,sh}`**: contracts -> 007 documents; new step `render vectors check`
  (`uv run --project worker --frozen python .../generate_render_vectors.py --check`).

## Test-first evidence (007:T009)

Test stack: separate project `stemstudio-devops007`, separate image tags
`stemstudio/{worker,api,web}:devops007-test`, host ports removed with a scratch override
(`ports: !reset []`), `STEMSTUDIO_SEPARATOR=fake`. The running `stemstudio` project was not touched.

1. Compose file of `d2e06a7` (before the change), `scripts/check-library-mount.sh` -> exit 1:

   ```text
   FAIL worker STEMSTUDIO_LIBRARY_DIR: STEMSTUDIO_LIBRARY_DIR is None, expected '/library'
   FAIL worker /health library.readable: /health library is {'readable': False, 'reason': 'The library is not available to the worker'}, expected readable: true
   FAIL worker cannot write /library: write to /library failed with 'No such file or directory', expected 'Read-only file system'
   ```

2. `down -v` (fresh volumes), new compose file, same check (`.sh` and `.ps1`, identical output) -> exit 1:

   ```text
   ok   worker STEMSTUDIO_LIBRARY_DIR
   FAIL worker /health library.readable: /health library is {'readable': False, 'reason': 'The library is not available to the worker'}, expected readable: true
   ok   worker cannot write /library
   ```

   The remaining failure is the placeholder `LIBRARY_NOT_CHECKED` in `/health`; it turns green
   when stem-separation wires `LibraryReader.status()` into `/health` (007:T021-T024, see the
   architect note). Proof that the mount itself is readable for uid 10001:
   `os.access('/library', R_OK|X_OK) -> True`, listing `stemstudio.db, -shm, -wal`, first bytes
   `b'SQLite format 3\x00'`.
3. Fresh-volume ownership: the worker starts first and mounts the empty `api-data` volume
   read-only; the API (uid 1654) still owns and writes `/data` (`drwxr-xr-x app app`,
   `stemstudio.db` created), so no Dockerfile change was needed (and none must create
   `/library` in the worker image: a root-owned mount point could be copied onto a fresh volume).

007:T009 is **left unticked** because its "make it pass" needs the stem-separation `/health`
change; tick it at integration when the CI `e2e` step is green.

## Files changed

- `docker-compose.yml`, `.github/workflows/ci.yml`, `scripts/dev.ps1`, `scripts/dev.sh`,
  `scripts/test-all.ps1`, `scripts/test-all.sh`
- new: `scripts/check-library-mount.ps1`, `scripts/check-library-mount.sh` (mode 100755),
  `scripts/library_mount_checks.py`
- `specs/005-artwork-band-info/tasks.md` (T008, T056), `specs/007-export-library/tasks.md` (T008, T069)

## Tests and coverage

`scripts/test-all.ps1 -ContinueOnError` (35 steps): 34 passed, 1 failed (`quality gate (run_all)`,
pre-existing, see Open issues):

```text
StemStudio.Domain.Tests       261 passed   Domain        98.54 % line  97.67 % branch
StemStudio.Persistence.Tests   48 passed   Persistence   98.56 %       100 %
StemStudio.Web.Tests          332 passed   Web           99.88 %       95.12 %
StemStudio.Api.Tests          473 passed   Api 99.63 % / 98.02 %, Contracts 100 % / 100 %
worker pytest                 874 passed, 3 deselected; TOTAL 3866 stmts 0 missed, 408 branches 1 partial -> 99.98 %
audio engine                  160 passed; web-interop 11 passed
tools/quality pytest          253 passed, 100 %; fixtures 86 passed, 100 %
contracts lint (007 docs)     "Your API descriptions are valid"
render vectors check          passed
e2e list                      Total: 275 tests in 23 files
quality gate                  640 files, 4808 units, 29 clones, 1 exemption, 1 error, 161 warnings -> exit 1
```

devops owns no coverage-gated component; the new Python check is container-side tooling (like
`cpu_image_guard.py`) and has no unit tests; it is exercised end-to-end above.

## Quality checks

- `docker compose config -q`: default and `-f docker-compose.yml -f docker-compose.gpu.yml` exit 0.
- actionlint 1.7.12 (Docker, shellcheck on `run:` blocks): "Found 0 errors in 2 files".
- shellcheck v0.11.0 on all seven `scripts/*.sh`: exit 0.
- PSScriptAnalyzer 1.25.0 on `scripts/`: 0 errors, 0 warnings, 6 Information (pre-existing).
- `scripts/library_mount_checks.py`: ruff check + format (worker config) clean, `mypy --strict` clean;
  `run_all.py` reports nothing for it.
- Contact-URL CI step tested both ways: `devops007-test` image -> ok; a throw-away image with
  `ENV STEMSTUDIO_CONTACT_URL=https://example.invalid` -> fails (image removed).
- Images: built with the `devops007-test` tags through compose (Dockerfiles unchanged), stack
  healthy; test images and volumes removed afterwards.

## Dependencies added

None.

## Open issues / requests

- **quality gate (qa / quality-gate)**: `run_all.py` has 1 error on the base commit, not from this
  lane: `e2e/support/preferences.ts:12 [duplication] 16 duplicated lines, also in
  src/web-interop/src/types.ts:8` (came with the 004 qa merge, `14651f4`; architect Phase 0 ran
  before that merge). CI job `quality` will be red until it is resolved.
- **stem-separation**: `/health` `library.readable` from `LibraryReader.status()` makes the CI
  `e2e` step `check-library-mount.sh` (and 007:T009) green. Until then that step fails on this branch.
- **CTO**: README env table (005/007 variables, `/library` mount) per plan; expect small merges
  with the 004 devops changes in `ci.yml`/`scripts/test-all.*`.
- The render-vector generator is not ruff-linted in CI (architect: 3 known findings, not in a lint
  gate); add it once those are fixed.
- `scripts/dev.*` were not started end to end: ports 8080/8081 are held by the running `stemstudio`
  project; changes are syntax-checked (`bash -n`, shellcheck, PSScriptAnalyzer).

## Next owner

stem-separation (library health), CTO (integration wave).
