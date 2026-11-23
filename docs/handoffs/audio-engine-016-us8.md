# Hand-off: audio-engine / 016 us8 (T125-T126)

Lane L1, branch `016-round-lane-l1`. Contract: audio-engine 2.6.0 (`types.ts` unchanged).

## What was built

| Task | Red | Green | Result |
|---|---|---|---|
| T125/T126 | `c096ed75` | `28161e0a` (+ `89beb359` SIG/lint follow-up) | `setChordDisplay({capo, showSounding})` in lane and banner: `SpelledChart.write` in `src/chords/display.ts` (shared by `lane-view.ts` and `banner.ts`), lane host `data-capo` |

Behaviour (for L5 and qa):

- Shapes: every lane item and banner chord shows the shape symbol with the capo (2.5 with `capo = c`, region key,
  012 transpose), e.g. sounding `Eb Cm Ab Bb` read `C Am F G` with capo 3.
- `showSounding: true`: each item/chord becomes `<shape text>` + `<span class="chord-sounding">Eb</span>` (the sounding
  symbol, capo 0, same transpose). The child holds the bare symbol; **L5 styles it** (small, and the parentheses of
  "C (Eb)" through CSS `::before`/`::after` or a margin). It is added whenever `showSounding` is set, also with capo 0
  (contract wording: "each item also gets"); Blazor decides whether to offer the switch at capo 0. Clearing
  `showSounding` removes the children. Items that enter the window later get them too.
- The child is only rebuilt when a symbol changes (counted as one structural write); a steady playback frame stays at
  2 writes on the lane.
- Lane host `data-capo` = the capo (`0` by default), written with the chord attributes on the next frame after
  `setChordDisplay`; removed on detach. The banner host gets no `data-capo` (contract names only the chord lane).
- Errors: `Capo out of range: <value>` for anything but an integer 0..12 (`-1`, `13`, `2.5`, `NaN`); nothing changes
  (capo and `showSounding` keep their values).

## Tests and coverage

`npx vitest run --coverage`: 76 files, 1409 tests passed. Engine total 99.81 % statements, 97.96 % branches, 99.92 %
functions, 99.83 % lines; `src/chords` 100 % lines / 98.9 % branches.

## Quality checks

`tsc`, `eslint` clean; `npm run build` ok; `run_all.py` PASS, 0 errors, 444 clones (jscpd ran); no warning in
`src/audio-engine/src/chords/**` (one pre-016-us8 test-unit warning in `chord-lane.test.ts:219`, for T134).

## Security self-review

The capo is checked (integer 0..12) before anything changes; symbols come from validated pitch classes and the quality
table and are written with `textContent`, never as markup.

## Decisions

1. `.chord-sounding` holds the bare symbol (no parentheses), styling is Blazor's.
2. Sounding children also at capo 0 when `showSounding` is set (literal contract reading).

## Deviations / dependencies / blockers

None / none / none.

## Next owner

L5 (capo badge reads `data-capo`; `.chord-sounding` CSS). L1 continues with T134 (polish, frame budget).
