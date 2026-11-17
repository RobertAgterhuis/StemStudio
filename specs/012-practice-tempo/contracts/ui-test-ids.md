# Contract: UI test ids (v1.11.0)

Binding for the **frontend** agent (renders the attributes) and the **qa** agent (Playwright
selectors use `page.getByTestId(...)` only). Changing, renaming or removing an id is a breaking
change: bump the version here and record it in the owner's hand-off.

Changelog: 1.1.0 (additive, feature 002) - detail-phase ids on the track detail page and grouped
detail strips in the mixer. All 1.0.0 ids are unchanged. 1.2.0 (additive, feature 003) - device
indication (GPU or CPU, CPU fallback) on the import page and the track detail page. All 1.1.0 ids
are unchanged. 1.3.0 (additive, feature 004; number confirmed in Phase 0 T003, 2026-11-15) -
app shell, experience levels, journey, presets, help, tours, command palette and expert
controls; ids that already existed in code but were not listed are adopted as binding; level
visibility rules R7-R9. All 1.2.0 ids keep their name, element and meaning. 1.4.0 (additive,
feature 005) - song artwork (cover, thumbnails, own picture) and the band info panel; ids may be
placed inside 004's layout, their names and meaning are binding. All 1.3.0 ids are unchanged.
1.5.0 (additive, feature 007) - export actions, the export dialog and the library page
(`/library`). All 1.4.0 ids are unchanged.
1.6.0 (additive, feature 006) - karaoke banner and controls in the mixer, lyrics section on the
track detail page. All 1.5.0 ids are unchanged.
1.7.0 (additive, feature 008) - sheet music actions, the sheet music dialog and score items in
the library. All 1.6.0 ids are unchanged.
1.8.0 (additive, feature 009) - generation status, restyle dialog, style library and editor,
generations list and take actions, AI badges. All 1.7.0 ids are unchanged.
1.9.0 (additive, feature 010) - song page, lyrics list and editor, voice profiles with recorder
and consent, "Use my voice", vocal badges. All 1.8.0 ids are unchanged.
1.10.0 (additive, feature 011) - agent panel on every page (toggle, conversation list, messages,
plan, steps with progress, approval cards, backend badge, unavailable state, undo, cancel, usage,
tool log), the settings page (`/settings`) with its agent section and its rail link. All 1.9.0
ids are unchanged.
1.21.0 (additive, 011 follow-up claude-key; merged from `ui-test-ids.delta.md` by the architect,
T002) - Claude key part of the settings agent section: status with source and last four, key
field, save, replace, cancel, delete with confirmation, test, local-only notice,
environment-override note and error; agent panel `agent-unavailable-switch-to-local` and
`agent-key-changed`. All 1.10.0 ids are unchanged; `data-key-configured` of
`settings-agent-claude-status` is now true for a usable key from either source. The numbers
1.11.0-1.20.0 stay allocated to 012-021 (ROADMAP contract version allocation).
1.11.0 (additive, feature 012; merged from `ui-test-ids.delta.md` by the architect, T003) -
practice panel in the mixer (speed, transpose, reset, effective tempo, quality hint, stretch
status), count-in controls and indicator, speed trainer, the export dialog's practice note and
the "Practice" shortcut commands. All 1.21.0 ids are unchanged. Merge order: this document is
1.21.0 plus the 012 delta and carries 012's allocated label 1.11.0 (labels identify the merged
deltas, not an order; ROADMAP "Contract version allocation"). Editorial 012 integration
amendment (before release, label unchanged): the practice panel is a disclosure
(`practice-panel-toggle`, `practice-panel-body`, `practice-panel-summary`, `data-expanded`, help
marker `practice-panel`).

## Rules

- Ids are static strings, kebab-case. `<stem>` is a stem name (`vocals`, `drums`, `bass`,
  `guitar`, `piano`, `other`); `<name>` in `track-stem-<name>` likewise; `<trackId>` and `<mixId>`
  are the GUIDs in their canonical lower-case `D` format. `<parent>` is a splittable stem
  (`vocals`, `drums`, `guitar`, `piano`, `other`); `<detail>` a detail stem name (`kick`,
  `snare`, `toms`, `hihat`, `cymbals`, `lead`, `backing`, `strings`, `brass`, `woodwinds`, `keys`,
  `synth`, `acoustic-guitar`, `electric-guitar`, `percussion`, `rest`).
- Put the id on the interactive element itself (the `input`, `button`), not on a wrapper, so
  Playwright can `fill`, `click` and read `aria-valuenow` directly.
- Meter elements are rendered by Blazor; the audio engine writes `--meter-level`,
  `data-peak-db` and `data-clip` into them (see [audio-engine.md](../../002-detail-separation/contracts/audio-engine.md), "Meters").
- Error elements (`*-error`) exist only while an error is shown and contain the readable reason.

## Ids per screen

| Screen | `data-testid` |
|---|---|
| Import (`/`) | `legal-notice`, `import-url-input`, `import-url-submit`, `import-file-input`, `import-error`, `stem-limitation-help` |
| Track detail (`/tracks/{id}`) | `track-title`, `track-status`, `track-stage`, `track-progress` (attribute `aria-valuenow` 0-100), `track-error`, `track-retry`, `track-retry-failed-again` (shown when a retry ends in `failed` again), `track-delete`, `track-delete-confirm`, `track-delete-cancel`, `track-bpm`, `track-duration`, `track-model`, `track-stem-<name>`, `open-mixer` |
| Track list (`/tracks`) | `track-row-<trackId>`, `track-row-status-<trackId>` |
| Mixer (`/tracks/{id}/mixer`) | `strip-<stem>`, `fader-<stem>`, `pan-<stem>`, `mute-<stem>`, `solo-<stem>`, `meter-<stem>` and `meter-master` (engine writes `data-peak-db`, `data-clip`), `master-fader`, `transport-play`, `transport-pause`, `transport-stop`, `transport-seek`, `transport-position`, `loop-start`, `loop-end`, `loop-toggle`, `loop-error`, `unsaved-indicator`, `metronome-toggle`, `metronome-volume`, `metronome-bpm`, `metronome-offset`, `metronome-beats-per-bar`, `metronome-reset`, `metronome-error`, `mix-save`, `mix-save-as`, `mix-name-input`, `mix-save-confirm`, `mix-error` |
| Mix library (`/mixes`) | `mix-row-<mixId>`, `mix-load-<mixId>`, `mix-duplicate-<mixId>`, `mix-rename-<mixId>`, `mix-delete-<mixId>`, `confirm-dialog-ok`, `confirm-dialog-cancel` |

Fader/pan/volume inputs expose their numeric value via `aria-valuenow` (fader 0-100, pan -100..100).

## Additions in 1.1.0 (feature 002)

