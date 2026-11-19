STATUS: SYNCED 2026-11-17

# Tipoca default design system

This is the house style Tipoca uses for any UI or visual design unless the project overrides it.
Machine-readable values are in `tokens.json` next to this file. If this document and `tokens.json`
disagree, `tokens.json` wins for values and this document wins for usage rules.

## Source

- Origin: Superdesign default project (private; snapshot).
- Snapshot date: 2026-11-17. Taken from 27 design drafts (OS-style shell: desktop, login, MFA,
  settings, dashboards, user management, logs, notifications, file manager, tool consoles).
- The source has no stored design-system object, components or brand assets. Tokens were taken
  from the CSS variables, custom CSS classes and Tailwind utility usage the drafts share.
- Values marked **derived** in this file or in `tokens.json` are not in the source. Tipoca added
  them to fill a gap (light mode, accessible text tints). Everything else is taken from the source.

## Tone

- **Visual character:** dark, cinematic and "command centre". Near-black canvas, frosted-glass
  panels, one strong crimson accent, a faint crimson glow in the background. Precise and technical,
  premium rather than playful.
- **Copy voice:** confident, terse and operational. Labels sound like system status
  ("System integrity at 100%", "Real-time telemetry", "Security protocols isolated the threat").
- **Copy style rules:**
  - Headings and titles use Title Case ("Account Security", "Network Interfaces").
  - Section eyebrows and table headers are short UPPERCASE labels with wide letter spacing.
  - Subtitles are short meta lines, often separated by ` | ` ("Kernel v4.2.0 | Administrator Session").
  - Body copy is one or two plain sentences. State the fact first, then the action.
  - Show numbers, versions, ids and timestamps in monospace. Say exactly what happened
    ("Version 4.2.1 is ready for installation"), never just "Something went wrong".
  - Buttons use short verbs: "Add User", "Refresh", "Apply Theme".

## Colour

The source is **dark-only**. It shows a light/dark toggle but defines no light palette.

### Dark (canonical, from source)

| Token | Value | Use |
|---|---|---|
| `bg.canvas` | `#050505` | Page and app background |
| `bg.mesh` | radial mesh: `#1a1a1a`, `#2d0a0f` (crimson-tinted corner), `#111` over `#050505` | Desktop/shell background |
| `bg.glow` | `rgba(220,20,60,0.06)` radial, 800px, blur 80px | Ambient accent glow behind content |
| `bg.sunken` | `#0a0a0a` | Sticky table headers, solid fallback |
| `surface.panel` | `rgba(15,15,15,0.45)` + blur 20px | Chrome: top bar, side rail, dock, tiles |
| `surface.card` | `rgba(20,20,20,0.40)` + blur 24px | Cards, login card |
| `surface.window` | `rgba(10,10,10,0.70)` + blur 32px | Main content windows and panels |
| `surface.subtle` | `rgba(255,255,255,0.03)` | List items, notification cards |
| `fill.control` | `rgba(255,255,255,0.05)` | Inputs, secondary buttons, chips |
| `fill.control-hover` | `rgba(255,255,255,0.10)` | Hover on controls |
| `fill.control-focus` | `rgba(255,255,255,0.08)` | Focused input background |
| `border.subtle` | `rgba(255,255,255,0.05)` | Dividers, inner borders |
| `border.default` | `rgba(255,255,255,0.08)` to `0.10` | Panel and control borders |
| `border.strong` | `rgba(255,255,255,0.20)` | Hover borders |
| `text.primary` | `#ffffff` | Headings, primary text |
| `text.secondary` | `rgba(255,255,255,0.70)` to `0.80` | Nav links, body text |
| `text.muted` | `rgba(255,255,255,0.40)`; also `#6b7280` (gray-500), `#9ca3af` (gray-400) | Labels, meta, table headers (see accessibility notes) |
| `text.faint` | `rgba(255,255,255,0.20)` to `0.30` | Decorative or disabled text only |
| `accent` | `#DC143C` (crimson) | Primary action, active state, focus border, brand highlight |
| `accent.hover` | `#B21032` | Hover/pressed primary |
| `accent.deep` | `#8B0000` | Gradient end (`#DC143C` to `#8B0000`, bottom right) for logo tiles and icon badges |
| `accent.tint` | `rgba(220,20,60,0.10)` to `0.20` | Active nav background, accent badges, selected chips |
| `accent.border` | `rgba(220,20,60,0.20)` to `0.40` | Accent badge and chip borders |
| `success` | `#10b981` / text `#34d399`, `#4ade80` | Healthy, online, passed |
| `warning` | `#f59e0b` / text `#fbbf24` | Warnings, quota |
| `info` | `#3b82f6` / text `#60a5fa` | Updates, informational |
| `danger` | `#ef4444` / text `#f87171` | Errors, destructive. Use crimson for brand and red for errors |

