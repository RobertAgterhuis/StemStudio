# Hand-off: audio-engine / 016 foundational (T053-T056)

Lane L1, branch `016-round-lane-l1`. Contract: audio-engine 2.6.0 (`types.ts` unchanged), chord spelling vectors 1.0.0.

## What was built

| Task | Red | Green | Result |
|---|---|---|---|
| T053/T054 | `5dfba35d` | `b9208d7d` (+ test dedup in the HO commit) | `src/timeline/`: `clock.ts` (`audioNow`, `outputLatency`, `heardTime` moved from `karaoke/clock.ts`; `TimelineClock.positionMs()` / `positionAtMs(contextTime)`), `cursor.ts` (moved from `karaoke/cursor.ts`, unchanged), `frames.ts` (`TimelineFrames`: transport listener, one frame after start/seek/pause/stop/loop/timing change and `redraw()`, every frame while playing), `paging.ts` (`pageStart`), `bars.ts` (`BarScale` from the grid downbeats, mean bar outside the grid; `SecondScale` without a grid; `scaleFor(grid)`). `KaraokeClock` now wraps `TimelineClock` minus the offset; `Karaoke` uses `TimelineFrames`. No behaviour change: every 006/012/014 karaoke test is unchanged and green |
| T055/T056 | `ab16368f` | `9f8ba6bd` | `src/chords/spelling.ts`: `chordSymbol(chord, key, {transpose, capo})`, `keyName`, `keyDisplay(key, transpose, confidence?)`, `usesFlats`, `noteName`, `keyAt(chart, ms)` (region the chord starts in, fallback global key, then null = sharps). All 245 `cases`, 90 `keys` (display) and 2211 `capo` vectors pass |

## Files changed

`src/audio-engine/src/timeline/{clock,cursor,frames,paging,bars}.ts` (new; cursor moved), `src/audio-engine/src/chords/spelling.ts` (new), `src/audio-engine/src/karaoke/{clock,index,view}.ts`, `src/audio-engine/src/engine.ts` (import only), `src/audio-engine/tests/timeline/*.test.ts`, `src/audio-engine/tests/chords/chord-spelling.test.ts`, `src/audio-engine/tests/karaoke/{clock,cursor}.test.ts` (moved blocks / import), `specs/016-instrument-views/tasks.md` (T053-T056 ticked).

## Tests and coverage

`npx vitest run --coverage`: 60 files, 1172 tests passed. Engine total 99.84 % statements, 98.05 % branches, 99.9 % functions, 99.86 % lines; `src/timeline/**` and `src/chords/**` 100 % lines and branches.

## Quality checks

`tsc --noEmit` clean, `eslint src tests` 0 problems; `tools/quality/run_all.py`: PASS, 0 errors, 442 clones (jscpd ran; one new 7-line test clone with the karaoke clock test removed); `dotnet format StemStudio.slnx --verify-no-changes` exit 0 (no .NET touched).

## Decisions

1. Engine `keyDisplay` implements the data-model 2.8 text incl. ` (uncertain)` (vectors `keys`); written keys are backend-only (not in the engine).
2. Without a grid with >= 2 downbeats the views use seconds (contract "4 s/12 s without a grid").

## Dependencies added

None.

## Docs impact

None (internal).

## Open issues / requests

None.

## Next owner

L1 continues with T066-T069 (chord lane, banner chords, `setChordDisplay`).
