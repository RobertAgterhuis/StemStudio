# Research: Design System and UX Foundation

Feature 004. Inputs: owner request (spec.md), `docs/research/roadmap-003-011.md` section 2 (called
"Feature 003" there; the roadmap was renumbered when GPU acceleration moved to 003), constitution
1.2.0, the current UI in `src/StemStudio.Web` (Radzen `material` theme, top header with three
links, `ss-*` styles in `wwwroot/app.css`), and the `frontend-design` guidance for a distinctive
aesthetic. Binding decisions: [ADR-013](../../docs/decisions/ADR-013-design-system-and-ux-foundation.md).

Status of facts: licences and Radzen theming facts come from the roadmap research (sources linked
there). Facts marked **verify** must be re-checked by the owning agent when the dependency is added
(tasks T001, T012, T020, T052, T054, T064); nothing in this feature was measured yet.

---

## R-01 Theme approach: token layer over a free base theme

**Decision**: Load one free MIT base theme, `software`, through `RadzenTheme` (no theme switching
at the Radzen level). Add `wwwroot/css/tokens.css` (StemStudio `--ss-*` tokens for both themes,
selected by `html[data-theme="dark"|"light"]`) and `wwwroot/css/radzen-map.css` (redefines every
used `--rz-*` variable from the `--ss-*` tokens), loaded after the base theme, then component
styles in `wwwroot/css/components/*.css`. Light/dark switching is a single attribute change on
`<html>`, so it is instant and needs no second stylesheet download.

**Rationale**: no build step and no copied SCSS (roadmap research option A); dark and light share
one base, so components look identical apart from tokens; the attribute is set by an inline
script before the first paint (R-10).

**Risk and fallback (ADR-013 D1)**: the `software` base may hard-code some colours outside the CSS
variables. Phase 1 task T012 (frontend spike) renders every Radzen component StemStudio uses
(button variants, badge, card, textbox, progress bar, dialog, tooltip, sidebar, dropdown,
checkbox, switch, tabs, notification) in both token sets and lists every computed colour that does
not come from a token. Go: at most a handful, fixed with targeted overrides in `radzen-map.css`.
No-go: option B, copy `software-base.scss` and partials (MIT, keep the notice) to
`src/StemStudio.Web/Styles/`, compile with Dart Sass (MIT) into `stemstudio-light.css` and
`stemstudio-dark.css`, and switch with `ThemeService`. B costs a Sass build step and drifts from
Radzen updates, so it is only taken on evidence.

**Alternatives rejected**: premium themes (`material3*`, `fluent*`, `zen*`), swatches and Radzen
Blazor Studio's designer (paid, constitution IV); switching between `software` and
`software-dark` via `ThemeService` (two different bases means two sets of overrides and a
stylesheet swap that flashes); `material` as base (ripples and Roboto proportions pull the look
towards stock Material, the opposite of distinctive); Tailwind or another CSS framework next to
Radzen (two styling systems, constitution V).

## R-02 Radzen WCAG stylesheet

**Decision**: set `Wcag="true"` on `RadzenTheme` only if a `software-wcag.css` exists in
Radzen.Blazor 13.1.0 (**verify** in T012); StemStudio's own focus ring tokens apply in any case.

**Rationale**: the WCAG variant improves focus and contrast of Radzen internals; our token layer
already sets contrast-safe colours (R-03), so it is a bonus, not a dependency.

## R-03 Design direction: "Colour belongs to the music"

The subject is a rehearsal tool for musicians at home: they take a song apart and play along. Its
vernacular is the mixing console and the rehearsal room: channel strips with coloured name tabs,
colour-coded cables, meter lamps, a tape counter. Comparable products (Moises, LALAL.AI, splitter
web apps, based on general product knowledge, **not a verified survey**) mostly use a near-black
background with a saturated purple, blue or neon gradient accent and marketing-style cards. The
direction below deliberately avoids that look.

**Principles**

1. *Colour belongs to the music.* Chrome (backgrounds, buttons, navigation) is graphite or cool
   paper, without a brand hue. Hue appears only where it identifies a stem or a state (meter
   levels, errors, success). Primary buttons are ink-filled (light on dark, dark on light).