| Screen | `data-testid` |
|---|---|
| Track detail (`/tracks/{id}`) | `detail-section`, `detail-status` (text = status: `none`, `queued`, `running`, `completed`, `failed`, `cancelled`), `detail-stage`, `detail-progress` (attribute `aria-valuenow` 0-100), `detail-long-notice` (shown while queued/running and for songs > 10 min), `detail-error`, `detail-retry` (failed, cancelled), `detail-split-further` (status `none`), `detail-cancel` (queued, running), `detail-outcome-<parent>` (attribute `data-outcome` = outcome kind; text = user text), `detail-stem-<parent>-<detail>`, `detail-limitation-help` (FR-017), `track-delete-blocked` (reason shown when delete is refused because the detail phase is active) |
| Mixer (`/tracks/{id}/mixer`) | `expand-<parent>` (button, `aria-expanded` true/false; only for parents with outcome `split`), `group-<parent>` (container of the detail strips), `strip-<parent>-<detail>`, `fader-<parent>-<detail>`, `pan-<parent>-<detail>`, `mute-<parent>-<detail>`, `solo-<parent>-<detail>`, `meter-<parent>-<detail>` (engine writes `data-peak-db`, `data-clip`), `strip-<stem>` gets `data-expanded="true"` while its group is expanded |

## Additions in 1.2.0 (feature 003)

| Screen | `data-testid` |
|---|---|
| Import (`/`) | `worker-device` (attribute `data-device` = `gpu`, `cpu` or `unknown`; text "GPU (<name>)", "CPU" or "Unknown"), `worker-device-reason` (exists only while the worker runs on the CPU although a GPU was requested; text = reason from `GET /api/device`) |
| Track detail (`/tracks/{id}`) | `track-device` (standard separation; exists once `track.device` is set; attribute `data-device` = `gpu`/`cpu`, `data-fallback` = `true`/`false`; text "GPU (<name>)" or "CPU"), `track-device-fallback` (exists only when `track.device.cpuFallback`; text = fallback reason), `detail-device` and `detail-device-fallback` (same rules for `track.detail.device`, inside `detail-section`) |

Device texts are exactly `GPU (<name>)` (name from the record) and `CPU`; tests compare
`data-device`, not the text, except for the fallback reason, which is shown verbatim.


## Additions in 1.3.0 (feature 004)

### Level visibility rules (binding)

- **R7**: at level **Advanced**, every id of 1.0.0-1.2.0 (and of the adopted list below) is rendered
  under exactly the same conditions as in 1.2.0. The existing end-to-end suite runs at Advanced
  (Playwright storage state with `level: "advanced"`, `help.welcomeDismissed: true`).
- **R8**: at level **Beginner** only these 1.2.0 ids are not rendered: `pan-<stem>`,
  `pan-<parent>-<detail>`, `loop-start`, `loop-end`, `metronome-bpm`, `metronome-offset`,
  `metronome-beats-per-bar`, `metronome-reset` (feature catalogue, design-spec.md 5). Every other
  id renders as at Advanced.
- **R9**: level **Expert** renders every Advanced id plus the Expert-only ids below.
- The root element `app-shell` carries `data-level` (`beginner`/`advanced`/`expert`),
  `data-theme` (`light`/`dark`, the resolved theme), `data-density` (`comfortable`/`compact`) and
  `data-motion` (`full`/`reduced`), so tests can assert the active state.
- `<topicId>` is a help topic id (kebab-case, [help-content.md](../../004-design-system-ux/contracts/help-content.md)); `<tourId>` a
  tour id; `<presetId>` one of `play-along-drums`, `play-along-bass`, `play-along-guitar`,
  `play-along-piano`, `karaoke`, `focus-vocals`, `reset`; `<key>` a channel key as in
  `strip-<key>` (`<stem>` or `<parent>-<detail>`); `<commandId>` a palette command id
  (`go-import`, `go-tracks`, `go-mixes`, `track-<trackId>`, `mix-<mixId>`, `help-<topicId>`, or an
  action id of the current screen such as `mixer-save`).

### Adopted ids (already rendered by 001-003 code, now binding, unchanged)

| Screen | `data-testid` |
|---|---|
| Shell | `nav-import`, `nav-tracks`, `nav-mixes` (rail links; under 768 px in the bottom bar) |
| Track list (`/tracks`) | `track-link` (title link per row), `track-mixer-link` (per completed row), `tracks-empty`, `tracks-error` |
| Track detail | `track-load-error`, `track-action-error`, `track-fallback-note` |
| Mixer | `mixer-load-error`, `mixer-engine-error`, `mixer-loading`, `mix-name`, `mix-save-cancel`, `loop-set-a`, `loop-set-b`, `metronome-hint`, `transport-state` |
| Mix library | `mixes-empty`, `mixes-error` |
| Dialogs | `confirm-dialog-message` |

### New ids

| Area | `data-testid` | Level | Notes |
|---|---|---|---|
| Shell | `app-shell` | all | root element, attributes above |
| Shell | `skip-link` | all | first focusable element, targets `main` |
| Shell | `nav-help` | all | rail item that opens the help panel |
| Shell | `level-switch` | all (>= 768 px in the top bar; below in `preferences-panel`) | radio group; children `level-beginner`, `level-advanced`, `level-expert` (`aria-checked`) |
| Shell | `help-toggle` | all | top-bar help button, `aria-expanded` |
| Shell | `command-palette-open` | all | top-bar button |
| Shell | `preferences-open`, `preferences-panel` | all | menu button and its panel |
| Preferences | `pref-theme-system`, `pref-theme-light`, `pref-theme-dark` | all | radio items |
| Preferences | `pref-density-auto`, `pref-density-comfortable`, `pref-density-compact` | all | radio items (`auto` = level default) |
| Preferences | `pref-motion-system`, `pref-motion-reduced` | all | radio items |
| Preferences | `pref-reset-help` | all | resets welcome, tours and hints |
| Preferences | `pref-storage-unavailable` | all | exists only when local storage cannot be used |
| Welcome | `welcome-panel` | all, until dismissed or a level is chosen | on Import |
| Welcome | `welcome-level-beginner`, `welcome-level-advanced`, `welcome-level-expert`, `welcome-tour-start`, `welcome-dismiss` | | buttons |
| Journey | `journey-rail` | Beginner | children `journey-step-import`, `journey-step-separate`, `journey-step-mix`, `journey-step-save` with `data-state` = `done`/`current`/`todo` and `aria-current="step"` on the current one |
| Design | `track-ribbon-<trackId>` (track list row), `track-ribbon` (track detail), `mix-ribbon-<mixId>` (mix library row) | all | `data-stems` = comma-separated stem names in display order; decorative, `aria-hidden` |
| Design | `split-progress` (standard), `detail-split-progress` (detail) | all | decorative visual next to `track-progress` / `detail-progress`; `data-state` = `idle`/`running`/`done` |
| Tracks | `tracks-search`, `tracks-sort` | Advanced, Expert | search input, sort select |
| Mixes | `mixes-search` | Advanced, Expert | |
| Track detail | `job-details` | Expert | disclosure button (`aria-expanded`); content `job-details-standard`, `job-details-detail` |
| Mixer | `mixer-presets` | all | Beginner: chip row container; Advanced/Expert: menu button whose items render when open |
| Mixer | `preset-<presetId>` | all | only presets whose stems exist (`reset` always) |
| Mixer | `pan-marker-<key>` | Beginner | read-only, exists only when the channel's pan is not 0; text e.g. "Panned L 30" |
| Mixer | `fader-value-<key>`, `pan-value-<key>` | Expert | number inputs mirroring fader (0-100) and pan (-100..100) |
| Mixer | `meter-db-<key>`, `meter-db-master` | Expert | text of the latest `data-peak-db` (one decimal, "-inf" for silence) |
| Mixer | (attribute) `data-selected="true"` on `strip-<key>` | all | channel selected with keys `1`..`9` |
| Help | `help-panel` | all | `aside`; attribute `data-topic-id` of the shown topic |
| Help | `help-close`, `help-back`, `help-search-input` | all | |
| Help | `help-result-<topicId>` | all | search result buttons |
| Help | `help-topic` | all | article; children `help-topic-title`, `help-topic-body` |
| Help | `help-related-<topicId>` | all | related-topic links |
| Help | `help-level-note`, `help-level-switch` | all | only when the topic's feature needs a higher level |
| Help | `help-index` | all | topic index (shown when no topic or an unknown topic is requested) |
| Help | `help-hint-<topicId>` | all | small help button next to a label; opens the panel at that topic |
| Help | `help-tooltip-<topicId>` | all | tooltip content (summary + `help-learn-more`) |
| Help | `hint-<topicId>` | Beginner | inline hint text under panels |
| Help | `help-tour-start-<tourId>` | all | tour list in the panel |
| Help | `help-shortcuts` | all | link to the shortcut overlay |
| Tours | `tour-offer-<tourId>`, `tour-offer-dismiss-<tourId>` | levels of the tour | offer chip and its dismiss button |
| Tours | `tour-popover` | while a tour runs | attributes `data-tour-id`, `data-step` (1-based), `data-steps`; children `tour-next`, `tour-prev`, `tour-close` |
| Palette | `command-palette`, `command-palette-input` | all | dialog and its input |
| Palette | `command-<commandId>` | all | result items (`aria-selected` on the active one) |
| Shortcuts | `shortcut-overlay`, `shortcut-overlay-close` | all (Ctrl+/ at Expert; `help-shortcuts` at every level) | |
| Notifications | `toast` | all | transient confirmation (e.g. "Saved", "Level set to Expert"), `role="status"` |

