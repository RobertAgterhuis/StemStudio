# Implementation Plan: Design System and UX Foundation

**Branch**: `004-design-system-ux` | **Date**: 2026-11-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/004-design-system-ux/spec.md`; constitution 1.2.0;
research `docs/research/roadmap-003-011.md` section 2 (called "003" there); the current Blazor UI
(`src/StemStudio.Web`).

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). Binding decisions are in
ADR-013 (`docs/decisions/ADR-013-design-system-and-ux-foundation.md`); reasoning in
[research.md](research.md); the visual system in [design-spec.md](design-spec.md).

## Summary

StemStudio gets its own design system and a level-aware user journey, entirely inside the web
front end. A token layer (`tokens.css` + `radzen-map.css`) over the free MIT Radzen `software`
base theme provides a dark and a light theme in the direction "Colour belongs to the music":
graphite/paper chrome, six fixed stem colours, Archivo as the single variable type family, and the
"one strand becomes six" split as the signature. A new app shell (left rail, context bar, help
panel, command palette) hosts every existing screen, rebuilt with the new components and designed
states. Three experience levels (Beginner, Advanced, Expert) gate controls through one feature
catalogue; Advanced is exactly today's control set, so every 1.2.0 test id keeps working. In-app
help (tooltips, contextual panel, searchable library with glossary, driver.js tours) is driven by
Markdown topics and JSON tours embedded in the web app and guarded by automated coverage checks.
Preferences live in local storage and are applied before the first paint by a tiny inline script.
No backend, worker or audio-engine change.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (Blazor Web App, Interactive Server, prerender off, as
today); TypeScript 6.0 for the new `src/web-interop` package; CSS custom properties.

**Primary Dependencies (new)**: NuGet `Markdig` 1.5.1 (BSD-2-Clause) and `YamlDotNet` (MIT,
already in `Directory.Packages.props` at 18.1.0, now also referenced by `StemStudio.Web`); npm
(`src/web-interop`) `driver.js` 1.10.2 (MIT) runtime, dev: `vite`, `vitest`, `@vitest/coverage-v8`,
`jsdom`, `eslint`, `typescript`, `typescript-eslint`, `eslint-plugin-jsdoc` at the versions already
pinned in `src/audio-engine/package.json`; e2e dev: `@axe-core/playwright` (MPL-2.0) and
`@lhci/cli` (Apache-2.0) (**verify** versions when added); assets: Archivo variable woff2
(SIL OFL-1.1). Material Symbols (Apache-2.0) as already shipped by Radzen. Versions and licences
are re-verified by the adding agent (constitution IV) and listed in its hand-off.

**Storage**: browser local storage `stemstudio.preferences` (data-model.md 1). No database change.

**Testing**: bUnit + xUnit v3 for components, catalogue, help and preferences state; Vitest +
jsdom for `web-interop` (driver.js faked); Playwright for E2E (journeys, help, tours, keyboard),
visual regression (`toHaveScreenshot` in the Playwright Linux container), accessibility
(`@axe-core/playwright`) and Lighthouse CI budgets; Python `tools/quality` checks for raw colours.

**Target Platform**: as today (Chromium and Firefox desktop; Playwright projects `chromium`,
`firefox`); layouts from 360 px (mixer from 768 px).

**Performance Goals**: SC-003 (FCP <= 1.0 s, LCP <= 1.5 s, CLS <= 0.05, +<= 200 KB compressed),
SC-010 (level/theme switch <= 200 ms), SC-011 (no style-caused long tasks while metering).

**Constraints**: every 1.2.0 test id unchanged at Advanced (R7); no premium Radzen asset; no runtime
third-party requests (fonts self-hosted); WCAG 2.2 AA; SIG limits for C# and TS.

**Scale/Scope**: 5 routed screens + 3 dialogs re-skinned; ~20 new components; ~60 help topics;
5 tours; 1 new TS package.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Every frontend task is a red/green pair (bUnit, Vitest); `src/web-interop` gets its own coverage gate (>= 90 %) like the audio engine; help content is guarded by tests (markers, anchors, links, tours, search); E2E for every story; visual and a11y suites. No real model or network in any test. | PASS |
| II | SIG 4 stars | Small components (one per concern), `FeatureCatalog` as data not branching, presets as a data list, help search as a small pure function; `tools/quality` extended to `src/web-interop` (TS unit size/complexity) and with the raw-colour guard. | PASS |
| III | Contracts first | UI test ids 1.3.0 (additive, rules R7-R9), help-content 1.0.0 (+ JSON schemas), web-interop 1.0.0 are written before the wave; backend 1.3.0, worker 1.2.0, engine 2.0.0 unchanged (DELTA.md). Public C# types and TS exports documented. | PASS |
| IV | Open source, lawful use | Markdig BSD-2, YamlDotNet MIT, driver.js MIT, Archivo OFL-1.1, Material Symbols Apache-2.0, axe MPL-2.0 (dev), Lighthouse CI Apache-2.0 (dev): all OSI-approved. Radzen premium themes, swatches and Radzen Blazor Studio excluded; Shepherd.js/Intro.js (AGPL + commercial) excluded. Legal notice stays visible at every level. | PASS |
| V | Simplicity and honesty | Token layer instead of a compiled theme (fallback only on evidence); preferences client-side (no endpoint, no migration); one font family; one TS package for all browser glue; limitation notes visible at every level and explained in help. Audio timing untouched. | PASS |
| - | Tech & security | Stack unchanged (Blazor + Radzen); Markdown rendered with raw HTML disabled; no user input reaches the help renderer (content is shipped); local storage holds no secrets; no new network calls. | PASS |
| - | Workflow | Spec Kit flow; disjoint owner paths; checkpoint hand-offs (ADR-008); an owner design-approval checkpoint before the remaining screens are polished. | PASS |

**Post-design re-check (after Phase 1)**: PASS; refinements tracked in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/004-design-system-ux/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, design-spec.md, tasks.md
├── checklists/requirements.md
└── contracts/
    ├── DELTA.md                    # backend 1.3.0 / worker 1.2.0 / engine 2.0.0 unchanged
    ├── ui-test-ids.md              # 1.3.0 (full document; supersedes 003's 1.2.0)
    ├── help-content.md             # 1.0.0 (+ help-topic.schema.json, tour.schema.json)
    └── web-interop.md              # 1.0.0
```

