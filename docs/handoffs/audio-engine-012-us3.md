# Hand-off: audio-engine / 012 HO:us3 (T055-T058)

## What was built

- `src/practice/count-in.ts`: `countInTempo` (manual BPM: `d = 60 / bpm`, `b` = first constant-grid click >= A,
  `b = A` without a grid; else the reliable grid interval at the first click `b >= A`, the last interval after the
  last beats; null without a usable tempo), `countInPlan` (clicks at `T0 + (b - A)/r - k*d/r`, k = N..1, accent on
  beat 1 of each counted bar), `countInLead`.
- `src/practice/count-in-run.ts`: clicks started at exact context times through the metronome `ClickPlayer`
  (metronome volume, straight to the destination, also with the metronome off); a 25 ms wake-up only updates the beat
  indicator: one `OnPracticeChanged` per beat (N..1) and a final one with `active: false`.
- `src/practice/count-ins.ts`: count-in before `play()` (`T0 = now + startDelay + max(L, lead)`, the music enters one
  beat after the last click, position A until T0, `play()` resolves at once) and before every pass (`everyPass`: gap
  of N beats at the next pass's rate, position A in the gap); pause, stop, seek and loop changes cancel future clicks;
  seeks while playing never count in; works without the stretch stage.
- `src/engine.ts`: one shared `ClickPlayer` for metronome and count-in; `play()` goes through the practice controller.

Frontend requests covered in vitest (`tests/practice/count-in-engine.test.ts` "reports for the UI"): rejections start
with `Time stretch unavailable:`; one report per count-in beat plus a final `active: false`; a report at every
trainer pass start.

## Tests and coverage

```text
npx vitest run --coverage   36 files, 487 passed; 100 % statements, branches, functions, lines
```

## Decisions

ADR-021 "Implementation notes" E9 (count-in tempo details).

## Dependencies added / Docs impact

None / none beyond the UI.

## Next owner

audio-engine (T062 HO:polish).