### Help markers (not test ids)

`data-help-id="<topicId>"` marks the element a help topic and a tour step refer to. It is set on
the same element as the control's `data-testid` where one exists (for example
`data-testid="pan-vocals" data-help-id="mixer-pan"`). Markers are validated by the help tests
([help-content.md](../../004-design-system-ux/contracts/help-content.md)), not used as Playwright selectors.


## Additions in 1.4.0 (feature 005)

`<artworkSource>` is `user`, `spotify`, `embedded`, `coverartarchive`, `youtube` or `none` (no
picture; the placeholder is shown). `<metadataStatus>` is a `TrackMetadata.status` value
(`none`, `pending`, `searching`, `found`, `notFound`, `failed`, `disabled`). Level visibility
follows R7-R9: every id below renders at Advanced and Expert; at Beginner the exceptions in the
last column apply.

| Screen | `data-testid` | Notes | Beginner |
|---|---|---|---|
| Track list (`/tracks`) | `track-thumb-<trackId>` | the `img` (160 px thumbnail, `loading="lazy"`, `decoding="async"`, width/height set, alt "Cover of <title>") or the placeholder element; attribute `data-artwork-source` = `<artworkSource>` | shown |
| Track detail (`/tracks/{id}`) | `track-cover` | the cover `img` or placeholder; `data-artwork-source` | shown |
| Track detail | `metadata-status` | attribute `data-status` = `<metadataStatus>`; text is the readable status | shown |
| Track detail, Mixer | `band-info-open` | button that opens the band info panel (keyboard accessible) | shown |
| Mixer (`/tracks/{id}/mixer`) | `mixer-cover` | cover in the mixer header; `data-artwork-source` | shown |
| Mixer | `mixer-album` | text "<album> (<year>)" (or "<album>" without a year); exists only when the status is `found` and an album is known | shown |
| Band info panel (both screens) | `band-info-panel` | the panel (`aside` or dialog) | shown |
| Band info panel | `band-info-status` | attribute `data-status` = `<metadataStatus>`; searching skeleton, not-found, failed and disabled texts | shown |
| Band info panel | `band-info-name`, `band-info-type`, `band-info-years`, `band-info-summary` | facts (exist only when known) | shown |
| Band info panel | `band-info-country`, `band-info-genres`, `band-info-members` (only for groups with members), `band-info-website`, `band-info-sources` | facts and the source list | not rendered |
| Band info panel | `band-info-attribution` | contains the three Wikipedia links (article title, "Wikipedia contributors" history link, "CC BY-SA 4.0" licence link) and the text "Excerpt, unmodified"; exists whenever `band-info-summary` exists | shown |
| Band info panel | `band-info-retry` | status `failed` or `notFound` | shown |
| Band info panel | `band-info-correct` | opens the correction dialog; statuses `found`, `notFound`, `failed` | shown |
| Correction dialog | `metadata-correct-title`, `metadata-correct-artist`, `metadata-correct-submit` | inputs (1..500 characters) and submit | shown |
| Band info panel | `band-info-disabled` | notice when online lookups are off (status `disabled`), with a link to help topic `online-lookups` | shown |
| Artwork actions (track detail) | `artwork-upload-input` | file input (accept JPEG, PNG, WebP; 10 MiB checked before upload) | not rendered |
| Artwork actions | `artwork-reset` | "Use automatic picture"; exists only while `data-artwork-source="user"` | not rendered |
| Artwork actions | `artwork-error` | readable upload error | not rendered |

Help topics (004 framework; topic ids are also the `data-help-id` values): `artwork` (on
`track-cover` and `mixer-cover`), `band-info` (on `band-info-panel`), `online-lookups` (on the
import page notice and `band-info-disabled`). DELTA.md wrote them as `help-artwork`,
`help-band-info`, `help-online-lookups`; the prefix was dropped in Phase 0 so they follow the
004 area naming (`mixer-pan`) and give `help-hint-artwork` rather than `help-hint-help-artwork`. The import page `legal-notice` text gains one sentence on
online lookups (pictures and band information are fetched from open data services; they are
shown in the app only and never exported).


## Additions in 1.5.0 (feature 007)

`<channelKey>` is a ChannelId with the dot written as a dash (`drums`, `drums-kick`), the same
form as `<key>` in `strip-<key>`. `<assetId>` is the item's GUID in lower-case `D` format.
Minimum levels: the format select is Beginner (FLAC preselected); bit depth, bit rate, range,
"selected channels" and the stem pack are Advanced (R7: rendered at Advanced and Expert, not at
Beginner).

