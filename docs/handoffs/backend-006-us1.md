# Hand-off: backend / 006-us1 (T031-T034)

## What was built

- **Lookup scheduling** (FR-001, FR-013): `Track.ScheduleLyricsLookup` runs at three points:
  - in `JobReportApplier` once a standard poll reports the duration;
  - in `TrackJobApplier.Finish` at completion;
  - in `TrackService.ScheduleOwnLookupAsync` for completed tracks whose lyrics status is `none`, so tracks imported
    before 006 get their search on the first `GET /api/tracks/{id}` or `GET .../lyrics`.

  The search uses the 005 cleaned lookup names (`LookupTitle/Artist/Album/DurationSeconds`) and falls back to the
  track's own title, artist and duration.
- **`LyricsSynchronizer`** runs in the `JobSyncService` tick after the metadata pass:
  - It submits lookups that have no job yet. A worker rejection fails the lyrics with the worker's detail.
  - It polls jobs and applies the results: found (word/line), alignPending (plain), instrumental, notFound and
    disabled. notFound and disabled get a fixed reason.
  - Invalid worker data fails the lyrics with `TrackLyrics.UnreadableReason`.
  - A forgotten job (404) fails with `LyricsJobApplier.ForgottenReason`. A failed job keeps the worker text.
  - After saving, it deletes the worker copy (best effort; the 24 h sweep removes the rest).
  - An unreachable worker keeps the state, and one broken track never stops the others.
- **Endpoints** (`Lyrics/LyricsEndpoints.cs`, `LyricsService.cs`, `LyricsProblems.cs`, `LyricsMapper.cs`):
  - `GET /api/tracks/{id}/lyrics` and `PUT .../lyrics/offset`.
  - Every answer, including problems, carries `Cache-Control: no-store`.
  - The credit comes from `LyricsCredit`.
  - 409 types: `lyrics-busy`, `lyrics-no-text`, `lyrics-none`, plus `lyrics-timed`, `lyrics-not-aligning` and
    `track-not-ready` (see requests). 422 problems carry the field `offsetMs`.
  - `TrackDto.lyricsStatus` is mapped. List and single-track responses never contain the text.
- **Logging guard** (FR-010, SC-006):
  - `LyricsDocument`, `LyricsLine`, `LyricsWord` and `WorkerLyricsAlignRequest` override `PrintMembers` and print
    timing only, so the text cannot reach a log through `ToString`.
  - Domain validation messages never quote text.
  - Synchroniser logs carry the track ID and the status only.
  - A Trace-level fake run (sync, get, offset, invalid offset) logs none of the fixture lines.
- `getLyrics` and `setLyricsOffset` are un-staged. `PendingLyricsOperations` now holds `setLyrics`, `removeLyrics`,
  `searchLyrics`, `alignLyrics` and `cancelLyricsAlignment`.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T031/T032 | 4a6bea8 | 9c1e1b3 |
| T033/T034 | 5713792 | 878bbbd (+ SIG refactor in the hand-off commit) |

## Tests and coverage

```text
StemStudio.Domain.Tests       500 passed   Domain  line 98.9 %   branch 97 %
StemStudio.Api.Tests          808 passed   Api     line 98.76 %  branch 94.31 %   Contracts 100 / 100
```

The uncovered Api lines are the alignment paths, which US2 (T044/T045) covers.

## Quality checks

- `dotnet build -c Release`: 0 warnings. `dotnet format --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.{Domain,Persistence,Api}`: 180 files, 749 units, 0 errors, 0 warnings.

## Dependencies added

None.

## Open issues / requests

- **architect**: the `LyricsConflict` examples list `lyrics-busy`, `lyrics-no-text` and `lyrics-none`. The backend
  also answers with:
  - `lyrics-timed`: align on word-synced lyrics;
  - `lyrics-not-aligning`: cancel without a running alignment;
  - `track-not-ready`: align before the standard separation is completed. This one is the 007 type.

  Please add them as examples, or tell me to fold them into the three listed types.
- **architect**: the Contracts records `LyricsDto`, `LyricsLineDto`, `LyricsWordDto` and `SetLyricsRequest` print
  their text in `ToString()`. Nothing in the backend logs them, but redacting `PrintMembers` the same way as the
  domain records would harden FR-010. Those files are architect-owned.

## Next owner

backend (006 US2: T044/T045); frontend can use `GET .../lyrics` and the offset now.
