# Hand-off: architect / 023 design (lyric writing assistant)

## What was built

Design only (no product code) for feature 023, owner request 2026-11-16: on-demand lyric writing
help and spelling/grammar/flow checks in Dutch and English for own 010 lyrics sheets, with poetic
licence always allowed and proposed by the assistant. Spec Kit set (spec, plan, research,
data-model, quickstart, tasks, checklist), contract deltas, model output schema, lyric evaluation
set, ADR-037 (accepted 2026-11-16 with the owner answers), MoSCoW S13 and ROADMAP row and version
reservation.

Owner answers recorded (2026-11-16, second commit): **Q1 yes** - own-work override of
`not-own-lyrics` with explicit confirmation (spec US6/FR-031, data-model 2.3, endpoints
`confirmOwnWork`/`revokeOwnWork`, problem `override-not-applicable`/`statement-outdated`, ui ids
`lyric-own-work-*`, eval L-V3..L-V5 and agent case S5, tasks T074-T078); **Q2 no for now** -
decision point T080 if Dutch grammar recall < 60 %; **Q3 yes** - explanations in the sheet's
language (ADR-037 D11). CTO-DECISIONS lines added. Task count 73 -> **79**.

Key decisions (ADR-037): tool-less assistant inside `StemStudio.Agent` reusing the 011 backends
(Ollama `qwen3.5:9b` default, CPU fallback `4b`, Claude opt-in with a separate one-time notice);
deterministic local layer first (WeCantSpell.Hunspell + OpenTaal/SCOWL, CMUdict, Dutch syllable and
rhyme rules, licence lexicon, intentional marks, personal words), so spelling, syllables and rhymes
work without a model; no LanguageTool (measured follow-up, owner Q2); anchored, persisted
suggestions with revisions, re-anchoring and one-step undo; JSON-schema output with deterministic
verification of rhyme/syllable claims; agent tools carry ids only, `apply_lyric_suggestions` always
needs approval (`lyrics-edit`), `lyrics-not-from-user` unchanged; 006 lyrics never accepted,
sheet-level `not-own-lyrics` guard and verbatim output guard against library lyrics; imitation guard
on instructions.

## Files changed

- `specs/023-lyric-writing-assistant/spec.md`, `plan.md`, `research.md`, `data-model.md`,
  `quickstart.md`, `tasks.md`, `checklists/requirements.md`
- `specs/023-lyric-writing-assistant/contracts/DELTA.md`, `backend-api.delta.openapi.yaml`
  (1.23.0-delta.1), `redocly.yaml`, `ui-test-ids.delta.md` (1.23.0), `tool-catalogue.delta.md`
  (1.2.0), `lyric-assist-output.schema.json` (1.0.0)
- `specs/023-lyric-writing-assistant/evaluation/lyric-eval-set.md` (1.0.0: 32 nl + 32 en, 8
  guardrail/injection, 2 performance)
- `docs/decisions/ADR-037-lyric-writing-assistant.md`, `docs/decisions/CTO-DECISIONS-2026-11-15.md` (3 lines)
- `docs/MoSCoW.md` (S13 + decision line), `docs/ROADMAP.md` (feature row, allocation row, note)

## Tests and coverage

No code; no tests run. Contract checks:
- `npx @redocly/cli@2.78.0 lint --config specs/023-lyric-writing-assistant/contracts/redocly.yaml
  specs/023-lyric-writing-assistant/contracts/backend-api.delta.openapi.yaml` -> "Your API
  description is valid", 0 errors, 2 documented warnings (`operation-4xx-response` on
  `getLyricAssistStatus`; `no-unused-components` on `LyricAssistEvent`). Re-run after the override
  endpoints were added: same result (valid, 0 errors, 2 warnings).
- `lyric-assist-output.schema.json`: Draft 2020-12 meta-schema check passed (python `jsonschema`);
  a valid sample validates, an invalid type is rejected.

## Quality checks

Design follows SIG limits by construction (small pure Domain services, parameter objects); checked
by quality-gate in T071.

## Dependencies added (for THIRD-PARTY-LICENSES, at Phase 0 T006)

| name | version | licence | url |
|---|---|---|---|
| WeCantSpell.Hunspell | 7.0.1 (re-check >= 14 days at pin) | MPL-1.1 (of MPL/GPL/LGPL tri-licence) | https://github.com/aarondandy/WeCantSpell.Hunspell |
| OpenTaal Hunspell nl (data) | pin at Phase 0 | BSD-3-Clause (of BSD-3 / CC BY 3.0) | https://github.com/OpenTaal/opentaal-hunspell |
| SCOWL Hunspell en_US/en_GB (data) | pin at Phase 0 | MIT-like (SCOWL Copyright file) | https://github.com/en-wl/wordlist |
| CMU Pronouncing Dictionary (data) | pin at Phase 0 | BSD-2-Clause-style (CMU) | https://github.com/cmusphinx/cmudict |

## Docs impact

User manual: new chapter "Lyric assistant" (check, poetic licence and sensitivity, intentional
marks, my words, devices, writing help, syllables and rhythm target, agent commands, privacy and
Claude notice, own lyrics only, limits). Technical manual: vendored dictionaries and licences, lyric
eval suite, fake scripts for E2E, no new container.

## Open issues / requests

- Versions: 022 had not allocated when 023 was written; 023 reserved backend-api/ui-test-ids
  **1.23.0**, catalogue **1.2.0** (1.1.0 left for 022), eval set 1.1.0. The CTO aligns with 022's
  final choice (T010).
- ADR number 037 is provisional (036 left for 022).
- Constitution: main shows 1.3.1; the design relies on 1.4.0 from the claude-key branch (only its
  Claude key handling; no new amendment is needed because the assistant runs in the agent runtime).
- SC-004 latency is an estimate; T008 measures it on the reference machine (no model was run for
  this design).
- Owner questions Q1-Q3 answered 2026-11-16 (plan.md "Owner decisions"); T080 is the remaining
  LanguageTool decision point after the eval run.

## Next owner

CTO (review), then architect Phase 0 (T001-T011).
