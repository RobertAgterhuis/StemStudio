# Hand-off: frontend / 012 US1 (T037-T046, incl. web-interop T043/T044)

Branch `012-lane-frontend`. Contracts: ui-test-ids 1.11.0 (`specs/012-practice-tempo/contracts/ui-test-ids.md`),
audio-engine 2.3.0, backend-api 1.11.0 DTOs, help-content 1.0.0. Engine and backend replaced by the substituted
`IAudioEngine` / `IStemStudioApiClient`.

## What was built

| Pair | Red | Green | Content |
|---|---|---|---|
| T037/T038 | fb0068be | 79fcf01b | `Components/Mixer/Practice/{PracticePanel,SpeedControl,TransposeControl}.razor(.cs)`, `Mixer.Practice.cs`, `Mixer/PracticeText.cs`, `wwwroot/css/components/practice.css` (sheet `practice` in `App.razor`), `FeatureCatalog` `practice.speed-reset` (Advanced), `practice.numeric` (Expert), `practice.stretch-status` (Expert) |
| T039/T040 | 0f7aa582 | efe87dda | `ExportTarget.AtPracticeTempo`, `export-practice-note` in the export dialog |
| T041/T042 | c55b662d | 87a16771 | topics `practice-speed`, `practice-transpose`, `practice-sound-quality`, glossary `semitone`; related links from `mixer-screen`, `export-dialog`; tour `mixer-basics` v2 (step on `practice-speed`); `HelpRenderer` keeps links below `/third-party/` (new tab, no `..`) |
| T043/T044 (web-interop) | 22eee2ff | e1098e8b | `src/web-interop/src/shortcuts.ts` practice bindings, new `src/web-interop/src/control-keys.ts` (auto-started in `index.ts`), `ShortcutCatalog.Practice` + overlay group "Practice", `shortcuts` help topic rows |
| T045/T046 | 35ce43d3 | 420cb080 | `Mixer.Practice.cs` `PracticeCommandAsync`, palette actions |
| refactor | | 8145ef95 | `Mixer.SubscribeEngine` (SIG unit size) |

Behaviour (ids and attributes exactly as ui-test-ids 1.11.0):

- `practice-panel` (`role="group"`, `aria-labelledby` -> "Practice tempo", `aria-busy` while `loading`) in
  `.ss-console-panels`; `data-stretch` from the engine status, `data-speed`/`data-transpose` = heard values
  (`PracticeRules.HeardSpeed/HeardTranspose`: 100/0 while unavailable, the trainer speed while it runs).
