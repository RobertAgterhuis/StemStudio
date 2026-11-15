# Hand-off: backend / 006-polish (T058) and feature summary

## What was built

- Refactors:
  - The duplicated "DELETE, 404 tolerated" block of the render and lyrics clients is now one
    `WorkerClient.DeleteToleratingMissingAsync`.
  - The 5-parameter theory in `WorkerLyricsClientTests` is reduced to 4.
  - Every lyrics unit is within 15 LOC, CC 5 and 4 parameters.
- Every public 006 type and member has XML docs; CS1591 is an error.
- Feature summary (see `backend-006-{foundational,us1,us2,us3}.md`):
  - Domain: `TrackLyrics` state machine, `LyricsDocument` with its rules, credit, the automatic rule, and the
    estimate.
  - Persistence: migration `AddTrackLyrics` (`TrackLyrics` table, `Tracks.LyricsStatus`); 007 databases migrate
    with status `none`.
  - Worker client: lyrics jobs (lookup/align/get/result/cancel/delete) and `Health.lyricsAlignment`.
  - `LyricsSynchronizer` runs in the `JobSyncService` tick.
  - Seven `no-store` endpoints. `PendingLyricsOperations` is **empty**.
- Copyright rules (FR-010, SC-006):
  - The text appears only in `GET/PUT/DELETE .../lyrics` responses, never in `GET /api/tracks` or
    `GET /api/tracks/{id}`.
  - It is never logged: domain records redact `ToString`, and validation messages never quote text. A Trace-level
    run proves it.
  - It is never exported: the export code does not read `TrackLyrics`.
  - The worker copy is deleted after it is stored.

## Red -> green evidence (feature 006)

| Task pair | Red | Green |
|---|---|---|
| T019/T020 | e1d4439 | e6c9acd |
| T021/T022 | 3b2b580 | 98f7127 (+ edc70e0) |
| T023/T024 | 8f84016 | 7c77a6a |
| T031/T032 | 4a6bea8 | 9c1e1b3 |
| T033/T034 | 5713792 | 878bbbd (+ 281bc43) |
| T044/T045 | e2b707a | 28a4886 |
| T049/T050 | 0ad6259 | 68e418c |
| T058 refactor | - | this commit |

## Tests and coverage

`dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors.
`dotnet test tests/StemStudio.<X>.Tests -c Release -p:CollectCoverage=true` (every gate on):

```text
StemStudio.Domain.Tests       500 passed   Domain        line 98.92 %  branch 97 %
StemStudio.Persistence.Tests   74 passed   Persistence   line 98.48 %  branch 92.85 %
StemStudio.Api.Tests          853 passed   Api           line 99.49 %  branch 96.09 %
                                           Contracts     line 100 %    branch 100 %
StemStudio.Web.Tests          332 passed   Web (untouched) line 99.88 % branch 95.12 %
```

`dotnet ef migrations has-pending-model-changes` reports no changes.

## Quality checks

- `dotnet format --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.{Domain,Persistence,Api}`: 0 errors, 0 warnings.
- `tools/quality/run_all.py`: 893 files, 7192 units, 63 clones, **0 errors**, 231 warnings, exit 0. Some of the
  warnings are in the new test files (unit size, shared `using` blocks); none is in production lyrics code.

## Dependencies added

None (feature 006 adds no NuGet package).

## Open issues / requests (consolidated)

- **architect**: add the extra 409 types `lyrics-timed`, `lyrics-not-aligning`, `lyrics-align-rejected` and
  `track-not-ready` to `LyricsConflict`, or tell me which types to fold them into.
- **architect**: give the Contracts lyrics records (`LyricsDto`, `LyricsLineDto`, `LyricsWordDto`,
  `SetLyricsRequest`) the same redacted `PrintMembers`.
- **architect**: `searchLyrics` with online lookups off answers `searching`; the next poll says `disabled`. The
  worker `/health` has no online-lookups flag.
- **frontend**:
  - poll `GET .../lyrics` while the status is `searching` or `aligning`;
  - `alignment.estimateSeconds` and `reason` carry the CPU estimate;
  - `alignment.progress` is 0..100.

## Next owner

backend continues with 008 (T030+); CTO for the 006 integration wave (T064+).