| Screen | `data-testid` | Notes | Beginner |
|---|---|---|---|
| Shell | `nav-library` | rail link to `/library` (bottom bar under 768 px) | shown |
| Track detail (`/tracks/{id}`) | `export-stem-<stem>` | button per standard stem (raw export) | shown |
| Track detail | `export-detail-<parent>-<detail>` | button per kept detail stem of a split parent | shown |
| Track detail | `export-all-stems` | stem pack (opens the dialog in stem-pack mode with `export-include-detail`) | not rendered |
| Mixer | `mixer-export` | opens the export dialog | shown |
| Export dialog | `export-dialog` | the dialog; attribute `data-mode` = `stem`, `mix` or `stem-pack` | shown |
| Export dialog | `export-source-audible` | radio "What I hear" (mixer; preselected) | shown |
| Export dialog | `export-source-selected` | radio "Selected channels" (mixer only) | not rendered |
| Export dialog | `export-channel-<channelKey>` | checkbox per channel (mixer, "selected channels"; preselected = audible channels) | not rendered |
| Export dialog | `export-format` | select `flac` / `wav` / `mp3` | shown |
| Export dialog | `export-bit-depth` | select `16` / `24` (FLAC, WAV) | not rendered |
| Export dialog | `export-mp3-bitrate` | select `192` / `256` / `320` (MP3) | not rendered |
| Export dialog | `export-range-loop` | checkbox "Only the loop region"; exists only when the mixer has a loop region | not rendered |
| Export dialog | `export-range-start`, `export-range-end` | number inputs (seconds) | not rendered |
| Export dialog | `export-name` | name input (1..120; empty = default name) | shown |
| Export dialog | `export-include-detail` | checkbox (stem pack only) | not rendered |
| Export dialog | `export-submit`, `export-cancel` | buttons | shown |
| Export dialog | `export-error` | readable reason (validation, 409, 422, 503) | shown |
| Export dialog | `export-saved` | confirmation after submit; contains `export-saved-open` (link to the item on `/library`) | shown |
| Library (`/library`) | `library-total-size` | total size of all items (text, e.g. "312 MB") | shown |
| Library | `library-filter-track`, `library-filter-kind`, `library-search` | filters | not rendered |
| Library | `library-empty` | shown when there are no items (or none match) | shown |
| Library | `asset-row-<assetId>` | row per item; attribute `data-status` = `queued`/`rendering`/`ready`/`failed` | shown |
| Library | `asset-name-<assetId>`, `asset-contents-<assetId>` | name and contents text | shown |
| Library | `asset-status-<assetId>` | text = status; `aria-valuenow` = progress (0-100) while rendering | shown |
| Library | `asset-source-<assetId>` | track title (link while the track exists); attribute `data-track-available` = `true`/`false` | shown |
| Library | `asset-gain-note-<assetId>` | "Lowered by X dB to avoid clipping"; exists only when `appliedGainDb < 0` | shown |
| Library | `asset-play-<assetId>` | an `<audio>` element (`src` = file URL with `inline=true`); ready items only | shown |
| Library | `asset-download-<assetId>` | download link; ready items only | shown |
| Library | `asset-rename-<assetId>`, `asset-rename-input`, `asset-rename-confirm` | rename button, its input and confirm | shown |
| Library | `asset-delete-<assetId>` | opens the existing confirm dialog (`confirm-dialog-ok`, `confirm-dialog-cancel`) | shown |
| Library | `asset-retry-<assetId>` | failed items only | shown |
| Library | `asset-error-<assetId>` | readable reason of a failed item | shown |

Help topics (004 framework; topic ids are also the `data-help-id` values): `library` (on the
library page header: what the library is, later use, sizes, the personal-practice line),
`export-dialog` (on `export-dialog`: what I hear vs selected channels, raw vs mixed),
`export-formats` (on `export-format`; Advanced), `export-range` (on `export-range-start`;
Advanced), `export-clipping` (on `asset-gain-note-<assetId>`).


## Additions in 1.6.0 (feature 006)

`<lyricsStatus>` is a `Track.lyricsStatus` / `Lyrics.status` value (`none`, `searching`, `found`,
`instrumental`, `notFound`, `alignPending`, `aligning`, `failed`, `disabled`, `removed`). Level
visibility follows R7-R9: every id renders at Advanced and Expert; at Beginner the exceptions in
the last column apply. Lyrics text is copyrighted: Playwright specs use the fake stack's
self-written lyrics (`STEMSTUDIO_FAKE_LYRICS`) only.

| Screen | `data-testid` | Notes | Beginner |
|---|---|---|---|
| Mixer (`/tracks/{id}/mixer`) | `karaoke-banner` | the host element at the bottom of the mixer, rendered **empty** by Blazor (`@key`, no child content); the engine owns its children and writes `data-state` (`hidden`/`countdown`/`singing`/`ended`), `data-line-index`, `data-word-index`, `data-position-ms` (rounded); Blazor sets `data-sync` = `word`/`line`/`aligned` | shown |
| Mixer | `karaoke-credit` | credit text next to or below the host (`Lyrics.credit`) | shown |
| Mixer | `karaoke-toggle` | show/hide the banner (`aria-pressed`); remembered in `localStorage` | shown |
| Mixer | `karaoke-size` | select `s` / `m` / `l` | not rendered |
| Mixer | `karaoke-offset` | slider -5000..5000 ms, step 10 (`aria-valuenow` = ms; arrow keys +-50 ms) | not rendered |
| Mixer | `karaoke-offset-reset` | sets the offset to 0 | not rendered |
| Mixer | `karaoke-unavailable` | rendered instead of `karaoke-banner` when the status is not `found` (neither is rendered while the user has hidden the banner with `karaoke-toggle`): readable reason (searching, instrumental, not found, aligning with progress, failed, disabled, removed) and a link to the track page; attribute `data-status` = `<lyricsStatus>` | shown |
| Track detail (`/tracks/{id}`) | `lyrics-section` | the lyrics card | shown |
| Track detail | `lyrics-status` | attribute `data-status` = `<lyricsStatus>`; text is the readable status | shown |
| Track detail | `lyrics-credit` | credit text | shown |
| Track detail | `lyrics-search` | "Search again" (statuses `removed`, `notFound`, `failed`, `disabled`, `found`) | shown |
| Track detail | `lyrics-remove` | opens the existing confirm dialog (`confirm-dialog-ok`, `confirm-dialog-cancel`) | not rendered |
| Track detail | `lyrics-paste-open`, `lyrics-paste-input`, `lyrics-paste-submit` | enter lyrics yourself (textarea 1..20,000 characters, checked before submit) | shown |
| Track detail | `lyrics-align` | "Align to the vocals" (status `alignPending`, or `found` with line timing at Expert) | shown |
| Track detail | `lyrics-align-estimate` | text with the expected time, e.g. "about 4 minutes on the CPU" | shown |
| Track detail | `lyrics-align-progress` | progress (`aria-valuenow` 0-100) while `aligning` | shown |
| Track detail | `lyrics-align-cancel` | cancel a running alignment | shown |
| Track detail | `lyrics-align-device` | device badge of the alignment (the 003 device text rules) | not rendered |
| Track detail | `lyrics-error` | readable reason of a failed lookup or alignment | shown |
| Track detail | `lyrics-preview` | static, unsynced text (statuses `alignPending`, `failed` with text) | shown |
| Track detail | `lyrics-copyright-notice` | "Lyrics are shown for your personal practice only ..." | shown |

Engine-created elements inside `karaoke-banner` have **no** test ids: they are asserted through
the host attributes and the class selectors `.karaoke-line[data-slot=previous|current|next]`,
`.karaoke-word[data-state=past|current|future]`, `.karaoke-ball` and `.karaoke-countdown`
(documented exception to the "test ids only" rule, like the meters' `data-peak-db`; contract
in [audio-engine.md](../../006-synced-lyrics-karaoke/contracts/audio-engine.md), "Karaoke").

