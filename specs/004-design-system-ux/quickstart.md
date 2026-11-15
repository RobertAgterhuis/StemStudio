# Quickstart and validation guide: Design System and UX Foundation

Builds on the [001](../001-stemstudio-mvp/quickstart.md), [002](../002-detail-separation/quickstart.md)
and [003](../003-gpu-acceleration/quickstart.md) quickstarts. Design: [design-spec.md](design-spec.md).
Contracts: [contracts/](contracts/). Entities: [data-model.md](data-model.md).

## Running

Unchanged: `docker compose up --build` (CPU) or with the GPU override; the web image now also
builds `src/web-interop`. Developer loop for the browser glue:

```bash
cd src/web-interop
npm ci
npm run build        # -> src/StemStudio.Web/wwwroot/js/{web-interop.js,first-paint.js}
npm run test:coverage
npm run lint && npm run typecheck
```

Reset preferences in the browser: preferences menu -> "Reset help and tours", or delete the
local-storage key `stemstudio.preferences` in the developer tools.

## Automated suites

```bash
dotnet test                                   # incl. tests/StemStudio.Web.Tests/{Experience,Preferences,Help,Design,Shell}
cd e2e && npm test                            # 1.2.0 suite at level Advanced + new journey/help/tour/keyboard specs
cd e2e && npm run test:visual                 # visual regression, ONLY inside the Playwright container (CI job "visual")
cd e2e && npm run test:a11y                   # axe scans, every screen x level x theme
cd e2e && npm run test:lighthouse             # Lighthouse CI budgets (SC-003)
python tools/quality/run_all.py               # SIG + raw-colour guard + web-interop dependency rule
```

Update visual baselines only through the CI job's "update baselines" run (workflow dispatch);
the diff images are attached as artifacts and reviewed in the pull request.

## Validation scenarios

Use the fixture song (fake stack) unless stated. Record results in the qa hand-off.

### Design (US1)

| # | Scenario | Expected |
|---|---|---|
| V-D1 | Load `/` with theme System while the OS is dark, then light | first frame dark / light (no flash, 20 reloads each, SC-004); switching the OS setting updates the open page |
| V-D2 | Preferences -> Light, Dark, System | instant change, `app-shell[data-theme]` follows; reload keeps it |
| V-D3 | Import the fixture song and watch the track page | `split-progress` animates once while running, `track-progress` exposes `aria-valuenow`; with reduced motion the split is static |
| V-D4 | Compare stem colours on Tracks (ribbon), Track (tabs), Mixer (strip tabs, meters) | the same colour per stem in all three; names always visible |
| V-D5 | Visit every screen in both themes | matches the approved baselines (CI visual job green) |
| V-D6 | Resize to 360 px (Import, Tracks, Track, Mixes, Help) and 768 px (Mixer) | no horizontal page scroll; rail becomes a bottom bar under 768 px; mixer strips scroll inside their region |
| V-D7 | Windows high-contrast (forced colours) | all controls visible and operable |
| V-D8 | Network panel on a cold load | no request leaves the local stack (fonts, icons self-hosted) |

### Journey levels (US2)

| # | Scenario | Expected |
|---|---|---|
| V-J1 | Empty preferences, open `/` | Beginner; `welcome-panel` above the form; the import form works while it is shown |
| V-J2 | Choose "I am new to this", import, wait, open mixer, pick "Play along: drums", press Save, enter a name | journey rail moves Import -> Separate -> Mix -> Save; drums muted, click on, unsaved indicator until saved (SC-007) |
| V-J3 | At Beginner, check the mixer | no `pan-*`, `loop-start`, `loop-end`, `metronome-bpm`, `metronome-offset`, `metronome-beats-per-bar`, `metronome-reset`; everything else present |
| V-J4 | Set pan at Advanced, switch to Beginner | `pan-marker-<key>` shows "Panned ..."; audio unchanged; mix still dirty/clean as before |
| V-J5 | Play, change a fader, switch level twice | playback continues, position and unsaved changes kept, switch < 200 ms (SC-010) |
| V-J6 | Switch to Expert | compact density, `fader-value-*`, `pan-value-*`, `meter-db-*`, `job-details`, `kbd` hints |
| V-J7 | Two tabs, change level in one | the other tab follows without reload |
| V-J8 | Private window with storage blocked | app works at Beginner; `pref-storage-unavailable` shown in preferences |
| V-J9 | Run the full 1.2.0 E2E suite | 100 % green at Advanced (SC-006) |

