# Contract delta: feature 023 (lyric writing assistant)

Feature 023 bumps exactly the contracts reserved for it in `docs/ROADMAP.md`: backend-api
**1.23.0**, ui-test-ids **1.23.0**, agent tool catalogue **1.2.0**, agent eval set **1.1.0**, and
adds the model output schema **lyric-assist-output 1.0.0**. Every change is additive. Worker,
generator, audio engine, web-interop, help-content (new topics only, no format change),
backup-archive and remote-protocol are unchanged.

**Version reservation (2026-11-16):** feature 022 (library by artist and album) is designed in
parallel and had not allocated versions when 023 was written. Following the CTO brief, 022 keeps
the expected **1.22.0** (backend-api, ui-test-ids) and the catalogue minor **1.1.0**; 023 takes
**1.23.0** and catalogue **1.2.0**. If 022 needs no catalogue minor, 1.1.0 stays unused (labels are
never renumbered). Whatever is built first, the CTO merges the deltas in feature order.

## Status (design, 2026-11-16)

| Contract | Base | 023 document | State |
|---|---|---|---|
| Backend API | 1.22.0 (022) or 1.21.0 (claude-key) | [backend-api.delta.openapi.yaml](backend-api.delta.openapi.yaml) `1.23.0-delta.1`; Redocly 2.78.0: **valid OpenAPI 3.1, 0 errors, 2 documented warnings** (`operation-4xx-response` on `getLyricAssistStatus`; `no-unused-components` on `LyricAssistEvent`, the SSE payload documentation) | binding for the wave; merged unchanged at T001 |
| UI test ids | 1.22.0 / 1.21.0 | [ui-test-ids.delta.md](ui-test-ids.delta.md) 1.23.0 | names binding; merged at T003 |
| Agent tool catalogue | 1.0.0 (011; 1.1.0 reserved for 022) | [tool-catalogue.delta.md](tool-catalogue.delta.md) 1.2.0 | binding; merged at T004 |
| Agent eval set | 1.0.0 (011) | cases 31-35 + S4 in the catalogue delta -> 1.1.0 | binding; merged at T004 |
| Lyric eval set | - | [../evaluation/lyric-eval-set.md](../evaluation/lyric-eval-set.md) 1.0.0 | binding (ai-agent converts to JSON) |
| Model output | - | [lyric-assist-output.schema.json](lyric-assist-output.schema.json) 1.0.0 (JSON Schema 2020-12) | binding (ai-agent: prompt format, validator) |
| C# | - | `src/StemStudio.Contracts/LyricAssist/*` DTOs, `ApiRoutes.LyricAssist*`, `LyricAssistLimits`, `LyricAssistProblemTypes`; `LyricsSheetDto` + `Revision`, `Assist`; `LyricsInput` + `ExpectedRevision`; `LyricsSnapshotDto` + `AssistedLineCount`; agent `PageContextDto` + `LyricsId`, `DecisionReasons.LyricsEdit`, `JobPhaseDto.LyricAssist` | T005 |

Lint command: see [redocly.yaml](redocly.yaml).

## 1. Backend API 1.23.0 (summary; the OpenAPI document wins)

