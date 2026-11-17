# tools/quality: the StemStudio quality gate

Owner: quality-gate agent. Enforces constitution principle II (SIG 4 stars) in CI.

| Script | Checks | Fails (exit 1) on | Warns on |
|---|---|---|---|
| `sig_check.py` | lizard unit metrics on C#, Python, TypeScript, Razor `@code` and C++ (`.cpp`/`.h`, feature 012) | NLOC > 30 (production code), CCN > 10, > 4 parameters | NLOC > 15, CCN > 5, test units > 30 NLOC, `@code` block > 30 lines |
| `duplication.py` | jscpd 5.4.2 via `npx`, config `.jscpd.json` (min 6 lines, 40 tokens) | any clone touching `src/`, `worker/src/`, `generator/src/` or `third_party/soundtouch/` production code | clones only between test files; worker <-> generator production clones (cross-service) |
| `dependency_check.py` | .NET `ProjectReference` graph, .NET feature-folder namespace references, TS package imports (`src/*/package.json`), TS module imports inside a package, the AudioWorklet scope of `stretch/` (012), worker import graph, worker third-party imports (`notation`, `engraving`), generator import graph, worker <-> generator imports | forbidden edge, cycle | module or package not in the dependency map |
| `css_tokens_check.py` | raw colours (hex, `rgb()`/`rgba()`/`hsl()`/`hsla()`/other colour functions, named colours) in `src/StemStudio.Web/wwwroot/css/**`, `*.razor.css` and Razor `style` attributes/`<style>` blocks; only `wwwroot/css/tokens.css` may hold literals | any raw colour | raw colours in the legacy `wwwroot/app.css` (until T020 retires it) |
| `vendor_check.py` | licence records of every package below `detail/vendor/` and `transcription/vendor/` | a package without a `LICENSE`/`LICENCE`/`COPYING` file or without a `NOTICE` file | |
| `secret_rules.py` | Claude key handling (011 follow-up claude-key): Data Protection namespace/entry points, `global using` and packages; `StemStudio.Api.Agent.Secrets` references; `record` declarations of `ClaudeApiKey`/`StoreClaudeKeyRequest`; log calls and `[LoggerMessage]` methods in `Api/Agent/Secrets/**` and `Agent/Backends/Claude*` | Data Protection outside `src/StemStudio.Api/Agent/Secrets/` (or any `global using` of it), the secrets namespace outside `StemStudio.Api`, a Data Protection package outside the API, a key value type declared as `record`, a log argument named `key`/`apiKey`, a `Reveal()` in a log argument, a `{Key}`/`{ApiKey}` placeholder | |
| `library_care_rules.py` | Library care (feature 013): references to `StemStudio.Api.LibraryCare.*` from other API folders (fully, partially or namespace-relative qualified); bulk ZIP helpers in `src/**` | outside `Api/LibraryCare/` and `ApiComposition.cs` any `LibraryCare` name but `Gate`; a call of `ExtractToDirectory`, `ExtractToFile`, `CreateFromDirectory` (or `...Async`), a `using static` of `ZipFile`/`ZipFileExtensions` | |
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

Practice tempo (feature 012):

- Module rules: `transport`, `mixer`, `metronome/` and `karaoke/` never import `practice/` or
  `stretch/`; `practice/` never imports `karaoke/` (same matching as above, type-only included).
- Worklet scope (`TS_WORKLET`, `check_worklet`): `stretch/processor.ts` (the entry of the
  AudioWorklet bundle `time-stretch-processor.js`) imports only
  `stretch/{constants,params,fifo,abi,core}`, type-only imports and npm packages included; those
  modules value-import only each other (`import type` is erased by the build and may point
  anywhere, e.g. `params.ts` -> `../types`). The message is
  `worklet scope: <file> must not import <module>`.
- C++: `.cpp` and `.h` files are measured with lizard like any other unit; `third_party` is a
  default root and `third_party/soundtouch/` is production code (hard limits). Our ABI shim
  `third_party/soundtouch/shim/` (audio-engine) and the header `include/` (architect) are measured;
  the downloaded upstream source `third_party/soundtouch/upstream/` (SoundTouch 2.4.1, LGPL-2.1)
  and the build output `third_party/soundtouch/build/` are excluded, as are the generated web
  outputs `wwwroot/js/time-stretch-processor.js(.map)`, `wwwroot/js/soundtouch/` and
  `wwwroot/third-party/` (same list in `.jscpd.json`, which also scans `cpp`, and in
  `sonar.exclusions`/`sonar.cpd.exclusions`). Component rows: `audio-engine/stretch`,
  `audio-engine/practice` (sub-rows of `audio-engine`) and `soundtouch shim`.

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
Feature 010 (namespace entries): `StemStudio.Api/Voices` -/- `StemStudio.Web`,
`StemStudio.Api.Generation` (one-way `Generation -> Voices`); `StemStudio.Api/Generator` -/-
`StemStudio.Api.Generation`, `StemStudio.Api.Voices` (the client is the lowest layer). A
forbidden name matches itself and every namespace below it, never a longer name
(`StemStudio.Api.GenerationTools` is not `StemStudio.Api.Generation`). Not detected: a
partially qualified name that relies on the enclosing namespace (`Generation.SongService` written
inside `namespace StemStudio.Api.Voices`); review catches those.

