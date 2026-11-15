# Hand-off: architect / plan-and-skeleton

## What was built

- `/speckit-plan` executed (`setup-plan.ps1 -Json`; no `.specify/extensions.yml`, so no hooks):
  plan (Constitution Check PASS before and after design), research (R-01..R-12), data model with
  Track and worker-job state machines and storage layout, quickstart/validation guide.
- Contracts: worker API and backend API (OpenAPI 3.1, schemas, examples, RFC 9457 problems; both
  valid with Redocly), audio-engine interop contract (`contracts/audio-engine.md` + binding
  `src/audio-engine/src/types.ts`).
- ADR-002 .. ADR-007 (architecture/layers, worker contract + ML stack, stem storage/serving,
  audio-engine design, testing/quality tooling, build conventions/CPM/xUnit).
- Skeleton that builds and tests green: .NET solution (5 src + 4 test projects, CPM),
  Python worker package (ports, schemas, config, placeholder app), audio-engine package.
- `StemStudio.Contracts` DTOs are complete (they are the backend contract); `schemas.py` is
  complete for the worker contract; `ports.py` defines every adapter protocol.

## Files changed

- `specs/001-stemstudio-mvp/{plan,research,data-model,quickstart}.md`
- `specs/001-stemstudio-mvp/contracts/{worker-api.openapi.yaml,backend-api.openapi.yaml,audio-engine.md,redocly.yaml}`
- `docs/decisions/ADR-002..ADR-007-*.md`
- `StemStudio.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`
- `src/StemStudio.{Domain,Contracts,Persistence,Api,Web}/**` (placeholders; Contracts complete)
- `tests/StemStudio.{Domain,Persistence,Api,Web}.Tests/**`, `tests/coverlet.runsettings`
- `src/StemStudio.Web/wwwroot/js/.gitignore` (engine bundle is a build artefact)
- `worker/{pyproject.toml,uv.lock,README.md}`, `worker/src/stemstudio_worker/{__init__,ports,schemas,config,main}.py`,
  `worker/src/stemstudio_worker/{api,jobs,ingest,separation,analysis}/__init__.py`, `py.typed`,
  `worker/tests/test_{smoke,config,schemas}.py`, `worker/tests/{ingest,separation,analysis,jobs,api}/.gitkeep`
- `src/audio-engine/{package.json,package-lock.json,tsconfig.json,vite.config.ts,vitest.config.ts,eslint.config.js}`,
  `src/audio-engine/src/{types,index}.ts`, `src/audio-engine/tests/index.test.ts`

## Tests and coverage

| Component | Command | Result | Coverage (line / branch) |
|---|---|---|---|
| .NET build | `dotnet build StemStudio.slnx --no-incremental` | 0 warnings, 0 errors | - |
| .NET tests | `dotnet test StemStudio.slnx -p:CollectCoverage=true` | 10 passed (Domain 1, Persistence 1, Api 5, Web 3), 0 failed | Domain 100/100, Persistence 100/100 (own tests), Api 100/100, Contracts 90/100 (via Api tests), Web 100/100 |
| .NET collector | `dotnet test StemStudio.slnx --collect "XPlat Code Coverage"` | 4 Cobertura files produced | - |
| Worker | `cd worker && uv run pytest --cov` | 24 passed, 0 failed | 100 % (164 stmts, 2 branches; gate 90 %) |
| Audio engine | `cd src/audio-engine && npm test` / `npm run test:coverage` | 2 passed | 100 % lines/statements/functions; 0/0 branches (thresholds 90 %) |

## Quality checks

- `dotnet format StemStudio.slnx --verify-no-changes`: clean (exit 0).
- Worker: `uv run ruff check .` all passed; `uv run ruff format --check .` 14 files formatted;
  `uv run mypy src tests` (strict + pydantic plugin) no issues in 13 files; `uv lock --check` ok.