2. *One strand becomes six.* The signature: a song is a grey strand that splits into the six stem
   colours. It appears once per context: the separation progress (the only orchestrated
   animation), the "stem ribbon" on track rows and the track header, and the logo mark.
3. *Console logic.* Each channel strip carries a coloured name tab at the top like masking tape
   on a desk; condensed type fits long detail names (`electric-guitar`) on narrow strips.
4. *Quiet until touched.* No decorative motion, no gradients, no shadows on flat content; elevation
   only for things that float (help panel, palette, dialogs, popovers).
5. *Hierarchy by radius.* Radius encodes containment: 2 px controls, 6 px panels, 12 px floating
   surfaces, full pill only for status chips. Not one radius for everything.

**Palette** (contrast measured with the WCAG 2.x formula; full token list in design-spec.md 2.1)

| Token | Dark ("night session") | Light ("day session") | Use |
|---|---|---|---|
| `base` | `#141A24` graphite blue | `#E6EAF0` cool paper | page background |
| `surface` | `#1C2431` | `#F7F9FC` | panels, strips |
| `raised` | `#252F3F` | `#FFFFFF` | floating surfaces, inputs |
| `ink` | `#E9EDF3` (14.9:1 on base) | `#172030` (13.5:1) | text, primary button fill |
| `muted` | `#A3ADBD` (7.7:1) | `#4F5B6E` (5.7:1) | secondary text |
| `control-border` | `#6B7A93` (3.1-4.0:1) | `#6E7B90` (3.6-4.3:1) | input and control boundaries |
| `focus` | `#FFB547` lamp amber (8.9:1 on surface) | `#9A5B00` (5.1:1 on surface) | focus ring, the one non-stem accent |

Stem identity colours (dark / light; all >= 5.8:1 on dark surfaces and >= 4.6:1 on light
`surface`/`raised`, so they are usable as text there; on the light `base` they are used for
graphics only, >= 4.0:1):

| Stem | Dark | Light |
|---|---|---|
| vocals | `#F28AB3` rose | `#B8336A` |
| drums | `#F5A04E` copper | `#A85400` |
| bass | `#A493F5` violet | `#5B45C9` |
| guitar | `#3FCBB4` teal | `#00806D` |
| piano | `#6FB9F7` sky | `#1F6FB5` |
| other | `#B4CE73` sage | `#5B7417` |

Detail stems use their parent's colour with a pattern index (solid, dashed tab underline, dotted),
never a new hue. States: success `#5FD38D` / `#1E7A45`, warning `#F6C944` / `#8A6200`, danger
`#FF7A70` / `#B3261E`. Meter gradient: success -> warning -> danger zones by dB, the clip lamp in
danger.

Review against the generic defaults: the first idea, "near-black studio with one neon accent", is
exactly the cluster comparable apps and generated designs fall into; it was replaced by a
blue-graphite base without a brand accent, where the six stem colours carry the identity. A cream
light theme was rejected for a cool paper grey. Monospace for numbers was rejected in favour of
tabular figures of the text face.

**Typography**

- One variable family: **Archivo** (Omnibus-Type, SIL OFL-1.1, axes width 62-125 and weight
  100-900; **verify** licence file and axes at the source in T020). The width axis is the type's
  personality: channel tabs and dense tables at width 75 (condensed), body at 100, page titles at
  width 112-118 and weight 750 (expanded, set tight). Numbers (time, BPM, dB, percent) use
  `font-variant-numeric: tabular-nums` so values do not jitter while playing.
- Scale (1.2 minor-third, 16 px base): 12, 13.3, 16, 19.2, 23, 27.6, 33.2 px; line height 1.5 body,
  1.15 titles; body lines <= 72 characters (help panel is 40-64 characters wide).
- Sentence case everywhere; no all-caps labels, no eyebrow labels above headings.
- Fallback stack `Archivo, "Segoe UI Variable", system-ui, sans-serif`; `font-display: swap`,
  only the Latin subset, one woff2 (about 90-110 KB, **verify**), preloaded.
