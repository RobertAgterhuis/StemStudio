# Hand-off: devops, 022+023 round, wave 2 (2026-11-17)

Branch `r22-lane-devops` (worktree `.claude/worktrees/r22-devops`, from round head 8f77df3e). Not pushed, not merged.
Task 023 T069 ticked; 022 T085 not needed (qa seeds the 5,000-item database with `docker compose cp`).

## Commits

| Commit | Content |
|---|---|
| 5a3d4924 | `docker-compose.e2e.yml`: `./e2e/lyric-scripts:/lyric-scripts:ro` and `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT=${...:-/lyric-scripts}` on the api; `scripts/dev.sh`/`dev.ps1` default the variable to `<repo>/e2e/lyric-scripts` (existing value wins); T069 ticked |
| d3c27e32 | `scripts/api_image_size_checks.py` + `check-api-image-size.{sh,ps1}` + tests (image size check), `scripts/pyproject.toml`, `scripts/README.md` |
| 9825d222 | `.github/workflows/ci.yml`, `scripts/test-all.{sh,ps1}`: contract checks, Perf step, lyric mount and image size steps |

## What was done

1. **Lyric scripts (T069).** Mounted at `/lyric-scripts` (read-only), not under `/scripts` (read-only agent-script mount;
   Docker cannot create a mount point inside it). The variable names the folder; `LyricScript.Resolve` picks
   `<title-slug>-<kind>.json`, else `<kind>.json`. It is only read with the agent backend `fake`; the override does not set the
   backend (agent-e2e passes and the specs' own api recreate do, as for 011). The Claude double ("fake-claude") has no variable
   in the contracts: nothing added, its test self-skips. CI e2e job step "The api sees the fake lyric scripts read-only ..."
   checks variable, folder content, read-only (`touch` fails) and that the worker has no `/lyric-scripts`.
2. **Image size check.** The vendored dictionaries (`src/StemStudio.Api/LyricAssist/Dictionaries`, 7.7 MB) must stay below 15 MB in
   the image (`/app/LyricAssist/Dictionaries`, read with a throwaway `du` container); with `--baseline-image` the whole image must
   also grow < 15 MB. CI runs the first check in the e2e job on the image its own compose build made
   (`stemstudio/api:local` on the runner). Python checker: 100 % line+branch, in the scripts gate (CI "Devops script checks" now
   also lints `backup_folder_checks.py` and the new file in `test-all`, which had missed `backup_folder_checks.py`).
3. **CI contracts job** (and the same steps in `test-all.sh`/`.ps1`): Redocly 2.78.0 lint of 022 (full + delta) and 023 (full + delta);
   `python -I .../022-library-folders/contracts/merge_full.py --check`; the same for 023; `check_output_schema.py`
   (jsonschema 4.26.0, as `worker/uv.lock`); `python -m pytest specs/023-lyric-writing-assistant/contracts` (pyyaml 6.0.3, pytest 9.2.0).
   All run via `uv run --no-project --with <pin>`.
4. **Perf budgets.** dotnet job step `dotnet test tests/StemStudio.Api.Tests -c Release --no-build --filter Category=Perf
   -p:RunSettingsFilePath=` after the coverage gate (also in `test-all`). The normal coverage run is unchanged (no filter, nothing
   excluded), so it still runs every test, Perf included, only without timing asserts. The empty `RunSettingsFilePath` removes
   the coverlet collector; without `CollectCoverage` coverlet.msbuild does not instrument either.

## Results (local)

| Check | Result |
|---|---|
| Redocly 2.78.0 022 (full + delta) | 0 errors, 11 warnings |
| Redocly 2.78.0 023 (full + delta) | 0 errors, 17 warnings (the `redocly.yaml` comment says 13 + 2: the contract grew, only warnings) |
| `merge_full.py --check` 022 / 023 | exit 0, `superset: yes` |
| `check_output_schema.py` | exit 0 (10 scripts, 3 valid + 8 invalid samples) |
| `pytest specs/023-.../contracts` | 4 passed |
| scripts tests (ruff, ruff format, mypy, pytest) | 102 passed, 100 % line and branch (key_volume, backup_folder, api_image_size) |
| `docker compose config -q`, 5 combinations (default; e2e; gpu+e2e; generative+fake+e2e; gpu+generative+e2e) | all valid; `check-key-volume.sh` with the e2e override ok |
| Perf command, local | exit 0, "No test matches `Category=Perf`" (no perf tests exist in this branch yet); proven on `Category=sec` (1 passed, no `TestResults` written) |
| bash -n, PowerShell parser (test-all, dev, check-api-image-size), YAML parse of ci.yml and the override | no errors |
| Image sizes (own tags `r22devops/api:base` = parent of the dictionaries commit, `:head`) | dictionaries 7.7 MB; whole image 437.7 -> 451.0 MB = +13.3 MB (budget 15) |
| E2E override trial (own project `r22devops`, own image, `run --no-deps`) | variable `/lyric-scripts`, 11 files, write fails with "Read-only file system"; project, volumes and images removed |

The owner's stack `stemstudio` and its images/volumes were not touched (`docker compose` only with project `r22devops`;
the api image was built from the Dockerfile under an own tag, never through the compose `image: stemstudio/api:local` tag).