Alternative accents offered by the source's theme picker: blue `#3b82f6`, emerald `#10b981`,
amber `#f59e0b`, violet `#8b5cf6`, pink `#ec4899`, white `#ffffff`. Crimson is the default.

### Light (derived, not in source)

Use this only when a product needs light mode. It keeps the same accent and structure:
canvas `#f7f7f8`, window `rgba(255,255,255,0.80)` + blur 32px, panel `rgba(255,255,255,0.60)`,
border `rgba(0,0,0,0.08)`, text primary `#0a0a0a`, secondary `rgba(0,0,0,0.70)`,
muted `rgba(0,0,0,0.55)`, accent `#C8102E` (darker crimson so it reaches AA on white). Exact values are in `tokens.json` → `color.light`.

## Typography

- **Sans (UI and display):** `Satoshi`, fallback `"Plus Jakarta Sans", ui-sans-serif, system-ui, sans-serif`.
  Satoshi is served by Fontshare, not Google Fonts. The source drafts try to load it from Google
  Fonts, which fails silently. Load it from Fontshare or self-host it.
- **Mono (data, ids, versions, code, timestamps):** `"JetBrains Mono", ui-monospace, monospace`.
- Weights: 300, 400, 500 (medium), 700 (bold, the most used weight), 900 (black, for display and
  eyebrow labels). Semibold 600 is rare.

| Role | Size / line height | Weight | Tracking | Notes |
|---|---|---|---|---|
| Display | 36px / 40px | 700 | -0.025em | Screen title on launcher-style pages |
| H1 | 30px / 36px | 900 | -0.025em to -0.05em | Page title |
| H2 | 24px / 32px | 700 to 900 | -0.025em | Section/profile title |
| H3 | 20px / 28px | 700 | -0.025em | Panel title |
| H4 / panel title small | 18px / 28px | 700 | normal | Card title |
| Body | 14px / 20px | 400 to 500 | normal | Default text |
| Body long | 14px / 1.625 | 400 | normal | Paragraphs (`leading-relaxed`) |
| Small | 12px / 16px | 500 to 700 | normal | Nav, inputs, buttons, table cells |
| Eyebrow / label | 10px / 14px | 700 to 900 | 0.1em to 0.3em, UPPERCASE | Section labels, table headers, button labels |
| Micro | 9px / 12px | 900 | 0.1em, UPPERCASE | Badges and status chips only |
| Wordmark | 12px | 900 | -0.05em | Two-part wordmark; the second part is in accent |

## Spacing

The base unit is 4px (Tailwind scale). Steps used: 4, 6, 8, 12, 16, 20, 24, 32, 40, 48px.

- Inside controls: 8 to 16px horizontal, 8 to 16px vertical (buttons `8px 16px` to `8px 24px`,
  large CTA `16px` vertical).
- Inside cards and windows: 24 to 32px (`p-6` / `p-8`). Hero and login cards use 40 to 48px.
- Gap between panels: 24px. Stacks inside panels: 8 / 12 / 24 / 32px.
- Table cells: 16px vertical, 16px horizontal (32px on the outer columns).

## Radii

The source uses large, soft radii. The bigger the container, the bigger the radius.

| Token | Value | Use |
|---|---|---|
| `radius.xs` | 6px | Badges, colour swatches, small chips |
| `radius.sm` | 8px | Segmented buttons, small chips, range track ends |
| `radius.md` | 12px | Buttons, inputs, icon buttons (the default control radius) |
| `radius.lg` | 16px | Large inputs (login), large CTAs, tiles |
| `radius.xl` | 24px | Cards |
| `radius.2xl` | 32px | Content windows and panels, login card |
| `radius.3xl` | 40px | Hero windows, launcher app tiles |
| `radius.full` | 9999px | Avatars, toggles, status dots, round icon buttons |

## Elevation

Depth comes from glass layers (translucency + backdrop blur + a 1px light border) plus a few soft,
deep shadows.

