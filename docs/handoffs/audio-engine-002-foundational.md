# Hand-off: audio-engine / 002-foundational

## What was built

Audible-level gate of engine contract 2.0.0 (T038, T039):

- `src/gate.ts`: `parentOf(id)` (`drums.snare` -> `drums`, standard stem -> null),
  `isHidden(id, expanded)` (an expanded parent, or a detail of a collapsed parent),
  `anyAudibleSolo(views)`, `levelGate(view, anySolo)` =
  `hidden || mute ? 0 : (anySolo && !solo ? 0 : 1)`, `gatesOf(views)`.
- `src/mixer.ts`: channels keyed by `ChannelId`; `build(ids)` starts every parent collapsed, so
  detail channels start at gain 0 (set as `value`, no automation); `setExpanded(parent, expanded,
  at)` re-gates every channel with ramps starting at the one context time `at`;
  `isExpanded(parent)`; `dispose()` disconnects all channels and collapses all parents.
  Without detail channels the behaviour is exactly the v1 rule (v1 tests unchanged and green).
- `src/channel.ts`: `apply(gate, at = currentTime)` (was `apply(anySolo)`), `initGate(gate)`.
- Solo/mute flags of hidden channels are kept and become effective again on the next
  expand/collapse (US2 AS3).

Red -> green: T038 `6255fc9` -> T039 `b1783a2`.

## Files changed

- `src/audio-engine/src/{gate.ts (new), mixer.ts, channel.ts}`
- `src/audio-engine/tests/{gate.test.ts (new), mixer.test.ts, mocks/mixer-rig.ts (new)}`
- `specs/002-detail-separation/tasks.md` (T038, T039 ticked)

## Tests and coverage

`npm run test:coverage` (Vitest 5.1.0, v8):

```text
Test Files  8 passed (8)
     Tests  136 passed (136)
Statements   : 100% ( 480/480 )
Branches     : 100% ( 160/160 )
Functions    : 100% ( 174/174 )
Lines        : 100% ( 454/454 )
```

## Quality checks

- `npx tsc --noEmit`: clean. `npx eslint .`: clean. `npm run build`: OK (`audio-engine.js` 35.73 kB).
- `uv run --project tools/quality python tools/quality/run_all.py src/audio-engine`:
  `30 files, 521 units, 0 clones, 0 exemptions, 0 errors, 12 warnings -> exit 0`
  (warnings: pre-existing test unit sizes only; the gate/mixer test clone was removed via
  `tests/mocks/mixer-rig.ts`).

## Dependencies added

None.

## Open issues / requests

None. `setExpanded` on the engine still rejects; it is wired in T058/T059 (US2).

## Next owner

audio-engine (T058 -> T061, US2); frontend can rely on the gate semantics above.
