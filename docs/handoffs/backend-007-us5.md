# Hand-off: backend / 007-us5 (T059-T060)

## What was built

- `POST /api/assets/stem-packs` (`createStemPack`, `Assets/StemPackService`):
  - Creates one raw item per standard stem in catalogue order. With `includeDetail`, it also creates one per kept
    detail stem of every split parent, in mixer order (e.g. `vocals.lead`, `vocals.backing`, `vocals.rest`,
    `drums.kick`, ...).
  - Each item has a unique default name ("<title> - vocals", ...), contents "<channel>, raw" and its own render job.
    FLAC/WAV default to 16 bit (raw single channel), MP3 to 320 kbps.
  - The disk guard is checked once for the whole pack (409 `insufficient-disk-space`).
  - Unknown track -> 404. Track not ready -> 409 `track-not-ready`. Invalid format -> 422. Unreachable worker -> 503,
    and nothing is stored.
  - Answers 202 `{ items }`.
- `ExportPlanner.RawDraft` and `TrackProblem` are now public helpers shared with single exports.
- **`PendingAssetOperations` is empty.** Every 007 operation is served, and
  `ServedOpenApiDocument_ListsExactlyTheContractOperations` passes with both pending lists empty.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T059 / T060 stem packs | 22fb9f0 (8 of 8 tests failed at run time) | 52de322 |

## Tests and coverage

```text
dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true
  672 passed   StemStudio.Api line 99.51 %  branch 95.6 %   Contracts 100 / 100
```

## Quality checks

`sig_check.py src/StemStudio.Api`: 88 files, 399 units, 0 errors, 0 warnings. `dotnet format` is clean.

## Dependencies added

None.

## Open issues / requests

- When the worker becomes unreachable partway through a pack, the jobs already submitted are not stored as items. The
  worker sweeps them after 24 h, and the user gets 503 and can try again.

## Next owner

backend (polish T066).
