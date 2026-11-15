# Hand-off: frontend / 004 US4 (T053-T056, guided tours)

## What was built

- **`src/web-interop/src/tour.ts`** (driver.js 1.10.2): resolves steps against `[data-help-id]` and skips
  missing or hidden targets (`hidden` ancestor, `display:none`, `visibility:hidden`); no visible step ->
  `OnTourEnded(id, "dismissed", 0)` at once. Popover gets `data-testid="tour-popover"`, `data-tour-id`,
  `data-step` (1-based), `data-steps`, `role="dialog"`, `aria-modal`, `aria-labelledby`; buttons
  `tour-next`, `tour-prev`, `tour-close`; focus goes to Next and is trapped (Tab / Shift+Tab wrap).
  Keys (own capture listener, driver.js keyboard off): ArrowRight and Enter = next (Enter on the last
  step completes), ArrowLeft = back, Escape = dismiss. Reduced motion -> `animate: false`. Exactly one
  `OnTourEnded` with the last shown step; focus restored; `dispose()` destroys without a callback.
  The factory is injectable for tests (`useDriverFactory`).
- **`focus.ts`**: `focusedHelpId()` (closest `data-help-id`), `captureFocus()` (restores if still in the
  page). `index.ts` now only re-exports; no stub remains.
- **`css/components/tour.css`**: the popover and overlay themed with tokens (driver.js's own CSS is not
  loaded; its structural rules are re-expressed, overlay fill `--ss-color-scrim`).
- **TourService** (complete): `CurrentOffers()` / `OffersFor(...)` (route template or `*`, targeted level,
  no record or an older version), `StartAsync`, `DismissOfferAsync` (records the version as dismissed),
  `CloseAsync`; navigating away from a running tour closes it and records it dismissed
  (data-model.md 1.3). `Help/AppRoutes` holds the route-template matching (shared with HelpContext).
- **TourOffer** (`Components/Help/TourOffer.razor`, above the page content in the shell):
  `tour-offer-<id>` with "Take the tour: <title>" and `tour-offer-dismiss-<id>`; follows navigation and
  preference changes; never starts a tour by itself.
- **Tours** (`Help/tours/*.json`): `import-basics`, `track-basics`, `mixer-basics` (Beginner),
  `mixer-advanced` (Advanced), all version 1. The coverage test renders each tour's route at each of its
  levels and finds every target.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T053 -> T054 | `596bed7` (2 Vitest files fail to import) | `e28f372` |
| T055 -> T056 | `fd031f4` (9 failing) | `666807f` |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
  Passed!  - Failed: 0, Passed: 747, Skipped: 0, Total: 747
  | StemStudio.Web | 99.24% | 92.34% | 99.83% |
src/web-interop: npx vitest run --coverage
  Tests 185 passed; Statements 100 % (208/208), Branches 99.17 % (120/121), Functions 100 % (77/77), Lines 100 % (200/200)
```

## Quality checks

`dotnet build` 0 warnings; `dotnet format --verify-no-changes` exit 0; web-interop lint/typecheck clean;
`run_all.py`: 674 files, 5 192 units, 31 clones, **0 errors**, 171 warnings.

## Dependencies added

None (driver.js 1.10.2 pinned in Phase 0, now used; its CSS is not shipped).

## Deviations

- `expert-keyboard` is written in US5 together with its targets (`shortcut-overlay`, `mixer-numeric-entry`).
- `TourService` takes a `NavigationManager` (fourth constructor parameter) to close tours on navigation.

## Open issues / requests

- **qa**: tour versions are all 1 (no bump to report). The offer chip is rendered inside `main`, above the
  page content. Tour keys: ArrowRight / Enter / ArrowLeft / Escape; Tab is trapped in the popover.

## Next owner

frontend (US5, T058 onwards).
