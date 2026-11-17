# Backend 014 - HO:polish (lane L3, round 014+015)

All L3 backend tasks of 014 are done: T034-T041 (foundational), T054-T055 (US1), T070-T071 (US3), T078-T079 (US4),
T088-T091 (US5), T096-T097 (US6). E2E: see `qa-014-polish.md`. Earlier notes: `backend-014-foundational.md`,
`backend-014-us1.md`, `qa-014-us1.md`.

## Served (backend 1.13.0, all 22 operations)

| Area | Code | Notes |
|---|---|---|
| Sections | `Api/Sections/` (namespace `StemStudio.Api.SongSections`) | get / replace / detect, `SectionJobApplier`, `SectionDetectTrigger` (`ITrackCompletionHook`) |
| Score follow | `Api/ScoreFollow/` | `listTrackScores`, `getScoreView` (a ready score without a row = `none`, as after a 013 restore), `createScoreView` (idempotent, one lock), `getScoreViewPage` (CSP `default-src 'none'; style-src 'unsafe-inline'; font-src data:`, `nosniff`, `private, max-age=31536000, immutable`), `ScoreViewJobApplier`, `ScoreViewReadyHook` (`IAssetReadyHook`, called by `Scores/ScoreJobApplier` after `CompleteScore`; a failing hook is logged, the score stays ready) |
| Setlists | `Api/Setlists/` | CRUD, entries (field codes in the message), `removedEntries` counted on track delete and reset by `acknowledgeRemoved=true`, PDF via the worker (200 ms polls on `TimeProvider`, `SetlistPdfTimeoutSeconds` 5..120, 504 `document-timeout`, job deleted afterwards, sanitised file name, lyrics cut at 10,000 characters) |
| Practice log | `Api/Practice/` | settings, upsert (one lock: two saves of a new session store it once), list/paging, deletes, summary (local days with the offset, split at local midnight), overview; no outgoing HTTP (spy test) |

Composition: `Practice/PracticeFlowRegistration.cs` (`AddPracticeFlow`, `MapPracticeFlowEndpoints`) and the 015 stub
`Recording/RecordingRegistration.cs` (owned by L4). Sync: `JobSyncService` runs `SectionJobApplier` and
`ScoreViewJobApplier` after the asset sync; hooks `ITrackCompletionHook`, `IAssetReadyHook` in `Sync/`.

## Tests and coverage (final full suites with `-p:CollectCoverage=true`)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 1319 passed | 98.98 % | 93.99 % |
| StemStudio.Persistence | 153 passed | 99.29 % | 94.73 % |
| StemStudio.Api (+Contracts 100 %) | 3070 passed, 4 skipped | 98.24 % | 90.73 % |
| new 014 Api files (before the last SIG refactor) | - | 98.2 % | 87 % |

The Api branch figure is close to the 90 % gate; the new 014 files sit below it on branches (null-coalescing of optional
recipe and grid parts) and are carried by the assembly total.

`tools/quality/run_all.py` (Domain, Persistence, Api): PASS, 0 clones (jscpd ran), 0 errors; 014 warnings are unit sizes
of 16-24 lines in mappers/endpoint maps and complexities 6-9 in a few services (listed in the quality log).
`dotnet format StemStudio.slnx --verify-no-changes` clean, solution build 0 warnings.

## Security self-review (untrusted input: worker results, file paths, request bodies)

- Page files: path built from a GUID and an int route value (`{page:int}`, 1..`PageCount`), checked with
  `DataDirectory.RequireContainedPath`; written atomically; served as `image/svg+xml` under a CSP without scripts and with
  `nosniff`, so an SVG with script cannot run when opened directly.
- Worker results: section count capped at 64, labels trimmed and cut at 40, unknown kinds -> `other`, unknown warning
  codes / snaps dropped; score view pages 1..200 and bars 1..2,000 or the view fails ("could not be made").
- PDF file name: only letters, digits, space, `-`, `_`, `.`; trimmed; at most 80 characters (tests with `../`, quotes and
  CR/LF header injection, unicode).
- Practice data: never logged; no practice endpoint makes an outgoing request (spy handler test).
- Concurrency: one lock each around check + submit + save for detection and score views, and around find + insert of a
  practice session (LESSONS backend).
- Residual (accepted, worker is internal): page SVGs and the PDF are not size-capped when downloaded from the worker.

## Decisions (no contract change; please confirm or redirect)

1. A failed setlist-document job answers 503 with type `.../document-failed` and the worker's text (the contract lists
   503 only as `worker-unavailable`).
2. `PUT /api/practice/settings` with neither field is 422 (`enabled`); a `mixId` of another track or an unknown mix in a
   session is 422 `mixId` (the FK would fail otherwise).
3. Summary `sections[]` and the totals cover all sessions of the track; `days[]` and `tempo[]` only the window.
4. `getScoreView` for a missing row reports the planned `trackId` and `offsetSeconds`; `TrackScore.rangeStartSeconds`
   is set only for a partial score (008 range or exported range).
5. Score view of a score whose track is gone (no grid, no manual tempo): the view is stored as `failed` with "The score
   has no tempo grid, so its bars cannot follow the playback."
6. `WorkerScoreViewResult.Bars` deserialises straight into the domain `ScoreBar` (one shape, no duplicate mapper).
7. Earlier: see `backend-014-foundational.md` (ScoreView in `Domain/Practice` with `Revision`, non-unique position
   indexes, unknown entry ids get new ids, edit during detection wins, removed entries counted in `TrackRepository`).

## Ownership changes during the wave (granted by the orchestrator)

`tests/StemStudio.Api.Tests/TestSupport/ApiFactory.cs` (+ `PracticeWorker` fake), `tests/StemStudio.Api.Tests/Contract/BackendContractTests.cs`
(line 214), `src/StemStudio.Api/Scores/**` and its tests (ready hook, public `ScoreSubmitter.WorkerGrid`).

## Dependencies

None added (.NET and npm). The E2E PDF check uses the worker's locked pypdf 6.21.4 via `uv run --project worker`.

## Open items for the integration

- E2E notes in `qa-014-polish.md` (scenario 4 stack setting, pypdf host, SC-004 trace).
- The 013 backup does not archive `assets/<id>/view/`; after a restore the views are made again lazily (orchestrator
  decision for T107); the API side is tested.