- Rejected: Inter + JetBrains Mono (roadmap suggestion): Inter is the default face of countless
  web apps and would make the UI look generic; a second monospace family adds payload for numbers
  that tabular figures already handle. Space Grotesk, Roboto: same "default" problem.

**Motion** (tokens in design-spec.md 2.5)

- Durations: 90 ms (press feedback), 160 ms (hover/disclosure), 240 ms (panels, palette, dialogs),
  600 ms (the split reveal once when a separation completes).
- Easing: `cubic-bezier(0.2, 0, 0, 1)` for entering, `cubic-bezier(0.3, 0, 1, 1)` for leaving.
- Only transform and opacity are animated (no layout properties), never on meters (the engine
  updates them every frame).
- Reduced motion (OS or preference): durations become 0 ms except opacity fades of <= 120 ms; the
  split visual is drawn in its final state.

**Layout**: a narrow left rail (icons with labels, 72 px; collapses to a bottom bar under 768 px)
and a top context bar (page title, journey rail at Beginner, level switch, command palette, help,
preferences). Content is left-aligned on a 12-column grid with max width 1280 px except the
mixer, which is full-bleed: strips scroll horizontally, master pinned right, transport docked at
the bottom of the viewport. Wireframes per screen and level: design-spec.md section 4.

## R-04 Journey-level model

**Decision**: an ordered `ExperienceLevel { Beginner, Advanced, Expert }` in the user preferences
plus a **feature catalogue**: every level-dependent control group has an id (for example
`mixer.pan`), a minimum level and a help topic. Components wrap gated controls in
`<LevelGate Feature="mixer.pan">`; a cascading `ExperienceContext` gives the level to every
component. Aids (journey rail, inline hints, tour offers) are catalogue entries too, but with a
maximum level (`mixer.presets` has none; `journey.rail` is Beginner only).

Level semantics:

| Level | Intent | Controls | Aids |
|---|---|---|---|
| Beginner | "I am new to this" | essentials only (catalogue) | journey rail, inline hints visible, tour offers, presets prominent, comfortable density |
| Advanced | "I know my way around" | exactly the 001-003 control set | hints on hover/focus only, presets in a menu |
| Expert | "Give me everything" | Advanced + numeric entry, dB readouts, job details, shortcut overview | compact density, shortcut hints (`kbd`) on buttons |

Rules: data, notices, limitations, errors and the user's own values are never hidden. A value set
at a higher level stays applied and visible as a read-only marker at a lower level (for example
"panned L 30"). The command palette and the shortcuts work at every level; Expert only advertises
them. The backend and audio engine do not know the level.

**Why Beginner as the initial level**: new users are the ones who never open settings; experienced
users switch with one click in the welcome panel or the top bar. The existing end-to-end suite
sets level Advanced through a Playwright storage state (task T011), so 1.2.0 ids behave as before.

**Alternatives rejected**: one adaptive UI that "learns" (unpredictable, hard to test); separate
"simple" and "pro" pages (duplicated screens, SIG duplication); hiding by CSS only (hidden
controls still in the tab order and in the accessibility tree; untestable in bUnit); a per-panel
"show more" for every group (too many toggles; one level switch plus help links is simpler).

## R-05 Where preferences live

**Decision**: browser local storage, key `stemstudio.preferences`, one JSON record with a schema
version (data-model.md 1), read and written through the `web-interop` module (R-09) behind
`IPreferencesStore` in C#. The `storage` event keeps tabs in sync. No server endpoint.

**Rationale**: single local user; the theme must be known before the first paint (an inline script
can read local storage synchronously, it cannot call the API); no migration, endpoint or backend
work; each Playwright test gets clean, controllable preferences through `storageState`.

