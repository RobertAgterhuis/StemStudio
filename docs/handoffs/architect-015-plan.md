# Hand-off: architect / 015 plan (play-along recording)

## What was built

The complete Spec Kit design of feature 015 (MoSCoW S3) on branch `015-play-along-recording`:
spec with clarifications (7 user stories, 21 FRs, 10 SCs), research (R-01..R-13), plan with
constitution check and ownership map, data model, contract deltas (backend 1.14.0-delta.1 as a
standalone OpenAPI 3.1 document, audio-engine 2.5.0, UI test ids 1.14.0, render-vectors 1.1.0,
help topics), quickstart, TDD task list (91 task ids, per lane, parallel markers) and ADR-024.
No production code and no skeleton change: the bases (012 2.3.0 / 1.11.0, 014 2.4.0 / 1.13.0)
are not merged yet, so the skeleton is Phase 0 work (T003, T004).

Key decisions (ADR-024): AudioWorklet capture in the engine's own context; loopback chirp
calibration with manual and estimated fallbacks, per device label in the backend; compensation
at capture time (`pos(f - L - P)`); record only at 100 % / 0 st, listen back at any setting;
takes stored as hidden library assets (origin `recording`), 24-bit WAV padded to song sample 0
and converted to bit-exact 24-bit FLAC by the unchanged 007 render job (no worker change); one
take strip with a sample-locked compare gate; one take per loop pass, all passes kept; upload
through Blazor stream interop (CORS stays GET-only); Chromium supported, Firefox best effort.

## Files changed

- `specs/015-play-along-recording/{spec,plan,research,data-model,quickstart,tasks}.md`
- `specs/015-play-along-recording/checklists/requirements.md`
- `specs/015-play-along-recording/contracts/{DELTA.md, recording-api.openapi.yaml, audio-engine.delta.md, ui-test-ids.delta.md, redocly.yaml}`
- `docs/decisions/ADR-024-play-along-recording-and-latency.md`
- `docs/handoffs/architect-015-plan.md`

## Tests and coverage

Documentation only; no code changed, so existing suites and coverage are unaffected.
Contract validation:
`npx @redocly/cli@2.78.0 lint --config specs/015-play-along-recording/contracts/redocly.yaml specs/015-play-along-recording/contracts/recording-api.openapi.yaml`
-> valid, 0 errors, 8 warnings (7 `no-unused-components` for the `*Additions` merge fragments and
`RecordingRecipe`, 1 `operation-4xx-response` on `listInputCalibrations`).

## Quality checks

Not applicable (no code). The plan keeps SIG limits by design (small pure units; parameter
objects) and adds dependency-direction rules (T007/T008).

## Dependencies added

None.

## Docs impact

User manual: new chapter "Play-along recording" (setup per instrument family, permissions,
calibration, loop takes, compare, export, privacy, limits). Technical manual: endpoints,
`StemStudio:Recording:MaxUploadBytes`, `StemStudio:Recording:TestLoopback` (E2E only),
`StemStudio:Recording:MaxStreamBytes`, browser support, storage layout. Task T091 (CTO).

## Open issues / requests

1. **012 / 014 engine hooks** (DELTA.md section 3): transport segments (play start, loop pass,
   end of count-in), `processingLatencySamples()` of the tempo stage, extra channels through the
   tempo stage, count-in with an explicit bar count. Please consider them in the 012/014 specs;
   otherwise 015 T015 adds them internally.
2. **ADR number**: 024 assumes feature-ordered numbering of the parallel 012-016 ADRs (021-025).
3. **render-vectors 1.1.0**: not in the ROADMAP allocation table; claimed by 015 unless 012/014
   bump it first (then the next minor).
4. **013 library care**: takes live under `assets/<assetId>/` with origin `recording` (plus a
   transient `capture.wav`); backup and storage overview should include them.
5. **014 shortcuts**: 015 needs `R` (record/stop) and `C` (compare) in the shortcut registry.
6. `DELTA.md` sits in `contracts/` as in features 004-011.

## Next owner

CTO (review), then architect Phase 0 (T001-T006) once 012 and 014 contracts are fixed.
