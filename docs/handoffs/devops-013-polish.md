# Hand-off: devops / 013 polish (T066, devops part)

## What was done

- Compose validation with the backups mount: `docker compose config -q` for the default, GPU,
  GPU + generative and generative + fake combinations; resolved config checked for the api bind
  mount `/backups` (default `<repo>/backups`, and with `STEMSTUDIO_BACKUP_HOST_DIR`), both env
  values, and no `/backups` in any other service. The same checks run in CI (jq assertions in the
  compose-validation job).
- CI validation: every stack start (e2e job, `fake-stack` action for visual/a11y/lighthouse,
  `e2e-real.yml`) creates a temporary backups folder owned by uid 1654 and runs
  `scripts/check-backup-folder.sh`; the e2e job proves the api writes `/backups` as its own user
  and the worker has no `/backups`.
- `scripts/check-key-volume.sh`: all checks ok with the new mount (the key ring stays api-only).

## QA request (CTO note, after the first polish)

- **E2E override** `docker-compose.e2e.yml`: api env
  `StemStudio__LibraryCare__ThrottleBytesPerSecond=${STEMSTUDIO_E2E_THROTTLE_BYTES_PER_SECOND:-8388608}`
  (8 MiB/s: a fake-stack backup of a few tens of MB takes a few seconds; the option is read
  once the backend adds it, unknown keys are ignored until then). CI's e2e job and the
  `fake-stack` action export `COMPOSE_FILE=docker-compose.yml:docker-compose.e2e.yml`,
  `E2E_BACKUP_DIR` (= the temporary uid-1654 backups folder) and `E2E_LIBRARY_THROTTLE=1` via
  `GITHUB_ENV`, so every later `docker compose` call and the Playwright run see them;
  `e2e-real.yml` exports `E2E_BACKUP_DIR` only (no throttle on the real stack). The e2e job also
  asserts the api has a throttle > 0; compose validation asserts only the api gets it.
- **Backup fixture CI step** (quality job): ruff, ruff format, mypy strict and pytest (fail_under
  90 from its config) with `tests/fixtures/backup/pyproject.toml`; guarded by
  `hashFiles('tests/fixtures/backup/pyproject.toml') != ''`, so it is skipped until the qa lane
  is merged and runs afterwards. The existing fixture mypy call excludes
  `^tests/fixtures/backup/` (that folder has its own `mypy_path`).

## Not done here (needs the api of this wave)

- A live run of the stack with the backups mount (separate project name and ports) was not
  started: building the images would retag `stemstudio/*:local`, which belong to the owner's
  stack, and the backup endpoints are not implemented yet. CI covers it on the first push of
  the integrated branch.

## Tests

```text
scripts tests: 91 passed, backup_folder_checks.py 100 % line / 100 % branch
```

## Next owner

CTO (integration), qa (e2e specs can rely on `STEMSTUDIO_BACKUP_HOST_DIR` being set in CI).