- Engine: `npm run lint` (ESLint 10, typescript-eslint strict type-checked, complexity 5,
  max-params 4, max-lines-per-function 30) clean; `npm run typecheck` clean; `npm run build` ok.
- Contracts: `npx @redocly/cli@2.78.0 lint --config specs/001-stemstudio-mvp/contracts/redocly.yaml ...`:
  both valid; 3 warnings `operation-4xx-response` on `GET /health` (x2) and `GET /api/tracks`,
  which have no client-error cases.
- Worker schemas cross-checked against the worker OpenAPI (property sets and `JobState` enum equal).
- `tools/quality/sig_check.py` does not exist yet (quality-gate); skeleton units are all < 15 LOC.
- `.venv`, `node_modules`, `bin/obj`, coverage output and `wwwroot/js/audio-engine.js` are ignored (verified with `git status --ignored`).

## Pinned versions and commands per agent

| Agent | Build / test / lint commands |
|---|---|
| backend | `dotnet build StemStudio.slnx`; `dotnet test tests/StemStudio.{Domain,Persistence,Api}.Tests -p:CollectCoverage=true`; `dotnet format StemStudio.slnx --verify-no-changes`. Migrations: `dotnet ef migrations add <Name> -p src/StemStudio.Persistence -s src/StemStudio.Api` (install `dotnet-ef` 10.0.15 as a local tool) |
| frontend | `dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`; engine bundle via `npm run build` in `src/audio-engine` |
| audio-engine | `npm ci`, `npm test`, `npm run test:coverage`, `npm run lint`, `npm run typecheck`, `npm run build` (in `src/audio-engine`) |
| audio-ingest / stem-separation / beat-analysis | in `worker/`: `uv sync`, `uv run pytest --cov`, `uv run pytest -m integration`, `uv run ruff check .`, `uv run ruff format --check .`, `uv run mypy src tests`; run: `uv run uvicorn --factory stemstudio_worker.main:create_app --port 8000` |
| qa / devops | all of the above; compose with `STEMSTUDIO_SEPARATOR=fake` for E2E; contract lint command above |

Toolchain: .NET SDK 10.0.401 (rollForward latestFeature), Python 3.12.13 via uv 0.13.5,
Node 22.23.2 / npm, TypeScript 6.0.3.

## Dependencies added

NuGet (all via `Directory.Packages.props`):

| Name | Version | Licence | URL |
|---|---|---|---|
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.15 | MIT | https://github.com/dotnet/efcore |
| Microsoft.EntityFrameworkCore.Design | 10.0.15 | MIT | https://github.com/dotnet/efcore |
| Microsoft.AspNetCore.OpenApi | 10.0.15 | MIT | https://github.com/dotnet/aspnetcore |
| Microsoft.Extensions.Http.Resilience | 10.11.0 | MIT | https://github.com/dotnet/extensions |
| Radzen.Blazor | 13.1.0 | MIT | https://github.com/radzenhq/radzen-blazor |
| Microsoft.NET.Test.Sdk | 18.12.0 | MIT | https://github.com/microsoft/vstest |
| xunit.v3 | 4.0.3 | Apache-2.0 | https://github.com/xunit/xunit |
| xunit.runner.visualstudio | 4.0.1 | Apache-2.0 | https://github.com/xunit/visualstudio.xunit |
| coverlet.collector | 10.1.0 | MIT | https://github.com/coverlet-coverage/coverlet |
| coverlet.msbuild | 10.1.0 | MIT | https://github.com/coverlet-coverage/coverlet |
| bunit | 2.15.2 | MIT | https://github.com/bUnit-dev/bUnit |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.15 | MIT | https://github.com/dotnet/aspnetcore |
| Microsoft.Extensions.TimeProvider.Testing | 10.11.0 | MIT | https://github.com/dotnet/extensions |
| NSubstitute | 6.2.0 | BSD-3-Clause | https://github.com/nsubstitute/NSubstitute |
| NSubstitute.Analyzers.CSharp | 1.0.17 | MIT (repo licence; nuspec only has deprecated licenseUrl) | https://github.com/nsubstitute/NSubstitute.Analyzers |
| Shouldly | 4.3.0 | BSD-3-Clause | https://github.com/shouldly/shouldly |

