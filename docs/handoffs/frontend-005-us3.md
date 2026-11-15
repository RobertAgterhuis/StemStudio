# Hand-off: frontend / 005 US3 (own picture)

Tasks 005:T048 (red) and T049 (green).

## What was built

- Client: `SetArtworkAsync(trackId, UploadFile)` (`PUT /api/tracks/{id}/artwork`, streamed multipart field `file`)
  and `ResetArtworkAsync(trackId)` (`DELETE`), both returning the track.
- `Api/ArtworkRules.cs`: `accept="image/jpeg,image/png,image/webp"`; client checks by browser MIME type
  ("Only JPEG, PNG and WebP pictures are supported."), empty file, > 10 MiB ("The picture is larger than 10 MB
  (10,485,760 bytes)."). Exactly 10 MiB passes. The API/worker stay authoritative (400/413/415/422 details shown).
- `Components/Artwork/ArtworkActions.razor` (+ code-behind), on the track page header, wrapped in the new catalogue
  feature `artwork.own-picture` (Advanced, Expert): visually hidden `InputFile` `artwork-upload-input` with a visible
  "Choose picture" label (focus ring via `:focus-within`), upload starts on selection, `artwork-progress`
  ("Uploading picture...", `role="status"`, input disabled), `artwork-error` (`role="alert"`), `artwork-reset`
  "Use automatic picture" only while `artwork.source = user`. Marker `data-help-id="artwork-actions"` (topic `artwork`).
- The page takes the returned track at once, so `track-cover` switches to the new source without a reload.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T048 / T049 | 6339aa7 (build fails: ArtworkRules, actions, client methods missing) | 49301c8 |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 900, Skipped: 0, Total: 900
| StemStudio.Web | 99.27% line | 93.05% branch | 100% method |
```

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean; `run_all.py` 0 errors (test-file duplication warnings
for the shared level render helper only); `css_tokens_check` 0.

## Dependencies added

None.

## Notes for qa

- Extra id `artwork-progress` (additive). `not_a_picture.png` passes the client check (its MIME type is image/png)
  and shows the server's 400 detail; `unsupported_picture.gif` is refused client-side; both show `artwork-error`.

## Next owner

frontend (005 polish T054), qa (005:T050).
