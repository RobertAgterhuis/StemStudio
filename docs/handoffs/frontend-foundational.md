# Hand-off: frontend / foundational

## What was built

- **Typed API client** (`T038`/`T039`): `IStemStudioApiClient` + `StemStudioApiClient` covering every
  backend operation of `backend-api.openapi.yaml` 1.0.1 (imports URL + streamed multipart upload,
  tracks list/get/retry, mixes list/get/create/update/delete/duplicate). Registered as a typed client on
  `StemStudioWebOptions.ApiClientName` with the server-side `ApiBaseUrl` (10 min timeout for 200 MiB uploads).
  Non-success responses become `ApiException` carrying an `ApiError` (status, title, detail, field errors,
  `UserMessage`, `IsConflict` / `IsUnavailable` / `IsNotFound`); transport failures map to a 503
  "The StemStudio API cannot be reached" error. Uploads are a `StreamContent` inside
  `MultipartFormDataContent` (never buffered).
- **Audio engine interop** (`T040`/`T041`): `IAudioEngine` + `AudioEngineInterop` (scoped per circuit).
  `import("./js/audio-engine.js")` -> `createEngine(DotNetObjectReference)`; `loadTrack` sends camelCase
  `{stems:[{name,url}],beatGrid}` with absolute stem URLs resolved against `PublicApiBaseUrl`;
  `play/pause/stop/seek/setChannel/setMaster/setLoop/setMetronome/attachMeter/dispose`. `[JSInvokable]`
  `OnPositionChanged` / `OnPlaybackStateChanged` raise C# events; `JSException` -> `AudioEngineException`;
  dispose tolerates a disconnected circuit and allows re-initialisation. All JS identifiers live in
  `EngineMethods`; the **U2 drift test** parses `src/audio-engine/src/types.ts` and asserts every invoked
  method exists in `interface AudioEngine`, `createEngine` exists, the `[JSInvokable]` names equal
  `DotNetCallbacks`, and the contract version is `1.0.0`.
- **Layout** (`T042`/`T135`): `MainLayout` with `NavMenu` (Import `/`, Tracks `/tracks`, Mixes `/mixes`;
  extra ids `nav-import`, `nav-tracks`, `nav-mixes`).

## Red -> green evidence

| Task pair | Red (failing test commit) | Green (implementation commit) |
|---|---|---|
| T038 -> T039 | 6213861 (does not compile: types missing) | e429d9a |
| T040 -> T041 | 559bf66 (does not compile) | 7e2fb9c |
| T042 -> T135 | 559bf66 (`NavMenu` missing) | 7e2fb9c |

## Files changed

- `src/StemStudio.Web/Api/{IStemStudioApiClient,StemStudioApiClient,ApiError,ApiException,ProblemReader}.cs`
- `src/StemStudio.Web/Audio/{IAudioEngine,AudioEngineInterop,AudioEngineException,EngineMethods}.cs`
- `src/StemStudio.Web/Components/Layout/{MainLayout,NavMenu}.razor`, `src/StemStudio.Web/Program.cs`,
  `src/StemStudio.Web/StemStudio.Web.csproj` (`InternalsVisibleTo StemStudio.Web.Tests`)
- `tests/StemStudio.Web.Tests/{Api,Audio,Layout,Fakes}/**`, `tests/StemStudio.Web.Tests/WebHostTests.cs`

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`:
`Passed! - Failed: 0, Passed: 38, Skipped: 0, Total: 38`; `StemStudio.Web` line 100 %, branch 97.67 %,
method 100 % (gate 90 % line+branch passes).

## Quality checks

- `dotnet build` 0 warnings (TreatWarningsAsErrors, latest-recommended analyzers).
- `dotnet format StemStudio.slnx --verify-no-changes`: exit 0.
- `tools/quality/sig_check.py` is not in this worktree yet; all units are <= 15 LOC, <= 4 parameters.

## Dependencies added

None (bUnit, NSubstitute, Radzen already in `Directory.Packages.props`).

## Open issues / requests

- None for this checkpoint.

## Next owner

CTO review; frontend continues with US1.
