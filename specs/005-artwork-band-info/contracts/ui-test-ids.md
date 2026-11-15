# Contract: UI test ids (v1.4.0)

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

