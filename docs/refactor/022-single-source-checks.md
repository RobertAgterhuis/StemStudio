# 022 One source for the checks that CI and test-all both run
- Status: proposed · Date: 2026-11-19 · Area: delivery / scripts and CI workflow · Priority: P2

## Problem statement
The same check lists are maintained by hand in three places, and half of the shell twins duplicate their logic.

1. **The contract checks exist three times.** The contracts job in `.github/workflows/ci.yml:307-393` (about 87 lines),
   `scripts/test-all.sh:44-62` and `scripts/test-all.ps1:53-71` each hold their own copy of the same 10 `specs/0xx/.../contracts`
   paths (grep `specs/0`: 10 / 10 / 10). Each copy also has its own list of redocly, `check_superset` and `merge_full` calls (23
   lines in each test-all script). Every feature round adds a directory to all three copies: in the last 30 days `ci.yml` changed in
   33 commits, `test-all.sh` in 24 and `test-all.ps1` in 24. The environment comments in `ci.yml:32-59` already have to explain
   which document "stays 008's" and which is "the newest full", which shows the lists drift.
2. **Shell twins duplicate logic.** `scripts/README.md` says "Every `.sh` script has a `.ps1` twin". Seven pairs carry the logic
   twice, with no shared core:

   | Pair | Lines (`.sh` / `.ps1`) |
   |---|---|
   | `build-soundtouch` | 278 / 260 |
   | `test-all` | 229 / 253 |
   | `dev` | 178 / 213 |
   | `agent-e2e` | 92 / 109 |
   | `check-soundtouch-served` | 102 / 105 |
   | `lyric-e2e` | 61 / 85 |
   | `check-worker-fonts` | 51 / 59 |

   That is about 1,000 duplicated lines. The other pairs show the better pattern: thin wrappers around a tested Python core
   (`check-key-volume.sh` → `key_volume_checks.py`, `check-backup-folder` → `backup_folder_checks.py`, and others).
3. **The CI job list is spelled out step by step.** The e2e job lists every suite as its own step (`ci.yml:721-762`), and so do
   `test-all.*`. A new suite therefore means editing two or three files.

## Impact
- Every round edits the same lists three times. A missed edit gives a local `test-all` that passes while CI fails, or the other
  way round. The PR checklist (`pull_request_template.md`) promises that `scripts/test-all` mirrors CI.
- SIG duplication: the repository's own measure counts this (the scripts are in `sonar.sources`, `sonar-project.properties:186`).
  Logic copied in two shell languages cannot be unit-tested once.
- Merge conflicts between parallel lanes concentrate in `ci.yml` and `test-all.*`.

## Risk
- Leaving it: the cost grows linearly with every feature, and the lists drift further.
- The change: low to medium. A manifest-driven runner must reproduce the current checks exactly. Mitigation: run the old and the
  new path side by side for one round and compare the lists of checked files. Rollback: revert, since the old scripts stay in
  git history.

## Effort
M (2–3 days): about 1 day for the contract manifest and runner, and 1–2 days to give the four largest twins a Python core.
Assumes the Python checks keep running in the `tools/quality` environment, as they do today.

## Step-by-step plan
1. Safety net: write a test (in `scripts/tests`) that collects, from all three current sources, the set of contract files that are
   linted, superset-checked and merge-checked, and asserts that the three sets are equal. It may fail today, which is a finding.
2. Introduce `specs/contracts.manifest.yaml`. Per directory it lists the redocly config, the documents to lint, the
   superset/merge checks with their arguments, and which document is "current" (replacing the `CONTRACTS_DIR` comments).
3. Write `scripts/contract_checks.py`, which reads the manifest and runs the checks, gated at 90 % coverage in `scripts/tests`.
   Replace the CI contracts job with one step that calls it, and replace the blocks in `test-all.sh` and `test-all.ps1` with one
   line each. Check: the step 1 test passes, and CI lints the same files as before (compare the logs).
4. Do the same for the E2E suites: one `e2e/suites.json` (name, npm script, stack preparation), read by CI (matrix, see
   `023-e2e-split.md`) and by `test-all`.
5. Twins: move the logic of `check-soundtouch-served`, `check-worker-fonts`, `agent-e2e` and `lyric-e2e` into Python modules, and
   keep the `.sh` and `.ps1` files as wrappers of under 30 lines, like `check-key-volume`. `build-soundtouch` and `dev` stay
   shell-native for now (container and process orchestration), but share one argument list.
6. Verify: a new feature round adds one manifest entry and no edits to `ci.yml` or `test-all.*`. SIG duplication for `scripts/`
   drops. Roll back per step.

## Dependencies and related proposals
- `013-gate-gaps.md` (the new Python cores must fall under the scripts coverage gate).
- `023-e2e-split.md` (the suite manifest drives the matrix).