**Alternatives rejected**: `GET/PUT /api/preferences` in SQLite (the API is a second source of truth
that the first paint cannot reach, plus a migration and contract bump for no user benefit today;
if the agent of feature 011 needs the level server-side, the UI passes it with each request, or
011 adds the endpoint then); a cookie (sent with every request including stems, 4 KB limit);
Blazor `ProtectedLocalStorage` (encrypted with data-protection keys that are not persisted in the
container, so values become unreadable after a restart; nothing here is sensitive).

## R-06 Help system

**Decision**: four layers over one content source.

| Layer | Implementation | Trigger |
|---|---|---|
| Tooltip | `HelpHint` component: Radzen tooltip with the topic `summary` (<= 140 characters) and a "Learn more" link | hover/focus on an element with `data-help-id`, or the small help button next to a label |
| Help panel | `HelpPanel` (right drawer, 400 px, non-modal on >= 1280 px where content reflows; overlay drawer below), renders the topic | help button in the top bar (same place on every screen, WCAG 3.2.6), `?` outside text fields, F1 |
| Library and search | index of all topics by area, glossary, search box; in-memory index built at start-up | inside the panel; also through the command palette |
| Tours | driver.js (R-07) | offer chip or the panel's tour list |

Context resolution: focused element -> closest ancestor with `data-help-id` -> its topic; else the
screen topic (`route` in the topic front matter). Search: tokenised, lower-cased, diacritics
folded; score = 5 x title hit + 3 x keyword hit + 2 x summary hit + 1 x body hit, prefix matches
count, ties by title; no library needed for about 60-120 topics.

Content: `src/StemStudio.Web/Help/topics/<area>/<id>.md` with YAML front matter
([contracts/help-content.md](contracts/help-content.md)), embedded as resources, parsed once at
start-up by `HelpCatalog` with YamlDotNet and rendered with Markdig (pipeline with `DisableHtml()`,
auto-identifiers, tables, task lists off). Internal links use `help:<topic-id>`; level notes use
the catalogue ("Available at Advanced level" with a switch button).

**Alternatives rejected**: help as Razor components (content mixed with code, not reviewable as
text, not reusable by the agent in 011); a separate docs site (leaves the app, not contextual);
a server help API (no need, content is static per build).

## R-07 Guided tours: driver.js

**Decision**: driver.js 1.10.2 (MIT; roadmap research 2.4, **verify** version and licence in T001 and T054),
wrapped by `web-interop/src/tour.ts`. Tours are JSON in `src/StemStudio.Web/Help/tours/<id>.json`
([contracts/help-content.md](contracts/help-content.md) and `tour.schema.json`); a step targets
`[data-help-id="<id>"]` and takes its text from the topic summary unless the step overrides it.
The wrapper: skips steps whose target is missing or hidden, sets `data-testid`s on the popover,
traps focus in the popover, restores focus on close, honours reduced motion (no animated
transitions), maps keys (arrows, Enter, Escape) and reports `completed`/`dismissed` back to .NET.
driver.js CSS is themed with our tokens.

**Alternatives rejected**: Shepherd.js 16 and Intro.js (AGPL-3.0 + commercial dual licence:
excluded, roadmap research 2.4); a home-made overlay (re-implementing positioning, scrolling and
highlight cut-outs is more code than the wrapper and a source of a11y bugs).

## R-08 How help content is versioned and tested

- Content lives next to the code and is reviewed in the same pull requests. Each topic carries
  `reviewed: "<feature>"` (e.g. `"004"`); each tour carries an integer `version`. Bumping a tour
  version re-offers it to users who completed an older version.
- Unit tests (`tests/StemStudio.Web.Tests/Help/`):
  - catalogue: every file parses, front matter validates against `help-topic.schema.json`, ids are
    unique and match the file name, summaries <= 140 characters, every `related` and `help:` link
    resolves, no raw HTML survives rendering, every area has an index topic;
  - markers: a source scan of `src/StemStudio.Web/**/*.razor` collects every literal
    `data-help-id` / `HelpId=` value; each must have a topic; each topic `anchors` entry must be
    found by the scan (dynamic ids are declared as patterns in front matter, e.g. `mixer-strip-*`);
  - tours: every tour validates against `tour.schema.json`, every step target is a known marker and
    is rendered at the tour's levels on its route (bUnit render of the page with fakes);
  - search: the 20 reference queries of quickstart V-H3 return the expected first topic.
