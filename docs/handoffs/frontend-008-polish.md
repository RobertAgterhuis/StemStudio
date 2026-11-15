# Hand-off: frontend / 008 polish (T075) and feature summary

## What was built

- **Accessibility (T075)**:
  - The PDF preview `iframe` has the title "Sheet music preview: <name>". Next to it, "Open the PDF in a new tab"
    (`.ss-pdf-open`, `target=_blank rel=noopener`, inline URL) and `asset-download-pdf-<id>` are the alternatives for
    browsers without an in-page viewer.
  - The warnings are text in a `<ul>` with a visible "Warnings" heading (`aria-labelledby`). Limits, key and tempo
    are text too.
  - Every dialog select has a `<label for>`; every input and button is named. The dialog is `aria-modal` with
    `aria-labelledby="score-title"`. Focus moves in and back through `ModalDialog`, and Escape closes.
- **Help review**: `sheet-music`, `sheet-music-profiles`, `sheet-music-key-tempo` (feature `score.key-override`),
  `sheet-music-drums` and `sheet-music-piano` (feature `score.quality`). The 007 `library` topic now covers score
  items (reviewed "008"; the 007 review test was updated for that one topic).
- **Feature summary** (see `frontend-008-{us1..us5}.md`):
  - client `GetScoreOptionsAsync` / `CreateScoreAsync`;
  - `Scores/{ScoreTarget, ScoreForm, TempoInput, ScoreText}`;
  - `Components/Scores/{ScoreDialog (+ .Drums), ScoreChoices, ScoreTempoFields, ScoreFactsPanel, PdfPreview}`;
  - shared `ChannelButtons`/`ChannelAction` (export + sheet music rows), `RangeFields` (export + score range) and
    the `ModalDialog` base;
  - track-page and library entries;
  - catalogue entries `score.detail-stems`, `score.key-override`, `score.manual-tempo`, `score.quality`,
    `score.range`;
  - `scores.css`.

## Red -> green evidence (feature 008)

| Task pair | Red | Green |
|---|---|---|
| T043 / T044 | 9d04b5e | 74c0c95 |
| T048 / T049 | dccccc4 | 7da27f6 |
| T054 / T055 | ee483f2 | 3df1d0d |
| T063 / T064 | 272eb12 | aae0aac |
| T068 / T069 | b93220f | 6174442 |
| T075 | 2b45ff9 | 74d275c |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1230, Total: 1230   (1003 before 006/008)
| StemStudio.Web | 99.53% line | 92.85% branch | 99.9% method |
```

## Quality checks

- `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors.
- `dotnet format StemStudio.slnx --verify-no-changes`: clean.
- `tools/quality/run_all.py`: 1312 files, **0 errors**, exit 0. Warnings only in older code and in test files.
- `css_tokens_check.py`: 0 raw colours.
- `src/web-interop` and `src/audio-engine` are unchanged, so ESLint/tsc have nothing new.

## Dependencies added

None.

## Open issues / requests

- **architect** (ui-test-ids 1.7.x, additive): please adopt `score-close`.
- **Deviation** (US2): a required tempo is prefilled from the track's detected beat grid, not from the mixer's
  metronome override; the dialog does not open in the mixer (details in `frontend-008-us2.md`).
- The library's limit texts are copies of the backend contract's `ScoreOptions.limits` (`ScoreText.Limit`). If the
  backend texts change, both must change. A `limits` field on `ScoreFacts` would remove the copy (architect).
- `docs/public` does not exist on this branch, so there is no manual update.

## Notes for qa

- Levels: Beginner has `score-profile`, `score-resolution`, `score-estimate`, `score-limits`, `asset-score-<id>` and,
  only with `score-tempo-required`, the three tempo fields. Beginner has no `score-quality`, `score-key-override`,
  `score-range-*` or `score-detail-*`.
- `asset-preview-<id>` is an `iframe` whose `src` ends in `files/pdf?inline=true`. The downloads are `download`
  links to the attachment URLs.

## Next owner

qa (008 T045/T050/T056/T065/T070/T076); CTO 008 T079+.
