# Hand-off: frontend / 002-us4

## What was built (T074, T075)

- `detail-split-further` ("Split further") appears in the detail section of a **completed** track whose detail
  status is `none`. That covers tracks imported before feature 002. (A cancelled phase gets `detail-retry`,
  which calls the same `POST /api/tracks/{id}/detail`.) The section is never shown on tracks that are not
  completed, so the button never appears there.
- A click calls `StartDetailAsync`, which returns the queued track. The button is busy ("Queuing...") while
  the request runs. The page shows `queued` and starts polling, so the stage and `detail-progress` follow as
  in US3. Errors go to `track-action-error` (US3 handler).
- A cache hit (US4 AS2, SC-007) needs no extra UI: the next poll shows `completed` with outcomes, and the mixer
  offers expand buttons for the split parents the next time it opens.

## Files changed

- `src/StemStudio.Web/Components/Pages/DetailSection.razor(.cs)`
- `tests/StemStudio.Web.Tests/Pages/TrackDetailSplitFurtherTests.cs` (new, 7 tests)
- `specs/002-detail-separation/tasks.md`: T074, T075 ticked

## Red -> green

| Task pair | Red | Green |
|---|---|---|
| T074 -> T075 | `4668a2c` (2 failing tests) | `5ad33b6` |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
  Passed!  - Failed: 0, Passed: 275, Skipped: 0, Total: 275
  | StemStudio.Web | 99.87% | 94.75% | 100% |   (line | branch | method; gate 90 % line+branch: pass)
```

## Quality checks

- `dotnet build` 0 warnings; `dotnet format --verify-no-changes` exit 0.
- `run_all.py`: `385 files, 2746 units, 17 clones, 1 exemptions, 0 errors, 107 warnings -> exit 0` (no new warnings).

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

frontend (US5: T079/T080); qa (US4 E2E, D8).
