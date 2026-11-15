# Hand-off: backend / 007-us4 (T054-T055)

## What was built

- `CreateAudioExportRequest.range` is validated against the track (FR-006): `startSeconds >= 0`,
  `endSeconds <= durationSeconds`, and a length of at least 0.5 s (`ContractLimits.MinExportRangeSeconds`).
  - Violations -> 422 `range`, with the track duration in the message; the worker is not called.
  - The whole track (0 to its duration) is allowed.
- A valid range is:
  - stored in the recipe
  - sent to the worker as `range`
  - kept by retry
  - added to the contents and the default name as ", m:ss-m:ss" (e.g. "drums, raw, 0:30-1:15")

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T054 / T055 ranges | eb057f3 (8 of 8 tests failed at run time) | be2d448 |

## Tests and coverage

```text
dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true
  665 passed   StemStudio.Api line 99.5 %  branch 95.64 %   Contracts 100 / 100
```

## Quality checks

`sig_check.py src/StemStudio.Api` reports 0 errors and 0 warnings. `dotnet format` is clean.

## Dependencies added

None.

## Next owner

backend (US5: T059/T060).
