# 020 Split the worker's `ports.py` and `schemas.py` hub modules per feature
- Status: proposed · Date: 2026-11-19 · Area: worker / module structure (ports, schemas) · Priority: P2

## Problem statement
Unit-level hygiene in the Python services is excellent: an AST scan of all 2,136 functions in `worker/src`
and `generator/src` (vendor excluded, `.claude/scratchpad/main/units.py` at the time of the audit) found no
unit over 30 lines, none with cyclomatic complexity above 5 and none with more than 4 parameters — enforced by
ruff (`worker/pyproject.toml`: `max-complexity = 5`, `max-args = 4`, `max-statements = 15`). The remaining
maintainability debt sits one level up, in two hub modules:

| Module | Lines | Top-level classes | Modules importing it (of 192 non-vendor worker modules) |
|---|---|---|---|
| `worker/src/stemstudio_worker/schemas.py` | 1,768 | 98 | 122 |
| `worker/src/stemstudio_worker/ports.py` | 1,523 | 86 | 107 |

(`grep -c "^class "`, `grep -rl "stemstudio_worker.ports import"`, `grep -rl "stemstudio_worker.schemas import"`.)

Both files are already organised as feature sections separated by banner comments, which is the natural split:
- `ports.py`: core/jobs (1–660), artwork+metadata 005 (661), rendering 007 (746), weights (861), lyrics 006
  (883), sheet music 008 (1056), storage 013 (1309), practice lane 014 (1391).
- `schemas.py`: core (1–364), metadata 1.3.0 (365), render 1.4.0 (510), lyrics 1.5.0 (642), transcription
  1.6.0 (953), practice 1.8.0 (1292), storage 1.7.0 (1611).

The generator has the same pattern at smaller scale (`generator/src/stemstudio_generator/ports.py` 609 lines,
`schemas.py` 425 lines).

## Impact
- SIG module size and module coupling (fan-in) ratings are pulled down by these two files; every feature
  touches both, so parallel feature lanes (the project runs several agents per round) collide in the same
  files and every lane's review diff includes unrelated context.
- Importing any port loads all 86 port classes and the full contract; cohesion is per feature, coupling is
  global. A change in the lyrics contract invalidates mypy caches and test selection for the whole worker.

## Risk
- Leaving it: steady merge-conflict and review cost; the files grow by one section per feature.
- Change: low-to-medium. Pure moves, but the public import paths are used by 100+ modules and by contract
  tests (`worker/tests/contract/*`, `tests/test_*_contract.py`). Keep `ports.py`/`schemas.py` as re-export
  facades during migration so nothing breaks; the wire contract (JSON schema/OpenAPI) must not change.

## Effort
M (3–5 days), mostly mechanical; assumes the facade approach and one feature section per PR.

## Step-by-step plan
1. Safety net: export the current OpenAPI document and the JSON schemas of every contract model to the
   scratchpad and add a test that compares them (byte-identical) after each step.
2. Turn `schemas.py` into a package `schemas/` with `__init__.py` re-exporting everything (`__all__`), then move
   one section per PR into `schemas/<feature>.py` (metadata, render, lyrics, transcription, practice, storage);
   core types stay in `schemas/core.py`. Check: schema snapshot identical, mypy strict and ruff green.
3. Same for `ports.py` → `ports/<feature>.py`, with the shared error hierarchy (`WorkerError` and friends) in
   `ports/errors.py`.
4. Migrate importers per feature to the specific module (e.g. `from stemstudio_worker.schemas.lyrics import ...`);
   add an import-linter (or a small pytest) rule that feature modules import only core + their own feature.
5. When no internal module imports the facades any more, keep them only for tests/contract or remove them.
6. Repeat for the generator if its modules keep growing (optional).
7. Verify: snapshot test, both suites and coverage ≥ 90 % green; re-measure fan-in (target: no module imported by
   more than ~40 % of the package). Rollback: each PR is a move and can be reverted alone.

## Dependencies and related proposals
- Independent; best done before 016-unify-job-lane-persistence so the lane code lands in the new layout.
