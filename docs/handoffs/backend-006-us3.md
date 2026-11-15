# Hand-off: backend / 006-us3 (T049-T050)

## What was built

- `DELETE /api/tracks/{id}/lyrics` (200, `removed`, the contract's `RemovedLyrics` example): it clears the
  document, the job, the reason and the estimate, and keeps the offset. A running lookup or alignment is removed
  from the worker; an unreachable worker is ignored and swept later.
- After removal no automatic lookup happens: not on standard polls, not on `GET /api/tracks/{id}`, and not on
  `GET .../lyrics`. This also works for tracks loaded without the lyrics row, through the mirrored
  `Tracks.LyricsStatus`. Removing a track that has no lyrics yet stores `removed`.
- `POST .../lyrics/search` (202, `searching`) works from removed, notFound, failed, disabled, found, instrumental,
  alignPending and none. The next synchroniser pass submits the lookup. It answers 409 `lyrics-busy` while a search
  or an alignment runs. With online lookups off, the worker completes the job at once as `disabled`, so the
  response says `searching` and the next poll says `disabled`. The backend cannot know the worker setting
  synchronously, because `/health` has no online-lookups flag.
- `PendingLyricsOperations` is **empty**: all seven lyrics operations are served and contract-checked.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T049/T050 | 0ad6259 | 68e418c |

## Tests and coverage

```text
StemStudio.Api.Tests   853 passed   Api line 99.49 %  branch 96.1 %   Contracts 100 / 100
```

## Quality checks

- Build: 0 warnings. `dotnet format --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.{Domain,Persistence,Api}`: 180 files, 771 units, 0 errors, 0 warnings.

## Dependencies added

None.

## Open issues / requests

- **architect / frontend**: `searchLyrics` with lookups off answers `searching`, not `disabled`. The poll a moment
  later says `disabled`. The contract sentence "(with lookups off the response has status `disabled`)" would need
  a worker health flag to be exact.

## Next owner

backend (006 polish T058), frontend T053/T054.
