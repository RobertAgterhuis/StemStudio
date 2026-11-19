# 025 Replace scattered score-profile conditionals with one profile strategy table
- Status: proposed · Date: 2026-11-19 · Area: worker / transcription + notation (feature 008) · Priority: P3

## Problem statement
The sheet-music feature supports six profiles (`ScoreProfile = Literal["vocals","bass","guitar","keys","piano","drums"]`,
`worker/src/stemstudio_worker/schemas.py:973`). `notation/profiles.py:51-65` already has a `Profile` table, but
the behaviour that differs per profile is decided by string comparisons spread over five modules:

- `jobs/transcription_pipeline.py:213` and `:255` (`profile == "drums"`), `:238` (`profile == "piano"`);
- `transcription/__init__.py:48` (`profile == "piano" and quality == "accurate"` → piano CRNN);
- `notation/profiles.py:91-95` `engine_for(...)` — **the same engine rule a second time**, but it is dead in
  production: `grep -rn engine_for worker` finds only its definition and `tests/notation/test_profiles_cleaning.py:24,108`.
  The rule that actually runs is the one in `transcription/__init__.py`; the tested one can drift silently;
- `schemas.py:1157` (`drums` input shape), `transcription/fake.py:130`;
- `jobs/transcription_pipeline.py:262` and `jobs/practice_service.py:233` detect a detail stem by the substring
  `"/detail/" in path` instead of the already-validated library path pattern.

Instrument neutrality (project rule: every instrument family counts equally): the per-profile capabilities
(an "accurate" engine only for piano; the `cpu-piano` warning; drum pieces) are legitimate model limits, but
because they are hard-coded `if`s, adding an accurate engine for another family means editing all of these
places, and there is no single place that shows which family gets what.

## Impact
- Two copies of the engine-selection rule, one only tested and one only used: the test suite gives false
  confidence about `ScoreSummary.engine` / attribution.
- Each new profile or engine touches 5+ modules; capability asymmetries between instrument families are
  invisible to reviewers.

## Risk
- Leaving it: low-to-medium (drift, slower feature work).
- Change: low; behaviour-preserving refactor behind existing contract tests
  (`worker/tests/contract/test_transcription_contract.py`, `tests/jobs/test_transcription_service.py`).

## Effort
S (1–2 days).

## Step-by-step plan
1. Safety net: a parametrised test over all profiles × qualities asserting the engine actually used by
   `Engines.pitched/drums`, the `ScoreSummary.engine`/`quality` and the warnings; it must agree with the
   current `engine_for` table (if not, that is a bug to fix first).
2. Extend `notation/profiles.Profile` with `kind: pitched|percussion`, `engines: Mapping[ScoreQuality, ScoreEngine]`
   and `warnings` (e.g. `cpu-piano` → a per-engine CPU warning); make `engine_for` read the table.
3. Make `transcription/__init__.Engines.pitched` dispatch on `engine_for(...)` instead of its own `if`; replace
   the `== "drums"` / `== "piano"` checks in the pipeline and the request validator with `Profile.kind` /
   `Profile.engines`.
4. Replace the `"/detail/" in path` sniffing with a helper on the validated path (`ports.LIBRARY_PATH_PATTERNS`).
5. Verify: suites green, transcription contract unchanged; `grep -rn 'profile == "' worker/src` returns nothing.
   Rollback: revert the commit.

## Dependencies and related proposals
- None; small, independent.
