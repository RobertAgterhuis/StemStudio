# Hand-off: backend / 013 US5 - merge restore (T062-T063)

Branch `013-cto-merge` (worktree `.claude/worktrees/013-merge`, on top of the integrated `013-library-care`). Replaces the
earlier "cut" note: `mode: merge` is now implemented.

## What was built

| Task | Commit | Content |
|---|---|---|
| T062 | red: `test(api): failing tests for the merge restore` | `LibraryMergerTests` (12 tests), domain restored-name tests, `FakeLibraryFiles.Copy` seam |
| T063 | green: `feat(api): merge restore` | `Restore/LibraryMerger.cs`, `Restore/MergePlanner.cs`, `Restore/MergePlan.cs`, `RestoreRun`/`RestoreService` wiring |

- `POST /api/backups/{fileName}/restore` with `mode: merge` no longer answers 400. It runs the US2 steps unchanged
  (`ArchiveValidator`, `StagingExtractor`, `StagedDatabase` incl. migration of an older schema, stored-path check,
  `ConsentCheck`, reconcile "Interrupted by restore"), then `LibraryMerger` instead of the journalled switch.
- `MergePlanner` (research R-07): entity by entity in dependency order - artists, user styles, lyrics sheets, voice
  profiles, tracks (with `TrackLyrics`), mixes, assets, generations (restyles/songs before voice conversions), audio hashes,
  agent conversations (with messages, runs, steps, approvals, undo entries, events). Rules: same id live -> `same-id`;
  same `Track.SourceKey` with another id -> `same-source` and its mixes, assets, generations (and conversions of those
  generations) -> `dependency-skipped`; a taken unique name (style, voice profile, mix per track) -> 007 copy-name rule with
  the word "restored" (`CopyNames.Next(..., word)`, `StyleNaming/MixNaming/VoiceNaming.RestoredName`,
  `Style/Mix/VoiceProfile.RenameRestored`). `AgentSettings` and `AgentSecrets` are never read from the archive.
- `LibraryMerger`: one `SaveChanges` (one SQLite transaction) on a second, live context; the commit runs under the
  maintenance gate (`EnterMaintenance`, wait for in-flight requests up to 10 s, commit, `ExitMaintenance` in every outcome).
  The library is never moved aside; no previous library is written or required.
