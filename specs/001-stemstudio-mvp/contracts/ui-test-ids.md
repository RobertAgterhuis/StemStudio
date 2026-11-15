# Contract: UI test ids (v1.0.0)

Binding for the **frontend** agent (renders the attributes) and the **qa** agent (Playwright
selectors use `page.getByTestId(...)` only). Changing, renaming or removing an id is a breaking
change: bump the version here and record it in the owner's hand-off.

## Rules

- Ids are static strings, kebab-case. `<stem>` is a stem name (`vocals`, `drums`, `bass`,
  `guitar`, `piano`, `other`); `<name>` in `track-stem-<name>` likewise; `<trackId>` and `<mixId>`
  are the GUIDs in their canonical lower-case `D` format.
- Put the id on the interactive element itself (the `input`, `button`), not on a wrapper, so
  Playwright can `fill`, `click` and read `aria-valuenow` directly.
- Meter elements are rendered by Blazor; the audio engine writes `--meter-level`,
  `data-peak-db` and `data-clip` into them (see [audio-engine.md](audio-engine.md), "Meters").
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
