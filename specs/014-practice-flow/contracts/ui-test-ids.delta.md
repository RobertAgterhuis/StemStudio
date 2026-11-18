# Contract delta: UI test ids for feature 014 (draft, additive)

Status: **binding names** (014 planning, 2026-11-16; Phase 0 part 2, 2026-11-17: the practice log panel ids are `practice-log-*` because 012's 1.11.0 already has `practice-panel` and `practice-panel-toggle` for the speed and key panel; `trainer-region-label` added; merged into `ui-test-ids.md` 1.13.0). Base: the full document of
feature 013 (expected **1.12.0**; 011 is 1.10.0, 012 is 1.11.0). In Phase 0 part 2 the architect
merges this delta unchanged into `specs/014-practice-flow/contracts/ui-test-ids.md` **1.13.0**.
Nothing below renames, removes or changes an existing id; every existing rule applies
(`getByTestId` only, level visibility R7-R9, placement inside 004's layout). If 012 already
defines an id listed here (e.g. a speed-trainer region label), 012's id wins and this entry is
dropped at the merge.

## Changelog line (for the merged document)

> 1.13.0 (additive, feature 014) - section lane, section editor, score follow panel, practice panel
> and Practice page, setlists page, setlist editor, setlist play bar and PDF dialog, shortcut
> settings, pedal setup and the extended shortcut overview. All 1.12.0 ids are unchanged.

## Ids

`<sectionId>`, `<setlistId>`, `<entryId>`, `<assetId>`, `<trackId>` are UUIDs; `<command>` is a
command id of data-model 5.2 with `:` and `.` replaced by `-` (e.g. `mixer-seek--5`,
`mixer-section-next`); `<n>` a 1-based number.

| Screen | `data-testid` | Element / attributes | Levels |
|---|---|---|---|
| Mixer | `section-lane` | list (`role="list"`) of section buttons; attributes `data-status` = `none`/`queued`/`detecting`/`ready`/`failed`, `data-revision` | all |
| Mixer | `section-<sectionId>` | button in the lane; text = label; attributes `data-kind`, `data-start`, `data-end` (seconds, 1 decimal), `data-current` = `true`/`false`, `data-looped` = `true`/`false`; `aria-pressed` = looped | all |
| Mixer | `section-status` | status text ("Detecting sections...", "Detected automatically - check and edit", "Tempo grid uncertain - boundaries may be off", failure reason) | all |
| Mixer | `section-retry` | exists when status is `failed` | all |
| Mixer | `section-previous`, `section-next`, `section-restart`, `section-loop-current` | transport-row buttons | all |
| Mixer | `trainer-region-label` | text in the 012 trainer panel naming the loop region: "Region: Guitar solo (0:40-0:56)" with a section loop, "Region: A-B loop (0:40-0:56)" otherwise (DELTA R-4: 012 has no such id) | advanced, expert |
| Mixer | `section-edit` | opens the section editor | advanced, expert |
| Mixer | `section-detect-again` | re-detect (confirmation when edited) | expert |
| Mixer | `section-confidence-<sectionId>` | confidence text ("82 %") | expert |
| Section editor | `section-editor`, `section-editor-done`, `section-editor-cancel`, `section-editor-error` | panel and actions; error shows 409/422 text | advanced, expert |
| Section editor | `section-editor-row-<n>` | one row per section in order; children `section-editor-kind-<n>` (select), `section-editor-label-<n>` (text), `section-editor-start-<n>` (bar or time field), `section-editor-instrument-<n>` (select, solos only), `section-editor-delete-<n>`, `section-editor-merge-<n>` (with next) | advanced, expert |
| Section editor | `section-editor-split` (at playhead), `section-editor-add` | buttons | advanced, expert |
| Confirm dialog | `section-overwrite-confirm`, `section-overwrite-cancel` | "Replace your edited sections?" | expert |
| Mixer | `score-panel-toggle` | opens/closes the Score panel (`aria-expanded`) | all |
| Score panel | `score-panel` | region (`aria-label="Score"`); attributes `data-asset-id`, `data-view-status` | all |
| Score panel | `score-select` | select of the track's score items (option text "Bass - <name>") | all |
| Score panel | `score-empty` | "No sheet music for this song yet" with a link to the 008 action | all |
| Score panel | `score-preparing` | "Preparing the score view..." | all |
| Score panel | `score-view` | the follower host; engine writes `data-bar-index`, `data-bar-number`, `data-page`, `data-position-ms` | all |
| Score panel | `score-page-<n>` | page container (`data-score-page`) with the `<img>` | all |
| Score panel | `score-follow-toggle` | auto-scroll on/off (`aria-pressed`) | all |
| Score panel | `score-transpose-note` | exists while transpose is not 0 | all |
| Score panel | `score-tempo-note` | exists for scores made with a manual tempo | all |
| Score panel | `score-bar-label` | visually hidden live text "Bar 17 of 64" (updated at most once per second, `aria-live="off"` by default; a "Read bar" button `score-read-bar` announces it once) | all |
| Mixer | `practice-log-panel-toggle`, `practice-log-panel` | Practice log panel (`aria-label="Practice log"`; 012's `practice-panel` is the speed and key panel) | all |
| Practice log panel | `practice-log-today`, `practice-log-week`, `practice-log-total` | durations as text ("12 min") | all |
| Practice log panel | `practice-log-sections-table` | table of time per section, trainer best | advanced, expert |
| Practice log panel | `practice-log-chart-days`, `practice-log-chart-tempo` | charts; each has a sibling `practice-log-chart-days-table` / `practice-log-chart-tempo-table` toggled by `practice-log-chart-as-table` | all (tempo chart: advanced, expert) |
| Practice log panel | `practice-log-off-note` | "Practice log is off" with `practice-log-turn-on` | all |
| Practice log panel | `practice-log-notice`, `practice-log-notice-ok`, `practice-log-notice-turn-off` | first-use notice | all |
| Practice log panel | `practice-log-delete-track`, `practice-log-delete-confirm` | delete this song's log | all |
| Practice page (`/practice`) | `practice-page`, `practice-enabled` (switch), `practice-delete-all`, `practice-overview-table`, `practice-overview-row-<trackId>`, `practice-sessions` (Expert list), `practice-session-<sessionId>`, `practice-session-delete-<sessionId>` | | all (sessions: expert) |
| Shell | `nav-setlists`, `nav-practice` | rail links to `/setlists`, `/practice` | all |
| Setlists page (`/setlists`) | `setlists-page`, `setlist-create`, `setlist-create-name`, `setlist-create-confirm`, `setlist-row-<setlistId>`, `setlist-open-<setlistId>`, `setlist-play-<setlistId>`, `setlist-delete-<setlistId>`, `setlists-empty` | | all |
| Setlist editor (`/setlists/{id}`) | `setlist-name`, `setlist-note`, `setlist-total`, `setlist-add` (opens track picker `setlist-add-dialog`, items `setlist-add-track-<trackId>`, mix select `setlist-add-mix`, `setlist-add-confirm`), `setlist-entry-<entryId>` (row; `data-position`), `setlist-entry-note-<entryId>`, `setlist-entry-mix-<entryId>`, `setlist-entry-up-<entryId>`, `setlist-entry-down-<entryId>`, `setlist-entry-remove-<entryId>`, `setlist-removed-note`, `setlist-save-error` | | all (note fields, mix select: advanced, expert) |
| Setlist editor | `setlist-pdf`, `setlist-pdf-dialog`, `setlist-pdf-lyrics`, `setlist-pdf-sections`, `setlist-pdf-notes`, `setlist-pdf-page-break`, `setlist-pdf-download`, `setlist-pdf-error` | PDF export (options: advanced, expert; beginner downloads with defaults) | all |
| Track / mix actions | `add-to-setlist-<trackId>`, `add-to-setlist-mix-<mixId>` | "Add to setlist" | all |
| Mixer (setlist play mode) | `setlist-bar` (attributes `data-setlist-id`, `data-position`, `data-count`), `setlist-bar-previous`, `setlist-bar-next`, `setlist-bar-note`, `setlist-bar-exit` | | all |
| Mixer | `mixer-console` | the mixer's console (root of the page under the journey rail); attribute `data-track-id` = the track it plays. Setlist play mode specs assert the shown song with it (added in the r14 fix round, 2026-11-18) | all |
| Shortcut overlay | `shortcut-overlay` (existing), `shortcut-group-<group>` (`transport`, `loop-sections`, `speed`, `channels`, `setlist`, `score`, `navigation`), `shortcut-row-<command>` with `data-keys` (e.g. `n|pagedown`) | all |
| Preferences panel | `pref-single-key` (switch "Single-key shortcuts"), `pref-shortcuts-open` (opens `/settings/shortcuts`) | all |
| Shortcut settings (`/settings/shortcuts`) | `shortcut-settings`, `shortcut-edit-<command>`, `shortcut-capture` (listening state), `shortcut-capture-cancel`, `shortcut-conflict` (reason text), `shortcut-clear-<command>`, `shortcut-reset-all` | advanced, expert |
| Shortcut settings | `pedal-setup`, `pedal-mode` (switch), `pedal-preset` (select), `pedal-learn-left`, `pedal-learn-right`, `pedal-left-key`, `pedal-right-key`, `pedal-test` (shows the last pedal command) | advanced, expert |
