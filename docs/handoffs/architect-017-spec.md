# Hand-off: architect / 017 spec and design

## What was built

Complete Spec Kit design for feature 017 Mixer effects and controllers (MoSCoW C2, C3):
spec with recorded decisions, requirements checklist, research (16 items), plan, data model,
contract deltas (backend-api 1.16.0 as a standalone OpenAPI 3.1 fragment, audio-engine 2.7.0,
ui-test-ids 1.16.0, help topics), DELTA.md, quickstart, tasks (108, TDD, per lane, `[P]`
markers) and ADR-026. Design only; no production code (the Phase 0 skeleton is tasks T003, T005,
T006).

Key decisions (ADR-026): native Web Audio nodes; pre-fader EQ -> compressor inserts and one
post-fader reverb send bus with generated, versioned IRs; practice EQ Focus/Make room for seven
instrument families; built-in presets as engine data; effects as optional mix members (old mixes
unchanged); exports with effects pre-rendered in the browser by the same graph builder and encoded
by the unchanged 007 worker render (no worker-api change); Web MIDI module in the engine package,
user-initiated, no SysEx, coalesced events to .NET as the single writer; backend profiles per
device with slot/bank strip targets; Beginner practice EQ only.

## Files changed

- `specs/017-effects-controllers/{spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md, DELTA.md}`
- `specs/017-effects-controllers/checklists/requirements.md`
- `specs/017-effects-controllers/contracts/{backend-api.delta.openapi.yaml, redocly.yaml, audio-engine.delta.md, ui-test-ids.delta.md, help-topics.md}`
- `docs/decisions/ADR-026-mixer-effects-and-midi-controllers.md`
- `docs/handoffs/architect-017-spec.md`

## Tests and coverage

No code changed, so no test or coverage run applies to this task. Contract validation:
`npx @redocly/cli@2.78.0 lint --config specs/017-effects-controllers/contracts/redocly.yaml specs/017-effects-controllers/contracts/backend-api.delta.openapi.yaml`
-> "Your API description is valid", 3 warnings (`no-unused-components` for `ChannelEffects`, `MixEffects`
and `ExportEffects`; they attach to existing schemas when T002 merges
the full document).

## Quality checks

Not applicable (documents only). SIG-relevant structure is planned (small modules, parameter
objects) in plan.md.

## Dependencies added

None.

## Docs impact

User manual: effects, practice EQ, export with effects, controllers, browser support. Technical
manual: effect chain, CPU budget, pre-render flow and limits, MIDI architecture, data model.
Tasks T108-T110 (CTO).

## Open issues / requests

1. ADR number 026 assumes 012-016 take 021-025; renumber at merge if needed.
2. Integration items with 012 (speed/transpose targets, tempo in exports), 014 (command ids),
   015 (`effects` on take strip settings), 013 (back up `ControllerProfiles`, skip `Prerenders`):
   plan.md "Integration".
3. Spike T007 decides whether EF Core optional nested complex types in JSON work for old rows.
4. Agent (011) exports stay without effects; a later agent catalogue version may add them.

## Next owner

architect (Phase 0, T001-T008) once 012-016 contracts are merged; then the lanes per tasks.md.
