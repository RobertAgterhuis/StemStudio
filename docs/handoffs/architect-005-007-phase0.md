# Hand-off: architect / 005 + 007 Phase 0 (contract code and skeleton)

005 T001-T005 and 007 T001-T005, executed as **one skeleton** on top of 004 Phase 0 (6258eae),
so the two features can be built in one combined wave in parallel with the 004 UI wave.

## What was built

### Contract versions (docs/ROADMAP.md, binding)

| Document | 005 (`specs/005-artwork-band-info/contracts/`) | 007 (`specs/007-export-library/contracts/`) |
|---|---|---|
| worker-api | **1.3.0** (1.2.0 + artwork, metadata jobs, `POST /v1/artwork`, `Health.onlineLookups`) | **1.4.0** = 1.3.0 + render jobs, `Health.library`, `Health.lanes` |
| backend-api | **1.4.0** (1.3.0 + `Track.artwork/metadata`, artwork routes, band info, lookup) | **1.5.0** = 1.4.0 + `/api/assets/**`, `AudioSource` |
| ui-test-ids | **1.4.0** (on 004's 1.3.0) | **1.5.0** = 1.4.0 + export dialog, library |
| render-vectors | - | **1.0.0** (`render-vectors.json` + `generate_render_vectors.py --check`) |
| redocly.yaml | yes | yes |

Code and test links use the **007** documents (worker `CONTRACT_VERSION = "1.4.0"`, Contracts
DTOs = backend 1.5.0). Tests prove the 007 documents contain the 005 documents unchanged
(`ArtworkContractTests.Feature007Contract_ContainsEveryFeature005EntryUnchanged`, worker
`test_1_4_0_contains_1_3_0_unchanged_except_health`; only `/health` and `Health` differ), so a
005 owner implementing against the 005 documents and a 007 owner against the 007 documents
produce one consistent system. ADR-014 and ADR-016 are **Accepted**. Refinements against the
deltas are listed at the top of each `contracts/DELTA.md` ("Phase 0 outcome"); the ones wave
agents must know:

- **005**: `MetadataLookup.lookup()` returns `MetadataFindings(result, artwork)`; settings live in
  `WorkerSettings.online.{lookups_enabled, contact_url, wikipedia_languages, fake_mode}`; worker
  422 bodies are problem+json (`ValidationFailed`): stem-separation adds a `RequestValidationError`
  handler; help topic ids are `artwork`, `band-info`, `online-lookups` (no `help-` prefix; tasks.md
  updated); backend 422 = `UnprocessableRequest`.
- **007**: `RenderJob.renderJobId` (not `id`; data-model 2.4 updated); the path allow-list is not
  a schema pattern (400 from the guard; `ports.LIBRARY_PATH_PATTERNS`, `is_allowed_library_path`);
  `RenderExecutor.validate()` + `render()`; `RenderProgress.__call__(fraction, /)`; settings in
  `WorkerSettings.render.{library_dir, block_seconds}`; file `HEAD` is documented inside
  `getAssetFile`; vectors carry `path` and `contents` per input, inputs in mixer order.

### Worker (T001/T002 of both)

- `schemas.py` (1.4.0): `Sha256`, `Isrc`, `Mbid`; `ArtworkSource`, `ArtworkVariant`,
  `ArtworkInfo`, `SourceMetadata.{album, isrc, duration_seconds, lookup_title, lookup_artist}`,
  `Job.artwork`, `MetadataJobState`, `CreateMetadataJobRequest`, `MetadataJob`, `RecordingMatch`,
  `ReleaseGroupInfo`, `ArtistType`, `ArtistMember`, `WikipediaSummary`, `ArtistInfo`,
  `MetadataResult`, `ArtworkUpload`; `RenderInput`, `RenderRange` (>= 0.5 s), `RenderOutput`,
  `RenderMetadata`, `RenderJobRequest`, `RenderJobStatus`, `RenderResult`, `RenderJob`,
  `LibraryHealth`, `LaneHealth`, `LanesHealth`; `Health.online_lookups/library/lanes` with
  defaults; fixed texts (`METADATA_FAILED_TEXT`, `WIKIPEDIA_WARNING_TEXT`, `COVER_WARNING_TEXT`,
  `LIBRARY_UNAVAILABLE_TEXT`, `RESTART_INTERRUPTED_TEXT`, `SILENT_RENDER_TEXT`).
- `ports.py`: `ArtworkCandidate` (exactly one of url/data), `ArtworkImages.info()`,
  `ImageNormalizer`, `MetadataQuery`, `MetadataFindings`, `MetadataLookup`, artwork/metadata
  errors; `SourceInfo` and `FetchedAudio` gain defaulted 005 fields; `LIBRARY_PATH_PATTERNS`,
  `is_allowed_library_path`, `RenderProgress`, `LibraryReader`, `RenderExecutor`, `RenderError`,
  `InputPathError`, `InputNotFoundError(path)`, `RenderCancelledError`.
- `config.py`: `OnlineSettings` (`STEMSTUDIO_ONLINE_LOOKUPS`, `..._CONTACT_URL`,
  `..._WIKIPEDIA_LANGUAGES` 1-5 codes, `..._FAKE_ONLINE`), `RenderSettings`
  (`STEMSTUDIO_LIBRARY_DIR`, `STEMSTUDIO_RENDER_BLOCK_SECONDS` > 0).
- `pyproject.toml` / `uv.lock`: direct pins (below); httpx moved from dev to runtime; mypy
  override for `lameenc` only (tinytag ships `py.typed`).
- Skeleton tests: `worker/tests/test_metadata_contract.py` (50), `worker/tests/test_render_contract.py` (59).

### .NET (005 T003, 007 T002)

- `StemStudio.Contracts`: `Tracks/{ArtworkDto, ArtworkSourceDto, TrackMetadataDto, MetadataStatusDto,
  MetadataLookupRequest}`, `TrackDto.Artwork` (null) / `TrackDto.Metadata` (default `None`),
  `Artists/{BandInfoDto, ArtistInfoDto, ArtistMemberDto, ArtistTypeDto, WikipediaSummaryDto,
  DataSourceDto, RecordingInfoDto}`, `Assets/{AssetDto, AssetKindDto, AssetStatusDto, AssetFileDto,
  AssetFileRoleDto, AudioFactsDto, AudioFileFormatDto, ProvenanceDto, AssetOriginDto,
  ExportRecipeDto, AssetListDto, CreateAudioExportRequest, CreateStemPackRequest, StemPackDto,
  RenameAssetRequest, ExportFormatDto, ExportModeDto, ExportMixDto, RangeDto, AudioSourceDto}`;
  `ApiRoutes.{TrackArtwork, TrackArtworkUpload, ArtworkCover, ArtworkThumb, BandInfo,
  MetadataLookup, Assets, AudioExports, StemPacks, Asset, RetryAsset, AssetFile, AssetFileAudio}`;
  `ContractLimits.{MaxArtworkUploadBytes, ArtworkMediaTypes, MetadataNameMaxLength,
  AssetNameMaxLength, MaxExportChannels, MinExportRangeSeconds, MinFreeDiskBytesDefault,
  ExportFormats, ExportBitDepths, Mp3Bitrates, MaxAssetPageSize, ChannelIdPattern}`.
- Skeleton tests `tests/StemStudio.Api.Tests/Contract/{ArtworkContractTests, AssetContractTests,
  ContractJson}.cs` (DTO round trips of every example, enum/limit/route parity, worker example
  copies, 005 subset rule, vectors).
- Links: `StemStudio.Api.Tests` -> 007 worker/backend documents, the 005 backend document
  (`Contract/005/`), `render-vectors.json`; `StemStudio.Domain.Tests` -> `Rendering/render-vectors.json`
  (for backend 007 T027).
- **No Web change** (no `IStemStudioApiClient` member was needed; the new `TrackDto` members have
  defaults).

### Agreed one-time edits outside the lane (mechanical consequences of the version bump)

- Worker: `tests/api/contract.py`, `tests/test_detail_contract.py`, `tests/test_device_contract.py`
  (document path -> 007, version 1.4.0); `tests/api/test_jobs_api.py` and `tests/test_smoke.py`
  (`/health` body with the 1.3.0/1.4.0 defaults; job metadata with the five new null fields);
  `tests/api/test_detail_routes.py` (version 1.4.0). The `/health` literal expects
  `library.readable = false` (the default) until stem-separation wires the real library state.
- Backend: `BackendContractTests.cs` (operationId list + 13 new ids),
  `BackendContractTests.StemsAndMixes.cs` (`PendingDetailOperations` replaced by
  `PendingArtworkOperations` (5 ids, backend 005 T026/T039/T046 removes them) and
  `PendingAssetOperations` (8 ids, backend 007 T035/T042/T049/T054/T059)),
  `DeviceContractTests.cs` (version test moved to `AssetContractTests`),
  `Worker/WorkerDeviceClientTests.cs` (health example version 1.4.0), `Worker/Examples/*.json`
  (5 copies re-synced, 16 new: metadata jobs/results, artwork upload, render jobs, problems).

## Files changed

- `specs/005-artwork-band-info/contracts/{worker-api.openapi.yaml, backend-api.openapi.yaml, ui-test-ids.md, redocly.yaml}` (new), `DELTA.md`, `tasks.md`
- `specs/007-export-library/contracts/{worker-api.openapi.yaml, backend-api.openapi.yaml, ui-test-ids.md, redocly.yaml, render-vectors.json, generate_render_vectors.py}` (new), `DELTA.md`, `data-model.md`, `tasks.md`
- `docs/decisions/ADR-014-artwork-and-band-info.md`, `docs/decisions/ADR-016-asset-library-and-export-rendering.md` (Accepted)
- `worker/src/stemstudio_worker/{schemas,ports,config}.py`, `worker/pyproject.toml`, `worker/uv.lock`, `worker/tests/test_{metadata,render}_contract.py` (new) + the one-time edits above
- `src/StemStudio.Contracts/**` (34 files new/changed), `tests/StemStudio.Api.Tests/**`, `tests/StemStudio.Domain.Tests/StemStudio.Domain.Tests.csproj`
- `docs/handoffs/architect-005-007-phase0.md` (this note)
- Untouched: `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**`, `src/audio-engine/**`, `src/web-interop/**`, `e2e/**`, CI, scripts, compose.

## Tests and coverage

`scripts/test-all.ps1 -ContinueOnError`: **all 29 steps passed** (exit 0).

```text
dotnet build StemStudio.slnx -c Release            0 Warning(s), 0 Error(s)
dotnet format --verify-no-changes                  passed
dotnet test -p:CollectCoverage=true (gates on):
  StemStudio.Domain.Tests       261 passed   Domain        line 98.54 %  branch 97.67 %
  StemStudio.Persistence.Tests   48 passed   Persistence   line 98.56 %  branch 100 %
  StemStudio.Web.Tests          332 passed   Web           line 99.88 %  branch 95.12 %
  StemStudio.Api.Tests          473 passed   Api           line 99.63 %  branch 98.02 %
                     (+59 skeleton cases)    Contracts     line 100 %    branch 100 %
worker uv run pytest --cov --cov-branch           874 passed, 3 deselected (integration)
  TOTAL 3866 stmts, 0 missed, 408 branches, 1 partial (pre-existing) -> 99.98 %
  schemas.py 361/361, ports.py 165/165, config.py 79/79 (100 % line and branch)
worker ruff check / ruff format --check / mypy --strict (src 81 files, tests 71 files)   clean
engine (untouched): lint, typecheck, test:coverage (160 passed, 100 %), build      passed
quality tools pytest 100 %; fixtures pytest 100 %, generate                        passed
e2e: npm ci, lint, typecheck, playwright --list (78 tests in 14 files)             passed
```

Redocly 2.78.0 (not yet in test-all, which still lints the 003 documents until devops T008):

```text
npx @redocly/cli@2.78.0 lint --config specs/005-artwork-band-info/contracts/redocly.yaml <worker> <backend>
  -> valid, 4 warnings (operation-4xx-response: worker /health, backend /health, /api/device, GET /api/tracks)
npx @redocly/cli@2.78.0 lint --config specs/007-export-library/contracts/redocly.yaml <worker> <backend>
  -> valid, 6 warnings (the 4 above + idempotent DELETE /v1/render-jobs/{id}; no-unused-components AudioSource)
python specs/007-export-library/contracts/generate_render_vectors.py --check       exit 0
```

## Quality checks

- `uv run --project tools/quality python tools/quality/run_all.py`: 611 files, 4521 units,
  27 clones, **0 errors**, 142 warnings (141 at 004 Phase 0). No warning in the new skeleton
  files (`schemas.py`, `ports.py`, `config.py`, the Contracts DTOs, the four new test files); the
  grown `/health` literals in `test_jobs_api.py`/`test_smoke.py` were already warnings.
- Public API documented: docstrings on every new schema/port/setting, XML docs on every DTO member
  (TreatWarningsAsErrors passes).

## Dependencies added

| Name | Version (published) | Licence | URL | Note |
|---|---|---|---|---|
| tinytag | 2.3.2 (2026-09-07) | MIT | https://pypi.org/project/tinytag/ | new, no runtime deps, ships `py.typed` |
| Pillow | 13.0.0 (2026-10-16) | MIT-CMU | https://pypi.org/project/pillow/ | was transitive, now direct |
| httpx | 0.28.1 (2024-12-06) | BSD-3-Clause | https://pypi.org/project/httpx/ | dev -> runtime |
| rapidfuzz | 3.14.6 (2026-08-30) | MIT | https://pypi.org/project/RapidFuzz/ | was transitive, now direct |
| lameenc | 1.9.0 (2026-10-15) | LGPL-3.0-or-later | https://pypi.org/project/lameenc/ | was transitive (demucs), now direct; used unmodified as an extension |

All published more than two weeks before 2026-11-15 (PyPI JSON API, `upload_time`). `uv lock`
added only tinytag; the other four versions were already locked. No NuGet or npm change.

## Combined 005 + 007 wave (execution plan)

Start every agent from the Phase 0 commit. Task ids collide between the features, so they are
written `005:T0nn` / `007:T0nn`; hand-offs are `docs/handoffs/<owner>-005-<checkpoint>.md` and
`<owner>-007-<checkpoint>.md`.

| Owner | Agents | Sequence | Why |
|---|---|---|---|
| audio-ingest | **2 in parallel** (safe) | A (005): 005:T010 -> T011 -> T012 -> T013 -> HO:foundational -> T022 -> T023 -> HO:us1 -> T031 -> T032 -> T033 -> T034 -> HO:us2 -> T051 (HO:polish). B (007): 007:T011 -> T012 -> ... -> T020 -> HO:foundational -> T064 (HO:polish) | disjoint: `metadata/**`, `ingest/**` (+ tests) vs `render/**` (+ tests); both only import `ports`/`schemas`/`config` |
| stem-separation | **1, sequential** | 005:T014 -> T015 -> HO:foundational -> T024 -> T025 -> HO:us1 -> T035 -> T036 -> HO:us2 -> T044 -> T045 -> HO:us3 -> T052 (HO:polish) -> 007:T021 -> T022 -> T023 -> T024 -> HO:foundational -> T033 -> T034 -> HO:us1 -> T065 (HO:polish) | both edit `main.py`, `api/routes.py` (`/health`), the job lanes and the app's problem handlers. Build the 005 metadata lane (005:T015) on a generic `jobs/lanes.py` `LaneScheduler`, so 007:T021/T022 extends it instead of adding a second scheduler; add the `RequestValidationError` -> problem+json handler once (005:T036) |
| backend | **1, sequential** | 005:T016 -> T017 -> T018 -> T019 -> T020 -> T021 -> HO:foundational -> T026 -> T027 -> HO:us1 -> T037 -> T038 -> T039 -> T040 -> HO:us2 -> T046 -> T047 -> HO:us3 -> T053 (HO:polish) -> 007:T025 -> ... -> T032 -> HO:foundational -> T035 -> T036 -> HO:us1 -> T042 -> T043 -> HO:us2 -> T049 -> T050 -> HO:us3 -> T054 -> T055 -> HO:us4 -> T059 -> T060 -> HO:us5 -> T066 (HO:polish) | two EF migrations (`AddArtworkAndArtists`, `AddAssets`) share `StemStudioDbContext` and the model snapshot; both extend `IWorkerClient`/`WorkerClient`, the composition root, `JobSyncService`, track deletion and the shared test support (`ApiFactory`, fake worker) |
| frontend | **1, sequential, after the 004 merge** | 005:T028 -> T029 -> HO:us1 -> T041 -> T042 -> HO:us2 -> T048 -> T049 -> HO:us3 -> T054 (HO:polish) -> 007:T037 -> T038 -> T039 -> T040 -> HO:us1 -> T044 -> T045 -> HO:us2 -> T051 -> T052 -> HO:us3 -> T056 -> T057 -> HO:us4 -> T061 -> T062 -> HO:us5 -> T067 (HO:polish) | both edit `IStemStudioApiClient` + its implementation and test fake, the track detail page, the mixer, the shell navigation and the help topics |
| qa | **1, sequential** | 005:T009 -> T030 -> HO:us1 -> T043 -> HO:us2 -> T050 -> HO:us3 -> T055 (HO:polish) -> 007:T010 -> T041 -> HO:us1 -> T047 -> T048 -> HO:us2 -> T053 -> HO:us3 -> T058 -> HO:us4 -> T063 -> HO:us5 -> T068 (HO:polish) | both edit `docs/testing/TEST-STRATEGY.md` and the shared e2e support/page objects |
| devops | **1, sequential** | 005:T008 -> 007:T008 -> T009 -> HO:foundational -> 005:T056 + 007:T069 (one HO:polish each) | both edit `docker-compose.yml` (worker env and the read-only `/library` mount), `scripts/dev.*`, CI. **Point `CONTRACTS_DIR` (ci.yml line 26) and `scripts/test-all.{ps1,sh}` at `specs/007-export-library/contracts` once** (it covers 005); 005:T008's "-> 005 contracts" is superseded (tasks.md updated) |
| quality-gate | **1, sequential** | 005:T006 -> T007 -> 007:T006 -> T007 -> HO:foundational -> 005:T057 + 007:T070 (HO:polish) | both add rules to `tools/quality/dependency_check.py` and its tests |
| audio-engine | 1 (007 only) | 007:T046 -> HO:us2 | test-only vector consumer; can start at once, independent of 004 |
| CTO | - | one integration wave: merge 004 first, then the 005/007 branches; 005:T058 + 007:T071 (merge, build, gates) -> 005:T059 (artwork/band-info e2e) + 007:T071 suites -> 005:T060 + 007:T073 (licences) -> 005:T061 + 007:T072 (README) -> 005:T062 (live services) -> 005:T063 + 007:T074 (analyze, STATUS, PR) | |

**Wait for the 004 merge**: every frontend task of both features (005:T028, T029, T041, T042,
T048, T049, T054; 007:T037-T040, T044, T045, T051, T052, T056, T057, T061, T062, T067). Each one
touches a page, the shell/navigation or the help/level framework that the 004 frontend wave is
rebuilding (`Tracks.razor`, `TrackDetail`, mixer header, import legal notice, `nav-library`,
help topics, journey levels). Starting them earlier means writing against stubs and redoing the
markup. qa may author new spec files and new page objects (`artwork.spec.ts`,
`band-info.spec.ts`, `export.spec.ts`, `library.spec.ts`, `panner-law.spec.ts`,
`e2e/pages/library-page.ts`, `e2e/support/seed-artwork.ts`) and the fixtures (005:T009) before
the 004 merge, but edits to existing page objects and the Playwright storage state (004 qa owns
the level fixture) should wait for it. devops and quality-gate may start now; expect a small
merge with the 004 devops/quality-gate branches in `ci.yml`, `scripts/test-all.*` and
`docs/quality/SIG-REPORT.md` (CTO resolves).

## Open issues / requests

- **devops (005:T008/007:T008)**: `CONTRACTS_DIR` and `scripts/test-all.{ps1,sh}` ->
  `specs/007-export-library/contracts` (not done here: 003 also left it to devops; CI/scripts are
  devops-owned); add `python specs/007-export-library/contracts/generate_render_vectors.py --check`
  to the contracts step if you like (pure stdlib).
- **stem-separation**: replace the placeholder `library` default in `/health` with
  `LibraryReader.status()` and update the `/health` literals in `test_jobs_api.py`/`test_smoke.py`
  accordingly; report `onlineLookups` from `settings.online.lookups_enabled`.
- **backend**: un-stage ids from `PendingArtworkOperations`/`PendingAssetOperations` as each
  endpoint is served; `ServedOpenApiDocument_ListsExactlyTheContractOperations` enforces it.
- **qa**: `e2e/support/api.ts` still names the 003 backend document in its doc comment.
- `specs/007-export-library/contracts/generate_render_vectors.py` is formatted with the worker
  ruff settings; three ruff findings remain on it (one long string, `_case` with 6 parameters),
  it is not part of a lint gate.
- plan.md/tasks.md headers of 005/007 still cite constitution 1.2.0; the 1.3.0 amendment (optional
  Claude backend, GPU-only generative services) does not affect 005 or 007.

## Next owner

The combined wave above (start from the Phase 0 commit); CTO for the integration wave.
