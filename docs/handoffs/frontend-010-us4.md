# Hand-off: frontend / 010 US4 (use the song for practice)

Task **010:T055** (ticked; one red -> green pair inside the frontend sequence). Commits: red 49310d2 -> green
efd35b0.

## What was built

- Track page (`TrackOrigin`): `track-vocal-badge` on generated tracks with `origin.vocal` (`data-vocal`; "No vocals",
  "AI voice" or "AI voice: <profile>"), next to `track-origin-badge`; `track-lyrics-link` ("Open the lyrics",
  `lyrics?sheet=<lyricsSheetId>`) while the track has a lyrics sheet.
- `/lyrics?sheet=<id>` highlights that row (`aria-current="true"`).
- Import, separation and the mixer of a song take are the 009 flow (`take-import-*` opens the new track).

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1433, Total: 1433
| StemStudio.Web | 99.11% line | 93.62% branch |
```

## Quality checks

Build 0 warnings; format clean.

## Dependencies added

None.

## Open issues / requests

- qa (`song.spec.ts` "imported song take"): `track-lyrics-link` points at `/lyrics?sheet=<id>` (the href contains the
  sheet id, as the spec expects).

## Next owner

frontend continues with 010 polish (T058).
