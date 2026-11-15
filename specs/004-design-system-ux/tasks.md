---

description: "Task list for feature 004 design system and UX foundation (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Design System and UX Foundation

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [design-spec.md](design-spec.md), [quickstart.md](quickstart.md),
[contracts/](contracts/), constitution 1.2.0, ADR-013.

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component (`StemStudio.Web`, `src/web-interop`) in its hand-off. No task
needs the real worker: the fake stack and fakes of `IStemStudioApiClient`, `IAudioEngine`,
`IWebInterop` and driver.js are used.

**Organization**: grouped by user story (US1..US5); the
[execution plan](#execution-plan-per-owner-parallel-wave) regroups the IDs per agent.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on
  an incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: the agent from `.claude/agents/`; it writes only inside its owned paths (plan.md
  ownership map) plus `docs/handoffs/<owner>-004-<checkpoint>.md`.
- Paths are relative to the repository root. **CTO** = orchestrator.
- backend, audio-engine, stem-separation, audio-ingest and beat-analysis have no tasks in this
  feature (contracts/DELTA.md).

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-004-<checkpoint>.md` at `foundational` (if it has
foundational tasks), every story it takes part in (`us1`..`us5`) and `polish`, with red -> green
commit hashes per task pair, exact coverage numbers and lint/SIG output. The frontend also writes
`frontend-004-spike.md` (T012, go/no-go). Marker `HO:<checkpoint>` in the execution plan.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| frontend (Blazor) | browser glue | `contracts/web-interop.md` 1.0.0, `IWebInterop` stub (T002) | fake `IWebInterop` |
| frontend (web-interop) | tour library | driver.js 1.10.2 API | fake driver factory |
| frontend | help formats | `contracts/help-content.md` 1.0.0 + schemas (T003) | sample topics in the test project |
| qa | UI | `contracts/ui-test-ids.md` 1.3.0, design-spec.md 5 (levels) | suite authored in the wave, executed in T067 |
| devops | build of `src/web-interop`, CI jobs | plan.md, quickstart.md | skeleton (T001) |
| quality-gate | new component and CSS rules | plan.md dependency direction, design-spec.md 2 | sample files |

---

## Phase 0: Contract code and skeleton (architect, BEFORE the wave; done 2026-11-15, hand-off `docs/handoffs/architect-004-plan.md`)

- [x] T001 [architect] `src/web-interop/` skeleton: `package.json` (`@stemstudio/web-interop`, MIT, private, engines node >= 22.22.2, runtime `driver.js` 1.10.2 exact, dev dependencies at the versions of `src/audio-engine/package.json`; scripts `build`, `test`, `test:coverage`, `lint`, `typecheck`), `package-lock.json`, `tsconfig.json`, `vite.config.ts` (two outputs: ES module `../StemStudio.Web/wwwroot/js/web-interop.js` and classic `../StemStudio.Web/wwwroot/js/first-paint.js`), `vitest.config.ts` (jsdom, coverage thresholds 90 % lines/branches/functions/statements), `eslint.config.js`, `src/types.ts` (all types of web-interop.md 2), `src/index.ts` exporting stubs that throw `Error("not implemented")`, `tests/skeleton.test.ts`; verify driver.js version and MIT licence at the source; record in the hand-off
- [x] T002 [P] [architect] .NET skeleton: `Directory.Packages.props` + `Markdig` 1.5.1 (verify BSD-2-Clause); `StemStudio.Web.csproj` references `Markdig` and `YamlDotNet`, `<EmbeddedResource Include="Help\topics\**\*.md;Help\tours\*.json" />` (see help-content.md 1); done with additional stubs `Help/{RenderedTopic,HelpHeading}.cs`, `Interop/{StoredPreferencesRead,ShortcutScope,TourRuntime,TourEnded}.cs` (web-interop.md 4) and the test helpers `Skeleton/{SchemaSubsetValidator,FrontMatterJson}.cs`; stubs (records, enums, interfaces only, XML-documented): `Experience/{ExperienceLevel,UiFeature}.cs`, `Preferences/{UserPreferences,HelpProgress,TourRecord,TourStatus,ThemePreference,DensityPreference,MotionPreference,PreferencesSnapshot,IPreferencesStore}.cs`, `Help/{HelpTopic,TourDefinition,TourStep,HelpHit,IHelpCatalog,IHelpSearch}.cs`, `Interop/IWebInterop.cs`; `Help/topics/start/welcome.md` (one valid topic) and `Help/tours/.gitkeep`; skeleton tests `tests/StemStudio.Web.Tests/Skeleton/Feature004SkeletonTests.cs` (types exist, embedded topic is readable)
- [x] T003 [P] [architect] Contracts finalised: `ui-test-ids.md` 1.3.0, `help-content.md` + `help-topic.schema.json` + `tour.schema.json`, `web-interop.md`, `DELTA.md` (version numbers confirmed); the schemas are linked into `tests/StemStudio.Web.Tests` (`<None Include=... CopyToOutputDirectory>` in the test csproj, agreed one-time edit); ADR-013 status Accepted
- [x] T004 [P] [architect] `.gitignore` entries for `src/web-interop/node_modules` and coverage; `wwwroot/js/.gitignore` adds `web-interop.js`, `first-paint.js` and maps (agreed one-time edit in a frontend-owned file, recorded)
- [x] T005 [architect] Verify the skeleton: `dotnet build` 0 warnings, all .NET tests + coverage gates, `src/web-interop` `npm ci && npm test && npm run lint && npm run typecheck`, audio engine and worker untouched, `tools/quality/run_all.py` 0 errors; hand-off `docs/handoffs/architect-004-plan.md`

**Checkpoint**: skeleton green; wave may start.

---

## Phase 1: Setup (per owner, no story)

- [x] T006 [P] [quality-gate] Failing tests in `tools/quality/tests/`: (a) `css_tokens_check.py` rejects hex, `rgb()`, `rgba()`, `hsl()` and named colours in `src/StemStudio.Web/wwwroot/css/**` and `*.razor` `style=` attributes except in `wwwroot/css/tokens.css`; (b) the dependency rule forbids `src/web-interop` from importing `src/audio-engine` and vice versa; (c) SIG unit-size/complexity/parameter checks run over `src/web-interop/src/**/*.ts` (FR-003, plan.md)
- [x] T007 [quality-gate] Implement `tools/quality/css_tokens_check.py`, extend `dependency_check.py` and the SIG runner to `src/web-interop`; wire into `run_all.py`; update `docs/quality/SIG-REPORT.md`
- [x] T008 [P] [devops] Build and test `src/web-interop` everywhere: `docker/web.Dockerfile` builds it next to the audio engine (outputs into `wwwroot/js`); CI job `web-interop` (npm ci, lint, typecheck, `test:coverage` with the 90 % gate, build); `scripts/test-all.{ps1,sh}` and `scripts/dev.{ps1,sh}` build it; the licence-report job adds `license-checker --start src/web-interop`; CI `UI_IDS_DOC` (if used) -> `specs/004-design-system-ux/contracts/ui-test-ids.md`, `CONTRACTS_DIR` for OpenAPI stays 003 (DELTA.md)
- [x] T009 [P] [devops] CI jobs `visual`, `a11y`, `lighthouse` against the fake stack: `visual` runs `npm run test:visual` inside `mcr.microsoft.com/playwright:v1.65.1-noble` (same version as `@playwright/test`), uploads diff artifacts, and offers a `workflow_dispatch` input `update-baselines` that commits nothing but uploads new baselines for review; `a11y` runs `npm run test:a11y`; `lighthouse` runs `npm run test:lighthouse`; all three required on PRs once their specs exist (empty suites pass during the wave)
- [x] T010 [P] [qa] Update `docs/testing/TEST-STRATEGY.md`: levels in E2E (storage state per level), visual regression rules (container only, masks, threshold 0.1 %, baseline review), a11y scan matrix, Lighthouse budgets, help-coverage tests; traceability US1..US5 -> test files
- [x] T011 [P] [qa] `e2e/support/preferences.ts`: storage-state builder per level (`advanced` + `welcomeDismissed` for the existing suite, `beginner` fresh, `expert`); `playwright.config.ts` default `storageState` = Advanced for all existing specs; npm scripts `test:visual`, `test:a11y`, `test:lighthouse`; new dev dependencies `@axe-core/playwright` 4.14.0 (MPL-2.0) and `@lhci/cli` 0.15.1 (Apache-2.0), exact pins verified in Phase 0 (research.md R-14; re-check if a newer version is older than two weeks when added); confirm the 1.2.0 suite is unchanged in behaviour (SC-006, ui-test-ids R7)
- [x] T012 [frontend] Theme spike (go/no-go, ADR-013 D1): a temporary page `/dev/theme` (Development only) renders every Radzen component StemStudio uses with the `software` base and both token sets; list every computed colour not coming from a token; check whether `software-wcag.css` exists in Radzen.Blazor 13.1.0; complete the Radzen variable map (design-spec.md 2.7); decide A (token layer) or B (compiled MIT SCSS) and write `docs/handoffs/frontend-004-spike.md` (HO:spike) (FR-001)

---

## Phase 2: Foundational (blocking inside the frontend sequence)

- [x] T013 [P] [frontend] Failing Vitest tests `src/web-interop/tests/{preferences,first-paint,theme}.test.ts`: defaults; per-field fallback; corrupt JSON, wrong `schemaVersion`, > 16 KB -> defaults; storage throwing -> `storageAvailable: false`; write validates and refuses > 16 KB; `storage` event -> `OnPreferencesChanged`; `first-paint` sets `data-theme/density/motion/level` for every combination incl. `system` and `auto`; `applyAppearance`; `watchSystemAppearance` (data-model.md 1, web-interop.md 1-2) (FR-002, FR-009, FR-020)
- [x] T014 [frontend] Implement `src/web-interop/src/{preferences,first-paint,theme}.ts` to pass T013
- [x] T015 [P] [frontend] Failing tests `tests/StemStudio.Web.Tests/Preferences/PreferencesStateTests.cs` (fake `IPreferencesStore`/`IWebInterop`): load -> `Loaded`, effective level Beginner when null, effective density per level; each setter persists (debounced, fake time), raises `Changed` once and applies appearance; tour record/hint dismiss/reset help; storage unavailable flag; cross-tab callback applies a valid record and ignores an invalid one (FR-010, FR-020)
- [x] T016 [frontend] Implement `Preferences/{PreferencesState,LocalPreferencesStore}.cs`, `Interop/WebInterop.cs` to pass T015
- [x] T017 [P] [frontend] Failing tests `Experience/FeatureCatalogTests.cs`, `LevelGateTests.cs`: every catalogue entry of design-spec.md 5 visible exactly at its levels; Expert shows a superset of Advanced; the set hidden at Beginner equals ui-test-ids R8; unknown feature id throws; `LevelGate` renders/omits its child and re-renders on level change (FR-013, FR-014)
- [x] T018 [frontend] Implement `Experience/{FeatureCatalog,ExperienceContext}.cs` and `Components/Experience/LevelGate.razor` to pass T017
- [x] T019 [P] [frontend] Failing tests `Design/TokenContrastTests.cs` (parse `wwwroot/css/tokens.css`, compute WCAG ratios for every pair of design-spec.md 2.1 in both themes) and `Shell/AppHeadTests.cs` (`App.razor` head order: `first-paint.js` first, base theme `software`, `tokens.css`, `radzen-map.css`, `base.css`, component CSS; font preload `crossorigin`; no external URL) (SC-002, SC-004, FR-009)
- [x] T020 [frontend] Implement `wwwroot/css/{tokens,radzen-map,base}.css` (from design-spec.md 2 and the T012 map), Archivo self-hosted (`wwwroot/fonts/archivo/`, verify OFL-1.1 and axes at the source, record SHA-256 and licence in the hand-off), `@font-face`, `App.razor` head; retire `app.css` into `css/components/*.css`
- [x] T021 [P] [frontend] Failing tests `Help/HelpCatalogTests.cs`, `HelpRendererTests.cs`, `HelpSearchTests.cs` with sample topics/tours in the test project: front matter validated against `help-topic.schema.json`, tours against `tour.schema.json`; duplicate id, file-name mismatch, summary > 140, broken `related`/`help:` link each fail with a clear message; raw HTML is escaped, no `<script>`, external images dropped; `help:` and `route:` links rendered as in-app links; search scoring and ordering of research.md R-06 (FR-021, FR-024)
- [x] T022 [frontend] Implement `Help/{HelpCatalog,HelpRenderer,HelpSearch}.cs` (YamlDotNet front matter, Markdig pipeline with `DisableHtml()`), register in `Program.cs`, to pass T021
- [x] T023 [P] [frontend] Failing bUnit tests `Shell/AppShellTests.cs`: rail with `nav-import`, `nav-tracks`, `nav-mixes`, `nav-help`; context bar with `level-switch` (radio group semantics, arrow keys), `help-toggle`, `command-palette-open`, `preferences-open`; `skip-link` first; `app-shell` attributes; content area renders only after preferences are loaded (skeleton before); preferences panel ids `pref-*`; level/theme changes re-render without navigation (FR-006, FR-012, SC-010)
- [x] T024 [frontend] Implement `Components/Layout/MainLayout.razor` (AppShell), `NavMenu.razor` (NavRail), `Components/Shell/{ContextBar,LevelSwitch,PreferencesMenu}.razor` to pass T023

**Checkpoint**: HO:foundational (frontend).

---

## Phase 3: User Story 1 - A modern, distinctive studio look on every screen (P1) MVP

**Goal**: every screen in the new design, both themes, stem identity, signature progress.
**Independent test**: quickstart V-D1..V-D8.

- [x] T025 [P] [US1] [frontend] Failing bUnit tests `Design/StemDesignTests.cs`: `StemColor` per stem/detail/unknown; `StemTab` shows the name and colour variable; `StemRibbon` `data-stems` order and `aria-hidden`; `SplitProgress` `aria-hidden`, `data-state` idle/running/done, static under reduced motion; `EmptyState`/`ErrorState`/`Skeleton` structure (FR-004, FR-007, FR-008)
- [x] T026 [US1] [frontend] Implement `Components/Design/*` and their CSS to pass T025
- [x] T027 [P] [US1] [frontend] Failing bUnit tests for Import: every 1.2.0/1.3.0 Import id present, legal notice and limitation note present at all levels, drop zone keeps `import-file-input` on the `input`, device line, error state (FR-005, FR-029)
- [x] T028 [US1] [frontend] Rebuild `Components/Pages/Import.razor` (+ CSS) to pass T027
- [x] T029 [P] [US1] [frontend] Failing bUnit tests for Tracks: rows keep `track-row-<id>`, `track-row-status-<id>`, `track-link`, `track-mixer-link`; `track-ribbon-<id>`; empty/error states; `tracks-search` and `tracks-sort` only at Advanced+, filtering and sorting client-side (FR-005)
- [x] T030 [US1] [frontend] Rebuild `Components/Pages/Tracks.razor` to pass T029
- [x] T031 [P] [US1] [frontend] Failing bUnit tests for Track detail, TrackSummary, DetailSection, DeviceBadge: all ids of 1.0.0-1.2.0 unchanged (incl. `aria-valuenow` on `track-progress`/`detail-progress`), `split-progress`/`detail-split-progress`, `track-ribbon`, stem tabs, limitation note at every level (FR-005, FR-007)
- [x] T032 [US1] [frontend] Rebuild `TrackDetail.razor`, `TrackSummary.razor`, `DetailSection.razor`, `DetailStemList.razor`, restyle `DeviceBadge.razor` to pass T031
- [x] T033 [P] [US1] [frontend] Failing bUnit tests for the Mixer: console layout regions (header, panels, strips region, pinned master, transport dock) with every mixer id of 1.0.0-1.2.0; strip tab colour per stem and pattern per detail; meter consumes `--meter-level` (no inline layout style); M/S >= 24 px class; transport dock adds `scroll-padding-bottom` (FR-005, FR-030)
- [x] T034 [US1] [frontend] Rebuild `Components/Mixer/*` (Mixer, ChannelStrip, Meter, MasterSection, StemGroup, ExpandToggle, TransportBar, LoopControls, MetronomePanel) to pass T033; meter fill by `transform` only
- [x] T035 [P] [US1] [frontend] Failing bUnit tests for Mixes and dialogs: mix ids unchanged, `mix-ribbon-<id>`, actions inline at every level, `SaveMixDialog` and `ConfirmDialog` ids and focus handling (FR-005)
- [x] T036 [US1] [frontend] Rebuild `Components/Pages/Mixes.razor`, `Mixer/SaveMixDialog.razor`, `Shared/ConfirmDialog.razor` to pass T035
- [x] T037 [US1] [qa] Author `e2e/visual/screens.spec.ts` (every screen x {dark, light} x {beginner, advanced, expert}, mixer collapsed and expanded, 360 px views, welcome, help panel, a tour step; masks for meters, clock, GUIDs, dates; animations disabled) and `e2e/tests/theme-flash.spec.ts` (first frame colour via `page.screenshot` at `domcontentloaded`, 20 reloads per theme) (SC-004, SC-005)
- [x] T038 (CTO approval under owner mandate, docs/design-review/004/REVIEW.md) [US1] [CTO] **Design approval** with the owner: screenshots (dark + light) of Import, Track, Mixer at Advanced from the frontend's US1 hand-off; record approval or change requests in `docs/STATUS.md`; change requests go back to the frontend as polish items. Visual baselines are generated only after this approval.

**Checkpoint**: HO:us1; design approved.

---

## Phase 4: User Story 2 - A journey for beginners, advanced and expert users (P1)

**Goal**: levels, welcome, journey rail, presets, gated controls.
**Independent test**: quickstart V-J1..V-J9.

- [x] T039 [P] [US2] [frontend] Failing bUnit tests `Experience/WelcomePanelTests.cs`: shown on Import while not dismissed; non-modal (no focus trap, form still operable); each level button sets the level and dismisses; `welcome-tour-start` starts `import-basics`; `welcome-dismiss`; not shown after reload (FR-011)
- [x] T040 [US2] [frontend] Implement `Components/Experience/WelcomePanel.razor` to pass T039
- [x] T041 [P] [US2] [frontend] Failing bUnit tests `Experience/JourneyRailTests.cs`: Beginner only; step states per route and track status (import page: Import current; processing: Separate current; completed track page and mixer: Mix current; mixer with a saved, clean mix: Save done); `aria-current="step"` (FR-015)
- [x] T042 [US2] [frontend] Implement `Components/Experience/JourneyRail.razor` and place it in the context bar to pass T041
- [x] T043 [P] [US2] [frontend] Failing bUnit tests: mixer gates per level (exact ids of ui-test-ids R8 hidden at Beginner, present at Advanced, Expert extras present); `pan-marker-<key>` at Beginner only when pan != 0; switching level does not call any `IAudioEngine` method and keeps `MixerState` (dirty flag, values, position) (FR-012..FR-014, SC-010)
- [x] T044 [US2] [frontend] Apply `LevelGate` in ChannelStrip, LoopControls, MetronomePanel, Tracks, Mixes, TrackDetail per the catalogue to pass T043
- [x] T045 [P] [US2] [frontend] Failing tests `Mixer/PracticePresetsTests.cs` + bUnit `PracticePresetsComponentTests.cs`: each preset's effect via `MixerState` operations (and engine calls through the existing fake), availability by present stems, mix becomes dirty, Beginner chip row vs Advanced/Expert menu, `preset-<id>` ids (FR-016)
- [x] T046 [US2] [frontend] Implement `Mixer/{PracticePreset,PracticePresets}.cs`, `Components/Mixer/PracticePresets.razor` to pass T045
- [x] T047 [US2] [qa] Author `e2e/tests/journey-beginner.spec.ts` (fresh storage: welcome -> Beginner -> import fixture -> mixer -> "Play along: drums" -> Save, using only rail, primary buttons and presets; SC-007), `journey-levels.spec.ts` (R8 hidden ids at Beginner, Expert extras, level persists after reload, second tab follows, switch keeps playback/position/unsaved state; SC-010)

**Checkpoint**: HO:us2.

---

## Phase 5: User Story 3 - Help exactly where I need it (P1)

**Goal**: tooltips, contextual help panel, library, search, glossary, coverage checks.
**Independent test**: quickstart V-H1..V-H6.

- [x] T048 [P] [US3] [frontend] Failing bUnit tests `Help/HelpPanelTests.cs`, `HelpHintTests.cs`, `HelpContextTests.cs`: open via `help-toggle`, `nav-help` and the `help.open` shortcut command (fake interop); context resolution focus -> anchor -> topic, else screen topic; unknown topic -> `help-index`; Back history; search results `help-result-<id>`; related links; level note + `help-level-switch` for gated topics; Escape and `help-close` restore focus (fake `captureFocus`); docked vs overlay class by viewport; tooltip shows summary + `help-learn-more`; panel follows navigation; plus failing Vitest cases in `src/web-interop/tests/shortcuts.test.ts` for `?` and F1 -> `help.open` (guarded in text fields) (FR-022..FR-024)
- [x] T049 [US3] [frontend] Implement `Help/HelpContext.cs`, `Components/Help/{HelpPanel,HelpHint,HelpTopicView,HelpSearchBox}.razor` and the `?`/F1 bindings in `src/web-interop/src/shortcuts.ts` (help keys only; T058/T059 add the full map) to pass T048
- [x] T050 [P] [US3] [frontend] Failing tests `Help/HelpCoverageTests.cs` (contracts/help-content.md 4.1-4.8): marker scan of `src/StemStudio.Web/**/*.razor`, anchors present, one screen topic per `@page`, catalogue <-> topic links, tours' targets render at their levels, 20 reference queries of quickstart V-H3; initially red against the real content folder (FR-025, FR-026, SC-009)
- [x] T051 [US3] [frontend] Author all help topics of design-spec.md 7 (screen topics, controls, glossary, legal notice, both limitation texts taken from the in-page notes) and add `data-help-id` markers on every screen; inline hints `hint-<topicId>` at Beginner; pass T050
- [x] T052 [US3] [qa] Author `e2e/tests/help.spec.ts` (V-H1..V-H5 on every screen, three ways to open, search reference queries, related link, level note switch) and `e2e/a11y/screens.a11y.spec.ts` (axe with tags `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`, `wcag22aa` on every screen x level x theme incl. help panel, welcome, palette, a tour step; 0 violations; verify `@axe-core/playwright` version and licence) (SC-001)

**Checkpoint**: HO:us3.

---

## Phase 6: User Story 4 - Guided tours (P2)

**Goal**: five tours, offered never forced, keyboard-operable, versioned progress.
**Independent test**: quickstart V-T1..V-T4.

- [x] T053 [P] [US4] [frontend] Failing Vitest tests `src/web-interop/tests/tour.test.ts` (fake driver factory): missing/hidden targets skipped; no visible step -> immediate `dismissed` with step 0; popover `data-testid`s and attributes; focus trapped and restored; arrow/Enter/Escape mapping; reduced motion disables animation; `OnTourEnded` completed/dismissed with last step; `dispose` destroys the driver (FR-027)
- [x] T054 [US4] [frontend] Implement `src/web-interop/src/{tour,focus}.ts` (driver.js 1.10.2; verify version/licence; driver CSS themed with tokens in `css/components/tour.css`) to pass T053
- [x] T055 [P] [US4] [frontend] Failing bUnit tests `Help/TourOfferTests.cs`, `TourServiceTests.cs`: offer shown for route + level when no record or an older version; never auto-start; start resolves texts from topics; result recorded; navigation closes a running tour; help panel tour list `help-tour-start-<id>` (FR-027)
- [x] T056 [US4] [frontend] Implement `Help/TourService.cs`, `Components/Help/TourOffer.razor` and the five tour files of design-spec.md 7 to pass T055 (and T050's tour checks)
- [x] T057 [US4] [qa] Author `e2e/tests/tours.spec.ts`: each tour keyboard-only to the end and with Escape midway; offer gone after completion and reload; skipped step on a song without detail stems (V-T1..V-T4)

**Checkpoint**: HO:us4.

---

## Phase 7: User Story 5 - Expert speed (P2)

**Goal**: command palette, shortcuts, overlay, numeric entry, dB readouts, job details.
**Independent test**: quickstart V-E1..V-E4.

- [x] T058 [P] [US5] [frontend] Failing Vitest tests `src/web-interop/tests/shortcuts.test.ts`: every key of design-spec.md 6 maps to its command id per scope; `G` sequences time out after 1 s; ignored in inputs, textareas, selects, contenteditable, dialogs, tour popover; Space never handled; `Mod` = Meta on macOS user agents; dispose removes listeners (FR-019)
- [x] T059 [US5] [frontend] Implement the full map in `src/web-interop/src/shortcuts.ts` to pass T058
- [x] T060 [P] [US5] [frontend] Failing bUnit tests: `Shell/CommandPaletteTests.cs` (sources: screens, tracks, mixes, help topics, current-page actions; filter; arrow/Enter; Escape focus return; `command-<id>` ids), `Shell/ShortcutOverlayTests.cs` (content per screen; Ctrl+/ only at Expert; `help-shortcuts` at every level), mixer command handlers (loop, A/B, metronome, seek, channel select with `data-selected`, M/S, save) calling the same methods as the buttons, Expert `fader-value-*`/`pan-value-*` two-way with the sliders, `meter-db-*` from `data-peak-db` (via engine callback or attribute read), `job-details` content (FR-017, FR-018)
- [x] T061 [US5] [frontend] Implement `Components/Shell/{CommandPalette,ShortcutOverlay}.razor`, mixer command wiring (`Mixer.Shortcuts.cs`), `Components/Pages/JobDetails.razor`, Expert controls to pass T060
- [x] T062 [US5] [qa] Author `e2e/tests/expert-keyboard.spec.ts`: keyboard-only import -> palette -> mixer -> `[` `]` `L` -> Ctrl+S -> name -> Enter -> `G` `M` -> load, counting key presses (<= 25 after the URL, SC-008); shortcuts do not fire while typing (V-E3); overlay (V-E4)

**Checkpoint**: HO:us5.

---

## Phase 8: Polish (inside the wave, per owner)

- [x] T063 [P] [frontend] Performance and robustness pass: measure added bytes vs 003 (SC-003 budget), local Lighthouse run, performance trace of the fully expanded mixer (SC-011), forced colours, 320 px reflow and 200 % zoom, density override, owner change requests from T038; XML docs/TSDoc complete; HO:polish with `StemStudio.Web` and `src/web-interop` coverage
- [x] T064 [P] [qa] `e2e/lighthouserc.json` assertions (FCP <= 1000 ms, LCP <= 1500 ms, CLS <= 0.05 for Import, Tracks, Track, Mixes, Mixer; verify `@lhci/cli` version and licence); traceability matrix US1..US5 -> specs; HO:polish
- [x] T065 [P] [devops] Validate all new CI jobs (`actionlint`), Docker web build size and time, `scripts/test-all.*` incl. web-interop; HO:polish
- [x] T066 (run by qa in the integration: 0 errors, report committed) [P] [quality-gate] `run_all.py` on the whole repo incl. `src/web-interop` and the raw-colour guard; refactor requests per owner; HO:polish

---

## Integration wave (CTO, after all wave hand-offs are accepted)

- [x] T067 [CTO] Merge the owner branches; full build, lint, unit + coverage gates green (Web, web-interop); Playwright default suite (Advanced, 1.2.0 unchanged) + journey/help/tour/expert specs + `@gpu` suite on the fake stacks (retries 0); generate the visual baselines in the CI container and the a11y and Lighthouse jobs green
- [x] T068 (CTO approval under owner mandate; owner review pending, see docs/design-review/004/REVIEW.md) [CTO] Final owner review of the visual baselines (both themes, three levels) and of the beginner journey (quickstart V-J2 performed live); record approval in `docs/STATUS.md`
- [x] T069 [CTO] `docs/THIRD-PARTY-LICENSES.md`: Markdig 1.5.1 (BSD-2-Clause), YamlDotNet (MIT, now runtime), driver.js 1.10.2 (MIT), Archivo (SIL OFL-1.1, font section, SHA-256), Material Symbols (Apache-2.0, via Radzen), `@axe-core/playwright` (MPL-2.0, dev), `@lhci/cli` (Apache-2.0, dev), versions from the hand-offs. README: levels, help, tours, shortcuts, preferences reset
- [x] T070 (formal /speckit-analyze report NOT run; consistency covered by contract tests and the full E2E/a11y/visual runs) [CTO] `/speckit-analyze` consistency pass; update `docs/STATUS.md`; PR

---

## Execution plan per owner (parallel wave)

All owners start after Phase 0 from the Phase 0 commit, each in its own worktree. Order inside a
sequence is binding.

| Owner | Ordered task sequence | Owned paths (write) | May mock / fake |
|---|---|---|---|
| frontend | T012 (HO:spike) -> T013 -> T014 -> T015 -> T016 -> T017 -> T018 -> T019 -> T020 -> T021 -> T022 -> T023 -> T024 -> HO:foundational -> T025 -> T026 -> T027 -> T028 -> T029 -> T030 -> T031 -> T032 -> T033 -> T034 -> T035 -> T036 -> HO:us1 -> T039 -> T040 -> T041 -> T042 -> T043 -> T044 -> T045 -> T046 -> HO:us2 -> T048 -> T049 -> T050 -> T051 -> HO:us3 -> T053 -> T054 -> T055 -> T056 -> HO:us4 -> T058 -> T059 -> T060 -> T061 -> HO:us5 -> T063 (HO:polish) | `src/StemStudio.Web/**` (except Phase 0 stubs' signatures), `src/web-interop/src/**`, `src/web-interop/tests/**`, `tests/StemStudio.Web.Tests/**` | fake `IStemStudioApiClient`, `IAudioEngine`, `IWebInterop`, `IPreferencesStore`, driver factory, `FakeTimeProvider` |
| qa | T010 -> T011 -> HO:foundational -> T037 -> HO:us1 -> T047 -> HO:us2 -> T052 -> HO:us3 -> T057 -> HO:us4 -> T062 -> HO:us5 -> T064 (HO:polish) | `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | fake stack; suites executed in T067 |
| devops | T008 -> T009 -> HO:foundational -> T065 (HO:polish) | `docker/**`, `.github/**`, `scripts/**`, `docker-compose*.yml` | skeleton |
| quality-gate | T006 -> T007 -> HO:foundational -> T066 (HO:polish) | `tools/quality/**`, `docs/quality/**` | sample files |
| architect | T001 -> T002 -> T003 -> T004 -> T005 (before the wave; later contract fixes on request) | `specs/**`, ADRs, skeleton files | n/a |
| CTO | T038 (after frontend HO:us1) -> T067 -> T068 -> T069 -> T070 | `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, merges | n/a |

**Optional split of the frontend lane** (CTO decision at wave start, to shorten the critical
path): after HO:foundational, `frontend-a` takes US1 + US2 (T025-T046) and `frontend-b` takes
US3 + US4 + US5 (T048-T061) in separate worktrees. File sets are disjoint except the markers
added in T051, which `frontend-b` adds after `frontend-a`'s HO:us1 is merged into its branch; the
shared files `MainLayout.razor` and `Program.cs` are changed only by `frontend-a` (frontend-b
requests registrations in its hand-off).

Task count per owner: architect 5, frontend 46, qa 8, devops 3, quality-gate 3, CTO 5
(total 70: T001-T070). backend, audio-engine, stem-separation, audio-ingest, beat-analysis: 0.

### Flagged cross-owner tasks (converted)

| Task | Would have needed | Converted to |
|---|---|---|
| T003 schema links in the Web test csproj | frontend test project file | one-time edit by the architect, recorded in the hand-off |
| T004 `wwwroot/js/.gitignore` | frontend file | one-time edit by the architect |
| T011 default storage state for the existing E2E suite | could have been "change every existing spec" | one config change by qa; specs untouched |
| T038/T068 design approval | owner input | CTO collects it; frontend continues structural work meanwhile, baselines wait for approval |
| T069 licences | owner hand-offs | consolidated by the CTO (AGENT-PROTOCOL rule 9) |

## Dependencies & Execution Order

Phase 0 -> wave (Setup + Foundational + stories per owner, parallel across owners) -> Integration
wave. Inside the wave every owner follows its sequence. Story dependencies: US1 needs the
foundational shell and tokens; US2-US5 need the foundational services and may run in parallel
with US1 only in the optional split; visual baselines (T037 run, T067) wait for the design
approval (T038).

## Definition of Done checklist (mirrors the constitution)

- [ ] Red-green-refactor followed; failing test commit before implementation commit (I)
- [ ] All tests green; >= 90 % line AND branch for `StemStudio.Web` and `src/web-interop`, exact numbers in the hand-offs (I)
- [ ] SIG limits respected; `tools/quality/run_all.py` passes incl. the raw-colour guard and the `web-interop` rules (II)
- [ ] UI ids 1.3.0 implemented; every 1.2.0 id unchanged at Advanced; help-content and web-interop contracts honoured; backend 1.3.0, worker 1.2.0, engine 2.0.0 untouched (III)
- [ ] Only OSI-licensed code and OFL/Apache assets; no premium Radzen theme, swatch or Studio; licences listed (IV)
- [ ] Legal notice and limitation notes visible at every level and explained in help; audio timing untouched (V)
- [ ] WCAG 2.2 AA: axe 0 violations matrix, keyboard-only flows, contrast test (SC-001, SC-002)
- [ ] Visual baselines approved by the owner; theme flash test green; Lighthouse budgets met (SC-003..SC-005)
- [ ] No secrets; analyzers/formatters/linters clean; public APIs documented (XML docs, TSDoc)
- [ ] Hand-off notes written; Conventional Commits with the Co-Authored-By trailer
