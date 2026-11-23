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

## Review fixes (independent code review, 2026-11-22; orchestrator decision: fix all, test-first)

Red tests in `977b6747`, fix in `f50cfe0c`.

| Id | File | Fix | Test |
|---|---|---|---|
| H1 | `src/singing/scorer.ts` | On a detected loop wrap, `finishAtWrap()` first scores the armed unit when the last frame came within `HOP_MS + NEAREST_MS` (25 ms) of the unit end, then `forget()`. A unit the loop leaves well before its end is still discarded. | unit [0, 2000), loop 0-2000, frames 0..1990 twice then 0 -> scores pass 1 and pass 2 (100 each); wrap at 1900 -> no score |
| M2 | `src/singing/feedback.ts` | The overrun guard counts a late hop only when it is delivered >= 5 ms (`LATE_SPACING_SECONDS`) from the previous counted one: one stall delivers its queued hops in a burst at about one `currentTime`, a sustained overrun keeps producing late hops as the context time advances. 50 such hops within 5 s still fail (FR-045). | 50 hops t 0..0.49 at `currentTime` 0.55, then 50 on-time hops -> `running`; existing overrun tests unchanged and green |
| M3 | `src/singing/take-score.ts`, `src/types.ts`, contract | Optional `TakeScoreRequest.transposeSemitones` (integer -12..12, default 0), validated before the fetch (`Invalid singing request: transposeSemitones`), passed to `scoreUnit` as the transpose. | take at reference +200 cents with transpose +2 -> 100/100 (0/0 without); 13, -13, 1.5, `'2'` rejected before fetching |
| M4 | `src/pitch/yin.ts` | `refineLag` interpolates only inside a valley (`b <= a && b <= c`, non-zero denominator), shift clamped to [-1, 1]; `lagToCents` returns null for a non-positive lag or a non-finite / out-of-range (0..12700) value. Both exported for tests. | slope at tauMin keeps the lag; valley shift exact; NaN, -3, 0, 1 -> null; 1250/1500 Hz sines at 16 kHz -> null or finite in 0..12700 |
| M5 | `src/singing/take-score.ts` | `trackInChunks` runs the decimator + YIN over ~1 s of take audio per chunk and awaits `setTimeout(0)` between chunks (offline analysis only, no audible timing). The tracker is block-size invariant, so results are identical. | fake `setTimeout`: a 3 s take waits on one zero-delay timer at a time, >= 2 yields, result equal to the real-timer run; bench reports ~5.4 s total for a 900 s take (linear from 30 s at 44.1 kHz), i.e. ~6 ms per chunk on the main thread |
| L1 | `src/pitch/decimate.ts` | Halves while the rate is >= 64 kHz (96 kHz once, 176.4/192 kHz twice), so the resampler always runs at 44.1-63.9 kHz; a non-finite rate is rejected (would loop). | 176.4/192 kHz: 12 kHz < -60 dB, 9 kHz < -20 dB, 1 kHz kept at 16,000 +- 20 samples; `new Decimator(Infinity)` throws |
| L6 | `src/drums/validate.ts`, `src/singing/feedback.ts` | `checkLookAhead` rejects a non-finite or non-number `lookAheadMs` (`Invalid drum hits: lookAheadMs`; NaN survived the clamp). `start()` re-reads the input after the module load: none -> state `failed`, reason `The input device closed while pitch tracking was starting.` (`INPUT_CLOSED_REASON`), the start rejects with the same message, no node or session. | NaN / Infinity / `'4000'` rejected, look-ahead stays 4000; input closed during a deferred load -> states `starting, failed(reason)`, no worklet node |

### Contract diff (authorised for this field only)

`src/audio-engine/src/types.ts`, `TakeScoreRequest`:

```ts
  /**
   * 012 transpose the take was recorded at (integer -12..12, default 0): the reference target moves by 100 cents per
   * semitone, as in the live scorer. Added at review 2026-11-22; optional, backwards compatible.
   */
  readonly transposeSemitones?: number;
```

`specs/016-instrument-views/contracts/audio-engine.md`, section "Instrument views", `scoreTake` row, appended:
"Optional `transposeSemitones` (integer -12..12, default 0): the 012 transpose the take was recorded at; the reference
target moves by 100 cents per semitone, as in the live scorer; another value rejects with
`Error("Invalid singing request: transposeSemitones")` (added at review 2026-11-22, optional, backwards compatible)."

### Deviations and open items

- **L1 test numbers**: the review asked for "9 kHz at 192 kHz below -60 dB at 16 kHz". That is not reachable with the
  binding R-05 filter (31-tap Blackman, 7 kHz pass-band edge): 9 kHz lies in its transition band, and 44.1/48 kHz
  give -22.7 to -25 dB there too. Measured before the fix at 192 kHz: 9 kHz -12.4 dB, 12 kHz -29.9 dB; after: the
  48 kHz values (9 kHz -22.7 dB, 12 kHz -82.7 dB). The test therefore asserts 12 kHz < -60 dB and 9 kHz < -20 dB at
  176.4/192 kHz (the same behaviour as 44.1/48 kHz). A stricter 9 kHz figure would need a longer filter (R-05 change).
- **H1 "loop end <= unit.endMs"**: the scorer does not know the loop bounds (it sees only the aligned song time), so
  only the "last frame within one hop + 15 ms of the unit end" condition is implemented; a unit cut short by a loop end
  inside it is still discarded as partial (unchanged data-model 3.4 reading). Passing the loop end into the scorer
  would be a new internal dependency; ask if wanted.
- **M4**: no real sine (1000-3000 Hz, with/without sub-harmonic, many offsets) reproduced NaN or > 12700 before the fix;
  the guards are covered with constructed difference arrays through the exported `refineLag` / `lagToCents`.
- **M3 playback rate (open item)**: `TakeScoreRequest` carries no playback rate, so a take recorded at a 012 rate other
  than 1 is still placed and scored at rate 1. No contract change made (orchestrator instruction).
- **Frontend**: Blazor must pass `transposeSemitones` of the take's practice settings (frontend fix agent).

### Checks

Vitest 77 files / 1444 tests green; coverage all files 99.77 % lines / 98.08 % branches / 99.86 % functions / 99.81 %
statements (`decimate.ts` 97.29 / 100, the open line is the no-op default `emit`; `yin.ts` 98.38 / 96.96, pre-existing
`tau <= 1` guard; `feedback.ts` 99.16 / 98.24, pre-existing `halt()` guard; `take-score.ts` 98.33 / 96, pre-existing
`take` guard; `scorer.ts` and `drums/validate.ts` 100 %). `eslint .` and `tsc --noEmit` clean; `npm run build` ok;
`run_all.py` PASS (0 errors, 444 clones, jscpd ran), no unit-size or complexity warning on the changed production
files. Security: the new input (`transposeSemitones`, `lookAheadMs`) is validated with hostile cases (out of range,
fractional, string, NaN, Infinity); `new Decimator(Infinity)` would have looped forever and is now rejected. No new
dependency.
