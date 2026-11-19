STATUS: SYNCED 2026-11-17

# Tipoca design preset: enterprise

Direction A, **Enterprise Precision**. Default for **application UI and PDF documents** (see
`../mix.json`). Machine-readable values are in `tokens.json` next to this file; if this document and
`tokens.json` disagree, `tokens.json` wins for values and this document wins for usage rules.

## Source

- Origin: the Tipoca software factory design project in Superdesign (private; snapshot).
- Snapshot date: 2026-11-17. Taken from the Direction A board, the design-system board and the dark
  and light Factory Console.
- Values marked **derived** (here, or `$extensions.tipoca.derived: true` in `tokens.json`) are not
  in the source; Tipoca added them for fallbacks and accessibility.

## Tone

- **Visual character:** precise, calm and dense. Flat near-black canvas, solid surfaces, 1px
  hairline borders, strict grid, compact controls and small radii. No mesh, no glass, no glow.
- **Copy voice:** operational and exact. Eyebrows are UPPERCASE labels; ids, timings and versions
  are in mono; state the fact first, then the action.

## Colour

One accent: crimson marks brand, primary action, active state and the focus series in charts.
Errors use signal orange, never crimson.

### Dark (canonical)

| Token | Value | Use |
|---|---|---|
| `bg.canvas` | `#0B0B0C` | Page background (flat) |
| `bg.sunken` | `#050505` | Wells, code, preview frames |
| `surface.default` | `#0B0B0C` + 1px white 0.08 border | Header and chrome |
| `surface.raised` | `#111113` + 1px white 0.08 border | Cards, tiles ("surface-2") |
| `fill.control` | white 0.05 | Inputs, secondary buttons |
| `border.default` / `subtle` / `strong` | white 0.08 / 0.06 / 0.20 | Hairlines, dividers, hover |
| `text.primary` / `secondary` / `muted` / `faint` | white / 0.72 / 0.60 / 0.50 | Headings / body / labels / mono meta |
| `accent.default` / `hover` / `deep` | `#DC143C` / `#B21032` / `#8B0000` | Brand |
| `accent.text` | `#FF8095` | Small crimson text, links, focus ring |
| `status.*` | success `#22C55E`, warning `#F59E0B`, danger `#F97316`, info `#60A5FA`, running `#DC143C` (pulse), queued `#71717A` | Always with icon and label |
| `chart.focus` / `comparison-1/2` | `#DC143C` / `#71717A` / `#3F3F46` | Focus + context charts (steel grey context) |
| `chart.categorical` | crimson, teal `#2DD4BF`, amber, azure `#60A5FA`, violet `#A78BFA`, sand `#D6C7A1` | Only when categories need hues |

Carbon and crimson scales as in the cinematic preset. Brand gradient: `135deg #DC143C → #8B0000`
(primary button, brand mark, segmented active state).

### Light and print

Canvas `#FAFAF9`, sunken `#F4F4F5`, surfaces white with a 1px `#E4E4E7` border and `0 1px 2px
rgba(0,0,0,0.06)`, text `#0B0B0C` / `#3F3F46` / `#52525B`, accent `#C8102E`, status success
`#15803D`, warning `#B45309`, danger `#C2410C`, info `#1D4ED8`. Print (PDF): white pages, hairline
rules `#E4E4E7`, a 3px `#C8102E` rule for brand and callouts, mono numbers, footer
"Page n / m · Confidential" in `#71717A`.

## Typography

Geist and Geist Mono (SIL OFL 1.1; self-host). Weights 400, 500, 600.

| Style | Screen | Print |
|---|---|---|
| Display | 40px / 1.1, 600, -0.02em | 28pt |
| H1 | 32px / 1.15, 600 | 24pt |
| H2 | 22px / 1.25, 600 | 16pt |
| H3 | 16px / 1.35, 600 | 12pt |
| Body | 15px / 1.6, 400 | 10pt |
| Small / controls | 13px | — |
| Eyebrow | 11px, 600, 0.18em, UPPERCASE, muted (crimson on the page title) | — |
| Wordmark | 14px, 600, 0.32em, UPPERCASE | — |
| Mono | Geist Mono 13px, tabular figures | 9pt |

## Spacing

4px base: 4, 8, 12, 16, 24, 32, 48, 64, 96. Sections `py-12` (48px) with a hairline top border;
grid gaps 12-16px; card padding 20-24px.

## Radii

4, 6 (controls, chips), 10, 12 (cards), 16, full (dots, progress). Site roles: control 6px, card
12px, window 12px, hero 16px.

## Elevation

Depth by value steps, no blur: L0 canvas `#0B0B0C`; L1 surface + border; L2 surface-2 `#111113` +
border; L3 surface-2 + strong border. No glow.

## Layout

Content max 1280px with 24px gutters. Sticky header 56px: logo + spaced wordmark, 13px nav, compact
gradient call-to-action (32px high, 6px radius). Page title 40px; cards in 3-column grids with
12-16px gaps; tables full width with hairline rows.

## Components

- **Buttons:** 32px high, 6px radius, 13px/500. Primary = brand gradient; secondary = white 0.05
  fill + border; ghost = text; destructive = `#F97316`.
- **Inputs:** 32px high, 6px radius, white 0.05 fill, white 0.10 border; focus = crimson border + 2px
  ring.
- **Segmented control, tabs, chips:** compact; segmented active = brand gradient; tab active = 2px
  crimson underline; chips 6px radius with a mono id.
- **Lane card:** title, mono lane id, 8px progress bar in brand gradient, status tags.
- **Pipeline stepper:** done = gradient circle with check; current = crimson ring, pulsing; next =
  outline with mono number.
- **KPI tile:** eyebrow, 36px value, delta in success green, crimson sparkline.
- **Data table:** UPPERCASE eyebrow headers, hairline rows, mono ids and numbers, right-aligned
  numbers.
- **Log viewer:** `#0B0B0C` well, mono 12px, dim line numbers, status glyph per line.

## Iconography

Lucide, 1.5-2px stroke, 16px in controls. Status icons always pair with a label.

## Motion

Hover 120ms, state 200ms, panel enter 320ms, page fade 250ms, easing `cubic-bezier(0.2,0.8,0.2,1)`.
No lift on hover; borders and fills change instead. Everything stops under `prefers-reduced-motion`.

## Accessibility notes

- Text at least 4.5:1; muted text is white 0.60 on `#0B0B0C`.
- Crimson `#DC143C` as text only at 18px+; smaller crimson text uses `#FF8095` (dark) or `#C8102E`
  (light).
- Focus is always visible: 2px ring in the accent text colour with a 2px offset.
- Status pairs colour with icon and label; print carries every meaning without colour.

## Usage rules

- One accent; hierarchy through size, weight and value steps, not hues.
- Solid surfaces only. If a design needs glass or a mesh, it belongs to the cinematic preset.
- Mono for ids, versions, timings, hashes, file paths and numbers.
