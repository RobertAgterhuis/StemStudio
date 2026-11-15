# Hand-off: architect / 002-plan

## What was built

- **Feasibility spike (measured)** on the reference machine (Core Ultra 9 285K, 24 threads,
  Docker Desktop 62.43 GiB, CPU only) in a throwaway container from the worker image; results,
  SHA-256s, URLs and commands in `specs/002-detail-separation/research.md` ("Feasibility
  measurements"). Container, volume (~6 GB) and scratch scripts removed; `worker/pyproject.toml`
  untouched.
- **Decisions** (ADR-010): drums MDX23C DrumSep 6-stem (overlap 2), vocals UVR MDX-Net KARA 2,
  other/guitar/piano MVSep Mega 53-stem BS-RoFormer run once on the sum of active instrument
  parents, folded into 8 families and re-assigned per parent with STFT power masks; energy gates,
  rest stem, model-residual / double-count / reconstruction checks with concrete constants;
  priority scheduler with per-parent checkpoints, cancel, restart reconciliation; first-use model
  download with SHA-256 verification.
- **SC-001 verdict**: met. Reference song "Hero of the Day" (4:22) took 19 min 58 s wall clock
  (19.5 min inference); scaled to 4:00 about 18.5-21 min (30-40 % headroom).
- Spec Kit artefacts: `plan.md` (Constitution Check + Complexity Tracking), `research.md`
  (D-01..D-12), `data-model.md`, `quickstart.md` (D1-D14), `tasks.md` (T001-T093),
  contracts (worker 1.1.0, backend 1.2.0, engine 2.0.0, UI test ids 1.1.0), Clarifications
  session in `spec.md`, ADR-010.

## Files changed

- `specs/002-detail-separation/{plan,research,data-model,quickstart,tasks}.md` (new)
- `specs/002-detail-separation/spec.md` (Clarifications section added)
- `specs/002-detail-separation/contracts/{worker-api.openapi.yaml, backend-api.openapi.yaml, audio-engine.md, ui-test-ids.md, redocly.yaml}` (new; full documents superseding the 001 versions)
- `docs/decisions/ADR-010-detail-separation-models-and-pipeline.md` (new)
- `docs/handoffs/architect-002-plan.md` (this note)

## Tests and coverage

No production or skeleton code was changed in this task, so build/test/coverage state is the one
of the previous commit (not re-run). Contract validation:

```text
npx @redocly/cli@2.78.0 lint --config specs/002-detail-separation/contracts/redocly.yaml \
  specs/002-detail-separation/contracts/worker-api.openapi.yaml \
  specs/002-detail-separation/contracts/backend-api.openapi.yaml
worker-api.openapi.yaml: valid, 1 warning (operation-4xx-response on GET /health, same as 001)
backend-api.openapi.yaml: valid, 2 warnings (operation-4xx-response on /health and GET /api/tracks, same as 001)
```

## Quality checks

Not applicable (documents only). Skeleton code changes (schemas.py, ports.py, config.py, DTOs,
types.ts, new worker deps) are Phase 0 tasks T001-T005 and still open.

## Dependencies added

None yet. Planned in T002 (record again when added):

| Name | Version | Licence | URL |
|---|---|---|---|
| onnxruntime | 1.32.0 | MIT | https://github.com/microsoft/onnxruntime |
| rotary-embedding-torch | 0.9.2 | MIT | https://github.com/lucidrains/rotary-embedding-torch |
| Vendored MSST inference code | commit 1f700c386e0dd763fe0a57242bb9420dce987354 | MIT | https://github.com/ZFTurbo/Music-Source-Separation-Training |

Model weights (ADR-009 exception, downloaded at first use; status, URL and SHA-256 in
research.md D-07): MDX23C DrumSep 6-stem (no licence stated), UVR MDX-Net KARA 2 (MIT per UVR,
credit required), MVSep Mega 53-stem v1 (no licence stated).

## Open issues / requests

1. **CTO decision**: Complexity Tracking lists two constitution interpretations - vendored MSST
   code excluded from SIG metrics and unit coverage (covered by an integration test instead), and
   a second `@pytest.mark.integration` test for the detail models. Please confirm or reject.
2. Standard jobs can wait up to one instrument step (~13.5 min for a 4-min song) behind a running
   detail job (allowed by FR-011; SC-006 still met). Chunk-level pre-emption is in the backlog.
3. Quality is experimental: inter-parent leakage from Demucs remains; sum-run re-assignment agrees
   with per-parent runs only to ~4.7 dB SDR on the strings family (decisions agree). UI must say so
   (FR-017).
4. Worker RAM: 16.6 GB measured for 4:22 with the unmodified `demix`; family accumulators (T049)
   should cut it to ~12 GB; a 15-min song must be measured in T092.
5. Phase 0 (T001-T005) must be executed before the wave; I can do it on request.
6. CTO: `docs/THIRD-PARTY-LICENSES.md` and README notice per research.md D-07 (T090).

## Next owner

Owner agents (the wave), per the execution plan in `specs/002-detail-separation/tasks.md`.

---

# Phase 0 (T001-T005), after CTO acceptance 2026-11-15

CTO decisions recorded: vendored-code exception "accepted by CTO 2026-11-15" in plan.md
Complexity Tracking and ADR-010; open points 2-6 accepted as documented.

## What was built

- **T001** worker contract code: `schemas.py` (`CONTRACT_VERSION = "1.1.0"`, `ParentStemName`,
  `DetailStemName`, `DetailModelId`, `DetailJobState`, `ParentOutcomeKind`,
  `CreateDetailJobRequest`, `DetailJob`, `DetailStemInfo`, `OutcomeMetrics`, `ParentOutcome`,
  `DetailResult`); `ports.py` (`DetailAudio`, `DetailModel`, `DetailModelProvider`,
  `DetailAdapters`, `DetailModelUnavailableError`, `DetailSeparationError`,
  `DetailCancelledError`); `config.py` (`model_dir` <- `STEMSTUDIO_MODEL_DIR`, default
  `data/models`; `fake_detail_step_seconds` <- `STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS`, default 1.0,
  validated non-negative); stub `detail/__init__.py::create_detail_adapters` (raises
  `NotImplementedError`); skeleton tests `worker/tests/test_detail_contract.py` (round-trip of
  every 1.1.0 example, enums vs. contract, settings, factory stub).
- **T002** `onnxruntime==1.32.0`, `rotary-embedding-torch==0.9.2` in `worker/pyproject.toml` +
  `uv.lock`; ruff `extend-exclude`, mypy `ignore_errors`, coverage `omit` for
  `detail/vendor/**`; mypy `ignore_missing_imports` for onnxruntime, rotary_embedding_torch, einops.
- **T003** Contracts DTOs: `DetailPhaseDto` (+ `DetailPhaseDto.None`), `DetailPhaseStatusDto`,
  `ParentOutcomeDto`, `ParentOutcomeKindDto`, `DetailStemDto`, `DetailChannelSettingsDto`,
  `TrackDto.Detail`, `MixSettingsDto.Expanded` / `.DetailChannels` (init properties, default empty),
  `DetailNames` (parents, per-parent allowed names, `AllowedFor`), `ApiRoutes.StartDetail`,
  `.CancelDetail`, `.DetailStem`, `ContractLimits.MaxExpandedParents/MaxDetailChannels/
  MaxDetailStemsPerParent`; skeleton tests `tests/StemStudio.Api.Tests/Contract/DetailContractTests.cs`
  (1.2.0 examples round-trip through the DTOs and validate against the schema). Api.Tests now link
  `specs/002-detail-separation/contracts/backend-api.openapi.yaml`.
- **T004** `src/audio-engine/src/types.ts` 2.0.0 (`ParentStemName`, `DetailStemName`, `ChannelId`,
  `DetailStemSource`, `DetailGroup`, `LoadTrackRequest.groups?`, `setExpanded`, `ChannelId` in
  `setChannel` / `MeterTarget`); `ENGINE_CONTRACT_VERSION = '2.0.0'`; `setExpanded` stub rejects
  "not implemented".
- **T005** verification below.

## Minimal changes outside the architect lane (needed to keep the skeleton green)

| File (owner) | Change | Why |
|---|---|---|
| `worker/tests/api/test_jobs_api.py` (stem-separation), `worker/tests/test_smoke.py` | health `contractVersion` literal `1.0.2` -> `1.1.0` | contract version bump |
| `tests/StemStudio.Api.Tests/StemStudio.Api.Tests.csproj` (skeleton) | contract link -> 002 | responses now carry `detail`, `expanded`, `detailChannels`, which the 001 schema forbids (`additionalProperties: false`) |
| `tests/StemStudio.Api.Tests/Contract/BackendContractTests.cs` (backend) | operation list includes `startDetail`, `cancelDetail`, `getDetailStem`; summary says 1.2.0 | contract self-check |
| `tests/StemStudio.Api.Tests/Contract/BackendContractTests.StemsAndMixes.cs` (backend) | `PendingDetailOperations` excluded from the served-operations comparison | the three operations are implemented in T054/T068; remove entries as they are served |
| `tests/StemStudio.Api.Tests/Mixes/MixEndpointsTests.cs` (backend) | duplicate test copies `Expanded`/`DetailChannels` into the expected record like `Channels` | record equality compares list references |
| `src/audio-engine/src/index.ts`, `src/engine.ts` (audio-engine) | version literal; `setExpanded` stub | agreed in T004 |
| `src/audio-engine/src/mixer.ts` (audio-engine) | `analyser()` looks the target up in a `ReadonlyMap<string, Channel>` view | `MeterTarget` now includes detail ids; they resolve to "not loaded" until groups exist |
| `src/StemStudio.Web/Audio/EngineMethods.cs` (frontend) | `ContractVersion = "2.0.0"` | `EngineContractDriftTests` guards the version against `types.ts` |

`TrackDto.Detail` is **not** a C# `required` member: it defaults to `DetailPhaseDto.None` and is
always serialized, which satisfies the JSON contract (`detail` required) without touching the
Api `TrackMapper` or the Web/Api test builders. The backend maps the real phase in T054.

## Tests and coverage (T005, all run on this commit)

```text
dotnet build StemStudio.slnx -c Release            0 Warning(s), 0 Error(s)
dotnet format StemStudio.slnx --verify-no-changes  OK
dotnet test StemStudio.slnx -c Release -p:CollectCoverage=true  (per-component gate 90 % line+branch)
  Domain.Tests       173 passed   line 97.94 %  branch 97.77 %
  Persistence.Tests   29 passed   line 99.17 %  branch 100 %
  Web.Tests          198 passed   line 99.85 %  branch 93.78 %
  Api.Tests          262 passed   line 99.87 %  branch 97.69 %   (gates Api + Contracts)
worker: uv run pytest --cov      440 passed, 2 deselected; line+branch 100.00 % (gate 90 %)
        ruff check / ruff format --check / mypy src tests (strict): clean
audio-engine: npm test 122 passed; test:coverage 100 % statements/branches/functions/lines;
        npm run lint, npm run typecheck: clean; npm run build OK
Redocly 2.78.0 lint (002 worker + backend): valid, 3 warnings (operation-4xx-response, same as 001)
uv run --project tools/quality python tools/quality/run_all.py:
        365 files, 2644 units, 17 clones, 0 errors, 106 warnings -> exit 0
        (new warning "detail is not in the dependency map": fixed by quality-gate T007)
```

Red -> green: `090ccf9` (failing worker skeleton test) -> `d3e7ee5` (T001); `66a114b` (T002);
`b10337a` (T003, DTOs + skeleton tests in one commit); `83c8468` (failing engine test) -> `5ca588d` (T004);
`EngineMethods.ContractVersion` and this note in the T005 commit.

## Dependencies added

| Name | Version | Licence | URL |
|---|---|---|---|
| onnxruntime | 1.32.0 | MIT | https://github.com/microsoft/onnxruntime |
| rotary-embedding-torch | 0.9.2 | MIT | https://github.com/lucidrains/rotary-embedding-torch |
| flatbuffers (transitive, onnxruntime) | 25.12.19 | Apache-2.0 | https://github.com/google/flatbuffers |
| protobuf (transitive, onnxruntime) | 7.36.2 | BSD-3-Clause | https://github.com/protocolbuffers/protobuf |

(`sympy`, `packaging`, `numpy` and `einops` were already locked.)
