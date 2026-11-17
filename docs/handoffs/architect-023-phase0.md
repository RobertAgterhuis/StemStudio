# Hand-off: architect / 023 Phase 0 (contracts, contract code, skeleton, dictionaries, fixtures)

Feature 023 (lyric writing assistant) tasks T001-T007 and T009-T011 of
`specs/023-lyric-writing-assistant/tasks.md`, on branch `r22-p0-023` (worktree
`.claude/worktrees/r22-p0-023`, from `main` 31d43422). Combined round with 022 (designed in parallel by
another architect, who owns `docs/ROADMAP.md` and `docs/decisions/CTO-DECISIONS-2026-11-15.md` this phase:
both untouched here; decision rows below). T008 (model spike) moved into the ai-agent lane (see "Open issues").
All work is additive; nothing pushed or merged.

## What was built

### Contract versions (binding for the wave)

| Contract | Version | Document | Base / proof |
|---|---|---|---|
| backend-api | **1.23.0** | `specs/023-lyric-writing-assistant/contracts/backend-api.openapi.yaml` (full, new) | 012's full 1.11.0 + `backend-api.delta.openapi.yaml` + the "schema additions at the merge"; `x-stemstudio-contains` = 1.1.0..1.10.0, 1.21.0, 1.11.0, 1.23.0. `LyricAssistContractTests` prove base + exactly the delta |
| ui-test-ids | **1.23.0** | `specs/023-lyric-writing-assistant/contracts/ui-test-ids.md` (full, new) | 012's 1.11.0 + delta (48 rows, 6 keys, 6 help topics); `LyricAssistUiTestIdsTests` |
| tool catalogue | **1.2.0** | `specs/011-ai-composer-agent/contracts/tool-catalogue.md` (contains 1.0.0, 1.3.0, 1.2.0) | 8 tools, group `lyric-assist`, `lyrics` context 28 tools, counts 65 / 14 (without 022) |
| agent eval set | **1.1.0** | `specs/011-ai-composer-agent/evaluation/eval-set.md` | cases 31-35 scored, S4 + S5 supplementary, fixture sheets `l-zomer`, `l-inject`, `l-fake-song` |
| lyric-assist-output | **1.0.0** | `specs/023-lyric-writing-assistant/contracts/lyric-assist-output.schema.json`, embedded in `StemStudio.Agent` | meta-schema valid; `check_output_schema.py` |
| worker, generator, audio engine, web-interop, help-content format, backup, remote | unchanged | | |

Details and every refinement: `specs/023-lyric-writing-assistant/contracts/DELTA.md` section 5 (new).

### Pre-release contract refinements (labels unchanged; found while building the skeleton)

1. **`LyricAssist` was unsatisfiable**: `allOf` over a closed `LyricAssistSummary` rejects `suggestions`.
   The summary is now open; `LyricAssist` has `unevaluatedProperties: false` (delta and full document).
2. **Miss verdict `keep`** (lyric-assist-output): correct but unknown words (names) had no verdict.
3. **Fake script folder mode**: `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT` = a file or a folder; per assist
   `<title-slug>-<kind>.json`, else `<kind>.json`, else `{"items":[]}` for every block (`LyricScript.Resolve`).
   Scripts renamed accordingly (`alternatives.json`, `l-inject-check.json`).
4. `LyricsSnapshot.assistedLineCount` written only when > 0 and `LyricsInput.expectedRevision` only when set
   (so all 1.9.0-1.11.0 examples round-trip unchanged).
5. `SheetAssistSettingsUpdate.rhythmTarget`: absent keeps, null clears (`RhythmTargetSpecified`).
6. Shared names of the delta (`LyricsId`, `NotFound`, `ValidationProblem`, `ProblemDetails`,
   `ValidationProblemDetails`, `TokenUsage`) keep the base definitions.
7. Spec/quickstart corrected to the fixture: four corrections in "Zomeravond" (spec said five), Q3 syllable fit
   on line 2 (15 -> 12; line 1 is the marker), fixture path `tests/fixtures/lyrics/assist/sheets/`.

### Code and skeleton

