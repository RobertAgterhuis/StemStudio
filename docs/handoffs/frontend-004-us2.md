# Hand-off: frontend / 004 US2 (T039-T046, levels and the beginner journey)

## What was built

- **WelcomePanel** (`Components/Experience/WelcomePanel.razor`, on Import above the form): non-modal
  `section` (`data-help-id="welcome"`), three level cards ("I am new to this", "I know my way around",
  "Give me everything"), "Take a one-minute tour" (dismisses, then `ITourLauncher.StartAsync("import-basics")`)
  and "Not now". Shown only after preferences loaded and while `welcomeDismissed` is false; comes back
  after "Reset help and tours".
- **TourService start/close/record** pulled forward from T056 (the welcome button needs it):
  `Help/ITourLauncher`, `Help/TourService` (resolves step titles/texts from topics unless overridden,
  labels Next/Back/Done/Close tour/"{{current}} of {{total}}", reduced motion from the preference, one
  tour at a time, records completed/dismissed with the tour version). Offers and navigation handling
  follow in US4.
- **JourneyRail** (`Components/Experience/JourneyRail.razor`, `Experience/JourneyStage.cs`): Beginner only
  (`journey.rail`), `nav` with an ordered list, `journey-step-*` with `data-state` and
  `aria-current="step"`; pages place it in the context bar through a `SectionOutlet` ("journey"):
  Import -> Import; Track -> Separate until completed, then Mix; Mixer -> Mix, Saved when the mix has an
  id and no unsaved changes (all four done). Other screens show no rail. The cascading
  `ExperienceContext` now wraps the context bar too.
- **Level gates** (FR-013/014): `pan-*` (`mixer.pan`), `loop-start`/`loop-end` (`mixer.loop.typed`),
  `metronome-bpm/offset/beats-per-bar/reset` (`mixer.metronome.advanced`); read-only
  `pan-marker-<key>` ("Panned L 30" / "Panned R 45") at Beginner when pan != 0. Switching level makes
  no engine call and keeps values, dirty flag and position (hidden values stay applied).
- **Practice presets** (`Mixer/PracticePreset.cs`, `Components/Mixer/PresetPicker.razor`): the seven
  presets of design-spec.md 8 as data; availability by present stems; `Apply` uses only `MixerState`
  operations and returns a `PresetResult` (channel settings + optional metronome) that the page sends
  through the same engine calls as manual changes; the mix becomes dirty; toast "<preset> applied".
  Beginner: chip row above the panels (`mixer-presets` container); Advanced/Expert: `mixer-presets`
  menu button (`aria-haspopup="menu"`, `aria-expanded`), items rendered only while open, Escape closes.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T039 -> T040 | `7a2310e` (9 failing) | `3761058`; tour start: red tests in `3761058`, green `42904f5` |
| T041 -> T042 | `71413d3` + compile fix `42bb55e` (16 failing) | `41b85ca` |
| T043 -> T044 | `78a2dfa` (6 failing) | `ba350e1` (the test helper that switches level now re-passes the child, see note) |
| T045 -> T046 | `89f45ac` + compile fix `e381c63` (16 failing) | `1d4d86c` |

Note on T044: bUnit's `Render` on a `CascadingValue` drops parameters that are not passed again, which
re-created the Mixer in the red test; the helper now passes the same child so the instance is kept (the
behaviour under test, "no engine call when the level switches", is unchanged).

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
  Passed!  - Failed: 0, Passed: 670, Skipped: 0, Total: 670
  | StemStudio.Web | 99.19% | 92.19% | 99.63% |
src/web-interop: 146 Vitest tests, 100 % (unchanged in US2)
```

## Quality checks

`dotnet build` 0 warnings; `dotnet format --verify-no-changes` exit 0; `run_all.py`: 650 files, 4 990
units, 31 clones, **0 errors**, 167 warnings.

## Dependencies added

None.

## Deviations

- `PracticePreset.Apply` is `Func<MixerState, PresetResult>` instead of `Action<MixerState>` (data-model.md 5)
  so the page can send exactly the changed settings to the engine.
- The preset component is `PresetPicker.razor` (the name `PracticePresets` is the static preset list;
  a component with the same name shadowed it in the mixer namespace).

## Open issues / requests

- **qa**: `pan-marker-<key>` text is exactly "Panned L <n>" / "Panned R <n>"; at Beginner the presets are
  a chip row, the `mixer-presets` id is on its container; at Advanced/Expert it is the menu button.
  Toasts: "Level set to <Level>", "<Preset label> applied", "Help and tours reset".

## Next owner

frontend (US3, T048 onwards).
