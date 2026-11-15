# Hand-off: frontend / 008 US1 (sheet music dialog and score items)

Tasks 008:T043 (red) and T044 (green).

## What was built

- **Client** (backend 1.7.0):
  - `GetScoreOptionsAsync(AudioSourceDto)`: `GET /api/scores/options?trackId&channel` or `?assetId`.
  - `CreateScoreAsync(CreateScoreRequest)`: `POST /api/assets/scores`.
- **`Scores/`**:
  - `ScoreTarget`: a channel of a track or a library item. It holds the subject text and `data-source`
    (`channel`/`asset`); the suggested tempo comes from the track's beat grid (used in US2).
  - `ScoreForm`: profile (the suggestion first; vocals switch "simplified" on), piano quality (the worker default),
    limit, estimate, validation and the request.
  - `ScoreText`: profile labels/values, the limit notes (contract texts, so the library needs no options call), key
    "A-flat major (83 %)" or "(your choice)", "118 BPM, 4/4", key names "B-flat major".
  - `Services/TimeText.About` ("about 31 seconds", "about 9 minutes"), shared with the lyrics estimate.
- **`Components/Scores/`**:
  - `ScoreDialog`: modal with `score-dialog` (`data-source`, marker `sheet-music`). Focus goes to the title and back
    to the opener. Escape and `score-cancel` close it. It also has `score-error`, `score-submit` and `score-saved`
    with `score-saved-open` = `library?asset=<id>`, and raises `OnSaved`.
  - `ScoreChoices`: `score-profile` (marker `sheet-music-profiles`; "Choose an instrument" when there is no
    suggestion), `score-limits` and `score-estimate` (text only, e.g. "about 31 seconds").
  - `ScoreFactsPanel` and `PdfPreview`, see the library below.
- **Track page**:
  - `Components/Shared/ChannelButtons` replaces `ExportButtons` with a `ChannelAction` (`Export` / `Score`), so the
    export row and the sheet music row share one component (no clone).
  - `score-stem-<stem>` for every standard stem; aria-label "Sheet music of bass".
  - `score-detail-<parent>-<detail>` for kept detail stems of every parent except drums (Advanced+, feature
    `score.detail-stems`).
  - `TrackDetail.Scores.cs` opens the dialog.
- **Library**:
  - A ready score item renders a second row under its row with key, tempo, limits, warnings (`<ul>`), attribution and
    the downloads `asset-download-{pdf,musicxml,midi}-<id>`.
  - It also shows `asset-preview-<id>`: an `iframe` with `src` = the pdf URL plus `?inline=true` and the title
    "Sheet music preview: <name>".
  - Score items get no `<audio>` or `asset-download-<id>`.
- **Help**: `tracks/sheet-music.md`, `tracks/sheet-music-profiles.md`.
- **CSS**: `css/components/scores.css` (tokens only).

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T043 / T044 | 9d04b5e (build fails) | 74c0c95 |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1186, Total: 1186
| StemStudio.Web | 99.42% line | 92.89% branch | 99.69% method |
```

The uncovered new paths (library-item target, drums split, attribution) belong to US3-US5.

## Quality checks

- Build: 0 warnings. `dotnet format --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.Web`: 0 errors. No new warning.
- `css_tokens_check.py`: 0 errors.

## Dependencies added

None.

## Notes for qa

- The new item id is in the `href` of `score-saved-open` (`library?asset=<uuid>`).
- `asset-preview-<id>` `src` contains `inline=true`.
- Extra id: `score-close` (Close after saving).

## Next owner

frontend (008 US2: T048/T049); qa T045.
