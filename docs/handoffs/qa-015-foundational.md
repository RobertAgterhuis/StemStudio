# Hand-off: qa / 015 foundational (lane L6, 015 T009 + 014 T017)

## What was built
- `tests/fixtures/recording/generate_recording_fixtures.py`: one fake-capture input per instrument family
  (`input-{vocals,guitar,bass,piano,drums,other}.wav`; piano and drums stereo), `click-track.wav` (track for the
  loopback test, clicks every 0.5 s from 0.5 s, sample-exact onsets) and `silent.wav`; 44.1 kHz PCM16, 20 s, peak
  -3.1 dBFS, fixed seeds. WAVs are git-ignored, `recording/manifest.json` (facts + SHA-256 hash list) committed.
- `tests/fixtures/tests/test_recording_fixtures.py`: format per file, one input per family, peaks, click onsets,
  stereo channels differ, every input sounds in each second, hash list, rerun byte-identical and equal to the
  committed manifest, CLI.
- `docs/testing/TEST-STRATEGY.md`: intro paragraph for 014/015, **section 20 (014, T017)** (what is tested where,
  structured songs / fake analyser / score views / PDF assertions, user story -> test files, request-log check
  SC-009, axe + keyboard-only rules) and **section 21 (015, T009)** (fixtures, fake capture with the full Chromium
  build, digital loopback via `StemStudio__Recording__TestLoopback`, secure context, network assertion SC-007,
  waiting on take ids, Firefox scope, user story -> test files).

## Files changed
- `tests/fixtures/recording/{generate_recording_fixtures.py,manifest.json,.gitignore}`
- `tests/fixtures/tests/test_recording_fixtures.py`
- `docs/testing/TEST-STRATEGY.md`

## Tests and coverage
- `cd tests/fixtures && uv run --project ../../worker pytest tests/test_recording_fixtures.py --cov=recording --cov-branch`:
  **30 passed**, `generate_recording_fixtures.py` **line 100 %, branch 100 %** (132 statements, 18 branches).
- ruff check / ruff format --check / mypy --strict: clean.

## Quality checks
`run_all.py` PASS; no 015 fixture warnings left.

## Dependencies added
none (numpy, soundfile from the worker environment)

## Docs impact
none (developer docs only)

## Decisions / deviations
- 015 T009 names "section 19"; 013 already uses 19, so 014 is section 20 and 015 section 21.
- The generator lives in `tests/fixtures/recording/` (lane-owned path) and runs as a module from `tests/fixtures`
  (`python -m recording.generate_recording_fixtures`) to reuse `generate_fixtures`, `generate_voice_fixtures` and
  `sung_voice`. `tests/fixtures/pyproject.toml` (coverage source list) is not L6's; the command above passes
  `--cov=recording` explicitly. Request for integration: add `recording.generate_recording_fixtures` to that list.
- Risk: the committed-manifest equality assumes identical float maths on Linux CI; if CI differs, the hash check
  is the one to relax (report, not silently).

## Open issues / requests
- L2 (devops T010): project `recording` should pass the fixture per family as the fake-capture file; the specs will
  launch their own browser per family through `support/recording.ts` (L6).

## Next owner
L6 continues with T044 (E2E US1) after T040-T043.