- **Contracts** `src/StemStudio.Contracts/LyricAssist/`: 16 enums (incl. the 29-value `SuggestionTypeDto`),
  44 records (every schema of the delta; `LyricAssistDto : LyricAssistSummaryDto`), `LyricAssistLimits`,
  `LyricAssistProblemTypes` (+ `AssistProblemCodes`), `LyricAssistProblemMembers`, `LyricAssistEventTypes`,
  `ApiRoutes.LyricAssist.cs` (27 operations). Base additions: `LyricsSheetDto.Revision` (default 1) and
  `.Assist` (default `SheetAssistInfoDto.Default`) - so the current API already serves valid 1.23.0 sheets -,
  `LyricsInput.ExpectedRevision`, `LyricsSnapshotDto.AssistedLineCount`, `PageContextDto.LyricsId`,
  `AgentDecisionReasons.LyricsEdit`, `JobPhaseDto.LyricAssist`.
- **Agent** `src/StemStudio.Agent/LyricAssist/`: `ILyricAssistant`, `ILyricAssistSink`, `LyricAssistJob`,
  `LyricBlock`, `DictionaryMiss`, `ModelSuggestionItem`, `ModelMissVerdict` + `MissVerdict`,
  `AssistantSuggestion`, `LyricBlockResult`, `LyricModelInfo`, `ILyricModel` + `LyricModelRequest`/`Reply`,
  `ILyricVerification`, `LyricAssistException`, `LyricAssistOutputSchema` (embedded contract file),
  `Fakes/LyricScript` + `Fakes/ScriptedLyricModel`. `Ports/ILyricAssistActions` (loopback port of the 8 tools)
  with text-free `LyricSheetState`, `LyricSuggestionsSummary`, `LyricSuggestionRef`, `LyricAcceptSummary`,
  `IntentionalMarkRef`. `Tools/Providers/LyricAssistToolNames` + empty `LyricAssistToolProvider` (not registered).
- **Domain** `src/StemStudio.Domain/Lyrics/Assist/`: ports `ISpellChecker`, `IPronunciationLexicon`, `IWordList`,
  `ILyricsCorpus` + `CorpusTrack`, `AssistLanguage`. **No stub services**: the pure services (SheetBlocks,
  AnchorResolver, ...) are written test-first by backend (T012/T013 ...); empty stubs would be untested code
  under the 90 % gate.
- **Dictionaries** (T006) `src/StemStudio.Api/LyricAssist/Dictionaries/` (7.4 MiB, `PROVENANCE.md`,
  `manifest.json`, nested `.gitattributes` `* -text -diff` so the CRLF files keep their SHA-256);
  `WeCantSpell.Hunspell` 7.0.1 pinned and referenced by `StemStudio.Api`; data copied to output and publish.
- **Fixtures** (T009) `tests/fixtures/lyrics/assist/` (12 sheets, 4 invented corpus songs, 300 nl + 300 en
  syllable gold words, 51 nl + 40 en rhyme pairs, `expected.json` with every planted fact, README) and
  `e2e/lyric-scripts/` (10 scripts + README). All texts self-written (CC0).
- **Tests**: `tests/StemStudio.Api.Tests/Contract/LyricAssistContractTests{,.Dtos}.cs` (merge rule, enums, inline
  enums, problem codes/members, event names, limits, routes, 33 DTO samples against their schemas, 3 JSON
  samples round-trip, base DTO additions, tri-state update), `LyricAssistUiTestIdsTests.cs`,
  `LyricAssist/VendoredDictionaryTests.cs` (SHA-256 of every file, licences, Hunspell loads nl / en_US /
  en_GB-ise / en_GB-ize, CMUdict), `tests/StemStudio.Agent.Tests/LyricAssist/` (schema, script, scripted
  model, skeleton). Staging: `PendingLyricAssistOperations` lists the 27 new operation ids (backend removes
  each when served: T028, T030, T039, T046, T062, T075).
- **One-time edits outside the lane (precedent 012)**: Api.Tests csproj links (current
  `Contract/backend-api.openapi.yaml` -> 023 document; 012 doc as `Contract/012/`), `ContractJson.Backend1110`
  + shared helpers `OperationId`/`Signature`/`Keys`/`ConstantValues` (moved from `ClaudeKeyContractTests`),
  `PracticeContractTests` proves 1.11.0 on the linked 012 document, Agent csproj embeds the output schema,
  Agent.Tests csproj links it.

## Files changed

- `specs/023-lyric-writing-assistant/`: `contracts/{backend-api.openapi.yaml (new), ui-test-ids.md (new),
  check_output_schema.py (new), backend-api.delta.openapi.yaml, lyric-assist-output.schema.json, DELTA.md,
  redocly.yaml}`, `plan.md`, `research.md`, `spec.md`, `quickstart.md`, `tasks.md`
