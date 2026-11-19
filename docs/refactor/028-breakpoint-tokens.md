# 028 One set of breakpoints from the design spec, in one unit
- Status: proposed · Date: 2026-11-19 · Area: frontend / StemStudio.Web wwwroot/css (design system) · Priority: P3

## Problem statement
The design spec defines three breakpoints: `sm` < 768 px, `md` 768–1279, `lg` >= 1280
(`specs/004-design-system-ux/design-spec.md:90-91`). The component stylesheets implement them with five different values in two
units (`grep -rn '@media (m' src/StemStudio.Web/wwwroot/css`, 17 width queries):

| Written as | Where | Matches the spec? |
|---|---|---|
| `max-width: 767.98px` | `help.css:48`, `karaoke.css:207`, `mixer.css:341`, `scores.css:48`, `shell.css:314`, `track.css:186,336` | yes (`sm`) |
| `max-width: 767px` | `agent.css:178` | off by a fraction: between 767 and 768 px the shell is in `sm`, the agent panel is not |
| `max-width: 48rem` | `library.css:133,407,436`, `lyrics.css:208` | only at a 16 px browser font size |
| `min-width: 64rem` | `lyrics.css:58` | not a spec breakpoint (1 024 px) |
| `max-width: 30rem` | `library.css:413` | not a spec breakpoint (480 px) |
| `min-width: 1280px` | `agent.css:22`, `help.css:37`, `karaoke.css:23` | yes (`lg`) |

`rem` in a media query is relative to the browser's *initial* font size, not to `html { font-size }`. A user who raises the default
font size to 20 px (a common low-vision setting) gets `48rem` = 960 px: between 768 and 960 px the library and lyrics pages switch
to their small-screen layout while the shell (rail vs. bottom bar, `shell.css:314`) and the mixer stay in `md`. The colour discipline
is clean (no literal colour outside `tokens.css`; `grep -rnE '#[0-9a-fA-F]{3,8}|rgba?\(|hsla?\(' ... | grep -v tokens.css` finds
nothing), but breakpoints have no single source; the content width is `--ss-content-max: 80rem` (`tokens.css:62`) while the `lg`
query is `1280px`, which again only agree at 16 px.

## Impact
- Inconsistent layouts for users with a larger default text size (WCAG 1.4.4 Resize Text and 1.4.10 Reflow are about exactly
  this group): parts of a page reflow to the mobile layout while the navigation does not.
- Design drift: two breakpoints outside the spec mean the visual baselines in `e2e/visual/` do not cover the widths where those
  pages change.

## Risk
Risk rating: **low**. CSS-only; the visual regression project shows every change. Choosing px or em for all queries changes
layout only for users with a non-default font size, and makes it consistent. Rollback: revert the stylesheet change.

## Effort
S (0.5–1 day): normalise 17 queries, decide on the two extra breakpoints with design, add a lint rule. Assumes the Playwright
`visual` project can be re-baselined for any intended change.

## Step-by-step plan
1. Safety net: run the Playwright `visual` project at 767, 768, 1024, 1279 and 1280 px and with a 20 px default font size
   (Chromium `--force-device-scale-factor` is not enough; set the font size in the browser context) and keep the screenshots.
2. Decide the unit once (recommendation: `em` for all queries, `48em` / `80em`, so the whole UI reflows together with the user's
   text size) and record it in `design-spec.md` 2.x next to the breakpoints.
3. Replace the 17 queries with the spec values in that unit; for `lyrics.css:58` (64rem) and `library.css:413` (30rem) either map
   them to `md`/`sm` or add them to the spec as named component breakpoints.
4. Add `stylelint` with `media-feature-name-value-allowed-list` (or a small check in the existing Web design tests) that allows
   only the spec values.
5. Verify: visual baselines unchanged at 16 px except intended changes; at 20 px the shell, library and lyrics switch layout at
   the same width. Roll back by reverting.

## Dependencies and related proposals
`027-browser-glue-toolchain.md` (step 5 introduces stylelint; this rule can ship with it).
