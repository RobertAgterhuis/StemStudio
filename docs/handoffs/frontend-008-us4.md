# Hand-off: frontend / 008 US4 (drum notation)

Tasks 008:T063 (red) and T064 (green).

## What was built

- When `ScoreOptions.drumsRequireSplit` is true, the dialog shows `score-drums-split-required` (marker
  `sheet-music-drums`) in place of the choices, with "Drum notation needs the drum detail stems: split the drums
  first." and `score-drums-split-start`. `score-submit` stays disabled.
- `score-drums-split-start` calls the existing `StartDetailAsync` (002 `POST .../detail`). The notice then says
  "The drums are being split into kick, snare, toms, hi-hat and cymbals. Open sheet music again when the detail
  stems are ready." A refusal (e.g. 409) appears in `score-error` and keeps the button.
- After the split the drums profile is a normal choice. No key override is offered or sent for drums.
- Single drum pieces have no `score-detail-drums-*` buttons (US1).
- Help: `tracks/sheet-music-drums.md`. Its keywords avoid the piece names so the 002 help search for "kick" still
  finds `detail-outcomes` first.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T063 / T064 | 272eb12 (4 failing) | aae0aac |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1215, Total: 1215
| StemStudio.Web | 99.49% line | 92.82% branch | 99.8% method |
```

## Quality checks

- Build: 0 warnings. `dotnet format --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.Web`: 0 errors.

## Dependencies added

None.

## Notes for qa

- Closing the dialog on the track page reloads the track and polls it, so a queued drums split shows at once
  (added in the US5 commit 6174442).

## Next owner

frontend (008 US5: T068/T069); qa T065.