- `specs/011-ai-composer-agent/contracts/tool-catalogue.md`, `specs/011-ai-composer-agent/evaluation/eval-set.md`
- `docs/decisions/ADR-037-lyric-writing-assistant.md` ("Phase 0 outcome")
- `Directory.Packages.props`, `src/StemStudio.Api/StemStudio.Api.csproj`, `src/StemStudio.Api/LyricAssist/Dictionaries/**`
- `src/StemStudio.Contracts/{LyricAssist/** (new), Lyrics/LyricsSheetDto.cs, Lyrics/LyricsInput.cs,
  Generation/LyricsSnapshotDto.cs, Agent/PageContextDto.cs, Agent/JobPhaseDto.cs, Agent/AgentDecisionReasons.cs,
  StemStudio.Contracts.csproj}`
- `src/StemStudio.Agent/{LyricAssist/** (new), Ports/{ILyricAssistActions, LyricSheetState, LyricSuggestionsSummary,
  LyricSuggestionRef, LyricAcceptSummary, IntentionalMarkRef}.cs, Tools/Providers/LyricAssistTool{Names,Provider}.cs,
  StemStudio.Agent.csproj}`, `src/StemStudio.Domain/Lyrics/Assist/*` (new)
- `tests/StemStudio.Api.Tests/{StemStudio.Api.Tests.csproj, Contract/(LyricAssistContractTests.cs,
  LyricAssistContractTests.Dtos.cs, LyricAssistSamples.json, LyricAssistUiTestIdsTests.cs, ContractJson.cs,
  ClaudeKeyContractTests.cs, PracticeContractTests.cs, BackendContractTests.cs, BackendContractTests.StemsAndMixes.cs),
  LyricAssist/VendoredDictionaryTests.cs}`, `tests/StemStudio.Agent.Tests/{StemStudio.Agent.Tests.csproj, LyricAssist/*}`
- `tests/fixtures/lyrics/{README.md, assist/**}`, `e2e/lyric-scripts/**`

## Tests and coverage

```text
dotnet build StemStudio.slnx -c Release                         0 Warning(s), 0 Error(s)
dotnet format StemStudio.slnx --verify-no-changes               passed
dotnet test StemStudio.slnx -c Release -p:CollectCoverage=true   all passed, gates met (see final numbers below)
Redocly 2.78.0 lint (023 redocly.yaml)                          full 1.23.0: valid, 11 warnings (9 inherited from 1.11.0
                                                                operation-4xx-response + getLyricAssistStatus + unused
                                                                LyricAssistEvent); delta: valid, 2 warnings
python -I .../contracts/check_output_schema.py                   meta-schema valid; 10 scripts; 3 valid + 8 invalid samples; 0 errors
uv run --project tools/quality python tools/quality/run_all.py  2431 files, 0 errors, 867 warnings (none in a 023 file)
python -I scripts/secret_scan.py                                3946 files clean
ruff check / format --check (check_output_schema.py)            clean
```

Final run (`dotnet test StemStudio.slnx -c Release -p:CollectCoverage=true`, 90 % line AND branch gate per component):

| Test project | Passed | Component | Line | Branch |
|---|---|---|---|---|
| Domain.Tests | 793 | Domain | 98.93 % | 94.46 % |
| Persistence.Tests | 120 | Persistence | 99.01 % | 95 % |
| Web.Tests | 2084 | Web | 99.6 % | 93.71 % |
| Api.Tests | 1869 (2 skipped, as before) | Api / Contracts | 98.97 % / **100 %** | 92.11 % / **100 %** |
| Agent.Tests | 759 | Agent | 99.04 % | 94.19 % |
| AgentEval.Tests | 134 | AgentEval | 99.83 % | 92.85 % |

Filtered runs: Api `FullyQualifiedName~Contract|FullyQualifiedName~LyricAssist` 605 passed (incl. the dictionary
tests); Agent `FullyQualifiedName~LyricAssist` 33 passed. Worker, generator, audio engine and web-interop untouched (no Python/TS test run needed).

## Quality checks