- `MergeCopies`: files of added items only (`tracks/<id>`, `assets/<id>`, `generations/<id>`, `voices/<id>`,
  `uploads/<file>`) are **copied** from staging through the new `ILibraryFiles.Copy` seam **before** the commit; whatever
  the copies created is removed again when a copy or the commit fails (`io-error`, "The backup could not be added to your
  library; nothing of it was kept. Try again."). An existing upload file is kept as is; an orphan folder of an added item
  is completed/overwritten from the archive.
- `RestoreService`: merge needs no typed confirmation and is not blocked by a kept previous library; busy, archive state,
  compatibility and free-space checks apply as for replace.
- Report: `mode: merge`, `previousLibraryKept: false`, `restored` = manifest categories counted for the added items only
  (plus the database entry), `skipped` = consent/option items of US2 followed by the merge items, `migratedFrom` and the
  reconcile/manifest warnings as for replace. The "stored Claude key must be entered again" warning is never added (no
  secret is imported).

## Contract readings (details the contract leaves open)

1. **Renamed items** are reported in `skipped` with `renamedTo` set and `reason: same-id` (the enum has no rename reason;
   the frontend `JobView.SkippedText` ignores the reason when `renamedTo` is set, as in its bUnit fixture). `name` is the
   archived name. Renamed items are added.
2. **Categories** are the collection names of the manifest counts: `tracks`, `mixes`, `styles`, `lyricsSheets`,
   `voiceProfiles` (as US2's consent check), `assets`, `generations`, `agentConversations`. Generations carry
   `name = restyle.sourceTrackTitle` or null. Artists, preset styles and audio hashes are merged silently (not reported).
3. **Dependants of a same-id track are added** when they are missing (R-07: only a same-source skip cascades). This is
   what makes "mix per track" renames possible (spec US5-2) and lets a merge bring back a deleted mix of an existing
   track. A same-id item whose parent was skipped is reported `same-id` (id check first).
4. **Files are copied before the commit, not moved after it** (as the brief asked; differs from R-07/T062 wording "moved
   after commit, completed by start-up recovery"). Added items get folders named by their new ids, invisible without
   their rows, so copy-then-commit gives "a failure leaves nothing" without a journal; staging is deleted afterwards as
   for every restore. There is no merge journal.
5. Lyrics sheet titles and asset names are not unique in the schema, so they are never renamed (spec US5-2 lists lyrics
   sheets; there is nothing to clash with).
6. `missing-file` is never produced by the merge (the archive was verified; `StagedDatabase` already applies the scrubbed
   state).
7. A merge into an empty library is allowed and simply adds everything (`mode: merge` in the report); the UI never sends it.

## Tests and coverage

```text
dotnet test tests/StemStudio.Api.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2041, Skipped: 2, Total: 2043
| StemStudio.Api       | 98.57% line | 91.16% branch | 98.73% method |   (was 98.54 / 90.98)
| StemStudio.Contracts | 100%        | 100%          | 100%          |
dotnet test tests/StemStudio.Domain.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 940   | StemStudio.Domain | 98.86% | 95.13% | 97.67% |
```

`LibraryMergerTests`: added items with byte-identical files (tracks, mixes, assets, generations, lyrics sheets,
conversations with all agent child rows); same id; same source key with dependants incl. a voice conversion; renames of
style, mix and voice profile with the live items keeping id and name; archive of the current library -> everything
`same-id`, files unchanged; `voiceProfiles: false`; pending uploads and an orphan folder completed; copy failure (IO and
unexpected) and a unique-key violation at commit (a rival track seeded while the merge copies) leave nothing and the gate
open; no typed confirmation and not blocked by a previous library (which stays); `AgentSecrets` of the archive not
imported, live secret unchanged. `ManifestRulesTests` now checks that merge, like replace, refuses an incompatible archive.

Observed once: `ClaudeKeyLeakTests.StoredSentinel_NeverLeaks` failed in one full-suite run and passed alone and in the two
following full runs (flaky under load; unrelated to the merge, not investigated further).

## Quality checks

- `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors. `dotnet format StemStudio.slnx --verify-no-changes`: clean.
- `run_all.py`: 8 errors, **none in backend code**: all pre-existing in the integrated branch and in frontend-owned files
  (`tests/StemStudio.Web.Tests/Fakes/LibraryCareData.cs`, `StorageData.cs` parameters; duplication between
  `src/StemStudio.Web/Components/Care/BackupsTab.razor.cs`/`StorageTab.razor.cs` and `CleanupDialog.razor.cs`/
  `RestoreDialog.razor.cs`). New backend units are within the hard limits; soft warnings remain on
  `RestoreRun.RunAsync` (24 LOC), `RestoreService.RefusalAsync` (CCN 10, 17 LOC) and on long test methods.

## Dependencies added

None.

## Open points

- Frontend: categories are plural collection names (`Tracks "Song" skipped ...` after `Capital`); a label map would read better.
- Agent runs that were active in the archive are added as they are (the same is true for replace; `StagedDatabase` does
  not reconcile agent runs and `AgentStartupRecovery` only runs at start-up). A merged non-terminal run would then count as
  active work. Suggest reconciling agent runs in `StagedDatabase` for both modes (follow-up).
- A process crash between the file copies and the commit leaves orphan item folders without rows (no merge journal);
  they are harmless and visible in the storage overview.
- Frontend quality-gate errors listed above (frontend T066).

## Next owner

CTO (T067 merge and gates; the merge E2E `library-care-merge.spec.ts` can now run against the fake stack).
