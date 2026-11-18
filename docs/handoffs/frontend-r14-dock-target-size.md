# Frontend hand-off: mixer dock target size (014 integration, open item 1)

## Cause

The karaoke/transport dock (`.ss-mixer-dock`) was `position: sticky`, so it is part of the page flow. The taller 014
mixer placed the Practice tempo heading at the dock's top edge on load, and axe `target-size` counted
`help-hint-practice-speed` as partly covered by the dock's controls (24 x 12.6 px). Any target that scrolls half under a
sticky dock fails the same way; axe treats only `position: fixed` bars as a separate layer.

## Fix (commit `18fc3d9c`)

- `css/components/karaoke.css`: the dock is a fixed bar (`left: calc(var(--ss-rail-width) + 1px)`, `right: 0`; `right:
  var(--ss-help-width)` when the help or agent panel is docked at >= 1280 px; `left: 1px; bottom: 3.5rem` above the
  bottom navigation bar under 768 px).
- `css/components/mixer.css`: `.ss-main:has(.ss-mixer-dock)` reserves `padding-bottom: calc(var(--ss-dock-inset, ...) +
  var(--ss-space-4))`, so the end of the console scrolls above the dock and the page end keeps its old geometry (the
  mixer screen baselines are unchanged). The old small-screen `padding-bottom: 3.5rem` is gone (the inset includes the
  offset). `scroll-padding-bottom` (WCAG 2.4.11) is unchanged.
- `js/dock-inset.js`: comments only.
- Test: `MixerDesignTests.MixerDock_IsAFixedBar_AndTheContentReservesItsInset` (red first); shared
  `RepoPaths.CssRule` reader.

## Verification

- Web bUnit: 3500 passed; coverage StemStudio.Web line 99.44 %, branch 93.29 %. Build 0 warnings;
  `dotnet format StemStudio.slnx --verify-no-changes` clean.
- Own stack `-p a11yfix` (ports 19180/19181, fake separator, own images), Playwright container (image = pin):
  before the fix 6 failed (the reported cases); after it `test:a11y` 201 passed, 22 skipped, 0 failed (includes
  `focus-not-obscured`).
- `test:visual` on a fresh stack: 4 `score-panel` element baselines changed (tall element capture now shows the dock
  over the panel bottom instead of the skip link); regenerated with `--update-snapshots -g "score-panel matches"`,
  commit `cd0760b5`, passing on rerun. The 6 `settings` failures are local only (the host's Ollama answers, so the
  masked Ollama status block is taller; CI has no Ollama). Tracks/library failures appear only on a reused stack
  (seeded data), not on a fresh one. Stack torn down with `down -v`.

## Dependencies

None added.