SIG: all new units <= 15 LOC, CC <= 5, <= 4 parameters; large DTO samples moved into static fields and a JSON
sample file (first draft had 4 unit-size and 2 duplication warnings, all removed). Public API XML-documented
(build would fail otherwise). Dependency rule kept: Agent -> Contracts only; Domain references nothing.

## Dependencies added

| Name | Version | Licence | URL | Note |
|---|---|---|---|---|
| WeCantSpell.Hunspell (NuGet) | 7.0.1 (2025-11-30) | MPL-1.1 (chosen from MPL-1.1/GPL-2.0/LGPL-2.1; unmodified) | https://github.com/aarondandy/WeCantSpell.Hunspell | `StemStudio.Api` |
| OpenTaal Hunspell nl | 2.20.23, commit `1c22bc3d` (2022-11-29, newest tagged state) | BSD-3-Clause (chosen from BSD-3-Clause / CC BY 3.0) | https://github.com/OpenTaal/opentaal-hunspell | vendored data + LICENSE.txt |
| SCOWL Hunspell en_US, en_GB-ise, en_GB-ize | rel-2026.02.25 (SCOWL `7e99eda`, size 60) | SCOWL notice (MIT/HPND-style) + Kuenning BSD-style affix files | https://github.com/en-wl/wordlist | vendored data + README_<dict>.txt |
| CMU Pronouncing Dictionary | commit `74790861` (2025-10-24) | BSD-2-Clause | https://github.com/cmusphinx/cmudict | vendored data + LICENSE |

No GPL/LGPL code or data bundled. Build-time tool only: `@redocly/cli` 2.78.0 via npx (not added to any
package.json). For THIRD-PARTY-LICENSES (CTO, T073).

## Docs impact

None user-visible yet (Phase 0). For T073: technical manual gets the vendored dictionaries and their
licences (PROVENANCE.md), the fake-script folder rule; user manual chapter "Lyric assistant" (frontend T072
texts).

## Decision rows for CTO-DECISIONS (please add; I did not edit the file)

| # | Decision | Reason |
|---|---|---|
| 023-P0-1 | backend-api 1.23.0 = 1.11.0 + 023 delta; labels per ROADMAP rule; with 022 in the same round the second-merged feature merges its delta onto the other's full document (DELTA.md 5); `LyricAssistContractTests.BaseDocument` is the single switch | additive, no renumbering |
| 023-P0-2 | `LyricAssistSummary` open + `LyricAssist` `unevaluatedProperties: false` (pre-release fix) | the closed base made every `LyricAssist` invalid |
| 023-P0-3 | output schema miss verdict `keep`; a miss without verdict = dictionary-only behaviour | names like `Zelmara` had no valid verdict |
| 023-P0-4 | fake script = file or folder (`<title-slug>-<kind>.json`, `<kind>.json`, else empty) | one stack must serve all E2E scenarios |
| 023-P0-5 | Agent stays Contracts-only: deterministic checks via Agent port `ILyricVerification` (API adapter over Domain); sensitivity/mark/word filtering = backend `SuggestionFilter` in the sink | keeps the 011 dependency rule; one filter for both layers |
| 023-P0-6 | Phase 0 creates ports only, no stub services | no untested code under the 90 % gate |
| 023-P0-7 | T008 spike moves into the ai-agent lane (before T068); SC-004 and `BlockLines = 8` stay estimates until then | machine busy; Docker stack (Ollama) off limits; change would be configuration only |
| 023-P0-8 | The six help topic ids are binding in ui-test-ids 1.23.0; topic files come with frontend T072 | the 004 help catalogue is the frontend-owned topic files; a topic needs its text |
| 023-P0-9 | OpenTaal 2.20.23 (2022) is pinned (newest tagged state; the 2023 "intermediate" commit is not used) | reproducible, licence verified |
| 023-P0-10 | E2E not-own / verbatim scenarios paste `tests/fixtures/lyrics/assist/corpus/t-fake-song.en.txt` into a track via 006 `setLyrics` | the 006 fake lyrics provider has 3-word lines that can never match the guard |

## Lane notes (what each lane must know)

**Split suggestion for the round (max 6 agents):** 022-backend, 022-frontend (other architect), **backend-023**,
**frontend-023**, **ai-agent shared 022 + 023**, **qa shared 022 + 023**. Wave 2 (after merge of wave 1):
ai-agent T068 eval run, devops T069, qa T070, quality-gate T071.

