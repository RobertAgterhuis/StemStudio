# Contract delta: UI test ids for feature 012 (additive, 1.11.0)

Status: **binding delta** (012 spec, 2026-11-16; merged in Phase 0 T003, 2026-11-16). Base at
merge time: the full `specs/011-ai-composer-agent/followups/claude-key/contracts/ui-test-ids.md`
**1.21.0** on `main` (= 011's 1.10.0 + the claude-key delta). The full
`specs/012-practice-tempo/contracts/ui-test-ids.md` **1.11.0** = 1.21.0 + this delta, unchanged
(the label is 012's allocation; ROADMAP "Contract version allocation", merge order).
Nothing below renames, removes or changes an existing id; every rule of 1.9.0 applies (R1-R9:
`getByTestId` only, level visibility R7-R9, placement inside 004's layout). The names are
binding now: the qa agent may author the 012 specs against them.

## Changelog line (for the merged document)

> 1.11.0 (additive, feature 012) - practice panel in the mixer (speed, transpose, reset, effective
> tempo, quality hint, stretch status), count-in controls and indicator, speed trainer, the export
> dialog's practice note. All 1.21.0 ids are unchanged. (Editorial 012 integration amendment,
> label unchanged: the practice panel is a disclosure - `practice-panel-toggle`,
> `practice-panel-body`, `practice-panel-summary`.)

## Level visibility (R7-R9 apply)

- At **Advanced** (the level of the existing suite) every id below renders under the conditions in
  the table, except the Expert-only ones.
- At **Beginner** only these render: `practice-panel`, `practice-panel-toggle`, `practice-panel-body`,
  `practice-panel-summary`, `practice-speed`, `practice-speed-value`,
  `practice-speed-down`, `practice-speed-up`, `practice-transpose`, `practice-transpose-down`,
  `practice-transpose-up`, `practice-reset`, `practice-quality-hint`, `practice-unavailable`,
  `practice-effective-bpm`, `countin-toggle`, `countin-indicator`, `countin-unavailable`,
  `export-practice-note`.
- **Expert** adds `practice-speed-input`, `practice-transpose-input`, `practice-stretch-status`.
- No 1.21.0 id changes its level rule.

## Ids

| Screen | `data-testid` | Element / attributes | Levels |
|---|---|---|---|
| Mixer (`/tracks/{id}/mixer`) | `practice-panel` | region "Practice tempo" (`role="group"`, `aria-labelledby`); attributes `data-stretch` = `bypass`/`loading`/`active`/`unavailable` (engine `PracticeStatus.stretch`), `data-speed` (integer heard now), `data-transpose` (integer), `data-expanded` = `true`/`false` (012 integration) | all |
| Mixer | `practice-panel-toggle` | disclosure button inside the panel's heading, text "Practice tempo" (plus the summary while closed); `aria-expanded`, `aria-controls="practice-panel-body"`; help marker `practice-panel`. Open/closed is remembered per browser (`localStorage["stemstudio.practice-panel"]`); without a stored choice the panel is open exactly while the heard speed is not 100, the transpose not 0, the count-in on or the trainer on (012 integration, CTO decision 2026-11-17) | all |
| Mixer | `practice-panel-body` | container of every other practice id below except `countin-indicator`; always rendered, `hidden` while the panel is closed (its ids keep their level rules; R7-R9 count them as rendered) | all |
| Mixer | `practice-panel-summary` | text inside `practice-panel-toggle`, e.g. "75 %, +2 st, count-in" (parts: heard speed, transpose, `count-in`, `trainer`); exists only while the panel is closed and something is not neutral | all |
| Mixer | `practice-speed` | slider 50-120, step 1 (arrow keys 1, Page Up/Down 10, Home 50, End 120); `aria-valuenow`, `aria-valuetext` "75 percent"; read-only (`aria-readonly="true"`) while the speed trainer runs; disabled while `data-stretch="unavailable"` | all |
| Mixer | `practice-speed-value` | text "75 %" (the speed heard now) | all |
| Mixer | `practice-speed-down`, `practice-speed-up` | buttons -5 % / +5 % (clamped) | all |
| Mixer | `practice-speed-reset` | button "100 %"; exists only while the speed is not 100 | Advanced, Expert |
| Mixer | `practice-speed-input` | number input 50-120 mirroring the slider | Expert |
| Mixer | `practice-transpose` | spinbutton -12..12 (`aria-valuetext` "+2 semitones", "0 semitones", "-3 semitones"); text shows "+2" | all |
| Mixer | `practice-transpose-down`, `practice-transpose-up` | buttons -1 / +1 semitone | all |
| Mixer | `practice-transpose-input` | number input -12..12 | Expert |
| Mixer | `practice-reset` | button "Original speed and key" (100 %, 0); disabled when both are already neutral | all |
| Mixer | `practice-effective-bpm` | text "≈ 90 BPM" (track tempo x speed, rounded); exists only when a tempo is known (reliable grid or `metronome-bpm` override) | all |
| Mixer | `practice-quality-hint` | inline hint; exists only while the heard speed is below 70 % or abs(transpose) is above 7 | all |
| Mixer | `practice-unavailable` | exists only when `data-stretch="unavailable"`; text = engine reason + "Playback works at the original speed and key." | all |
| Mixer | `practice-stretch-status` | text "Stretch: active, latency 106 ms, 0 dropouts"; attributes `data-latency-ms`, `data-underruns` | Expert |
| Mixer | `countin-toggle` | switch "Count-in" (`aria-checked`); on = 1 bar at Beginner, restores the last bar count (1 or 2) at Advanced/Expert; disabled with `countin-unavailable` when no tempo is known | all |
| Mixer | `countin-bars` | select 1 / 2 bars | Advanced, Expert |
| Mixer | `countin-every-pass` | checkbox "Before every loop pass"; disabled (with `aria-describedby` -> `trainer-needs-loop` text) while no loop region is enabled | Advanced, Expert |
| Mixer | `countin-indicator` | `role="status"` (`aria-live="polite"`, announces "Count-in" once and "Go" at the end, not every beat); exists only while a count-in runs; attributes `data-beats-remaining`, `data-beats-total`; children `countin-dot-<n>` (n = 1..beatsTotal, `data-state` = `done`/`current`/`todo`) | all |
| Mixer | `countin-unavailable` | text "Count-in needs a beat grid or a manual BPM."; exists only when the count-in cannot be used | all |
| Mixer | `trainer-panel` | region "Speed trainer" | Advanced, Expert |
| Mixer | `trainer-toggle` | switch "Speed trainer" (`aria-checked`); disabled while no loop region is enabled or `data-stretch="unavailable"` | Advanced, Expert |
| Mixer | `trainer-needs-loop` | text "Set a loop (A-B) to use the speed trainer."; exists only while no loop region is enabled | Advanced, Expert |
| Mixer | `trainer-start`, `trainer-step`, `trainer-target`, `trainer-passes` | number inputs (start 50-119 %, step 1-20 %, target 51-120 %, passes per step 1-8) | Advanced, Expert |
| Mixer | `trainer-error` | validation message (e.g. "The target must be above the start speed."); exists only while invalid | Advanced, Expert |
| Mixer | `trainer-status` | text "Pass 3 - 80 % (target 100 %)"; attributes `data-state` = `off`/`waiting`/`running`/`target`, `data-pass`, `data-speed`; `aria-live="polite"` announces only speed steps | Advanced, Expert |
| Export dialog | `export-practice-note` | text "Exports use the original speed and key."; exists only while the mixer's speed is not 100 % or transpose is not 0 | all |

## Help markers (not test ids)

`data-help-id` values set by 012 (validated by the 004 help tests): `practice-panel` (on
`practice-panel-toggle`, topic `practice-speed`; the practice steps of the `mixer-basics` and `mixer-advanced` tours
point here, so a closed panel does not skip them), `practice-speed` (on `practice-speed`), `practice-transpose` (on `practice-transpose`), `practice-count-in` (on
`countin-toggle`), `practice-speed-trainer` (on `trainer-panel`), `practice-sound-quality` (on
`practice-quality-hint` and `practice-stretch-status`).

## Shortcut commands (design-spec.md 6 addendum; mixer scope; web-interop 1.0.0 unchanged)

| Key | Command id | Effect | Levels |
|---|---|---|---|
| `-` | `mixer.speed:-5` | speed -5 % | all |
| `=` | `mixer.speed:5` | speed +5 % | all |
| `,` | `mixer.transpose:-1` | -1 semitone | all |
| `.` | `mixer.transpose:1` | +1 semitone | all |
| `r` | `mixer.practice.reset` | original speed and key | all |
| `c` | `mixer.countin.toggle` | count-in on/off | all |
| `t` | `mixer.trainer.toggle` | speed trainer on/off (ignored below Advanced and without a loop) | Advanced, Expert |

Same rules as the 004 bindings: ignored in typing contexts and dialogs, no modifier other than
Shift, `t` as the second key of the `G then T` sequence stays `go.tracks`. The shortcut overlay
lists them under "Practice".
