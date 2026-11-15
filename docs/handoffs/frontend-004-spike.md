# Hand-off: frontend / 004 spike (T012, theme go/no-go, ADR-013 D1)

## What was built

A temporary Development-only page `/dev/theme` (`Components/Pages/Dev/ThemeSpike.razor`) that renders
every Radzen component StemStudio uses: `RadzenButton` (filled primary, light, danger, success,
warning, outlined danger, small, icon-only, busy, disabled), `RadzenBadge` (success, danger, info,
light), `RadzenCard`, `RadzenTextBox`, `RadzenProgressBar` (determinate with value), `RadzenDialog`
(via `DialogService`, title bar, close button, body) and the page body. The page is removed again in
T020 (a dev route would break the one-screen-topic-per-`@page` help check, help-content.md 4.3).

Method: the app ran locally (`dotnet run`, Development, port 5180) with the `software` base theme,
the draft `css/tokens.css` and `css/radzen-map.css` (committed with T020). A Playwright script
switched `html[data-theme]` between `dark` and `light`, collected the computed `color`,
`background-color`, `border-top-color` (when the border is visible) and `outline-color` of all 53
elements under the spike root and the open dialog, and compared each value with the resolved values
of every `--ss-*` token (plus the `color-mix` shades of the map and `transparent`).

## Findings

1. **Radzen 13.1.0 `software.css`**: 1 805 custom properties on `:root`; 131 distinct literal colour
   declarations outside variables. Nearly all of them are chart palettes (`.rz-scheme-*`,
   `.rz-series-*`), scrollbar and frozen-grid shadows, `rgba(0,0,0,0)` (transparent) and the hover
   overlays of outlined light/dark buttons (`rgba(255,255,255,.12)` / `rgba(0,0,0,.12)`). None of the
   charts, grids or scrollbars are used by StemStudio.
2. **First run of the probe** (map without the last three overrides): 3 off-token values -
   `.rz-progressbar-value` background (the theme's own progress colour), and the dialog close button
   colour (a `<button>` that inherits the UA `ButtonText`, white in dark, black in light).
3. **After the targeted overrides** (`--rz-progressbar-*`, `--rz-dialog-close-color`, one rule for
   `.rz-dialog-titlebar-close`, one for the outlined light/dark hover): **light 0, dark 1** off-token
   value - the inherited `color` of the empty `.rz-progressbar-value` bar (no text is rendered in it,
   so it is never visible). Accepted.
4. Software theme specifics neutralised by the map: button hover/focus/active gradients and inset
   shadows (`--rz-button-*-gradient/shadow: none`, no gradients per design-spec.md 2.4), input inset
   shadows, card/grid shadows (`--rz-shadow-1/2` -> none, 3..10 -> `--ss-elev-float`), upper-case badge
   text (`--rz-badge-text-transform: none`), the Source Sans Pro font (replaced by `--ss-font`, so the
   theme's font files are never requested).
5. **`software-wcag.css` exists** in Radzen.Blazor 13.1.0 (2.6 KB). It only redefines the `:root`
   colour variables (base ramp, state colours, link and input border), all of which our map overrides.
   Decision: **`Wcag` stays false** (no benefit, one more request); contrast is owned by our tokens and
   guarded by `Design/TokenContrastTests.cs` (T019).
6. Specificity: `html[data-theme]` (0,1,1) outranks the theme's `:root, .rz-software` (0,1,0), and the
   map is loaded after the theme, so it wins in both respects.

## Radzen variable map (design-spec.md 2.7 completed)

`wwwroot/css/radzen-map.css` maps, under `html[data-theme]`:

| Group | Variables | Token |
|---|---|---|
| Base ramp | `--rz-white`, `--rz-black`, `--rz-base`, `--rz-base-50..900`, `-light/-lighter/-dark/-darker`, `--rz-on-base*` | raised, surface, base, divider, control-border, muted, ink |
| Primary | `--rz-primary(-light/-lighter/-dark/-darker)`, `--rz-on-primary*` | primary (= ink), on-primary; shades by `color-mix` with base |
| Secondary / info | `--rz-secondary*`, `--rz-on-secondary*`, `--rz-info*`, `--rz-on-info*` | muted / ink |
| States | `--rz-success*`, `--rz-warning*`, `--rz-danger*` (+ `-lighter` tints, `--rz-on-*`) | success, warning, danger, on-state |
| Text and page | `--rz-text-title/-color/-secondary/-tertiary/-disabled/-contrast-color`, `--rz-text-font-family`, `--rz-body-font-size`, `--rz-body-background-color`, `--rz-link(-hover)-color` | ink, muted, base, font |
| Shape | `--rz-border-radius`, `--rz-input-border`, `--rz-input-hover-border`, `--rz-input-background-color`, `--rz-card-border-radius`, `--rz-card-background-color`, `--rz-dialog-border-radius`, `--rz-badge-border-radius`, `--rz-badge-text-transform`, `--rz-badge-letter-spacing` | radius-control/panel/float/pill, control-border, raised, surface |
| Focus | `--rz-outline-color`, `--rz-outline-width`, `--rz-outline-offset` | focus, 2 px, 2 px |
| Elevation | `--rz-shadow-1..10`, `--rz-dialog-shadow`, `--rz-tooltip-shadow`, `--rz-overlay-shadow`, button/checkbox/radio/slider/input/grid/tabs/datalist/dropdown shadows and gradients | elev-0 / elev-float / none |
| Components | `--rz-progressbar-background-color`, `--rz-progressbar-value-background-color`, `--rz-progressbar-color`, `--rz-dialog-close-color`, `--rz-tooltip-background-color`, `--rz-tooltip-color`, `--rz-tooltip-border-radius` | sunken, ink, raised, radius-panel |
| Targeted rules | outlined light/dark hover background; `.rz-dialog-titlebar-close` colour | raised, ink |

Every mapped name was checked to exist in `software.css` 13.1.0.

## Decision

**GO - option A (token layer over the free MIT `software` base).** Only a handful of colours needed
targeted overrides, all expressible through tokens. Option B (compiled SCSS) is not needed.

## Files changed

- `src/StemStudio.Web/Components/Pages/Dev/ThemeSpike.razor` (temporary; removed in T020)
- `docs/handoffs/frontend-004-spike.md`

## Tests and coverage

Spike only (no production logic). Probe output: dark 1 / light 0 off-token computed colours over
53 elements (see Findings 3).

## Quality checks

`dotnet build` 0 warnings (page compiles; `IWebHostEnvironment` guard).

## Dependencies added

None.

## Open issues / requests

None. The draft token files are committed with T020 under test (T019).

## Next owner

frontend (T013 onwards).
