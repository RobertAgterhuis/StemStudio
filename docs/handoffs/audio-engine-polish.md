# Hand-off: audio-engine / polish

## What was built

T113: TSDoc on every export (enforced by `eslint-plugin-jsdoc`; public class members documented
too) and a SIG pass over `src/audio-engine/src`:

- Every unit is <= 15 LOC (checked with `max-lines-per-function` 15 on top of the configured
  hard limit of 30; `Transport.setLoop` and the constant click grid were split to get there),
  cyclomatic complexity <= 5, <= 4 parameters (parameter objects where needed), max depth 3.
- No duplicated blocks of 6+ lines in `src` or `tests` (jscpd 5.4.3 via `npx`, not a dependency).
- Dependencies are one-way: `engine -> {transport, mixer, meters, callbacks, loop, metronome/*,
  stem-loader}`; `transport -> {loop, stem-loader}`; `mixer -> channel`; `metronome/* ->
  {transport (types), channel, grid, timeline}`; `callbacks -> transport (type)`. No cycles.
- Audible timing: every `start()` time comes from `AudioContext.currentTime` (stems:
  `currentTime + startDelay`; clicks: anchor + grid). `setInterval` is used only to wake the
  metronome scheduler and for the 10 Hz position report to .NET.

Checkpoint history (red -> green):

| Checkpoint | Tasks | Red | Green | Hand-off |
|---|---|---|---|---|
| foundational | T037 | (test doubles) | c4c82f6 | `audio-engine-foundational.md` |
| us2 | T082-T090, T137 | dbf7307, 80130bd, 89fe9be, 82f947e, 827fd9c | 0fe4818, 3ea7a5e, deabaf2, fa84204, 5f0efb1 | `audio-engine-us2.md` |
| us3 | T098, T099 | de83a18 | 5992d6f | `audio-engine-us3.md` |

## Files changed

- `src/audio-engine/src/{transport.ts,metronome/grid.ts}` (unit-size refactors, no behaviour change)
- `src/audio-engine/tests/metronome.test.ts` (duplicate block extracted into helpers)
- `specs/001-stemstudio-mvp/tasks.md` (T113 ticked)

## Tests and coverage

`npm run test:coverage` (exit 0):

```text
Test Files  7 passed (7)
     Tests  121 passed (121)
Statements   : 100% ( 456/456 )
Branches     : 100% ( 147/147 )
Functions    : 100% ( 160/160 )
Lines        : 100% ( 432/432 )
```

## Quality checks

- `npm run lint`: exit 0, no findings.
- `npm run typecheck`: exit 0.
- `npm run build`: `../StemStudio.Web/wwwroot/js/audio-engine.js 32.69 kB | gzip 9.94 kB`
  (+ source map), both git-ignored via `src/StemStudio.Web/wwwroot/js/.gitignore`.
- `npx jscpd --min-lines 6 src tests`: 0 clones.
- `tools/quality/sig_check.py` is not present in this worktree (quality-gate T005); please run it
  on `src/audio-engine/src` after the merge.

## Dependencies added

None (package.json and lock file unchanged).

## Open issues / requests

Behaviour the frontend should know (all within contract v1.0.0, no `types.ts` change requested):

1. `setLoop` validates disabled regions as well; to switch looping off, send the last valid region
   with `enabled: false` (a `{0, 0, false}` region is rejected as `B <= A`).
2. `seek(t)` with an enabled loop that does not contain `t` lands on the loop start.
3. `setChannel(stem)` / `attachMeter(stem)` require a resolved `loadTrack`; `attachMeter("master")`
   works any time; meter bindings survive a new `loadTrack`.
4. `loadTrack` resets the loop and all channels (volume 1, pan 0, no mute/solo); re-apply the mix
   afterwards. Metronome settings are kept and re-evaluated against the new beat grid.
5. Non-finite numbers (volume, pan, seek position) throw `Invalid <name>: ...`; out-of-range
   volume/pan are clamped.

## Next owner

CTO (merge, T119); frontend for the interop wrapper; qa for the browser SC-002 check (T124).
