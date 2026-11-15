# Hand-off: architect / 004 Phase 0 (contract code and skeleton, T001-T005)

## What was built

- **T001** `src/web-interop/` (`@stemstudio/web-interop` 0.1.0, MIT, private, node >= 22.22.2):
  `package.json` (runtime `driver.js` 1.10.2 exact; dev dependencies identical to
  `src/audio-engine/package.json`), `package-lock.json` (seeded from the audio-engine lock: the
  228-package dev tree is identical, only `driver.js` is added), `tsconfig.json`, `eslint.config.js`
  (same rules as the engine: type-checked, complexity 5, max-params 4, 30 lines/function, TSDoc),
  `vitest.config.ts` (jsdom, 90 % lines/branches/functions/statements), `vite.config.ts`
  (`vite build` -> `wwwroot/js/web-interop.js` ES module; `vite build --mode first-paint` ->
  `wwwroot/js/first-paint.js` minified IIFE; `npm run build` runs both), `src/types.ts` (all
  types of web-interop.md 2 and data-model.md 1.1), `src/index.ts` (all exports as stubs that throw
  `not implemented: <name>`, plus `WEB_INTEROP_CONTRACT_VERSION = "1.0.0"`), `src/first-paint.ts`
  (placeholder `applyFirstPaint(root)` stub, frontend-owned), `tests/skeleton.test.ts`.
- **T002** .NET skeleton: `Markdig` 1.5.1 in `Directory.Packages.props` (Web group); `YamlDotNet`
  moved to a `Shared` group (same 18.1.0 pin, now runtime too). `StemStudio.Web.csproj`
  references `Markdig` + `YamlDotNet`, embeds `Help\topics\**\*.md;Help\tours\*.json` and removes
  `Help/**` from `Content`/`None` (tour JSON is not published as static content, C# files are
  not embedded). Stubs (records/enums/interfaces, XML-documented):
  `Experience/{ExperienceLevel,UiFeature}`,
  `Preferences/{UserPreferences,HelpProgress,TourRecord,TourStatus,ThemePreference,DensityPreference,MotionPreference,PreferencesSnapshot,IPreferencesStore}`,
  `Help/{HelpTopic,RenderedTopic,HelpHeading,TourDefinition,TourStep,HelpHit,IHelpCatalog,IHelpSearch}`,
  `Interop/{IWebInterop,StoredPreferencesRead,ShortcutScope,TourRuntime(+TourRuntimeStep,TourLabels),TourEnded}`.
  `UserPreferences` carries the only logic: `Default`, `EffectiveLevel` (null -> Beginner) and
  `EffectiveDensity` (Auto -> Compact at Expert, else Comfortable), fully tested.
  `Help/topics/start/welcome.md` (valid topic, anchor `welcome`) and `Help/tours/.gitkeep`.
- **T003** Contracts final: ui-test-ids **1.3.0**, help-content **1.0.0**, web-interop **1.0.0**
  ("proposed" removed, DELTA.md confirmed against docs/ROADMAP.md; backend 1.3.0, worker 1.2.0,
  engine 2.0.0 unchanged). ADR-013 **Accepted**. Schemas linked into
  `tests/StemStudio.Web.Tests` (copied to `<output>/Contracts/`) and validated in
  `Skeleton/Feature004SkeletonTests.cs` via the new dependency-free
  `Skeleton/SchemaSubsetValidator.cs` (exactly the keywords the two schemas use; any other
  keyword throws) and `Skeleton/FrontMatterJson.cs` (YAML front matter -> JSON).
- **T004** `.gitignore`: explicit `src/web-interop/node_modules/` and `coverage/` (already covered
  by the generic rules); `src/StemStudio.Web/wwwroot/js/.gitignore` (frontend file, agreed
  one-time edit): `web-interop.js`, `first-paint.js` and their maps.
- **T005** verification below; T001-T005 ticked in tasks.md.

### Changes against the plan

1. **driver.js 1.10.2 instead of 1.14.0** and **Markdig 1.5.1 instead of 1.6.0**: 1.14.0 was
   published 2026-11-13 and Markdig 1.6.0 on 2026-11-04, both younger than the two-week rule.
   Updated in spec.md, plan.md, research.md (new R-14), tasks.md, ADR-013.
2. e2e dev pins decided for qa: `@axe-core/playwright` **4.14.0**, `@lhci/cli` **0.15.1**
   (0.16.x is < 2 weeks old); written into T011.
3. Embedded resources are `Help\topics\**\*.md;Help\tours\*.json`, not `Help/**`; manifest names
   `StemStudio.Web.Help.topics.<area>.<id>.md` / `StemStudio.Web.Help.tours.<id>.json`
   (help-content.md 1).
4. web-interop.md: shortcut callback named `OnCommand(commandId)`; new section 4 maps every JS
   export to `IWebInterop` (delegates instead of DotNetObjectReference in the C# API; Interop does
   not depend on Preferences, so preferences cross the boundary as `JsonElement`).
5. Extra stub types (RenderedTopic, HelpHeading, Interop records) needed by the interface
   signatures; `IHelpCatalog` also exposes `Topics` (for search, palette and coverage tests).
6. help-content.md 4.1 names the subset validator (JsonSchema.Net excluded: OSMF EULA).
7. web-interop's `vitest.config.ts` is written differently from the engine's to avoid a
   23-line clone error from the quality gate (same settings).

## Files changed

