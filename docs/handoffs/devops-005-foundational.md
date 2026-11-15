# Hand-off: devops / 005 foundational (005:T008)

Done together with 007:T008/T009 in commit `e5b8a17`; full evidence in `devops-007-foundational.md`.

## What was built

- `docker-compose.yml` worker env, pass-through with the worker's own defaults and no value
  baked in: `STEMSTUDIO_ONLINE_LOOKUPS: ${..:-on}`, `STEMSTUDIO_CONTACT_URL: ${..:-}` (empty ->
  generic User-Agent token, FR-010), `STEMSTUDIO_WIKIPEDIA_LANGUAGES: ${..:-en}`,
  `STEMSTUDIO_FAKE_ONLINE: ${..:-found}`; documented in the header comment.
- `scripts/dev.{ps1,sh}`: the four variables pass through from the shell unchanged; the start-up
  line prints the lookups state.
- CI `CONTRACTS_DIR` and `scripts/test-all.*` -> `specs/007-export-library/contracts` (one switch,
  covers 005 per architect note).
- CI `cpu-image-guard`: fails when the worker image sets `STEMSTUDIO_CONTACT_URL` (FR-010);
  verified against a good and a deliberately bad image.

## Files changed

`docker-compose.yml`, `.github/workflows/ci.yml`, `scripts/dev.{ps1,sh}`, `scripts/test-all.{ps1,sh}`,
`specs/005-artwork-band-info/tasks.md` (T008).

## Tests and coverage

See `devops-007-foundational.md` (`scripts/test-all.ps1 -ContinueOnError`: 34/35 passed; the one
failure is the pre-existing quality-gate duplication error from the 004 qa merge).

## Quality checks

`docker compose config -q` (default + GPU) exit 0; actionlint 0 errors; shellcheck exit 0;
PSScriptAnalyzer 0 warnings/errors.

## Dependencies added

None.

## Open issues / requests

- `STEMSTUDIO_FAKE_ONLINE` default in compose is `found` (the worker default); qa sets
  `not-found`/`failed`/`off` per spec run (quickstart A2/A3).

## Next owner

CTO (README env table, integration).
