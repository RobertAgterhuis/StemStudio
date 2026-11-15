# Hand-off: frontend / 005 polish (T054)

## What was built

- Focus order: opening the band info panel focuses its heading (`tabindex="-1"`); closing it (button or Escape)
  returns focus to `band-info-open` (`BandInfoToggle`). The correction dialog captures and restores focus
  (004 `IWebInterop.CaptureFocusAsync`), starts in the title field.
- Names and alt texts: every button/link in the panel has an accessible name (tested); covers `alt="Cover of
  <title>"`, placeholder `role="img"` + `aria-label="No picture for <title>"`, initials `aria-hidden`.
- Keyboard: `band-info-open` is a native button with `aria-expanded`/`aria-controls`; the file input of the own
  picture keeps native keyboard access with a visible focus ring on its label (`:focus-within`).
- Contrast: all new styles use 004 tokens only (`ink`, `muted`, `control-border`, `divider`, stem tones for the
  placeholder ring/initials, which meet >= 4.6:1 on `surface`/`raised` per design-spec 2.1); raw-colour guard 0.
- Status is never colour-only: `metadata-status` and `band-info-status` carry text.
- Help review: topics `artwork`, `band-info`, `online-lookups` reviewed "005", <= 250 words; search finds them for
  "band", "wikipedia", "cover", "album art", "offline".

## Feature 005 summary (frontend)

| Pair | Red | Green |
|---|---|---|
| T028 / T029 | e91bf93 | 944271f |
| T041 / T042 | 1d0f157 | 2e8aa74 |
| T048 / T049 | 6339aa7 | 49301c8 |
| T054 | 7839ed8 | 3303b7b |

## Tests and coverage

```text
dotnet build StemStudio.slnx -c Release          0 Warning(s), 0 Error(s)
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 908, Skipped: 0, Total: 908
| StemStudio.Web | 99.27% line | 93.07% branch | 100% method |
```

## Quality checks

`dotnet format StemStudio.slnx --verify-no-changes` clean; `tools/quality/run_all.py`: 975 files, 0 errors,
243 warnings; `css_tokens_check.py`: 0 errors, 0 warnings.

## Dependencies added

None.

## Open issues / requests

- ui-test-ids next minor (architect): adopt the additive ids `band-info-close`, `band-info-album`,
  `band-info-error`, `metadata-correct-cancel`, `metadata-correct-error`, `artwork-progress`.
- help-content.md areas are fixed; 005 topics live in `tracks` and `start`.
- FR-015 vs ui-test-ids: `band-info-correct` at Beginner follows ui-test-ids.

## Next owner

qa (005:T055), CTO (005:T058 merge), frontend continues with 007.
