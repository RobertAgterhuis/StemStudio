# Contract delta: UI test ids 1.13.0 -> 1.14.0 (feature 015)

Additive. All 1.13.0 ids keep their name, element and meaning. The rules of `ui-test-ids.md`
apply (ids on the interactive element, `*-error` only while shown, `aria-valuenow` for values).
Merged into the full `ui-test-ids.md` 1.14.0 in Phase 0 (T002). New key conventions:
`<takeId>` and `<sessionId>` are GUIDs in lower-case `D` format; `<family>` is one of
`vocals`, `guitar`, `bass`, `piano`, `drums`, `other`, `none` (the `RecordingPart` values).
The take channel strip uses the key `take` in every existing strip pattern.

| Screen | `data-testid` | Notes | Min level |
|---|---|---|---|
| Mixer toolbar | `mixer-record-panel-toggle` | opens/closes the recording panel (`aria-expanded`) | Beginner |
| Recording panel | `rec-panel` | container; attribute `data-state` = engine `RecordingState` (`closed`, `open`, `countIn`, `recording`, `finishing`) and `data-permission` (`granted`, `prompt`, `denied`, `unknown`) | Beginner |
| | `rec-privacy-note` | "Recordings stay on this computer" | Beginner |
| | `rec-allow-mic` | button that triggers the permission prompt (only before permission) | Beginner |
| | `rec-unavailable` | reason why recording is not possible (insecure context, unsupported browser, permission denied, no device, busy); `data-reason` = `insecure`, `unsupported`, `denied`, `not-found`, `busy`, `lost` | Beginner |
| | `rec-retry-permission` | "Try again" after a denial or a lost device | Beginner |
| | `rec-device-select` | input device select (option values = device labels) | Beginner |
| | `rec-input-mode` | `mono-1`, `mono-2`, `stereo` select | Advanced |
| | `meter-input` | input level meter; engine writes `--meter-level`, `data-peak-db`, `data-clip`; `role="meter"` with `aria-valuenow` (dBFS, updated at most 2 Hz) | Beginner |
| | `meter-db-input` | text of the input peak | Expert |
| | `rec-part-select` | part select (`<family>` values) | Beginner |
| | `rec-mute-original` | checkbox "Mute the original part while recording" | Beginner |
| | `rec-count-in` | select 0, 1, 2 bars | Beginner |
| | `rec-monitoring` | switch (`aria-checked`) | Beginner |
| | `rec-monitoring-volume` | slider 0-100 (`aria-valuenow`) | Advanced |
| | `rec-monitoring-warning` | headphones warning (while monitoring is on) | Beginner |
| | `rec-latency-status` | text: calibrated / manual / estimated with value in ms; `data-method` = `loopback`, `manual`, `estimated` | Beginner |
| | `rec-latency-samples` | latency in samples (text) | Expert |
| | `rec-setup-changed` | "Your audio setup changed - calibrate again" (only when the output latency differs by > 5 ms) | Beginner |
| | `rec-calibrate` | opens the calibration dialog | Beginner |
| | `rec-record` | Record/Stop toggle button (`aria-pressed`; text "Record"/"Stop"); shortcut `R` | Beginner |
| | `rec-record-disabled-reason` | why Record is disabled (speed/transpose, speed trainer, take limit, input not open, track not ready); `data-reason` = `speed`, `trainer`, `limit`, `input`, `track` | Beginner |
| | `rec-set-normal-speed` | "Set to 100 %" (only with `data-reason="speed"`) | Beginner |
| | `rec-status` | live region (`aria-live="polite"`): "Count-in 3", "Recording", "Saving 2 takes", "Saved" | Beginner |
| | `rec-elapsed` | elapsed recording time `m:ss.s` | Beginner |
| | `rec-pass` | "Pass n" while loop-recording (text; attribute `data-pass`) | Advanced |
| | `rec-saving` | progress of uploads (`aria-valuenow` 0-100) | Beginner |
| | `rec-error` | readable error (upload refused, silent take, disk space) | Beginner |
| | `rec-save-again` | retries the upload of local captures that were refused or failed | Beginner |
| Calibration dialog | `cal-dialog`, `cal-instructions`, `cal-start`, `cal-cancel`, `cal-running` (`aria-busy`), `cal-result` (`data-ok` true/false), `cal-result-ms`, `cal-result-samples` (Expert), `cal-result-clicks`, `cal-result-spread`, `cal-reason` (`data-reason` = engine reason), `cal-try-again`, `cal-manual`, `cal-manual-ms` (number 0-1000, step 0.1), `cal-manual-slider`, `cal-save`, `cal-delete` | manual entry Advanced, samples Expert | Beginner |
| Take strip (mixer) | `strip-take`, `fader-take`, `pan-take`, `mute-take`, `solo-take`, `meter-take`, `fader-value-take`, `pan-value-take`, `meter-db-take`, `pan-marker-take` | same rules and levels as the stem patterns; exists only while a take is loaded; `strip-take` has `data-take-id` | as stems |
| | `take-compare` | radio group container | Beginner |
| | `take-compare-take`, `take-compare-original`, `take-compare-both` | radio buttons (`aria-checked`); shortcut `C` cycles; with part `none` only `take-compare-toggle` exists | Beginner |
| | `take-compare-toggle` | take on/off switch for part `none` | Beginner |
| | `take-adjust` | timing adjustment slider -200..200 ms (`aria-valuenow` ms) | Expert |
| | `take-adjust-ms` | number field (step 0.1) | Expert |
| | `take-adjust-apply`, `take-adjust-reset` | buttons | Expert |
| | `take-adjust-note` | "Timing adjusted by X ms" | Advanced |
| Takes panel (mixer) | `takes-panel`, `takes-empty`, `takes-total-size` | - | Beginner |
| | `takes-session-<sessionId>` | group of a loop session (`data-passes`) | Advanced |
| | `takes-keep-starred-<sessionId>` | "Delete the other passes" (only when a pass is starred) | Advanced |
| | `rec-take-row-<takeId>` | row; attributes `data-status` (`saving`, `ready`, `failed`, `local` before the upload finished), `data-selected` | Beginner |
| | `rec-take-select-<takeId>` | loads the take into the strip (radio) | Beginner |
| | `rec-take-name-<takeId>` | name text | Beginner |
| | `rec-take-part-<takeId>` | part badge (`data-part`) | Beginner |
| | `rec-take-range-<takeId>` | "1:00.0 - 1:08.0" | Beginner |
| | `rec-take-pass-<takeId>` | "Pass 2", "partial" | Advanced |
| | `rec-take-latency-<takeId>` | latency method badge (`data-method`) | Advanced |
| | `rec-take-warning-<takeId>` | clipped / very quiet / dropouts (`data-warning` = `clipped`, `quiet`, `dropouts`) | Beginner |
| | `rec-take-star-<takeId>` | star toggle (`aria-pressed`) | Beginner |
| | `rec-take-play-from-<takeId>` | moves the playhead to the take start | Beginner |
| | `rec-take-rename-<takeId>`, `rec-take-rename-input`, `rec-take-rename-confirm` | - | Beginner |
| | `rec-take-export-<takeId>` | opens the 007 export dialog preset for the take | Beginner |
| | `rec-take-delete-<takeId>` | delete (confirm with the existing `confirm-dialog-ok` / `confirm-dialog-cancel`) | Beginner |
| | `rec-take-retry-<takeId>` | only `failed` | Beginner |
| | `rec-take-error-<takeId>` | readable error | Beginner |
| | `rec-take-details-<takeId>` | device, input mode, latency samples, dropouts, clipped count | Expert |
| Export dialog (007) | `export-source-take-only`, `export-source-take-mix` | presets when opened from a take | Beginner |
| | `export-channel-take` | checkbox of the take in "Selected channels" | Advanced |
| | `export-take-saving` | "The take is still being saved" (export disabled) | Beginner |
| Track detail | `track-delete-takes-note` | "This also deletes N takes" in the delete confirmation (only when N > 0) | Beginner |
| Shortcut overview | `shortcut-record`, `shortcut-compare` | rows of the 004/014 shortcut overview | Expert |

Help ids (`data-help-id`) used by these elements: data-model.md 5.

Level rule (new; numbered after the 012-014 rules at the merge): at level Beginner the ids marked Advanced or Expert are not rendered; at
Advanced the Expert ids are not rendered (same mechanism as R7-R9).
