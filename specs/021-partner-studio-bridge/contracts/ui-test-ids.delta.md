# Contract delta: UI test ids 1.20.0 (feature 021, additive)

Status: **binding names** (021 design, 2026-11-16). Base: `ui-test-ids.md` **1.19.0** (020). Merged
unchanged into the full document as **1.20.0** in Phase 0 (T003). Nothing below renames, removes or
changes an existing id; every existing rule applies (`getByTestId` only, level visibility,
placement inside 004's layout, `data-help-id` markers for help anchors).

## Changelog line

> 1.20.0 (additive, feature 021) - practice partner dialog, takes with fit check, part strips; amp
> panel on the 015 input, latency and load, rig presets, amp profile library (catalogue, uploads,
> cabinets), take wet/dry and re-amp; DAW export dialog and report. All 1.19.0 ids are unchanged.

## Ids

| Screen | `data-testid` | Element / attributes | Levels |
|---|---|---|---|
| Mixer toolbar | `partner-open` | button; disabled with `title` = reason while `available` is false; `data-help-id="partner-open"` | all |
| Mixer | `partner-unavailable` | reason text + help link (exists only while unavailable) | all |
| Partner dialog | `partner-dialog` | modal dialog (`aria-labelledby`), focus trapped | all |
| Partner dialog | `partner-family` | radio group; options `partner-family-<family>` (`drums`, `percussion`, `bass`, `guitar`, `keys`, `synth`, `strings`, `brass`, `woodwinds`, `backingVocals`) | all |
| Partner dialog | `partner-mode-add`, `partner-mode-alternative` | radios | all |
| Partner dialog | `partner-replaces` | channel select (exists only in mode alternative) | all |
| Partner dialog | `partner-range-whole`, `partner-range-sections` | radios; `partner-section-<sectionId>` checkboxes (consecutive only) | Advanced, Expert |
| Partner dialog | `partner-bpm`, `partner-time-signature`, `partner-key` | prefilled inputs | Advanced, Expert |
| Partner dialog | `partner-style-hint` | text input (200 chars), `partner-style-error` for the imitation reason | Advanced, Expert |
| Partner dialog | `partner-takes` | 1/2 selector | Advanced, Expert |
| Partner dialog | `partner-steps`, `partner-guidance`, `partner-seed` | Expert settings | Expert |
| Partner dialog | `partner-download-note` | first-use download size (exists only when `baseModelPresent` is false) | all |
| Partner dialog | `partner-limits` | the 8-minute / quality note with help link | all |
| Partner dialog | `partner-generate`, `partner-cancel-dialog` | buttons | all |
| Partner dialog | `partner-error` | readable 4xx/409 problem | all |
| Partner job | `partner-job` | job card; attributes `data-status` (009 GenerationState), `data-generation-id` | all |
| Partner job | `partner-job-progress` | progress bar `aria-valuenow` 0-100 + stage text | all |
| Partner job | `partner-job-cancel` | exists while queued/waiting/running | all |
| Partner takes | `partner-take-<index>` | take row; attributes `data-verdict` `good`/`fair`/`poor`/`pending`, `data-recommended` `true`/`false` | all |
| Partner takes | `partner-take-play-<index>` | play with the song (toggle, `aria-pressed`) | all |
| Partner takes | `partner-take-fit-<index>` | fit summary (offset, key, chords, loudness); details expandable | all (details Advanced+) |
| Partner takes | `partner-take-add-<index>` | "Add to mixer"; disabled after the take was added | all |
| Partner takes | `partner-take-ai-badge-<index>` | AI-generated badge | all |
| Mixer strip | `part-strip-<channelId>` | strip of a part (`channelId` = `part-xxxxxxxx`); attributes `data-family`, `data-status` | all |
| Mixer strip | `part-ai-badge-<channelId>` | AI-generated badge on the strip | all |
| Mixer strip | `part-rename-<channelId>`, `part-delete-<channelId>` | strip menu actions | rename Advanced+, delete all |
| Mixer strip | `part-conforming-<channelId>` | busy indicator while `conforming` | all |
| Input panel (015) | `amp-toggle` | switch (`role="switch"`, `aria-checked`); disabled with reason when no input | all |
| Input panel | `amp-panel` | the chain panel (region, `aria-label="Amp"`) | all |
| Input panel | `amp-rig-select` | rig preset picker | all |
| Input panel | `amp-rig-save`, `amp-rig-delete` | preset actions | Advanced, Expert |
| Input panel | `amp-bypass` | toggle (`aria-pressed`) | all |
| Input panel | `amp-input-gain`, `amp-output-level` | sliders (dB, `aria-valuetext` "-6 dB") | all |
| Input panel | `amp-gate`, `amp-gate-threshold` | switch + slider | Advanced, Expert |
| Input panel | `amp-pedal-select`, `amp-amp-select`, `amp-cabinet-select` | pickers (built-ins, installed captures, IRs, "none") | Advanced, Expert (Beginner sees the rig picker only) |
| Input panel | `amp-tone-bass`, `amp-tone-mid`, `amp-tone-treble`, `amp-drive` | built-in amp controls (exist only for built-in amps) | Advanced, Expert |
| Input panel | `amp-latency` | badge; attributes `data-latency-ms`, `data-level` `ok`/`usable`/`high` | all |
| Input panel | `amp-latency-warning` | warning with tips (exists only when > 30 ms) | all |
| Input panel | `amp-latency-details` | breakdown (calibration, chain, resampler) | Expert |
| Input panel | `amp-load` | CPU load badge; attribute `data-state` (RigState) | all |
| Input panel | `amp-overload-warning` | exists only while overloaded | all |
| Input panel | `amp-missing-warning` | fallbacks applied (deleted capture/IR) | all |
| Input panel | `amp-record-dry-only` | "Record dry, hear amp on playback" switch | Advanced, Expert |
| Take row (015) | `take-wet-badge-<takeId>` | exists when the take has a wet file | all |
| Take row | `take-play-wet-<takeId>` | wet/dry toggle (`aria-pressed` = wet) | Advanced, Expert |
| Take row | `take-reamp-<takeId>` | re-amp toggle + `take-reamp-rig-<takeId>` picker | Advanced, Expert |
| Take row | `take-print-<takeId>` | print re-amped sound to a new wet file | Advanced, Expert |
| Amp profiles page (`/amp`) | `amp-profiles-page` | page root; screen topic `amp-profiles` | Advanced, Expert |
| Amp profiles page | `amp-catalogue-list` | catalogue list; entries `amp-catalogue-<entryId>` with attributes `data-licence`, `data-installed` | Advanced, Expert |
| Amp profiles page | `amp-catalogue-install-<entryId>`, `amp-catalogue-licence-<entryId>` | install button, licence + attribution text/link | Advanced, Expert |
| Amp profiles page | `amp-upload`, `amp-upload-input`, `amp-upload-error` | `.nam` upload | Advanced, Expert |
| Amp profiles page | `amp-profile-<profileId>` | installed/uploaded profile row; attributes `data-source`, `data-architecture`, `data-size-class` | Advanced, Expert |
| Amp profiles page | `amp-profile-rename-<profileId>`, `amp-profile-delete-<profileId>` | actions | Advanced, Expert |
| Amp profiles page | `amp-ir-upload`, `amp-ir-upload-input`, `amp-ir-<irId>`, `amp-ir-delete-<irId>` | cabinet IRs | Advanced, Expert |
| Track / mixer menu | `daw-export-open` | "Export to DAW" | all |
| DAW export dialog | `daw-export-dialog` | modal dialog | all |
| DAW export dialog | `daw-export-channels` | channel checklist; entries `daw-export-channel-<channelId>` | Advanced, Expert |
| DAW export dialog | `daw-export-mix` | mix select (unity by default) | Advanced, Expert |
| DAW export dialog | `daw-export-arrangement` | arrangement select (exists when the track has arrangements) | Advanced, Expert |
| DAW export dialog | `daw-export-format`, `daw-export-bit-depth`, `daw-export-tempo-mode`, `daw-export-effects` | options; `daw-export-flac-warning` when FLAC is chosen | Advanced, Expert |
| DAW export dialog | `daw-export-size` | estimated size text | all |
| DAW export dialog | `daw-compat` | compatibility note (supported DAWs, Ableton not supported) with help link | all |
| DAW export dialog | `daw-export-start`, `daw-export-error` | start button, readable problem | all |
| Library (007) | `library-kind-dawProject` | kind filter option (existing filter, new value) | all |
| Library item | `daw-report` | export report (mapped / not mapped / skipped), in the item details | Advanced, Expert (summary line for all) |
| Library item | `daw-report-not-mapped-<n>` | one row per not-mapped setting | Advanced, Expert |

## Feature catalogue ids (004 design-spec 5, additive)

`partner.generate`, `partner.parts`, `amp.chain`, `amp.profiles`, `amp.takes`, `export.dawproject`
with the levels of data-model.md section 9.