| Method + path | operationId | Notes |
|---|---|---|
| `GET /api/lyric-assist/status` | `getLyricAssistStatus` | layers, model state (011 status), Claude notice, active assist |
| `PUT /api/lyric-assist/settings` | `updateLyricAssistSettings` | acknowledge / withdraw the Claude notice |
| `POST /api/lyrics/{lyricsId}/assists` | `createLyricAssist` | `202 LyricAssist`; `400 imitation`; `409 revision-conflict / language-unsupported / not-own-lyrics / assist-active / model-unavailable / claude-notice-required` |
| `GET /api/lyrics/{lyricsId}/assists` | `listLyricAssists` | newest 20 |
| `GET /api/lyric-assists/{assistId}` | `getLyricAssist` | snapshot with suggestions |
| `GET /api/lyric-assists/{assistId}/events` | `streamLyricAssistEvents` | SSE (data-model 10) |
| `POST /api/lyric-assists/{assistId}/cancel` | `cancelLyricAssist` | `409 assist-ended` |
| `GET /api/lyrics/{lyricsId}/suggestions` | `listLyricSuggestions` | `status`, `category` filters |
| `POST /api/lyrics/{lyricsId}/suggestions/accept` | `acceptLyricSuggestions` | one revision, one undo step; `409 revision-conflict / overlapping-suggestions / suggestion-not-open`; `422 too-long` |
| `POST /api/lyrics/{lyricsId}/suggestions/reject` | `rejectLyricSuggestions` | |
| `POST /api/lyrics/{lyricsId}/suggestions/{suggestionId}/intentional` | `markSuggestionIntentional` | `201 IntentionalMark` |
| `GET/POST /api/lyrics/{lyricsId}/intentional-marks`, `DELETE .../{markId}` | `listIntentionalMarks`, `createIntentionalMark`, `deleteIntentionalMark` | max 200 (`409 mark-limit`) |
| `PUT /api/lyrics/{lyricsId}/assist-settings` | `updateSheetAssistSettings` | sensitivity, rhythm target |
| `PUT /api/lyrics/{lyricsId}/own-work-override`, `DELETE` same path | `confirmOwnWork`, `revokeOwnWork` | owner decision Q1: confirm own work for the matched track (statement, name, tick); `409 override-not-applicable`; revoke `204`; not agent tools |
| `POST /api/lyrics/{lyricsId}/undo` | `undoLyricSuggestions` | `409 nothing-to-undo / undo-blocked-by-edit / revision-conflict` |
| `GET /api/lyrics/{lyricsId}/revisions`, `POST .../revisions/{revision}/restore` | `listLyricsRevisions`, `restoreLyricsRevision` | Expert |
| `GET /api/lyrics/{lyricsId}/syllables`, `POST /api/lyric-assist/syllables` | `getSheetSyllables`, `countSyllables` | deterministic |
| `GET /api/lyric-assist/rhymes` | `findRhymes` | local data, <= 500 ms |
| `GET/POST /api/lyric-assist/words`, `DELETE .../{wordId}` | `listLyricWords`, `addLyricWord`, `deleteLyricWord` | personal word list, max 5,000 per language |
| `POST /api/lyric-assist/language-guess` | `guessLyricsLanguage` | for `unknown` sheets |

