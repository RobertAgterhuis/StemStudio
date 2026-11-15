# Hand-off: frontend / 003-us1

## What was built (T031, T032)

- `DeviceDisplay` (`Services/DeviceDisplay.cs`): view record for a device badge (`Kind` = `data-device`,
  `Text`, `Fallback` = `data-fallback` or omitted when null, `Reason`). `FromJob(JobDeviceDto)` uses
  `JobDeviceDto.DisplayText()` ("GPU (<name>)" / "CPU"). The reason is shown only when `CpuFallback` is true,
  and verbatim. When the flag is set without a reason, a generic text is shown
  (`UnknownFallbackReason`).
- `DeviceBadge` (`Components/Shared/DeviceBadge.razor`): renders the device text with `data-testid` = `TestId`,
  `data-device` and `data-fallback`, plus the reason element (`ReasonTestId`) only when a reason is set.
  Plain markup, two CSS classes in `app.css` (`ss-device*`), so the 004 theme can restyle it.
- Track page: `track-device` / `track-device-fallback` in the status card at any status, once `track.device`
  is set. Cached results (device null) show nothing.
- Detail section: `detail-device` / `detail-device-fallback` inside `detail-section`, once
  `track.detail.device` is set.

## Files changed

- `src/StemStudio.Web/Services/DeviceDisplay.cs` (new)
- `src/StemStudio.Web/Components/Shared/DeviceBadge.razor` (new)
- `src/StemStudio.Web/Components/Pages/TrackDetail.razor`, `DetailSection.razor`
- `src/StemStudio.Web/wwwroot/app.css`
- `tests/StemStudio.Web.Tests/Components/DeviceBadgeTests.cs` (new, 7 tests)
- `tests/StemStudio.Web.Tests/Pages/TrackDetailDeviceTests.cs` (new, 5 tests)
- `specs/003-gpu-acceleration/tasks.md`: T031, T032 ticked

## Red -> green

| Task pair | Red | Green |
|---|---|---|
| T031 -> T032 | `a20c917` (11 of 12 failing; `NoDevice_ShowsNoBadge` passes by design) | `be8e5cc` |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
  Passed!  - Failed: 0, Passed: 296, Skipped: 0, Total: 296
  | StemStudio.Web | 99.87% | 94.98% | 100% |   (line | branch | method; gate 90 % line+branch: pass)
```

## Quality checks

- `dotnet build` 0 warnings (TreatWarningsAsErrors); `dotnet format StemStudio.slnx --verify-no-changes` exit 0.
- `uv run --project tools/quality --frozen python tools/quality/sig_check.py src/StemStudio.Web tests/StemStudio.Web.Tests`
  -> `106 files, 484 units, 0 errors, 24 warnings, 0 exemptions` (all pre-existing; none in the new files).

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

frontend (T042/T043, US3); qa uses `track-device*` / `detail-device*` (ui-test-ids 1.2.0).
