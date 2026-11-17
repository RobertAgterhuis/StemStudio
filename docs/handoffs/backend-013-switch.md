# Hand-off: backend / 013 switch data-integrity fix (CTO E2E finding)

Branch `013-cto-switch` (worktree `.claude/worktrees/013-switch`, from `013-library-care` e0e48e72). Not pushed or merged.
Test-first: `SwitchInterferenceTests` was written first and failed 6 of 7 on the unchanged code (see "Tests").

## Finding

Firefox E2E run (fake stack, after about 40 restores/undos in Chromium): an "Undo restore" job failed `io-error`
("A library file is in use and could not be moved; the library was not changed"), its journal ended `RolledBack`, but
the live `/data/stemstudio.db` was a 0-byte file (with a fresh `-wal` 0 B and `-shm` 32 KB), every API call answered 500
"no such table: Tracks", and the real database sat unrecorded in `library-care/previous/<undoJobId>/stemstudio.db`.

## Root cause (confirmed, with one correction)

1. **Creation.** The live connection string was `Data Source=/data/stemstudio.db` with Microsoft.Data.Sqlite's default
   `Mode=ReadWriteCreate`. Any connection opened after `MoveAside stemstudio.db` created an empty database at the live
   path. Confirmed by `SwitchInterferenceTests` (a pooled default-mode connection plus a query reproduces the observed
   files: empty database, `-wal` 0 B, `-shm` 32 KB) and by a probe (`Mode=ReadWrite` on a missing file fails with SQLite
   error 14 and creates nothing).
   Openers during maintenance:
   - `GET /api/library-care/status` is never counted by the gate middleware (it is always allowed). It read
     `gate.IsMaintenance` once and then ran `LibraryState.ActiveWorkAsync`/`IsEmptyAsync`. A status request that read the
     mode just before `EnterMaintenance` was not waited for (`WaitForIdleAsync` counts only tracked requests), so its
     queries ran while the files moved. This matches the stack trace you saw.
   - Sync passes (`JobSyncService`, `GenerationSyncService`) skip a tick that *starts* in maintenance, but a pass that
     started just before keeps querying.
2. **Move-in failed.** `MoveIn ... -> stemstudio.db` used `File.Move` without overwrite. The destination now existed,
   so the move threw, was retried 10 times and failed, and a rollback followed.
3. **Rollback skipped the database (correction: it skipped it silently, it did not fail).** `RollBackAsync` undid a
   step only when `!Exists(from) && Exists(to)`. For `MoveAside stemstudio.db`, `from` (the live path) held the empty
   interloper, so the step was skipped and the journal was saved `RolledBack` anyway. `tracks/` and `assets/` came back
   because nothing had recreated them. The journal never had a non-terminal outcome.

Merge restore: the commit only writes rows to the live database and moves nothing. Before the fix a connection could not
create a file there either, because the database always exists during a merge. It gets the read-write guard anyway, and
nothing else had to change.

## Fix

- **Only start-up creates the live database** (`LibraryCare/LiveDatabaseFile`). `ApiComposition` passes
  `LiveDatabaseFile.ForLive(configured)` to EF and `LiveDatabase`. For a file database on the default mode this sets
  `Mode=ReadWrite`; an explicit mode, `:memory:`, `Mode=Memory` and `file:` URIs are left alone. Every derived string
  (snapshot, merge target, `UseWal`) inherits the mode. `UseStemStudioApiAsync` calls
  `LiveDatabaseFile.EnsureCreated` once, after library-care recovery and before `MigrateAsync`. It uses a one-off
  `ReadWriteCreate` connection and only when the file is missing (first start, or an explicit connection string to a
  new file). A connection during a switch now fails ("unable to open database file") instead of creating anything.
- **The status endpoint counts itself** before it reads the mode (`gate.TrackRequest()`, like the middleware). A switch
  that starts meanwhile waits for its reads, and a status during maintenance reads no database: `activeWork: []`,
  `libraryEmpty: false`, gate `maintenance`, active job and previous library from their JSON files.
  Sync services are unchanged: a tick that starts in maintenance is skipped. A pass already running is not waited for,
  because its worker calls can outlast the 10 s idle timeout and would fail restores. With read-write mode its database
  work now fails, is logged and is retried on the next tick; it can no longer create a database.
- **Switch** (`LibrarySwitcher` + new `Restore/JournalMover`):
  - The plan always moves `stemstudio.db-wal`/`-shm` aside when the database exists (a missing source is skipped). A
    companion that a connection creates after planning is therefore moved aside too.
  - Move-ins run with **the database last**, so it appears only once its companions and folders are in place.
  - Before each move-in, an item at the destination is removed **only when `FreshItems.IsFreshEmpty`**. For the
    database, companions that no step moves in are removed too. Fresh and empty means: an empty folder, a 0-byte
    file, a `-shm` whose `-wal` is absent or 0 B, or a `.db` that opens read-only with zero `sqlite_master` rows.
    Anything else counts as data: the move fails and the switch is rolled back, and nothing is deleted.
  - Between attempts, idle pooled connections are closed (`ClearAllPools`), because a pooled connection opened during
    the switch holds its files on Windows. A source that disappears counts as moved.
