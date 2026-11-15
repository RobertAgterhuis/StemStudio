# Hand-off: audio-engine / 002-polish

## What was built

T085: memory release and TSDoc.

- `Engine.dispose()` releases everything the engine holds, in this order:
  - **Transport** (`transport.dispose()`): stops the sources, sets `buffer = null` on every
    running source, and drops all decoded standard and detail buffers (duration 0).
  - **Groups** (`StemGroups.dispose()`): forgets every group, cached load and expansion. A load
    still in flight rejects and attaches nothing.
  - **Mixer** (`Mixer.dispose()`): disconnects every standard and detail channel.
  - **Rest**: notifier, meters and metronome are disposed, then the AudioContext is closed (as
    in 001).
- TSDoc is complete for the new public units (`gate.ts`, `groups.ts`, `Transport.addBuffers`,
  `Transport.startTimeAfter`, `Mixer.setExpanded`, `Mixer.isExpanded`, `Mixer.dispose`,
  `StemGroups.dispose`, `NamedSource`, `ChannelBuffers`, `GroupLoader`). I also updated the
  module header of `engine.ts`. The ESLint jsdoc rules are clean.

Red -> green: T085 `1076c07` -> `00cd2de`.

All 002 audio-engine commits:

| Commit | Content |
|---|---|
| `6255fc9` | test: gate (T038 red) |
| `b1783a2` | feat: gate + mixer (T039 green) |
| `3277233` | docs: HO:foundational |
| `4b419f3` | test: groups (T058 red) |
| `ff6bfe3` | feat: groups (T059 green) |
| `d54c497` | test: SC-004 with groups (T060, green on first run; mutation-checked) |
| `6722170` | docs: HO:us2 (T061 verified, no code change) |
| `1076c07` | test: dispose release (T085 red) |
| `00cd2de` | feat: dispose release + TSDoc (T085 green) |

## Files changed

- `src/audio-engine/src/{transport.ts, groups.ts, engine.ts}`
- `src/audio-engine/tests/{groups.test.ts, transport.test.ts}`
- `specs/002-detail-separation/tasks.md` (T085 ticked)

## Tests and coverage

`npm run test:coverage` (Vitest 5.1.0, v8):

```text
Test Files  9 passed (9)
     Tests  160 passed (160)
Statements   : 100% ( 565/565 )
Branches     : 100% ( 188/188 )
Functions    : 100% ( 203/203 )
Lines        : 100% ( 534/534 )
```

## Quality checks

- `npx tsc --noEmit` and `npx eslint .`: clean.
- `npm run build`: OK (`audio-engine.js` 42.03 kB, gzip 12.60 kB).
- `uv run --project tools/quality python tools/quality/run_all.py src/audio-engine`:
  `32 files, 621 units, 0 clones, 0 exemptions, 0 errors, 13 warnings -> exit 0`. The warnings
  are all pre-existing 001 test-unit sizes plus the `detail` dependency-map note. No production
  unit warnings.
- Audible timing: every start and swap time is computed from `AudioContext.currentTime` and the
  shared anchor. Timers only drive `OnPositionChanged` (UI) and wake the metronome scheduler.

## Dependencies added

None.

## Open issues / requests

See `audio-engine-002-us2.md` (minor <= 10 ms swap/setChannel edge case; frontend must call
`setExpanded` after `loadTrack` for the saved `Expanded` parents).

## Next owner

quality-gate (T088), qa (T086). CTO for consolidation.
