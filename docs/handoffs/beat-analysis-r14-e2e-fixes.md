# Hand-off: beat-analysis / round 014+015 E2E worker fixes

Branch `r14-fix-wrk` (from `014-015-round` 5e5d683f). Worker only; no contract file changed.

## 1. Range score view started at the track's first downbeat

E2E `score-follow.spec.ts` (scenario 5, range 6..14 s): `bars[0].startSeconds` was 1.4 instead of >= 6.

Root cause: not the score view alone. The API shifts the track grid so 0 is the range start and keeps the
earlier beats *and downbeats* (negative). Both the 008 notator (`notation/timebase.py`) and the score view
(`engraving/score_view.py`) took `downbeats[0]` as bar 1, i.e. the track's first downbeat (-4.6 s in range
time = 1.4 s track time). The MusicXML therefore had two empty bars before the range, and the view timed them
correctly at 1.4 s and 3.4 s.

Fix: `BeatGridTempo.bar_one_downbeat` (schemas.py) = the first downbeat at or after the input start
(tolerance 1 ms for the API's 6-decimal rounding), else the last downbeat. The notator and the score view
both use it, so score and view stay consistent. Notes between the range start and that downbeat become the
pickup (existing 008 rule). Result for the E2E case: pickup 6.4 s, bar 1 at 7.4 s (track time, after the
API's +6 s offset). Unranged scores are unchanged (their first downbeat is >= 0). The fake transcriber's bar
positions are modulo the bar and need no change.

Spec note for the architect: 008 data-model 2.4 says "`k0` = index of the beat nearest to `downbeats[0]`".
For a shifted grid this now reads "the first downbeat at or after 0" (014 data-model 2.5 step 3 inherits it).
Suggest updating the 008 text; no API change needed.

## 2. Section times rounded to 0.1 s

Both the `stems-novelty` analyser (`boundaries.spans_in_seconds`) and the fake analyser (the E2E stack, the
8.6153 source) now go through `analysis/structure/times.py`: inner boundaries rounded to 0.1 s at the output,
first start 0, last end the exact `durationSeconds`, each end = the next start (contiguous, ordered). The
analysis itself keeps precise frame times. The fake drops a section whose rounded start would reach the end.
Before, downbeat/beat snaps rounded to 3 decimals.

## Tests and quality

New tests: `test_schemas.py` (3, bar-one rule), `notation/test_timebase.py` (range bar 1),
`engraving/test_score_view.py` (E2E range case), `analysis/structure/test_fake.py` (2, rounding,
contiguity, start rounding onto the end), `test_novelty_boundaries.py` (grid snaps). Adjusted: the old 3-decimal
expectations and the shift test (range quarters are now one bar less, same meter phase).

Worker: 2763 passed, 3 skipped; coverage 100 % line, 100 % branch (11825 statements, 1350 branches).
ruff, ruff format, `mypy src tests` clean. `tools/quality/run_all.py` PASS (jscpd ran; no warning in changed
code).

## Dependencies

None added.
