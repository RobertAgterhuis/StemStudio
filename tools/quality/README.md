# tools/quality: the StemStudio quality gate

Owner: quality-gate agent. Enforces constitution principle II (SIG 4 stars) in CI.

| Script | Checks | Fails (exit 1) on | Warns on |
|---|---|---|---|
| `sig_check.py` | lizard unit metrics on C#, Python, TypeScript and Razor `@code` | NLOC > 30 (production code), CCN > 10, > 4 parameters | NLOC > 15, CCN > 5, test units > 30 NLOC, `@code` block > 30 lines |
| `duplication.py` | jscpd 5.4.2 via `npx`, config `.jscpd.json` (min 6 lines, 40 tokens) | any clone touching `src/`, `worker/src/` or `generator/src/` production code | clones only between test files; worker <-> generator production clones (cross-service) |
| `dependency_check.py` | .NET `ProjectReference` graph, .NET feature-folder namespace references, TS package imports (`src/*/package.json`), TS module imports inside a package, worker import graph, worker third-party imports (`notation`, `engraving`), generator import graph, worker <-> generator imports | forbidden edge, cycle | module or package not in the dependency map |
| `css_tokens_check.py` | raw colours (hex, `rgb()`/`rgba()`/`hsl()`/`hsla()`/other colour functions, named colours) in `src/StemStudio.Web/wwwroot/css/**`, `*.razor.css` and Razor `style` attributes/`<style>` blocks; only `wwwroot/css/tokens.css` may hold literals | any raw colour | raw colours in the legacy `wwwroot/app.css` (until T020 retires it) |
| `vendor_check.py` | licence records of every package below `detail/vendor/` and `transcription/vendor/` | a package without a `LICENSE`/`LICENCE`/`COPYING` file or without a `NOTICE` file | |
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
Complexity Tracking, accepted by the CTO; it is covered by the marked integration test instead)
and the vendored basic-pitch and piano-transcription code in
`worker/src/stemstudio_worker/transcription/vendor/` (feature 008 plan.md, same rule; thin adapters
in `transcription/` are measured). Both folders are also in `.jscpd.json` and in
`sonar.exclusions`. Vendored files contribute no edges to the dependency graph; instead
`vendor_check.py` requires a `LICENSE` and a `NOTICE` file (origin, version, modifications) in
every vendored package directory.

TypeScript package rule (feature 004): `src/web-interop` and `src/audio-engine` never import
each other (`@stemstudio/<name>` or a relative path into the other package); npm packages such as
`driver.js` are not edges. A new package below `src/` with a `package.json` is a warning until it
is added to `TS_ALLOWED`.

TypeScript module rule (feature 006, `TS_MODULE_FORBIDDEN`): inside `src/audio-engine/src/`,
`transport` and `mixer` (the file `transport.ts` or any file below `transport/`) never import
`karaoke/` through a relative specifier (static, type-only, `export ... from` or dynamic
`import()`). `karaoke` may read the transport clock, `engine.ts`/`index.ts` compose it, and
test files are not modules.

Raw-colour guard (feature 004, FR-003): values are checked per declaration, so selectors (`#id`,
`.red`), comments, strings, `url()` and custom-property names are ignored; `var()`,
`transparent`, `currentColor`, CSS-wide keywords and `color-mix()` over tokens are allowed.
Findings are owned by frontend.

Worker dependency rule: `api -> jobs -> ports <- {ingest, separation, analysis, detail, devices,
metadata, render, lyrics, alignment, transcription}`, `{notation, engraving} -> {ports,
schemas}` (feature 008: no `config`), every other module may import `schemas` and `config`, `detail` may additionally import its own
`vendor`, `jobs` never imports `detail`, no module imports `devices` (feature 003: `separation` and
`detail` receive the `DeviceRunner` from the composition root), no module imports `metadata`
(feature 005: `ingest` receives the `ImageNormalizer` from the composition root), no module imports
`render` (feature 007: `jobs` receives the `RenderExecutor` from the composition root), no module
imports `lyrics` or `alignment` (feature 006: `jobs` receives the `LyricsLookup` and the `Aligner`
from the composition root), no module imports `transcription`, `notation` or `engraving`
(feature 008: `jobs` receives the `PitchTranscriber`, `Notator` and `ScoreEngraver` from the
composition root) and `main.py` (composition root) is unrestricted.

Third-party rule (`PYTHON_EXTERNAL_ALLOWED`, feature 008): `notation` may import only `music21`
and `engraving` only `verovio`, `cairosvg` and `pypdf` besides the standard library
(`sys.stdlib_module_names`). Lazy imports inside functions and `TYPE_CHECKING` blocks count. So
neither imports `torch`, `onnxruntime` or `numpy`. Other modules are not restricted (transcription
uses onnxruntime/torch). The error reads `forbidden third-party dependency notation -> numpy`.

Sub-module exception (`PYTHON_SUBMODULES_ALLOWED`, feature 006): `lyrics` may import the shared
egress modules `metadata.http`, `metadata.ratelimit` and `metadata.user_agent` (and names inside
them), no other `metadata` module; the error names the imported module and the allowed ones.
Edges are recorded per imported name, and one statement yields at most one finding.

.NET feature-folder rule (`DOTNET_FOLDER_FORBIDDEN`): the C# files below a listed folder must not
reference the forbidden StemStudio project namespaces, neither in `using`/`global using`/
`using static`/alias directives nor as qualified names in code; comments, string and char
literals and `namespace` declarations are ignored, `obj/` and generated files are skipped.
Feature 005: `StemStudio.Api/Metadata` and `StemStudio.Api/Artwork` -/- `StemStudio.Web`.
Feature 007: `StemStudio.Api/Assets` -/- `StemStudio.Web`; `StemStudio.Domain/Rendering` -/-
`StemStudio.{Api, Persistence, Web, Contracts}` (the planner only knows Domain types and the
library layout strings it produces).
Feature 009: `StemStudio.Api/Generation` and the generator client `StemStudio.Api/Generator` -/-
`StemStudio.Web`.

Generator (features 009/010, `GENERATOR_ALLOWED`): `generator/src/stemstudio_generator` follows
`api -> jobs -> ports <- {engines, devices, audio}`; `engines`, `devices` and `audio` import only
`ports`, `schemas` and `config`, `jobs` the same, `api` additionally `jobs`; `ports`, `schemas`
and `config` are leaves; nothing but `main.py` (the composition root) imports an adapter. The
generator is a default root of `sig_check` and `duplication`, `generator/src/` is production code
(hard limits apply), it has component rows `generator/{engines,devices,audio,jobs,api,core}`,
and its service code and tests belong to the `generative` agent (`ports`, `schemas`, `config` and
`__init__` stay with the architect). It has no vendored code (SoulX-Singer is path-installed in
the singer image, ADR-019), so nothing below `generator/` is excluded. Sonar analyses
`generator/src` and `generator/tests` and reads `generator/coverage.xml`.

Service separation (`PythonPackage.foreign`): no worker module imports `stemstudio_generator`
and no generator module imports `stemstudio_worker` (`main.py` included); the services share only
the GPU lease file. Because they cannot share code, a jscpd clone between worker and generator
production code (environment parsing, contract mirrors such as `DeviceInfo` and `Problem`) is a
**warning** marked `(cross-service: ...)`, not an error; every other production clone, including
one inside the generator, stays an error.

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
`python tools/quality/css_tokens_check.py`, `python tools/quality/vendor_check.py`.
