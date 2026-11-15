# Hand-off: frontend / 002-us1

## What was built (T055, T056)

- **Detail section** on `/tracks/{id}` for completed tracks (`DetailSection.razor(.cs)`, `DetailStemList.razor`,
  `Services/DetailPhaseText.cs`). It is placed under the track summary. It renders these ids from ui-test-ids 1.1.0:
  - `detail-section`;
  - `detail-status` (RadzenBadge, text = contract status `none` / `queued` / `running` / `completed` / `failed` /
    `cancelled`);
  - `detail-stage` and `detail-progress` (`aria-valuenow`), only while the phase is queued or running;
  - `detail-long-notice` ("considerably longer than the standard separation ... the standard stems stay playable
    meanwhile"). It is shown while the phase is queued or running. For songs over 10 minutes it is shown until
    the phase completes, with the extra sentence "longer than 10 minutes";
  - `detail-outcome-<parent>` with `data-outcome` = kebab-case kind, and the user texts of data-model 2.2:
    "Split into kick, snare" (kept sub-stems in display order, without `rest`), "Skipped: (almost) silent",
    "Nothing further to separate", "Split was not reliable", "Not split";
  - `detail-stem-<parent>-<detail>` per split parent, in display order with `rest` last. Names not allowed for the
    parent are dropped;
  - `detail-limitation-help` (FR-017): "experimental: quality varies per song and instrument, and some
    instruments cannot be separated (...). The detail phase takes considerably longer than the standard
    separation."
- **Section placement**: the section is not rendered while the standard separation still runs (status `none`
  would be misleading there).
- **Polling**: `TrackDetail` polls while the track is not terminal **or** its detail phase is queued or running
  (`NeedsPolling`). So the page follows standard -> completed -> detail queued (auto-queued by the backend) ->
  running -> completed without a reload, and stops afterwards. 001 behaviour is unchanged for tracks whose detail
  status is `none`.
- `StemGroupState.From` reuses `DetailPhaseText.OrderedStems`, so the track page and the mixer use one
  ordering rule.

Deviation from plan.md: the component is `Components/Pages/DetailSection.razor(.cs)`, not
`TrackDetail.Detail.razor(.cs)`. A dot in a `.razor` file name is not a valid component class name. The page
actions (retry/cancel/split further) follow in `TrackDetail.Actions.cs`, as tasks.md says.

## Files changed

- `src/StemStudio.Web/Components/Pages/DetailSection.razor`, `DetailSection.razor.cs`, `DetailStemList.razor` (new)
- `src/StemStudio.Web/Services/DetailPhaseText.cs` (new)
- `src/StemStudio.Web/Components/Pages/TrackDetail.razor`, `TrackDetail.razor.cs` (section + polling rule)
- `src/StemStudio.Web/Mixer/StemGroupState.cs` (shared ordering)
- `src/StemStudio.Web/wwwroot/app.css` (`ss-outcomes`, `ss-outcome`, `ss-outcome-parent`, `ss-detail-stems`)
- `tests/StemStudio.Web.Tests/Pages/TrackDetailDetailTests.cs` (new, 17 tests)
- `specs/002-detail-separation/tasks.md`: T055, T056 ticked

## Red -> green

| Task pair | Red | Green |
|---|---|---|
| T055 -> T056 | `64d6dec` (14 failing tests) | `e366846` |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
  Passed!  - Failed: 0, Passed: 239, Skipped: 0, Total: 239
  | StemStudio.Web | 99.86% | 94.41% | 100% |   (line | branch | method; gate 90 % line+branch: pass)
```

Per file: `DetailPhaseText.cs` 100 % / 100 %, `DetailStemList.razor` 100 % / 100 %, `DetailSection.razor.cs`
100 %, `TrackDetail.razor.cs` 100 % line / 91.7 % branch.

## Quality checks

- `dotnet build` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` exit 0.
- `run_all.py`: `375 files, 2708 units, 17 clones, 1 exemptions, 0 errors, 107 warnings -> exit 0`. No warnings
  in the new files. Outcome and status texts are lookup tables, so no unit has more than 4 branches.

## Dependencies added

None.

## Open issues / requests

- qa: Playwright can use `detail-status` text (contract values) and `detail-outcome-<parent>[data-outcome]`.
  `detail-outcome-bass` (`not-supported`) is also rendered, because the backend returns an outcome for
  every standard stem.

## Next owner

frontend (US2: T062/T063); qa (US1 E2E, T057).
