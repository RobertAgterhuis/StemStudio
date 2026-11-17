# Backend 014 - HO:foundational (lane L3, round 014+015)

Tasks T034-T041 (014 `tasks.md`). Branch `014-015-round-lane-l3-backend014`.

## Model frozen (rule M)

Migration `20261117212542_AddPracticeFlow` (only new tables, nothing changed in existing ones):
`TrackSections` (PK/FK TrackId -> Tracks cascade), `Sections` (FK TrackId -> TrackSections cascade, index
TrackId+Position), `ScoreViews` (PK/FK AssetId -> Assets cascade, `BarsJson`, `Revision`), `Setlists`,
`SetlistEntries` (Setlist cascade, Track cascade, Mix set null), `PracticeLogSettings` (seeded row Id 1, on),
`PracticeSessions` (Track cascade, Mix set null), `PracticeSectionTimes` (owned by the session, key SessionId+Index).
L4 may now create `AddRecordingTakes` on top of this snapshot. A later model change of mine goes through the orchestrator.

DbSets live in `src/StemStudio.Persistence/StemStudioDbContext.PracticeFlow.cs` (context is `partial`, commit 786a55af).

## What is in

- Domain `Sections/`: `TrackSections` aggregate (status, origin, revision, detect refusal, apply detected, replace with
  kept ids; changed detected sections become `user`), `SectionRules`, `SectionSnapper` (mode, snap, `BarAt`),
  `SectionChannels` (stems + split detail stems), `SectionsChangedException`.
- Domain `Setlists/`: `Setlist`, `SetlistEntry`, `SetlistRules` (field codes `track-not-found`, `mix-track-mismatch`).
- Domain `Practice/`: `PracticeSession` (+ `PracticeSectionTime`), `PracticeSessionRules`, `PracticeLogSettings`,
  `PracticeSummaryCalculator`, `LocalDay`, `SummaryWindow`, and `ScoreView` (see decisions).
- Persistence: configurations (`PracticeFlowConfiguration.cs`), repositories `ISectionRepository`, `IScoreViewRepository`,
  `ISetlistRepository`, `IPracticeRepository` (registered in `AddStemStudioPersistence`); `TrackRepository.Remove*`
  counts removed setlist entries (`SetlistRemovals`).
- Api: `IWorkerPracticeClient` (new interface, implemented by a `WorkerClient` partial; `IWorkerClient` and its GPU
  decorator untouched) + practice job records; `WorkerHealth.Lanes` / `.Structure` (worker 1.8.0);
  `Practice/PracticeFlowRegistration.cs` (`AddPracticeFlow`, `MapPracticeFlowEndpoints`, `PracticeFlowOptions`),
  wired in `ApiComposition`. Composition hook for 015 (`AddRecording`, `MapRecordingEndpoints`, stub file) commit 89bf0338.
- Tests: `ApiFactory.PracticeWorker` (`FakeWorkerPracticeClient`, built from the worker 1.8.0 examples) registered for
  every API test (ownership of `ApiFactory.cs` given to L3 by the orchestrator).

## Tests and coverage (full suites, `-p:CollectCoverage=true`)

| Component | Tests | Line | Branch | New 014 files |
|---|---|---|---|---|
| StemStudio.Domain | 1319 passed | 98.98 % | 94 % | 100 % line, 91.8 % branch |
| StemStudio.Persistence | 153 passed | 99.32 % | 94.73 % | 99.8 % line, 100 % branch |
| StemStudio.Api (+Contracts) | 2962 passed, 4 skipped | 98.24 % | 91.13 % | 95.7 % line, 100 % branch |

`tools/quality/run_all.py` on Domain, Persistence, Api: PASS, 0 clones (jscpd ran), 0 errors; new warnings: unit size
16-18 lines in `LocalDay.Split`, `PracticeSummaryCalculator.Summarize`, `SectionRules.Validate`.
`dotnet format StemStudio.slnx --verify-no-changes` clean; solution build 0 warnings.

## Decisions (no contract change)

1. `ScoreView` entity lives in `Domain/Practice/` (no `ScoreFollow` domain folder was assigned); it has an extra
   `Revision` column (+1 per ready) for the `?v=<revision>` page URL the contract asks for.
2. `(TrackId, Position)` and `(SetlistId, Position)` are plain indexes, not unique: SQLite checks uniqueness per row
   during reorder updates; the domain keeps positions unique.
3. Setlist entry ids that are unknown or repeated get a new server id (sections refuse foreign ids, as data-model 3.2 says).
4. Field error messages carry the code: "The song is not in the library (track-not-found)."
5. A section replace while a detection is queued/running wins: the set becomes `ready` and the job id is cleared (the
   late worker result is ignored).
6. Summary: `days[]` has one entry per local day of the window (zeros included, data-model 3.8 is binding over the
   shortened example); `sections[]` and the totals cover all sessions; `tempo[]` only the window.
7. Removed setlist entries are counted in `TrackRepository` (both remove paths), not by a trigger.
8. Migration tested against pre-014 databases built from our own migrations (`MigrateLegacyAsync` at
   `AddAgentMessageLyricsId`), never against the owner's database.

## Open / next

- T038's "mix loop JSON with `sectionId`": the forward-compatible load is tested; the round trip belongs to L4's T056.
- Score view auto-creation when a 008 score becomes ready needs a one-line hook in L4's `AssetSynchronizer` (requested
  at US4 time).
- Next: T054-T055 (US1 sections endpoints, applier, detect trigger), T060 (E2E).
