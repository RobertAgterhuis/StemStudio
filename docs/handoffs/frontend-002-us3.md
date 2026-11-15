# Hand-off: frontend / 002-us3

## What was built (T069, T070)

- **Detail actions** on `/tracks/{id}` (`TrackDetail.Detail.cs`, `DetailSection.razor(.cs)`):
  - `detail-retry` ("Retry detail separation") is shown for `failed` and `cancelled` and calls
    `StartDetailAsync`. Only the detail phase is queued again: `RetryTrackAsync` is not called, and the
    standard stems and mixer link stay as they were.
  - `detail-cancel` ("Cancel detail separation") is shown for `queued` and `running` and calls
    `CancelDetailAsync`.
  - While a request runs, both buttons are busy and disabled ("Queuing..." / "Cancelling..."). The returned
    track replaces the page state, and polling restarts when the phase is active again. After a cancel,
    polling stops on the next poll.
  - When a start or cancel is rejected (404/409/503), the readable problem `detail` goes to the existing
    `track-action-error`.
- **`detail-error`**: the `error` text of a `failed` phase, e.g. "interrupted by restart" or "Detail model
  could not be downloaded; try again when online.". The track keeps showing `Completed`, and its stems and
  mixer link remain (FR-008).
- **`track-delete-blocked`** (FR-016): when `DELETE` returns 409 while the detail phase is queued or running,
  the server reason is shown with "Cancel the detail phase or wait until it has finished, then delete the
  track." The notice is computed: it disappears as soon as the phase is no longer active (after Cancel or
  completion). Every other delete error keeps the 001 behaviour (`track-action-error`).
- 001 behaviour is unchanged: the retry busy state, the failed-again notice and delete confirm/cancel tests
  pass as before.

## Files changed

- `src/StemStudio.Web/Components/Pages/TrackDetail.Detail.cs` (new)
- `src/StemStudio.Web/Components/Pages/DetailSection.razor(.cs)` (buttons, error, `Busy`/`OnStart`/`OnCancel`)
- `src/StemStudio.Web/Components/Pages/TrackDetail.razor` (`track-delete-blocked`, section wiring),
  `TrackDetail.Actions.cs` (`ReportDeleteError`)
- `tests/StemStudio.Web.Tests/Pages/TrackDetailDetailActionsTests.cs` (new, 17 tests),
  `TrackDetailTestContext.cs` (new shared fixture), `TrackDetailDetailTests.cs` (uses the fixture)
- `specs/002-detail-separation/tasks.md`: T069, T070 ticked

## Red -> green

| Task pair | Red | Green |
|---|---|---|
| T069 -> T070 | `e42ea87` (13 failing tests) | `59a4720` |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
  Passed!  - Failed: 0, Passed: 268, Skipped: 0, Total: 268
  | StemStudio.Web | 99.87% | 94.75% | 100% |   (line | branch | method; gate 90 % line+branch: pass)
```

`TrackDetail.Detail.cs` 100 % / 100 %, `DetailSection.razor.cs` 100 % / 100 %, `TrackDetail.Actions.cs` 100 % / 100 %.

## Quality checks

- `dotnet build` 0 warnings; `dotnet format --verify-no-changes` exit 0.
- `run_all.py`: `384 files, 2744 units, 17 clones, 1 exemptions, 0 errors, 107 warnings -> exit 0`. No new
  warnings. I split the action unit into busy and submit parts (<= 15 LOC) and removed test fixture
  duplication through `TrackDetailTestContext`.

## Dependencies added

None.

## Open issues / requests

- backend: the UI assumes `DELETE /api/tracks/{id}` answers 409 with a readable `detail` while the phase is
  queued or running (contract 1.2.0). It also assumes cancel returns the track with `detail.status = cancelled`.

## Next owner

frontend (US4: T074/T075); qa (US3 E2E).
