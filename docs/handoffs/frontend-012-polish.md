# Hand-off: frontend / 012 polish (T064, T065) - lane summary

Branch `012-lane-frontend` (from `012-practice-tempo` 010c3e6d). All frontend and web-interop tasks of 012 are done:
T029-T032, T037-T046, T052-T053, T059-T060, T064-T065 (ticked in `specs/012-practice-tempo/tasks.md`).
Checkpoint notes: `frontend-012-foundational.md`, `-us1.md`, `-us2.md`, `-us3.md`.

## What was built (polish)

- `tests/StemStudio.Web.Tests/Mixer/Practice/PracticeAccessibilityTests.cs` (e8edf8d0): keyboard order through the
  practice panel equals the visual order (speed -, slider, +, Expert input, transpose -, spinbutton, +, input, reset,
  count-in switch, bars, every pass, trainer switch and fields), no positive `tabindex`, every control named, the only
  live regions are `trainer-status` and the count-in announcer/indicator, spoken values "75 percent" / "+2 semitones",
  focus rings (global `.rz-button:focus-visible`, `.ss-practice-spin:focus-visible`), on-state switch colours use the
  tested `color-primary` / `color-on-primary` pair (>= 4.5:1 in both themes, `TokenContrastTests`).
- Contrast: only 004 tokens in `practice.css` (`css_tokens_check.py` clean); muted text on surface is the existing
  tested pair. Reduced motion: the count-in dot scale is off with `data-motion="reduced"` and the OS setting; no
  CSS animation anywhere in practice.css; no flashing (see HO:us3).
- Help review: all 012 topics `reviewed: "012"`; touched topics `mixer-screen`, `mixer-metronome`, `export-dialog`,
  `shortcuts` re-reviewed for 012 (`ExportAccessibilityTests` expectation updated like 008 did for `library`).
- web-interop (T065): coverage, lint and typecheck below; shortcut overlay text reviewed (group "Practice":
  "- / =", ", / .", "R", "C", "T (Advanced)").

## Commits of the lane

