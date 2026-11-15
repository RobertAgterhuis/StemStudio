# Hand-off: qa / 004 integration (T067 E2E part, T068 baseline generation)

## What was built

Integration run of feature 004 on branch `004-design-system-ux` (from `d553bfd`) against a separate
compose project `stemstudio-e2e` (fresh volumes, `STEMSTUDIO_SEPARATOR=fake`,
`STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5`, CPU images). The owner's `stemstudio` project and its volumes
were not touched; `stemstudio-e2e` was torn down with `down -v` at the end. Every suite is green with
retries = 0. Failures were triaged as test faults (fixed in `e2e/`) or app faults (fixed test-first in
`src/StemStudio.Web` and `src/web-interop`).

- 60 visual baselines generated on a fresh stack inside `mcr.microsoft.com/playwright:v1.65.1-noble`
  and confirmed by a second run (`e2e/visual/__screenshots__/screens.spec.ts/*.png`), ready for the
  owner review (T068).
- New `e2e/a11y/robustness.a11y.spec.ts` + `e2e/support/robustness.ts` (frontend's suggestion): 320 x 720
  reflow (0 px horizontal document overflow) on Import, Tracks, Track, Track without detail, Mixes and
  Mixer, and `emulateMedia({ forcedColors: 'active' })` checks (checked level segment and primary Import
  button keep a label distinct from their fill; axe 0 violations on Import, Track and Mixer), both
  themes: 22 tests.

## Results (final run, final build, retries 0)

| Suite | Command | Result |
|---|---|---|
| Fixtures | `npm run fixtures` | 8 + 13 fixtures written |
| 001-003 at Advanced (SC-006) | `npm run test:legacy` | **66 / 66 passed** (Chromium 33, Firefox 33), 9.4 min |
| UX (`@ux`) | `npm run test:ux` | **74 / 74 passed** (Chromium 37, Firefox 37), 2.2 min |
| Accessibility | `npm run test:a11y` | **84 / 84 passed** (62 axe matrix + 22 robustness), run twice in a row, both green; **0 axe violations** |
| Lighthouse (SC-003) | `npm run test:lighthouse` | setup 1 passed; lhci 5 URLs x 3 runs, all assertions passed |
| Visual (SC-005) | container, `--update-snapshots` then compare | **60 baselines written, then 60 / 60 passed** |
| Everything else | `scripts/test-all.ps1 -ContinueOnError` | **all 34 steps passed**, exit 0 |
| Quality gate | `run_all.py --report docs/quality/SIG-REPORT.md` | 720 files, 5 665 units, 39 clones, **0 errors**, 188 warnings |

Lighthouse medians of the final run (desktop preset, `devtools` throttling, cold cache, 3 runs):

| URL | FCP ms (budget 1000) | LCP ms (budget 1500) | CLS (budget 0.05) |
|---|---|---|---|
| Import `/` | 232 | 356 | 0.000 |
| Tracks | 189 | 305 | 0.000 |
| Track | 211 | 412 | 0.000 |
| Mixes | 221 | 404 | 0.000 |
| Mixer | 192 | 459 | 0.025 |

## Fixes

### Test faults (e2e, qa-owned)

1. `pages/tour-popover.ts`: the offer chip `tour-offer-<id>` is a non-focusable wrapper; the keyboard
   start now focuses its start button (`offerStart`) and asserts focus before Enter.
2. `tests/tours.spec.ts`, `tests/help.spec.ts`, `tests/journey-levels.spec.ts`: one-shot reads of
   asynchronous state (stored tour record, focus inside the help panel, stored level before reload)
   replaced by `expect.poll`.
3. `tests/journey-levels.spec.ts`: wait for the stored level (`advanced`) before switching; the shell
   renders Beginner until the record is read, and a click in that window was overwritten.
4. `support/help.ts`: reference queries polled the presence of the expected result, which a stale list
   of the previous query could satisfy; now the first position is polled.
5. `support/expert-flow.ts`: the loop keys waited for nothing between presses; keys sent with no gap
   overtake each other on the circuit (seek read a stale position). Each key now waits for its effect
   (human pace); the key count is unchanged.
6. `tests/expert-keyboard.spec.ts`: the metronome starts on when the beat grid is reliable
   (`MixerState`), so toggles are compared with their state before typing, not with "off".
7. `support/screens.ts`: after expanding drums the pointer rested on the drums Mute/Solo and its help
   tooltip was captured in screenshots and scans; the pointer is moved away first.
8. `lighthouserc.json`: `throttlingMethod: "devtools"`. Lantern (`simulate`) cannot model Blazor
   Server's first render, which waits for the SignalR circuit (prerendering is off): it estimated FCP
   2.0-2.1 s and Mixer LCP 4.4-4.6 s, while the same page measured FCP 0.17-0.19 s / LCP 0.35-0.37 s
   with `devtools` or `provided` throttling. Documented in TEST-STRATEGY 10.4.
9. `support/preferences.ts`: the choice types were a 16-line clone of `src/web-interop/src/types.ts`
   (quality-gate duplication error); they are now derived from one `preferenceChoices` constant.

### App faults (minimal, test-first)

| # | Fault (found by) | Fix | Test first |
|---|---|---|---|
| A1 | Command palette lost typed characters (expert-keyboard, Firefox and Chromium): the server echoed `value="@Query"` into the input while the user kept typing | `CommandPalette.razor`: input no longer bound to a server value (the browser owns the text) | `CommandPaletteTests.Typing_DoesNotEchoTheQueryIntoTheInput` |
| A2 | Enter in the Save-mix name field did nothing (SC-008 flow "Mod+S, name, Enter") | `SaveMixDialog.razor`: Enter in the name field confirms unless busy | `MixesDesignTests.SaveMixDialog_EnterInTheNameField_Confirms_UnlessBusy` |
| A3 | `help-shortcuts` and `help-tour-start-*` only existed in the index, which a screen topic never shows (design-spec 4.7 puts them below every topic) | new `Help/HelpLinks.razor` (shortcut link + level tours), rendered below the topic and inside the index | `ShortcutOverlayTests.HelpShortcuts_UnderAShownTopic_OpensIt`, `TourOfferTests.HelpPanel_UnderAShownTopic_ListsTheToursOfTheLevel` |
| A4 | Focus fell to `body` after a tour started from an offer chip ended (the chip is removed once the tour is recorded; WCAG 2.4.3) | `web-interop/src/tour.ts`: focus returns to the page `h1` (tabindex -1) when the opener sat in an offer chip, else to the opener | 2 Vitest cases in `tour.test.ts` |
| A5 | axe `aria-allowed-attr`: driver.js puts `aria-haspopup/expanded/controls` on the highlighted element (invalid on the pan slider; wrong on `expand-*`) | `tour.ts`: `onHighlightStarted` snapshots and `onHighlighted` restores the target's own values | Vitest "keeps the ARIA semantics of the highlighted control"; `fake-driver.ts` now mimics driver.js |
| A6 | axe `label-content-name-mismatch` on `expand-*` (visible "Expand", name "Show detail stems of ...") | `ExpandToggle.razor`: name "Expand/Collapse detail stems of <parent>" | `DetailAccessibilityTests` expectation updated |
| A7 | axe `aria-progressbar-name` on `track-progress`, `detail-progress` (Radzen dropped the `aria-label` attribute) | `TrackProcessing.razor`, `DetailSection.razor`: Radzen `AriaLabel` parameter | `TrackDetailDesignTests` (2 assertions) |
| A8 | axe `color-contrast` in expanded/silent strips (whole strip at opacity 0.6/0.45 dimmed Collapse, dB text, numbers, tooltips) | `mixer.css`: only meter, fader and pan are dimmed | E2E `a11y` mixer-expanded (CSS) |
| A9 | Help tooltip of the first strip was clipped by the scrolling strip lane, "Learn more" unclickable under the rail | `help.css`: tooltip starts at the control's left edge instead of centred | E2E `help.spec` "Learn more" (CSS) |
| A10 | Mixer overflowed 655 px at 320 px (64-char hash title in the console grid; WCAG 1.4.10) | `mixer.css`: `overflow-wrap: anywhere` on the console title | E2E `robustness.a11y` 320 px (CSS) |

## Files changed

- e2e: `lighthouserc.json`, `pages/tour-popover.ts`, `support/{expert-flow,help,screens,preferences,robustness}.ts`,
  `tests/{expert-keyboard,help,journey-levels,tours}.spec.ts`, `a11y/robustness.a11y.spec.ts` (new),
  `visual/__screenshots__/screens.spec.ts/*.png` (60, new)
- web: `Components/Help/{HelpLinks (new),HelpIndex,HelpPanel}.razor`, `Components/Mixer/{ExpandToggle,SaveMixDialog}.razor`,
  `Components/Pages/{DetailSection,TrackProcessing}.razor`, `Components/Shell/CommandPalette.razor`,
  `wwwroot/css/components/{help,mixer}.css`
- web tests: `Help/TourOfferTests.cs`, `Mixer/DetailAccessibilityTests.cs`, `Pages/{MixesDesignTests,TrackDetailDesignTests}.cs`,
  `Shell/{CommandPaletteTests,ShortcutOverlayTests}.cs`
- web-interop: `src/tour.ts`, `tests/{tour.test,fake-driver}.ts`
- docs: `docs/testing/TEST-STRATEGY.md` (10.3 robustness spec, 10.4 throttling, 9 Windows container note),
  `docs/quality/SIG-REPORT.md`, this note

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests   Passed! Failed 0, Passed 798, Total 798
  StemStudio.Web 99.45 % line | 92.11 % branch | 100 % method   (gate 90 %)
src/web-interop: vitest 232 passed; Statements 99.64 % (277/278), Branches 97.7 % (170/174),
  Lines 99.62 % (266/267), Functions 100 %
scripts/test-all.ps1 -ContinueOnError: 34/34 steps passed (Api 414, Domain 261, Persistence 48,
  worker pytest 765 + 3 deselected, engine 160, tools/quality 253, fixtures 86, e2e list 297 tests)
```

## Quality checks

- `npm run lint` (e2e, `--max-warnings 0`) and `npm run typecheck`: 0 problems.
- web-interop ESLint + `tsc --noEmit`: clean; `dotnet build` warnings-as-errors clean; `dotnet format --verify-no-changes` exit 0.
- `sig_check.py ../../e2e`: 63 files, 580 units, 0 errors, 9 warnings (unit size 16-20, under the hard limit 30).
- `run_all.py`: 0 errors, 188 warnings; report committed in `docs/quality/SIG-REPORT.md`.

## Dependencies added

None.

## Open issues / requests

- **frontend / devops**: `_content/Radzen.Blazor/css/software.<fingerprint>.css` is served uncompressed
  (812 450 B; the endpoint manifest has no br/gz selectors for the fingerprinted route, while
  `software.css` without fingerprint has br 64 407 B). Same for `Radzen.Blazor.<fp>.js`. Not a budget
  failure on the local stack, but the SC-003 "+200 KB compressed" figure assumes compression.
  `MaterialSymbolsOutlined.woff2` (1 MB) also loads on every page.
- **frontend**: a level click within the first interop round trip after load is overwritten by the
  stored level (test now waits; the app could ignore the late read once the user has chosen).
- **frontend**: rapid mixer keys (no gap) overtake each other (`mixer.seek` then `mixer.loop.a` read a
  stale position); serialising `OnCommand` would make the shortcuts robust to key repeat/macros.
- **frontend (design)**: silent and expanded strips now dim only meter, fader and pan (A8); visible in
  the new baselines, for the T068 owner review.
- One a11y run showed a single blank Mixes page (app-shell absent for 10 s, no server error); it did
  not reproduce in 40 repeats and 2 further full runs.
- T067/T068 checkboxes in `specs/004-design-system-ux/tasks.md` are left to the CTO.

## Next owner

CTO: review of the 60 baselines with the owner (T068), merge, T069/T070.