Python direct dependencies (`worker/pyproject.toml`, locked in `uv.lock`, 120 packages):

| Name | Version | Licence | URL |
|---|---|---|---|
| fastapi | 0.163.0 | MIT | https://github.com/fastapi/fastapi |
| uvicorn[standard] | 0.23.2 | BSD-3-Clause | https://github.com/encode/uvicorn |
| python-multipart | 0.0.33 | Apache-2.0 | https://github.com/Kludex/python-multipart |
| pydantic | 2.14.3 | MIT | https://github.com/pydantic/pydantic |
| yt-dlp | 2026.10.26 | Unlicense | https://github.com/yt-dlp/yt-dlp |
| spotdl | 4.6.0 | MIT | https://github.com/spotDL/spotify-downloader |
| demucs | 4.1.0 | MIT | https://github.com/adefossez/demucs |
| torch (CPU) | 2.15.0+cpu | BSD-3-Clause (+ bundled Apache-2.0/MIT/Zlib/BSL-1.0 parts) | https://github.com/pytorch/pytorch |
| soundfile | 0.14.0 | BSD-3-Clause | https://github.com/bastibe/python-soundfile |
| librosa | 1.0.1 | ISC | https://github.com/librosa/librosa |
| numpy | 2.5.4 | BSD-3-Clause (+0BSD/MIT/Zlib/CC0 parts) | https://github.com/numpy/numpy |
| dev: pytest 9.2.0 (MIT), pytest-cov 7.1.0 (MIT), httpx 0.28.1 (BSD-3), ruff 0.17.5 (MIT), mypy 2.4.1 (MIT) | | | |

Notable transitive Python licences (all OSI-approved, but **copyleft**, for the CTO's licence
review): lameenc 1.9.0 LGPL-3.0+ (demucs), soxr 1.1.0 LGPL-2.1+ (librosa), mutagen 1.48.1
GPL-2.0+, unidecode 1.4.0 GPL-2.0+, text-unidecode 1.3 (Artistic/GPL), pykakasi 2.3.0 GPL-3.0+,
spotapi 1.2.8 GPL-3.0 (all via spotdl). Also certifi/pathspec MPL-2.0, sphn 0.2.1 Apache-2.0,
spotipyfree 1.9.14 MIT. Full list: `uv tree` / `uv pip list` in `worker/`.

npm devDependencies (`src/audio-engine/package.json`, exact pins, lock committed):

| Name | Version | Licence | URL |
|---|---|---|---|
| typescript | 6.0.3 | Apache-2.0 | https://github.com/microsoft/TypeScript |
| vite | 8.3.8 | MIT | https://github.com/vitejs/vite |
| vitest | 5.1.0 | MIT | https://github.com/vitest-dev/vitest |
| @vitest/coverage-v8 | 5.1.0 | MIT | https://github.com/vitest-dev/vitest |
| eslint | 10.15.0 | MIT | https://github.com/eslint/eslint |
| @eslint/js | 10.0.1 | MIT | https://github.com/eslint/eslint |
| typescript-eslint | 8.72.2 | MIT | https://github.com/typescript-eslint/typescript-eslint |
| globals | 17.14.0 | MIT | https://github.com/sindresorhus/globals |
| jsdom | 31.0.1 | MIT | https://github.com/jsdom/jsdom |
| @types/node | 22.20.5 | MIT | https://github.com/DefinitelyTyped/DefinitelyTyped |
| @redocly/cli (via npx, not installed) | 2.78.0 | MIT | https://github.com/Redocly/redocly-cli |

## Open issues / requests

