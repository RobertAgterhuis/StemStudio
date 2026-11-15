# Hand-off: frontend / 003-us3

## What was built (T042, T043)

- `IStemStudioApiClient.GetDeviceAsync` / `StemStudioApiClient`: `GET /api/device` -> `ProcessingDeviceDto`
  (backend contract 1.3.0). Errors throw `ApiException`, like every other client method.
- `DeviceDisplay.FromWorker(ProcessingDeviceDto?)` and `DeviceDisplay.Unknown`. The display is unknown when
  there is no answer, `reachable` is false, or `kind` is null. Otherwise it is GPU (name) or CPU, without
  `data-fallback`, with `reason` when the API sends one.
- Import page (`/`): `DeviceBadge` with `worker-device` (`data-device` `gpu` / `cpu` / `unknown`, text
  "GPU (<name>)" / "CPU" / "Unknown") and `worker-device-reason` (only when a reason is set). The device is
  read once in `OnInitializedAsync`. An `ApiException` shows "Unknown" and never blocks or errors the import
  form.

## Files changed

- `src/StemStudio.Web/Api/IStemStudioApiClient.cs`, `Api/StemStudioApiClient.cs`
- `src/StemStudio.Web/Services/DeviceDisplay.cs`
- `src/StemStudio.Web/Components/Pages/Import.razor`, `Import.razor.cs`
- `tests/StemStudio.Web.Tests/Api/DeviceApiClientTests.cs` (new, 3 tests)
- `tests/StemStudio.Web.Tests/Pages/ImportDeviceTests.cs` (new, 5 tests)
- `tests/StemStudio.Web.Tests/Components/DeviceBadgeTests.cs` (+3 `FromWorker` tests)
- `tests/StemStudio.Web.Tests/Pages/ImportPageTests.cs`: the 001 "no API call" assertions now ignore the
  device query (`ImportCalls()`). The real-client upload test queues a device answer before the track. The
  behaviour under test is unchanged.
- `specs/003-gpu-acceleration/tasks.md`: T042, T043 ticked

## Red -> green

| Task pair | Red | Green |
|---|---|---|
| T042 -> T043 | `b56ad9a` (10 failing; `GetTrack_ReadsTheJobDevice` already passed via the T003 DTOs) | `f1a1721` |

`b56ad9a` had a whitespace-only `dotnet format` finding in `ImportDeviceTests.cs`. It is fixed in `f1a1721`.

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
  Passed!  - Failed: 0, Passed: 307, Skipped: 0, Total: 307
  | StemStudio.Web | 99.88% | 95.05% | 100% |   (line | branch | method; gate 90 % line+branch: pass)
```

## Quality checks

- `dotnet build` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` exit 0.
- `sig_check.py src/StemStudio.Web tests/StemStudio.Web.Tests` -> `108 files, 501 units, 0 errors, 24 warnings`.
  The count is unchanged. The only Import warning is the pre-existing `Import::RunImportAsync` (18 LOC); it was
  not touched.

## Dependencies added

None.

## Open issues / requests

- qa: the import page calls `GET /api/device` on every load, prerender included. With
  `STEMSTUDIO_FAKE_DEVICE` the e2e suite can assert `worker-device[data-device]`.

## Next owner

frontend (T047, polish); qa (T044).