### Source Code (additions and changes)

```text
src/web-interop/                                   # NEW TS package (skeleton: architect; code: frontend)
├── package.json, package-lock.json, tsconfig.json, vite.config.ts, vitest.config.ts, eslint.config.js
├── src/{types,index,preferences,theme,shortcuts,tour,focus}.ts
├── src/first-paint.ts                             # built to wwwroot/js/first-paint.js (classic script)
└── tests/*.test.ts

src/StemStudio.Web/
├── StemStudio.Web.csproj                          # + Markdig, YamlDotNet, EmbeddedResource Help/** (architect)
├── Components/App.razor                           # first-paint script, base theme software, token CSS, font preload
├── Components/Layout/{MainLayout,NavMenu}.razor   # -> AppShell, NavRail, ContextBar
├── Components/Shell/{CommandPalette,ShortcutOverlay,PreferencesMenu,LevelSwitch}.razor
├── Components/Design/{StemTab,StemRibbon,SplitProgress,EmptyState,ErrorState,Skeleton}.razor
├── Components/Experience/{LevelGate,WelcomePanel,JourneyRail}.razor
├── Components/Help/{HelpPanel,HelpHint,HelpTopicView,HelpSearchBox,TourOffer}.razor
├── Components/Pages/**, Components/Mixer/**, Components/Shared/**   # re-skinned, markers added
├── Components/Mixer/PracticePresets.razor, Components/Pages/JobDetails.razor
├── Experience/{ExperienceLevel,UiFeature,FeatureCatalog,ExperienceContext}.cs   # stubs: architect
├── Preferences/{UserPreferences,HelpProgress,TourRecord,IPreferencesStore,PreferencesState,LocalPreferencesStore}.cs
├── Help/{HelpTopic,TourDefinition,TourStep,IHelpCatalog,IHelpSearch,HelpCatalog,HelpSearch,HelpRenderer,HelpContext}.cs
├── Help/topics/<area>/<id>.md, Help/tours/<id>.json                 # content (frontend)
├── Interop/{IWebInterop,WebInterop}.cs
├── Mixer/{PracticePreset,PracticePresets,ChannelSelection}.cs
└── wwwroot/
    ├── css/{tokens,radzen-map,base}.css, css/components/*.css     # app.css split up and retired
    ├── fonts/archivo/{Archivo-Variable-latin.woff2,OFL.txt}
    └── js/{web-interop.js,first-paint.js}                         # build output of src/web-interop (git-ignored like audio-engine.js)

tests/StemStudio.Web.Tests/{Experience,Preferences,Help,Design,Shell,Components}/**   # frontend
e2e/{support/preferences.ts, tests/journey-*.spec.ts, tests/help.spec.ts, tests/tours.spec.ts,
     tests/expert-keyboard.spec.ts, visual/*.spec.ts, a11y/*.spec.ts, lighthouserc.json}  # qa
tools/quality/{css_tokens_check.py, dependency rules for src/web-interop, tests/}          # quality-gate
docker/web.Dockerfile (build src/web-interop), .github/workflows/ci.yml (web-interop, visual,
     a11y, lighthouse jobs), scripts/test-all.*, scripts/dev.*                            # devops
```

