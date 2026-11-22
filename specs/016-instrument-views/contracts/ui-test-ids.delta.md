# Contract delta: UI test ids for feature 016 (draft, additive)

Status: **merged** (016 Phase 0 T004, 2026-11-18) into `specs/016-instrument-views/contracts/ui-test-ids.md`
**1.15.0** = 015's 1.14.0 plus the table below unchanged (section "Additions in 1.15.0 (feature 016)"); Phase 0
fixes are listed under "Phase 0 changes". Planning base was 1.14.0 (015). Nothing below renames, removes or changes an existing id; every existing rule applies
(R1-R9: `getByTestId` only, level visibility, placement inside 004's layout).

Binding for the **frontend** agent once merged; the **qa** agent may author the 016 specs
against these names now.

## Changelog line (for the merged document)

> 1.15.0 (additive, feature 016) - instrument views: "Practise as" picker, song key and key
> regions, capo mode with suggestions, chords lane and banner chords, chord sheet in the lyrics card, chord edit/export, tab and part options in the sheet
> music dialog, MIDI actions per channel, drum trainer, Drumless, singing feedback panel, score
> facts for tab and parts. All 1.14.0 ids are unchanged.

## Placeholders

`<key>` a channel key as in `strip-<key>` (`<stem>` or `<parent>-<detail>`); `<family>` one of
`vocals`, `drums`, `bass`, `guitar`, `keys`, `orchestral`; `<piece>` one of `kick`, `snare`,
`toms`, `hihat`, `cymbals`; `<n>` a zero-based index; `<assetId>` as in 1.7.0.

## Ids

| Screen | `data-testid` | Element / attributes | Levels |
|---|---|---|---|
| Mixer | `practise-as` | menu button "Practise as ..."; `data-family` = chosen family or empty | all |
| Mixer | `practise-as-<family>` | menu items (`role="menuitemradio"`, `aria-checked`) | all |
| Mixer | `practise-as-offer` | chip "Practise as <family> again?" (remembered family, not applied) | all |
| Mixer | `instrument-view` | region (`aria-label` = view name) that hosts the open family view; `data-view` = `chords`/`tablature`/`drum-trainer`/`singing`/`part` | all |
| Mixer | `chord-lane` | engine host (rendered **empty**, `@key`); engine writes `data-chord-index`, `data-current-symbol`, `data-next-symbol`, `data-position-ms`; children `.chord-lane-item[data-state]` (no test ids) | all |
| Mixer | `chord-lane-toggle` | show/hide the lane (`aria-pressed`) | all |
| Mixer | `chords-status` | `data-status` = `AnalysisStatus`; text readable status, progress while running | all |
| Mixer | `chords-detect` | "Detect chords" (status `none`, `failed`, `stale`, `ready` at Advanced = re-detect) | all |
| Mixer | `chords-vocabulary` | select `simple`/`full` | advanced, expert |
| Mixer | `chords-engine` | select `btc`/`chroma-hmm` | expert |
| Mixer | `chords-limits` | quality limits text (FR-064) | all |
| Mixer | `chords-overwrite-confirm` | confirm dialog when edits exist (uses `confirm-dialog-ok` / `confirm-dialog-cancel`) | advanced, expert |
| Mixer | (attribute) `data-chords="true"` on `karaoke-banner` | set by Blazor while banner chords are attached; chord elements `.karaoke-chord[data-state][data-lead]` are engine-owned | all |
| Track detail, Mixer | `track-key` / `mixer-key` | key next to `track-bpm` / the mixer tempo; text = display (data-model 2.8); `data-tonic` (0-11), `data-mode`, `data-uncertain` (`true`/`false`); empty element with `data-status` while chords are not ready | all |
| Mixer | `mixer-key-current` | key of the current region during playback (mirrors the chord lane host's `data-current-key`); `data-region-index` | all |
| Mixer | `mixer-key-written` | "Your part: A major" for a transposing instrument of the orchestral family (exists only then) | all |
| Mixer | `mixer-key-confidence` | "81 % (or A minor)" | advanced, expert |
| Track detail | `track-key-regions` | list of key regions; items `track-key-region-<n>` with text "Bar 9: D major" | advanced, expert |
| Mixer, Track detail | `capo-mode` | toggle (`aria-pressed`) of capo mode; exists only for the guitar family (chosen in "Practise as" or the chord view's menu) | all |
| Mixer, Track detail | `capo-badge` | "Capo 3"; exists only while capo mode is on with capo > 0 | all |
| Mixer, Track detail | `capo-select` | number input 0..12 (`aria-valuenow`); exists only for the guitar family | all |
| Mixer, Track detail | `capo-suggestions` | plain list (`<ul>`, no `listbox`: options may not hold buttons); items `capo-suggestion-<n>` are buttons (text "Capo 3 - C shapes, 100 % open"; `data-capo`) | all |
| Mixer, Track detail | `capo-shortcut` | button "Capo 2: play the original shapes" (exists only when a shortcut applies) | all |
| Mixer, Track detail | `capo-show-sounding` | checkbox | advanced, expert |
| Track detail | `chord-sheet` | chord-over-lyrics rows inside `lyrics-section`; each row `chord-sheet-row-<n>` with chord spans `chord-sheet-chord-<n>-<m>` (text = symbol) | all |
| Track detail | `chord-sheet-empty` | "No chords were detected" / not ready text | all |
| Track detail | `chord-sheet-transpose` | number input -12..12 (display only; the capo comes from `capo-select` of capo mode) | all |
| Track detail | `chord-sheet-download` | ChordPro download link | advanced, expert |
| Track detail | `chord-edit-<n>` | edit button per chord span (opens `chord-edit-dialog`) | advanced, expert |
| Track detail | `chord-edit-dialog`, `chord-edit-root`, `chord-edit-quality`, `chord-edit-bass`, `chord-edit-save`, `chord-edit-cancel`, `chord-edit-error` | edit dialog | advanced, expert |
| Track detail, Mixer | `midi-<key>` | "MIDI" action per channel (track page stem/detail rows, mixer strip menu); `aria-busy="true"` while a score is generated for it | all |
| Track detail, Mixer | `midi-progress-<key>` | progress (`aria-valuenow` 0-100) while generating | all |
| Track detail, Mixer | `midi-error-<key>` | readable reason | all |
| Sheet music dialog | `score-tab-layout` | select `notation-and-tab`/`tab-only`/`none` (guitar, bass profiles only; `none` sends `tablature: null`) | all |
| Sheet music dialog | `score-tab-tuning` | select of catalogue tunings for the profile | all (Beginner: standard only + drop D) |
| Sheet music dialog | `score-tab-capo` | number input 0..12 (guitar only) | advanced, expert |
| Sheet music dialog | `score-part-instrument` | select of the written instrument catalogue (`concert` first); also in the instrument view (Practise as strings/brass/woodwinds) | all |
| Sheet music dialog | `score-part-clef` | select of clefs | advanced, expert |
| Sheet music dialog | `score-include-chords` | checkbox (disabled with `aria-describedby` reason when chords are not ready) | advanced, expert |
| Library | `asset-score-tab-<assetId>` | text e.g. "Tab: Drop D, capo 2 (3 notes moved by an octave)" | all |
| Library | `asset-score-part-<assetId>` | text e.g. "Alto Saxophone in E-flat, treble clef (2 notes out of range)" | all |
| Mixer | `drumless` | toggle button (`aria-pressed`); pressed = minus-one drums applied | all |
| Mixer | `drum-trainer` | region of the drum trainer | all |
| Drum trainer | `drum-lanes` | engine host (rendered **empty**, `@key`, `role="img"`, `aria-label`); engine writes `data-position-ms`, `data-next-hit-ms`, `data-next-hit-piece`, `data-visible-hits`, `data-look-ahead-ms` | all |
| Drum trainer | `drum-lane-label-<piece>` | button per lane label; at Advanced+ toggles the guide piece (`aria-pressed`) | all (button behaviour advanced, expert) |
| Drum trainer | `drum-hits-status` | `data-status`; readable status/progress | all |
| Drum trainer | `drum-hits-split-required` | notice with `drum-hits-split-start` (starts the detail phase) | all |
| Drum trainer | `drum-look-ahead` | slider 2000..8000 ms (`aria-valuenow`) | advanced, expert |
| Drum trainer | `drum-bar-text` | visually hidden text list of the hits of the current bar (text alternative, FR-062) | all |
| Drum trainer | `drum-score` | the 014 score view host for the track's drum score item; `drum-score-generate` when none exists | all |
| Mixer | `singing` | region of the singing feedback panel | all |
| Singing | `singing-toggle` | start/stop (`aria-pressed`); disabled with reason while the reference is not ready | all |
| Singing | `singing-state` | `data-state` = `SingingState`; readable text | all |
| Singing | `singing-reference-status` | `data-status`; readable status, `singing-reference-analyse` button and `singing-reference-estimate` text | all |
| Singing | `pitch-view` | engine host (rendered **empty**, `@key`, `role="img"`); engine writes `data-state`, `data-singer-cents`, `data-target-cents`, `data-unit-index`, `data-last-score`, `data-latency-source` | all |
| Singing | `singing-tolerance` | slider 10..200 step 5 (`aria-valuenow` = cents) | advanced, expert |
| Singing | `singing-latency` | text "Latency: 42 ms (calibrated)" (015 `LatencyMethod` `loopback` or `manual`) / "(estimated - calibrate in Recording settings)" (`estimated`) with a link to the 015 settings; `data-latency-source` = the method | advanced, expert |
| Singing | `singing-raw` | text "220.0 Hz, A3 +3 cents" | expert |
| Singing | `singing-headphones-hint` | hint text | all |
| Singing | `singing-error` | readable reason (no permission, no device, too slow) with link to the 015 input settings | all |
| Singing | `singing-announcer` | `aria-live="polite"` region; receives "Line 3: 87 points" | all |
| Singing | `singing-summary` | summary card after stop/end: `data-average`, `data-units`; text | all |
| Karaoke banner | `karaoke-score` | Blazor element next to the banner host showing the last unit score; `data-unit-index`, `data-score` (empty for null) | all |
| Takes (015 list) | `take-score-<takeId>` | "Score this take" | all |
| Takes (015 list) | `take-score-result-<takeId>` | result text and `data-average` | all |

## Help anchors (`data-help-id`)

`practise-as`, `chords` (on `chord-lane`), `chords-vocabulary`, `chord-sheet`, `chord-edit`,
`midi-download` (on every `midi-<key>`, pattern `midi-*`), `score-tablature` (on
`score-tab-layout`), `score-tab-tuning`, `score-part` (on `score-part-instrument`),
`score-part-clef`, `drumless`, `drum-trainer`, `drum-lanes`, `singing`, `singing-tolerance`,
`singing-latency`, `pitch-view`, `song-key` (on `track-key`, `mixer-key`), `capo-mode` (on
`capo-mode`, `capo-suggestions`).

## Phase 0 changes (2026-11-18)

- `capo-select`: the planning text named a non-existent `chords-capo`; removed.
- `capo-suggestions`: written down as a plain list of buttons (LESSONS frontend: `listbox` options may not hold
  interactive content).
- `score-tab-layout`: `none` = `tablature: null` in the request (the contract enum has two layouts).
- `singing-latency`: mapped to the 015 `LatencyMethod` values (`loopback`/`manual` = calibrated, `estimated`).
- Help anchor `song-key` is a `data-help-id`; the 010 `data-testid` `song-key` (song page input) is a different
  attribute and is unchanged.
