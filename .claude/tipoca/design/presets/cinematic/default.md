STATUS: SYNCED 2026-11-17

# Tipoca design preset: cinematic

Direction B, **Cinematic Foundry**. Default for **visuals and the project website** (see
`../mix.json`). Machine-readable values are in `tokens.json` next to this file; if this document and
`tokens.json` disagree, `tokens.json` wins for values and this document wins for usage rules.

## Source

- Origin: the Tipoca software factory design project in Superdesign (private; snapshot).
- Snapshot date: 2026-11-17. Taken from the Direction B board, the design-system board, the dark
  and light Factory Console and the Visuals Kit.
- Values marked **derived** (here, or `$extensions.tipoca.derived: true` in `tokens.json`) are not
  in the source; Tipoca added them for fallbacks and accessibility.

## Tone

- **Visual character:** cinematic and premium. Near-black canvas with a crimson radial mesh, deep
  frosted glass with a 1px light top highlight, very large radii, oversized display type and the
  "template, then clone" motif: rows of crimson capsules in perspective, fading as they recede.
- **Copy voice:** short, declarative, confident ("One template in. Consistent output out."). Eyebrows
  are UPPERCASE labels; evidence (ids, timings, versions) is in mono.

## Colour

One accent: crimson marks brand, primary action, active state and the focus series in charts. It is
never decoration and never an error colour (errors use signal orange).

### Dark (canonical)

| Token | Value | Use |
|---|---|---|
| `bg.canvas` | `#050505` | Page background |
| `bg.mesh-1/2/3` | `#3A0D14` at 85% 8%, `#201012` at 12% 92%, `#2D0A0F` at 50% 120% | Radial mesh over the canvas, fixed |
| `bg.glow` | `rgba(220,20,60,0.10)`, 220px | Ambient glow |
| `bg.sunken` | `#0B0B0C` | Code, log viewer, scrollbar track |
| `surface.panel` | `rgba(15,15,15,0.60)` + blur 32px | Header, chips, small tiles |
| `surface.card` | `rgba(24,24,27,0.52)` + blur 32px | Cards |
| `surface.stage` | `#050505` + mesh-3 from below | Hero stage |
| `surface.solid-fallback` | `#111113` (derived) | Without backdrop-filter |
| `border.default` / `subtle` / `strong` | white at 0.10 / 0.06 / 0.20 | Card borders, dividers, hover |
| `border.highlight` | white at 0.12, inset top 1px | Glass highlight |
| `text.primary` / `secondary` / `muted` / `faint` | white / 0.72 / 0.60 / 0.50 | Headings / body / labels / mono meta |
| `accent.default` / `hover` / `deep` / `light` | `#DC143C` / `#B21032` / `#8B0000` / `#FF4D6D` | Brand crimson scale |
| `accent.text` | `#FF8095` | Small crimson text, links, focus ring |
| `status.*` | success `#22C55E`, warning `#F59E0B`, danger `#F97316`, info `#60A5FA`, running `#DC143C` (pulse), queued `#71717A` | Always with icon and label |
| `chart.focus` / `comparison-1/2` | `#DC143C` / `#71717A` / `#3F3F46` | Focus + context charts (steel grey context) |
| `chart.categorical` | crimson, teal `#2DD4BF`, amber, azure `#60A5FA`, violet `#A78BFA`, sand `#D6C7A1` | Only when categories need hues |

Carbon scale: 950 `#050505`, 900 `#0B0B0C`, 800 `#18181B`, 600 `#3F3F46`, 500 `#71717A`, 400
`#A1A1AA`, 100 `#F4F4F5`. Crimson scale: 300 `#FF8095`, 400 `#FF4D6D`, 500 `#DC143C`, 600 `#B21032`,
700 `#8B0000`, 900 `#2D0A0F`.

Gradients: brand `135deg #FF4D6D 0% → #DC143C 45% → #8B0000 100%` (primary buttons, brand mark,
progress); capsule `180deg` same stops at 0/50/100%; divider `90deg transparent → #8B0000 20% →
#DC143C 50% → #8B0000 80% → transparent`, 2px high, between sections.

### Light

From the light Factory Console: canvas `#FAFAF9`, sunken `#F4F4F5`, surfaces white with a 1px
`#E4E4E7` border and `0 1px 2px rgba(0,0,0,0.06)`, text `#0B0B0C` / `#3F3F46` / `#52525B`, accent
`#C8102E`, status success `#15803D`, warning `#B45309`, danger `#C2410C`, info `#1D4ED8`. The mesh is
reduced to a faint tint; glass becomes solid white.

## Typography