| Level | Recipe | Use |
|---|---|---|
| 0 canvas | `#050505` + mesh + glow | Background |
| 1 panel | `surface.panel`, blur 20px, border `rgba(255,255,255,0.08)`, no shadow | Shell chrome, tiles |
| 2 card | `surface.card`, blur 24px, border `rgba(255,255,255,0.10)`, `0 25px 50px -12px rgba(0,0,0,0.5)` | Cards |
| 3 window | `surface.window`, blur 32px, border `rgba(255,255,255,0.08)`, `0 30px 60px -12px rgba(0,0,0,0.6)` | Content windows, modals |
| accent glow | `0 10px 15px -3px rgba(220,20,60,0.20)` | Primary buttons |
| control glow | `0 0 10px rgba(220,20,60,0.40)` | Slider thumb, focused accent control |
| inset | `inset 0 2px 4px rgba(0,0,0,0.05)` | Progress tracks, wells |

## Layout

- **App shell (OS-style):** top bar 40px (glass panel, wordmark + menu left, status/clock/bell right),
  left icon rail 80px (glass panel, vertical icons, active item = accent tint + 3px right accent
  border), optional bottom dock 64px, main canvas scrolls inside.
- **Content:** windows on a 12-column grid with 24px gaps. Common splits are 8/4, 2/3 + 1/3, and
  2-, 3- or 4-up stat rows. Use `max-width` 896 to 1152px for readable single-column pages.
  Login/MFA: one centred card, max 448px.
- **Settings pattern:** inner secondary nav on the left (active = accent tint + 2px left accent border),
  form sections on the right.
- **Launcher grid:** `repeat(auto-fill, minmax(100px, 1fr))`, 32px gap.
- **Breakpoints:** Tailwind defaults (sm 640, md 768, lg 1024, xl 1280). On mobile, the rail
  collapses and stats stack to one column.
- **Scrollbars:** 6px wide, transparent track, thumb `rgba(255,255,255,0.10)` with radius 10px.
  The thumb turns crimson or lighter on hover.

## Components

All controls sit on dark glass. The default state is quiet (white at 5% fill and a 10% border).
Hover makes the fill or border brighter. Selected and primary states use crimson.

- **Button, primary:** bg `#DC143C`, white text, 10 to 12px bold UPPERCASE (often with wide
  tracking), padding `8px 16px` to `8px 24px`, radius 12px, accent glow shadow.
  Hover `#B21032`. The large CTA variant is full width, 16px vertical padding, radius 16px,
  tracking 0.3em, and scales to 1.02 on hover. Disabled: 40% opacity, no glow.
- **Button, secondary:** bg `rgba(255,255,255,0.05)`, border `rgba(255,255,255,0.10)`, text
  `rgba(255,255,255,0.70)`, radius 12px. Hover bg 10%, text white.
- **Button, ghost / icon:** no fill, text `rgba(255,255,255,0.30 to 0.40)`. Hover text white (or
  crimson for destructive or accent actions). Round icon button: 40px, radius full, 5% fill, 10% border.
- **Segmented / filter chip:** padding `4px 12px`, radius 8px, 9 to 10px bold UPPERCASE.
  Active: bg crimson or accent tint with accent border. Inactive: 5% fill, muted text, hover 10%.
- **Input (text/search):** bg `rgba(255,255,255,0.05)`, border `rgba(255,255,255,0.10)`, radius 12px
  (16px for large/login), padding `8px 12px` to `12px 16px`, 12 to 14px text, placeholder gray-600.
  Optional leading icon at 36 to 48px left padding. Focus: border crimson, bg 8%, no outline ring.
  Error: border danger and a helper line in danger text.
- **Checkbox / radio:** `accent-color: #DC143C`, 16px, radius 4px.
- **Toggle switch:** 38 to 40 x 20px pill. Track `rgba(255,255,255,0.10)`, crimson when on.
  Knob is 14px white, 400ms slide.
- **Range slider:** track 4px at 10% white, radius 2px. Thumb 14 to 16px crimson circle with control glow.
- **Card / window:** glass recipe (see Elevation), radius 24 to 32px, padding 24 to 32px.
  Header row: title (18 to 20px bold) with an icon, actions on the right. Interactive list card:
  hover bg 6%, border 15%, slides 4px to the right.
- **Badge / tag:** padding `2px 8px`, radius 6 to 8px, 9 to 10px black UPPERCASE with wide tracking.
  Neutral: 5% fill, 10% border, muted text. Accent: crimson 10% fill, 20% border, crimson text.
  Status badges use the same recipe in success/warning/info/danger hues.
- **Status dot:** 6 to 8px, radius full, solid status colour. Live dots pulse (2s).
- **Table:** full width, left aligned, collapsed borders. Header: 10px black UPPERCASE with wide
  tracking, muted text, optional sticky `#0a0a0a` or 2% fill background, bottom border 10%.
  Rows: 16px vertical padding, 5% dividers, hover 2% fill. Put ids and numbers in mono; put
  row actions right aligned as ghost icon buttons.
