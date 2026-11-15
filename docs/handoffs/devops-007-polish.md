# Hand-off: devops / 007 polish (007:T069)

## What was built

Validation of the 007 compose/CI changes (commit `e5b8a17`, see `devops-007-foundational.md`);
no functional change, this note and the T069 tick only.

## Files changed

`docs/handoffs/devops-007-polish.md`, `specs/007-export-library/tasks.md` (T069).

## Tests and coverage

`scripts/test-all.ps1 -ContinueOnError`: 34/35 steps passed, including the new
`render vectors check` and the contracts lint of the 007 documents; the failing step is the
pre-existing quality-gate duplication error (see the foundational note).

## Quality checks

- `docker compose config -q` -> exit 0 for `docker-compose.yml` and for
  `-f docker-compose.yml -f docker-compose.gpu.yml`; both show the worker volume
  `api-data` -> `/library` with `read_only: true` and `STEMSTUDIO_LIBRARY_DIR: /library`.
- actionlint 1.7.12: "Found 0 errors in 2 files"; shellcheck v0.11.0 exit 0;
  PSScriptAnalyzer 0 errors/warnings.
- Mount check on a fresh fake stack (separate project and image tags): env and read-only checks
  pass, `/health library.readable` waits for stem-separation.

## Dependencies added

None.

## Open issues / requests

Same as `devops-007-foundational.md` (007:T009 tick at integration).

## Next owner

CTO.