1. **Demucs version deviates from the brief**: 4.1.0 instead of 4.0.1 (ADR-003). Verified only
   that it imports with torch 2.15.0+cpu and exposes `get_model`/`apply_model`/`demucs.api`; a
   real separation run is first exercised by the stem-separation integration test. Weights are
   fetched from the Hugging Face Hub (fallback dl.fbaipublicfiles.com) on first use: devops must
   persist `HF_HOME` and `TORCH_HOME` in a volume and pre-warm or document first-run download.
2. **No torchaudio** in the stack (not needed with demucs 4.1.0). Agents must not add it; use
   soundfile/ffmpeg for audio I/O.
3. **spotdl 4.6.0** pins uvicorn 0.23.2 and pulls unofficial Spotify web clients
   (spotipyfree/spotapi, GPL-3.0) plus pymongo/redis client libs. Metadata only; flag for the
   CTO's licence and ToS review. If this is unacceptable, the audio-ingest agent could resolve
   Spotify metadata from the public oEmbed/page title instead (contract unchanged).
4. **xUnit v3 runs on VSTest** (`IsTestingPlatformApplication=false`, ADR-007). CI must use
   `dotnet test` (VSTest mode); do not add `"test": {"runner": ...}` to `global.json`.
5. **devops**: build `src/audio-engine` (`npm ci && npm run build`) before publishing
   `StemStudio.Web`; the bundle is git-ignored. Persist API `/data` and worker `/data` volumes.
6. **backend**: SQLite cannot order `DateTimeOffset`; follow the note in data-model.md. Domain
   contains only the two enums; entities, SourceKey parsing and validation are yours.
7. **frontend**: `Home.razor`/`MainLayout.razor` are placeholders; keep the
   `StemStudioWebOptions` names and the `StemStudioApi` named client (or replace with a typed
   client using the same config keys).
8. The CTO-owned `docs/THIRD-PARTY-LICENSES.md` should be consolidated from the tables above.

## Next owner

CTO: review, then `/speckit-tasks` and dispatch backend, frontend, audio-engine, audio-ingest,
stem-separation, beat-analysis, qa, quality-gate and devops against the contracts.

## Phase 0 (contract fixes before the wave, tasks T001-T003)

### What was built
- `specs/001-stemstudio-mvp/tasks.md`: 128 tasks, a per-owner wave plan and an integration wave (commit ef47be1).
- T001 (ea80444): `ports.py` now has the `UploadIntake` protocol and the `IngestAdapters` / `PipelineAdapters`
  dataclasses. Factory stubs with fixed signatures (`ingest.create_ingest_adapters`, `separation.create_separator`,
  `analysis.create_beat_analyzer`) raise `NotImplementedError` until their owners implement them. Covered by
  `worker/tests/test_ports.py`.
- T002 (8650a6c): `contracts/ui-test-ids.md` v1.0.0, linked from `audio-engine.md`.
- T003 (ae481c4): test-only dependencies (tables below) and the local tool manifest `dotnet-tools.json` (dotnet-ef).
  T016 now says the CI coverage gate needs an `-p:Include=[<Project>]*` filter per test project. Without it,
  Contracts (0 % in Web.Tests) fails the default `ThresholdStat=Minimum`.

### Proof (after T003)
- `dotnet build StemStudio.slnx --no-incremental`: 0 warnings, 0 errors. `dotnet test`: 10 passed.
  `dotnet format --verify-no-changes`: clean. Line/branch coverage per own module: Domain, Persistence, Api and
  Web 100/100; Contracts 90/100 (via Api.Tests).
- Worker: `ruff check` clean, `ruff format --check` clean, `mypy src tests` (strict) no issues in 14 files,
  `pytest --cov` 28 passed, coverage 100 % (192 statements, 2 branches). `uv lock --check` ok.
- Audio engine: `npm test` 2 passed; `lint`, `typecheck` and `test:coverage` clean (100 %).