- `practice-speed`: native range 50-120 step 1, `aria-valuetext` "75 percent", `data-page-step="10"` (Page Up/Down via
  web-interop; arrows/Home/End native), `aria-readonly="true"` while the trainer runs (CSS `pointer-events: none`,
  web-interop blocks the value keys, input ignored in C#), `disabled` while unavailable. `practice-speed-down/up`
  (+-5, clamped), `practice-speed-value` "75 %", `practice-speed-reset` "100 %" (Advanced+, only away from 100),
  `practice-speed-input` (Expert).
- `practice-transpose`: `role="spinbutton"` div (shows "+2", speaks "+2 semitones"), Arrow Up/Down, `data-ss-spin` so
  web-interop stops the page scroll; `practice-transpose-down/up`, `practice-transpose-input` (Expert).
- `practice-reset` "Original speed and key" (100 %, 0; trainer and count-in kept), disabled at 100/0 or unavailable.
- `practice-effective-bpm` "≈ 90 BPM" only with a tempo (manual BPM, else reliable grid). `practice-quality-hint`
  (< 70 % or |st| > 7). `practice-unavailable` = reason + " Playback works at the original speed and key.".
  `practice-stretch-status` (Expert) "Stretch: active, latency 106 ms, 0 dropouts" with `data-latency-ms`, `data-underruns`.
- Engine traffic: neutral settings are never sent (no `setPractice` on open); a saved non-neutral mix is sent once
  on open/load; drags go through `PracticeSender` (first at once, then the latest per 50 ms). Changes before the
  engine is ready are kept and sent by the push after loading. `Invalid practice settings` shows in `mixer-engine-error`.
- Shortcuts (mixer scope): `-` `=` `,` `.` `r` `c` `t`; ignored in typing contexts/dialogs and with Ctrl/Alt/Meta;
  `G then T` stays `go.tracks`. C#: speed/transpose/reset ignored while unavailable, speed also while the trainer runs;
  `c`/`t` arrive with US3/US2. Palette: "Original speed and key", "Speed +5 %", "Speed -5 %".
- Save, save as and the agent's `mixer_get_state.settings` carry `practice` (`MixerState.ToSettings`, since T032) -
  the coordinator's blocking request from the backend lane (PUT without `practice` resets it) is covered by
  `PracticeSaveExportTests.Save_SendsThePracticeSettings_SoTheBackendDoesNotResetThem`, `SaveAs_IncludesThePracticeSettings`
  and `AgentMixerState_CarriesThePracticeSettings` (green when written: the fix had already landed in bdb20125).
- `export-practice-note` "Exports use the original speed and key." while speed != 100, transpose != 0 or the trainer
  is on; the export request has no practice field.

## Decisions (owner unavailable)

1. **`+` alias for `=`** (Shift+= and the keypad plus), listed in the overlay as "- / =". Additive to the contract.
2. **Spoken singular** "+1 semitone" / "-1 semitone" (contract examples are plural for other values).
3. **Page Up/Down = 10 %** by a small web-interop module (browsers use 10 % of the range = 7); no new interop call,
   web-interop contract 1.0.0 unchanged (auto-started like the meter readouts).
4. **Reset keeps trainer and count-in**: "Original speed and key" means 100 % / 0 st as in the contract.
5. Slider and value show the **heard** speed (100 while unavailable) rather than the saved setting; the setting is
   still saved.
6. Help topics were added already in T038 (minimal) because the help coverage suite fails on markers without a topic;
   T042 completed them.
7. SoundTouch licence link points to **`/third-party/soundtouch/index.html`** (devops: the folder URL 404s under
   `MapStaticAssets`).

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2002, Skipped: 0, Total: 2002
| StemStudio.Web | 99.59% line | 93.68% branch | 99.7% method |   gate 90/90 met

src/web-interop: npx vitest run --coverage
Tests 293 passed (293); All files 99.71 % stmts | 98.19 % branch | 100 % funcs | 99.71 % lines
(control-keys.ts and shortcuts.ts 100 %; only tour.ts below 100, unchanged)
```

## Quality checks

`dotnet build` 0 warnings; `dotnet format --verify-no-changes` (Web, Web.Tests) clean; web-interop `eslint .` and
`tsc --noEmit` clean. `sig_check.py src/StemStudio.Web src/web-interop/src`: 0 errors, 42 warnings, none in a new
012 unit (StartEngineAsync was refactored; `Mixer.EngineAsync` 16 LOC is pre-existing).

## Dependencies added

None.

## Docs impact

User manual (mixer): practice tempo panel - speed 50-120 % (buttons +-5, Page Up/Down 10), transpose +-12, "Original
speed and key", effective BPM, quality hint below 70 % / beyond 7 semitones, unavailable message, Expert diagnostics,
shortcuts `- = , . r`, exports keep the original speed and key, SoundTouch licence link.

## Open issues / requests

- qa: `practice-transpose` is a `div[role=spinbutton]`; use `press('ArrowUp')` after focusing it, or the buttons.
  Page Up/Down on `practice-speed` need the built `web-interop.js`.
- devops: the help links `/third-party/soundtouch/index.html` (folder URL 404 under `MapStaticAssets`).

## Next owner

frontend (US2: T052/T053).