- **Navigation:** icon rail and sidebar items (active = accent tint 15% + 3px accent border on the
  inside edge, white text; hover 3% fill). Top-bar text links 12px medium at 70% white, white on hover.
- **Progress / meter:** 6 to 8px track at 5 to 10% white, radius full. The fill is the accent or a
  status colour.
- **Notification item:** subtle glass card with a coloured icon tile (status colour), a bold title,
  a one-line description and a mono timestamp on the right.
- **Avatar:** circle, 32 to 48px. A crimson gradient ring marks the current user.
- **Logo tile:** small square with a crimson to deep-red gradient, radius 6px, white icon.

## Iconography

- Set: **Lucide** (the source loads it through Iconify, `lucide:*`). Use Lucide everywhere and do
  not mix icon sets.
- Style: outline, 2px stroke, rounded caps.
- Sizes: 14px in dense chrome and tables, 16 to 20px in buttons and nav, 24px in rail and feature
  tiles, 32px+ in launcher tiles.
- Colour: inherits text colour. Active or brand icons are crimson. Status icons use the status hue,
  sometimes on a tinted 40px rounded tile.

## Motion

- Default: `transition: all 300ms cubic-bezier(0.4, 0, 0.2, 1)`. Colour-only changes take 200ms.
- Toggles and sliders: 400ms. Large card hover: 500ms.
- Hover lift: `translateY(-4px)` (tiles) or `translateX(4px)` (list cards). Press: `scale(0.95)`.
  CTA hover: `scale(1.02)`.
- Page transitions: cross-fade, 250ms ease-out for the old page and 250ms ease-in after 100ms for
  the new page (View Transitions API). Shell chrome (top bar, rail, dock) stays fixed.
- Ambient: live status pulses (2s), charts draw their lines (stroke-dash). Keep ambient motion subtle.
- Respect `prefers-reduced-motion: reduce`: drop the transforms, the pulses and the line drawing,
  and keep only short opacity fades.

## Accessibility notes

- Contrast was checked against `#050505`/`#0a0a0a` (WCAG 2.2):
  - White text passes everywhere. White on crimson `#DC143C` is about 5.0:1, which passes AA.
  - Crimson `#DC143C` as text on black is about 4.1:1. Use it only for large or bold text (18px+, or
    14px+ bold) and for UI accents (3:1). For small crimson text use the **derived** `accent.text`
    `#FF4D6D` (about 6.3:1).
  - `rgba(255,255,255,0.40)` text is about 3.8:1 and **fails** AA for small text. The source uses it
    a lot for labels. Tipoca rule: body and label text is at least `rgba(255,255,255,0.60)` (about
    7:1). Keep 20 to 40% white for decorative or disabled text only.
- Focus: the source shows focus only as a crimson border on inputs. Tipoca adds a visible
  `:focus-visible` ring on every interactive element: `2px solid #FF4D6D`, offset 2px.
- Glass needs fallbacks. Where `backdrop-filter` is unsupported, or when the user prefers reduced
  transparency, use solid `#0f0f0f` / `#141414`.
- Never show status by colour alone: pair the dot or colour with a text label or an icon.
- Icon-only buttons need `aria-label`. The source drafts have no ARIA at all, so add it.
- 9 to 10px uppercase labels are a stylistic choice. Never use them for essential or long content,
  and keep 12px as the minimum size for body text.
- Hit targets are at least 24 x 24px (WCAG 2.5.8). Aim for 40px in primary navigation.

## Usage rules

1. Default to dark mode. Use the derived light palette only when the product explicitly needs it.
2. Use one accent per screen (crimson by default). Keep status colours for status only.
3. Layer surfaces as canvas, then panel, then card, then window. Do not nest more than two glass
   layers, because the blur gets muddy.
4. Each view has one primary (crimson) button. Every other action is secondary or ghost.
5. Bigger containers get bigger radii: controls 12, cards 24, windows 32, hero 40.
6. Use monospace for any machine value (ids, versions, metrics, timestamps, code).
7. Use UPPERCASE with wide tracking only for short labels (3 words or fewer), never for sentences.
8. Apply the accessibility tightening above (60% minimum text tint, `accent.text` for small
   crimson text, focus-visible ring). It takes precedence over a literal copy of the source.
9. Read token values from `tokens.json`. Do not hard-code values that differ from it.
10. A project's `.claude/design/override.md` replaces this file completely (see README).
