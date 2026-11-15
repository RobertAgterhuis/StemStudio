# Hand-off: frontend / 007 US1 (raw stem export and the library)

Tasks 007:T037/T039 (red, one commit) and T038/T040 (green, one commit).

## What was built

- Client (backend 1.5.0): `ListAssetsAsync` (`GET /api/assets?limit=200`), `GetAssetAsync`, `CreateAudioExportAsync`,
  `CreateStemPackAsync`, `RenameAssetAsync` (PATCH), `DeleteAssetAsync`, `RetryAssetAsync`.
- `Export/ExportTarget.cs` (kind stem/mix/stem-pack -> `data-mode`), `Export/ExportForm.cs` (choices -> request; format
  details null unless chosen = backend default), `Export/RangeInput.cs` (used from US4).
- `Components/Export/ExportButtons.razor`: "Save to library:" row under the stem tabs (`export-stem-<stem>`) and under
  each split parent's detail stems (`export-detail-<parent>-<detail>`, every kept stem incl. `rest`); native buttons,
  `aria-label="Save <name> to the library"`, marker `export-actions`. Only on completed tracks.
- `Components/Export/ExportDialog.razor` (+ code-behind): modal (`export-dialog`, `data-mode`, `data-help-id`), focus to
  its title and back to the opener, Escape/`export-cancel` close; `FormatPicker` (`export-format` native select,
  FLAC preselected, marker `export-formats`; `export-bit-depth` / `export-mp3-bitrate` behind catalogue feature
  `export.format-details`, Advanced+); `export-name` (empty = default name; > 120 -> readable error); `export-error`
  shows the problem detail (409 disk space / source-unavailable, 422, 503); `export-saved` with
  `export-saved-open` = `library?asset=<id>` (the href carries the asset id) and `export-close`.
- `/library` (`Components/Library/LibraryPage.razor`, rail link `nav-library`): `library-total-size` (always rendered,
  `ByteSize`: "0 B", "1.5 KB", "312 MB"), newest first, `asset-row-<id>` (`data-status`, `aria-current` for
  `?asset=`), `asset-name-`, `asset-contents-` + "FLAC 16-bit · 3:33 · 12 MB", `asset-source-` (link while the track
  exists, else "(deleted)"; `data-track-available`), `asset-status-` (`role="progressbar"`, `aria-valuenow` while
  rendering), `asset-play-` (`<audio controls preload="none" src=...?inline=true>`), `asset-download-` (`download`
  link) when ready; `library-empty`; `library-error`. Polls while an item is queued/rendering.
- `Components/PollingComponent.cs`: base of the track page, the band info panel and the library (one `PollLoop`,
  cancellation on dispose) - removes the polling duplicates the quality gate reported.
- Help topics `mixes/library.md` (screen topic `/library`), `tracks/export-dialog.md`, `tracks/export-formats.md`
  (feature `export.format-details`). Style sheet `css/components/library.css`.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T037+T039 / T038+T040 | 6560763 (build fails) | 9d058dd |

## Tests and coverage

```text
Passed! - Failed: 0, Passed: 949, Total: 949
| StemStudio.Web | 98.11% line | 90.09% branch | 98.63% method |   (mix/range parts of ExportForm wait for US2/US4)
```

## Quality checks

Build 0 warnings; format clean; `run_all.py` 0 errors; raw-colour guard 0.

## Notes for qa

- Extra ids: `export-close`, `library-error`. The library loads the newest 200 items and filters on the client.
- Download links point at the API (`PublicApiBaseUrl`); the backend's `Content-Disposition: attachment` names the file.

## Next owner

frontend (007 US2), qa (007:T041).