Geist for everything, Geist Mono (tabular figures) for evidence. Both SIL OFL 1.1; self-host the
woff2 files (no font CDN). Weights 400, 500, 600 only.

| Style | Screen | Print |
|---|---|---|
| Hero (site only) | 80px / 0.98, 600, -0.03em | — |
| Display | 56px / 1.05, 600, -0.02em | 40pt |
| H1 | 36px / 1.1, 600, -0.015em | 26pt |
| H2 | 24px / 1.25, 600 | 18pt |
| H3 | 18px / 1.35, 600 | 13pt |
| Lead | 17px / 1.6, 400, secondary text | — |
| Body | 15px / 1.6, 400 | 10pt |
| Small | 13px / 1.6 | — |
| Eyebrow | 11px, 600, 0.18em, UPPERCASE, muted (crimson `#FF4D6D` on the hero) | — |
| Wordmark | 14px, 600, 0.32em, UPPERCASE | — |
| Mono | Geist Mono 13px | 9pt |

## Spacing

4px base: 4, 8, 12, 16, 24, 32, 48, 64, 96. Sections `py-20` (80px); hero 128px top, 112px bottom;
grid gaps 20-24px; card padding 24-40px.

## Radii

6, 8, 12 (controls), 16 (tiles), 24 (component cards), 32 (feature cards), 40 (hero stage and large
panels), full (chips, capsules, status dots). Site roles: control 12px, card 24px, window 32px,
hero 40px.

## Elevation

L0 canvas; L1 glass panel (blur 32, inset highlight); L2 card glass plus `0 20px 60px -20px
rgba(0,0,0,0.6)`; L3 `0 24px 64px rgba(0,0,0,0.6)`. Primary buttons glow `0 14px 36px -8px
rgba(220,20,60,0.45)`; capsules `0 8px 20px -6px rgba(220,20,60,0.4)`. Depth comes from light, never
from extra hues.

## Layout

Content max 1280px with 32px gutters. Sticky glass header 64px: logo + spaced wordmark left, quiet
13px nav (white 0.72, white on hover), one gradient call-to-action right. Hero: eyebrow, oversized
headline (max about 14 characters per line), lead (max 672px), then the capsule stage (4 rows of 7
capsules, rotateX 34deg, perspective 900px, opacity 1 / 0.8 / 0.55 / 0.3) and a row of three glass
cards. Sections are separated by the gradient divider, start with an eyebrow, and use 2-3 column
glass grids. Footer: hairline top border, wordmark and a mono meta line.

## Components

- **Buttons:** primary = brand gradient + glow, 12px radius, 14px/500; secondary = glass; ghost =
  text only; destructive = signal orange `#F97316`.
- **Inputs:** fill white 0.05, border white 0.10, 12px radius; focus = crimson border + 2px
  `rgba(220,20,60,0.45)` ring.
- **Segmented control, tabs, chips:** segmented active = brand gradient; tab active = 2px crimson
  underline; chips are full-radius glass with a mono id and a status dot.
- **Cards:** card glass, 24-32px radius, eyebrow + title + 13px muted text; link cards end with a
  crimson-300 "View …" and an arrow that nudges 4px on hover.
- **Principle tiles:** glass, 16px radius, mono crimson number ("01"), 18px title, 13px muted text.
- **Lane card, stepper, KPI tile, data table, log viewer:** as in the enterprise preset, with glass
  surfaces; running state pulses crimson.

## Iconography

Lucide, 1.5-2px stroke, 16-20px, white 0.72 or crimson for the active item. Status icons always pair
with a label.

## Motion

Hover 120ms, state 200ms, panel enter 320ms, page fade 250ms, easing `cubic-bezier(0.2,0.8,0.2,1)`.
Clone stagger 40ms. Status pulse 1.6s. Everything decorative stops under `prefers-reduced-motion`.

## Accessibility notes

- Text at least 4.5:1: muted text is white 0.60 (about 7:1 on the canvas); never use 0.40 for text.
- Crimson `#DC143C` as text only at 18px+ (about 4.1:1); smaller crimson text uses `#FF8095`.
- Focus is always visible: 2px `#FF8095` ring with a 2px offset (`#C8102E` in light).
- Status pairs colour with icon and label; no meaning carried by glow or blur alone (print parity).

## Usage rules

- One accent. Do not introduce extra hues for hierarchy; use light, glass and type size.
- Use the capsule motif once per page, as the hero visual; do not repeat it as decoration.
- Mono for ids, versions, timings, hashes, file paths and numbers.
- On light backgrounds and in print, switch to the light values; glow and blur become borders.
