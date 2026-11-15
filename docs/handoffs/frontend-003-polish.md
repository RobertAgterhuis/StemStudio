# Hand-off: frontend / 003-polish

## What was built (T047)

Accessibility of the device badge: everything is in text, and no state is shown by colour alone.

- `DeviceBadge` has a visible label in front of the device text (`Label`, default "Device"). The labels are
  "Processed on:" (track), "Detail stems processed on:" (detail section) and "Processing device:" (import).
  They sit outside the `data-testid` element, so the device text stays exactly "GPU (<name>)" / "CPU" /
  "Unknown" (ui-test-ids 1.2.0).
- A reason gets a visible prefix: "CPU fallback:" for a job fallback (`data-fallback="true"`), or
  "GPU not used:" for the worker reason. It sits in a `role="note"` container. The
  `*-fallback` / `worker-device-reason` element itself still holds only the reason, verbatim.
- Styling is plain (`ss-device*` classes, muted label colour from the existing tokens), so feature 004 can
  restyle it.
- I checked the qa 003 specs relayed by the CTO and the implementation already matches them. `detail-device`
  is inside `detail-section`. Both job badges always render `data-device` (cpu|gpu) and `data-fallback`
  (true|false). The fallback elements show the reason text.

## Files changed

- `src/StemStudio.Web/Components/Shared/DeviceBadge.razor`
- `src/StemStudio.Web/Components/Pages/TrackDetail.razor`, `DetailSection.razor`, `Import.razor` (labels)
- `src/StemStudio.Web/wwwroot/app.css`
- `tests/StemStudio.Web.Tests/Components/DeviceBadgeTests.cs` (+4), `Pages/TrackDetailDeviceTests.cs` (+1),
  `Pages/ImportDeviceTests.cs` (label assertion)
- `specs/003-gpu-acceleration/tasks.md`: T047 ticked

## Red -> green (whole feature)

| Task pair | Red | Green |
|---|---|---|
| T031 -> T032 | `a20c917` | `be8e5cc` |
| T042 -> T043 | `b56ad9a` | `f1a1721` |
| T047 | `4f70029` (6 failing) | `de8d902` |

## Tests and coverage

```text
dotnet build StemStudio.slnx -c Release                      0 Warning(s), 0 Error(s)
dotnet test StemStudio.slnx -c Release --no-build -p:CollectCoverage=true
  StemStudio.Web.Tests          312 passed   StemStudio.Web   line 99.88 %  branch 95.08 %  method 100 %
  StemStudio.Domain.Tests       239 passed   (untouched)
  StemStudio.Persistence.Tests   40 passed   (untouched)
  StemStudio.Api.Tests          381 passed   (untouched)
```

Web tests went from 284 to 312 (+28) over the feature.

## Quality checks

- `dotnet format StemStudio.slnx --verify-no-changes` exit 0.
- `uv run --project tools/quality --frozen python tools/quality/run_all.py` ->
  `501 files, 3927 units, 24 clones, 1 exemptions, 0 errors, 135 warnings -> exit 0`. The warning count equals
  the architect baseline, and no warning is in a frontend 003 file. The only device-related hit is
  `DeviceContractTests.cs` (backend tests, architect skeleton).
- `sig_check.py src/StemStudio.Web tests/StemStudio.Web.Tests` -> `108 files, 508 units, 0 errors, 24 warnings`
  (all pre-existing).

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

CTO integration wave (T051 e2e run, T052 GPU measurement).