- End-to-end (qa): open help per screen via button, `?` and F1; search; follow a related link; run
  each tour with the keyboard.
- Cross-cutting rule for features 005-011: a PR that adds a `data-help-id` without a topic fails
  CI (the marker test); new controls register in the feature catalogue.

## R-09 Where the browser-side code lives: a `web-interop` package

**Decision**: a new small TypeScript package `src/web-interop/` (`@stemstudio/web-interop`,
Vite library build to `src/StemStudio.Web/wwwroot/js/web-interop.js`, Vitest + jsdom, ESLint,
`tsc --noEmit`, coverage >= 90 %), owned by the frontend agent. Modules: `preferences.ts` (read,
write, validate, `storage` event), `theme.ts` (apply attributes, follow `prefers-color-scheme`),
`shortcuts.ts` (global key map with text-field guard), `tour.ts` (driver.js wrapper),
`focus.ts` (focus return helpers for the help panel and palette). The inline first-paint script
(R-10) is a separate, tiny, hand-written file `wwwroot/js/first-paint.js` tested in the same
package's test suite by importing its source. Contract: [contracts/web-interop.md](contracts/web-interop.md).

**Rationale**: driver.js must come from npm with a pinned version and lockfile (licence record,
reproducible); the constitution requires linted, tested TypeScript with coverage for JS; keeping
it out of `src/audio-engine` keeps the engine single-purpose (component balance, ADR-005).
The existing `mixer-keys.js` stays as is (space key); `shortcuts.ts` adds the other keys and the
two never handle the same key.

