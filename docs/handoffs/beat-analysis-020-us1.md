# beat-analysis 020: HO us1 (T030-T039)

Lane BEAT-ANALYSIS, branch `020-lane-beat`, 2026-11-23. Questions and answers:
`beat-analysis-020-questions.md` (Q1 and Q2 answered, **Q3 open**).

## Done

| Task | Commits | Result |
|---|---|---|
| T030/T031 | `9b10f56b` / `df2d9d90` | `insights/bleed.py`: B2 with the ratio-floor guard (Q1), co-occurrence, sources of detail and rest stems |
| T032/T033 | `1bdfbb48` / `37815d9c` | `insights/artefacts.py` (A2 dropouts, Q2), `insights/detail.py` (A3 kinds) |
| T034/T035 | `b30d0608` / `0836fe3b` | `insights/rating.py`, `insights/thresholds.py` (calibrated, `ANALYZER_VERSION = "1.0.0"`), `insights/analyzer.py` (`SignalQualityAnalyzer`) |
| T036/T037 | `2d7476e2` / `7f8fd455` | `insights/fake.py` (`FakeQualityAnalyzer`, data-model 2.6) |
| T038 | `974c76bc` | reliability table test (the table itself landed in `df2d9d90`, see Deviations) |
| T039 | `32bfccca` | G1 harness `tests/insights/g1.py`, `g1_museval.py`, `test_g1_validation.py`: **pending dataset** |

