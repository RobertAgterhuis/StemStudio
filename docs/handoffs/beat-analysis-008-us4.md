# Hand-off: beat-analysis / 008 US4 (T057-T058, drum onsets)

## What was built

`worker/src/stemstudio_worker/analysis/drum_onsets.py`: `LibrosaDrumTranscriber`, the
`DrumTranscriber` port, exported from `stemstudio_worker.analysis`. It is stateless, runs on the
CPU only and is safe to reuse.

How it works, per given piece (kick, snare, toms, hihat, cymbals):

1. The stem (or its `window`) is loaded mono at 22,050 Hz.
2. librosa `onset_strength` + `onset_detect` run with hop 256 (11.6 ms) and `delta` 0.2 for kick
   and snare, 0.3 otherwise (research S-05).
3. Each onset is refined to its attack: the first sample that reaches 25 % of the local absolute
   peak, searched from 25 ms before to 30 ms after the detected frame. librosa's detection lags the
   attack by 5-15 ms; after refinement the measured error is <= 0.6 ms.
4. The bleed gate drops onsets whose linear peak is below 10 % (`BLEED_GATE`) of the piece's
   99th-percentile onset peak.
5. `strength` = peak / that percentile, clamped to 0..1.
6. A silence floor of -60 dBFS (`SILENCE_PEAK = 1e-3`) applies to stems and to single onsets.
   Undecodable stems give no hits and a warning in the log.

Other behaviour:

- Missing pieces give no hits.
- Times are relative to the window start.
- `progress(done / n)` is called once per piece; `TranscriptionCancelledError` from it propagates.
- Hits come back sorted by time; ties are ordered kick, snare, toms, hihat, cymbals.

Gate design note: the gate uses the linear sample peak, not librosa's onset strength. Onset
strength is computed on a dB mel spectrogram, so a copy 26 dB quieter still has about 60 % of the
flux, and a 10 % gate on it would not remove bleed. data-model 2.3 asks for "10 % of the piece's
99th-percentile strength". I implemented "strength" as linear loudness, which also matches the
research note "a per-piece loudness gate relative to the stem's peak".

## TDD evidence (red -> green)

| Step | Red | Green |
|---|---|---|
| T057 / T058 drum onsets | `9b2c2ce` | `f320c5a` (+2 tests for coverage in the green commit: undecodable stem, gate below the floor) |
| attack refinement | `a9de326` (onsets within 3 ms) | `469cdd2` |

## Files changed

- `worker/src/stemstudio_worker/analysis/drum_onsets.py` (new), `analysis/__init__.py` (export + docstring)
- `worker/tests/analysis/test_drum_onsets.py` (new)
- `docs/handoffs/beat-analysis-008-us4.md`

## Tests and coverage

```text
uv run pytest tests/analysis --cov=stemstudio_worker.analysis --cov-branch
  61 passed in 17 s; analysis 211+ stmts: drum_onsets.py 74 stmts / 6 branches 100 %, package 100 % line and branch
```

Tests cover:

- hits of every piece within 20 ms on generated click tracks with distinct timbres (and within
  3 ms after refinement);
- time order;
- strengths in 0..1, relative to the piece's percentile;
- the bleed gate removes a copy of the kick at half the gate level in the snare stem and keeps
  a ghost note at 2.5x the gate;
- missing pieces, no inputs, silent stems, -70 dBFS hiss and an undecodable file give no hits;
- window-relative times;
- one progress step per piece and propagated cancellation.

## Quality checks

```text
ruff check src tests / ruff format --check     clean
mypy src tests (strict)                        Success: no issues found in 254 source files
sig_check.py drum_onsets.py + test             2 files, 31 units, 0 errors, 0 warnings
```

## Dependencies added

None (librosa and numpy were already locked).

## Open issues / requests

- **stem-separation (transcription jobs, drums)**:
  - Construct `LibrosaDrumTranscriber()` in the runtime and call `notate_drums(hits, settings)`.
  - For pieces missing from `drumInputs`, add `score_warning("missing-drum-piece", piece=...)`
    (`notation.notices`).
  - Add `detail-bleed` if the product wants it for drums.
- **architect**: data-model 2.3 "onset strength" should read "linear onset peak" (see the design
  note above).
- Real-song tuning (the 10 % gate on bleed from real detail stems) remains for the integration
  wave on the reference machine (008:T080).

## Next owner

beat-analysis: 008:T071 (HO:polish). stem-separation (B): wire the drum transcriber into the
transcription job (US4).
