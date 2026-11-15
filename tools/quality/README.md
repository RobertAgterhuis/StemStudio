# tools/quality: the StemStudio quality gate

Owner: quality-gate agent. Enforces constitution principle II (SIG 4 stars) in CI.

| Script | Checks | Fails (exit 1) on | Warns on |
|---|---|---|---|
| `sig_check.py` | lizard unit metrics on C#, Python, TypeScript and Razor `@code` | NLOC > 30 (production code), CCN > 10, > 4 parameters | NLOC > 15, CCN > 5, test units > 30 NLOC, `@code` block > 30 lines |
| `duplication.py` | jscpd 5.4.2 via `npx`, config `.jscpd.json` (min 6 lines, 40 tokens) | any clone touching `src/` or `worker/src/` production code | clones only between test files |
| `dependency_check.py` | .NET `ProjectReference` graph, .NET feature-folder namespace references, TS package imports (`src/*/package.json`), worker import graph | forbidden edge, cycle | module or package not in the dependency map |
| `css_tokens_check.py` | raw colours (hex, `rgb()`/`rgba()`/`hsl()`/`hsla()`/other colour functions, named colours) in `src/StemStudio.Web/wwwroot/css/**`, `*.razor.css` and Razor `style` attributes/`<style>` blocks; only `wwwroot/css/tokens.css` may hold literals | any raw colour | raw colours in the legacy `wwwroot/app.css` (until T020 retires it) |
| `run_all.py` | all of the above, optional `--report` Markdown | any error (1); tool missing (2) | |
| `sonar_args.py` | `sonar-project.properties` -> `/d:` args for `dotnet sonarscanner begin` | | |

Report helpers (no CLI): `risk.py` (SIG risk profiles, house star estimate), `components.py`
(per-component three-band distributions), `priorities.py` (top-10 refactor requests),
`report.py` (Markdown). lizard's `*global*` pseudo-unit (file-level tokens, e.g. after a C# switch
expression) is not a unit and is not measured.

## Exemption: `sig-exempt: <reason>`

For a signature fixed by an interface or framework that cannot meet the parameter limit, put a
comment on the signature line or on the line directly above it:

```csharp
// sig-exempt: interface-mandated signature (ILogger.Log)
public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => ...
```

- It waives only the `parameters` check of that unit; unit size and complexity still apply.
- The reason is mandatory: `sig-exempt:` without one is an error (`exemption` check).
- A marker on a unit with <= 4 parameters is a warning (remove it).
- Accepted comment forms: `//`, `#`, `/* */`, `@* *@`. Every exemption is printed and listed in
  the report with file:line and reason.

Excluded everywhere: `bin/`, `obj/`, `node_modules/`, `.venv/`, `Migrations/`, `TestResults/`,
`coverage/`, `dist/`, `*.g.cs`, `*.generated.cs`, `*.Designer.cs`, `*.d.ts`,
`wwwroot/js/{audio-engine,web-interop,first-paint}.js` (build output), the deliberate samples in `tests/samples/` and the vendored
third-party MSST code in `worker/src/stemstudio_worker/detail/vendor/` (feature 002 plan.md
Complexity Tracking, accepted by the CTO; it is covered by the marked integration test instead).
Vendored files contribute no edges to the dependency graph.

TypeScript package rule (feature 004): `src/web-interop` and `src/audio-engine` never import
each other (`@stemstudio/<name>` or a relative path into the other package); npm packages such as
`driver.js` are not edges. A new package below `src/` with a `package.json` is a warning until it
is added to `TS_ALLOWED`.

Raw-colour guard (feature 004, FR-003): values are checked per declaration, so selectors (`#id`,
`.red`), comments, strings, `url()` and custom-property names are ignored; `var()`,
`transparent`, `currentColor`, CSS-wide keywords and `color-mix()` over tokens are allowed.
Findings are owned by frontend.

Worker dependency rule: `api -> jobs -> ports <- {ingest, separation, analysis, detail, devices,
metadata, render}`, every module may import `schemas` and `config`, `detail` may additionally import its own
`vendor`, `jobs` never imports `detail`, no module imports `devices` (feature 003: `separation` and
`detail` receive the `DeviceRunner` from the composition root), no module imports `metadata`
(feature 005: `ingest` receives the `ImageNormalizer` from the composition root), no module imports
`render` (feature 007: `jobs` receives the `RenderExecutor` from the composition root) and
`main.py` (composition root) is unrestricted.

.NET feature-folder rule (`DOTNET_FOLDER_FORBIDDEN`): the C# files below a listed folder must not
reference the forbidden StemStudio project namespaces, neither in `using`/`global using`/
`using static`/alias directives nor as qualified names in code; comments, string and char
literals and `namespace` declarations are ignored, `obj/` and generated files are skipped.
Feature 005: `StemStudio.Api/Metadata` and `StemStudio.Api/Artwork` -/- `StemStudio.Web`.
Feature 007: `StemStudio.Api/Assets` -/- `StemStudio.Web`; `StemStudio.Domain/Rendering` -/-
`StemStudio.{Api, Persistence, Web, Contracts}` (the planner only knows Domain types and the
library layout strings it produces).

## Commands (repository root)

```bash
# the gate (exit 0 = pass, 1 = violation, 2 = tool error; needs Node 22 for jscpd)
uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md
# without uv
pip install -r tools/quality/requirements.txt && python tools/quality/run_all.py

# self-checks of this component
uv run --directory tools/quality ruff check .
uv run --directory tools/quality ruff format --check .
uv run --directory tools/quality mypy
uv run --directory tools/quality pytest --cov --cov-branch --cov-report=xml
```

Single checks: `python tools/quality/sig_check.py [paths] [--json out.json]`,
`python tools/quality/duplication.py [paths]`, `python tools/quality/dependency_check.py`,
`python tools/quality/css_tokens_check.py`.
