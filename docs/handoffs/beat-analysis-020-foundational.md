# beat-analysis 020: HO foundational (T014, T015)

Lane BEAT-ANALYSIS, branch `020-lane-beat` (from ca82a055), 2026-11-23.

## Done

| Task | Commits | Result |
|---|---|---|
| T014 | `f7b6a2cc` (red) | `worker/tests/insights/test_representation.py`; `synthetic.py` (six family generators, exact bleed/TF-mask/gating injections) |
| T015 | `a231addb`, `ee369e62` | `insights/representation.py`: block-wise read (`BLOCK_FRAMES` 65,536), mono, soxr to 22,050 Hz, STFT 2048/512 Hann, 64 HTK mel bands 30 Hz-11 kHz, frame level, 23 ms block level (dropouts), activity `max(-50 dBFS, peak - 40 dB)`, the six standard stems read in lockstep and summed in the time domain (mix), `spectrogram_of` for in-memory signals |

Memory: a 15-minute stereo stem peaks below 48 MB (tracemalloc; decoded audio alone would be about
318 MB). The mel power is about 10 MB.

## Decisions

- A shorter standard stem is padded with silence in the mix.
- Standard stems with different sample rates are refused (`QualityAnalysisError`). Stems of one
  separation always share a rate.
- An empty FLAC cannot be opened by libsndfile and fails like any other unreadable file. An empty WAV
  gives 0 frames, which rates `too-short`.
- Error text: "A stem could not be read: <file name>". It never contains folders.
- `block_db` (the first 512 samples of every frame) was added because a 50 ms gating hole is invisible
  in the 93 ms frames.

## Quality

See `beat-analysis-020-us1.md` for the totals: coverage 100 % line and branch for
`representation.py`, ruff, ruff format and mypy strict clean, `run_all.py` PASS.

## Dependencies

None new. numpy, soxr and soundfile are already locked.