**Schema additions at the merge** (listed in the delta's `info.description`): `LyricsSheet.revision`,
`LyricsSheet.assist` (`SheetAssistInfo`), `LyricsInput.expectedRevision` (optional),
`LyricsSnapshot.assistedLineCount`, `SendMessageRequest.context.lyricsId` (optional),
`Step.decisionReasons` + `lyrics-edit`, `JobPhase` + `lyricAssist`.

**Compatibility**: old clients ignore the new members; 010 saves without `expectedRevision` keep
last-write-wins; `revision` of existing sheets is 1 after the migration. `DELETE /api/lyrics/{id}`
(010) also deletes revisions, assists, suggestions, marks and assisted lines (cascade) and cancels a
running assist of that sheet.

## 2. Backend-internal (not HTTP)

- `ILyricAssistant` (`StemStudio.Agent/LyricAssist/`, ai-agent): `RunAsync(LyricAssistJob job,
  ILyricAssistSink sink, CancellationToken ct)`; the backend's `LyricAssistService` creates the
  assist row, runs the deterministic layer, calls the assistant for the model layer, persists
  suggestions and events. The assistant never touches persistence.
- Ports in `StemStudio.Domain/Lyrics/Assist/` (backend): `ISpellChecker` (Hunspell adapter in the
  API), `ILyricsCorpus` (n-gram index over 006 lyrics), `IPronunciationLexicon` (CMUdict),
  `IWordList` (OpenTaal / SCOWL words for Dutch rhymes); pure services `SyllableCounter`,
  `RhymeKey`, `LicenceLexicon`, `AnchorResolver`, `SuggestionApplier`, `SuggestionFilter`,
  `VerbatimGuard`, `SheetBlocks`.
- GPU: an assist is not a heavy GPU job; it uses the 011 model selection (fallback model while
  `IGpuActivity.IsHeavyJobActive()`); it does not call `ILlmGpuRelease`.
- Concurrency: at most one assist globally; assists and agent runs may overlap (Ollama serialises
  or parallelises per its own settings).

## 3. Agent changes (catalogue 1.2.0 + backend API agent part)

- 8 tools and the group `lyric-assist` (tool-catalogue.delta.md).
- `PageContext.lyricsId` (Web app sets it on `/lyrics/{id}`).
- Policy: `apply_lyric_suggestions` -> reason `lyrics-edit`, approval always, never plan-covered.
- Approval card for `apply_lyric_suggestions`: the Web app renders `agent-approval-lyrics-diff`
  from `GET /api/lyrics/{id}/suggestions?status=open` filtered to the step's `suggestionIds`; the
  `approval.requested` event payload is unchanged (arguments carry ids only).

## 4. Runtime

- No new service or container. Dictionaries are vendored files inside the API image (about 10 MB);
  nothing is downloaded at run time.
- E2E fake stack: `STEMSTUDIO_AGENT_BACKEND=fake` + `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT=/scripts/lyric/<name>.json`
  (scripted assistant output per block, same format as `lyric-assist-output.schema.json` wrapped as
  `{version: 1, blocks: [<output>]}`), mounted from `e2e/lyric-scripts/`.
- CI: no Ollama, no Claude; a failing HTTP handler proves no outbound request during assistant
  actions (SC-006).

## 5. Phase 0 outcome (architect, 2026-11-17; binding for the wave)

| Contract | Document | State |
|---|---|---|
| Backend API **1.23.0** | [backend-api.openapi.yaml](backend-api.openapi.yaml) (full) = 012's full **1.11.0** (newest on `main` 31d43422) + [backend-api.delta.openapi.yaml](backend-api.delta.openapi.yaml) + the schema additions of section 1; `info.x-stemstudio-contains` = 1.1.0..1.10.0, 1.21.0, 1.11.0, 1.23.0 | Redocly 2.78.0: valid, **11 warnings** = the 9 of 1.11.0 (`operation-4xx-response`) + `getLyricAssistStatus` (4xx) + `LyricAssistEvent` (unused, SSE payload doc). Delta: valid, 2 warnings. Proven by `LyricAssistContractTests` |
| UI test ids **1.23.0** | [ui-test-ids.md](ui-test-ids.md) (full) = 012's 1.11.0 + [ui-test-ids.delta.md](ui-test-ids.delta.md) (section "Additions in 1.23.0") | proven by `LyricAssistUiTestIdsTests` (48 rows, 6 keys, 6 help topics) |
| Tool catalogue **1.2.0** | merged into `specs/011-ai-composer-agent/contracts/tool-catalogue.md` (title v1.2.0, "contains 1.0.0, 1.3.0, 1.2.0") | 8 tools, group `lyric-assist`, `lyrics` context 28 tools, counts 65 / 14 without 022 |
| Agent eval set **1.1.0** | merged into `specs/011-ai-composer-agent/evaluation/eval-set.md` (cases 31-35 scored; S4, S5 supplementary; fixture sheets `l-zomer`, `l-inject`, `l-fake-song`) | JSON: ai-agent T055 |
| lyric-assist-output **1.0.0** | [lyric-assist-output.schema.json](lyric-assist-output.schema.json), embedded in `StemStudio.Agent` (`LyricAssistOutputSchema`) | meta-schema valid (jsonschema 4.26, Draft 2020-12); [check_output_schema.py](check_output_schema.py): 10 scripts, 3 valid + 8 invalid samples, 0 errors |
| C# | `src/StemStudio.Contracts/LyricAssist/*` (16 enums, 44 records, `LyricAssistLimits`, `LyricAssistProblemTypes`, `LyricAssistProblemMembers`, `LyricAssistEventTypes`, `ApiRoutes.LyricAssist*`); base additions listed in the status table above | serialisation, enum, limit and route tests in `LyricAssistContractTests` |

**Pre-release refinements (labels unchanged, nothing was released):**

1. `LyricAssistSummary` is no longer closed (`additionalProperties: false` removed); `LyricAssist` (=
   `allOf` summary + `suggestions`) is closed with `unevaluatedProperties: false`. A closed base made every
   `LyricAssist` invalid (`$.suggestions: additional property not allowed`). Applied to delta and full document.
2. `lyric-assist-output` `misses[].verdict` gains `keep` (a correct word the dictionary does not know, e.g. a
   name such as `Zelmara`: nothing is reported). A miss without a verdict is reported as when no model ran
   (`improvement` with the dictionary suggestions, research R-03 step 5).
3. Fake script selection (section 4): `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT` names a file (every assist uses
   it) **or a folder**; per assist the fake uses `<title-slug>-<kind>.json`, else `<kind>.json`, else no script.
   Blocks beyond a script, and a block without a script, answer `{"items":[]}`; a retry gets the same answer
   (`LyricScript.Resolve`, `LyricScript.Slug`, `ScriptedLyricModel`). The E2E stack mounts `e2e/lyric-scripts/`
   as the folder.
4. `LyricsSnapshot.assistedLineCount` is written only when > 0 (schema default 0), so 1.9.0-1.11.0 song
   examples stay byte-identical; `LyricsInput.expectedRevision` is written only when set.
5. `SheetAssistSettingsUpdate.rhythmTarget`: absent keeps, `null` clears; the C# record tells them apart
   with `RhythmTargetSpecified` (the Web client always writes the member).
6. Shared component names: the delta's `LyricsId`, `NotFound`, `ValidationProblem`, `ProblemDetails`,
   `ValidationProblemDetails`, `TokenUsage` keep the **base** definitions in the full document (same meaning).

**Ports across lanes (decided in Phase 0):** `StemStudio.Agent` keeps its 011 dependency rule (Agent ->
Contracts only, no Domain reference). The assistant therefore gets its deterministic checks through the Agent
port `ILyricVerification` (syllables, rhyme kind, verbatim), implemented in the API over the Domain services;
the Domain ports `ISpellChecker`, `IPronunciationLexicon`, `IWordList`, `ILyricsCorpus` (+ `AssistLanguage`,
`CorpusTrack`) are used by the backend only. Sensitivity, intentional-mark and personal-word filtering is the
backend's Domain `SuggestionFilter`, applied in the sink to dictionary and model suggestions alike (the
assistant validates schema, anchors, kinds, limits, verification, verbatim and duplicates). The agent's
loopback port for the 8 tools is `StemStudio.Agent.Ports.ILyricAssistActions` (text-free projections
`LyricSheetState`, `LyricSuggestionsSummary`/`LyricSuggestionRef`, `LyricAcceptSummary`, `IntentionalMarkRef`).

