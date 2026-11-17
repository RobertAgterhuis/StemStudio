# Hand-off: backend / 013 US3 - storage overview (T054-T055)

## What was built

`LibraryCare/Storage`: `LibraryWalker` + `LibraryIndexReader` (shared with backup and clean-up), `StorageOverviewService`
(categories from the classified walk; worker categories from `GET /v1/storage`, null with a reason when the worker is
down), `StorageAttribution` (per-track rows incl. `sourceBytes`, `workerCacheBytes`, `detailFilesRemoved`, `recordingsBytes`;
unattached items), `StorageVolumes` (library, backups, workerData, modelCache; `sharedWith` = same free and total bytes),
`StorageOverviewCache` (30 s, `refresh=true`, invalidated by a clean-up). `GET /api/storage`.
`totalBytes` of a track = source + stems + detail + artwork + exports + scores + generations + recordings (the worker cache
is reported separately in `workerCacheBytes`).

## Tests and coverage

Library categories sum exactly to the walked fixture bytes (SC-006); 500 tracks under 2 s. Coverage as in
`backend-013-polish.md` (Api 98.54 % / 90.98 %).

## Next owner

frontend (T056), qa (T057).