### Dependencies added
| Name | Version | Licence | URL |
|---|---|---|---|
| jsonschema (dev) | 4.26.0 | MIT | https://github.com/python-jsonschema/jsonschema |
| pyyaml (dev, already transitive) | 6.0.3 | MIT | https://github.com/yaml/pyyaml |
| types-PyYAML (dev) | 6.0.12.20261030 | Apache-2.0 | https://github.com/python/typeshed |
| types-jsonschema (dev) | 4.26.0.20261006 | Apache-2.0 | https://github.com/python/typeshed |
| pytest-timeout (dev) | 2.4.0 (2.5.0 is yanked) | MIT | https://github.com/pytest-dev/pytest-timeout |
| transitive: referencing 0.37.0, rpds-py 2026.9.1, jsonschema-specifications 2025.9.1 | | MIT | |
| YamlDotNet (test projects) | 18.1.0 | MIT | https://github.com/aaubry/YamlDotNet |
| dotnet-ef (local tool) | 10.0.15 | MIT | https://github.com/dotnet/efcore |

The audio-engine `package.json` is unchanged; the wave plan needs no new engine dependency.

**JsonSchema.Net is rejected.** Version 9.x ships under the OSMF EULA, which is not an OSI licence.

### Open issues
- dotnet-ef 10.0.15 was published on 2026-11-10, less than two weeks ago. It must match the EF Core 10.0.15
  packages that were already accepted.
- The CI job must run `dotnet tool restore` before migrations are used (devops).

### Phase 0, round 2 (speckit-analyze findings; tasks T129-T133)
- 3735ab9: contracts 1.0.1. Adds `contracts/source-url-vectors.json` (16 accepted forms, 22 rejected), canonical-URL
  forwarding, "200 MiB (209,715,200 bytes)", and getStem 304/ETag/Last-Modified plus 416 `bytes */N`. Collections
  are ordered by `updatedAt` desc. Field limits are aligned in both OpenAPI files and `schemas.py`. The worker result
  example lists all 6 stems, and the model -> stem-set rule is documented. Engine `stop()` always resets to 0 (types.ts
  and audio-engine.md). data-model has `uploads/<jobId>/input<ext>` and fixed error texts. quickstart V4 uses
  200 MiB + 1 byte.
- 1feb1a3: `Directory.Build.props` gates coverage per component with coverlet.msbuild (`Include=[StemStudio.X]*`;
  Api.Tests also covers Contracts), at 90 % line+branch minimum, writing Cobertura and OpenCover. ADR-006 is amended,
  including the sonarscanner flow. The audio engine adds `eslint-plugin-jsdoc` 65.4.2 (BSD-3-Clause) with
  `require-jsdoc` on the exported API; I checked that the rule fires on an undocumented export.
- aec8aa5: plan.md makes Contracts architect-owned and read-only for backend and frontend during the wave. Safari is
  best effort and untested.
- tasks.md now has 142 tasks: new T134-T142 (split tasks) and checkpoint hand-offs `HO:<checkpoint>` in every owner
  sequence.
- Proof: `dotnet build` 0 warnings; `dotnet test -p:CollectCoverage=true` 10 passed, gate passes (Domain, Persistence,
  Api and Web 100/100, Contracts 90/100); `dotnet format` clean. Worker ruff, format and mypy are clean, 31 passed,
  100 %. Engine test, lint, typecheck, coverage and build are green. Redocly: both files valid, the same 3 known
  warnings.
- Open:
  (a) `.claude/agents/backend.md` still lists `src/StemStudio.Contracts/**` as backend-owned. That file is CTO-owned
      and needs aligning with O1.
  (b) Contracts sits exactly at the 90 % line gate. Api.Tests must cover `ApiRoutes`/`StemNames` as the backend grows.
  (c) `npm audit` reports a moderate minimatch advisory, inherited from typescript-eslint 8.72.2 (dev-only).
      It was already present before this change.
