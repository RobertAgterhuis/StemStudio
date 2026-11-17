# Hand-off: frontend 014, HO:foundational (lane L5, round 014+015)

Branch `014-015-round-lane-l5-frontend014` (from design head 5e67d97a). Merged in: L1 a8d9a4d6 (`OnLoopPass`, 014
T042-T043), L6 f9e51b6a (015 stub help topics under `Help/topics/mixer/recording*.md`, with the L6 commits below it).

## Tasks (red -> green)

| Task | Test commit (red) | Implementation (green) |
|---|---|---|
| T044 -> T045 web-interop key map as data | ed4cbea2 | 907a13a2 |
| T046 -> T047 preferences v2, validator, builder, pedal presets | cd347efa | 440bd132 |
| T048 -> T049 section navigator, practice recorder | b31472e5 | 970d7ea6 |
| T066 -> T067 mixer command dispatcher (pulled forward from US2, needed by the hook) | 162bf498 | d9f8e1a2 |

## Hooks delivered for L6 (015)

| # | Hook | Commit |
|---|---|---|
| 1 | `AudioEngineInterop` is `partial` (L6 adds `Audio/AudioEngineInterop.Recording.cs`) | b632e788 |
| 2 | `Program.cs` calls `builder.Services.AddRecordingUi()`; stub `Recording/RecordingUiRegistration.cs` (class `RecordingUiRegistration`; CA1711 forbids `...ServiceCollection`), owned by L6 | bb77f30f |
| - | Engine options: one constructor `AudioEngineInterop(IJSRuntime, IOptions<StemStudioWebOptions>, IEngineOptionsSource? engineOptions = null)`; `IEngineOptionsSource.Options` non-null -> `createEngine(self, options)`, else `createEngine(self)`. L6 registers an `IEngineOptionsSource` in `AddRecordingUi` | f85ce7ee |
| B | `ChannelStrip` `AdditionalAttributes` (`CaptureUnmatchedValues`) splatted on the root div | 9f507c5b |
| A/5 | 9 FeatureCatalog rows of 015 data-model 6 (topics resolve via L6's stubs) | 99ff1ed1 |
| 3 | `Mixer.razor` slots: `<RecordingToolbarToggle Mixer="this" />` (toolbar, after BandInfoToggle), `<TakeStripSlot Mixer="this" />` (after the stem strips in `ss-strips`), `<RecordingArea Mixer="this" />` (after `ss-console-panels`). Stubs in `Components/Recording/*.razor` inherit `Components/Mixer/MixerSlot` (`[Parameter, EditorRequired] Mixer Mixer`); L6 owns and replaces the three `.razor` files (keep `@inherits MixerSlot` or declare the same parameter) | d9f8e1a2 |
| 4 | **Command registration hook**: `Mixer.Shortcuts.cs` declares `partial void RegisterRecordingCommands(MixerCommandRegistry commands);`. L6 implements it in `Components/Mixer/Mixer.Recording.cs`: `commands.Add("mixer.record.toggle", ToggleRecordingAsync); commands.Add("mixer.compare.cycle", CycleCompareAsync);` (`Add(string, Func<Task>)` or `Add(string, Func<double, Task>)` for `id:n` arguments). The mixer calls `StateHasChanged` after every handled command. `keymap-defaults.json` has `reserved: false` for both rows; `KeyMapDefaultsTests` updated | d9f8e1a2 |
| - | Help area `recording` added then reverted (orchestrator decision: topics use area `mixer`) | 85454c43, c9e0343d |

Still open: hook 5 rest (shortcut overview rows `shortcut-record` / `shortcut-compare`) at polish; hook C
(`MixerState` external solo) at US4.

## Design decisions (within the contract)

- **Override rules** (web-interop 1.1.0 5.3, the same in TS `overrides.ts` and C# `KeyMapValidator`, both run the shared
  cases `tests/StemStudio.Web.Tests/Practice/keymap-override-cases.json`): command kept only for a known, non-reserved
  catalogue command with <= 2 chords (max 60); a chord is dropped when malformed (key 1..20 chars, lower-case), reserved
  or handled elsewhere (`isReservedChord`, including 023's external chords: reason `Used elsewhere: <label>`), the plain
  `g` of the sequences, or clashing with a taken chord (defaults of non-overridden commands incl. reserved ones, then
  accepted overrides in order). An overridden command never falls back to its defaults (deterministic, no fix-point).
- **Shift rule**: `shift` omitted = forbidden for letters, not checked for other keys (contract 5.1 + catalogue comment).
- **Layers**: an override list holds keys- and pedal-layer chords. Keys layer = the override's keys chords when the
  list is empty or has any keys chord, else the defaults. Pedal layer = the override's pedal chords, else the preset key
  (PageUp/PageDown) for the preset's two commands. A learned pedal key is a pedal-layer override of the preset's command;
  switching presets moves learned keys to the new commands; `custom` starts as a copy of the previous preset and is
  edited per command. Learn keeps at most one keys chord of the command (2-chord limit).
- **Guard**: `input[type=range]` moved from the text list to the control list (rule 1 second list), so pedal mode
  works on sliders; Space/Enter on activatable controls are "control" context.
- **Single-key off** also disables the "G then X" sequences (they are single keys).
- **Section navigation**: next/previous inside an enabled loop move the loop to the neighbouring section (data-model 5.3
  states it for next; previous does the same for symmetry, documented in the help topic at T061).
- **Recorder**: session `MaxSpeedPercent` = highest playback speed reported with the positions (clamped 50..120);
  a flush attempt resets the 30 s timer also when it fails (retry at the next flush); flushes never overlap; empty
  sessions are not written; loop passes are counted while running or holding.
- Contract constant `WEB_INTEROP_CONTRACT_VERSION` is now `"1.1.0"` (key map shipped); preferences records are written
  as schemaVersion 2 (TS and C#); `first-paint` reads version 2 too.
- `mixer-keys.js` and `Services/KeyboardShortcuts.cs` retired: Space is the binding `mixer.play-pause`. The mixer,
  the global layout and the library page register their key maps through `Practice/ShortcutKeys` (follows
  `PreferencesState.SetShortcutsAsync`).

## Quality

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Web (`dotnet test -p:CollectCoverage=true`, Release) | 3109 passed | 99.43 % | 93.61 % |
| web-interop (`vitest --coverage`) | 412 passed | 99.8 % | 98.29 % |

- `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors.
- `dotnet format --verify-no-changes`: clean for my files; 3 WHITESPACE findings in L6's
  `tests/StemStudio.Web.Tests/Recording/RecordingApiClientTests.cs:103` (reported to the orchestrator; L6 fixes).
- web-interop `eslint --max-warnings 0` and `tsc --noEmit`: clean.
- `tools/quality/run_all.py`: jscpd ran (367 clones, none mine as errors after the stub fix); **1 error open**:
  `src/web-interop/src/catalogue.ts:7 forbidden dependency web-interop -> StemStudio.Web`, the build-time JSON import of
  `keymap-defaults.json` that contract 5.4 prescribes; rule exception requested from L2 (quality-gate) via the
  orchestrator. SIG warnings (no errors) in my code: `Mixer::RegisterCommands` 17 NLOC, `KeyMapValidator::Accept` 19,
  `PedalPresets::Choose` 16, `PracticeRecorder::FlushAsync` 24 NLOC / CCN 6, `ShortcutJson::ReadChord` CCN 6,
  `ShortcutJson::ChordNode` 18.
- Security self-review: the only untrusted input is the browser preferences record (validated field by field on both
  sides, 16 KB limit kept, malformed chords dropped; hostile shapes in the shared cases). No new dependencies.
- a11y: no new visible UI at this checkpoint (stubs render nothing).