| Task(s) | Red | Green / other |
|---|---|---|
| T029/T030 | e2c4d755 | ad8c6b38 |
| T031/T032 | ad026ac4 | bdb20125 |
| HO:foundational | | 15e6a399 (also `SetPracticeAsync` <= 15 LOC) |
| T037/T038 | fb0068be | 79fcf01b |
| T039/T040 | 0f7aa582 | efe87dda |
| T041/T042 | c55b662d | 87a16771 |
| T043/T044 (web-interop) | 22eee2ff | e1098e8b |
| T045/T046 | 35ce43d3 | 420cb080, refactor 8145ef95 |
| HO:us1 | | 818ac151 |
| T052/T053 | 02440bca | 8c1bcc7c (incl. quality-gate findings) |
| HO:us2 | | 03e6fac9 |
| T059/T060 | dabecad2 | 844037df |
| HO:us3 | | 861b8fe5 |
| T064/T065 | | e8edf8d0 (polish tests; green on the first full run after two test fixes) |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed!  - Failed: 0, Passed: 2056, Skipped: 0, Total: 2056
| StemStudio.Web | 99.6% line | 93.66% branch | 99.71% method |   gate 90/90 met
(baseline before 012: 1875 tests, 99.58 % / 93.64 %)
```

### web-interop lane (separate gate)

```text
src/web-interop: npm ci; npx vitest run --coverage
Test Files 13 passed; Tests 293 passed (293)
All files 99.71 % statements | 98.19 % branches | 100 % functions | 99.71 % lines (gate 90)
control-keys.ts and shortcuts.ts 100 % (only tour.ts below 100, unchanged since 004)
npx eslint .        clean
npx tsc --noEmit    clean
```

## Quality checks

`dotnet build` 0 warnings (TreatWarningsAsErrors). `dotnet format StemStudio.slnx --verify-no-changes --include
src/StemStudio.Web tests/StemStudio.Web.Tests` clean. `python tools/quality/run_all.py`:
`2256 files, 17730 units, 259 clones, 3 exemptions, 0 errors, 825 warnings -> exit 0`. New 012 units are within
15 LOC / CC 5 / 4 parameters except test `Reset_IsDisabledAtNeutral_AndRestoresSpeedAndKey` (16 LOC, warning).

## Dependencies added

None (NuGet, npm).

## Docs impact

- **User manual, mixer chapter - "Practice tempo"**: speed 50-120 % (slider, -5/+5 %, Page Up/Down 10 %, Advanced
  "100 %", Expert number entry), transpose -12..+12 semitones (spinbutton, -1/+1), "Original speed and key", "≈ BPM",
  quality hint below 70 % / beyond 7 semitones (smeary attacks, no formant correction, meters a little ahead),
  unavailable message (playback keeps working), Expert "Stretch" diagnostics (state, latency, dropouts); speed trainer
  (start, step, target, passes per step, needs an enabled loop >= 0.5 s, status line, pause keeps progress); count-in
  (1/2 bars, before every loop pass, needs a tempo, click volume, dots above the transport); exports keep the original
  speed and key (note in the export dialog); shortcuts `-` `=` (`+`) `,` `.` `R` `C` `T`; saved with the mix.
- **Technical manual**: web side of the time-stretch stage - `IAudioEngine.SetPracticeAsync/GetPracticeStatusAsync`,
  `OnPracticeChanged`, the UI-side 50 ms coalescing (`PracticeSender`, no `setPractice` at neutral settings),
  `Time stretch unavailable` mapped to a status; web-interop `control-keys.ts`; SoundTouch licence/source link
  `/third-party/soundtouch/index.html` in the help (help links below `/third-party/` are allowed, no `..`).

## Decisions taken (owner unavailable) - consolidated

1. `+` is an alias of `=` for speed +5 % (Shift+= / keypad).
2. Spoken singular "+1 semitone".
3. Page Up/Down = 10 % via an auto-started web-interop module; web-interop contract 1.0.0 unchanged.
4. "Original speed and key" keeps trainer and count-in settings.
5. Speed slider/value show the heard speed (100 % while the stage is unavailable, the trainer speed while it runs).
6. Minimal help topics were added with T038 (the coverage suite needs a topic per marker); completed in T042.
7. SoundTouch link to `/third-party/soundtouch/index.html` (devops: the folder URL 404s).
8. An enabled trainer, count-in or every-pass option can always be turned off; the disabled state only blocks turning
   them on (otherwise saved-but-inactive settings could not be cleared).
9. `trainer-needs-loop` also explains the 0.5 s minimum for a too-short loop.
10. Pass numbers are visible but `aria-hidden` inside the live `trainer-status`; only speed steps are announced.
11. Count-in announcements through a persistent sr-only live region ("Count-in", "Go"); the indicator itself has no
    changing accessible text.
12. Choosing a count-in bar count switches the count-in on.

## Open issues / requests

- **audio-engine**: rejection text must start with `Time stretch unavailable:`; one `OnPracticeChanged` per count-in
  beat and a final one with `countIn.active: false`; status after every trainer pass start.
- **backend** (request handled): `MixerState.ToSettings` always includes `practice` (save, save as, agent
  `mixer_get_state`); guarded by `PracticeSaveExportTests` (tests were green when written - the fix had landed in T032).
- **qa**: `practice-transpose` is a `div[role=spinbutton]` (focus + ArrowUp/ArrowDown, or the buttons); Page Up/Down
  on `practice-speed` needs the built `web-interop.js`; the count-in announcer is `[data-countin-announcer]`; the
  count-in indicator sits in `.ss-countin-slot` between the karaoke banner and the transport (visual baselines change).
- **CTO**: docs impact above; `KaraokeMixerTests` dock order now karaoke, count-in slot, transport.

## Next owner

CTO (integration wave T069-T075).
