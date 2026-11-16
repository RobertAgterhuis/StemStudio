# Hand-off: architect / 011 Phase 0 part 2 (AI composer agent) - T002 final, T003, T005, T006

Base: local `main` 041ca70 (001-010 delivered, 011 Phase 0 part 1, ROADMAP 012-020). Branch
`011-ai-agent`. Part 1: `docs/handoffs/architect-011-phase0-part1.md`. Specs 012-020, worker,
generator, audio engine and the Web app were not touched. No LLM was called; Ollama was queried
read-only (`/api/version`, `/api/tags`, `/api/show`), nothing was pulled.

## What was built

### Contract versions (docs/ROADMAP.md row 011)

| Document | Version | Content |
|---|---|---|
| `specs/011-ai-composer-agent/contracts/backend-api.openapi.yaml` | **1.10.0** (new) | 010's 1.9.0 + `agent-api.openapi.yaml` 1.10.0-delta.1, merged unchanged (17 operations, tag `agent`, parameters, `AgentConflict`/`AgentUnprocessable`, all agent schemas); the four components the delta shares with 1.9.0 (`ProblemDetails`, `ValidationProblemDetails` - identical - and the responses `ValidationProblem`, `NotFound` - text-only differences) keep their 1.9.0 definitions; changelog line 1.10.0 |
| `contracts/ui-test-ids.md` | **1.10.0** (new) | 1.9.0 + the 011 delta unchanged + `agent-approval-effect`, `agent-approval-estimate`, `nav-settings`; `/settings` is a new page (009/010 added none); help topics `agent-panel`, `agent-approvals`, `agent-backends` |
| `contracts/tool-catalogue.md` | **1.0.0 final** | 57 tools (42 + 15 for 009/010); stays 1.0.0 as allocated in the ROADMAP (it was never released; part 1 had planned 1.1.0) |
| `evaluation/eval-set.md` | **1.0.0 final** | cases 29-30 scored (30 of 30: 16 nl, 14 en), fixture styles/generation/voice data, supplementary guardrail cases S1-S3 (not in SC-001, must pass) |
| `agent-script.schema.json` | 1.0.0 | unchanged |
| worker-api / generator-api / audio-engine | 1.6.0 / 1.1.0 / 2.1.0 | unchanged - no audio-engine 2.2.0 needed (the agent drives the mixer through `MixerState`) |

`agent-api.openapi.yaml` and `ui-test-ids.delta.md` are kept (marked "merged") for the review
history and the merge test. One description-only change in both API documents:
`PageContext.page` lists the 009/010 pages (`styles`, `generations`, `lyrics`, `songs`, `voices`).

### 009/010 tools (catalogue "Feature 009" / "Feature 010")

- 009 (10): `get_generation_status`, `list_styles`, `create_style`, `delete_style`,
  `restyle_track` (heavy), `list_generations`, `cancel_generation`, `retry_generation` (heavy),
  `import_take`, `delete_generation`; `+service generation`.
- 010 (5): `list_lyrics_sheets` (never text), `save_lyrics_sheet`, `generate_song` (heavy),
  `list_voice_profiles` (never person name / sample URL), `convert_take_voice` (heavy);
  voice tools `+service voice`.
- Guardrails: `artist-imitation` (agent pre-check on free text + backend 400 `imitation`
  authoritative; only `styleId`s), `lyrics-not-from-user` (text must occur in a user message;
  otherwise **denied**, not approvable). Consent: the agent never creates/deletes profiles or gives
  consent; conversion uses an existing current-consent profile, is always heavy, backend 409 is
  authoritative.
- 32-tool cap: groups `core` 14 / `mixer` 10 / `tracks` 13 / `library` 5 / `create` 15; per page
  context 32 / 32 / 32 / 29 (table in the catalogue; ai-agent proves it in T012).
- `wait_for_job` phases `restyle`/`generation` (`JobPhaseDto` already had them - confirmed).

### Skeleton (T004 remainder, T005 remainder)

- Ports `IStyleActions` (+ `StyleQuery`), `IGenerationActions` (+ `GenerationQuery`),
  `ISongActions` (+ text-free `LyricsSheetSummary`, `DeleteLyricsSheetAsync` for compensation),
  `IVoiceActions` (list + convert only; `VoiceProfileSummary` without personal data,
  `ConsentCurrent`), added to `IStemStudioActions` (nine groups). XML docs on all.
- Api.Tests: main backend document -> 011 1.10.0, 010 linked as `Contract/010/`
  (`ContractJson.Backend010`); `GenerationContractTests` compare 009 -> 010 via `Backend010`;
  `AgentContractTests` validate DTOs/events/routes against 1.10.0 and add the merge rule tests
  (`Backend1100_Is190PlusExactlyTheDeltaOperations`, `Backend1100_Contains190Unchanged` x 6
  sections, `Backend1100_ContainsTheDeltaUnchangedExceptTheShared190Components` x 4,
  `Delta_CollidesWithNothingInBackend190`); `PendingAgentOperations` stages the 17 agent ids in
  both the served-document and the operation-list test (agreed one-time edit; backend un-stages
  them in T032/T045/T054/T060/T070).
- `tests/StemStudio.Agent.Tests/Ports/PortRecordTests.cs` (7 cases).

### Spec Kit updates

tasks.md: T003/T005/T006 done; `[amend]` no longer blocking (constitution 1.3.0 ratified; Claude
opt-in via `ANTHROPIC_API_KEY` only, Ollama default); T077 split into T077 (009) and T077b (010);
new T078b (qa "create" E2E) and T081a (Ollama readiness, CTO); T010 contracts lint of the 011
folder; T012/T016/T034/T047/T063/T078/T079a/T081 refined; lanes table. plan.md, data-model.md
(denied -> step `failed` with the refusal; descriptor `Group`; guardrail scope), research.md F-16,
DELTA.md status table and sections 5/6.