Feature 011 (composer agent): project map `Api -> Agent -> Contracts`, `Web` and `Persistence`
never reference `StemStudio.Agent`. Package rule (`DOTNET_PACKAGES_ALLOWED`): the
`PackageReference`s of `StemStudio.Agent.csproj` must belong to the families
`Microsoft.Extensions.AI`, `OllamaSharp` or `Anthropic` (a family name also allows
`<name>.<anything>`, never a longer name such as `OllamaSharpExtras`); the error reads
`forbidden package StemStudio.Agent -> <package>`. SDK namespaces: every C# file of
`src/StemStudio.Agent/` except `Backends/**` (`DOTNET_FOLDER_EXEMPT`) must not reference the
`OllamaSharp` or `Anthropic` namespaces (using directives of any kind and qualified names count;
`AgentBackendDto.Anthropic`, an enum member `Anthropic` or the identifier `OllamaModel` do not).
`StemStudio.Agent/Policy` is an allow-list (`DOTNET_FOLDER_ALLOWED`, CTO decision at the 011
integration T080): inside `StemStudio.Agent` it may reference only `StemStudio.Agent.Model`,
`StemStudio.Agent.Tools`, the data-only `StemStudio.Agent.Resources` (embedded 009 lexicon) and its
own namespace; anything else (`Runtime`, `Backends`, `Ports`, the root namespace) is
`forbidden dependency StemStudio.Agent/Policy -> <name>`. Same gap as above: `Runtime.RunLoop`
written inside `namespace StemStudio.Agent.Policy` is not detected.
Owners: `src/StemStudio.Agent/{Ports,Model}/` and the csproj are the architect's, the rest of the
library, `tests/StemStudio.Agent.Tests/` and `tools/agent-eval/` belong to `ai-agent`; the
library is the `Agent` row of the component table. `tools/agent-eval` (the eval harness) is a
default root and its own component row (measured, not production code).

Data copies (`duplication.DATA_COPIES`, feature 011): a production file that holds copied data,
not logic, is listed with a mandatory reason; a clone touching it is a warning ending in
`(data copy: <reason>)`. The list is empty: the former C# copy of the 009 v1 imitation lexicon
(`src/StemStudio.Agent/Policy/ImitationLexicon.cs`) was replaced by the embedded Domain resource
files (`StemStudio.Agent.Resources.EmbeddedLexicon`) at the 011 integration (T080).

Generator (features 009/010, `GENERATOR_ALLOWED`): `generator/src/stemstudio_generator` follows
`api -> jobs -> ports <- {engines, devices, audio}`; `engines`, `devices` and `audio` import only
`ports`, `schemas` and `config`, `jobs` the same, `api` additionally `jobs`; `ports`, `schemas`
and `config` are leaves; nothing but `main.py` (the composition root) imports an adapter. The
generator is a default root of `sig_check` and `duplication`, `generator/src/` is production code
(hard limits apply), it has component rows `generator/{engines,devices,audio,jobs,api,core}`,
and its service code and tests belong to the `generative` agent (`ports`, `schemas`, `config` and
`__init__` stay with the architect). The SoulX-Singer adapters `engines/soulx_svc.py` and
`engines/rmvpe.py` and `audio/{remix,normalise}.py` (feature 010) follow the same rule. It has no
vendored code (SoulX-Singer is path-installed in
the singer image, ADR-019), so nothing below `generator/` is excluded. Sonar analyses
`generator/src` and `generator/tests` and reads `generator/coverage.xml`.

Service separation (`PythonPackage.foreign`): no worker module imports `stemstudio_generator`
and no generator module imports `stemstudio_worker` (`main.py` included); the services share only
the GPU lease file. Because they cannot share code, a jscpd clone between worker and generator
production code (environment parsing, contract mirrors such as `DeviceInfo` and `Problem`) is a
**warning** marked `(cross-service: ...)`, not an error; every other production clone, including
one inside the generator, stays an error.

Library care (feature 013, `library_care_rules.py`, tasks.md T009/T010): (a) inside
`src/StemStudio.Api` a file outside `LibraryCare/` and the composition root `ApiComposition.cs`
may reference only `StemStudio.Api.LibraryCare.Gate` (and names below it); `StemStudio.Api.LibraryCare.Backup`,
`Api.LibraryCare.Restore` and the namespace-relative `LibraryCare.Storage` (which a
`StemStudio.Api.*` file resolves to the API folder, unlike the gap noted above for the other
folder rules) are `forbidden dependency on <name>: ...`; `StemStudio.Contracts.LibraryCare` and
identifiers such as `LibraryCareLimits` are not references. (b) No production file calls the
bulk `System.IO.Compression` helpers that extract or pack whole trees without per-entry checks
(`ExtractToDirectory`, `ExtractToFile`, `CreateFromDirectory`, each also `...Async`, as member
calls) or has a `using static` of `ZipFile`/`ZipFileExtensions`: `banned API <name>: ...`.
Hostile archives (research R-06) are handled entry by entry; own methods must not reuse those
names. (c) Worker `storage` (`PYTHON_ALLOWED`): imports only `jobs`, `ports`, `schemas`,
`config` (never `separation`, `detail` or `api`); `api` may import `storage`; `jobs` and the
adapters never do. `worker/storage` is its own component row, owned by stem-separation.

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
`python tools/quality/css_tokens_check.py`, `python tools/quality/vendor_check.py`,
`python tools/quality/secret_rules.py`, `python tools/quality/library_care_rules.py`.