Later commits: `bf123547` (SIG refactor, shared test helper), `7ca735a5` (contract conformance of both
analyzers' results), `a7642fa5` (hostile inputs).

Composition (as the worker-io lane wired it): `stemstudio_worker.insights.analyzer:SignalQualityAnalyzer`
and `stemstudio_worker.insights.fake:FakeQualityAnalyzer`, both built **without arguments**. The fake
has one optional argument, `sleep`, which defaults to `time.sleep`; it exists for tests.

## Formulas for the architect (data-model 4.2-4.4 at T108; I did not edit data-model)

**Bleed (4.2/4.3, Q1 refinement plus the gain cap that Q3 asks you to confirm).** For a target i and a
source j, per band b, over j's active frames:

- `g_b = max(0, <P_i, P_j> / <P_j, P_j>)`
- `floor_b` = 5th percentile of `P_i / P_j` over j's loud cells (j active and `L_j >= peak_b(j) - 20 dB`)
- `g*_b = min(g_b, floor_b, 1)`
- `E_ij = sum_b g*_b sum_t P_j`; `s_ij = E_ij / sum_tb P_i`, clipped to 0..1; `index = max_j s_ij`

Co-occurrence: `full / guarded > 2` for the four pairs caps the level at `medium`. Detail stems use
every other standard stem plus their siblings, and a sibling counts as the parent stem (`mainSource` is
always a standard stem).

Constants: `LOUD_RANGE_DB` 20, `FLOOR_PERCENTILE` 5, `MAX_GAIN` 1, `CO_OCCURRENCE_RATIO` 2. Bleed
levels: T1 = **0.0065**, T2 = **0.053**. The index is an energy share, so it is linear in alpha².

**Artefacts (4.4, Q2).**

- A1 flicker is **not shipped** and does not feed the level.
- A2 dropouts work on the 23 ms block envelope. A hole is a run of blocks that:
  - lies more than 30 dB below the median of +-0.5 s (43 blocks);
  - lasts at most 13 blocks (0.3 s);
  - **falls abruptly**: the maximum of the two blocks before it minus its first block is at least 25 dB;
  - **recovers abruptly**: the maximum of the two blocks after it minus its last block is at least 25 dB.
- Share = hole blocks / active frames. `x = share / max(mix share, 1e-3)`; `index = x / (x + 50)`.
- Artefact levels: T1 0.35, T2 **0.62**.
- Kind `dropouts` when the level is at least medium.
- Detail kinds as specified: `model-residual` when the parent's residual is above -20 dB;
  `rest-share` when the rest energy share is above 0.25.

## V1 results (`test_validation_v1.py`, in the default suite)

| Measure | Song | Spearman | Levels correct | Notes |
|---|---|---|---|---|
| bleed, 30 ordered pairs x 4 alphas (index = max over sources) | arranged, seed 7 / 11 / 23 | 0.94 / 0.97 / 0.91 | 93 % / 87 % / 87 % | mainSource correct 100 % at alpha >= 0.1 |
| bleed, playing-together control (kick and bass on the same onsets) | - | - | never High | capped by co-occurrence |
| bleed, dense song (all families all the time on one grid) | seed 7 | - | 1 of 6 stems falsely High (guitar 0.080 from bass) | **strict xfail, Q3** |
| dropouts, 6 families x r {0, 0.5, 2} | arranged + dense | 0.96 | 100 % | clean stems of every family 0 |

Flicker (Q2 record):
- (c), a tile-wise injection: still flat.
- (b), a stem-only dip statistic over the full band: rises for 5 of 6 families. Clean vocals already
  score 1.5-1.8 per mille, higher than piano at p = 0.3, and the scales differ about tenfold per family,
  so no family-neutral thresholds exist. About 20 of the 60 minutes were used.

Scripts: `.claude/scratchpad/020-round/beat/exp/` (not committed).

## G1 (T039): pending dataset

- `test_g1_validation.py::test_g1_on_musdb18_hq` (`@pytest.mark.integration`) needs
  `STEMSTUDIO_MUSDB_DIR`, `STEMSTUDIO_MUSEVAL_PYTHON` (a throw-away env with `museval==0.4.1`) and
  optionally `STEMSTUDIO_G1_OUT`. Without them it skips with "pending dataset".
- The run uses Demucs `htdemucs_6s` and sums guitar and piano into `other`. Our index for `other` is the
  energy-weighted mean of the guitar, piano and other indices.
- Spearman per stem type: bleed vs -SIR >= 0.5, artefacts vs -SAR >= 0.4. Fewer than 10 tracks counts
  as not measured.
- The synthetic two-track stand-in runs end to end in the default suite, scored with a 0-tap BSS
  decomposition.
- Reliability table: all `not-measured`, every label `indicative`, nothing masked.

## Decisions

- Q1, Q2: as answered (see questions file).
- The V1 song is `arranged_song`: instruments with rests in independent 2-s sections, 60 % on. The
  dense `song` is the control.
- The synthetic drum sounds now decay naturally. Earlier they stopped abruptly at -30 dB, which is a
  real gate.
- The fake keeps the data-model 2.6 values, including `flicker` for guitar, although the real analyzer
  no longer emits flicker. The fake piano: artefacts low, `activeSeconds` 11 (not `too-short`).
- Both analyzers' results are validated against the OpenAPI `QualityResult` schema.
- When both dimensions are `unknown`, the reason is `clean`. No code fits better, and that case only
  arises after G1 has failed a dimension.

## Deviations

- **T038 order:** the reliability table was written with T031 (thresholds.py), so the T038 test was
  green on its first run instead of red first.
- **Q3 (open):** the gain cap `g* <= 1`, and the dense control as a strict xfail.

## Security self-review

- Untrusted input: audio content of library files whose paths the job layer has already checked.
- Decoding errors (unreadable, empty or truncated files) become `QualityAnalysisError` with the file
  name only.
- Memory per stem is bounded by the block reading. Any channel count is mixed down.
- There is no subprocess or network in `insights/`. The G1 test runs one subprocess, with a fixed
  argument list and no shell.

## Quality (full own suite)

| Component | Tests | Line | Branch |
|---|---|---|---|
| `worker/src/stemstudio_worker/insights/` (9 modules) | 179 passed, 1 xfailed (Q3), 2 deselected (integration) | 100 % (519 statements) | 100 % (68 branches) |

- ruff, ruff format and mypy strict are clean on `insights` and `tests/insights`.
- `tools/quality/run_all.py worker`: **PASS**, 0 errors. jscpd ran: 14 clones, none in
  `insights`/`tests/insights` (I removed mine). No finding in `insights/` production units.
- Worker contract tests (`tests/contract/test_insights_contract.py`): 49 passed.

## Needs from others (shared files I do not own)

- **quality-gate:** `insights:0: warning [dependency] insights is not in the dependency map` (T009/T010).
  `insights` now imports only `ports`, `schemas`, numpy, soxr and soundfile; scipy is not needed.
- **CTO/architect:** data-model 4.2-4.4 and research R-02/R-03 "Phase 0 measurements" with the formulas
  and V1 table above (T108). Help topic `stem-quality-reliability`: say that artefacts are estimated
  from dropouts only.
- **Dependencies:** none new. scipy is used only in tests (already locked). museval 0.4.1 (MIT) runs only
  in a throw-away env and is never added to the lock.
