# Hand-off: backend / 020 US4 setlist handouts (T081, T082)

| Task pair | Red | Green |
|---|---|---|
| T081/T082 | 4ef522e3 | 97bc5e88 |

- `createHandout` with `source.setlistId` and `getHandoutOptions?setlistId=` (query name unchanged): 404 unknown setlist,
  409 `setlist-empty`, 409 `too-many-songs` above `StemStudio:MaxSetlistHandoutSongs` (default 50), entries in 014
  running order (positions 1-based), setlist and entry notes (`note` "" -> null), a song that is not completed is
  included with what it has (FR-010; `track-not-ready` only for song handouts), entries whose track is gone are skipped.
- Document: `kind setlist`, title = setlist name, subtitle "Setlist - N songs", `SetlistBlock` (key from the chords in
  concert pitch, rounded tempo, length, notes); `setlist-notes` printed per song that has entry notes.
- Item: `trackId` null, `trackTitle` = setlist name, recipe with `setlistName` snapshot.
- Ingredient availability over a setlist = best song; each song prints only what it has. Setlist options carry no create
  actions (they are per song).
- Bookmarks/page layout (one bookmark per song with the track title, "Page n of N") are the worker's (audio-ingest).

## Final verification (all backend checkpoints of this lane; Domain/Persistence on 52be7bf8, Api on c4fee551)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 4331 passed | 98.65 % | 94.12 % |
| StemStudio.Persistence | 185 passed | 99.5 % | 95.45 % |
| StemStudio.Api | 3683 passed, 4 skipped | 98.32 % | 90.81 % |
| (020 Api files only: Insights, Handouts, worker insights client) | | 98.9 % | 89.0 % |

- `dotnet build StemStudio.slnx -warnaserror`: 0 warnings, 0 errors; `dotnet format StemStudio.slnx --verify-no-changes`: clean.
- `tools/quality/run_all.py` on Domain, Persistence, Api and the 020 test folders: PASS, 0 errors (jscpd ran: 14 clones,
  none in production code; the 020 test clones are setup lines of the persistence and endpoint tests).
- Earlier full runs showed two failures, both handled: the 016 hook test (one-time edit, 046982e1) and one
  `LyricAssistEdgeTests.Stream_ClosesWhenTheAssistIsGone` timing flake (SSE, 9 s under load; passed in the next two runs).

## Integration list (not edited by this lane)

- Settings for the technical manual: `StemStudio:AutoQualityReport` (default true), `StemStudio:MaxSetlistHandoutSongs` (50).
- Copy-first migration test of AddQualityReport/AddPracticeNotes on the owner's database at deploy.
- No new dependencies, no licence rows. No STATUS/ROADMAP/decision-log edits.

## Security fixes (security review of the 020 backend, 2026-11-23)

Test-first per finding (red commit compiles and fails, then green).