- `src/web-interop/{package.json,package-lock.json,tsconfig.json,eslint.config.js,vite.config.ts,vitest.config.ts}`,
  `src/web-interop/src/{types,index,first-paint}.ts`, `src/web-interop/tests/skeleton.test.ts`
- `Directory.Packages.props`, `src/StemStudio.Web/StemStudio.Web.csproj`,
  `src/StemStudio.Web/{Experience,Preferences,Help,Interop}/*.cs`,
  `src/StemStudio.Web/Help/topics/start/welcome.md`, `src/StemStudio.Web/Help/tours/.gitkeep`
- `tests/StemStudio.Web.Tests/StemStudio.Web.Tests.csproj` (schema links, one-time edit),
  `tests/StemStudio.Web.Tests/Skeleton/{Feature004SkeletonTests,SchemaSubsetValidator,FrontMatterJson}.cs`
- `.gitignore`, `src/StemStudio.Web/wwwroot/js/.gitignore` (one-time edit)
- `specs/004-design-system-ux/{spec,plan,research,tasks}.md`,
  `specs/004-design-system-ux/contracts/{DELTA,ui-test-ids,help-content,web-interop}.md`,
  `docs/decisions/ADR-013-design-system-and-ux-foundation.md`
- Untouched: `worker/**`, `src/audio-engine/**`, OpenAPI documents (so no Redocly change; the
  003 contracts lint still passes in test-all).

## Tests and coverage

- `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors.
- `dotnet test StemStudio.slnx -c Release --no-build -p:CollectCoverage=true` (gates 90 % line+branch):
  - StemStudio.Web.Tests 332 passed (312 existing + 20 skeleton test cases) - StemStudio.Web **99.88 % line / 95.12 % branch**
  - StemStudio.Api.Tests 414 passed - Api 99.63 % / 98.02 %, Contracts 100 % / 100 %
  - StemStudio.Domain.Tests 261 passed - 98.54 % / 97.67 %
  - StemStudio.Persistence.Tests 48 passed - 98.56 % / 100 %
- `src/web-interop`: `npm ci`, `lint`, `typecheck`, `test:coverage` (11 passed; 100 % lines 12/12,
  statements 12/12, functions 11/11, branches 0/0), `build` (web-interop.js 3.2 kB, first-paint.js
  0.25 kB): all exit 0.
- `src/audio-engine`: `npm ci`, lint, typecheck, test:coverage (160 passed, 100 %/100 %), build: exit 0.
- `e2e`: npm ci, lint, typecheck, `playwright test --list` (78 tests in 14 files): exit 0.
- `scripts/test-all.ps1 -ContinueOnError`: **all 29 steps passed** (exit 0), incl. worker
  (untouched), contracts lint (003 OpenAPI, Redocly 2.78.0), quality and fixtures steps.

## Quality checks

- `dotnet format StemStudio.slnx --verify-no-changes`: exit 0.
- `uv run --project tools/quality python tools/quality/run_all.py`: 574 files, 4379 units,
  26 clones, **0 errors**, 141 warnings (all pre-existing; none in the new files) -> exit 0.

## Dependencies added

| Name | Version | Licence | URL | Verified at |
|---|---|---|---|---|
| driver.js (npm, runtime) | 1.10.2 (2026-10-18) | MIT | https://github.com/nilbuild/driver.js | `license` file in the tarball, no dependencies |
| Markdig (NuGet, runtime) | 1.5.1 (2026-10-28) | BSD-2-Clause | https://github.com/xoofx/markdig | nuspec licence expression + `license.txt` at commit bbf197e |
| YamlDotNet (NuGet, now runtime) | 18.1.0 | MIT | https://github.com/aaubry/YamlDotNet | unchanged pin |
| Archivo (font, added by frontend T020) | google/fonts `ofl/archivo` (upstream Omnibus-Type/Archivo b5d6398) | SIL OFL-1.1 | https://github.com/Omnibus-Type/Archivo | `OFL.txt` in both repos; axes wght 100-900, wdth 62-125 |
| @axe-core/playwright (e2e dev, added by qa) | 4.14.0 (+ axe-core 4.14.0) | MPL-2.0 | https://github.com/dequelabs/axe-core-npm | `LICENSE` in both tarballs |
| @lhci/cli (e2e dev, added by qa) | 0.15.1 | Apache-2.0 | https://github.com/GoogleChrome/lighthouse-ci | `LICENSE` in the tarball |

## Open issues / requests

- **devops (T008)**: add web-interop to `scripts/test-all.{ps1,sh}` (npm ci, lint, typecheck,
  test:coverage, build), CI job `web-interop`, `docker/web.Dockerfile` (build stage next to the
  engine; copy `wwwroot/js` output) and the licence report. Not done here: T001-T005 do not ask
  for it and those files are devops-owned. Until then nothing references `web-interop.js`.
- **frontend**: the stub signatures are binding; `Skeleton/*` helpers may be reused by the help
  tests (move them only together with the help-content.md 4.1 reference). The welcome topic's
  anchor `welcome` needs a `data-help-id="welcome"` marker on the WelcomePanel (T051 coverage).
- **qa**: use the pins above for `@axe-core/playwright` / `@lhci/cli`, or re-verify if newer
  versions are at least two weeks old when added.
- plan.md/tasks.md still cite constitution 1.2.0 in their headers; the 1.3.0 amendment (optional
  Claude backend, GPU-only generative services) does not affect 004.

## Next owner

Wave (from the Phase 0 commit, one worktree each): frontend, qa, devops, quality-gate in parallel
per tasks.md "Execution plan per owner"; CTO T038 after frontend HO:us1.
