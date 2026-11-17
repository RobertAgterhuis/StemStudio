# Hand-off: frontend / 015 foundational (lane L6, T026-T027)

## What was built
- `IRecordingEngine` (all 14 recording methods of audio-engine 2.5.0) implemented by `RecordingInterop` over the one
  shared engine object (`IRecordingJsHost`, implemented by `AudioEngineInterop`). JS errors become `RecordingException`
  with the parsed `RecordingErrorCode` (`permissionDenied:`, `notFound:`, `busy:`, `modeUnavailable:`, `unsupported:`,
  `recordingSpeed:`, `speedTrainer:`; anything else `Other`) and the readable sentence as `Message`.
- The five `[JSInvokable]` recording callbacks in `Audio/AudioEngineInterop.Recording.cs` (L6-owned partial, L5 hook
  b632e788) forward to `RecordingCallbacks` (events `StateChanged`, `ProgressChanged`, `TakesCaptured`, `InputLost`,
  `InputDevicesChanged`); unknown states are ignored.
- C# shapes of the 2.5.0 types (`RecordingEngineTypes.cs`); enums reuse the contract DTO enums (`InputChannelModeDto`,
  `RecordingPartDto`, `LatencyMethodDto`) so JSON values are the contract strings; new `RecordingState`,
  `MicrophoneAccess` (permission; name forced by CA1711), `TakeCompareMode`.
- `RecordingPreferences` (data-model 4) in `localStorage["stemstudio.recording"]`, missing fields keep defaults,
  out-of-range values fall back field by field, unreadable record -> defaults.
- `AddRecordingUi()` (`Recording/RecordingUiRegistration.cs`, called by Program.cs via L5 hook bb77f30f): options
  section `StemStudio:Recording` (`TestLoopback`), `IEngineOptionsSource` -> `RecordingEngineOptions`
  (`{ testInput: 'loopback' }` when on; L5 hook f85ce7ee), `IRecordingJsHost`, `IRecordingEngine`, `RecordingPreferences`.
- Drift: `EngineContractDriftTests.PendingCallbacks` keeps only `OnLoopPass` (L5's); a recording drift test checks
  every `RecordingMethods.All` name exists in `types.ts`.

## Files changed
- `src/StemStudio.Web/Recording/{IRecordingEngine,RecordingInterop,RecordingCallbacks,RecordingEngineTypes,RecordingException,RecordingPreferences,RecordingUiRegistration}.cs`
- `src/StemStudio.Web/Audio/AudioEngineInterop.Recording.cs`
- `tests/StemStudio.Web.Tests/Recording/{RecordingInteropTests,RecordingPreferencesTests}.cs`
- `tests/StemStudio.Web.Tests/Audio/EngineContractDriftTests.cs` (the five recording names only)

## Tests and coverage
- `dotnet test tests/StemStudio.Web.Tests --settings tests/coverlet.runsettings`: **2978 passed, 0 failed**.
- StemStudio.Web total: line 98.4 %, branch 93.8 %. 015 recording files (`Web/Recording/**`,
  `AudioEngineInterop.Recording.cs`): **line 144/144 = 100 %, branch 46/46 = 100 %**.

## Quality checks
- `dotnet format StemStudio.slnx --verify-no-changes`: clean. Build 0 warnings (TreatWarningsAsErrors).
- `uv run --project tools/quality --frozen python tools/quality/run_all.py`: PASS, 0 errors (jscpd ran: 362 clones in
  total, none in 015 code; an earlier clone of the JS guard pattern with `AudioEngineInterop` was removed). Remaining
  warnings in 015 code: three test units of 16-18 lines.

## Dependencies added
none

## Docs impact
Setup: `StemStudio__Recording__TestLoopback` (web, E2E only) - technical manual, settings list.

## Decisions / deviations
- Preferences live under their own key `stemstudio.recording` (like `stemstudio.karaoke`), not inside the 004
  preferences record, so web-interop's record validation stays unchanged (data-model 4 says "no contract change").
- `take` channel settings and `take`/`input` meters go through the existing `IAudioEngine.SetChannelAsync` /
  `AttachMeterAsync` (no duplicate methods on `IRecordingEngine`).

## Open issues / requests
- Merge note: `EngineContractDriftTests` `PendingCallbacks` line - L5 removes `OnLoopPass`, L6 removed the five
  recording names; on merge the result is `[]`.
- Still pending from L5: hooks 3 (mixer slots), 4 (commands, `reserved: false`), 5 (polish).

## Next owner
L6 continues with US1 (T040-T043).
