# Hand-off: frontend / us1

## What was built

- **Import page** `/` (`Import.razor` + `.razor.cs`, replaces `Home.razor`): Radzen text box + submit
  (form submit, Enter works), `InputFile` restricted to `.mp3,.wav,.flac`. Client-side checks in
  `UploadRules` (extension, empty, > 200 MiB). The upload streams `IBrowserFile.OpenReadStream(ContractLimits.MaxUploadBytes)`
  into a `StreamContent` (G1, asserted through the stub handler with a 600 KiB file). Problem details are
  shown verbatim in `import-error` (field errors first, SC-007); 503 shows the service message. Success
  (200 or 202) navigates to `/tracks/{id}`. Legal notice (FR-005) and stem-limitation help (FR-010) are always visible.
- **Track detail** `/tracks/{id}` (`TrackDetail.razor` + `.razor.cs`, `TrackSummary.razor`): it polls
  `GET /api/tracks/{id}` through `IPollingTimer` (`PollingTimer`: 1 s `Task.Delay` on `TimeProvider`)
  until the track is completed or failed. It shows a status badge, the stage and a progress bar (`aria-valuenow`
  0-100). A failed track shows the reason and a Retry button (restarts polling; a 409 or 503 is shown in
  `track-action-error`). A completed track shows its stems, BPM, duration and model ("(4 stems)" plus a fallback
  note for `htdemucs`) and has `open-mixer`. A transient poll error is shown and polling continues. A 404 shows
  `track-load-error`. Polling stops on dispose.
- **Track list** `/tracks` (`Tracks.razor` + `.razor.cs`): title (link), artist, status badge
  (`track-row-status-<id>`, success/danger/info), duration, "Open mixer" for completed tracks; empty and error states.
- Prerendering is off (`InteractiveServerRenderMode(prerender: false)` in `App.razor`). Each page loads data
  exactly once, after the circuit starts, and the mixer can call JS on its first render.
- Helpers: `TrackStatusText`, `TimeText`, `TrackFacts`. DI: `TimeProvider.System`, `IPollingTimer`.

## Red -> green evidence

| Task pair | Red | Green |
|---|---|---|
| T076 -> T077 | 9649118 (does not compile: `Import`, `UploadRules` missing) | 976f1ea |
| T078 -> T079 | 9649118 (`TrackDetail`, `IPollingTimer`, `PollingTimer` missing) | 976f1ea |
| T080 -> T136 | 9649118 (`Tracks` missing) | 976f1ea |

## Files changed

- `src/StemStudio.Web/Components/Pages/{Import,TrackDetail,Tracks}.razor(.cs)`, `TrackSummary.razor`; `Home.razor` removed
- `src/StemStudio.Web/Api/UploadRules.cs`, `src/StemStudio.Web/Services/{PollingTimer,TrackStatusText,TimeText,TrackFacts}.cs`
- `src/StemStudio.Web/Components/{App.razor,_Imports.razor}`, `src/StemStudio.Web/Program.cs`
- `tests/StemStudio.Web.Tests/Pages/{ImportPageTests,TrackDetailPageTests,TrackListPageTests}.cs`,
  `tests/StemStudio.Web.Tests/Services/PollingTimerTests.cs`, `tests/StemStudio.Web.Tests/Layout/RoutesTests.cs`,
  `tests/StemStudio.Web.Tests/Fakes/{PageTestContext,ManualPollingTimer}.cs`, `WebHostTests.cs` (GET `/`);
  `HomePageTests.cs` removed; test csproj references `Microsoft.Extensions.TimeProvider.Testing` (already in CPM)

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`:
`Passed! - Failed: 0, Passed: 85, Skipped: 0, Total: 85`; `StemStudio.Web` line 98.92 %, branch 93.81 %,
method 100 %. The gate (90 % line and branch) passes.

## Quality checks

- `dotnet build` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` exit 0.
- Units are 15 LOC or less and take 4 parameters or fewer. Logic is in the code-behind and the static helpers.

## Dependencies added

None.

## Open issues / requests

- **qa**: the extra ids are `track-load-error` (404 on load) and `track-action-error` (retry or poll failure), plus
  `track-link`, `track-mixer-link`, `tracks-empty`, `tracks-error`, `track-fallback-note`, `nav-*`. They are
  additive; `ui-test-ids.md` is unchanged.
- **devops**: the upload goes through Blazor Server's `InputFile` (SignalR). With 200 MiB the stream is read in
  chunks, so no hub size change is needed, but uploads of that size take a while. The API client timeout is 10 minutes.

## Next owner

CTO review; frontend continues with US2.
