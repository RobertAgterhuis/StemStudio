# Hand-off: audio-engine / 006 US3 (T051, T052)

## What was built

- `src/karaoke/motion.ts` - `MotionPreference`: `KaraokeOptions.reducedMotion` when given
  (the media query is then ignored), else `matchMedia('(prefers-reduced-motion: reduce)')`
  with its `change` events followed at runtime; `false` without `matchMedia`.
- `src/karaoke/view.ts` - reduced motion: `.karaoke-ball` gets `hidden` and no transform
  writes; the current word keeps `data-state="current"` (the highlight). Switching back places
  the ball at once and draws one frame.
- `src/karaoke/layout.ts` - `LayoutWatcher` also observes the host's `class` and `data-size`
  attributes (`MutationObserver`): a change re-measures (outside the frame callback), moves
  the ball and draws one frame. Other host attributes (`data-sync`, the engine's own) do not.

Red -> green: `d5d3cf6` (failing tests) -> `c5a787d` (implementation).

## Files changed

- `src/audio-engine/src/karaoke/{motion.ts (new), view.ts, layout.ts, index.ts}`
- `src/audio-engine/tests/karaoke/motion.test.ts` (new)
- `specs/006-synced-lyrics-karaoke/tasks.md` (T051, T052 ticked)

## Tests and coverage

```text
npm run test:coverage   20 files, 288 passed
Statements 100% (922/922)  Branches 100% (357/357)  Functions 100% (309/309)  Lines 100% (884/884)
```

## Quality checks

`npm run lint`, `npm run typecheck` clean; `npm run build` OK (67.7 kB); `sig_check.py
src/audio-engine/src` 0 errors, 0 warnings.

## Dependencies added

None.

## Open issues / requests

- **frontend (T053/T054)**: put the text size on the host as a class (e.g. `karaoke-size-l`)
  or `data-size="s|m|l"`; either triggers the re-measure. Style the reduced-motion highlight on
  `.karaoke-word[data-state=current]` (the ball is `hidden`); pass `reducedMotion` only when the
  app has its own setting, otherwise omit it so the OS preference is followed live.

## Next owner

frontend (006 T053/T054); audio-engine T059 (polish).
