# Hand-off: qa / us4 (T107, T108)

## What was built

- `e2e/tests/main-flow.spec.ts` (T107, SC-001, SC-005, US4): upload `music_100bpm_20s.mp3` in the
  UI -> completed -> `open-mixer` -> apply the "Practice" settings (fader, pan, mute, solo,
  master, metronome on + BPM 120 + offset 50 ms, loop 10-20 s on) -> `unsaved-indicator` visible
  -> save "Practice <runId>-<project>" -> reload -> load from `/mixes` -> every control equals
  the saved value -> save-as "Practice 2 ..." -> duplicate -> "(copy)" exists -> delete it with
  `confirm-dialog-ok` -> gone, track still completed.
- The same spec has a `@restart` test: T122 runs `npm test`, then `docker compose restart api
  web`, then `npm run test:restart`, which reads `e2e/.state/<project>-mix.json` (written by the
  main flow), loads the mix and asserts every setting (G2, SC-005).
- `e2e/tests/main-flow.real.spec.ts` (T108, `@real`, `npm run test:real` only): the main flow on
  `music_100bpm_20s.flac` with real Demucs; accepts model `htdemucs_6s` (6 stems) or `htdemucs`
  (4 stems); timeout 15 min job + 5 min; records model and processing seconds as a test annotation.
- `e2e/support/main-flow.ts`: shared steps (import and open mixer, save new mix, reload and
  verify, hand-over state) so the three flows do not duplicate code. The Practice settings use
  only stems that exist in both models (vocals, drums, bass, other).

## Red -> green evidence

Red against the skeleton (no UI yet), same harness as qa-us1.md. Commits `1a7edca` (T107),
`d817f0e` (T108). Green evidence follows in T122/T123.

## Files changed

`e2e/support/main-flow.ts`, `e2e/tests/main-flow.spec.ts`, `e2e/tests/main-flow.real.spec.ts`,
`e2e/package.json` (`lint` = `eslint . --max-warnings 0`).

## Tests and coverage

```text
$ npx playwright test --list
Total: 40 tests in 5 files          (20 per browser: chromium, firefox)
$ npx playwright test --list --grep-invert "@real|@restart"   -> Total: 36 tests in 4 files  (npm test)
$ npx playwright test --list --grep @real                      -> Total: 2 tests in 1 file
$ npx playwright test --list --grep @restart                   -> Total: 2 tests in 1 file
```

`npx tsc --noEmit` clean; `npm run lint` (0 warnings allowed) clean. Coverage n/a (E2E).

## Quality checks

`uvx lizard -l typescript -C 5 -L 15 -a 4 -w e2e/...`: no unit above CC 5 or 4 parameters; five
units exceed the 15-line soft limit but stay under the hard 30 (the three in-page functions of
`engine-probe.ts` are serialised by Playwright and cannot call module helpers; two test bodies
are linear `test.step` sequences).

## Dependencies added

None.

## Open issues / requests

- **frontend**: `mix-save` on a mix that has never been saved must open the name dialog
  (`mix-name-input` + `mix-save-confirm`); on a loaded mix it saves directly (PUT). Saving clears
  `unsaved-indicator`. `mix-load-<id>` navigates to `/tracks/{id}/mixer?mixId=<id>`.
- **frontend**: `metronome-bpm`, `metronome-offset`, `loop-start`, `loop-end` are read with
  `toHaveValue` (input elements); BPM as an integer string ("120"), offset in ms ("50").
- **devops / CTO**: the real run needs a fresh data volume or at least a source not processed by
  the fake separator (the FLAC fixture is used for that reason).

## Next owner

qa (T110 audit, T118). CTO: T122/T123.
