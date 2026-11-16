# Hand-off: architect / 016 specs (planning round)

## What was built

The complete Spec Kit design of feature 016 Instrument views (MoSCoW S1, S4, S9, S10, the
"other instruments" line, and the CTO scope addition S11 key detection + S12 capo mode): spec with
eight user stories (one per family plus "Practise as", key and capo), research with licence
checks, plan, data model, standalone additive contract deltas, quickstart, tasks (143, TDD, per
lane) and ADR-031..033. No production code; the Phase 0 skeleton is tasks T001-T008.

## Files changed

- `specs/016-instrument-views/{spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md}`
- `specs/016-instrument-views/checklists/requirements.md`
- `specs/016-instrument-views/contracts/{DELTA.md, worker-api.delta.openapi.yaml, backend-api.delta.openapi.yaml, audio-engine.delta.md, ui-test-ids.delta.md, redocly.yaml}`
- `docs/decisions/ADR-031-chord-recognition.md`, `ADR-032-singing-pitch-tracking.md`, `ADR-033-instrument-views-architecture.md`

## Tests and coverage

No code changed, so no unit tests or coverage apply. Contract validation:
`npx @redocly/cli@2.78.0 lint --config specs/016-instrument-views/contracts/redocly.yaml <worker delta> <backend delta>`
-> "Your API descriptions are valid", 15 warnings (13 `no-unused-components` for the merge-only
`*Extension` schemas, `TrackKeySummary` and the `listAssets` `channel` parameter; 2
`operation-4xx-response` for an idempotent DELETE and the static catalogue GET).

## Quality checks

n/a (documents only). Contract deltas are OpenAPI 3.1 and reference the last full documents on
`main` (worker 008 1.6.0, backend 010 1.9.0) for shared schemas.

## Dependencies added

None (planned: no new Python, .NET or npm package). Planned vendored code and weights (recorded
for the CTO, installed only in the wave):

| Name | Version | Licence | URL |
|---|---|---|---|
| BTC-ISMIR19 model code subset (vendored) | commit `2682317be668032e6e4b269ded36adaa2ad57df0` | MIT | https://github.com/jayg996/BTC-ISMIR19 |
| `btc_model.pt` (weights, model cache) | 12,154,754 B, SHA-256 `71c2c5db17e8c43b8a9a9da5db36ef2d667158c07a214eba16344c154c00bf54` | MIT | same repo, `test/` |
| `btc_model_large_voca.pt` (weights, model cache) | 12,229,576 B, SHA-256 `1673d23f8f9a55ae7f9e8b80a51da616debb22675b8d8b67ea6ce0ef37b0ab51` | MIT | same repo, `test/` |

## Docs impact

User manual: new chapter "Instrument views" (per family, key, capo, singing privacy); technical
manual: analyses lane, worklet, contracts 1.15.0/1.9.0/2.6.0, models (task T143).

## Open issues / requests

- 014 and 015 names (score view, sections, count-in, transpose state, input node, round-trip
  latency, takes) are placeholders; reconcile in Phase 0 T001 after both merge.
- ADR numbers 031-033 were chosen to avoid the parallel 012-015 specs; renumber only on collision.
- DELTA.md lives in `contracts/` (convention of 005-011), not in the feature root.

## Next owner

CTO (review), then architect Phase 0 (T001-T008) once 014 and 015 are merged.