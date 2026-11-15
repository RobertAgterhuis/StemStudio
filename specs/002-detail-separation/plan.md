# Implementation Plan: Detail Separation

**Branch**: `002-detail-separation` | **Date**: 2026-11-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-detail-separation/spec.md`; constitution 1.1.0
(model weights exception, ADR-009); research `docs/research/detail-separation-models.md`.

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). Binding decisions are in
ADR-010 (`docs/decisions/ADR-010-detail-separation-models-and-pipeline.md`); measurements and
thresholds in [research.md](research.md).

## Summary

After the standard 6-stem separation a track is playable; the worker then runs a lower-priority
**detail job** on the cached standard stems: a parent gate skips (almost) silent stems, MDX23C
DrumSep splits drums (kick, snare, toms, hihat, cymbals), UVR MDX-Net KARA 2 splits vocals (lead,
backing), and the MVSep Mega 53-stem BS-RoFormer runs **once on the sum of the active
other/guitar/piano stems**, folded into instrument families and re-assigned to each parent with
STFT power masks. A sub-stem audibility gate keeps only audible sub-stems, a `rest` stem makes
every split parent sum back exactly, and a model-residual check plus a reconstruction check
(<= -30 dB) mark implausible splits as "not reliable". Each parent step is checkpointed; a waiting
standard job always runs before the next detail step. The backend stores the detail phase on the
track, serves detail stems like stems, auto-queues the phase after standard completion and offers
Split further / Retry / Cancel. The mixer shows grouped detail strips per parent with
expand/collapse that swaps the parent's audio for its detail stems sample-aligned; mixes store
expansion and detail channel settings, old mixes load unchanged.

Measured on the reference machine (Intel Core Ultra 9 285K, 24 threads, CPU only): a full
4:22 song needed **19 min 58 s** for the detail phase (drums, vocals and guitar active); scaled
to 4:00 that is about 18.5-21 minutes, so SC-001 (<= 30 min) is met with about 30-40 % headroom
(research.md D-01..D-03).

## Technical Context

**Language/Version**: unchanged from 001 (C# 14 / .NET 10; Python 3.12; TypeScript 6.0).

**Primary Dependencies (new)**: worker `onnxruntime` 1.32.0 (MIT, KARA 2), `rotary-embedding-torch`
0.9.2 (MIT, BS-RoFormer); vendored MIT inference code from ZFTurbo
Music-Source-Separation-Training at commit `1f700c386e0dd763fe0a57242bb9420dce987354`
(`bs_roformer`, `mdx23c_tfc_tdf_v3`, overlap-add `demix`; licence header + attribution kept) and
the MDX-Net STFT wrapper re-implemented after python-audio-separator (MIT, attribution).
`einops` is already locked (via demucs). **Not added**: `python-audio-separator` (hard dep
`diffq` is CC BY-NC, ADR-009), `omegaconf`/`ml_collections` (hyper-parameters become Python
constants), `beartype` (decorators removed when vendoring). No new .NET or npm packages.

**Model weights (downloaded at first use, never committed)**: MDX23C DrumSep 6-stem
(437,652,699 B), UVR_MDXNET_KARA_2.onnx (52,786,726 B), MVSep Mega 53-stem v1 (1,368,919,887 B);
URLs, SHA-256 and licence status in research.md D-07.

**Storage**: SQLite gains two JSON columns on `Tracks` (`DetailPhase`) and two on `Mixes`
(`Expanded`, `DetailChannels`) via EF migrations; detail stems under
`<DataDir>/tracks/<id>/detail/<parent>/<name>.flac`; worker `detail-jobs/*.json` and
`cache/by-audio/<sha>/{detail.partial,detail}/`; models in `STEMSTUDIO_MODEL_DIR` (compose:
`/models`, the existing `model-cache` volume). See [data-model.md](data-model.md).

**Testing**: as 001. Worker gating/reconstruction tests on synthetic fixtures generated in the
tests; detail models behind a `DetailModel` port with fakes; one additional
`@pytest.mark.integration` test runs the three real detail models on a 10 s fixture (constitution
I amended interpretation: "exactly one integration test per real model family" is recorded in
Complexity Tracking). Playwright `@detail` E2E with `STEMSTUDIO_SEPARATOR=fake` (fake detail).

**Target Platform**: unchanged. Docker Desktop memory >= 20 GB for real detail separation
(reference machine: 62 GB).

**Performance Goals**: SC-001 detail phase <= 30 min for a 4-min song (measured: 18.5-21
min); SC-004 controls <= 50 ms, alignment <= 1 ms after 50 loops; SC-006 standard job starts
<= 5 s after the current parent step; SC-007 cached detail <= 5 s.

**Constraints**: detail work never blocks standard jobs for more than one parent step (measured:
instrument step 14.7 min for 4:22, about 13.5 min for 4:00; see Complexity Tracking); peak worker RAM
<= 16 GB for 4 min (measured 16.6 GB for 4:22 with all 53 accumulators; family folding lowers it), <= 24 GB for 15 min (family folding inside `demix`, research.md D-06).

**Scale/Scope**: up to ~15 extra FLAC files per track (~3x disk), 5 splittable parents.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Every owner pairs test task -> implementation task (tasks.md). Gating, folding, reassignment, reconstruction and scheduling are pure functions/classes tested on synthetic signals; models behind `DetailModel` with fakes; restart/cancel/priority tested with fake clock and fake models; bUnit for grouped strips; Vitest for groups/expand/gate; Playwright `@detail` with fake detail. Vendored code is excluded from unit coverage and exercised by the integration test (Complexity Tracking). | PASS (with tracked exception) |
| II | SIG 4 stars | New worker package `detail/` split into small modules (gating, folding, reassign, checks, registry, download, runner steps); scheduler is a separate class; backend detail logic in its own `Detail/` folder; engine `groups.ts`. Vendored MSST code is third-party and excluded from SIG metrics like `Migrations/` (Complexity Tracking). | PASS (with tracked exception) |
| III | Contracts first | Worker API 1.1.0, backend API 1.2.0 (both additive, Redocly-valid OpenAPI 3.1), engine contract 2.0.0 (major: gate semantics), UI test ids 1.1.0, written before tasks; `schemas.py`, `StemStudio.Contracts`, `types.ts` follow in Phase 0 (architect). ADR-010 records the design. | PASS |
| IV | Open source, lawful use | New libraries MIT (`onnxruntime`, `rotary-embedding-torch`, vendored MSST, re-implemented audio-separator MDX wrapper). `python-audio-separator`/`diffq` not used. Weights under the ADR-009 exception: listed with status, URL, SHA-256; downloaded into the model-cache volume at first use; README + THIRD-PARTY-LICENSES notice; none of the chosen weights explicitly forbids local non-commercial use. | PASS |
| V | Simplicity and honesty | One extra job type in the existing worker (no new service); energy gating instead of a tagger model; no settings switch (spec). Limitations shown in the UI (FR-017) and quality is labelled experimental; SC-001 measured, not assumed; Web Audio timing unchanged (expand = gain swap on `currentTime`). | PASS |
| - | Tech & security | Stack unchanged. Detail names are enums (safe path components); model URLs are constants (no user input reaches a download); SHA-256 verification; no secrets; subprocesses unchanged. | PASS |
| - | Workflow | Spec Kit flow; disjoint owner paths; hand-off per checkpoint (ADR-008). | PASS |

**Post-design re-check (after Phase 1)**: PASS with the two exceptions in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/002-detail-separation/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/
    ├── worker-api.openapi.yaml    # 1.1.0 (full document; supersedes 001's 1.0.2)
    ├── backend-api.openapi.yaml   # 1.2.0 (full document; supersedes 001's 1.1.0)
    ├── audio-engine.md            # 2.0.0
    ├── ui-test-ids.md             # 1.1.0
    └── redocly.yaml
```

The 002 contract files are full documents and become the binding versions; contract tests and CI
switch their paths from `specs/001-stemstudio-mvp/contracts/` to
`specs/002-detail-separation/contracts/` (tasks T00x). 001's files stay as the historical record.

### Source Code (additions)

```text
worker/src/stemstudio_worker/
├── ports.py                 # + DetailModel, DetailModelProvider protocols, detail errors (architect)
├── schemas.py               # + DetailJob, DetailResult, ParentOutcome, names (architect)
├── config.py                # + model_dir, fake_detail_step_seconds (architect)
├── detail/                  # stem-separation agent
│   ├── __init__.py          # create_detail_pipeline(settings) factory
│   ├── names.py             # parent/detail enums, family tables, display order
│   ├── gating.py            # frame levels, parent gate, sub-stem gate (pure numpy)
│   ├── folding.py           # 53 Mega outputs -> families; DrumSep 6 -> 5
│   ├── reassign.py          # STFT power-mask re-assignment of sum-run families to parents
│   ├── checks.py            # model residual, rest stem, reconstruction check
│   ├── registry.py          # ModelSpec constants (URL, size, SHA-256, licence status)
│   ├── download.py          # first-use download, .part + SHA-256 verify, offline error
│   ├── runtimes.py          # real model runtimes (lazy torch/onnxruntime imports)
│   ├── fake.py              # fake detail models for STEMSTUDIO_SEPARATOR=fake
│   ├── steps.py             # plan of parent steps, per-step execution + checkpoint
│   └── vendor/msst/         # vendored MIT code (LICENSE, NOTICE, bs_roformer, mdx23c, demix)
├── jobs/
│   ├── scheduler.py         # priority scheduler: standard queue first, detail steps between
│   ├── detail_store.py      # detail-jobs/*.json
│   ├── detail_service.py    # create (idempotent per audio), cancel
│   └── detail_runner.py     # runs one detail step; completes/fails/cancels the job
└── api/detail_routes.py     # /v1/detail-jobs endpoints

src/StemStudio.Contracts/Tracks/{DetailPhaseDto,ParentOutcomeDto,DetailStemDto,...}.cs   (architect)
src/StemStudio.Contracts/Mixes/DetailChannelSettingsDto.cs; MixSettingsDto(+Expanded,+DetailChannels)
src/StemStudio.Domain/Tracks/Detail/{DetailPhase,ParentOutcome,DetailStem,DetailNames}.cs
src/StemStudio.Domain/Mixes/DetailChannelSettings.cs (+ Mix rules)
src/StemStudio.Persistence/Migrations/*_AddDetailPhase.cs, *_AddMixDetailSettings.cs
src/StemStudio.Api/Detail/{DetailEndpoints,DetailService,DetailSynchronizer,DetailJobApplier}.cs
src/StemStudio.Api/Worker/{IWorkerClient (+detail methods), WorkerDetailJob, WorkerDetailResult}.cs
src/StemStudio.Api/Storage/DetailStemFileStore.cs
src/StemStudio.Web/Components/Pages/TrackDetail.Detail.razor(.cs)   # detail section
src/StemStudio.Web/Components/Mixer/{StemGroup.razor, ExpandToggle.razor}
src/StemStudio.Web/Mixer/{MixerState.Detail.cs, DetailChannelState.cs}
src/audio-engine/src/{types.ts (architect), groups.ts, gate.ts}
tests/fixtures/generate_detail_fixtures.py (qa); e2e/tests/detail.spec.ts (qa)
```

**Dependency direction**: unchanged. Worker: `api -> jobs -> ports <- detail`; `detail` imports
only `ports`, `schemas`, `config` (and its own `vendor`); `jobs` never imports `detail`
(composition in `main.py`). .NET: `Api.Detail -> Domain, Persistence, Worker client`.

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/002-*/**`, ADR-010, `ports.py`, `schemas.py`, `config.py` (new fields), `pyproject.toml`/`uv.lock` (new deps), `StemStudio.Contracts/**`, `src/audio-engine/src/types.ts` | architect |
| `worker/src/stemstudio_worker/{detail,jobs,api}/**`, `main.py`, matching tests | stem-separation |
| `src/StemStudio.{Domain,Persistence,Api}/**`, matching tests | backend |
| `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | frontend |
| `src/audio-engine/**` (except `types.ts`, configs) | audio-engine |
| `tests/fixtures/**`, `e2e/**`, `docs/testing/**` | qa |
| `docker-compose.yml`, `docker/**`, `.github/**` | devops |
| `tools/quality/**` (vendor exclusion) | quality-gate |
| `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md` | CTO |

audio-ingest and beat-analysis have no work in this feature.

## Key flows

1. **Auto-queue**: `TrackJobApplier` marks the track `completed`, then sets
   `DetailPhase = Queued` (no worker id yet) in the same save. `DetailSynchronizer` (same
   `JobSyncService` loop) submits `POST /v1/detail-jobs {standardJobId}`; unreachable worker ->
   retried next poll.
2. **Worker scheduling**: `PriorityScheduler` thread loop: run a waiting standard job if any;
   else run the next step of the oldest active detail job (step 0 = plan/gate; one step per
   parent group; last = finalise). A cancelled flag is checked by the progress callback per
   inference chunk (raises `DetailCancelled`).
3. **Detail step (instrument group)**: sum of active parents among other/guitar/piano -> Mega
   53 (`demix` with family accumulators) -> families -> STFT power-mask share per parent -> per
   parent: sub-stem gate, rest, checks -> checkpoint each parent.
4. **Sync**: `GET /v1/detail-jobs/{id}` ~1 s; `completed` -> result -> download every detail
   stem (temp + rename) -> `DetailPhase.Complete(parents)`; `failed`/`cancelled` mapped; worker
   404 -> `failed` "interrupted by restart".
5. **Mixer**: Web passes `groups` (from `track.detail.parents` with outcome `split`) to
   `loadTrack`; Expand -> `setExpanded(parent, true)` (lazy load) -> strips of the group appear;
   settings sent with `setChannel("drums.snare", ...)`.
6. **Mix persistence**: `MixSettings.expanded` + `detailChannels`; loading an old mix -> both
   empty -> all collapsed.

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| Vendored third-party code (`detail/vendor/msst/**`, ~900 LOC) excluded from SIG metrics and from the 90 % unit-coverage gate | MIT inference code for BS-RoFormer and MDX23C cannot be rewritten to SIG limits without risking numerical drift against the published weights | Installing MSST as a package drags in demucs 4.0.0, torchaudio, wandb, GUI deps; `python-audio-separator` depends on CC BY-NC `diffq` (ADR-009). Coverage of vendored code comes from the marked integration test instead; our wrapper code around it stays under all gates |
| A second `@pytest.mark.integration` test (detail models) besides the Demucs one | Constitution I says "exactly one marked integration test runs the real separation model"; the detail models are separate model families that unit fakes cannot validate | Folding the detail models into the existing Demucs integration test would make it a 10+ minute test and mix two concerns; recorded here and in ADR-010 as an interpretation ("one per model family") for the CTO to confirm |
| A standard job may wait up to one instrument step (~13.5 min for a 4-min song, measured) | FR-011 allows finishing the current parent step; chunk-level pre-emption would discard up to ~14 min of work or need resumable `demix` state | Pre-emption mid-step adds a resumable inference state machine; revisit if users report long waits (backlog). SC-006 is still met (start within 5 s after the step) |
| Mega 53-stem runs once on the sum of active instrument parents, re-assigned by STFT masks | Running it per parent costs ~13.5-18 min per parent for a 4-min song and breaks SC-001 when 2-3 parents are active | Per-parent runs are exact but infeasible within SC-001; a single combined "instruments" group would break the per-parent expand rule of FR-012. When only one instrument parent is active it is run directly (no re-assignment) |
