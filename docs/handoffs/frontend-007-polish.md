# Hand-off: frontend / 007 polish (T067)

## What was built

- Status never colour-only: `asset-status-<id>` is text ("Queued", "Rendering 42 %", "Ready", "Failed"), rendering
  items are a `progressbar` with `aria-valuenow`; failed items add the readable `asset-error-<id>`; the gain note is text.
- Keyboard use of dialogs: export, rename and correction dialogs capture focus on open and restore it on close
  (`IWebInterop.CaptureFocusAsync`), Escape closes, Enter in a text field confirms (not on buttons), other keys do
  nothing. Every control in the export dialog and the library rows has an accessible name (tested); `<audio>` has
  `aria-label="Play <name>"`, download/rename/delete/retry name the item.
- Command palette: "Go to Library" (`command-go-library`, additive to the ui-test-ids `<commandId>` list).
- Help texts final: `library` (screen topic `/library`), `export-dialog` ("Export: save to library"), `export-formats`
  ("File formats"), `export-range`, `export-clipping`; all reviewed "007", <= 250 words, found first for "export",
  "what i hear", "flac", "download", "lowered", "loop region".
- Test stability: `PageTestContext` sets bUnit's `DefaultWaitTimeout` to 10 s. Polling tests release
  `ManualPollingTimer` continuations on the thread pool; under a fully parallel run the pool was sometimes busy for
  more than 1 s (seen in 004's `TrackDetailActionsTests.Retry_EndingFailedAgain_...` and in new polling tests).
  6 consecutive full runs green afterwards.

## Feature 007 summary (frontend)

| Pair | Red | Green |
|---|---|---|
| T037+T039 / T038+T040 | 6560763 | 9d058dd |
| T044 / T045 | dc8f58a | 81ee4eb |
| T051 / T052 | c970e7f | 3a59849 |
| T056 / T057 | 91c3e68, 458e90c | 39fd8fa |
| T061 / T062 | 1db9996 | 33dfbee |
| T067 | 89da519 | fac6c4b |

New catalogue features (design-spec 5 extension, all Advanced+): `artist.details`, `artwork.own-picture`,
`export.format-details`, `export.selected-channels`, `library.filters`, `export.range`, `export.stem-pack`.

## Tests and coverage

```text
dotnet build StemStudio.slnx -c Release                0 Warning(s), 0 Error(s)
dotnet format StemStudio.slnx --verify-no-changes      clean
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1003, Skipped: 0, Total: 1003
| StemStudio.Web | 99.21% line | 92.96% branch | 99.88% method |
```

`src/web-interop` was not changed (no new interop was needed), so its Vitest/ESLint/tsc gates are unaffected.

## Quality checks

`tools/quality/run_all.py`: 1009 files, 0 errors, 258 warnings; `css_tokens_check.py`: 0 errors, 0 warnings.

## Dependencies added

None.

## Open issues / requests

- ui-test-ids next minor (architect): additive ids `export-close`, `export-error-hint`, `library-error`,
  `library-action-error`, `asset-rename-cancel`, `asset-rename-error`, command id `go-library`.
- The library loads the newest 200 items (`MaxAssetPageSize`) and filters on the client; paging is not built.
- help-content.md areas are fixed by the schema; 007 topics use `tracks` (dialog, formats, range) and `mixes`
  (library, clipping).

## Next owner

qa (007:T068), CTO (integration wave 007:T071).
