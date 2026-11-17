# Hand-off: audio-engine / 012 HO:us2 (T048-T051)

## What was built

- `src/practice/trainer.ts`: `SpeedTrainer` (off / waiting / running / target; pass 1 at `startPercent`, step after
  every `passesPerStep` completed passes, capped at the target; pause keeps, stop/loadTrack/loop start-end change
  and any trainer field change reset; `save`/`restore` to undo a pass that was created but never heard).
- `src/practice/passes.ts`: `PassScheduler` (`MIN_PASS_SECONDS` 0.5, look-ahead 0.4 s, 25 ms wake-up). Pass k+1
  starts in source time exactly where pass k stops (`S_{k+1} = S_k + (B - A) / r_k + gap`), its pitch is sent for
  its first input frame, heard anchor `S + L(p_{k+1})`.
- `src/practice/pass-mode.ts`: when passes are used, what each pass plays, trainer status of the pass heard now,
  transpose live during passes.
- `src/transport.ts`: `PassSpec`, `PassPlanner`, `setPlanner`, `passes`, `launch`; sources with a duration and
  `loop = false`; late detail sources get the rest of the running pass; `setLoop` in pass mode only stores the region
  (the controller restarts the passes).
- `src/engine.ts`: `setLoop` notifies the practice controller.

## Tests and coverage

```text
npm run lint / typecheck              clean
npx vitest run --coverage             33 files, 459 passed
  Statements 100 %  Lines 100 %  Functions 100 %  Branches 99.28 % (the remaining branches are the
  count-in-every-pass paths, covered with US3)
```

## Decisions / deviations (ADR-021 "Implementation notes" at HO:polish)

- E4 Pass k+1 starts gapless in **source** time; the heard anchor is `S + L(p_{k+1})`, so a trainer step moves the
  heard pass start by the difference of the two content offsets (1-2 ms for a 5 % step) instead of overlapping or
  gapping the audio.
- E5 While passes play, speed changes of the settings (and disabling the trainer) apply from the next pass boundary;
  the transpose applies live (pitch only; the heard anchor is not shifted by the c(p) difference, <= 2 ms per
  semitone for the rest of that pass).
- E6 Enabling the trainer while playing, and every loop change while passes play, restart at the heard position
  (one gap of `startDelay + L`).
- E7 No `stage.reset()` between passes, also not after a count-in gap: the gap carries silence through the stage and
  the end of the previous pass must still be heard; a reset would cut it.

## Dependencies added

None.

## Docs impact

None beyond the UI (frontend).

## Open issues / requests

None.

## Next owner

audio-engine (US3: T055-T058).
