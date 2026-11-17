# Hand-off: backend / 013 US4 - free space safely (T058-T059)

## What was built

`LibraryCare/Cleanup`: `CleanupPlanner` (detail stems, worker cache, leftovers; skips `detail-active`, `export-active`,
`job-active`, `worker-unavailable`, `nothing-to-remove`; plan file with a fingerprint over path/size/last write plus the worker
entries, valid `CleanupPlanMinutes`), `CleanupExecutor` (`plan-expired`, `plan-stale`; every file is classified again and
checked with `CleanupRules` right before deletion, so originals are never removed (SC-007, SC-010); `Track.RemoveDetailFiles`;
one worker call with the entries and, for leftovers, `scratch` + `oldJobFiles`). Endpoints preview/execute (gated,
`library-job-running`). After execution the track's `detail.filesRemovedAt` is set and the overview reports
`detailFilesRemoved: true` (QA reading confirmed).

## Tests and coverage

Coverage as in `backend-013-polish.md` (Api 98.54 % / 90.98 %).

## Next owner

frontend (T060), qa (T061).
