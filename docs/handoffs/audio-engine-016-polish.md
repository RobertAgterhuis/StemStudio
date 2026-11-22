# Hand-off: audio-engine / 016 polish (T134)

Lane L1, branch `016-round-lane-l1`. Contract: audio-engine 2.6.0 (`types.ts` unchanged). All L1 tasks of round 016 are
done: T053-T056, T066-T069, T088-T089, T097-T104, T118-T119, T125-T126, T134.

## What was done in T134

| Item | Commit | Result |
|---|---|---|
| Frame budget of all views together | `test(audio-engine): frame-budget benchmark ...` | `tests/views/frame-budget.bench.test.ts`: one engine with karaoke banner + chords, chord lane, hit lanes (dense groove) and pitch view with live singing, 300 playing frames |
| Duplication / SIG | `e69d4b7f`, `51ee6daa`, `89beb359`, `b5477fee`, last test commit | shared `worklet-module.ts`, `timeline/frame-listener.ts`, `CanvasSurface.drawNowLine`, `SpelledChart.write`; test stubs `stubSecureWorklets` (`mocks/media.ts`), `stubHostLayout` (`mocks/engine-rig.ts`), `chordSegment` (`mocks/views-rig.ts`) |
| `views/pending.ts` | `350d0021` (T104) | removed; no 2.6.0 method is a stub any more |
| TSDoc | - | every exported 016 symbol documented (eslint-plugin-jsdoc clean) |

Frame budget (jsdom, this machine): **0.25 ms average, 0.49 ms p95 per frame** for all views together (budget 4 ms,
gated on the average), worst frame 550 canvas primitives (gate <= 1000, R-12 "a few hundred" per canvas). Chord lane
steady frame 2 counted writes (gated in `chord-lane.test.ts` and `capo-display.test.ts`). SC-007 in Chromium stays with
the CTO (performance trace). YIN (SC-004, reported): 0.019 / 0.015 / 0.011 ms average per 128-frame block at
44.1 / 48 / 96 kHz.

## Tests and coverage

`npx vitest run --coverage`: 77 files, 1410 tests passed. Engine total **99.81 % statements, 97.96 % branches,
99.92 % functions, 99.83 % lines** (gate 90 %). 016 folders: `src/chords` 100 / 98.9, `src/pitch` 99.24 / 95.65,
`src/singing` 99.48 / 97.92, `src/views` 100 / 90.9 (lines / branches); `src/drums`, `src/timeline` 100 %.

## Quality checks

- `tsc --noEmit` and `eslint .` clean; `npm run build` ok (main bundle, stretch, capture and pitch worklets in
  `src/StemStudio.Web/wwwroot/js/`).
- `run_all.py` (tools/quality venv): PASS, 0 errors, 444 clones, jscpd ran (no "duplication NOT measured" line).
- No unit-size/complexity warning in 016 production code (`src/{chords,drums,pitch,singing,timeline,views}`,
  `worklet-module.ts`). Remaining engine production warnings are pre-016 units this round did not change:
  `engine.ts` `createParts` (17) / `loadTrack` (16), `recording/*` (6 units), `score-follow/*` (2). Test-code
  unit-size warnings follow the existing pattern (scenario tests 16-28 lines).
- No .NET code changed in this lane (no `dotnet format` run needed).

## Security self-review (whole lane diff)

Every Blazor input is validated before use: chord charts (segments <= 4000, ordered and contiguous, pitch classes,
quality whitelist, key regions <= 50), drum hits (<= 40000, piece whitelist, strength, order), pitch contours
(<= 90000 frames, 0..12700), units, tolerance 10..200, round trip 0..1000, capo integer 0..12. Text goes to the DOM only
through `textContent` / `dataset`; no `innerHTML`. Worklet messages are shape-checked. No microphone audio leaves the
engine (pitch node has no output).

## Decisions (round summary)

See the us1, us3, us4, us7 and us8 hand-offs; new here: the frame budget gates on the average (p95 reported) so a
GC pause in CI cannot flake it.

## Deviations / dependencies / blockers

None / none (no new packages) / none.

## Next owner

Integration (T139+): CTO for the SC-007 Chromium trace; L5 for the CSS custom properties and `.chord-sounding` styling.

## Security fixes (independent security review, 2 LOW findings)

Test-first: red tests in `df1e9873` (+ lint fix of the test helper `226891a4`), fix in `3534018b`.

1. **Start/stop race** (`src/singing/feedback.ts`). `start()` awaited the pitch worklet module load and then always
   connected; a `stop()` / `dispose()` during that wait cancelled nothing, so the tracker was still attached to the mic,
   a session was built on a possibly disposed host and `running` was reported. Fix: a start token. Every start bumps it;
   `stop()` and `dispose()` bump it too (`cancelStart`) and set the state to `stopped` when it was `starting`. After the
   await, `loaded(token)` returns false for a stale token, so `connect()` and the `running` notification are skipped. A
   load failure after a cancel still rejects the start promise but leaves the state `stopped` (no `failed`).
   Tests: deferred `addModule`; stop and dispose during the load -> no worklet node, no session, no input connection,
   states `starting, stopped`; failed load after stop; a new start after a cancelled one runs once (stale load connects
   nothing).
2. **Unvalidated Blazor input** (`src/singing/validate.ts`, used by `startSingingFeedback`; `validateUnits` is also used
   by `scoreTake`). New `validateLatencyMethod`: allow-list `loopback | manual | estimated` (types.ts `LatencyMethod`,
   unchanged); null/absent keeps the `manual` default; anything else -> `Invalid singing request: latencyMethod`.
   `validateUnits`: at most `MAX_UNITS = 20000` (`more than 20000 units`), each unit an object with
   `Number.isInteger(index) && index >= 0` (`unit index invalid`) and finite `startMs`/`endMs` (`unit times not finite`),
   then the existing order check. Tests: method `'x'` and `7`, 100,001 units, index -1 / 1.5 / null unit, NaN start,
   Infinity end, string start.

Checks after the fix: Vitest 77 files / 1423 tests green; coverage all files 99.81 % lines / 97.99 % branches
(`feedback.ts` 99.09 / 98.03, the one open line is the pre-existing defensive guard in `halt()`; `validate.ts` 100 %);
`eslint .` and `tsc --noEmit` clean; `npm run build` ok; `run_all.py` PASS (0 errors, 444 clones, jscpd ran), no
unit-size/complexity warning on `feedback.ts` or `validate.ts` (`start` split into `start` + `loaded` to stay <= 15
lines, `validateUnit` + `isIndex` to stay <= complexity 5). No contract or `types.ts` change, no new dependency.
