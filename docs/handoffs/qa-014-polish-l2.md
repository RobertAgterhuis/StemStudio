# Hand-off: qa (lane L2) / 014 T018 practice-flow fixture (HO:polish)

## What was built

`tests/fixtures/practice-flow/generate_practice_flow_fixture.py` (CC0, fixed seeds, byte-identical reruns) writes
into its own folder (audio git-ignored) and commits `manifest.json`:

- `practice-song.wav`: 82 s stereo PCM16, 120 BPM 4/4, first downbeat 0.5 s; form Intro 4, Verse 8, Chorus 8,
  Guitar solo 8, Chorus 8, Outro 4 bars; manifest: `downbeats` (40), `sections` (kind, label, start/end seconds,
  bars, startBar).
- `stems/{vocals,drums,bass,guitar,piano,other}.flac`: the same song as separate mono stems (sum = mix).
- `practice-song-free.wav`: 30 s steady pad in soft noise; the real beat analyser finds no reliable grid
  (confidence 0.03, `reliable: false`) - for sections scenario 5 instead of `silence_5s.wav` (L3 request).

`tests/fixtures/tests/test_practice_flow_fixture.py` (12): formats and lengths, committed manifest = generated,
sections contiguous on downbeats, stems sum to the mix, vocals only in verse/choruses, `LibrosaBeatAnalyzer`
finds 120 BPM reliable with the beats within 30 ms, the free song unreliable, `StemsNoveltyAnalyzer` on the stems
finds the form (recall >= 0.8 within one bar, the solo with `guitar`), reruns byte-identical.

Generation: `uv run --project worker python tests/fixtures/practice-flow/generate_practice_flow_fixture.py`
(added to the CI e2e job and the `fake-stack` action, together with the 015 recording fixtures).

## Tests and coverage

`uv run --project worker pytest -c tests/fixtures/pyproject.toml tests/fixtures/tests --cov --cov-branch
--cov-config=tests/fixtures/pyproject.toml`: 266 passed, total line 100 %, branch 100 % (the generator 100/100;
coverage source `tests/fixtures/practice-flow` because the folder name is no module name).

## Open issues / requests

- L3: point scenario 5 at `tests/fixtures/practice-flow/practice-song-free.wav` (copy path as for the other
  fixtures) and, if useful, scenario 1 at `practice-song.wav` (manifest gives the expected bars).
- The fake-stack action now runs `python -m recording.generate_recording_fixtures`; it needs L6's
  `tests/fixtures/recording/` (merged at the checkpoint).

## Next owner

L3 (E2E), integration.
