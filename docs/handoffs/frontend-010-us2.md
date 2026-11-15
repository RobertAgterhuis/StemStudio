# Hand-off: frontend / 010 US2 (keep and reuse my lyrics)

Tasks **010:T036 -> T037** (ticked). Commits: red 009b03c -> green 0ceceb8.

## What was built

- `/lyrics` (`Components/Songs/LyricsListPage`): `lyrics-row-<id>` (title, language name, first line),
  `lyrics-song-<id>` (link `songs/new?lyricsId=<id>`), `lyrics-edit-<id>`, `lyrics-delete-<id>` (confirm
  `lyrics-delete-confirm`, cancel/Escape; message: songs keep their own copy), `lyrics-new`, `lyrics-search` (title
  and text, client side), `lyrics-empty` (no sheets / no match), `lyrics-load-error`, `lyrics-action-error`.
- `LyricsSheetDialog` (modal, marker `lyrics-format`): `lyrics-title-input` (1-80, checked before sending),
  `lyrics-language-select`, `lyrics-text-input`, `lyrics-save`, `lyrics-cancel`, `lyrics-error` with `data-field`.
  The saved sheet moves to the top of the list.
- Help topic `lyrics-library` (screen topic of `/lyrics`); nav `nav-lyrics`.

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1388, Total: 1388
| StemStudio.Web | 99.22% line | 93.53% branch |
```

## Quality checks

Build 0 warnings; format clean; `run_all.py`: 0 errors.

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

frontend continues with 010 US3 (T050/T051).
