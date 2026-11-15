# Hand-off: quality-gate / 004-foundational (T006, T007)

## What was built
- **Raw-colour guard** `tools/quality/css_tokens_check.py` (FR-003, design-spec.md 2). Only
  `src/StemStudio.Web/wwwroot/css/tokens.css` may hold literal colours.
  - Scanned: every `*.css` below `wwwroot/css/` (incl. `components/`, `radzen-map.css`,
    `base.css`), every scoped `*.razor.css`, and in every `*.razor` file of `src/StemStudio.Web`
    the `style="..."`/`Style='...'` attributes (Radzen `Style` parameter included,
    `data-style` not) and `<style>` blocks. `bin/`, `obj/` and other excluded folders are skipped;
    `wwwroot/lib/` is not scanned.
  - Rejected in declaration values: hex `#rgb`/`#rgba`/`#rrggbb`/`#rrggbbaa`, `rgb()`, `rgba()`,
    `hsl()`, `hsla()`, and also `hwb()`, `lab()`, `lch()`, `oklab()`, `oklch()`, `color()`, and
    the 148 CSS named colours (case-insensitive).
  - Allowed: `var(--...)` (incl. names like `--ss-red`), `transparent`, `currentColor`,
    CSS-wide keywords, `color-mix()` over tokens. Selectors (`#id`, `.red`, `a:hover`), at-rule
    preludes (`prefers-color-scheme: dark`), comments, strings and `url()` are ignored. Line
    numbers survive multi-line comments and strings.
  - Findings: check `raw-colour`, owner `frontend`, hard error. The legacy `wwwroot/app.css` is
    checked as well, but reported as **warnings** until T020 retires it. This keeps the gate green
    during the wave and makes the debt visible (14 warnings today).
  - CLI: `python tools/quality/css_tokens_check.py [--root DIR]`, exit 1 on any error.
- **TS package dependency rule** in `dependency_check.py`: `TS_ALLOWED = {web-interop: {},
  audio-engine: {}}`. A package is any `src/<name>/package.json`. An edge is an import or
  re-export (`import ... from`, `export ... from`, side-effect `import '...'`, dynamic
  `import()`) whose specifier is `@stemstudio/<name>[/...]` or a relative path into
  `src/<name>/`. npm packages (`driver.js`) and imports inside the package are not edges.
  Leading `//` and `/* */` comments are ignored, and `node_modules/`, `dist/`, `coverage/` and
  `*.d.ts` are pruned. Both directions are hard errors. A new unmapped package is a warning.
- **SIG over `src/web-interop`.** `sig_check` already measures `*.ts` below `src/`; a test now
  pins this for `src/web-interop/src/**` (unit size, complexity and parameters all fail there).
  Ownership in `common.OWNERS`: `src/web-interop/src/types.ts` -> architect, the rest of
  `src/web-interop/` -> frontend. New `web-interop` component row. Build output
  `wwwroot/js/{web-interop,first-paint}.js` is excluded (like `audio-engine.js`).
- **Wiring.** `run_all.py` runs the guard (`GateResult.token_findings`). The report gets a
  `## Design tokens` section after `## Dependency direction`. `priorities.py` has a weight
  (1.0) and a remedy for `raw-colour`. `common.report_findings` is the shared CLI tail of
  `dependency_check` and `css_tokens_check`, which removes a 7-line clone.
- **Sonar** (`sonar-project.properties`): `src/web-interop/tests` added to tests and test
  inclusions, `src/web-interop/coverage/lcov.info` added to the lcov paths, web-interop configs
  added to coverage exclusions, web-interop/first-paint build output added to exclusions.
- README updated.

Red to green: `ea0c7c2` (T006, 2 new test modules fail at import), then `62c1421` (T007, with
the wiring tests that were written before the wiring).

## Files changed
- `tools/quality/css_tokens_check.py` (new), `dependency_check.py`, `common.py`,
  `components.py`, `priorities.py`, `report.py`, `run_all.py`, `README.md`
- `tools/quality/tests/test_css_tokens_check.py` (new), `test_web_interop_rules.py` (new),
  `test_common.py`, `test_components.py`, `test_priorities.py`, `test_report.py`,
  `test_run_all.py`
- `sonar-project.properties`, `docs/quality/SIG-REPORT.md` (regenerated)
- `specs/004-design-system-ux/tasks.md` (T006, T007 ticked)

## Tests and coverage
```
uv run --directory tools/quality pytest --cov --cov-branch
css_tokens_check.py   90 stmts 0 miss  20 branches 0 partial 100%
dependency_check.py  150 stmts 0 miss  58 branches 0 partial 100%
TOTAL                903 stmts 0 miss 176 branches 0 partial 100%
Required test coverage of 90.0% reached. Total coverage: 100.00%
253 passed
```
Line coverage is 100% and branch coverage is 100% (176/176). Before: 183 tests.

## Quality checks
```
uv run --directory tools/quality ruff check .          -> All checks passed!
uv run --directory tools/quality ruff format --check . -> 28 files already formatted
uv run --directory tools/quality mypy                  -> Success: no issues found in 27 source files
python tools/quality/sig_check.py tools/quality src/web-interop
sig_check: 33 files, 328 units, 0 errors, 0 warnings, 0 exemptions
python tools/quality/css_tokens_check.py  -> css_tokens_check: 0 errors, 14 warnings (all wwwroot/app.css)
python tools/quality/dependency_check.py  -> dependency_check: 0 errors, 0 warnings
uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md
quality gate: 577 files, 4455 units, 26 clones, 1 exemptions, 0 errors, 155 warnings -> exit 0
```
Compared with the 003 report (141 warnings, 26 clones), the 14 new warnings are the legacy
`app.css` colours. `web-interop` (Phase 0 skeleton) has 14 units, all in the low band.
CI needs no change: the `quality` job already runs `run_all.py`, which now includes the guard.

## Dependencies added
None (stdlib `re` only).

## Open issues / requests
- **frontend (T020):** move every `app.css` colour into `tokens.css` and use `var(--ss-...)` in
  `css/components/*.css`. When `app.css` is deleted, its 14 warnings disappear. Any raw colour
  that lands in `css/**`, `*.razor.css` or a `style` attribute is a hard error.
- **frontend:** colours computed in C# and bound dynamically (`style="color: @c"`, Radzen
  `Color`/`IconColor`-like parameters with literals) are outside what a static check can see.
  Keep stem colours as tokens (`var(--ss-stem-...)`) and bind only the token name. Tell
  quality-gate if a component needs a literal-colour parameter, and the check can be widened.
- **frontend:** `rgb(var(--x) / 0.5)` counts as a raw colour function. Use `color-mix()` over
  tokens, or a dedicated alpha token in `tokens.css`.
- **T066 (HO:polish)** needs the merged wave. See `quality-gate-004-polish.md` for the pre-merge run.

## Next owner
frontend (T012/T020 onwards). After the merge, the CTO re-runs `run_all.py` for T066.