- **backend-023** (24 tasks, heaviest lane; order: Domain -> persistence -> 010 compatibility -> US1 -> US2/US3
  -> US4 -> US5 -> US6): T012-T017, T025-T030, T038-T039, T045-T048, T056-T057, T061-T062, T074-T075.
  Start with the pure Domain services in `src/StemStudio.Domain/Lyrics/Assist/` against the ports there and the
  fixtures in `tests/fixtures/lyrics/assist/` (gold lists, rhyme vectors, `expected.json`). Remove each served
  id from `PendingLyricAssistOperations`. Implement `ILyricVerification` (Agent port) in the API over the Domain
  services and `ILyricAssistSink` in `LyricAssistService` (apply `SuggestionFilter` there, count `filtered`).
  The fake stack: wire `ScriptedLyricModel` via `LyricScript.Resolve(path, title, kind)` when
  `STEMSTUDIO_AGENT_BACKEND=fake` and the variable is set. Dictionaries: load from `AppContext.BaseDirectory/
  LyricAssist/Dictionaries` (copied by the csproj); `VendoredDictionaryTests` shows the WeCantSpell API.
  Security: sheet text is untrusted for the model (assistant), problem details must never echo text.
- **ai-agent (shared with 022)**: T018-T021, T031-T032, T040-T041, T049-T050, T054-T055, T066-T067, plus **T008**
  (spike, only when the machine is free, native Ollama, never the Docker stack). Build on
  `StemStudio.Agent/LyricAssist/*` (ports fixed; add `LyricPromptBuilder`, `LyricOutputParser`,
  `SuggestionValidator`, `LyricModelClient`, `LyricAssistant`, `Fakes/AdversarialLyricModel`). Output schema:
  `LyricAssistOutputSchema.Text` (Ollama `format`; Claude tool `emit_suggestions`). Tool descriptors in
  `LyricAssistToolProvider` with the names of `LyricAssistToolNames`; loopback port `ILyricAssistActions`
  (backend implements it, T056); registry `lyrics` context = 28; policy `lyrics-edit`; eval-set.json cases 31-35,
  S4, S5 and the three fixture sheets. Note on L-I1: the validator rejects whole-line corrections over 60 %; a
  short "delete" proposal with a valid anchor would pass, so the eval case relies on the model (fixture gap 4).
- **frontend-023**: T022-T023, T033-T036, T042-T043, T051-T052, T058-T059, T063-T064, T072, T076-T077. Ids:
  `specs/023-lyric-writing-assistant/contracts/ui-test-ids.md` section "Additions in 1.23.0" (Level column
  binding); keyboard and announcements there. DTOs and routes: `StemStudio.Contracts.LyricAssist` +
  `ApiRoutes.LyricAssist*`; SSE event names `LyricAssistEventTypes`. `SheetAssistSettingsUpdate`: always send
  `rhythmTarget` (null clears). Six help topic files (T072). Fake API client from the contract samples.
- **qa (shared)**: T024, T037, T044, T053, T060, T065, T078 (T070 in wave 2). Scripts in `e2e/lyric-scripts/`
  (folder rule above; sheet titles from `expected.json` select them), sheets in
  `tests/fixtures/lyrics/assist/sheets/`; the not-own/verbatim scenarios need the corpus text pasted via
  `setLyrics` (decision 023-P0-10). Devops T069 mounts the folder and sets the variable to it.

## Open issues / requests

1. **T008 not run** (see decision 023-P0-7): the ai-agent runs it in the wave and writes the numbers into
   research.md R-05/R-06 and the ADR-037 "Phase 0 outcome" (an architect-owned file: agreed one-time edit).
2. **Merge with 022** (CTO / round orchestrator): DELTA.md section 5 describes the procedure; expected textual
   conflicts in the Api.Tests csproj link, `ContractJson.cs`, `PracticeContractTests.cs`, the Pending lists,
   `tool-catalogue.md` (title line, counts, selection table) and `eval-set.md`; keep both additions.
3. Fixture notes for the owners: English rhyme pairs with variant pronunciations were left out; `o'er` must count
   1 syllable (gold list); syllable counts are hand-checked, first verified by backend T038.
4. THIRD-PARTY-LICENSES: four entries above (CTO, T073). ROADMAP/MoSCoW: no change needed (022 kept 1.22.0 / 1.1.0).

## Next owner

Round orchestrator: merge this branch and 022's Phase 0 into the round branch, then start the lanes above.
