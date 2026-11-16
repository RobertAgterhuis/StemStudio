# Contract delta: UI test ids 1.18.0 (feature 019)

**Additive minor** on top of the version current when 019 starts (018's 1.17.0 by the roadmap
allocation). All earlier ids keep their name, element and meaning. Phase 0 (tasks.md T003)
merges this table into the full `ui-test-ids.md` 1.18.0. Rules of `ui-test-ids.md` apply:
static kebab-case ids on the interactive element itself; `<index>` is the 0-based region index;
`<n>` a bar number (0 = pickup); `<channel>` a `ChannelId` with `.` written as `-`
(e.g. `drums-snare`); `<arrangementId>`, `<mixId>`, `<assetId>` are lower-case `D` GUIDs.

Changelog line: 1.18.0 (additive, feature 019) - arrangement editor in the mixer, export option
"Use arrangement", mix and library badges, Beginner read-only chip. All 1.17.0 ids are unchanged.

| Screen | New `data-testid` |
|---|---|
| Mixer, tabs | `mixer-tab-arrange` (Advanced+; absent at Beginner) |
| Mixer, read-only (any level, mix with an arrangement) | `arrange-chip` (text "Arranged: <name>"), `arrange-chip-song-order` (switch) |
| Arrange toolbar | `arrange-panel`, `arrange-select` (select: `song-order` + one option per arrangement id), `arrange-new-sections`, `arrange-new-song`, `arrange-name`, `arrange-save`, `arrange-save-as`, `arrange-duplicate`, `arrange-delete`, `arrange-undo`, `arrange-redo`, `arrange-export`, `arrange-unsaved` (present only with unsaved edits), `arrange-total` (arranged length text; `data-seconds`), `arrange-error` (validation/limit message), `arrange-warning` (grid-unreliable / bar-lines-changed / unknown-channel), `arrange-snap-bar-lines` (only when stale), `arrange-live` (polite live region) |
| Arrange toolbar, Expert | `arrange-crossfade` (number 0-50 ms), `arrange-snap` (select `bars`/`free`) |
| Song strip | `arrange-song-strip`, `arrange-bar-<n>` (button; `aria-pressed` = in selection), `arrange-section-<sectionId>` (014 section chip; selects its bars), `arrange-from-bar`, `arrange-to-bar` (number inputs), `arrange-selection` (text "Bars a-b"), `arrange-add-region` |
| Region lane | `arrange-lane` (`role=listbox`), `arrange-region-<index>` (`role=option`; `data-start-bar`, `data-end-bar`, `data-repeat`, `data-muted` = space-separated channel ids, `aria-current="true"` while it plays), `arrange-region-repeat-<index>` (badge "x4"), `arrange-playhead` (`role=slider`, `aria-valuenow` = arranged seconds, `aria-valuemax` = arranged length) |
| Region inspector | `region-inspector`, `region-label`, `region-start-bar`, `region-end-bar`, `region-repeat` (number 1-16), `region-mute-<channel>` (checkbox, one per channel of the track incl. kept detail stems), `region-mute-note` (stored mute of a missing channel), `region-move-left`, `region-move-right`, `region-duplicate`, `region-split`, `region-remove`, `region-play`, `region-loop` (toggle: loop this region) |
| Region inspector, Expert | `region-start-seconds`, `region-end-seconds` (free timing) |
| Confirm dialogs (existing) | `confirm-dialog-ok`, `confirm-dialog-cancel`; delete confirmation lists the mixes in `arrange-delete-mixes`; unsaved guard buttons `arrange-guard-save`, `arrange-guard-discard`, `arrange-guard-stay`; conflict dialog `arrange-conflict-reload`, `arrange-conflict-copy` |
| Export dialog (mixer) | `export-arrangement` (checkbox "Use arrangement <name>", checked by default when a timeline is active; absent in song order), `export-arrangement-name` (text) |
| Mixes list | `mix-arrangement-<mixId>` (badge with the arrangement name; absent in song order) |
| Recording (015) | existing record button gets `aria-disabled="true"` and `record-disabled-reason` text while arranged |

## Level visibility (R-rules, additive)

- R19-1: `mixer-tab-arrange` and everything inside `arrange-panel` require level Advanced.
- R19-2: `arrange-crossfade`, `arrange-snap`, `region-start-seconds`, `region-end-seconds`
  require level Expert.
- R19-3: `arrange-chip` and `arrange-chip-song-order` are rendered at every level when the active
  mix has an arrangement (at Advanced+ only while the Arrange tab is closed).
