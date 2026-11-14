# Hand-off: audio-engine / us3

## What was built

Look-ahead metronome (FR-014, FR-015, SC-003), wired into `setMetronome` / `loadTrack`:

- `metronome/grid.ts`: click grid in track time. Detected grid: `beats[i] + offsetMs/1000`,
  accent when `(i - index of nearest preceding downbeat) mod beatsPerBar == 0` (beats before the
  first downbeat count back from it; no downbeats: from beat 0). Override grid:
  `t0 + k*60/bpm + offsetMs/1000`, `t0 = downbeats[0] ?? beats[0] ?? 0`, accent when
  `k mod beatsPerBar == 0` (also for negative k). Unreliable/missing grid without override: silent.
- `metronome/timeline.ts`: maps a context-time window onto track-time segments from the
  transport anchor; a window crossing `loopEnd` continues at `loopStart` (capped at 64 segments
  per window); no clicks before `when` or past the track end.
- `metronome/clicks.ts`: accent (1600 Hz, amplitude 1.0) and normal (1000 Hz, 0.6) 30 ms mono
  clicks rendered once; own `GainNode` straight to `destination` (bypasses the master); volume
  ramped immediately.
- `metronome/scheduler.ts`: a `setInterval` (25 ms default) only wakes the scheduler. Each run
  covers `[max(now, scheduledUntil), now + scheduleAhead)` and calls `source.start(t)` with `t`
  derived from `currentTime`, the anchor and the grid. State changes, seek, pause, stop and loop
  changes (transport `onReposition`) and every `configure` cancel future clicks (`stop()` on
  sources with `t >= now`) and reschedule. A starved timer never schedules into the past.
- `metronome/settings.ts` (US2): validation `bpmOverride` 30..300 or null, `offsetMs` -2000..2000,
  `beatsPerBar` integer 1..12, volume clamped.

`setInterval` appears only in `callbacks.ts` (10 Hz position reports to .NET, UI only) and
`metronome/scheduler.ts` (wake-up only); no audible start time depends on a timer.

Red-to-green: T098 failing tests de83a18 -> T099 implementation 5992d6f.

## Files changed

- `src/audio-engine/src/metronome/{grid,timeline,clicks,scheduler}.ts`, `src/audio-engine/src/engine.ts`
- `src/audio-engine/tests/metronome.test.ts`
- `specs/001-stemstudio-mvp/tasks.md` (T098, T099 ticked)

## Tests and coverage

`npm run test:coverage`:

```text
Test Files  7 passed (7)
     Tests  121 passed (121)
Statements   : 100% ( 455/455 )
Branches     : 100% ( 147/147 )
Functions    : 100% ( 158/158 )
Lines        : 100% ( 432/432 )
```

SC-003: "SC-003: every click of the detected grid lands within 1 ms of its beat (jittery timer)"
(21 clicks over 10 s with +/- 4 ms timer jitter against the audio clock) and "SC-003 with a loop"
(6 loop cycles, window crossing `loopEnd`) assert `|start time - grid time| < 1 ms` for every click,
no missing and no duplicate clicks, and that each wake-up only schedules clicks in `[now, now + 0.1)`.

## Quality checks

`npm run lint` clean, `npm run typecheck` clean, `npm run build` ->
`../StemStudio.Web/wwwroot/js/audio-engine.js 32.55 kB` (git-ignored).

## Dependencies added

None.

## Open issues / requests

- The metronome bypasses the master but is not metered (contract has no `metronome` meter target).

## Next owner

frontend (T100/T101 metronome controls call `setMetronome`; errors surface as
`Invalid metronome settings: ...`); audio-engine continues with T113 (polish).
