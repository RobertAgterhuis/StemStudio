# Hand-off: frontend / 004 foundational (T012-T024)

## What was built

- **T012 spike** (`frontend-004-spike.md`): GO for ADR-013 D1 option A (token layer over the free MIT
  `software` base). `software-wcag.css` exists but only redefines root colours we override; `Wcag` off.
- **T013/T014 `src/web-interop`**: `preferences.ts` (defaults, field-by-field validation, 16 KB limit,
  storage-unavailable handling, `storage` event -> `OnPreferencesChanged`), `theme.ts` (resolve theme /
  density / motion / level against the OS, `applyAppearance` on `<html>` and on `[data-testid=app-shell]`,
  `watchSystemAppearance`, `applyFirstPaint`), `first-paint.ts` (IIFE entry, no exports, 1 020 bytes of
  code minified, under the 1 KB limit), `validation.ts` helpers.
- **T015/T016 Preferences**: `PreferencesState` (load once, immediate apply + one `Changed` per change,
  250 ms debounced save on `TimeProvider`, cross-tab apply with tour merge by newer `at`, OS change
  re-apply, no writes when storage is unavailable), `PreferencesJson` (record <-> `UserPreferences`,
  per-field defaults, `TryParse` for other tabs), `LocalPreferencesStore` (interop failure = unavailable
  storage), `Interop/WebInterop` (+ `InteropCallbacks` `[JSInvokable]` receiver, one
  `DotNetObjectReference` per registration, disconnect-safe dispose). Registered scoped in `Program.cs`.
- **T017/T018 Experience**: `FeatureCatalog` (the 15 entries of design-spec.md 5), `ExperienceContext`
  (cascaded record; `Default` = Advanced for isolated renders), `Components/Experience/LevelGate.razor`.
- **T019/T020 Design foundation**: `wwwroot/css/tokens.css` (both themes, density, motion, forced
  colours; the only file with literal colours), `radzen-map.css` (spike map), `base.css` (Archivo
  `@font-face`, typography scale, focus ring, controls, reduced motion), `css/components/{states,shell,
  track,mixer,dialog}.css` (app.css retired). `App.razor` head: `meta charset`, **`js/first-paint.js`
  first**, font preload (`crossorigin`), static `software.css` link (not `RadzenTheme`: its interactive
  render mode would add the theme only after the circuit starts and flash), tokens, map, base,
  components; no external URL.
- **T021/T022 Help**: `HelpFile`, `HelpContentException`, `TopicParser` (YamlDotNet, unknown keys fail),
  `TourParser` (System.Text.Json, unmapped members fail), `HelpCatalog` (embedded resources, duplicate /
  file name / folder / summary / level / related / `help:` link / tour topic checks, anchor lookup
  literal-before-pattern, render cache), `HelpRenderer` (Markdig: pipe tables, GitHub auto ids scoped as
  `help-<topic>-<id>`, `DisableHtml`, `help:` -> `/help/<id>` + `data-help-link`, `route:` -> app path,
  `https:` -> new tab `rel=noopener`, other schemes and non-local images unwrapped to text),
  `HelpSearch` (R-06 weights, half score for prefix matches, diacritic folding, ties by title),
  `HelpLinks`. Catalogue built eagerly at start-up (fails fast).
- **T023/T024 Shell**: `MainLayout` (app-shell with `data-level`/`data-density`, skip link, rail,
  context bar, content gated on loaded preferences with a skeleton, cascading `ExperienceContext`,
  toast region), `NavMenu` (rail with brand mark, `nav-*` ids, `nav-help`; bottom bar < 768 px),
  `Shell/{ContextBar,LevelSwitch,PreferencesMenu,SegmentedChoice,BrandMark,PreferencesAware}`,
  `Services/ToastService` (4 s, `TimeProvider`).

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T013 -> T014 | `3f2174b` (3 files fail to import) | `b937944` |
| T015 -> T016 | `56dde42` (50 failing) | `82f4aaa` |
| T017 -> T018 | `d09cc7b` (23 failing) | `e73e0ce` |
| T019 -> T020 | `142193d` (3 head tests failing; contrast tests pass by design against the then-untracked spike tokens) | `9ce73ee` |
| T021 -> T022 | `dc24dba` (51 failing) | `c2ea136` |
| T023 -> T024 | `c3e8a4d` (23 failing + 1 Vitest) | `5d6b563` |

T012 spike: `11fd86d`.

## Files changed

