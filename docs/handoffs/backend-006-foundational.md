# Hand-off: backend / 006-foundational (T019-T024)

## What was built

- **Domain** (`Domain/Lyrics/*`, `Tracks/Track.Lyrics.cs`):
  - `TrackLyrics` implements the data-model 3.2 state machine. A search or an alignment without a worker job is
    "to be submitted" by the synchroniser. Paste works from every idle state. Search again keeps the text until the
    result arrives. Remove clears everything and blocks later automatic lookups. The offset is -5000..5000 ms in
    steps of 10. `DeclineAutoAlign` stores the estimate shown on a CPU worker.
  - `LyricsConflictException` carries the conflict: busy, no text, already timed, none, or not aligning.
  - `LyricsDocument` is the wire shape of `TimedLyrics`. `FromUserText` builds pasted text: CRLF/CR become LF,
    lines are split at line breaks, words at spaces and tabs, and control characters are rejected.
  - `LyricsDocumentRules` re-checks the data-model 2.1 invariants on worker data. Invalid data fails the lyrics with
    a reason that never quotes the text.
  - `LyricsCredit`, `AutoAlignRule` (`always`, or `gpu` on a GPU worker, and only after the standard separation is
    completed) and `AlignmentEstimate`, which uses the worker formula plus 140 s while the weights download, and
    the "Aligning takes about N minutes on the CPU." text.
  - `Track.Lyrics` is the row and `Track.LyricsStatus` is its status. The status is read through the property, so
    the `Tracks.LyricsStatus` column always mirrors the loaded row. A row exists exactly when the status is not
    `none`. `EnsureLyrics` refuses to create a row when the stored status says one exists but it was not loaded.
- **Persistence**:
  - Migration `AddTrackLyrics` creates table `TrackLyrics` (PK/FK `TrackId`, cascade delete). The document is
    stored as JSON and the device as the 003 device JSON. The migration also adds the `Tracks.LyricsStatus`
    column, with default `None` for existing rows.
  - `TrackRepository.FindAsync` now includes the lyrics row. List queries never load it.
  - `ListLyricsActiveAsync` returns searching and aligning lyrics, plus alignPending lyrics whose automatic rule is
    still pending.
- **Worker client** (`Worker/WorkerClient.Lyrics.cs`, `WorkerLyricsJob.cs`):
  - Lookup and align creation; neither is ever retried.
  - Get the job (404 -> null), the result, cancel, and delete (404 tolerated).
  - `TimedLyrics` is read straight into `LyricsDocument`: same camelCase shape, and the domain validates it.
  - `WorkerHealth.LyricsAlignment` is null for older workers.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T019/T020 | e1d4439 | e6c9acd |
| T021/T022 | 3b2b580 | 98f7127 (+ edc70e0 refactor) |
| T023/T024 | 8f84016 | 7c77a6a |

## Tests and coverage

`dotnet test tests/StemStudio.<X>.Tests -c Release -p:CollectCoverage=true`:

```text
StemStudio.Domain.Tests       499 passed   Domain       line 98.91 %  branch 97 %
StemStudio.Persistence.Tests   74 passed   Persistence  line 98.48 %  branch 92.85 %
StemStudio.Api.Tests          766 passed   Api          line 99.47 %  branch 95.78 %   Contracts 100 / 100
```

`dotnet ef migrations has-pending-model-changes` reports no changes.

## Quality checks

- `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors. `dotnet format --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.{Domain,Persistence,Api}`: 0 errors, 0 warnings after edc70e0.

## Dependencies added

None.

## Open issues / requests

- None yet. `AssetPersistenceTests` now inserts its legacy track with raw SQL (shared
  `SqliteTestDatabase.MigrateLegacyAsync`), because the current model has columns a 005 database lacks.

## Next owner

backend (006 US1: T031-T034).
