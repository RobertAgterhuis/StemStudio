# Hand-off: audio-engine / 016 us1 (T066-T069)

Lane L1, branch `016-round-lane-l1`. Contract: audio-engine 2.6.0 (`types.ts` unchanged).

## What was built

| Task | Red | Green | Result |
|---|---|---|---|
| T066/T067 | `c6cb2c5c` | `d6440674` | `attachChordLane` / `detachChordLane`: `src/chords/{chart,display,lane,lane-view}.ts`, `src/views/{dom-writer,instrument-views}.ts`, `src/timeline/host-size.ts` |
| T068/T069 | `34bf6a78` | `5cf937d2` (+ SIG/dup fixes in the HO commit) | `setKaraokeChords`, `setChordDisplay`: `src/chords/banner.ts`, karaoke view hook; transpose (012) re-spells every chord view automatically |

Behaviour (for L5 and qa):

- **Chord lane DOM**: host > `div.chord-lane-track` (translated per frame with `style.transform: translateX(..px)`, per page with
  reduced motion) > `div.chord-lane-item` (`textContent` = symbol, `data-state` past/current/future, `data-start-ms`,
  `style.transform: translateX(..px)` + `style.width` placed once on the bar timeline). L5 CSS: the track should be
  `position: relative` (or absolute) with the items `position: absolute; left: 0`; the host `overflow: hidden`.
- Window: `barsBehind` (default 1, clamped 0..8) bars before, the current bar plus `barsAhead` (default 6, clamped 1..16)
  after; without a grid with >= 2 downbeats 4 s / 12 s. Reduced motion pages by `barsAhead + 1` bars (12 s without a
  grid), the window starting `barsBehind` before the page. At most 40 items (4 kept before the current one when capped).
- Host attributes: `data-chord-index` (-1 outside a segment), `data-current-symbol`, `data-next-symbol` (`''` at the end),
  `data-position-ms`; all removed on detach. (`data-capo` / `data-current-key` follow in T118/T125.)
- **Banner chords**: `span.karaoke-chord` inline **sibling right before** its anchor `.karaoke-word` in the line slot
  (lead chords, `data-lead="true"`, first in the slot); `data-state` past/current/future against the karaoke clock;
  host `data-chord-index`. A chord starting after its line ended is a lead chord of the next line; `N.C.` and chords after
  the last line are not shown. The ball geometry reads only `.karaoke-word` children. L5 CSS places the chord above the
  word (e.g. `display:inline-block; width:0; transform:translateY(-1.1em)`); the dock grows by that row.
- Errors: `Chord lane host not found: <id>`, `Invalid chord chart: <reason>` (`segments missing`, `more than 4000 segments`,
  `segments not ordered/contiguous`, `root outside 0..11`, `bass outside 0..11`, `unknown quality <q>`,
  `key tonic outside 0..11`, `unknown key mode <m>`, `key regions not ordered`, `more than 50 key regions`),
  `Capo out of range: <n>`; validation before an attached view is replaced.

## Tests and coverage

`npx vitest run --coverage`: 63 files, 1230 tests passed. Engine total 99.69 % statements, 98.05 % branches, 99.48 %
functions, 99.7 % lines. New files: `chart.ts`, `banner.ts`, `lane.ts`, `spelling.ts`, `dom-writer.ts`,
`instrument-views.ts` 100/100; `lane-view.ts` 100 % lines / 96.96 % branches; `display.ts` 87.8 % lines (the
`showSounding` part is used from T125 on); `host-size.ts` 94.11 % lines (`height` used by the canvases from T089 on).
Frame budget (counted writes): a steady playback frame of the lane = 2 writes; a chord change <= 10.

## Quality checks

`tsc --noEmit`, `eslint src tests`: clean. `npm run build`: ok (4 entries). `tools/quality/run_all.py`: PASS, 0 errors,
442 clones (jscpd ran; my one new test clone removed); only test unit-size warnings in my files.
`dotnet format StemStudio.slnx --verify-no-changes`: exit 0.

## Security self-review

Input = chart JSON from Blazor (backend data). Shapes are validated (counts, ranges, ordering, quality whitelist);
symbols are written with `textContent` only; no `innerHTML`, no URL use.

## Decisions

1. Inner `.chord-lane-track` element (one transform write per frame instead of up to 40).
2. Banner chords: siblings before the word (not children), `N.C.` hidden, chord after a line = lead of the next line,
   chords after the last line hidden; states use the karaoke clock (with the user offset), like the words.
3. `barsBehind`/`barsAhead` are clamped (0..8, 1..16), not rejected (the contract lists no option error).

## Deviations

None from `types.ts` / the contract.

## Dependencies added

None.

## Docs impact

None beyond the user-manual chapter already listed by the architect.

## Open issues / requests

None.

## Next owner

L5 (chord lane / banner CSS per the DOM above). L1 continues with T088-T089 (hit lanes).