Help topics (004 framework; topic ids are also the `data-help-id` values; no `help-` prefix, as
in 005): `karaoke` (on `karaoke-banner`), `karaoke-offset` (on `karaoke-offset`; Advanced),
`lyrics-sources` (on `lyrics-section`: LRCLIB, what is sent, copyright), `lyrics-align` (on
`lyrics-align`: GPU automatic, CPU on click with the estimate, 1.6 GB model). DELTA.md wrote them
as `help-karaoke` ... `help-karaoke-offset`; the prefix was dropped in Phase 0. The import page
`legal-notice` gains one sentence on lyrics (fetched from LRCLIB, shown in the app only, never
exported).


## Additions in 1.7.0 (feature 008)

`<assetId>`, `<stem>`, `<parent>` and `<detail>` as above; `score-detail-<parent>-<detail>` exists
only for pitched detail stems (not `drums.*`). Minimum levels: profile and "simplified" rhythm are
Beginner; quality (piano), key override, manual tempo and range are Advanced (R7: rendered at
Advanced and Expert, not at Beginner), except `score-tempo-*` which are also rendered at Beginner
while `score-tempo-required` is shown (the score cannot be made without them).

| Screen | `data-testid` | Notes | Beginner |
|---|---|---|---|
| Track detail (`/tracks/{id}`) | `score-stem-<stem>` | "Sheet music" per standard stem (opens the dialog with `{trackId, channel: <stem>}`) | shown |
| Track detail | `score-detail-<parent>-<detail>` | "Sheet music" per kept pitched detail stem | not rendered |
| Library (`/library`) | `asset-score-<assetId>` | "Sheet music" row action on ready audio items | shown |
| Library | `asset-preview-<assetId>` | PDF viewer of a ready score item (`iframe`/`object`, `src` = the `pdf` file URL with `inline=true`, `title` set) | shown |
| Library | `asset-download-pdf-<assetId>`, `asset-download-musicxml-<assetId>`, `asset-download-midi-<assetId>` | download links (attachments) | shown |
| Library | `asset-score-key-<assetId>` | key text with confidence, e.g. "A-flat major (83 %)" (absent for drums) | shown |
| Library | `asset-score-tempo-<assetId>` | tempo and time signature, e.g. "118 BPM, 4/4" | shown |
| Library | `asset-score-warnings-<assetId>` | the warnings as a list (exists only with warnings) | shown |
| Library | `asset-score-limits-<assetId>` | the profile's quality note | shown |
| Library | `asset-attribution-<assetId>` | piano model credit (CC BY 4.0); piano items only | shown |
| Sheet music dialog | `score-dialog` | the dialog; attribute `data-source` = `channel` or `asset` | shown |
| Sheet music dialog | `score-profile` | select (allowed profiles; suggested preselected) | shown |
| Sheet music dialog | `score-quality` | select `accurate` / `fast` (piano only) with both estimates | not rendered |
| Sheet music dialog | `score-estimate` | expected time as text ("about 30 seconds", "about 8 minutes") | shown |
| Sheet music dialog | `score-limits` | the selected profile's quality note | shown |
| Sheet music dialog | `score-resolution` | checkbox "Simplified rhythm" (eighth notes) | shown |
| Sheet music dialog | `score-key-override` | select "Automatic" + the 30 keys | not rendered |
| Sheet music dialog | `score-tempo-bpm`, `score-tempo-beats-per-bar`, `score-tempo-first-downbeat` | manual tempo inputs (30-300, 1-12, seconds) | see above |
| Sheet music dialog | `score-tempo-required` | notice when `gridStatus` is `unreliable`/`none` | shown |
| Sheet music dialog | `score-range-start`, `score-range-end` | range inputs (seconds, >= 2 s) | not rendered |
| Sheet music dialog | `score-drums-split-required` | notice for drums before the split, containing `score-drums-split-start` (starts the detail phase) | shown |
| Sheet music dialog | `score-submit`, `score-cancel` | buttons | shown |
| Sheet music dialog | `score-error` | readable reason (validation, 409, 422, 503) | shown |
| Sheet music dialog | `score-saved` | confirmation after submit; contains `score-saved-open` (link to the item on `/library`) | shown |

Help topics (004 framework, ids = `data-help-id`): `sheet-music` (on `score-dialog`),
`sheet-music-profiles` (on `score-profile`), `sheet-music-key-tempo` (on `score-key-override`
and `score-tempo-required`; Advanced), `sheet-music-drums` (on `score-drums-split-required`),
`sheet-music-piano` (on `score-quality`: accurate vs fast, CPU time, CC BY 4.0 attribution).


## Additions in 1.8.0 (feature 009)

