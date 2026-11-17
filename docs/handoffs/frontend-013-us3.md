# Hand-off: frontend / 013 US3 (T056)

Branch `013-lane-frontend`. Contracts as in `frontend-013-us1.md`.

## What was built

| Task | Red | Green | Content |
|---|---|---|---|
| T056 | 2e15e9b7 | 7b2b4fcd | `Components/Care/{StorageTab,StorageTracksTable,WorkerEntriesTable}.razor(.cs)`, `LibraryCare/StorageView.cs`, `FeatureCatalog` `care.track-table` (Advanced), `care.top-tracks` (Beginner only), `care.worker-entries` (Expert); help topics `storage-overview` (anchors `care-tab-storage`, `storage-volume-*`) and `storage-regenerable` (`storage-category-*`) |

Behaviour (ids exactly as ui-test-ids 1.12.0):

- `storage-refresh` (calls `getStorageOverview?refresh=true`), `storage-measured-at`.
- `storage-volume-<volumeId>` (`library`, `backups`, `worker-data`, `model-cache`): `role="meter"`, `aria-valuenow` =
  used %, `aria-valuetext` "100 GB free of 400 GB", `data-free-bytes`, `data-total-bytes`. Volumes on the same disk
  (`sharedWith`) are one line: the first volume carries `data-shared-with` (kebab ids, comma-separated) and the label
  "Library, Worker data and Model cache (same disk)"; the folded volumes have no element of their own. A volume
  without numbers shows "Not available: <reason>" and has no meter role.
- `storage-worker-unavailable` while `worker.available` is false; the library part stays.
- `storage-category-<category>` (`data-bytes`, `data-class` `original`/`regenerable`, `data-help-id`): size or "Not
  measured", files, badge, how to regenerate (+ cost) for regenerable rows; `storage-manage-<category>` for original
  rows: database/stems/artwork/pending uploads/recordings -> `tracks`, exports/scores -> `library`, generations ->
  `generations`, voice profiles -> `voices`, previous library -> `care`; other originals say "Only from a backup".
- Advanced+: `storage-tracks-table` with `storage-sort-<column>` buttons in the headers (`aria-sort` on the `th`;
  first press = largest first, second press reverses; unknown sizes always last), `storage-track-row-<trackId>`
  (`data-total-bytes`, `data-detail-removed`, title links to `tracks/{id}`), in a focusable scroll region.
  Beginner: `storage-top-tracks` (five largest).
- `storage-unattached`, `storage-model-cache-<id>` (size, models, "Downloaded again at first use", no control),
  `storage-backups` (count, size, link to the Backups tab), `storage-previous-library` (when the status has one).
- Expert: `storage-worker-entries`.

## Decisions (owner unavailable; also in `docs/decisions/CTO-DECISIONS-2026-11-15.md`)

1. **Contract gap**: ui-test-ids asks for a worker cache entries table at Expert "(sha prefix, size, in use, from
   library)", but backend-api 1.12.0 `StorageOverview` carries no per-entry data. `storage-worker-entries` lists the
   four worker categories (separation cache, detail cache, job files, scratch) with size and files. Per-entry rows need
   an additive backend field (architect).
2. Manage-link targets for original categories as listed above (the contract names the screens, not the mapping).

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2283, Skipped: 0
| StemStudio.Web | 99.54% line | 93.61% branch | 99.74% method |   gate 90/90 met
```

## Quality checks

`dotnet build` 0 warnings; `dotnet format --verify-no-changes` clean; `sig_check.py src/StemStudio.Web`: 0 errors,
39 warnings, none in a 013 unit.

## Dependencies added

None.
