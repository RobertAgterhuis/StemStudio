# Hand-off: audio-engine / 012 HO:us1 (T033-T034)

## What was built

`setPractice` / `getPracticeStatus` / `OnPracticeChanged` are real (the Phase 0 stubs and
`PRACTICE_NOT_IMPLEMENTED` are gone).

- `src/practice/settings.ts`: `DEFAULT_PRACTICE`, `validatePractice` ->
  `Invalid practice settings: <field> <reason>`, e.g. `speedPercent must be an integer 50..120 (got 49)`,
  `trainer.targetPercent must be above the start speed (start 90, target 90)`, `trainer must be an object`.
- `src/practice/tempo.ts`: `CHANGE_LEAD_SECONDS`, `liveChangeTimes` (U, frame(U), `T + c(p)/sr`, `T + c(pOld)/sr`).
- `src/practice/controller.ts`: stretch state machine of data-model 3.1; the stage is loaded once (concurrent calls
  share the load), inserted between the master gain and the master meter, sources restarted at the heard position;
  live changes at one U on every source (detail sources included) with the pending change replaced at the same U;
  the stage stays engaged at 100 %/0 st until the next restart at neutral settings (then `bypass`, the 2.1.0 graph);
  load failure -> reject, 100 %, 0 st, trainer off, count-in applied, one retry per `loadTrack`; processor failure
  after ready -> `unavailable`, bypass, restart at the heard position; the stage is reset on pause/stop and before
  every start; underruns since `loadTrack`, polled every 100 ms while playing.
- `src/callbacks.ts` `PracticeNotifier`: `OnPracticeChanged` at most 10 Hz (immediate + one trailing report).
- `src/mixer.ts` `setStage`; `src/transport.ts` `cancelPending`, restart hook runs before the timing is read.
- `countIn.available` / `beatsTotal` are reported (manual BPM or a reliable grid with >= 2 beats); the count-in
  itself comes with US3 (T055-T058). `trainer` reports `off` until US2.

## Tests and coverage

```text
npm run lint / typecheck / build      clean (audio-engine.js 95 KB, time-stretch-processor.js 14 KB)
npx vitest run --coverage             30 files, 427 passed
  Statements 100 % (1496/1496)  Branches 100 % (563/563)  Functions 100 % (498/498)  Lines 100 % (1437/1437)
```

`tests/practice-contract.test.ts` (architect skeleton): the stub assertion was replaced by the real behaviour
(neutral settings resolve without loading anything), as announced in the skeleton ("replaced in T034").

## Decisions / deviations (recorded in ADR-021 "Implementation notes" at HO:polish)

- E1 Stage position: master gain -> **stage** -> master meter (contract graph: sum -> stage -> master gain). Same
  sound (linear chain), the 2.1.0 graph and every pre-012 graph test stay literally unchanged; only master-fader
  moves are heard L later while engaged.
- E2 The stage is also reset on pause and stop, so no 66-147 ms of music trails after pressing pause.
- E3 No 10 ms fade on the engage restart (the same hard stop/start as every seek since 001).

## Dependencies added

None.

## Docs impact

None beyond HO:foundational (the UI comes from the frontend lane).

## Open issues / requests

- frontend: `setPractice` rejects with `Invalid practice settings: ...` or `Time stretch unavailable: <reason>`;
  in the second case `getPracticeStatus().stretchReason` holds the reason without the prefix.

## Next owner

audio-engine (US2: T048-T051).
