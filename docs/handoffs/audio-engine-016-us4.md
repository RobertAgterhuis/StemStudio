# Hand-off: audio-engine / 016 us4 (T097-T104)

Lane L1, branch `016-round-lane-l1`. Contract: audio-engine 2.6.0 (`types.ts` unchanged).

## What was built

| Task | Red | Green | Result |
|---|---|---|---|
| T097/T098 | `020cab4f` | `bf222cd9` | `src/pitch/{yin,decimate}.ts`: pure YIN core on a 16 kHz polyphase decimator (31 taps, 7 kHz cut-off), shared by the worklet and `scoreTake` |
| T099/T100 | `cace8194` | `314378c7` | `src/pitch/worklet.ts` (processor `stemstudio-pitch-tracker`, bundle `js/audio-engine-pitch-worklet.js`), `src/singing/feedback.ts`: `startSingingFeedback`, `stopSingingFeedback`, `getRoundTripLatency` |
| T101/T102 | `877c6a28` | `42c3a488` | `src/singing/{scorer,pitch-view,session}.ts`, `OnSingingUnitScored` / `OnSingingStateChanged` callbacks |
| T103/T104 | `d0fada19` | `350d0021` | `src/singing/take-score.ts`: `scoreTake` with the same core and median; `src/views/pending.ts` removed (no 2.6.0 method is pending any more) |
| refactor | - | `e69d4b7f`, `51ee6daa` | shared `worklet-module.ts` (`onceLoader`, `WorkletNodeCreator`), `timeline/frame-listener.ts`, `CanvasSurface.drawNowLine`; three units split below 15 lines |

Behaviour (for L5 and qa):

- Live path: the open 015 input feeds the pitch worklet (one explicit mono input); frames every 10 ms are median-smoothed
  (5 frames) and aligned by the round trip: `roundTripMs` + `latencyMethod` from the request, else the open input's
  `estimatedLatencySamples` (`source: 'estimated'`, 30 ms when no input is open).
- Overrun guard: more than 50 frames later than 50 ms within 5 s -> state `failed`, reason
  `Pitch tracking is too slow on this device.`; the worklet module is loaded once per context, a failed load is retried.
- Scorer: a unit arms when the song enters it within 50 ms of its start, scores when the song passes its end, again on
  every loop pass; a seek discards the partial unit; fewer than 20 voiced frames -> no score. Octave folding to the
  nearest target, the 012 transpose moves the target.
- Pitch view: one `<canvas aria-hidden="true">`, 4 s back / 2 s ahead, now line at 2/3; reference bars per run of equal
  (50 cents) voiced frames, singer polyline; range = reference 5th-95th percentile +-3 semitones (at least an octave).
  Reduced motion pages every 6 s. CSS custom properties `--pitch-view-reference` (`#64748b`), `--pitch-view-singer`
  (`#22c55e`), `--pitch-view-now` (`#e5e7eb`). Host attributes `data-state`, `data-singer-cents`, `data-target-cents`,
  `data-unit-index`, `data-last-score`, `data-latency-source`; removed on stop.
- `scoreTake`: fetch + decode the take, pad half a YIN window, ignore unvoiced edge windows, same scorer core, pass 1,
  `latencySource: 'estimated'`, latency 0.
- Errors: `No input device is open`, `Singing feedback is already running`, `Pitch view host not found: <id>`,
  `Invalid pitch contour: hopMs not 10 | frames missing | more than 90000 frames | values outside 0..12700`,
  `Invalid singing request: tolerance outside 10..200 | roundTripMs outside 0..1000 | ...`,
  `Pitch input rate must be at least 16000 Hz`.

## Tests and coverage

`npx vitest run --coverage`: 74 files, 1385 tests passed. Engine total 99.72 % statements, 97.89 % branches, 99.71 %
functions, 99.73 % lines; `src/singing` 99.48 % lines / 97.92 % branches, `src/pitch` 99.24 % / 95.65 %, `src/views`
100 % / 90.9 %. SC-004 benchmark (reported, not gating): 0.024 / 0.019 / 0.015 ms average per 128-frame block at
44.1 / 48 / 96 kHz (worst 4.8 ms is the first, JIT-cold block).

## Quality checks

`tsc`, `eslint` clean; `npm run build` ok (four bundles); `run_all.py` (tools/quality venv) PASS, 0 errors, 444 clones
(jscpd ran); no unit-size/complexity warning left in `src/{singing,pitch,drums,chords,timeline,views}`. Remaining
engine warnings are in pre-016 code (`recording/`, `score-follow/`, `engine.ts`), picked up in T134.

## Security self-review

Reference contour, units, tolerance and round trip are validated before anything starts (count cap 90 000 frames,
range, finite numbers). The take URL goes through the existing stem loader (injected fetch, same as stems); fetch and decoding errors reject.
Worklet messages are shape-checked (`isFrame`) before use. Nothing is written as markup.

## Decisions

1. Fallback round trip 30 ms when the input has no estimate (`source: 'estimated'`).
2. `scoreTake` skips edge windows that the padding silenced (no evidence about the singer).

## Deviations / dependencies / blockers

None / none / none.

## Docs impact

None beyond the user-manual chapter.

## Next owner

L5 (singing page CSS custom properties, callbacks). L1 continues with T118-T119 (current key).
