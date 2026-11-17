# Hand-off: backend / 013 US2 - restore a backup (T042-T051)

## What was built

- `LibraryCare/Restore`: `ArchiveValidator` (data-model 3.4 steps 1-5) and `StagingExtractor` (step 6: `GetFullPath`
  containment, bounded and hashed writes), `StagedDatabase` (read-only integrity and migration history, migration of older
  schemas in staging, stored paths against the layout, `ConsentCheck`, reconcile "Interrupted by restore" in staging),
  `RestoreJournal`/`JournalPlans` (validated paths, idempotent rollback, start-up recovery before job recovery),
  `LibrarySwitcher` (maintenance, in-flight wait up to 10 s, `ClearAllPools`, 10 x 200 ms move retries, rollback to `io-error`;
  `IJournalStepHook` crash seam), `PreviousLibraryStore`, `RestoreRun` (WAL after the switch, previous-library record, Claude
  key check with the report warning "The stored Claude key must be entered again."), `UndoRun`, `RestoreService`
  (`REPLACE`/`DISCARD` confirmations, `jobs-active` with `activeWork`, `previous-library-exists`, archive state, space).
  Endpoints restore (gated), previous, revert (gated), discard (gated).
- `Worker/WorkerCacheRehydrator` + `Track.RehydrateWorkerJob`: detail submission and lyrics alignment rehydrate on a 404/409
  cache miss and retry once; a failed rehydration or retry surfaces the original error.
- `ILibraryFiles.Move` seam (rename failures are tested through it).

## Contract readings

`SkippedItem` = `{ category: "voiceProfiles", id, name, reason: consent-invalid | option }`; `migratedFrom` = the archive's
last migration; an empty library (R-07 definition) restores with mode `empty` and keeps no previous library.

## Tests and coverage

```text
Domain 98.86 % / 95.13 %, Persistence 99.01 % / 95 %, Api 98.54 % / 90.98 % (2029 passed, 2 skipped), Contracts 100 % / 100 %
crash after every journal step recovered (SC-005); hostile corpus H1-H12, H16, format 2, newer schema, not a ZIP (SC-004)
```

## Open issues / requests

- The stage text "Preparing stems" is not shown during a rehydration (the detail phase stays "Queued").
- Merge (US5) not implemented: `mode: merge` answers 400 (CTO row).
- Hostile vectors H13-H15 and H17 are covered by unit checks (integrity, stored paths, consent, entry limit) but not by a
  generated archive in the backend tests; qa's corpus (T012) covers them end to end.

## Next owner

frontend (T052), qa (T053).
