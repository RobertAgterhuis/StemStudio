# Hand-off: architect / 019-plan (Arrangement editor, planning round)

## What was built

The complete Spec Kit design for feature 019 (MoSCoW C6), written in parallel with 012-018:

- `specs/019-arrangement-editor/spec.md` (7 user stories, 17 FRs, 8 SCs, clarifications answered
  autonomously) + `checklists/requirements.md` (all items pass);
- `research.md` (R-01..R-12, alternatives, the 012 seam and two "verify in wave" items),
  `plan.md` (constitution check PASS, structure, ownership, flows, FR -> owner map, complexity
  tracking), `data-model.md` (storage, document, validation codes, the timeline law, worker and
  export shapes, limits), `quickstart.md` (17 validation scenarios), `tasks.md` (84 tasks, TDD
  pairs per lane, [P] markers, execution plan);
- `contracts/`: `DELTA.md`, `worker-api.delta.openapi.yaml` (1.10.0-delta.1),
  `backend-api.delta.openapi.yaml` (1.18.0-delta.1), `audio-engine.md` (2.8.0 delta),
  `ui-test-ids.delta.md` (1.18.0), `render-vectors.json` **1.2.0** + `generate_render_vectors.py`,
  `redocly.yaml`;
- `docs/decisions/ADR-028-arrangement-timeline-model.md` (Proposed; number = 021 + (019 - 012) to
  avoid collisions with the parallel rounds, the CTO renumbers if needed).

CTO allocation change applied: render-vectors **1.2.0** (containing 015's 1.1.0). The generator
copies every section of the newest base it finds (015's file when it exists, else 007's 1.0.0)
and records `baseVersion`; Phase 0 T004 regenerates on the merged 1.1.0.

No production code and no skeleton change in this round: the bases (012-018 contracts) are not
merged yet, so contract code (`schemas.py`, Contracts DTOs, `types.ts` stubs) is Phase 0
(T001-T006).

## Files changed

- `specs/019-arrangement-editor/**` (new)
- `docs/decisions/ADR-028-arrangement-timeline-model.md` (new)
- `docs/handoffs/architect-019-plan.md` (this file)

## Tests and coverage

No component code changed, so no coverage numbers apply. Contract artefacts checked:

- `python specs/019-arrangement-editor/contracts/generate_render_vectors.py --check` -> exit 0
  (17 resolve, 6 spans, 3 samples, 1 clock, 1 metronome case with 64 clicks).
- `npx @redocly/cli@2.78.0 lint --config specs/019-arrangement-editor/contracts/redocly.yaml <both deltas>`
  -> both valid OpenAPI 3.1; 1 warning (`no-unused-components` for `ArrangementSnapshot`, which
  is referenced only from the `x-stemstudio-schema-additions` root extension; expected).

## Quality checks

- Generator: `ruff check` (line length 100) clean, `ruff format --check` clean, `mypy --strict`
  "no issues found", `tools/quality/sig_check.py`: 49 units, **0 errors**, 3 warnings
  (`bar_lines` CC 6, `_document_errors` CC 9, `spans_cases` 26 LOC; all within the hard limits).

## Dependencies added

None (name | version | licence | url: -).

## Docs impact

User-visible: a new Arrange panel in the mixer (Advanced), a Beginner read-only chip, an export
option, mix badges, a disabled record button while arranged. The CTO writes the user manual
chapter and the technical manual section in T082 (outline in tasks.md T082).

## Open issues / requests

1. **012 engine shape** (research R-04): the run scheduler assumes sources with `playbackRate`;
   if 012 reads PCM in a worklet, the fallback seam (TimelineMap read API, same vectors) is used.
   The audio-engine owner confirms in T032.
2. **014 sections endpoint name**: assumed `GET /api/tracks/{trackId}/sections` with start/end
   seconds; Phase 0 T003 aligns names with the merged 014 contract (behaviour unchanged).
3. **ADR number**: ADR-028 chosen to avoid collisions; renumber at merge if 012-018 used other
   numbers.
4. **Merge order**: worker 1.10.0 = 016's 1.9.0 + delta; backend 1.18.0 = 018's 1.17.0 + delta;
   engine 2.8.0 = 017's 2.7.0 + delta; UI ids 1.18.0 = 018's 1.17.0 + delta; vectors 1.2.0 =
   015's 1.1.0 + `timeline`. All 019 edits are additive; `RenderResult.durationSeconds` maximum
   900 -> 1800 is a relaxed response bound.

## Next owner

CTO (review), then architect Phase 0 (T001-T006) when 019 starts.
