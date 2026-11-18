# Hand-off: frontend 014, HO:us2 (lane L5, round 014+015)

US2 "Hands-free control with keys and pedals". T066/T067 (command dispatcher, key map registration) were delivered at
HO:foundational (`frontend-014-foundational.md`).

## Tasks (red -> green)

| Task | Commit |
|---|---|
| T062 failing tests: live shortcut overview | 845d0ee0 |
| T063 overview from the live key map; help topic `shortcuts` reviewed "014" | 783248ad |
| T064 failing tests: shortcut settings, pedal setup, single-key switch | 45cf4608 |
| T065 `/settings/shortcuts`, pedal setup, Preferences switch | 24e6d711 |
| T069 help `shortcuts-custom`, `foot-pedals`, tour `hands-free` v1 | 9e2df62a |
| Interop tests for the key map overload, `updateShortcuts`, `captureChord`; text/edge tests | (this checkpoint's last commit) |

## Behaviour

- **Overlay** (`shortcut-overlay`): groups `shortcut-group-<group>` in catalogue order, rows `shortcut-row-<command>`
  (`.`/`:` -> `-`) with `data-keys` (`space|pagedown`, `mod+arrowright`, `g i` for sequences); visible keys include
  "(pedal)". Scopes per screen: global everywhere, + mixer and setlist on the mixer, + library on the library. Reserved
  rows hidden. A note says when single-key shortcuts are off. **Ctrl+/ now opens it at every level** (FR-024; 004 had it
  Expert-only); the Expert bar button stays Expert (`shell.shortcut-overlay-key`).
- **Settings** (`/settings/shortcuts`, FeatureCatalog `shortcuts.settings` Advanced+, screen topic `shortcuts-custom`):
  `shortcut-edit-<command>` (Learn, 10 s, `shortcut-capture` + `shortcut-capture-cancel`), refusals in
  `shortcut-conflict` ("L is already used by "Loop on/off". Change that one first." / "F5 cannot be used: Reloads the
  page."), `shortcut-clear-<command>`, `shortcut-reset-all`. Edit sets one keys chord (pedal chords of the command stay);
  a second keys chord is not editable on the page (defaults with two chords keep them until edited) - FR-019 "up to two"
  is met by the data model and validator; a UI for a second chord can follow if wanted.
- **Pedals** (`pedal-setup`): `pedal-preset` select, `pedal-mode` switch, `pedal-left-key` / `pedal-right-key`,
  `pedal-learn-left` / `-right` (validated like any chord), `pedal-test` shows "Right pedal pressed: Play / pause" via a
  page-local key map with only the two pedal keys (pedal mode on). Custom preset: no Learn buttons, note to assign per command.
- **Preferences panel**: `pref-single-key` (`role="switch"`, every level) and `pref-shortcuts-open` (link, closes the panel).
- Tour `hands-free` targets `section-loop-current` and `preferences` (data-model 6 lists `shortcut-overlay`,
  `pedal-setup`, `score-panel`, but the overlay button is Expert-only and the pedal setup is on another route; the tour
  test requires targets on the tour's route at its level). `score-panel` is added at US4.

## Quality

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Web (Release, coverage) | 3162+ passed | 99.42 % | 93.55 % |
| web-interop | 412 passed | 99.8 % | 98.29 % |

- Release build 0 warnings; format clean except L6's `RecordingApiClientTests.cs:103`; `run_all.py` PASS (0 errors, jscpd ran).
- a11y: switches are `role="switch"` with `aria-checked`, every button's name contains its visible text, conflicts are
  `role="alert"`, capture state `role="status"`, pedal test `aria-live="polite"`.

## For L3 (E2E)

- T065 is done: `practice-levels.spec.ts` may switch `r`/`c` to `0`/`Shift+K`. R and C now dispatch
  `mixer.record.toggle` / `mixer.compare.cycle` (handled by L6 once merged).
