# Hand-off: devops / 013 foundational (T011)

Branch `013-lane-worker`. Owner's compose project `stemstudio`, its images and volumes were not
touched: no stack was started and no image built (only `docker compose config`).

## What was built

- **`docker-compose.yml`** (api): bind mount `${STEMSTUDIO_BACKUP_HOST_DIR:-./backups}:/backups`
  (a host folder, not a named volume: survives `down -v`, can live on another drive), env
  `StemStudio__BackupDir=/backups`, `StemStudio__BackupHostDirHint=${STEMSTUDIO_BACKUP_HOST_DIR:-./backups}`;
  header section "Library care" (create the folder first, `sudo chown 1654:1654` on Linux, Windows
  example, check script). The worker gets no `/backups`.
- **`.gitignore`** `backups/`; **`.dockerignore`** `backups` (multi-GB archives never reach the
  build context, which is the repository root).
- **`scripts/check-backup-folder.{sh,ps1}`** + **`scripts/backup_folder_checks.py`** (stdlib,
  host-side, starts no container): folder exists (else "Docker would create it owned by root"),
  writable for uid/gid 1654 on Linux hosts (owner/group/other write+search bits, with the
  `sudo chown` fix in the message) or for the current user on Docker Desktop, free space
  >= 256 MiB (`--min-free-bytes`, = api `StopBelowFreeBytes`), free GiB always printed.
  Tests `scripts/tests/test_backup_folder_checks.py`; registered in `scripts/pyproject.toml`.
- **Non-Docker path**: `dev.sh`/`dev.ps1` set `StemStudio__BackupDir=<repo>/data/backups` and the
  hint (existing values win) and create the folder; documented in the new `scripts/README.md`.
- **CI** (`ci.yml`): `LIBRARY_CARE_CONTRACTS`; Redocly lint of the three 013 documents
  (`backend-api.openapi.yaml` 1.12.0, `backend-api.delta.openapi.yaml`, `worker-api.openapi.yaml`
  1.7.0) with `contracts/redocly.yaml`; `check_superset.py` twice (default 1.11.0 -> 1.12.0, and
  the delta as `older`); devops script checks include `backup_folder_checks.py`; compose
  validation asserts the bind mount and both env values with `STEMSTUDIO_BACKUP_HOST_DIR` set and
  the default `./backups` (jq); the e2e job, the `fake-stack` action (visual, a11y, lighthouse)
  and `e2e-real.yml` create `$RUNNER_TEMP/stemstudio-backups` owned by 1654, export
  `STEMSTUDIO_BACKUP_HOST_DIR` and run the check; the e2e job verifies the api writes `/backups`
  as its own user and the worker has no `/backups`.

## Files changed

`docker-compose.yml`, `.gitignore`, `.dockerignore`, `.github/workflows/{ci,e2e-real}.yml`,
`.github/actions/fake-stack/action.yml`, `scripts/{backup_folder_checks.py, check-backup-folder.sh,
check-backup-folder.ps1, README.md, dev.sh, dev.ps1, pyproject.toml}`,
`scripts/tests/test_backup_folder_checks.py`, `specs/013-library-care/tasks.md` (T011 ticked).
Commits c0927bd5, plus the exec-bit fix after it.

## Tests and coverage

```text
uv run --project tools/quality --frozen pytest -c scripts/pyproject.toml --cov --cov-branch \
    --cov-config=scripts/pyproject.toml scripts/tests
  91 passed; backup_folder_checks.py 100 % line / 100 % branch (85 stmts, 14 branches);
  key_volume_checks.py 100 % / 100 %
scripts/check-backup-folder.sh                    missing ./backups -> FAIL line, exit 1
scripts/check-backup-folder.sh --folder <tmp>     3 x ok, exit 0 (same with the .ps1)
```

## Quality checks

```text
ruff check / ruff format --check / mypy --strict (scripts/pyproject.toml)   clean
sig_check.py scripts/backup_folder_checks.py + its tests                   0 errors, 0 warnings
Invoke-ScriptAnalyzer check-backup-folder.ps1                              clean (dev.ps1: only
                                                                           pre-existing findings)
docker compose config -q: default, +gpu, +gpu+generative, +generative+fake   valid
scripts/check-key-volume.sh                                                all ok (api-keys unaffected)
Redocly 2.78.0 on the three 013 documents                                  valid, 18 warnings
                                                                           (11 + 3 + 4, the accepted kinds)
check_superset.py (both CI invocations)                                    superset: yes
YAML of ci.yml, e2e-real.yml, fake-stack/action.yml                        parses
```

`actionlint` was not run locally (not installed; installing it was not permitted here); the
workflow changes follow the existing step patterns.

## Dependencies added

None (the check is standard library; `jq` is on the GitHub runner image).

## Docs impact

Setup change for the technical manual (CTO, `docs/public`): backups folder `./backups` (or
`STEMSTUDIO_BACKUP_HOST_DIR`), must exist before the first start, `sudo chown 1654:1654` on
Linux, `scripts/check-backup-folder.{sh,ps1}`; non-Docker default `data/backups`
(`StemStudio__BackupDir`). Wording is in the compose header and `scripts/README.md`.

## Open issues / requests

- `scripts/test-all.{sh,ps1}` does not run the scripts tests today (CI does, in the quality job);
  unchanged.
- T066 (devops part): a compose run with the backups mount on a separate project name and ports
  once the backend serves `/api/backups` (needs the api image of this branch).

## Next owner

backend (T031+ use `StemStudio__BackupDir`, `BackupHostDirHint`), qa (e2e backups folder is in
place for every stack), quality-gate (T009-T010).
