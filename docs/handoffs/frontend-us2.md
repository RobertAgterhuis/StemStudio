# Hand-off: frontend / us2

## What was built

- **`MixerState`** (pure C#, `Mixer/MixerState*.cs`, `ChannelState`, `LoopValidator`): it has one channel per
  available stem in `StemNames.SixStem` order. Defaults: fader 0.8, pan 0, master 1.0, metronome enabled only for a
  reliable grid, with the detected beats per bar. It applies the solo/mute audibility rule for display. Every
  mutator clamps its input, sets `IsDirty` and returns the payload for the engine. Loop markers A/B are validated
  with the messages "B must be after A." and "The loop is outside the track (0:00 - m:ss)." Enabling the loop
  without markers gives "Set both A and B before enabling the loop." An invalid edit while looping disables the
  loop by sending the last valid region with `enabled:false`. No `setLoop` is sent before a valid region exists.
  An engine rejection disables the loop and shows the engine's message.
- **Mixer page** `/tracks/{id}/mixer` (`Components/Mixer/Mixer.razor` + `Mixer.razor.cs` + `Mixer.Controls.cs`)
  with `ChannelStrip`, `Meter`, `MasterSection`, `TransportBar` and `LoopControls`. The engine sequence follows the
  CTO note and is asserted in `MixerComponentTests.EngineSequence_LoadThenMetersThenFullMix`: `initialize` ->
  `await loadTrack` -> `attachMeter` for each stem and `master` -> full mix (channels, master, metronome, loop).
  Every control calls the engine at once (SC-004). The transport buttons stay disabled until the stems are decoded,
  and qa's `waitForReady` relies on this. The position comes only from the engine's `OnPositionChanged`; a seek
  does not move it (see the test). Engine errors show in `mixer-engine-error`, and controls do not call the engine
  before it is ready. A track that is not completed or not found shows `mixer-load-error`.
- **Unsaved changes**: `unsaved-indicator` while dirty. `NavigationLock` asks `IConfirmService` before any
  internal navigation while dirty. `DialogConfirmService` opens a Radzen dialog (`ConfirmDialog`:
  `confirm-dialog-ok`, `confirm-dialog-cancel`, `confirm-dialog-message`), never `window.confirm`. External
  navigation uses the browser's `beforeunload` prompt (`ConfirmExternalNavigation`).
- **qa conventions (CTO message)**: the mute, solo and loop toggles expose `aria-pressed` on the element that has
  the test id. Faders, pan, master and seek are native `<input type="range">` with `aria-valuenow`. Loop A/B are
  native number inputs committed on `change`. All `e2e/pages/mixer-page.ts` selectors for US2 exist; the
  metronome and save ids follow in US3/US4. There is no metronome meter.
- Styling: `wwwroot/app.css` (vertical faders, meters driven by the engine's `--meter-level` / `data-clip`).

## Red -> green evidence

| Task pair | Red | Green |
|---|---|---|
| T093 -> T094 | befdd4d (does not compile: `MixerState`, `LoopValidator` missing) | 636a423 |
| T095 -> T096 | ca5f1a1 (does not compile: `Mixer`, `IConfirmService`, `DialogConfirmService` missing) | 583fa15 |

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`:
`Passed! - Failed: 0, Passed: 139, Skipped: 0, Total: 139`; `StemStudio.Web` line 98.6 %, branch 91.18 %,
method 99.39 %. The gate passes. `ChannelState.Apply` is exercised by US4.

## Quality checks

`dotnet build` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` exit 0. Units are 15 LOC or less.
The page logic is split into partial code-behind files.

## Dependencies added

None.

## Open issues / requests

- Extra ids (additive): `mixer-load-error`, `mixer-engine-error`, `mixer-loading`, `transport-state`, `loop-set-a`,
  `loop-set-b`, `mix-name`, `confirm-dialog-message`.

## Next owner

CTO review; frontend continues with US3.