`src/web-interop/src/{validation,preferences,theme,first-paint,index}.ts`, `src/web-interop/tests/*`;
`src/StemStudio.Web/{Preferences,Interop,Experience,Help,Services}/*.cs` (new files listed above),
`Components/{App,_Imports}.razor`, `Components/Layout/{MainLayout.razor,MainLayout.razor.cs,NavMenu.razor}`,
`Components/Shell/*`, `Components/Experience/LevelGate.razor`, `Program.cs`, `wwwroot/css/**`,
`wwwroot/fonts/archivo/{Archivo-Variable-latin.woff2,OFL.txt}`; removed `wwwroot/app.css`;
tests under `tests/StemStudio.Web.Tests/{Preferences,Interop,Experience,Design,Shell,Help,Fakes}`
(`Layout/MainLayoutTests.cs` replaced by `Shell/AppShellTests.cs`); tasks.md T012-T024 ticked.

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
  Passed!  - Failed: 0, Passed: 501, Skipped: 0, Total: 501
  | StemStudio.Web | 99.16% | 92.04% | 99.57% |   (line | branch | method; gate 90 % line+branch: pass)
src/web-interop: npx vitest run --coverage
  Tests 146 passed (146); Statements 100 % (116/116), Branches 100 % (69/69), Functions 100 % (45/45), Lines 100 % (111/111)
```

## Quality checks

- `dotnet build` 0 warnings (TreatWarningsAsErrors); `dotnet format StemStudio.slnx --verify-no-changes` exit 0.
- `src/web-interop`: `npm run lint` and `npm run typecheck` clean; `npm run build`: `web-interop.js`
  10.2 kB (3.6 kB gzip), `first-paint.js` 1.05 kB incl. the source-map comment (0.58 kB gzip).
- `uv run --project tools/quality --frozen python tools/quality/run_all.py`: 620 files, 4 805 units,
  27 clones, **0 errors**, 161 warnings (new warnings: unit size of `HelpRenderer.Render/Rewrite`,
  `TopicParser.Parse`, `HelpSearch.Search`, `PreferencesJson.TryParse/Tours`; to be reduced in polish).
  One duplication error (test default literal vs `defaultPreferences`) was fixed before this note.

## Dependencies added

| Name | Version | Licence | URL | Verified |
|---|---|---|---|---|
| Archivo variable font, Latin subset (`Archivo-Variable-latin.woff2`) | google/fonts `ofl/archivo` (upstream Omnibus-Type/Archivo commit `b5d63988`), v2.001 | SIL OFL-1.1 (no Reserved Font Name) | https://github.com/Omnibus-Type/Archivo | `OFL.txt` copied next to the font; axes wght 100-900, wdth 62-125; subset with fontTools 4.60.1 (build-time only, not shipped), all OpenType features kept (incl. `tnum`); source TTF SHA-256 `0e094a7d3c7c4c25cf1310c4b30014f1dae9332220b1c2c88f4fa996f0b05053`, woff2 SHA-256 `e6fc4127b49ae0474c97ed3d7814e2ce745faca44055718039ef8a5948b7ead1`, 92 912 bytes |

Markdig 1.5.1, YamlDotNet 18.1.0, driver.js 1.10.2 were pinned in Phase 0 (now used).

## Deviations / notes

- Level switch stays in the context bar at every width (it wraps; labels of the icon buttons become
  visually hidden under 768 px) instead of moving into the preferences panel: two copies would duplicate
  the `level-switch` test ids in the DOM.
- `data-theme` and `data-motion` on `app-shell` are written by `applyAppearance` (they depend on OS
  settings C# cannot know); Blazor renders `data-level` and `data-density`.
- Help links are rendered as `/help/<id>`; the shell will intercept that navigation (US3) and the mixer's
  unsaved-changes lock must ignore it (T049).
- The temporary App.razor head of the spike was included in red commit `d09cc7b` by mistake; it was
  completed in `9ce73ee`.

## Open issues / requests

- **devops**: `scripts/test-all.*`, CI and `docker/web.Dockerfile` must run `npm run build` in
  `src/web-interop` before the web image starts, otherwise `js/first-paint.js` and `js/web-interop.js`
  are 404 (the app still works with defaults: `LocalPreferencesStore` treats a failing module as
  unavailable storage).
- **qa**: alignment items from the CTO note are scheduled in US2-US5 (`Mod+K` in text fields, `Mod+S`
  focusing `mix-name-input`, help button opening the screen topic, etc.).

## Next owner

frontend (US1, T025 onwards).
