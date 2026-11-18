# Hand-off: beat-analysis / 014 T020-T025, T050-T051 (L2, HO:polish)

## What was built

The `stems-novelty` 1.0.0 section analyser in `worker/src/stemstudio_worker/analysis/structure/`
(data-model 2.2-2.3, research R-10 constants):

- `features.py`: frames from the grid (bars of a reliable grid with the lead-in as one frame; beats grouped by
  `beatsPerBar` for an unreliable grid, snap `beat`; 2 s without beats, snap `time`); stems loaded mono at
  22,050 Hz (`StructureError` "The stems could not be read." on a bad file); per frame chroma CQT (tuning 0),
  MFCC 20 (z-scored per coefficient), mix RMS, vocal activity ratio (hops above 0.1 x p95 of the vocals RMS),
  energy share per standard stem; `stack_memory(n_steps=2)` and L2-normalised columns; per-channel energy (also
  detail channels) for the solo rule.
- `novelty.py`: tapered checkerboard (half 6), Foote novelty on `S = F^T F` (edge padding), peak picking with the
  frozen window (`pre/post_max 3`, `pre/post_avg 2`, `delta 0`, `wait 3`). Own loop instead of
  `librosa.util.peak_pick`: that one counts flat zero stretches as peaks and lets them block real ones via `wait`.
- `boundaries.py`: vocal switches holding 2 frames, union (novelty wins within 1 frame), short-segment merges
  (4 frames, first/last 2) into the more similar neighbour (cosine of mean features), `maxSections` limit
  (`merged-to-limit`), times (downbeat/beat times, 0.1 s for `time`; first 0, last the duration).
- `clusters.py`: average-linkage cosine clustering, threshold 0.25, letters by first appearance (`Z` caps beyond
  26); margins = cosine distance to the nearest other centroid (0.5 with one cluster).
- `labels.py` + `names.py`: data-model 2.3 rules in order, numbering per kind, "<instrument> solo" with the 004
  display names (detail channel when it holds >= 50 % of its parent), "Part <cluster>" for `few-repetitions`,
  `no-vocals`, a single segment is "Song".
- `analyzer.py`: `StemsNoveltyAnalyzer` (progress 0.05 / 0.4 / 0.7 / 1.0), warnings `grid-unreliable`, `no-grid`,
  `few-repetitions`, `no-vocals`, `merged-to-limit`; confidence 0.5 x boundary strength + 0.5 x margin.

Decisions (no contract change):
- `stack_memory` puts the previous frame into every column, which delays each novelty peak by exactly one frame
  (measured on the synthetic set: every peak +1 against the plain features). The analyser takes the boundary one
  frame before the peak (`STACK_LAG = 1`); without it every boundary landed one bar late (still inside the SC-001
  tolerance, but the loops would start a bar late).
- In-between vocal ratios (0.25..0.5) are labelled `verse` (data-model: "verse by default"); the neighbour rule
  is not applied.

## Tests and coverage

`tests/analysis/structure/` (74 tests): `test_features.py`, `test_novelty_boundaries.py`, `test_clusters_labels.py`,
`test_synthetic_songs.py` (the 6 forms of R-10 generated in-test by `songs.py`), `test_names.py`, `test_fake.py`.
Results on the 6-form set: every special section right (guitar, piano, bass, woodwinds solo with the instrument,
bridge, drums break as instrumental: 6/6), boundaries exact or one bar off (recall and precision 1.0 within one
bar); a 336 s song analyses in about 2 s (SC-001 budget 10 s). Coverage of `analysis/structure/**` 100 % line,
100 % branch (worker total 100 % / 100 %, 2754 passed).

## Quality checks

ruff, ruff format, `mypy src tests` clean; `sig_check` 0 warnings for the package; the 014 T015 scope rule holds
(ports, schemas, numpy, scipy, sklearn, librosa, soundfile only); `run_all.py` PASS.

## Dependencies added

none (scikit-learn 1.9.1 and scipy 1.18.1 are already in `uv.lock` through librosa; `sklearn.*` added to the mypy
ignore-missing-imports list).

## Docs impact

Technical manual: section detection runs on the stems (no download, about 2 s per 5-minute song on one core);
`STEMSTUDIO_STRUCTURE_ANALYZER` selects real/fake.

## Next owner

Integration (CTO T102-T104).