- **Rollback** (`RestoreJournal.RollBackAsync`, now returns `bool`):
  - A moved-in item goes back to staging or the previous library when its source path is free.
  - A moved-aside item goes back whenever it is still aside, replacing a fresh, empty item at the live path.
  - The journal becomes `RolledBack` **only when every item is back**. Otherwise it stays `Switching`, and the job fails
    `io-error` with `LibrarySwitcher.NotRestoredText` ("... could not be moved back; nothing was deleted. Restart
    StemStudio to finish putting your library back."). The fully restored case keeps the old text (`NotChangedText`).
    The gate opens in every outcome, as before. Because of read-write mode, a missing live database answers 500 instead
    of being replaced by an empty one.
- **Start-up recovery** (`RestoreJournal.RecoverAsync`):
  - `Staged`/`Switching` journals are rolled back as above. If one cannot be completed, it stays open and `RecoverAsync`
    throws `InvalidOperationException(RecoveryIncompleteText)`. Start-up stops instead of serving, or creating, a
    half-switched library. `EnsureCreated` runs after recovery, so it never creates a database while the real one is
    aside.
  - **The observed state is repaired.** For `RolledBack` journals, every `MoveAside` item that is still aside is moved
    back when the live path is missing or fresh and empty. Real live data is never replaced. The return value counts
    rolled-back plus repaired journals.
- Restore, undo and start-up recovery all go through the same switcher and journal code. The merge commit needed no
  change beyond the read-write guard (see above).

## Tests

New `LibraryCare/SwitchInterferenceTests`. On the unchanged code all but the last test failed (6 of 7):

- An empty database (pooled, WAL, failed query: the observed files) created after the move-aside is replaced by the
  restored one, with no live `-wal`/`-shm` left. The journal ends `Committed`.
- The same interloper followed by a failing move: the rollback puts the original database back and ends `RolledBack`.
- Real data (a non-SQLite file) at the live path: it is not deleted, the journal stays `Switching`, the error text is
  `NotRestoredText` and the gate is open. Recovery throws `RecoveryIncompleteText` while the way is blocked; once the
  file is moved away, recovery finishes and ends `RolledBack`.
- Recovery of a `Switching` journal (crash after the last move-in) with an interfering empty live database restores the
  original database, tracks, assets and staging.
- Recovery of the observed `RolledBack` journal with the database still aside and a 0-byte live database puts it back.
  This is idempotent: a second run counts 0.
- A `RolledBack` journal never replaces a real live database. A move-back that keeps failing leaves the item aside.
- Undo with an interloper brings the previous library back.

New `LibraryCare/LiveDatabaseFileTests`:

- `ForLive` modes, and that explicit or non-file strings are kept.
- A live-mode connection on a missing file fails and creates nothing.
- `EnsureCreated` creates the file once, and only for read-write file strings.
- Through the API: with the database moved aside, `GET /api/tracks` and the status answer 500 and no file is created.
- Status during maintenance answers 200 (`maintenance`, empty `activeWork`) without touching the database, and its
  request count returns to 0.
- `FreshItems` cases: folder, `-wal`/`-shm`, a database without and with tables, a text file.
- `JournalMover`: it refuses to clear data, gives up after `MoveAttempts`, and treats a vanished source as moved.

Changed: `LibrarySwitcherTests` and the new tests share the new `SwitchLibrary` fixture (temporary data directory, live library and staged restore), so the setup is not cloned.
`ConnectionString_CanBeConfiguredExplicitly` (CompositionTests) still passes. It covers first-start creation of an
explicitly configured new file.

### Results (Release, this branch's head)

- `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors. `dotnet format StemStudio.slnx --verify-no-changes`: clean.
- `dotnet test tests/StemStudio.Api.Tests -c Release -p:CollectCoverage=true`: **2143 passed, 2 skipped, 0 failed**.
  Coverage StemStudio.Api **98.58 % line, 91.1 % branch**, 98.87 % method (before: 90.93 % branch). Domain and
  Persistence were not changed.
- `run_all.py`: 0 errors, 1088 warnings (before: 1091). The warnings left in touched files are unit-size warnings of
  16-21 lines, in `JournalMover.MoveAsync`/`TryMove`, `LibrarySwitcher.SwitchAsync`/`MoveAllAsync` and five tests,
  plus three small test clones of 7-8 lines (using blocks and similar arrange lines).

## Open points

- A sync pass that started before maintenance is still not waited for (see "Fix"). Its database work fails and is
  retried; it can no longer create a database. If the CTO wants passes counted as requests, the cost is that a slow
  worker call fails restores with the 10 s idle timeout.
- An interfering `-wal`/`-shm` created during the switch is moved aside together with the real database (it is
  indistinguishable from an original at that point). It holds no frames (0-byte `-wal`), so SQLite ignores it when the
  library comes back.
- Start-up now stops with `RecoveryIncompleteText` when a journal cannot be rolled back. That is deliberate (no
  half-switched library is served), but an operator must free the blocking file. The text says so; there is no UI for
  it.

## Dependencies

None new.
