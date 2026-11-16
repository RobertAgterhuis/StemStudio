# Hand-off: frontend / 012 foundational (T029-T032)

Branch `012-lane-frontend` (from `012-practice-tempo`, Phase 0 commit 010c3e6d). Contracts: audio-engine 2.3.0
(`src/audio-engine/src/types.ts`, `specs/012-practice-tempo/contracts/audio-engine.md`), backend-api 1.11.0 DTOs
(`StemStudio.Contracts/Mixes/*Practice*`), data-model.md 2-3. The engine and the backend are built in parallel; this
lane uses the substituted `IAudioEngine` and bUnit's JS interop mock only.

## What was built

| Pair | Red | Green | Content |
|---|---|---|---|
| T029/T030 | e2c4d755 | ad8c6b38 | `IAudioEngine.SetPracticeAsync(PracticeSettingsDto)`, `GetPracticeStatusAsync()`, event `PracticeChanged`; `AudioEngineInterop` calls `setPractice` / `getPracticeStatus`, `[JSInvokable] OnPracticeChanged(PracticeStatus)`; `Audio/PracticeStatus.cs` (`PracticeStatus`, `TrainerStatus`, `CountInStatus`, `StretchState`, `TrainerState` with the contract's lower-case JSON names via `JsonStringEnumMemberName`); `EngineMethods.SetPractice/GetPracticeStatus`; `OnPracticeChanged` removed from `EngineContractDriftTests.PendingCallbacks` (now empty) |
| T031/T032 | ad026ac4 | bdb20125 | `Mixer/MixerState.Practice.cs`, `Mixer/PracticeRules.cs`, `Mixer/PracticeSender.cs`; `MixerState.ApplyMix` / `ToSettings` carry `practice` |

Behaviour:

- `"Time stretch unavailable: <reason>"` from `setPractice` does not throw: the wrapper raises `PracticeChanged` with
  `PracticeStatus.ForUnavailable(reason)` (first line only; Blazor appends the JS stack). The engine also sends its own
  `OnPracticeChanged`; both carry the same state. `"Invalid practice settings: ..."` and every other error stay an
  `AudioEngineException`. `OnPracticeChanged` only raises the event; the mixer marshals it with `InvokeAsync` like the
  existing position/state callbacks (T038).
- `MixerState.Practice` (default `PracticeSettingsDto.Default`); mixes saved before 012, and a `practice: null`, load the
  defaults. Mutators clamp to `PracticeLimits` and mark the mixer dirty. `ResetPractice` = 100 % / 0 st and keeps the
  trainer and count-in. Count-in: `SetCountInBars` remembers the last non-zero bar count (default 1); the switch turns
  on 1 bar at Beginner and the remembered count elsewhere; the Beginner switch does not overwrite the memory.
- `PracticeRules`: `TrackBpm` (override, else reliable grid), `EffectiveBpm` (rounded half away from zero),
  `NeedsQualityHint` (< 70 % or |st| > 7), `IsNeutral` (count-in ignored), `HeardSpeed`/`HeardTranspose` (100/0 while
  unavailable, trainer speed while running/target), `TrainerRuns`, `LoopProblem` (no enabled loop / loop < 0.5 s).
- `PracticeSender`: first change after a quiet 50 ms goes out at once, later ones are replaced by the latest and sent
  at the end of the interval (one `TimeProvider` timer). Settings equal to what the engine has are never sent, so a
  neutral mixer makes no `setPractice` call on open (SC-007). UI traffic only; audible timing stays in the engine.

## Files changed

- New: `src/StemStudio.Web/Audio/PracticeStatus.cs`, `src/StemStudio.Web/Mixer/{MixerState.Practice,PracticeRules,PracticeSender}.cs`,
  `tests/StemStudio.Web.Tests/Audio/AudioEngineInteropPracticeTests.cs`, `tests/StemStudio.Web.Tests/Mixer/MixerStatePracticeTests.cs`.
- Modified: `src/StemStudio.Web/Audio/{IAudioEngine,AudioEngineInterop,EngineMethods}.cs`, `src/StemStudio.Web/Mixer/MixerState.Mix.cs`,
  `tests/StemStudio.Web.Tests/Audio/EngineContractDriftTests.cs` (pending list emptied, `PracticeMethods_AreInvoked`),
  `specs/012-practice-tempo/tasks.md` (T029-T032 ticked).

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 1924, Skipped: 0, Total: 1924
| StemStudio.Web | 99.58% (line) | 93.65% (branch) | 99.69% (method) |   gate 90/90 met
```

Red runs: T029 7 failing of 46 audio tests; T031 32 failing of 32.

## Quality checks

`dotnet build` 0 warnings (TreatWarningsAsErrors); `dotnet format tests/StemStudio.Web.Tests --verify-no-changes` clean.
`tools/quality/sig_check.py src/StemStudio.Web/Audio src/StemStudio.Web/Mixer`: 0 errors; the only warnings are the
two existing ones (`ChannelState.Apply` CC 9, `MixerState` ctor CC 6); `SetPracticeAsync` was cut to <= 15 LOC.

## Dependencies added

None.

## Docs impact

None yet (no visible UI in this checkpoint).

## Open issues / requests

- audio-engine: the wrapper relies on the rejection text starting with `Time stretch unavailable:` exactly
  (audio-engine.md "Validation and errors") and on `PracticeStatus` property names as in types.ts.

## Next owner

frontend (US1: T037-T046).