### Help (US3)

| # | Scenario | Expected |
|---|---|---|
| V-H1 | On every screen: help button, `?`, F1 | panel opens with the screen topic; with a focused marked control, that control's topic; focus moves into the panel; Escape returns focus |
| V-H2 | Hover and keyboard-focus a marked control (e.g. a pan slider at Advanced) | tooltip with the summary and "Learn more" |
| V-H3 | Search the 20 reference queries below | expected topic first, results within 100 ms |
| V-H4 | At Beginner open the `mixer-pan` topic | level note "Available at Advanced level" with a switch button that works |
| V-H5 | Panel open at 1280 px in the mixer | transport and strips stay visible and usable |
| V-H6 | Unknown topic id requested (dev tools: `HelpContext.OpenAsync("nope")` via a test hook) | help index shown, no error |

Reference queries (query -> expected first topic): `pan` -> `mixer-pan`; `solo` -> `mixer-mute-solo`;
`mute` -> `mixer-mute-solo`; `loop` -> `mixer-loop`; `a-b` -> `mixer-loop`; `metronome` ->
`mixer-metronome`; `click` -> `mixer-metronome`; `bpm` -> `bpm`; `tempo` -> `bpm`; `downbeat` ->
`downbeat`; `detail stems` -> `detail-stems`; `kick` -> `detail-outcomes`; `youtube` -> `import-url`;
`spotify` -> `import-url`; `upload` -> `import-file`; `gpu` -> `processing-device`; `save` ->
`mixer-save`; `delete track` -> `track-delete`; `shortcuts` -> `shortcuts`; `copyright` ->
`legal-notice`.

### Tours (US4)

| # | Scenario | Expected |
|---|---|---|
| V-T1 | First visit at Beginner on `/` | `tour-offer-import-basics` shown; no tour starts by itself |
| V-T2 | Start and finish with Enter / arrow keys only | `tour-popover` steps 1..n, focus trapped; at the end the offer is gone (also after reload) |
| V-T3 | Start the mixer tour on a song without detail stems | steps with missing targets are skipped |
| V-T4 | Escape in the middle | tour closes, focus returns, status dismissed |

### Expert (US5)

| # | Scenario | Expected |
|---|---|---|
| V-E1 | Ctrl+K anywhere, type "mixes", Enter | Mix library opens |
| V-E2 | Keyboard-only: import URL, open track via palette, mixer, `[` `]` `L`, Ctrl+S, name, Enter, `G` `M`, load the mix | done in <= 25 key presses after typing the URL (SC-008) |
| V-E3 | Mixer shortcuts while typing in the mix name field | no shortcut fires |
| V-E4 | Ctrl+/ at Expert | `shortcut-overlay` lists the current screen's shortcuts |

### Accessibility and performance

| # | Scenario | Expected |
|---|---|---|
| V-A1 | axe scans (CI job "a11y") | 0 violations, every screen x level x theme (SC-001) |
| V-A2 | Keyboard-only main flow at each level; NVDA spot check on Import and Mixer | all reachable, focus always visible and not obscured, level switch announced |
| V-A3 | Token contrast test | all pairs >= thresholds (SC-002) |
| V-P1 | Lighthouse CI on Import, Tracks, Track, Mixes, Mixer (desktop, cold) | FCP <= 1.0 s, LCP <= 1.5 s, CLS <= 0.05 (SC-003) |
| V-P2 | Bytes added vs 003 (CSS, fonts, JS, compressed) | <= 200 KB |
| V-P3 | Performance trace: mixer, fully split song, all groups expanded, playing 30 s | no long task > 50 ms caused by style/layout (SC-011) |