**Merging with feature 022 in the same round.** 022 (backend 1.22.0, ui-test-ids 1.22.0, catalogue 1.1.0)
is designed in parallel on the same base 1.11.0; both deltas are standalone and share no path or new schema
name (only the base response `NotFound`, which keeps the base text). Whichever lands second on the round
branch is merged onto the other's full document: backend = the other full document + this feature's delta
(same rules: shared names keep the base), label = the feature merged last, `x-stemstudio-contains` lists both;
in `LyricAssistContractTests` only `BaseDocument` (now `Backend1110`) changes to the 1.22.0 document and the
expected `x-stemstudio-contains`; in `LyricAssistUiTestIdsTests` the base file. The catalogue document then
contains 1.0.0, 1.1.0, 1.3.0 and 1.2.0; counts become 65 + 022's tools. The one-time edits both Phase 0
branches make to the same files (`tests/StemStudio.Api.Tests/StemStudio.Api.Tests.csproj` link of
`Contract/backend-api.openapi.yaml`, `ContractJson.cs`, `PracticeContractTests.cs`, the `Pending*Operations`
lists, `tool-catalogue.md`, `eval-set.md`) conflict textually and are resolved by keeping both additions.

**Done at the 022+023 round start (2026-11-17)**: 022 landed first on the round branch, so 1.23.0 was
re-based onto 022's full 1.22.0 (= 013's 1.12.0 + 022). The Phase 0 document (on 1.11.0) is kept as
[backend-api.023-on-1.11.0.openapi.yaml](backend-api.023-on-1.11.0.openapi.yaml); [merge_full.py](merge_full.py)
builds `backend-api.openapi.yaml` by a block-level three-way merge (base 1.11.0, ours 1.22.0, theirs the
kept document; a block changed on both sides fails) and `--check` proves freshness and that 1.23.0 contains
1.22.0 and the 023 document (lists of the extended schemas may only grow). `x-stemstudio-contains` ends
`1.11.0, 1.12.0, 1.22.0, 1.23.0`; Redocly 2.78.0: valid, 13 warnings (11 of 1.22.0 + 2 of 023). UI test
ids 1.23.0 = 022's 1.22.0 + the 1.23.0 changelog paragraph + "Additions in 1.23.0". Tool catalogue 1.2.0
contains 1.0.0, 1.3.0, 1.1.0 and 1.2.0 (69 tools, 14 with approval always). `LyricAssistContractTests.BaseDocument`
= `Backend022`, `LyricAssistUiTestIdsTests.Base` = `Contract/022`; 022's tests check the linked 1.22.0
(`ContractJson.Backend022`).
