# Hand-off: frontend / 002-us2

## What was built (T062, T063)

- **Engine interop 2.0.0** (`Audio/`):
  - `IAudioEngine.SetExpandedAsync(parent, expanded)` -> `setExpanded`. `EngineMethods.SetExpanded` is in
    `All`, so the drift test against `types.ts` covers it.
  - `LoadTrackAsync` sends `groups: [{ parent, stems: [{ name, url }] }]`. URLs are absolute against
    `PublicApiBaseUrl`, groups are in display order and stems have `rest` last. A track without split parents
    sends `groups: []`.
  - `setChannel` / `attachMeter` take `ChannelId`s (`drums.snare`) unchanged.
- **One rule for groups**: `DetailPhaseText.SplitParents(track)` returns the parents with outcome `split`
  whose stem exists on the track and that have at least one allowed detail stem, in display order. Both
  `MixerState` and the engine groups use it.
- **Mixer UI**:
  - `ExpandToggle.razor` (`expand-<parent>`): a RadzenButton with `aria-expanded`, `aria-controls="group-<parent>"`,
    the label "Show/Hide detail stems of <parent>" and busy "Loading..." while the engine loads the group.
    Only split parents get one.
  - `StemGroup.razor` (`group-<parent>`, `role=group`) renders one `ChannelStrip` per detail stem with
    `strip/fader/pan/mute/solo/meter-<parent>-<detail>`.
  - `ChannelStrip` now uses `ChannelState.Key` for ids and `Label` for the visible name. The spoken name is
    "drums snare". It renders `data-expanded="true|false"` on parents that have a group (absent otherwise) and
    `ChildContent` (the toggle).
- **Wiring** (`Mixer.Detail.cs`):
  - Toggle -> `Engine.SetExpandedAsync` first; on success `MixerState.SetExpanded` (dirty).
  - On `AudioEngineException` (e.g. `Failed to load stem "drums.snare": HTTP 404`) the message goes to
    `mixer-engine-error` and `RejectExpanded` keeps the parent collapsed and audible. The mixer stays clean
    and the toggle stays enabled, so the user can retry.
  - Re-entrant toggles while a load is pending, and toggles before the engine is ready, are ignored.
- **At load**: meters are attached for every channel, including detail ones
  (`attachMeter("drums.snare", "meter-drums-snare")`), and `setChannel` is pushed for every channel. The
  engine creates detail channels in `loadTrack`, so this is valid while collapsed. The meter lookup is by
  element id per frame, so a meter that is not rendered yet starts working when the group appears.
- The parent strip stays visible while expanded and is dimmed (hidden in the gate), and its own settings are
  kept. The CTO's note on the audio-engine hand-off (expand after `loadTrack`, then re-apply detail
  `setChannel`; error text; parent stays audible) matches this design. Applying `expanded` from a mix follows
  in US5.

## Files changed

- `src/StemStudio.Web/Audio/IAudioEngine.cs`, `AudioEngineInterop.cs`, `EngineMethods.cs`
- `src/StemStudio.Web/Components/Mixer/ExpandToggle.razor`, `StemGroup.razor`, `Mixer.Detail.cs` (new)
- `src/StemStudio.Web/Components/Mixer/ChannelStrip.razor(.cs)`, `Meter.razor`, `Mixer.razor`, `Mixer.razor.cs`
- `src/StemStudio.Web/Mixer/ChannelValue.cs` (new), `MixerState.cs`, `MixerState.Detail.cs` (`AllChannels`)
- `src/StemStudio.Web/Services/DetailPhaseText.cs` (`SplitParents`)
- `src/StemStudio.Web/wwwroot/app.css` (`ss-expand`, `ss-group`, expanded outline)
- `tests/StemStudio.Web.Tests/Mixer/StemGroupTests.cs` (new, 10 tests)
- `tests/StemStudio.Web.Tests/Audio/AudioEngineInteropDetailTests.cs` (new, 3 tests; `partial` of
  `AudioEngineInteropTests`)
- `specs/002-detail-separation/tasks.md`: T062, T063 ticked

## Red -> green

| Task pair | Red | Green |
|---|---|---|
| T062 -> T063 | `07d15e8` (does not compile: `IAudioEngine.SetExpandedAsync` missing) | `fd201e6` |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
  Passed!  - Failed: 0, Passed: 251, Skipped: 0, Total: 251
  | StemStudio.Web | 99.87% | 94.61% | 100% |   (line | branch | method; gate 90 % line+branch: pass)
```

Per file: `Mixer.Detail.cs` 100 % / 100 %, `StemGroup.razor` 100 % / 100 %, `ChannelStrip.razor.cs` 100 % / 100 %,
`AudioEngineInterop.cs` 100 % line / 94.7 % branch, `DetailPhaseText.cs` 100 % / 100 %.
The existing mixer, a11y (every control has a name) and save tests pass unchanged.

## Quality checks

- `dotnet build` 0 warnings; `dotnet format --verify-no-changes` exit 0.
- `run_all.py`: `381 files, 2724 units, 17 clones, 1 exemptions, 0 errors, 107 warnings -> exit 0`. No new
  warnings. `Mixer::StartEngineAsync` (17 LOC) is a 001 warning and I did not change that method.

## Dependencies added

None.

## Open issues / requests

- qa: `expand-<parent>` is a RadzenButton (`button` element), so Playwright `click()` and keyboard both work.
  While a group loads, the button is disabled and shows "Loading...".

## Next owner

frontend (US3: T069/T070); qa (US2 E2E).
