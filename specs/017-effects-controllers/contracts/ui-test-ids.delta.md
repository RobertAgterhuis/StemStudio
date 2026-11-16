# Contract delta: UI test ids 1.16.0 (feature 017)

Additive on top of the ui-test-ids version current when 017 starts (1.15.0 from 016). All
existing ids keep their name, element and meaning. `<key>` is a strip key as in 1.3.0 (`<stem>` or
`<parent>-<detail>`; 015 take strips use 015's key). `<presetId>`, `<profileId>` (canonical GUID)
and `<bindingId>` (8 chars) are data ids. Levels: B = Beginner, A = Advanced, E = Expert.

## Mixer (`/tracks/{id}/mixer`)

| Area | `data-testid` | Level | Element / notes |
|---|---|---|---|
| Practice EQ | `practice-eq-<key>` | B A E | menu button on each strip; `aria-haspopup="menu"` |
| | `practice-eq-focus-<key>`, `practice-eq-make-room-<key>`, `practice-eq-off-<key>` | B A E | menu items (rendered while open); make-room item `aria-disabled="true"` with reason when the track has one audible strip |
| | (attribute) `data-practice-eq` on `strip-<key>` | B A E | `focus`, `make-room` or `none` |
| | `strip-has-effects-<key>` | B | marker on a strip with saved non-practice effects; links help `mixer-effects` |
| Strip | `fx-open-<key>` | A E | opens the effects panel; `data-active="true"` when the strip has active effects |
| | `fx-bypass-<key>` | A E | toggle button, `aria-pressed` |
| Effects panel | `fx-panel` | A E | `role="dialog"`, labelled by `fx-panel-title` |
| | `fx-panel-title`, `fx-close`, `fx-reset`, `fx-copy`, `fx-paste` | A E | `fx-paste` disabled while the clipboard is empty |
| | `fx-preset-menu`, `fx-preset-<presetId>`, `fx-preset-modified` | A E | items render while open; `fx-preset-modified` only while values differ from the preset |
| EQ | `fx-eq-toggle`, `fx-eq-curve` | A E | toggle (`aria-pressed`); `<svg>` drawn by the engine, `aria-hidden="true"` |
| | `fx-eq-hp-toggle`, `fx-eq-hp-freq`, `fx-eq-lp-toggle`, `fx-eq-lp-freq` | A E | sliders expose `aria-valuenow` in Hz and `aria-valuetext` ("2.5 kHz") |
| | `fx-eq-hp-slope`, `fx-eq-lp-slope` | E | segmented 12/24 |
| | `fx-eq-lowshelf-freq`, `fx-eq-lowshelf-gain`, `fx-eq-mid1-freq`, `fx-eq-mid1-gain`, `fx-eq-mid2-freq`, `fx-eq-mid2-gain`, `fx-eq-highshelf-freq`, `fx-eq-highshelf-gain` | A E | gain `aria-valuenow` in dB with one decimal |
| | `fx-eq-mid1-q`, `fx-eq-mid2-q` | E | |
| Compressor | `fx-comp-toggle`, `fx-comp-threshold`, `fx-comp-ratio`, `fx-comp-makeup` | A E | |
| | `fx-comp-knee`, `fx-comp-attack`, `fx-comp-release` | E | |
| | `fx-comp-gr` | E | `role="meter"`; engine writes `data-gr-db`, `--gr-level`, `aria-valuenow` |
| Send | `fx-reverb-send` | A E | slider 0-100 |
| Numeric entry | `fx-number-<control>` | E | text input next to each slider (`<control>` = the slider id without `fx-`, e.g. `fx-number-eq-mid1-freq`) |
| Errors | `fx-error` | A E | readable reason (e.g. an invalid typed value) |
| Master section | `reverb-kind`, `reverb-return`, `meter-reverb` | A E | `meter-reverb` written by the engine like other meters |
| | `reverb-predelay`, `fx-load` | E | `fx-load` carries `data-load` (0..1) and `data-cost-units` |
| | `fx-bypass-all` | A E | toggle, `aria-pressed`; `data-state` not saved |
| MIDI | `midi-status` | A E | chip; `data-state` = `unsupported`/`insecure-context`/`prompt`/`denied`/`no-device`/`connected`; links `/controllers` |
| | `midi-learn-toggle` | A E | toggle, `aria-pressed`; disabled unless `connected` |
| | `midi-learn-prompt`, `midi-learn-cancel` | A E | `role="status"` (polite) while learning |
| | (attribute) `data-midi-learn="true"` on every mappable control while learn mode is on; `data-midi-bound="true"` when bound | A E | |
| | `midi-bank-indicator` | A E | "Strips 1-8"; shown when the active profile has strip bindings |

Learn conflicts use the existing `confirm-dialog-ok` / `confirm-dialog-cancel`.

## Export dialog (mixer)

| `data-testid` | Level | Notes |
|---|---|---|
| `export-effects` | A E | checkbox "Include effects (as you hear them)"; present only in modes "What I hear"/"Selected channels" when the mix has effects; unchecked and disabled while `fx-bypass-all` is on |
| `export-effects-note` | A E | explains "without browser" / bypass / long-track memory warning |
| `export-prerender-progress` | A E | progress bar, `aria-valuenow` 0-100 |
| `export-prerender-cancel` | A E | cancels render/upload; no item is created |

Beginner: the mix is exported with effects when it has them (no checkbox; the note says so).

## Library (`/library`)

| `data-testid` | Level | Notes |
|---|---|---|
| `asset-effects-<assetId>` | all | badge "with effects" / "without effects"; only when the source mix had effects |

## Controllers page (`/controllers`, new)

| `data-testid` | Level | Notes |
|---|---|---|
| `nav-controllers` | A E | navigation entry (Beginner: the page shows only `controllers-level-note`) |
| `controllers-level-note` | B | "Hardware controllers are available at level Advanced" with the level switch |
| `controllers-support` | A E | availability text; `data-reason` = `MidiAvailability` |
| `controllers-connect` | A E | "Connect controller" (shown for `prompt`), "Try again" for `denied` |
| `controllers-device-list`, `controllers-empty` | A E | list of inputs; empty state for `no-device` |
| `controllers-profile-<profileId>` | A E | profile card (device name, manufacturer, connected state in `data-connected`) |
| `controllers-profile-enable-<profileId>`, `controllers-profile-rename-<profileId>`, `controllers-profile-delete-<profileId>` | A E | |
| `controllers-strips-per-bank-<profileId>` | E | number 1-16 |
| `controllers-binding-<bindingId>` | A E | table row: control, target, mode |
| `controllers-binding-learn-<bindingId>`, `controllers-binding-delete-<bindingId>` | A E | re-learn, delete |
| `controllers-binding-mode-<bindingId>`, `controllers-binding-invert-<bindingId>`, `controllers-binding-pickup-<bindingId>` | E | |
| `controllers-binding-unavailable-<bindingId>` | A E | "not available here" (e.g. speed without 012) |
| `controllers-add-binding` | A E | opens a target picker, then learns |
| `controllers-monitor` | E | last message (`role="log"`, polite); raw bytes in hex |
| `controllers-error` | A E | readable reason (save failed, validation) |

## Level visibility rule R10 (binding)

At level Beginner no `fx-*`, `reverb-*`, `midi-*` or `controllers-*` id except
`controllers-level-note` is rendered; `practice-eq-*` and `strip-has-effects-<key>` are. Effects
and an enabled controller profile keep working at every level.
