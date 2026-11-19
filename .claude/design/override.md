# StemStudio design (override)

This project uses its **own** design system, not a Tipoca preset. It fully overrides the Tipoca default and the
preset in `.claude/tipoca/design/selection.json`. Set on the owner's word (2026-11-19).

## Source of truth

- Spec: `specs/004-design-system-ux/design-spec.md` (design tokens 1.0, section 2).
- Tokens: `src/StemStudio.Web/wwwroot/css/tokens.css`, the **only** file allowed to contain literal colours
  (enforced by `tools/quality/css_tokens_check.py`). Radzen is mapped onto these tokens in
  `src/StemStudio.Web/wwwroot/css/radzen-map.css`.
- When this file and `tokens.css` disagree, `tokens.css` wins. Update the JSON block below to match it.

## Rules (summary; the spec has the details)

- Themes: dark ("night session", default and fallback) and light ("day session"), chosen by `html[data-theme]`;
  density through `html[data-density]`, motion through `html[data-motion]`; `first-paint.js` sets all three.
- Primary is the ink colour (neutral, high contrast); the warm amber `focus` colour is the only bright UI accent.
- Stem colours (`--ss-stem-*`) identify instrument families; every family counts equally, none is visually favoured.
- Font: Archivo (variable width) with `Segoe UI Variable` / `system-ui` fallback; tabular numbers for counters.
- Radius encodes containment: control 2px, panel 6px, floating 12px, pill 999px.
- 4px spacing grid; targets never below 24px (WCAG 2.5.8); forced-colours mode falls back to system colours.
- Use `var(--ss-*)` tokens in components; never hard-code colours outside `tokens.css`.

## Tokens (design override token contract)

```json
{
  "source": "src/StemStudio.Web/wwwroot/css/tokens.css (StemStudio design tokens 1.0, specs/004-design-system-ux/design-spec.md section 2)",
  "dark": {
    "canvas": "#141A24",
    "surface": "#1C2431",
    "surfaceSoft": "#10151D",
    "text": "#E9EDF3",
    "muted": "#A3ADBD",
    "line": "#2E3949",
    "primary": "#E9EDF3",
    "onPrimary": "#141A24",
    "accent": "#FFB547",
    "onAccent": "#10151D",
    "success": "#5FD38D",
    "warning": "#F6C944",
    "danger": "#FF7A70",
    "raised": "#252F3F",
    "controlBorder": "#6B7A93",
    "stemVocals": "#F28AB3",
    "stemDrums": "#F5A04E",
    "stemBass": "#A493F5",
    "stemGuitar": "#3FCBB4",
    "stemPiano": "#6FB9F7",
    "stemOther": "#B4CE73"
  },
  "light": {
    "canvas": "#E6EAF0",
    "surface": "#F7F9FC",
    "surfaceSoft": "#DCE1E8",
    "text": "#172030",
    "muted": "#4F5B6E",
    "line": "#CBD2DC",
    "primary": "#172030",
    "onPrimary": "#F7F9FC",
    "accent": "#9A5B00",
    "onAccent": "#FFFFFF",
    "success": "#1E7A45",
    "warning": "#8A6200",
    "danger": "#B3261E",
    "raised": "#FFFFFF",
    "controlBorder": "#6E7B90",
    "stemVocals": "#B8336A",
    "stemDrums": "#A85400",
    "stemBass": "#5B45C9",
    "stemGuitar": "#00806D",
    "stemPiano": "#1F6FB5",
    "stemOther": "#5B7417"
  },
  "fonts": {
    "display": ["Archivo", "Segoe UI Variable", "system-ui", "sans-serif"],
    "body": ["Archivo", "Segoe UI Variable", "system-ui", "sans-serif"],
    "mono": ["Cascadia Mono", "Consolas", "ui-monospace", "monospace"]
  },
  "radius": { "control": "2px", "card": "6px", "window": "12px", "hero": "12px" }
}
```

Notes: `accent` is the focus/amber token (`--ss-color-focus`); `surfaceSoft` is `--ss-color-sunken`. The mono stack
is not defined in `tokens.css`; it uses system monospace fonts.
