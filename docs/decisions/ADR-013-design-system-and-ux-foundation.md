# ADR-013: Design system, experience levels and in-app help (feature 004)

- **Status**: Proposed (architect, 2026-11-15); becomes Accepted in feature 004 Phase 0 (T003)
- **Context documents**: `specs/004-design-system-ux/{spec.md, research.md, design-spec.md,
  data-model.md, contracts/}`, `docs/research/roadmap-003-011.md` section 2, ADR-002 (layers),
  ADR-005 (audio engine), constitution 1.2.0

## Context

The owner asked (2026-11-15) for a modern, distinctive UI with an own Radzen theme, a user journey
that fits new, advanced and very experienced users, and in-app help. Today the UI uses the stock
Radzen `material` theme, a three-link header and one `app.css`. Constraints: constitution IV
(Radzen premium themes, swatches and Radzen Blazor Studio are paid and excluded; Shepherd.js and
Intro.js are AGPL + commercial and excluded), every existing UI test id must keep working, and no
backend/worker/engine change is wanted for a front-end feature.

## Decisions

### D1 Theme: token layer over the free `software` base

One free MIT base theme (`software`) loaded once through `RadzenTheme`; StemStudio tokens
(`wwwroot/css/tokens.css`, `--ss-*`) for a dark and a light set selected by
`html[data-theme]`, and `wwwroot/css/radzen-map.css` mapping the Radzen variables (`--rz-*`) onto
them. Light/dark is an attribute switch, not a stylesheet swap. A spike (task T012) verifies that
the base exposes enough variables; if not, the fallback is compiling Radzen's MIT SCSS
(`software-base.scss` and partials, notice kept) with Dart Sass into two theme files. No premium
theme, swatch or paid designer is used. Raw colour literals outside `tokens.css` fail CI.

### D2 Visual direction: "Colour belongs to the music"

Graphite-blue (dark) and cool-paper (light) chrome without a brand hue; six fixed stem identity
colours used everywhere a stem appears; ink-filled primary buttons; one amber focus colour; the
"one strand becomes six" split as the single signature motion; radius encodes containment;
Archivo (variable, OFL-1.1) as the only typeface with its width axis used deliberately; tabular
figures instead of a monospace face; Material Symbols as shipped by Radzen. Tokens and contrast
values: design-spec.md section 2. This replaces the roadmap suggestion Inter + JetBrains Mono
(Inter is a default face; a second family adds payload without need).

### D3 Experience levels with a feature catalogue

`ExperienceLevel { Beginner, Advanced, Expert }`; Beginner until chosen; one-click switch in the
app shell. A single `FeatureCatalog` (min/max level and help topic per control group) drives
`<LevelGate>`; levels change controls and aids only, never data, legal notices, limitation notes,
errors or user values. Advanced equals the 001-003 control set exactly, so every 1.2.0 test id is
unchanged at Advanced and the existing E2E suite runs at Advanced (ui-test-ids 1.3.0 R7-R9).
Beginner adds a journey rail, presets and inline hints; Expert adds numeric entry, dB readouts,
job details, a shortcut overview and compact density. The command palette and shortcuts work at
every level.

### D4 Preferences in browser local storage, no API

One versioned JSON record (`stemstudio.preferences`, data-model.md 1) for level, theme, density,
motion and help progress, accessed through the `web-interop` module and `IPreferencesStore`.
A synchronous inline `first-paint.js` applies theme/density/motion before the first paint (no
flash). Backend API stays 1.3.0; a server endpoint is deferred until a feature needs it
(contracts/DELTA.md).

### D5 Help: Markdown topics, contextual panel, search, driver.js tours

Help topics are Markdown files with YAML front matter and tours are JSON files, embedded in
`StemStudio.Web`, loaded once into `HelpCatalog`, rendered with Markdig (raw HTML disabled) and
searched with a small in-memory index. Four layers: tooltip (topic summary), contextual help panel
(help button in a fixed place, `?`, F1), library with glossary and search, and guided tours via
driver.js 1.14.0 (MIT) that are offered, never auto-started, and recorded per tour version.
Automated tests fail the build when a help marker has no topic, an anchor or link is broken, or a
tour step targets an unknown marker; later features must add topics and tours for their UI.

### D6 Browser glue in a new `src/web-interop` TypeScript package

Preferences, first paint, theme, shortcuts, tours and focus helpers live in
`@stemstudio/web-interop` (Vite build into `wwwroot/js`, Vitest + jsdom, >= 90 % coverage,
ESLint), owned by the frontend agent, separate from the audio engine (which stays single-purpose
and unchanged). The existing `mixer-keys.js` keeps the space key.

### D7 Design quality gates

Visual regression with Playwright screenshots generated only in the Playwright Linux container
(owner-approved baselines, 0.1 % pixel tolerance), axe scans for WCAG 2.2 AA on every screen x
level x theme, Lighthouse CI budgets (FCP <= 1.0 s, LCP <= 1.5 s, CLS <= 0.05, +<= 200 KB), a token
contrast unit test, and an owner design-approval checkpoint before baselines are generated.

## Consequences

- One new component (`src/web-interop`) with its own CI job and coverage gate; one new runtime
  NuGet (Markdig) and YamlDotNet becomes a runtime dependency of the web app; one npm runtime
  dependency (driver.js); one self-hosted font. All OSI/OFL/Apache.
- Every screen is rebuilt; the frontend lane is the critical path (46 of 70 tasks); the plan
  allows splitting it into two parallel frontend worktrees.
- Preferences do not follow the user across browsers or survive clearing site data (acceptable for
  a single local user; documented in the help topic `preferences`).
- Features 005-011 inherit the tokens, components, feature catalogue and help framework and must
  add help topics, catalogue entries and (where useful) a tour for their UI.

## Alternatives considered

Premium Radzen themes / Studio (paid); Radzen theme switching between `software` and
`software-dark` (two bases, flash on switch); Tailwind or another CSS framework next to Radzen (two
styling systems); server-side preferences endpoint (no first-paint access, extra migration and
contract); adaptive UI without explicit levels (unpredictable, untestable); Shepherd.js / Intro.js
(AGPL + commercial); help as Razor components (content mixed with code); vendored driver.js
without a lockfile; adding the browser glue to the audio-engine package (mixed concerns).