`<styleId>` and `<generationId>` are GUIDs in canonical lower-case `D` format; `<index>` is the
take number (1 or 2); `<stem>` as above. Visibility follows R7-R9: every id renders at Advanced
and Expert; at Beginner the ids marked "not rendered" are absent (the defaults apply: whole track
up to 8 minutes, all stems, the style's default strength, 1 take, mode `range`). Generative
controls are **disabled, not hidden**, when generation is not ready: the button keeps its id, has
the `disabled` attribute and `aria-describedby` pointing at `generation-status-reason`.

| Screen | `data-testid` | Notes | Beginner |
|---|---|---|---|
| Shell | `nav-generations`, `nav-styles` | rail links to `/generations` and `/styles` | shown |
| Any page with generative controls (track detail, `/generations`, `/styles`) | `generation-status` | banner or chip; attribute `data-state` = `notInstalled` / `unavailable` / `ready` (from `GET /api/generation/status`) | shown |
| Same | `generation-status-reason` | the readable reason (exists when `data-state` is not `ready`) | shown |
| Same | `generation-help-link` | opens help topic `generation-gpu-required` | shown |
| Same | `generation-download-notice` | "First use downloads about X GB" (exists when `downloadBytesNeeded` > 0 and ready) | shown |
| Track detail (`/tracks/{id}`) | `track-restyle` | button "Restyle"; opens the restyle dialog; disabled with the reason when not ready or the track is not completed | shown |
| Track detail | `track-origin-badge` | "AI-generated" text badge on generated tracks (not colour only) | shown |
| Track detail | `track-origin-source-link` | link to the source track of a restyled take (exists while the source track exists) | shown |
| Track list (`/tracks`) | `track-ai-badge-<trackId>` | "AI" text badge on generated tracks | shown |
| Restyle dialog | `restyle-dialog` | the dialog; attribute `data-track-id` | shown |
| Restyle dialog | `restyle-style-select` | select of all styles (presets first) | shown |
| Restyle dialog | `restyle-style-description` | description of the selected style | shown |
| Restyle dialog | `restyle-mode-range`, `restyle-mode-inplace` | radio buttons (`range` default; in-place with its explanation) | not rendered |
| Restyle dialog | `restyle-range-start`, `restyle-range-end` | number inputs in seconds (length 10-480 s) | not rendered |
| Restyle dialog | `restyle-stem-<stem>` | checkbox per stem of the track (vocals unchecked and disabled for an instrumental style) | not rendered |
| Restyle dialog | `restyle-strength` | 5-step slider, `aria-valuenow` 0-4 (subtle ... free), `aria-valuetext` the step name | not rendered |
| Restyle dialog | `restyle-takes` | number input or select 1-2 | not rendered |
| Restyle dialog | `restyle-personal-use-notice` | "For personal use; AI-generated; no artist imitation" (topic `generation-personal-use`) | shown |
| Restyle dialog | `restyle-start`, `restyle-cancel` | buttons | shown |
| Restyle dialog | `restyle-error` | readable reason (400 field messages, 409, 503) | shown |
| Style library (`/styles`) | `style-filter-genre`, `style-filter-mood`, `style-filter-instrument`, `style-filter-kind`, `style-search` | filters (kind: all / presets / mine) and search box | shown |
| Style library | `style-row-<styleId>` | one row per style; attribute `data-kind` = `preset` / `user` | shown |
| Style library | `style-duplicate-<styleId>` | "Duplicate" (presets and user styles) | shown |
| Style library | `style-edit-<styleId>`, `style-delete-<styleId>` | user styles only | shown |
| Style library | `style-delete-confirm` | confirm button of the delete dialog | shown |
| Style library | `style-new` | "New style" | shown |
| Style library | `style-empty` | empty state when filters match nothing | shown |
| Style editor (`/styles/new`, `/styles/{id}/edit`) | `style-name-input`, `style-genre-select`, `style-subgenre-input`, `style-tempo-select`, `style-instrument-tags`, `style-mood-tags`, `style-vocal-select`, `style-strength-select`, `style-description-input` | fields; tag inputs accept at most 8 tags | shown |
| Style editor | `style-save`, `style-cancel` | buttons | shown |
| Style editor | `style-error` | readable reason; attribute `data-field` names the field (`name`, `description`, ...), incl. the imitation guard text | shown |
| Generations (`/generations`) and the track page section `track-generations` | `generation-row-<generationId>` | attribute `data-status` (= `Generation.status`) and `data-kind` | shown |
| Same | `generation-progress-<generationId>` | progress bar, `aria-valuenow` 0-100 | shown |
| Same | `generation-stage-<generationId>` | stage text | shown |
| Same | `generation-cancel-<generationId>` | while active | shown |
| Same | `generation-retry-<generationId>` | when failed or cancelled | shown |
| Same | `generation-delete-<generationId>` | when terminal; opens a confirm dialog with `generation-delete-confirm` | shown |
| Same | `generation-error-<generationId>` | readable error (exists when failed) | shown |
| Same | `take-play-<generationId>-<index>` | `<audio controls>` with the take URL (plain element, not the mixer engine) | shown |
| Same | `take-import-<generationId>-<index>` | "Import as track"; after import a link to the track | shown |
| Same | `take-details-<generationId>-<index>` | disclosure with style, strength, seed, source and range, model and revision, device | shown |
| Same | `take-ai-badge-<generationId>-<index>` | "AI-generated" text badge | shown |

Help topics (004 framework; ids = `data-help-id`, no `help-` prefix): `generation-gpu-required`
(on `generation-status`: GPU-only, how to start the generator, sizes), `styles-library` (on the
style library), `styles-editor` (on the editor; the imitation rule), `restyle-options` (on
`restyle-dialog`: range, in-place, stems, takes), `restyle-strength` (on `restyle-strength`;
Advanced), `generation-ai-label` (on `take-ai-badge-*` and `track-origin-badge`),
`generation-personal-use` (on `restyle-personal-use-notice`), `generation-limits` (on the
generations page: quality varies, the artist deny-list is not complete). DELTA.md wrote them as
`help.generation.*`; Phase 0 uses the 005/008 topic-id convention.


## Additions in 1.9.0 (feature 010)

`<lyricsId>` and `<profileId>` are GUIDs in canonical lower-case `D` format; `<generationId>`
and `<index>` as in 1.8.0. Visibility follows R7-R9; at Beginner the ids marked "not rendered"
are absent (defaults: language from the lyrics sheet, 120 s, 1 take, BPM/key/time signature
automatic). Song and voice controls are disabled with the reason (not hidden) when the generator
or the singer is not ready; `generation-status` gains the attribute `data-voice-state`
(`notInstalled` / `unavailable` / `ready`, from `GenerationStatus.voice`). The recorder is a
JS-interop module of the Web project; its buttons are ordinary Blazor buttons with these ids.

| Screen | `data-testid` | Notes | Beginner |
|---|---|---|---|
| Shell | `nav-songs`, `nav-lyrics`, `nav-voices` | rail links to `/songs/new`, `/lyrics`, `/voices` | shown |
| Song page (`/songs/new`, `?lyricsId=` preselects) | `song-lyrics-select` | select of saved lyrics sheets ("New lyrics" first) | shown |
| Song page | `song-lyrics-text` | textarea; character counter `song-lyrics-count` (`n / 4096`) | shown |
| Song page | `song-lyrics-upload` | file input (`.txt`, `.lrc`, `.md`) | shown |
| Song page | `song-lyrics-save` | saves the text as a lyrics sheet | shown |
| Song page | `song-sections-propose` | "Add sections" (calls `POST /api/lyrics/sections`, replaces the text) | shown |
| Song page | `song-density-warning` | exists when the density is above 4 words per second | shown |
| Song page | `song-title-input`, `song-language-select` | title and language | shown |
| Song page | `song-style-select` | style from the 009 library ("Free description" option enables `song-free-caption`) | shown |
| Song page | `song-free-caption` | free text (guarded) | not rendered |
| Song page | `song-lead-voice` | select female / male / any | shown |
| Song page | `song-delivery-input` | e.g. "breathy, soft" (guarded) | not rendered |
| Song page | `song-instrumental` | checkbox "Instrumental (no vocals)" | shown |
| Song page | `song-bpm`, `song-key`, `song-time-signature` | optional inputs ("Automatic" default) | not rendered |
| Song page | `song-duration` | slider or input in seconds, `aria-valuenow` = seconds (10-480) | shown |
| Song page | `song-takes` | 1-2 | not rendered |
| Song page | `song-generate` | button; disabled with the reason when not ready | shown |
| Song page | `song-error` | readable reason; attribute `data-field` for field errors | shown |
| Song page | `song-personal-use-notice` | personal use, AI voice label, own lyrics only, no artist imitation | shown |
| Song page | `song-generations` | the song generations list (rows use the 1.8.0 `generation-row-*` / `take-*` ids) | shown |
| Lyrics list (`/lyrics`) | `lyrics-row-<lyricsId>` | one row per sheet | shown |
| Lyrics list | `lyrics-edit-<lyricsId>`, `lyrics-delete-<lyricsId>`, `lyrics-song-<lyricsId>` | edit, delete (confirm `lyrics-delete-confirm`), "New song from these lyrics" | shown |
| Lyrics list | `lyrics-new`, `lyrics-search`, `lyrics-empty` | new sheet, search, empty state | shown |
| Lyrics editor (dialog or `/lyrics/{id}`) | `lyrics-title-input`, `lyrics-language-select`, `lyrics-text-input`, `lyrics-save`, `lyrics-error` | fields and save | shown |
| Voice profiles (`/voices`) | `voice-new` | starts the "new voice profile" panel | shown |
| Voice profiles | `voice-record-start`, `voice-record-stop` | microphone recording (MediaRecorder); timer `voice-record-timer`; auto-stop at 60 s | shown |
| Voice profiles | `voice-record-unavailable` | notice when the microphone is unavailable or permission was denied (upload remains possible) | shown |
| Voice profiles | `voice-upload` | file input (WAV, FLAC, MP3, WebM; <= 20 MiB) | shown |
| Voice profiles | `voice-preview` | `<audio controls>` of the recording before saving | shown |
| Voice profiles | `voice-consent-text` | the statement from `GET /api/consent/voice` (English and Dutch), attribute `data-version` | shown |
| Voice profiles | `voice-consent-checkbox`, `voice-person-name`, `voice-display-name` | consent checkbox, typed person name (1-80), profile name (1-60) | shown |
| Voice profiles | `voice-save` | enabled only with a sample, the checkbox ticked and both names filled | shown |
| Voice profiles | `voice-error` | readable reason (`consent-required`, `too-short`, `unvoiced`, `library-audio`, 409, 503) | shown |
| Voice profiles | `voice-row-<profileId>` | one row per profile | shown |
| Voice profiles | `voice-play-<profileId>` | `<audio controls>` of the sample | shown |
| Voice profiles | `voice-consent-<profileId>` | consent details (statement version, person name, date) | shown |
| Voice profiles | `voice-delete-<profileId>` | opens the confirm dialog | shown |
| Voice profiles | `voice-delete-takes-<profileId>` | checkbox "Also delete takes converted to this voice" in the confirm dialog | shown |
| Voice profiles | `voice-delete-confirm` | confirm button | shown |
| Take actions (all take lists) | `take-use-my-voice-<generationId>-<index>` | "Use my voice" on song takes with `vocal: synthetic` only | shown |
| Use-my-voice dialog | `take-voice-profile-select`, `take-voice-convert-start`, `take-voice-convert-cancel` | profile select (link to `/voices` when there is none) and buttons | shown |
| Use-my-voice dialog | `take-voice-pitch-shift`, `take-voice-auto-shift` | -12..12 semitones, auto shift checkbox | not rendered |
| Take actions | `take-vocal-badge-<generationId>-<index>` | "AI voice" / "AI voice: <name>" text badge; attribute `data-vocal` = `none` / `synthetic` / `converted` | shown |
| Take actions | `take-vocal-play-<generationId>-<index>` | `<audio controls>` of the converted vocal (conversion takes) | shown |
| Track detail | `track-lyrics-link` | link to the lyrics sheet of a generated song track | shown |
| Track detail | `track-vocal-badge` | "AI voice" / "AI voice: <name>" on generated tracks with a vocal label | shown |

Help topics (ids = `data-help-id`): `lyrics-format` (markers, length, LRC timestamps dropped),
`lyrics-sections` (on `song-sections-propose`), `song-options` (on the song page), `song-language`
(on `song-language-select`), `voice-profiles` (on `/voices`: 20-30 s of dry singing, no music),
`voice-consent` (on `voice-consent-text`: only your own voice, what is stored, deletion),
`voice-conversion` (on the use-my-voice dialog: quality limits, languages, the exact-copy guard
is not a similarity check). DELTA.md wrote them as `help.lyrics.*`, `help.song.*`,
`help.voice.*`; Phase 0 uses the topic-id convention.

## Additions in 1.10.0 (feature 011)

Merged unchanged from `ui-test-ids.delta.md` (011 Phase 0 part 1) plus three ids fixed in Phase 0
part 2 (`agent-approval-effect`, `agent-approval-estimate`, `nav-settings`). 009 and 010 added no
settings page, so `/settings` is a new page; the agent section is its first section. The panel is
part of 004's layout (R1-R9 apply); `<conversationId>` is a GUID in canonical lower-case `D`
format, `<seq>` and `<index>` are integers from the run snapshot (`Step.seq`, `PlanItem.index`).
Approval cards of 009/010 tools (`restyle_track`, `generate_song`, `convert_take_voice`, ...) use
the same ids. A step denied by a guardrail (`decision: denied`, reasons `artist-imitation` or
`lyrics-not-from-user`) never shows a card: its `agent-step-<seq>` row has `data-status="failed"`
and `agent-step-error-<seq>` contains the refusal.

| Screen | `data-testid` | Element / attributes | Levels |
|---|---|---|---|
| Layout (every page) | `agent-toggle` | button that opens/closes the panel (`aria-expanded`) | all |
| Layout | `agent-panel` | the panel (complementary landmark, `aria-label="Composer agent"`) | all |
| Layout | `agent-backend-badge` | attributes `data-backend` = `ollama`/`claude`/`none`/`fake`, `data-model` (model id or empty), `data-fallback` = `true`/`false` | all |
| Layout | `agent-unavailable` | exists only while the backend is not ready; contains the reason and the fix text of `GET /api/agent/status` | all |
| Agent panel | `agent-input` | multi-line text input (max 4,000 characters); Enter sends, Shift+Enter new line | all |
| Agent panel | `agent-send` | send button; disabled while not ready, while a run is active or when the input is empty | all |
| Agent panel | `agent-cancel` | exists only while a run is active | all |
| Agent panel | `agent-undo` | enabled when something can be undone | all |
| Agent panel | `agent-new-conversation` | starts a new conversation | all |
| Agent panel | `agent-conversation-list` | list of conversations, newest first | all |
| Agent panel | `agent-conversation-<conversationId>` | one list entry (button); `aria-current="true"` on the open one | all |
| Agent panel | `agent-conversation-rename-<conversationId>`, `agent-conversation-delete-<conversationId>` | entry actions (delete disabled while its run is active) | Advanced, Expert |
| Agent panel | `agent-message-<seq>` | one message; attribute `data-role` = `user`/`assistant` | all |
| Agent panel | `agent-streaming` | exists only while answer text streams (live region `aria-live="polite"`) | all |
| Agent panel | `agent-plan` | plan checklist (exists only when the run has a plan) | all |
| Agent panel | `agent-plan-item-<index>` | attribute `data-status` = `pending`/`running`/`done`/`failed`/`skipped` | all |
| Agent panel | `agent-step-<seq>` | step row; attributes `data-tool`, `data-status` (AgentStepStatus wire value), `data-risk` | Advanced, Expert (Beginner sees the plan only, spec Clarifications) |
| Agent panel | `agent-step-progress-<seq>` | progress bar of a job step, `aria-valuenow` 0-100 | all |
| Agent panel | `agent-step-error-<seq>` | readable error of a failed step | all |
| Agent panel | `agent-approval-card` | confirmation card; attributes `data-tool`, `data-risk`, `data-reasons` (comma-separated reason codes, e.g. `risk-heavy,tainted`) | all |
| Agent panel | `agent-approval-arguments` | the exact arguments shown on the card | all |
| Agent panel | `agent-approve`, `agent-reject` | card buttons; Approve never receives focus automatically | all |
| Agent panel | `agent-approve-plan` | exists only when `allowPlanScope` is true | all |
| Agent panel | `agent-run-error` | readable failure with the fix; contains `agent-switch-to-local` when the failed backend is Claude | all |
| Agent panel | `agent-switch-to-local` | button inside `agent-run-error` (Claude failures only) | all |
| Agent panel | `agent-usage` | token usage of the run (Claude only) | Advanced, Expert |
| Agent panel | `agent-tool-log` | raw step log (tool, arguments, result summary) | Expert |
| Settings (`/settings`, new page: 004 has no settings route) | `settings-agent-backend` | radio group with `settings-agent-backend-ollama`, `settings-agent-backend-claude`, `settings-agent-backend-none` | all |
| Settings | `settings-agent-ollama-status` | Ollama reachability, version, models | all |
| Settings | `settings-agent-ollama-model`, `settings-agent-ollama-fallback-model` | model pickers (only tool-capable models) | Advanced, Expert |
| Settings | `settings-agent-claude-status` | attribute `data-key-configured` = `true`/`false`; never the key | all |
| Settings | `settings-agent-claude-model`, `settings-agent-claude-effort` | Claude pickers | Expert |
| Settings | `settings-agent-claude-notice`, `settings-agent-claude-notice-ack` | paid-service notice (what is sent, paid per use) and its acknowledge checkbox | all |
| Settings | `settings-agent-test`, `settings-agent-test-result` | connection test button and its result | all |
| Settings | `settings-agent-save`, `settings-agent-error` | save button and readable error (409/422 of the settings endpoint) | all |
| Agent panel | `agent-approval-effect` | the readable effect of the step (`approval.requested.effect`) | all |
| Agent panel | `agent-approval-estimate` | exists only for `heavy` steps; estimated minutes and device; attributes `data-minutes`, `data-device` = `gpu`/`cpu` | all |
| Shell | `nav-settings` | rail link to `/settings` | all |

Help topics (ids = `data-help-id`): `agent-panel` (on `agent-panel`: what the agent can do, levels),
`agent-approvals` (on `agent-approval-card`: why a step asks, tainted content, undo),
`agent-backends` (on `settings-agent-backend`: Ollama default, Claude opt-in and paid, none).

## Claude key in Settings (1.21.0, 011 follow-up claude-key)

The 011 ids `settings-agent-claude-status` (`data-key-configured`), `settings-agent-claude-notice*` and
`settings-agent-test*` keep their meaning; `data-key-configured` is now true for a usable key from
either source. Every rule above (R1-R9) applies.

All on the Settings page (`/settings`), inside the Claude part of the agent section, visible on
**all** levels (Beginner, Advanced, Expert).

| `data-testid` | Element / attributes | Present when |
|---|---|---|
| `settings-agent-claude-key` | the key group (`role="group"`, `aria-labelledby` its heading "Claude API key") | always |
| `settings-agent-claude-key-status` | status text; attributes `data-state` = `not-configured` / `environment` / `stored` / `stored-unreadable`, `data-source` = `environment` / `stored` / empty, `data-stored` = `absent` / `readable` / `unreadable`; live region `aria-live="polite"`; never the key (at most "ends with ...WXYZ") | always |
| `settings-agent-claude-key-env-override` | note "A saved key exists (ends with ...WXYZ, saved <date>) but is not used while ANTHROPIC_API_KEY is set" | `data-source="environment"` and `data-stored` != `absent` |
| `settings-agent-claude-key-unreadable` | explanation and fix of `stored-unreadable` ("Enter the key again") | `data-stored="unreadable"` |
| `settings-agent-claude-key-input` | `<input type="password" autocomplete="off" spellcheck="false">`, label "Claude API key", `maxlength="512"`; never pre-filled; emptied after every save attempt | no stored key, or after `settings-agent-claude-key-replace` was pressed |
| `settings-agent-claude-key-save` | button "Save key" (with a stored key: "Save new key"); disabled while the input is empty, while saving and when local-only applies | with `settings-agent-claude-key-input` |
| `settings-agent-claude-key-cancel` | button "Cancel"; hides the input again and empties it | after `settings-agent-claude-key-replace` |
| `settings-agent-claude-key-replace` | button "Replace key" | a stored key exists (`data-stored` != `absent`) and the input is hidden |
| `settings-agent-claude-key-delete` | button "Delete key"; opens the confirmation | a stored key exists |
| `settings-agent-claude-key-delete-confirm` | confirmation dialog (`role="alertdialog"`) with `settings-agent-claude-key-delete-confirm-yes` and `settings-agent-claude-key-delete-confirm-no` | after `settings-agent-claude-key-delete` |
| `settings-agent-claude-key-test` | button "Test key"; when both sources exist a choice `settings-agent-claude-key-test-source` (radio group: `-active`, `-stored`) | `data-state` is `environment` or `stored`, or `data-stored="readable"` |
| `settings-agent-claude-key-test-result` | result text; attribute `data-ok` = `true` / `false`, `data-key-source` = `environment` / `stored` | after a test |
| `settings-agent-claude-key-local-only` | notice "The Claude key can only be entered on the computer StemStudio runs on"; all key controls disabled | page opened under a non-loopback host name, or the API answered `403 local-only` |
| `settings-agent-claude-key-error` | readable error of the key endpoints (422 reason text, 503 key store, 413) - never the value | after a failed action |

### Agent panel

Agent panel (layout, all levels; added in 1.21.0 before its release, frontend us-p2 request). The
1.10.0 `agent-switch-to-local` stays the button inside `agent-run-error` only.

| Area | `data-testid` | Element / attributes | Present when |
|---|---|---|---|
| Agent panel | `agent-unavailable-switch-to-local` | button "Switch to local" inside `agent-unavailable`; selects the local backend (`PUT /api/agent/settings` `backend: ollama`) | the selected backend is Claude and it is not ready (no usable key, key deleted or `stored-unreadable`) |
| Agent panel | `agent-key-changed` | note (`role="status"`) "Key changed - applies to the next command. The active run keeps the key it started with; cancel it to use the new key now." (CK-FR-016) | a key was saved, replaced or deleted while a run is active; removed when that run ends |

### Help topic

`agent-claude-key` (004 help framework) on `settings-agent-claude-key`: the two sources and their
precedence, "stored encrypted on this computer", what this protects and what it does not, how to
replace a leaked key, and the recovery from `stored-unreadable`.

### Rules for qa

- Tests never type a literal key-shaped string: the sentinel is built at run time (repository
  secret scan, research R-07).
- After a save, assert that the DOM (`page.content()`) and the input value do not contain the
  sentinel.

## Additions in 1.11.0 (feature 012)

Practice tempo in the mixer and the export dialog note (design: `specs/012-practice-tempo/`).
Every rule above (R1-R9) applies.

### Level visibility (R7-R9 apply)

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

### Ids

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

### Help markers (not test ids)

`data-help-id` values set by 012 (validated by the 004 help tests): `practice-panel` (on
`practice-panel-toggle`, topic `practice-speed`; the practice steps of the `mixer-basics` and `mixer-advanced` tours
point here, so a closed panel does not skip them), `practice-speed` (on `practice-speed`), `practice-transpose` (on `practice-transpose`), `practice-count-in` (on
`countin-toggle`), `practice-speed-trainer` (on `trainer-panel`), `practice-sound-quality` (on
`practice-quality-hint` and `practice-stretch-status`).

### Shortcut commands (design-spec.md 6 addendum; mixer scope; web-interop 1.0.0 unchanged)

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
