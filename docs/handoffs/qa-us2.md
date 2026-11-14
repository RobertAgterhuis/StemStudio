# Hand-off: qa / us2 (T097)

## What was built

`e2e/tests/mixer.spec.ts` (executed in T122). Set-up: a completed `music_100bpm_20s.mp3` track
via the API, the SC-004 probe installed, mixer opened and ready (`strip-<stem>` visible,
`transport-play` enabled).

- one strip per stem (6, model `fake`) with fader 80, pan 0, mute/solo off, a meter per stem and
  `master-fader` (US2-1);
- `transport-play` (user gesture) -> `meter-master` `data-peak-db` > -60 and
  `transport-position` moves (US2-2, FR-011);
- mute `vocals` -> `meter-vocals` <= -60; solo `drums` -> `meter-bass` <= -60 while
  `meter-drums` > -60 (US2-3);
- loop 10 s -> 20 s accepted, no `loop-error` (US2-4); B <= A (20 -> 10) refused with a visible
  `loop-error`, toggle stays off (US2-5);
- **SC-004** (ADR-008 #5): fader, mute and solo each reach the engine within 50 ms. Method:
  `e2e/support/engine-probe.ts` wraps `AudioParam.setValueAtTime/setTargetAtTime/
  linearRampToValueAtTime` and the `value` setter in an init script; the action is dispatched
  inside the page and both timestamps are `performance.now()` in the page, so Playwright IPC is
  excluded. The measured values are attached as a test annotation (`sc-004 ms`). This measures
  "user action -> engine applies the AudioParam change", one level below `setChannel`, so it
  does not depend on the engine's private object graph.

## Red -> green evidence

Red against the skeleton (no mixer page yet), same harness as qa-us1.md. Commit `48e3d04`.
Green evidence follows in T122.

## Files changed

`e2e/tests/mixer.spec.ts`.

## Tests and coverage

`npx tsc --noEmit` clean, `npm run lint` clean. Coverage n/a (E2E).

## Quality checks

ESLint strict type-checked + eslint-plugin-playwright clean.

## Dependencies added

None.

## Open issues / requests

- **architect / frontend**: ui-test-ids.md does not say how toggles expose their state. The
  suite reads `aria-pressed`, then `aria-checked`, then a checkbox's `checked` (then an `active`
  class). Request: pin `aria-pressed` on `mute-*`, `solo-*`, `loop-toggle`, `metronome-toggle`.
- **frontend**: faders/pan as native `<input type="range">` are preferred (the suite sets the value
  and fires `input` + `change`); ARIA sliders are driven with arrow keys until `aria-valuenow`
  matches, which is slower.
- **frontend**: `loop-start`/`loop-end` are assumed to be inputs in seconds.
- **audio-engine**: the SC-004 probe relies on gain/pan changes going through `AudioParam`
  automation (as the contract states); keep it that way.

## Next owner

qa (T107, T108). CTO: run in T122.
