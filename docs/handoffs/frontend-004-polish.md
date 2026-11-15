# Hand-off: frontend / 004 polish (T063, performance and robustness)

## What was done

- **Payload vs 003 (SC-003, budget 200 KB gzip)**: 003 shipped `material.css` + `app.css` = 93 635 B gz.
  004 ships Radzen `software.css` + own CSS = 108 512 B gz (own CSS 17 919 B), the Archivo variable font
  (latin subset, woff2) 92 912 B, JS 15 963 B gz (`web-interop.js` 54 kB raw / 15.5 kB gz incl. driver.js;
  `first-paint.js` 1.05 kB). Net added: **+123 752 B**, within budget.
- **Lighthouse** (desktop, local, Import and Tracks): Accessibility 100, Best practices 96 -> fixed the
  only finding (missing favicon, now `wwwroot/favicon.svg`), SEO 90 -> fixed (meta description added).
- **Performance trace (SC-011)**: Expert mixer, 18 strips (vocals, drums and guitar groups expanded), 10 s
  playback: **0 long tasks**; dB readouts update live through the web-interop MutationObserver.
- **Reflow 320 CSS px (WCAG 1.4.10)**: long file-hash titles widened Tracks and Track detail; fixed with
  `overflow-wrap: anywhere` on page/table/title text and full-width library tools under 768 px. All
  screens now 0 px horizontal overflow at 320 px. 200 % zoom checked on Import, Tracks, Track, Mixer.
- **Forced colours**: filled controls (checked segment, primary button, toast, master tab) lost their
  label; they now use `--ss-forced-accent: Highlight` / `--ss-forced-on-accent: HighlightText` (tokens.css)
  with `forced-color-adjust: none`; stem tabs and meters get a system border.
- **Density**: compact sets `--ss-density: 0.8`; every control height is `max(--ss-target-min, ...)`, so
  targets stay >= 24 px (WCAG 2.5.8). Checked with token arithmetic and the bUnit density tests; no
  separate screenshot.
- **Narrow strips**: long detail names (`electric-guitar`) end in an ellipsis; the full name stays the
  strip label.
- **Docs**: CS1591 is an error for production code (Directory.Build.props) and `jsdoc/require-jsdoc` is on
  for web-interop; both builds are clean, so XML docs and TSDoc are complete.
- **T038 owner change requests**: none were raised; the CTO pre-approved the screenshots.

Screenshots: `docs/design-review/004/` (now also `tour-step-dark`, `help-panel-dark`, `320-import`,
`320-track`, `320-mixes`, `forced-import`, `forced-track`, `mixer-expert-playing-dark`).

## Commit

`f151dc7` fix(frontend): 320 px reflow, forced-colours labels, favicon and meta description (T063).
This is a verification task, so there is no red test commit; it changes only CSS, markup in `App.razor`
and a static asset.

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
  Passed!  - Failed: 0, Passed: 794, Skipped: 0, Total: 794
  | StemStudio.Web | 99.27% line | 92.54% branch | 100% method |
src/web-interop: npx vitest run --coverage
  Tests 229 passed; Statements 100 % (253/253), Branches 99.37 % (160/161), Functions 100 % (97/97), Lines 100 % (243/243)
```

## Quality checks

`dotnet build` 0 warnings; `dotnet format --verify-no-changes` exit 0; web-interop ESLint and `tsc`
clean; `run_all.py`: 688 files, 5 329 units, 38 clones, **0 errors**, 181 warnings (none new).

## Dependencies

None added in this checkpoint. Feature total: Archivo font (SIL OFL 1.1, `fonts/archivo/OFL.txt`),
driver.js 1.10.2 (MIT), YamlDotNet (MIT), Markdig (BSD-2-Clause).

## Deviations (whole feature)

- Level switch stays in the context bar below 768 px instead of moving into the menu.
- `app-shell` also gets `data-theme`/`data-motion` mirrored by JS (theme.ts) for tests and styling hooks.
- Help links in topics are `/help/<id>`, intercepted with `NavigationLock` to open the panel.
- The preset picker component is `PresetPicker.razor` (static class `PracticePresets` kept the name).
- `expert-keyboard` tour runs on the mixer route.
- `meter-db-*` readouts come from a web-interop MutationObserver (`startMeterReadouts`, extra export),
  not from the engine.
- `TourService` has a fourth constructor parameter; an extra `help` topic exists; help search gives
  prefix matches half score.
- driver.js puts initial focus on the tour close button, not on the Next button.

## Open requests

- **devops**: Docker, CI and the non-Docker scripts must build `src/web-interop` (npm ci + build) before
  publishing the web app; otherwise `js/first-paint.js` and `js/web-interop.js` return 404.
- **architect**: the Vite module build is unminified (config is architect-owned); minifying would shrink
  the JS payload further (not measured).
- **qa**: Playwright notes from the US1-US5 hand-offs still apply; the 320 px and forced-colours checks
  above can be automated with `page.setViewportSize({ width: 320 })` and
  `page.emulateMedia({ forcedColors: 'active' })`.