**Dependency direction**: Blazor components -> `Experience`, `Preferences`, `Help`, `Mixer` state ->
`Interop` -> `web-interop.js`; `Help` does not depend on components; `web-interop` imports only
`driver.js` (never `audio-engine`). Web -> API client unchanged.

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/004-*/**`, ADR-013, skeleton: `src/web-interop/{package.json,package-lock.json,configs,src/types.ts,src/index.ts}`, Web csproj + `Directory.Packages.props`, C# stubs in `Experience/`, `Preferences/`, `Help/`, `Interop/` (interfaces and records only) | architect |
| `src/StemStudio.Web/**` (everything else), `src/web-interop/src/**`, `src/web-interop/tests/**`, `tests/StemStudio.Web.Tests/**` | frontend |
| `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | qa |
| `docker/**`, `.github/**`, `scripts/**`, `docker-compose*.yml` | devops |
| `tools/quality/**`, `docs/quality/**` | quality-gate |
| `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, owner design reviews | CTO |

backend, audio-engine, stem-separation, audio-ingest and beat-analysis have no work in this feature.

## Key flows

1. **Load**: browser requests `/` -> `first-paint.js` sets `data-theme/density/motion/level` ->
   base theme + tokens paint the shell colours -> Blazor circuit starts -> `MainLayout` renders the
   shell with a content skeleton -> `PreferencesState.LoadAsync` (one interop call) -> content
   renders at the effective level; `watchPreferences` and `watchSystemAppearance` are registered.
2. **Level switch**: `LevelSwitch` -> `PreferencesState.SetLevelAsync` -> `Changed` -> the cascading
   `ExperienceContext` re-renders gated components; the mixer keeps `MixerState` and the engine
   untouched (only markup changes) -> debounced `writePreferences` -> other tabs receive `storage`.
3. **Help**: `?`/F1 (web-interop shortcut) or the help button -> `HelpContext.OpenAsync(null)` ->
   `focusedHelpId()` -> topic for that anchor, else the screen topic -> `HelpRenderer` (Markdig,
   cached) -> panel opens (docked >= 1280 px), focus moves to the panel heading; Escape -> close and
   `captureFocus` handle restores focus.
4. **Tour**: `TourOffer` (route + level + no newer record) -> user starts -> .NET resolves texts from
   topics -> `startTour` (driver.js) -> `OnTourEnded` -> `PreferencesState.RecordTourAsync`.
5. **Expert keyboard**: `registerShortcuts(scope)` -> command id -> the page's command handler (the
   same methods the buttons call) -> state change -> render.

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| New TS package `src/web-interop` (a seventh component) | driver.js from npm with a lockfile; tested, typed browser glue for preferences, first paint, shortcuts, tours, focus | plain JS files in `wwwroot` are untested (constitution I); adding UI glue to `src/audio-engine` mixes concerns and owners |
| Feature catalogue with min/max levels instead of only "min" | aids such as the journey rail and inline hints are Beginner-only | per-component `if` statements scatter level logic and cannot be listed for help or the agent in 011 |
| Visual regression baselines generated only in a Linux container | font rendering differs between Windows and Linux | local baselines would flap on every machine |
| Typeface differs from the roadmap research (Archivo instead of Inter + JetBrains Mono) | distinctiveness (owner requirement) and payload (one file) | Inter is the default face of many web apps; a monospace family is not needed with tabular figures |
