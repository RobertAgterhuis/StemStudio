# Hand-off: frontend / 007 US3 (library management)

Tasks 007:T051 (red) and T052 (green).

## What was built

- `Services/AssetFilter.cs`: client-side filter (track, kind, case-insensitive name search), track options (distinct
  source tracks by title, "(deleted)" for removed tracks), name validation 1-120 ("Enter a name of 1 to 120 characters.").
- `Components/Library/LibraryFilters.razor` (catalogue feature `library.filters`, Advanced+): native selects
  `library-filter-track` (option value = track id, "" = all), `library-filter-kind` (`audio`, `score`, ""),
  `library-search`. `library-empty` says "No items match the filters." when filters hide everything.
- `AssetRow` (+ code-behind): `asset-gain-note-<id>` ("Lowered by 1.8 dB to avoid clipping", marker
  `export-clipping`) only when `appliedGainDb < 0`; `asset-error-<id>` and `asset-retry-<id>` only for failed items;
  `asset-rename-<id>`, `asset-delete-<id>` on every row.
- `AssetRenameDialog` (+ code-behind): `asset-rename-input`, `asset-rename-confirm`, `asset-rename-cancel`,
  `asset-rename-error`; Enter confirms, Escape cancels, focus returns to the opener.
- `LibraryPage.Actions.cs`: delete through `IConfirmService` (existing confirm dialog, `confirm-dialog-ok`), removes the
  row and subtracts its size; retry replaces the row (queued) and restarts polling; failures in
  `library-action-error` (e.g. 409 `source-unavailable` detail).
- Help topic `mixes/export-clipping.md`.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T051 / T052 | c970e7f (11 of 12 fail) | 3a59849 |

## Tests and coverage

```text
Passed! - Failed: 0, Passed: 970, Total: 970
| StemStudio.Web | 98.45% line | 90.96% branch | 98.68% method |
```

## Quality checks

Build 0 warnings, format clean, `run_all.py` 0 errors.

## Notes for qa

- Extra ids: `asset-rename-cancel`, `asset-rename-error`, `library-action-error`.
- The download name comes from the backend's `Content-Disposition` (`<item name>.<ext>`).

## Next owner

frontend (007 US4), qa (007:T053).