| # | Finding | Red | Green | Fix |
|---|---|---|---|---|
| 1 | Numeric/undefined enums in `createHandout` gave 500 or were stored | 965d32f8 | 7aac6d93 | `WireEnumConverter<T>` (family, include, transposition, paper): only wire names; any other token reads as an undefined value that `HandoutRequests.Validate` refuses with 422 (`Enum.IsDefined`); `HandoutMapper.Convert` throws on an undefined member. An unknown name ("tuba") is now 422 instead of 400. |
| 2 | Deleting a queued handout cancelled a render job | 5874ff55 | 387b6761 | `AssetJobWorkers` picks the lane (render, transcription, handout) for the library delete and the asset synchroniser. |
| 3 | Concurrent first PUT of a practice note gave 500 | 343c0ded | 6f314a0d | `PracticeNoteRepository.SetAsync` upsert with retry (3 attempts) on a key or concurrency conflict; `DeleteAsync` is one idempotent statement. |
| 4 | Unbounded handout POSTs, raced names | 99def746 | 8d54c371 | `HandoutGate` (a `StartLock`, like `QualityGate`) around active count, name pick, insert and submit; at most `HandoutService.MaxActivePerSource` = 3 queued/rendering handouts per song or setlist, else 409 `jobs-active`. |
| 5 | Worker job submitted before the save could be orphaned | de7e9424 | a0b16102 | `SubmittedJob.KeepAsync`: storing after an accepted job runs with `CancellationToken.None`; on failure the job is deleted (best effort); an invalid job id answers 503 `worker-unavailable` and stores nothing (the quality job id is checked before the track changes). |
| 6 | Automatic quality start checked a track loaded before the lock | 3adc70ee | 40fb580d | Under the lock `ITrackRepository.IsQualityActiveStoredAsync` reads the stored report (deterministic test with a gated fake). |
| 7 | Quality sync loaded every completed track with a report; one corrupt row failed all queries | 1ff638a9 | e1cede37 | Ids selected in SQL (`json_extract` on `$.Status`, guarded by `json_valid` in a `CASE`); no migration change. `JsonColumn<QualityReport>.TolerantConverter`: an unreadable report reads as none, counted in `StoredJsonFaults` (process-wide) and logged once by the quality synchroniser. |
| 8 | `getHandoutOptions` read every entry of a long setlist | 1cfa0e33 | 76638c35 | Reads at most `MaxSetlistHandoutSongs` (50) entries; `songs` then reports 50. |
| 9 | Worker `Detail` echoed to clients | ad296161 | 16bacd1e | Fixed texts `HandoutService.WorkerRefused` / `QualityService.WorkerRefused`; the detail is logged (warning). |
| 10 | New writes not behind the backup gate | e3ca4019 | e48cf72f | `RequireGateOpen()` on `requestQualityReport`, `createHandout`, `setPracticeNote`; marked `x-stemstudio-write-gate: backupRunning` in the 020 delta and the full backend-api 1.19.0 (`merge_full.py --check` green, Redocly 2.78.0 lint valid, 33 warnings as before). |

Follow-up commits: ff421292 (edge-case tests of the helpers), plus the quality-gate refactor (unit size of
`QualityService.StartLockedAsync`, duplicated using block) in the final commit.

### Decisions to confirm (CTO / architect)

- **409 code of the active limit**: no 020 code fits; reused the existing 013 code `jobs-active` (without
  `activeWork`). Suggest a dedicated `handouts-active` in `HandoutConflict` (contract change, architect).
- **Options of a setlist above the limit**: `getHandoutOptions` has no 409 in the contract, so the options cover the first
  50 songs; the UI cannot tell the setlist is longer until create answers 409 `too-many-songs`. Option: add a 409 or a flag.
- **setPracticeNote documents no 409**: the gate answers 409 `backup-running`; the contract needs `'409': BackupRunning`
  (the component must be added to the delta and its `shared` list in `merge_full.py`, architect).
- **Gate policy**: 1.13.0 says database edits and job starts stay allowed during a backup; these three 020 writes are
  now gated as ordered. The contract note of 1.19.0 may need a sentence.
- Retrying a failed handout is not counted against the active limit (bounded by the number of failed items).
- The refused texts are new user-facing strings (quality report `error`, handout `document` error): help/UI copy check.

### Verification

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 4331 passed | 98.65 % | 94.12 % |
| StemStudio.Persistence | 195 passed | 99.51 % | 93.51 % |
| StemStudio.Api | 3724 passed, 4 skipped | 98.37 % | 90.95 % |
| (020 Api files only: Insights, Handouts, worker insights client) | | 99.2 % | 90.2 % (was 89.0 %) |

- `dotnet build StemStudio.slnx -warnaserror`: 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes`: clean.
- `tools/quality/run_all.py` (Domain, Persistence, Api, the 020 test folders): PASS, 0 errors; jscpd 37 clones, none in
  production code (new test clones: the SaveFailure factory setup in the handout/quality security tests, test seeding).
- No new dependencies. `StemStudio.Domain.JobIds` is now public (used by the API to check a quality job id first).
- Not mirrored yet (integration): the worker renderer's per-job limits (text chars, chord cells, notation bytes).
