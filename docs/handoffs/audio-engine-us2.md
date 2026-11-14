# Hand-off: audio-engine / us2

## What was built

The mixer engine behind `createEngine` (contract v1.0.0, `types.ts` unchanged):

- `stem-loader.ts`: parallel `fetch` + `decodeAudioData`; errors name the stem
  (`Failed to load stem "bass": HTTP 404`, `... : <network message>`,
  `Failed to decode stem "x": ...`, `Duplicate stem "x"`). Duration = longest stem.
- `transport.ts`: one fresh `AudioBufferSourceNode` per stem per transport event, all started
  with the same `when = currentTime + startDelay` and the same offset. Position =
  `offset + max(0, currentTime - when)`, wrapped into an active loop, capped at the duration.
  pause / stop (always to 0) / seek (clamped, restart all while playing). End of the longest
  stem -> `stopped`, position 0; stale `ended` events of replaced sources are ignored.
- `loop.ts`: `validateLoopRegion` (`Invalid loop region: ...` unless `0 <= start < end <= duration`;
  the previous loop stays), `loop/loopStart/loopEnd` on every source, wrap math. Enabling while
  outside the region restarts at `start`; enabling/disabling/changing while inside re-anchors the
  virtual position (no restart, no audible gap).
- `channel.ts` / `mixer.ts`: `source -> Gain(volume x gate) -> StereoPanner -> Analyser -> master
  Gain -> master Analyser -> destination`; gate = `mute ? 0 : (anySolo && !solo ? 0 : 1)`;
  ramps via `cancelScheduledValues` + `setTargetAtTime(v, now, 0.003)` (~95 % within 9 ms).
  Volume clamped 0..1, pan -1..1, non-finite values throw.
- `meters.ts`: `requestAnimationFrame` loop only while meters are attached; peak ->
  `20*log10`, clamped -60..0, 20 dB/s fall-back; writes `--meter-level` ((dB+60)/60),
  `data-peak-db` (rounded) and `data-clip` (`"true"` for 1.5 s after `|x| >= 1`). The element is
  looked up by id on every frame, so Blazor re-renders are picked up.
- `callbacks.ts`: `OnPlaybackStateChanged` on every change, `OnPositionChanged` after
  seek/pause/stop/end/loop change and every 100 ms while playing (UI only, not audible timing).
  Rejected interop calls (circuit gone) are swallowed.
- `engine.ts` / `index.ts`: composition root; every method throws
  `Audio engine has been disposed` after `dispose()` (dispose itself is idempotent and stops
  playback without calling .NET). `setMetronome` validates and stores (scheduling: US3).

Red-to-green evidence (failing test commit -> implementation commit):

| Tasks | Red | Green |
|---|---|---|
| T082 / T083 transport, loader, callbacks, G4 | dbf7307 | 0fe4818 |
| T084 / T085 mixer | 80130bd | 3ea7a5e |
| T086 / T087 loop | 89fe9be | deabaf2 |
| T088 / T089 meters | 82f947e | fa84204 |
| T090 / T137 engine | 827fd9c | 5f0efb1 |

## Files changed

- `src/audio-engine/src/{stem-loader,transport,callbacks,loop,channel,mixer,meters,engine,index}.ts`,
  `src/audio-engine/src/metronome/settings.ts`
- `src/audio-engine/tests/{transport,mixer,loop,meters,engine}.test.ts`
  (`tests/index.test.ts` placeholder removed; covered by `engine.test.ts`)
- `specs/001-stemstudio-mvp/tasks.md` (T082-T090, T137 ticked)

## Tests and coverage

`npm run test:coverage` (Vitest 5.1.0, v8):

```text
Test Files  6 passed (6)
     Tests  94 passed (94)
Statements   : 100% ( 323/323 )
Branches     : 100% ( 112/112 )
Functions    : 100% ( 118/118 )
Lines        : 100% ( 311/311 )
```

G4: `transport.test.ts` "G4: 50 loop wraps and 20 seeks ..." runs 51 wraps and 20 seeks and asserts
every start call of each transport event has the identical `{when, offset}` across all stems and
that the position stays in `[A, B)` and matches the wrap formula to 1e-9 s.

## Quality checks

- `npm run lint`: clean (complexity <= 5, max-params 4, max-depth 3, jsdoc required on exports).
- `npm run typecheck`: clean.
- `npm run build`: `../StemStudio.Web/wwwroot/js/audio-engine.js 24.11 kB` (+ map), git-ignored.
- `tools/quality/sig_check.py` does not exist yet in this worktree (quality-gate T005).

## Dependencies added

None.

## Open issues / requests

- Behaviour choices inside the contract, for the frontend: (1) `seek(t)` while an enabled loop does
  not contain `t` lands on the loop start (same rule as "enabling while outside restarts at
  start"); (2) `setLoop` validates disabled regions too, so to switch looping off pass the last
  valid region with `enabled: false`; (3) `setChannel`/`attachMeter` for a stem need `loadTrack`
  to have resolved first (`attachMeter('master', ...)` works any time); meter bindings survive a
  new `loadTrack`; (4) `loadTrack` resets the loop and all channels to defaults (volume 1, pan 0),
  so the frontend re-applies the mix after loading.

## Next owner

frontend (T093-T096 can bind to the built bundle); audio-engine continues with US3 (metronome).
