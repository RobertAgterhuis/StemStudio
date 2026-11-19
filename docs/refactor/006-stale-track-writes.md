# 006 Optimistic concurrency for Track and TrackLyrics instead of per-feature locks
- Status: proposed · Date: 2026-11-19 · Area: Api sync services / Persistence (Track, TrackLyrics) · Priority: P1

## Problem statement
Several writers change the same `Tracks` and `TrackLyrics` rows: the background synchronisers (`JobSyncService`, detail, lyrics,
metadata and generation passes, `LibraryFilingBackfill`) and the user's requests (cancel, paste, remove, retry). Each synchroniser
loads every active track at the start of a pass, works through them one by one with worker calls in between, and then saves. There
is no concurrency token on either table, so a user change that lands between the load and the save can be silently overwritten or
undone.

Evidence:
- Commit `004196f3` "fix(api): never save a stale detail phase over a concurrent cancel" (2026-11-18) fixed exactly this for the
  detail phase, after an E2E failure. The fix is specific to one feature: a per-track `SemaphoreSlim` map
  (`src/StemStudio.Api/Detail/DetailPhaseLocks.cs:11-34`) plus a hand-written comparison of the stored and loaded JSON column
  (`src/StemStudio.Persistence/Repositories/TrackRepository.cs:42-50`, `IsDetailPhaseCurrentAsync`).
- The same pattern is unguarded for lyrics. `src/StemStudio.Api/Lyrics/LyricsSynchronizer.cs:26-45` loads
  `ListLyricsActiveAsync`, then polls or submits per track and saves. `src/StemStudio.Api/Lyrics/LyricsService.cs:44-112`
  (`SetAsync` paste, `RemoveAsync`, `CancelAlignAsync`) load the same track in another scope and save without any lock or check.
  A completed lookup or alignment that is applied after the user pasted their own text writes the stale lyrics document back over
  the paste. A cancel during a pass can be followed by a re-submit, which is the detail bug again.
- Only one concurrency token exists in the whole model: `src/StemStudio.Persistence/Configurations/LyricsVoicesConfiguration.cs:30`
  (`LyricsSheet.Revision`). `grep IsConcurrencyToken|IsRowVersion|DbUpdateConcurrencyException` finds no other.
- `Track` is a large aggregate split over 8 partial files (`src/StemStudio.Domain/Tracks/Track*.cs`, about 780 lines), with six JSON
  columns (`TrackConfiguration.cs:22-28`). 45 Api files take a `Track`.

## Impact
- User data loss: pasted or edited lyrics can be replaced by a worker result. User intent is lost: a cancel can be undone and the
  worker job keeps running on the GPU.
- Each new feature that adds a pass needs its own lock class. Locks held in memory do not protect against a second API instance or
  the restore and merge paths.
- These faults show up as flaky E2E runs and cost a full debug round each time (the detail fix came from E2E run 44212916383).

## Risk
- Leaving it: rare but real data loss. It gets more likely as more passes are added (practice, recording and lyric assist add
  writers).
- The change: adding a `Version` column needs a migration on a live SQLite library (with a backup or restore round-trip). Every save
  path has to handle `DbUpdateConcurrencyException` (reload and re-evaluate, or return 409 to the user). Blast radius is medium.
  Rollback means a down-migration that drops the column, plus reverting the handlers.

## Effort
M (3-5 days). Assumes EF Core property-level concurrency tokens on SQLite (a client-set integer `Version`, incremented on save),
and that the existing `TrackPass` helper, used by five of the six synchronisers (track, detail, lyrics, metadata, asset; not
`GenerationSynchronizer`), is where the conflict handling goes.

## Step-by-step plan
1. Safety net: add a failing characterisation test, like `DetailConcurrencyTests`, for lyrics. The synchroniser loads, the user
   pastes in a second scope, the synchroniser applies a completed job: assert that the paste survives. Add a second test for
   cancel-align during a pass.
2. Add an integer `Version` concurrency token to `Track` and `TrackLyrics` (configuration, migration, increment in
   `SaveChangesAsync` for modified entries). Check: migration tests and the backup and restore schema tests
   (`KnownSchema`, `ReferenceSchema`) pass.
3. In `TrackPass.RunAsync`, catch `DbUpdateConcurrencyException` per track, log it and skip the track. The next pass reloads fresh
   state. Check: the step-1 tests turn green.
4. User endpoints (`LyricsService.ChangeAsync`, `DetailService`, the retry and delete services): on a conflict, reload once and
   re-apply the transition, or return 409 with the existing problem shape. Check: the endpoint tests for 409 paths.
5. Replace `DetailPhaseLocks` and `IsDetailPhaseCurrentAsync` with the token, keeping `DetailConcurrencyTests` as the proof. Remove
   the singleton from the composition root.
6. Verify: the full Api.Tests and the E2E detail and lyrics specs pass, and a repeated stress test (pass and request in a loop, 200
   iterations) shows no lost updates. Roll back by reverting step 5 first (the locks come back), then the migration.

## Dependencies and related proposals
Touches the same composition root as the backend composition-modules draft; merge order does not matter. Coordinate the migration
with any open feature branch that changes `TrackConfiguration`.
