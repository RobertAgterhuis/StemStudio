# Hand-off: audio-engine / 016 us7 (T118-T119)

Lane L1, branch `016-round-lane-l1`. Contract: audio-engine 2.6.0 (`types.ts` unchanged).

## What was built

| Task | Red | Green | Result |
|---|---|---|---|
| T118/T119 | `20c48e66` | `0599c0c0` | Chord lane host `data-current-key`: `src/chords/current-key.ts` (`CurrentKey`), written by `LaneView` next to `data-position-ms` |

Behaviour (for L5 and qa):

- `data-current-key` = key display of data-model 2.8 for the key region holding the position (fallback the global key):
  `D major`, with a 012 transpose `E major (original D major, +2)`; `''` when the chart has no key. Key regions carry no
  confidence in `KeyRegionData`, so the engine never appends `(uncertain)`; Blazor shows confidence from the track key.
- Follows the position while playing and on seek; a transpose change re-writes it on the next frame (also while paused).
  The attribute is written through the counted `DomWriter`, so a steady frame still costs 2 writes; the region lookup is
  a `Cursor` (O(1) per frame) and the text is rebuilt only when the region or the transpose changes.
- Removed on detach with the other lane host attributes.
- Spelling per region key (T056) is unchanged and now covered on the lane: F#/Gb major reads `F#` in D major and `Gb`
  in D-flat major.

## Tests and coverage

`npx vitest run --coverage`: 75 files, 1394 tests passed. Engine total 99.72 % statements, 97.90 % branches, 99.71 %
functions, 99.73 % lines; `src/chords` 98.75 % lines / 98.28 % branches (`current-key.ts` 100 %).

## Quality checks

`tsc`, `eslint` clean; `npm run build` ok; `run_all.py` PASS, 0 errors, 444 clones (jscpd ran), no warning in
`src/audio-engine/src/chords/**`.

## Security self-review

No new input: the regions are validated by `prepareChart` (at most 50, ordered, pitch class and mode checked); the key
text is written as a data attribute, never as markup.

## Decisions

1. `''` (not an absent attribute) when the chart has no key, like the other lane attributes.
2. Capo does not change `data-current-key` (contract: "current region's key at the current transpose").

## Deviations / dependencies / blockers

None / none / none.

## Next owner

L5 (key badge reads `data-current-key`). L1 continues with T125-T126 (capo display).