## Decisions and notes

- The whole-image growth is +13.3 MB against the pre-Phase-0 image (dictionaries 7.7 MB, the rest the Phase 0 libraries and
  contracts). The 15 MB budget holds, with 1.7 MB left for the whole round if somebody wants to apply it to the total.
- `check-api-image-size` in CI measures only the dictionaries (needs no baseline image); the total growth is a one-off check.

## Open items (for the CTO / other lanes)

1. **Not read by code yet.** `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT` is not read anywhere in `src` (only documented in
   `LyricScript.cs`); until backend-023/ai-agent wire `LyricScript.Resolve` into the fake model, the variable is inert.
2. **Lyric E2E pass (added after the CTO follow-up).** `scripts/lyric-e2e.{sh,ps1} fake|none`: api-only recreate with backend `fake` +
   `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT=/lyric-scripts`, then `npm run test:lyric-assist`; `none`: backend `none`, then
   `npm run test:lyric-assist-none`. `test:lyric-assist-claude` self-skips and is not run. The scripts pass with a notice while
   `e2e/package.json` has no `test:lyric-assist` (qa's branch adds it). CI e2e job: two new steps after the agent passes;
   `test-all --agent-e2e` runs them too. `agent-e2e.*` now adds `docker-compose.e2e.yml` with `-f` when `COMPOSE_FILE` is empty,
   so the lyric tests of `agent.spec.ts` (qa) see `/lyric-scripts` in local runs as in CI. Checked with stubbed docker/npm
   (command line, backend and variable per pass); not run against a stack (the reader, `LyricAssistantFactory` on `r22-lane-agent`,
   and qa's `package.json` land with the integration).
3. **Stale text.** `e2e/lyric-scripts/README.md` is fixed on this branch (`/lyric-scripts`). `specs/023-.../contracts/DELTA.md`
   section 4 still says `/scripts/lyric/<name>.json` (contracts, CTO).
4. **Perf tests do not exist in this branch** (022 T044/T062 are backend wave 1). The CI step is a no-op until then; it must
   start to run real tests once backend's `[Trait("Category", "Perf")]` tests are merged. Backend must keep the timing asserts
   skipping under coverlet (the coverage run includes the Perf tests).
5. `redocly.yaml` of 023 mentions "13 + 2 warnings"; now 17 (comment only, contracts are CTO-owned).

## Dependencies and licences

None new in the product. CI pins used by `uv run --with`: `jsonschema==4.26.0` (MIT), `pytest==9.2.0` (MIT), `pyyaml==6.0.3` (MIT);
all already pinned in the repository's `uv.lock` files. No new image, no new action.
