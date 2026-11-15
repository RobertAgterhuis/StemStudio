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

CTO (review plan + ADR-010, decide item 1), then architect for Phase 0 (T001-T005), then the wave.
