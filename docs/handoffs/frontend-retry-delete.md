# Hand-off: frontend / retry-delete

## What was built

User problem: "Retry does nothing on a failed import, and I cannot delete it."

- **Retry feedback** (`/tracks/{id}`): while `POST /api/tracks/{id}/retry` runs, the Retry button
  shows Radzen `IsBusy` with `BusyText="Retrying..."` and is `Disabled`. When a track that was
  retried successfully on this page reaches `failed` again, an extra alert
  `track-retry-failed-again` ("The retry failed again.") is shown under the error. It is hidden
  again as soon as a new retry starts, and is not shown when the retry request itself is rejected
  (that case keeps the existing `track-action-error` behaviour).
- **Delete** (contract 1.1.0 `deleteTrack`): `IStemStudioApiClient.DeleteTrackAsync(Guid, CancellationToken)`
  sends `DELETE /api/tracks/{trackId}`; 204 completes, 404/409 throw `ApiException` through the
  existing problem-details reader. The page shows `track-delete` for `failed` and `completed`
  tracks only. It opens an inline confirmation ("This deletes the track, its stems and all of its
  mixes.") with `track-delete-confirm` (busy "Deleting..." while the request runs) and
  `track-delete-cancel`. On success it navigates to `/tracks`. On `ApiException` the message goes
  to `track-action-error` and the confirmation closes.
- **Double submit**: RadzenButton 13.1.0 has its own re-entry guard (private `clicking` field,
  checked by reflection). It ignores a click while the previous `Click` handler is still running,
  so the page has no separate guard. The bUnit tests click twice during a pending request and
  assert a single API call.
- Retry/delete logic lives in the new partial `TrackDetail.Actions.cs`, so `TrackDetail.razor.cs`
  only does loading and polling.

## Files changed

- `src/StemStudio.Web/Api/IStemStudioApiClient.cs`, `src/StemStudio.Web/Api/StemStudioApiClient.cs`: `DeleteTrackAsync`
- `src/StemStudio.Web/Components/Pages/TrackDetail.razor`: busy retry, failed-again alert, delete + inline confirmation
- `src/StemStudio.Web/Components/Pages/TrackDetail.razor.cs`: retry moved out, doc tweak
- `src/StemStudio.Web/Components/Pages/TrackDetail.Actions.cs` (new): retry/delete state and handlers
- `tests/StemStudio.Web.Tests/Pages/TrackDetailActionsTests.cs` (new, 11 tests)
- `tests/StemStudio.Web.Tests/Api/StemStudioApiClientTests.cs`: +3 tests (204, 404, 409)
- `e2e/tests/retry-delete.spec.ts` (new), `e2e/support/failed-track.ts` (new helper),
  `e2e/pages/track-detail-page.ts` (locators `retryFailedAgain`, `deleteButton`, `deleteConfirm`,
  `deleteCancel`; method `deleteAfterCancel`)

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true` (coverlet gate 90% line+branch):

```
Passed!  - Failed: 0, Passed: 198, Skipped: 0, Total: 198 - StemStudio.Web.Tests.dll (net10.0)
| Total   | 99.78% | 94.75% | 100%   |   (line | branch | method)
```

`TrackDetail.Actions.cs`: 100% line, 100% branch. `TrackDetail.razor.cs`: 100% / 100%.

bUnit covers: busy text and disabled state during retry, a second click during a pending retry
(one API call), the failed-again message after retry -> poll -> failed and its reset on the next
retry, a rejected retry (action error, no failed-again message, button enabled again), delete
button visibility for failed/completed/queued/separating, confirm/cancel, navigation to `/tracks`,
busy and a second click during a pending delete, and a 409 shown in `track-action-error`.

E2E: `retry-delete.spec.ts` (chromium + firefox): corrupt upload -> failed -> Retry ->
`track-retry-failed-again` visible -> Delete, Cancel, Delete, Confirm -> `/tracks` without the row
and absent from `GET /api/tracks`. **Not run**: it needs the backend `DELETE /api/tracks/{id}`
endpoint. `npm run lint` exit 0, `npx tsc --noEmit` exit 0, `playwright test --list` lists both
projects.

## Quality checks

- `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors.
- `dotnet format StemStudio.slnx --verify-no-changes`: exit 0.
- `uv run --project tools/quality python tools/quality/run_all.py`: `353 files, 2569 units,
  16 clones, 1 exemptions, 0 errors, 104 warnings -> exit 0`. One new warning, in test code:
  the 9-line `using` header of `TrackDetailActionsTests.cs` is a clone of `TrackListPageTests.cs`
  (all usings are needed). No new warnings in production code.

## Dependencies added

None. (`npm ci` in `e2e/` installed the existing lockfile only.)

## Open issues / requests

- Assumption: "retried in this page session" means a successful retry request (202) made on this
  page instance. Reloading the page forgets it, so a reloaded failed page shows only the error.
- The e2e spec creates a fresh failed track via `corrupt.mp3`. If the backend dedupes identical
  uploads to the existing failed track, the spec still holds. It deletes that track, so
  `import.spec.ts`'s corrupt-upload test, if run later, creates a new one.
- CTO: run `npx playwright test tests/retry-delete.spec.ts` once the backend DELETE endpoint is merged.

## Next owner

CTO (merge with the backend `deleteTrack` work, then run the e2e spec); qa for e2e review.
