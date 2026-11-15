# Hand-off: devops / 003 us2 (T039)

## What was built
`8c0771d`. The CI `e2e` job runs two extra steps after the default run and the `@restart` run:
1. `STEMSTUDIO_FAKE_DEVICE=gpu`: `docker compose up -d --wait` (only the worker's environment
   changes, so compose recreates just the worker), then a print of the worker's `/health`, then
   `npm run test:gpu`.
2. The same steps with `STEMSTUDIO_FAKE_DEVICE=gpu-oom` (fallback notice, SC-004).

The mode is set as step `env`, so both compose (worker env) and the Playwright process get it.
This follows the qa hand-off: two passes, with the mode exported to both.
`scripts/test-all.sh --gpu-e2e` and `scripts/test-all.ps1 -Gpu` run the same two passes on the
compose project **`stemstudio-e2e`** with `STEMSTUDIO_SEPARATOR=fake`, then `down -v` that project
only. They never stop or recreate the developer's `stemstudio` project. Ports 8080/8081 must be
free.

## Files changed
`.github/workflows/ci.yml`, `scripts/test-all.sh`, `scripts/test-all.ps1`,
`specs/003-gpu-acceleration/tasks.md` (T039 ticked).

## Tests and coverage
No unit-tested component (the coverage gate does not apply). Checks run:
- actionlint 1.7.12: clean.
- shellcheck 0.11.0: clean. `bash -n scripts/test-all.sh`: OK.
- PSScriptAnalyzer: 0 warnings or errors. `test-all.ps1`: 0 parse errors.
- `STEMSTUDIO_SEPARATOR=fake STEMSTUDIO_FAKE_DEVICE=gpu-oom docker compose -p stemstudio-e2e config`:
  name `stemstudio-e2e`, worker `STEMSTUDIO_FAKE_DEVICE=gpu-oom`, `STEMSTUDIO_SEPARATOR=fake`.
- I did not run the E2E passes locally: ports 8080/8081 are taken by the running stack, which must
  not be stopped. They first run in CI after the merge.

## Quality checks
See above. No Python changed in this checkpoint.

## Dependencies added
None.

## Open issues / requests
- qa: `e2e/package.json` must have `test:gpu` (`--grep @gpu`), and `npm test` must exclude
  `@gpu`. Otherwise the default pass runs the `@gpu` specs with fake device `cpu`. According to
  the coordinator this is in the merged qa hand-off; it is not on this branch.

## Next owner
devops T049 (polish), then CTO.
