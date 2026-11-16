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