## Lanes (tasks.md "Execution plan per owner")

| Lane | Open tasks |
|---|---|
| agent (ai-agent) | 30 |
| backend | 20 |
| frontend | 16 |
| qa | 8 |
| devops | 4 |
| quality-gate | 3 |
| CTO | 7 |
| architect | 0 (6 done) |
| **Total** | **88 open / 94** |

All six wave lanes can start in parallel now.

## Ollama evaluation check (T081a pre-check, research F-16)

Ollama 0.40.16 at `http://localhost:11434`: `qwen3.5:9b` present (6,594,474,711 B, capabilities
incl. `tools`, `thinking`) and fallback `qwen3.5:4b` present (3,324,173,934 B, `tools`). Nothing
had to be pulled.

## Files changed

- New: `specs/011-ai-composer-agent/contracts/{backend-api.openapi.yaml,ui-test-ids.md}`,
  `src/StemStudio.Agent/Ports/{IStyleActions,StyleQuery,IGenerationActions,GenerationQuery,ISongActions,LyricsSheetSummary,IVoiceActions,VoiceProfileSummary}.cs`,
  `tests/StemStudio.Agent.Tests/Ports/PortRecordTests.cs`, this hand-off.
- Modified: `specs/011-ai-composer-agent/{plan,research,data-model,tasks}.md`,
  `evaluation/eval-set.md`, `contracts/{DELTA.md,tool-catalogue.md,agent-api.openapi.yaml,ui-test-ids.delta.md,redocly.yaml}`,
  `src/StemStudio.Agent/Ports/IStemStudioActions.cs`, `src/StemStudio.Contracts/StemStudio.Contracts.csproj` (comment),
  `tests/StemStudio.Api.Tests/StemStudio.Api.Tests.csproj`,
  `tests/StemStudio.Api.Tests/Contract/{AgentContractTests,BackendContractTests,BackendContractTests.StemsAndMixes,ContractJson,GenerationContractTests}.cs`.

## Tests and coverage

`pwsh scripts/test-all.ps1 -ContinueOnError`: **44 of 45 steps passed**; the one failure is
pre-existing (see Open issues 1).

| Project / component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Agent.Tests -> StemStudio.Agent | 55 passed | 100 % | 100 % |
| StemStudio.Api.Tests -> Api / Contracts | 1269 passed | 98.95 % / 100 % | 92.92 % / 100 % |
| StemStudio.Domain.Tests | 752 passed | 98.97 % | 94.52 % |
| StemStudio.Persistence.Tests | 92 passed | 98.62 % | 94.73 % |
| StemStudio.Web.Tests | 1471 passed | 99.63 % | 93.9 % |
| worker pytest | 2381 passed, 3 skipped | 100 % | 100 % |
| generator pytest | 501 passed, 2 skipped | 99 % | (gate passed) |
| engine / web-interop vitest | 287 / 254 passed | 100 % / 99.68 % | 100 % / 97.95 % |
| quality tools / fixtures pytest | 534 / 250 passed | 100 % | 100 % |

`dotnet build -c Release`: 0 warnings, 0 errors; `dotnet format --verify-no-changes`: clean.
Contract lint for 011 (not yet in test-all, devops T010):
`npx @redocly/cli@2.78.0 lint --config specs/011-ai-composer-agent/contracts/redocly.yaml specs/011-ai-composer-agent/contracts/backend-api.openapi.yaml specs/011-ai-composer-agent/contracts/agent-api.openapi.yaml`
-> both valid (OpenAPI 3.1), 8 + 2 `operation-4xx-response` warnings (the known 4xx-less GETs).

## Quality checks

`tools/quality/run_all.py`: 1837 files, 0 errors, 550 warnings (exit 0); no warning in a new or
changed Phase 0 unit except the known `StemStudio.Agent is not in the dependency map`
(quality-gate T007/T008).

## Dependencies added

None.

## Docs impact

User-visible: the agent panel, `/settings` page and the 009/010 agent actions arrive with the wave;
the CTO updates the manuals in T084a. Setup: Ollama on the host with `qwen3.5:9b` (6.6 GB) and
`qwen3.5:4b` (3.3 GB); Claude optional via `ANTHROPIC_API_KEY` in the shell only.

## Open issues / requests

1. **Pre-existing red step, not caused by this work**: `docs manuals up to date` fails on `main`
   because `docs/ROADMAP.md` changed in 041ca70/5ae61cf without regenerating the PDFs. CTO: run
   `cd tools/docs && npm ci && npm run browser && npm run build` and commit `docs/public`.
2. **CTO / owner decision - FR-025 "MUST NOT offer voice cloning"**: interpreted as "no tool creates
   a voice profile or touches consent"; `convert_take_voice` ("Use my voice" with an existing,
   consented own-voice profile, always approved) is offered, as the brief asked. If the owner reads
   FR-025 more strictly, drop `list_voice_profiles`/`convert_take_voice` (catalogue major bump not
   needed before release: remove them from 1.0.0 and S2).
3. **devops (T010)**: add the 011 contracts-lint step to CI and `scripts/test-all.{ps1,sh}`.
4. **backend**: `PendingAgentOperations` must be empty at the end of the wave; `LoopbackActions`
   implements the four new port groups (T034/T035).
5. **quality-gate (T007/T008)**: dependency map for `StemStudio.Agent`.
6. **ai-agent**: `Backends/Fakes/ScriptedChatClient` (seeded in part 1) is yours; registry groups
   per the catalogue table.

## Next owner

CTO: review and start the wave (agent, backend, frontend, qa, devops, quality-gate in parallel).