**Alternatives rejected**: vendoring driver.js's built file into `wwwroot/lib` (no lockfile, no
tests for the wrapper); adding the modules to the audio-engine package (mixes UI concerns into the
engine and its owner's lane); plain JS files like `mixer-keys.js` (untested, no types).

## R-10 First paint without a theme flash

**Decision**: `App.razor` gets `<script src="js/first-paint.js"></script>` as the first element in
`<head>` (synchronous, < 1 KB, no dependencies). It reads `stemstudio.preferences`, resolves
`theme` (`system` -> `matchMedia('(prefers-color-scheme: dark)')`), and sets `data-theme`,
`data-density` and `data-motion` on `<html>` before any stylesheet applies. On any error it sets
the defaults. The `html` and `body` background come from tokens, so the very first frame has the
right colour. The Blazor layout renders the shell immediately and the content area once
preferences are loaded through interop (one call right after the circuit starts); a skeleton of
the content area keeps layout shift at zero.

**Alternatives rejected**: theme from the server (the server does not know local storage); CSS
`prefers-color-scheme` only (ignores an explicit Light/Dark choice).

## R-11 Test tooling for design quality

| Need | Tool | Licence | Owner |
|---|---|---|---|
| Visual regression | Playwright `toHaveScreenshot` (already used: @playwright/test 1.65.1), run in the Playwright Linux container image of the same version so rendering is deterministic; animations disabled, caret hidden, dynamic regions (meters, clock, GUIDs) masked | Apache-2.0 | qa (specs), devops (CI job) |
| Accessibility scan | `@axe-core/playwright` (**verify** current version) with tags `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`, `wcag22aa` | MPL-2.0 (OSI) | qa |
| Performance budget | Lighthouse CI `@lhci/cli` (**verify** version) against the fake stack, desktop preset, assertions from SC-003 | Apache-2.0 | qa (config), devops (job) |
| Token contrast | unit test over `tokens.css` computing WCAG ratios for the pairs listed in design-spec.md 2.1 | n/a | frontend |
| Raw colour guard | `tools/quality/css_tokens_check.py`: no hex/rgb/hsl literals outside `wwwroot/css/tokens.css` | n/a | quality-gate |
| Component behaviour | bUnit (levels, gates, help, palette) | MIT | frontend |

Visual baselines are generated only in the CI container (never on Windows), committed under
`e2e/visual/__screenshots__/`, and the first set is approved by the owner (CTO task).

## R-12 Payload estimate (to be measured in T063)

| Asset | Estimate (compressed) |
|---|---|
| Archivo variable woff2, Latin subset | 90-110 KB (**verify**) |
| tokens + Radzen map + component CSS | 15-25 KB |
| `web-interop.js` incl. driver.js | 10-15 KB |
| driver.js CSS | 1-2 KB |
| Help topics (embedded, served as rendered HTML only when opened) | 0 KB on first paint |

Total within the SC-003 budget of 200 KB. The Radzen `material` theme CSS currently loaded is
replaced by the `software` base, so the net change is smaller.

## R-13 Risks

| Risk | Mitigation |
|---|---|
| `software` base hard-codes colours | T012 spike, ADR-013 D1 fallback to compiled SCSS |
| Visual baselines flaky across machines | CI container only, fonts self-hosted, animations off, masks; threshold 0.1 % |
| Beginner gating breaks existing E2E | existing suite runs at Advanced via storage state (T011); 1.2.0 ids unchanged at Advanced |
| Key conflicts with browser or screen readers | no single-letter shortcuts inside text fields or while a screen reader virtual cursor is in a form (guard by focused element); `?`/F1 only outside inputs; every shortcut also has a visible control |
| Help content drifts from the UI | marker/anchor/tour tests fail the build (R-08) |
| Tour library a11y gaps | wrapper adds focus trap/return and labels; tour content is also readable in the help panel |

## R-14 Phase 0 verification of versions and licences (T001-T003, 2026-11-15)

Rule: exact pins, each version published at least two weeks before pinning (before 2026-11-01).

| Dependency | Planned | Pinned / verified | Licence (source checked) | Note |
|---|---|---|---|---|
| driver.js (npm, runtime of `src/web-interop`) | 1.14.0 | **1.10.2** (2026-10-18) | MIT: `license` file in the npm tarball (Copyright Kamran Ahmed), `package.json` `license: MIT`, no dependencies | 1.14.0 was published 2026-11-13 (2 days old); 1.10.2 is the newest version older than two weeks. The API the wrapper needs (`driver()`, `popoverClass`, `onPopoverRender`, `onDestroyed`, `animate`, `allowKeyboardControl`) is in its `driver.js.d.ts`. Repository now `github.com/nilbuild/driver.js` |
| Markdig (NuGet) | 1.6.0 | **1.5.1** (2026-10-28) | BSD-2-Clause: nuspec `licenseExpression` and `license.txt` at the packaged commit `bbf197e` of `github.com/xoofx/markdig` | 1.6.0 was published 2026-11-04 (11 days); net10.0 asset, no dependencies |
| YamlDotNet (NuGet) | 18.1.0 | 18.1.0 (2026-06-26), now also runtime | MIT | unchanged pin |
| Archivo (font, frontend T020) | variable, OFL | `google/fonts` `ofl/archivo/Archivo[wdth,wght].ttf` (+ Italic), upstream `Omnibus-Type/Archivo` commit `b5d6398` (2021-02-01) | SIL OFL-1.1: `OFL.txt` in both repositories | axes `wght` 100-900, `wdth` 62-125; frontend subsets to a Latin woff2 and records the SHA-256 |
| `@axe-core/playwright` (e2e dev, qa) | verify | **4.14.0** (2026-10-15); pulls `axe-core ~4.14.0` (4.14.0, 2026-10-05) | MPL-2.0: `LICENSE` in both tarballs (axe-core also ships `LICENSE-3RD-PARTY.txt`, MIT parts) | peer `playwright-core >= 1.0.0` |
| `@lhci/cli` (e2e dev, qa) | verify | **0.15.1** (2025-06-25) | Apache-2.0: `LICENSE` in the tarball | 0.16.0 (2026-11-02) and 0.16.1 are less than two weeks old |

`src/web-interop/package-lock.json` was seeded from `src/audio-engine/package-lock.json`, so the
dev tree (228 packages) is identical to the audio engine's; the only addition is driver.js.
