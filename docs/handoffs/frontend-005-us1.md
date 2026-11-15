# Hand-off: frontend / 005 US1 (song artwork)

Tasks 005:T028 (red) and T029 (green), built on the complete 004 design system.

## What was built

- `Services/ArtworkDisplay.cs` + `ArtworkVariant` (Cover 600 px / Thumb 160 px): the
  `data-artwork-source` value (`none` without a picture), the browser URL (relative API URL resolved
  against `StemStudio:PublicApiBaseUrl`, like the stems), initials (first letter or digit of the
  first two words, `?` otherwise) and a stable placeholder tone (FNV-1a of the title -> one of the
  six stem colour tokens, never a raw colour).
- `Components/Shared/TrackArtwork.razor`: `img` with `src`, `alt="Cover of <title>"`, `width`/`height`,
  `loading="lazy"`, `decoding="async"`, `data-testid`, `data-artwork-source` and optional
  `data-help-id`, all on the `img` itself (qa-005-pre004 assumption). `@onerror` switches to the
  placeholder for that artwork version; a new version is tried again.
- `Components/Shared/ArtworkPlaceholder.razor`: `role="img"`, `aria-label="No picture for <title>"`,
  initials in an `aria-hidden` span, class `ss-art-tone-<stem>`.
- Placement: thumbnail column `track-thumb-<trackId>` in `TrackTable.razor`, `track-cover` in the
  track detail header (also while processing; polling picks up a picture that arrives later),
  `mixer-cover` in the mixer header. Markers `data-help-id="artwork"` on the cover and the mixer cover.
- Style sheet `wwwroot/css/components/artwork.css` (tokens only), registered in `App.razor`
  before `dialog`.
- Help topic `Help/topics/tracks/artwork.md` (id `artwork`, area `tracks`, reviewed `005`).

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T028 / T029 | e91bf93 (build fails: components missing) | 944271f |

## Files changed

- `src/StemStudio.Web/Services/ArtworkDisplay.cs`, `Components/Shared/{TrackArtwork,ArtworkPlaceholder}.razor` (new)
- `src/StemStudio.Web/Components/Pages/{TrackTable,TrackDetail}.razor`, `Components/Mixer/Mixer.razor`, `Components/App.razor`
- `src/StemStudio.Web/wwwroot/css/components/artwork.css`, `Help/topics/tracks/artwork.md` (new)
- `tests/StemStudio.Web.Tests/Artwork/{TrackArtworkTests,ArtworkPlacementTests,ArtworkHelpTests}.cs`, `Fakes/ArtworkData.cs` (new)

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 824, Skipped: 0, Total: 824
| StemStudio.Web | 99.27% line | 92.7% branch | 100% method |
```

## Quality checks

`dotnet build` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` clean;
`css_tokens_check.py`: 0 errors, 0 warnings; `sig_check.py src/StemStudio.Web tests/StemStudio.Web.Tests`:
0 errors, no warning in the new files.

## Dependencies added

None.

## Open issues / requests

- qa: the help areas are fixed by `help-topic.schema.json` 1.0.0; the 005 topics use `tracks`
  (artwork, band info) and `start` (online lookups).

## Next owner

frontend (005 US2), qa (005:T030).
