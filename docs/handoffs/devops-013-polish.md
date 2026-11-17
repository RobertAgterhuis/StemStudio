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
