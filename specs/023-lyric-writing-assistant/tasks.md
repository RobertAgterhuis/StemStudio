---

description: "Task list for feature 023 lyric writing assistant (Phase 0 by the architect, a parallel wave of at most 4 owner lanes, then a review/eval wave and CTO integration)"
---

# Tasks: Lyric Writing Assistant

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/DELTA.md](contracts/DELTA.md),
[evaluation/lyric-eval-set.md](evaluation/lyric-eval-set.md), constitution 1.4.0, ADR-037, the merged
010/011 (+ claude-key) artefacts.

**Tests**: REQUIRED (constitution I). Every implementation task follows its failing-test task
(red -> green -> refactor), >= 90 % line AND branch per component. Nothing in CI needs Ollama,
Claude, a GPU or the internet: the assistant model is replaced by `ScriptedLyricModel` /
`AdversarialLyricModel`, the agent model by the 011 scripted client, dictionaries by fixture files
(plus one real-dictionary test class), the corpus by seeded fake lyrics. Marked exceptions: the
reference-machine runs T008, T068 and the owner checks (quickstart Q8, Q10).

**Status**: design written 2026-11-16 (architect). Phase 0 starts when 011 claude-key is merged
(and 022 if it is merged first; its minors are the base of the 023 deltas).

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task).
- **[USn]**: user story of spec.md. Owners: architect, ai-agent, backend, frontend, qa,
  quality-gate, devops, CTO.

## Checkpoint hand-offs (binding, ADR-008)

`docs/handoffs/<owner>-023-<checkpoint>.md` at `foundational`, every story and `polish`, with real
test and coverage output.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| ai-agent | assistant job/sink ports, domain ports | `StemStudio.Agent/LyricAssist/ILyricAssistant` + Domain ports (T007) | fake sink, fixture dictionaries |
| backend | assistant service | `ILyricAssistant` (T007) | `ScriptedLyricModel` behind a fake `ILyricAssistant` |
| frontend | endpoints, SSE, ids | backend 1.23.0 + DTOs (T005), ui-test-ids 1.23.0 (T003) | fake API client, fake event stream |
| ai-agent (tools) | endpoints | backend 1.23.0 (T001), catalogue 1.2.0 (T004) | stub loopback handler |
| qa | UI, fake stack | ui-test-ids 1.23.0, fake script format (DELTA.md 4), fixtures (T009) | - |

---

## Phase 0: Contracts, skeleton, spike (architect, BEFORE the wave)

- [x] T001 [architect] Merge `contracts/backend-api.delta.openapi.yaml` into the full `contracts/backend-api.openapi.yaml` **1.23.0** on the newest full document (1.22.0 or 1.21.0), apply the "Schema additions at the merge" (LyricsSheet.revision/assist, LyricsInput.expectedRevision, LyricsSnapshot.assistedLineCount, PageContext.lyricsId, decision reason `lyrics-edit`, JobPhase `lyricAssist`); Redocly 2.78.0 lint 0 errors, documented warnings only; contract tests prove 1.23.0 = base + delta
- [x] T002 [P] [architect] Validate `contracts/lyric-assist-output.schema.json` (JSON Schema 2020-12 meta-schema) and add it as an embedded resource of `StemStudio.Agent`; schema test with valid/invalid samples
- [x] T003 [P] [architect] Merge `ui-test-ids.delta.md` into the full `ui-test-ids.md` 1.23.0; add the six help topics to the 004 help-content catalogue resource (ids only, texts by frontend)
- [x] T004 [P] [architect] Merge `tool-catalogue.delta.md` into `specs/011-ai-composer-agent/contracts/tool-catalogue.md` 1.2.0 (changelog, rules, table, groups, counts) and the agent cases 31-35 + S4 into the eval set 1.1.0 (`eval-set.md`)
- [x] T005 [P] [architect] .NET contract code `src/StemStudio.Contracts/LyricAssist/*` (all DTOs and enums of the delta, `LyricAssistLimits`, `LyricAssistProblemTypes`, `ApiRoutes.LyricAssist*`), additions to `LyricsSheetDto`, `LyricsInput`, `LyricsSnapshotDto`, agent `PageContextDto`, `DecisionReasons`, `JobPhaseDto`; serialisation tests against the OpenAPI examples
- [x] T006 [architect] Vendor the dictionaries under `src/StemStudio.Api/LyricAssist/Dictionaries/` (OpenTaal nl, SCOWL en_US + en_GB-ise/ize, CMUdict) with licence files and `PROVENANCE.md` (URL, version/commit, SHA-256, licence chosen); pin WeCantSpell.Hunspell in `Directory.Packages.props` (>= 14 days old); a skeleton test loads each dictionary and checks the SHA-256 (FR-004, research R-02, R-10)
- [x] T007 [P] [architect] Skeleton: Domain ports (`ISpellChecker`, `ILyricsCorpus`, `IPronunciationLexicon`, `IWordList`) and stub services (data-model, plan structure); `StemStudio.Agent/LyricAssist/ILyricAssistant`, `LyricAssistJob`, `ILyricAssistSink`, `LyricBlock`, `ModelSuggestionItem`; `Fakes/ScriptedLyricModel` script loader (format DELTA.md 4); empty `LyricAssistToolProvider`; build 0 warnings
- [x] T008 [architect] Spike S-01 on the reference machine with the ai-agent (Ollama only, no Docker): per-block latency of `qwen3.5:9b` warm and `qwen3.5:4b` on the CPU with the R-05 prompt and `format` schema (5 blocks x 3 runs), JSON validity rate, `think:false`; quick look at 10 Dutch eval cases; update research R-05/R-06 with numbers, confirm or adjust SC-004 and `BlockLines` with an ADR-037 "Phase 0 outcome" note - **moved into the ai-agent lane of the wave** (Phase 0, 2026-11-17: machine busy, Docker stack off limits; research.md "Phase 0 results")
- [x] T009 [P] [architect] Fixtures: `tests/fixtures/lyrics/` (Zomeravond nl, Radio en, 40-line P1/P2, injection sheets, fake-song lyrics), syllable gold lists (300 nl, 300 en words with counts) and rhyme vectors, `e2e/lyric-scripts/*.json` for the quickstart scenarios
- [x] T010 [P] [architect] `docs/MoSCoW.md` S14 and `docs/ROADMAP.md` row/allocation (done in design); re-check against 022's final allocation and fix labels if 022 chose differently - re-checked 2026-11-17: 022 keeps backend/ui-ids 1.22.0 and catalogue 1.1.0, no label change; ROADMAP/MoSCoW untouched (022 architect owns them this phase)
- [x] T011 [architect] Verify the skeleton: `dotnet build` 0 warnings, all tests and coverage gates, `tools/quality` run; ADR-037 "Phase 0 outcome" note (ADR accepted by the owner 2026-11-16); hand-off `docs/handoffs/architect-023-phase0.md`

---

## Phase 1: Foundational (per lane, before the stories)

### backend

- [x] T012 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Lyrics/Assist/`: `SheetBlocks` (sections, blank lines, <= 8 lines, markers excluded), `AnchorResolver` (same place, same line, +-2 lines via line diff, stale), `SuggestionApplier` (descending order, overlap refusal, insert-after, set-title, 4,096 limit, alternatives supersede), `IntentionalMarkMatcher` (word case-sensitive, phrase, line, not-in-text) (FR-006, FR-008, FR-011)
- [x] T013 [backend] Implement T012 in `src/StemStudio.Domain/Lyrics/Assist/`
- [x] T014 [P] [backend] Failing tests persistence: migration `AddLyricAssist` (existing sheets get revision 1, sensitivity balanced), revision retention 50, cascade delete of revisions/assists/suggestions/marks/assisted lines with the sheet, `LyricWords` uniqueness and limits, `LyricAssistSettings` single row (data-model 2-7)
- [x] T015 [backend] Implement T014 in `src/StemStudio.Persistence/` (entities, configurations, migration, stores)
- [x] T016 [P] [backend] Failing tests 010 compatibility: `PUT /api/lyrics/{id}` without `expectedRevision` unchanged; with a stale one `409 revision-conflict`; every save increments `revision`, prunes `AssistedLines`, re-anchors open suggestions; `LyricsSheet` responses carry `revision` and `assist` (FR-007, FR-024, SC-009)
- [x] T017 [backend] Implement T016 in `LyricsSheetService` / endpoints (010 owner code, extended)

### ai-agent

- [x] T018 [P] [ai-agent] Failing tests `tests/StemStudio.Agent.Tests/LyricAssist/`: `LyricPromptBuilder` (system prompt with licence rules R-03 step 6, language-specific explanation language, delimiters with a random boundary, whole sheet as context + block marked, dictionary misses listed, intentional marks and sensitivity hints), `LyricOutputParser` (schema-valid JSON, invalid JSON, extra fields, oversize arrays), `SuggestionValidator` (anchor equality, re-find once on the line, category allowed per kind, replacement limits, duplicate merge) (FR-005, FR-021, R-05)
- [x] T019 [ai-agent] Implement T018 in `src/StemStudio.Agent/LyricAssist/`
- [x] T020 [P] [ai-agent] Failing tests `LyricModelClient`: Ollama request with `format` = the output schema, `think:false`, temperature per kind, `num_ctx` 16384; Claude request with one forced tool `emit_suggestions` and no other tools; model selection via the 011 selector (default, fallback while a heavy GPU job runs); per-block timeout; one retry with a stricter instruction on invalid output, then skip; Claude token caps (20k/4k) stop with `budget-exhausted`; outbound guard: no Claude call unless selected (FR-021, FR-025, FR-026, SC-006)
- [x] T021 [ai-agent] Implement T020 (reusing `OllamaChatClientFactory`, `ClaudeChatClientFactory`, `AgentModelSelector`)

### frontend

- [x] T022 [P] [frontend] Failing bUnit tests of the pane shell in the 010 editor: `lyric-assist-pane`/`-toggle`, status from `GET /api/lyric-assist/status` (layers line, unavailable reasons, quality note), journey-level visibility per the ui-ids table, language guess for `unknown`, unsupported language disabled with reason, help links (FR-001, FR-027, FR-029, FR-030)
- [x] T023 [frontend] Implement T022 in `src/StemStudio.Web/Components/Lyrics/Assist/` and the API client (`StemStudioApiClient.LyricAssist.cs`)

### qa

- [x] T024 [P] [qa] Test strategy addendum `docs/testing/023-lyric-assist.md`: coverage map per FR/SC, fixture inventory (T009), fake stack scripts, injection/guardrail matrix, a11y plan (axe + one NVDA pass), evidence required per hand-off

---

## Phase 2: User Story 1 - Check with poetic licence (P1)

- [x] T025 [P] [US1] [backend] Failing tests deterministic layer: `HunspellSpellChecker` (fixture dictionaries; en = US or GB accepts; personal words; capitalised sentence start), `LicenceLexicon` (nl/en entries of R-03), code-switch rule, sound-spelling rule, `SuggestionFilter` (sensitivity loose/balanced/strict; recognised licences never `error`; marks; markers never checked), plus one test class over the real vendored dictionaries (FR-004, FR-009, FR-010, FR-012)
- [x] T026 [US1] [backend] Implement T025 (`src/StemStudio.Api/LyricAssist/HunspellSpellChecker.cs`, Domain services)
- [x] T027 [P] [US1] [backend] Failing tests `LyricAssistService` + endpoints: `POST /api/lyrics/{id}/assists` refusal order (404, revision, language, not-own, imitation, active, model, Claude notice), dictionary layer emits at once, model layer via a fake `ILyricAssistant`, one assist globally, cancel, restart -> failed interrupted, sheet deleted -> cancelled, events persisted and replayable (SSE `after`/`Last-Event-ID`, keep-alive, close after terminal), `GET` snapshot, list newest 20 (FR-002, FR-004, SC-004 structure)
- [x] T028 [US1] [backend] Implement T027 (`LyricAssistEndpoints`, `LyricAssistService`, `LyricAssistEventStream`); round start (013 on `main`): the assist runner awaits `LibraryGate.WaitWhileMaintenanceAsync` before starting a queued assist and between blocks (as `AgentRunExecutor`), with a test in T027
- [x] T029 [P] [US1] [backend] Failing tests suggestions API: list with filters, accept one / many as one revision and accept group, stale skipped, overlap 409, too-long 422, reject, mark intentional from a suggestion (word/phrase/line), marks CRUD and limits, personal words CRUD, undo (newest accept only, `undo-blocked-by-edit`, reopen suggestions, remove assisted lines), revisions list and restore, `PUT assist-settings` (FR-006 - FR-012)
- [x] T030 [US1] [backend] Implement T029 (`SuggestionService`, `LyricsRevisionService`, endpoints)
- [x] T031 [P] [US1] [ai-agent] Failing tests `LyricAssistant.RunAsync` for `kind: check`: blocks sequential, dictionary misses classified by the model (`misses`), per-block streaming to the sink, progress, notes (block skipped, fallback model), counts and discarded counters, cancellation between blocks, adversarial fake (injection items, fake anchors, oversize text) never produces an invalid suggestion (FR-021, SC-003, SC-005)
- [x] T032 [US1] [ai-agent] Implement T031
- [x] T033 [P] [US1] [frontend] Failing bUnit tests: Check (saves unsaved changes first), progress, streamed suggestions into `lyric-assist-list`, highlights overlay (`lyric-highlight-*`, underline style per category, not colour only), accept / reject / mark intentional (scope menu) / add to my words, accept all for the filter, undo, stale and `revision-conflict` handling (reload + re-anchor), provenance markers, sensitivity radios, category filters, marks and words lists (FR-005 - FR-012, FR-024)
- [x] T034 [US1] [frontend] Implement T033
- [x] T035 [P] [US1] [frontend] Failing bUnit tests keyboard and screen reader: F8/Shift+F8, Alt+Enter, Alt+Backspace, Alt+I, Escape, Ctrl+Alt+C; `role=listbox/option`, `aria-activedescendant`; live-region texts of ui-test-ids.delta.md (FR-028, SC-008)
- [x] T036 [US1] [frontend] Implement T035
- [x] T037 [P] [US1] [qa] Failing Playwright `e2e/lyric-assist.spec.ts` "check": quickstart Q1 and Q2 on the fake stack (Dutch sheet; licences untouched at balanced, notes at strict; accept, accept all, undo, intentional mark respected, my words respected)

## Phase 3: User Story 2 - Devices and proposed poetic licence (P1)

- [x] T038 [P] [US2] [backend] Failing tests `SyllableCounterDutch`/`SyllableCounterEnglish` against the gold lists (>= 97 % / >= 95 %), `CmuPronunciationLexicon`, `RhymeKeyDutch`/`RhymeKeyEnglish` vectors, `RhythmTarget` math and section medians, density at the target duration (FR-014, FR-017, SC-007)
- [x] T039 [US2] [backend] Implement T038
- [x] T040 [P] [US2] [ai-agent] Failing tests `kind: devices`: device and licence types only, instruction passed, rhyme claims verified through the Domain rhyme key (wrong ones dropped as `failedVerification`), syllable before/after computed by the counter (claims ignored), target-aware prompt (FR-013, FR-014, SC-002)
- [x] T041 [US2] [ai-agent] Implement T040
- [x] T042 [P] [US2] [frontend] Failing bUnit tests: Suggest devices, selection-scoped requests, device/licence type labels, syllables before -> after, rhyme partner shown, rhythm target form and syllable gutter (`lyric-syllables-<n>` with `data-deviates`, updates after a typing pause through `POST /api/lyric-assist/syllables`), density line (FR-013, FR-017)
- [x] T043 [US2] [frontend] Implement T042
- [x] T044 [P] [US2] [qa] Playwright "devices": quickstart Q3 steps 1-2

## Phase 4: User Story 3 - Writing help (P2)

- [x] T045 [P] [US3] [backend] Failing tests rhyme endpoint (local only, <= 500 ms on the fixture word list, limit, perfect/near, syllables), `language-guess`, and writing kinds in `LyricAssistService` (alternatives need one line, continue insertion point, titles, `model-unavailable` when no model, `imitation` on instructions with the 009 lexicon and library artists) (FR-015, FR-016, FR-022)
- [x] T046 [US3] [backend] Implement T045 (`RhymeFinder`, `LanguageGuesser`, endpoints)
- [x] T047 [P] [US3] [backend] Failing tests `LyricsCorpusIndex` + `VerbatimGuard`: shingles of 7 words and full lines of 5+ words over 006 lyrics, refreshed on lyrics change, sheet guard thresholds (>= 3 lines and >= 25 %; cliché set passes), output guard withholds and counts (FR-003, FR-023, SC-005)
- [x] T048 [US3] [backend] Implement T047
- [x] T049 [P] [US3] [ai-agent] Failing tests writing kinds: alternatives (1-5, one group), continuation (1-4 lines, LF), titles (<= 5, 1-80 chars), temperature 0.8, every generated text passed through the verbatim guard port before the sink (FR-015, FR-023)
- [x] T050 [US3] [ai-agent] Implement T049
- [x] T051 [P] [US3] [frontend] Failing bUnit tests: Alternatives (count), Continue, Title ideas (sets the title as one undo step), instruction field, rhyme finder panel (insert at cursor), withheld counter, not-own refusal, imitation refusal message (FR-015, FR-016, FR-022, FR-023)
- [x] T052 [US3] [frontend] Implement T051
- [x] T053 [P] [US3] [qa] Playwright "writing help" and "guardrails": quickstart Q3 steps 3-6 and Q5

## Phase 5: User Story 4 - Agent (P2)

- [x] T054 [P] [US4] [ai-agent] Failing tests `LyricAssistToolProvider` (8 descriptors: kinds, risks, undo, schemas with `additionalProperties:false`, projections without text, `+service lyricModel` for `suggest_lyric_lines`), registry selection for the `lyrics` context (28 tools; other contexts unchanged, cap 32), policy `lyrics-edit` (approval always, never plan-covered, never fresh), `artist-imitation` pre-check on instructions, `find_rhymes.word` provenance, `wait_for_job` phase `lyricAssist`, `PageContext.lyricsId` resolution, injection sheet via the adversarial agent fake (FR-018 - FR-020, SC-005)
- [x] T055 [US4] [ai-agent] Implement T054 (provider, loopback `ILyricAssistActions` port, policy rule, eval-set JSON cases 31-35 + S4)
- [x] T056 [P] [US4] [backend] Failing tests loopback actions for the 8 tools (`LoopbackLyricAssistActions`), and `POST /api/songs` snapshot `assistedLineCount` + take details (FR-024)
- [x] T057 [US4] [backend] Implement T056
- [x] T058 [P] [US4] [frontend] Failing bUnit tests: panel context sends `lyricsId` on `/lyrics/{id}`; `open_lyrics_editor` UI handler (`?assist=` opens the pane); approval card diff `agent-approval-lyrics-diff-*` rendered from the stored suggestions; take details `take-lyrics-assisted` (FR-018, FR-024)
- [x] T059 [US4] [frontend] Implement T058
- [x] T060 [P] [US4] [qa] Playwright "agent": quickstart Q4 with the scripted agent backend (no text in the tool log; approval reject/approve; undo)

## Phase 6: User Story 5 - Local, honest, degraded (P1)

- [x] T061 [P] [US5] [backend] Failing tests: `GET /api/lyric-assist/status` mirrors 011 status (none, Ollama down, model missing, fallback), `PUT /api/lyric-assist/settings` notice versioning, `claude-notice-required` before any Claude call, Check with no model runs the dictionary layer and lists the model layer unavailable, failing outbound handler records 0 requests in every assistant endpoint test (FR-025 - FR-027, SC-006)
- [x] T062 [US5] [backend] Implement T061
- [x] T063 [P] [US5] [frontend] Failing bUnit tests: Claude notice dialog (ack/cancel; nothing sent on cancel), disabled writing buttons with reason when the model is unavailable, fallback badge and estimate, usage line (Advanced), assist log (Expert), revision list (Expert) (FR-026, FR-027, FR-029)
- [x] T064 [US5] [frontend] Implement T063
- [x] T065 [P] [US5] [qa] Playwright "local-only": quickstart Q6 (backend none; fake Claude notice) with the outbound-request recorder

---

## Phase 6b: User Story 6 - Own-work override (P3, owner decision Q1)

- [x] T074 [P] [US6] [backend] Failing tests: `PUT /api/lyrics/{id}/own-work-override` (statement version must be current, name 1-80, `confirm: true`, `matchedTrackId` must be a current match else `409 override-not-applicable`, no match at all -> `409 override-not-applicable`), `DELETE` revokes (204; 404 when none), sheet guard allows only when every matching track equals the override's track (a second track -> `not-own-lyrics` again), the override never disables `VerbatimGuard` (fixture 9-word run still withheld) nor `imitation`, `SheetAssistInfo.ownWorkOverride` in sheet responses, the `not-own-lyrics` problem carries `matchedTrackId` and `overridable`, the override is deleted with the sheet (FR-031, SC-005)
- [x] T075 [US6] [backend] Implement T074 (`OwnWorkOverride` owned type in the `AddLyricAssist` migration, `OwnWorkOverrideService`, endpoints, guard change)
- [x] T076 [P] [US6] [frontend] Failing bUnit tests: `lyric-assist-own-work-open` on the refusal, dialog (statement text and version from `GET /api/lyric-assist/settings`, matched track, name, tick box; confirm enabled only when both are set; cancel sends nothing), badge `lyric-own-work-badge` with name/date/track, revoke with confirm, refusal shown again after revoke, keyboard and screen-reader labels; no override control in the agent panel (FR-031, FR-028)
- [x] T077 [US6] [frontend] Implement T076
- [x] T078 [P] [US6] [qa] Playwright "own-work override": quickstart Q5 steps 4-7 (refused -> confirm -> runs; verbatim still withheld; imitation still refused; revoke -> refused again) and agent case S5 (no override tool) on the fake stack

## Phase 7: Evaluation, polish, review (wave 2)

- [x] T066 [P] [ai-agent] Failing self-test of `tools/agent-eval --suite lyric` (grading of recall/precision/licence FP/latency with a scripted model; JSON conversion of `lyric-eval-set.md` in sync, test asserts case ids)
- [x] T067 [ai-agent] Implement T066 (`tools/agent-eval` suite, `lyric-eval-set.json`, report)
- [x] T068 [ai-agent] Reference-machine run (not CI): lyric suite with `qwen3.5:9b` (3 runs) and `qwen3.5:4b` (1 run), P1/P2 latency; report numbers vs SC-001/SC-004 in the hand-off; if Dutch misses SC-001 set the standing quality note text and flag owner question Q2 to the CTO (2026-11-24: run done, SC-001 failed in nl and en (recall 0 % on spelling, grammar and flow, latency met); standing quality note on; `docs/handoffs/cto-023-t068-reference-run.md`; follow-up 024)
- [x] T069 [P] [devops] Fake stack: mount `e2e/lyric-scripts/` and set `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT` in the E2E compose override and the non-Docker E2E script; CI job keeps no Ollama/Claude; image size check after vendored dictionaries (< +15 MB)
- [ ] T070 [qa] Run the full E2E suite green on the fake stack (T037, T044, T053, T060, T065) + axe; one manual NVDA pass recorded (quickstart Q9); audit the owners' coverage evidence (>= 90 % line and branch per component)
- [x] T071 [P] [quality-gate] SIG and static analysis over the 023 paths (unit size, complexity, parameters, duplication, dependency direction Web -> API client -> API -> {Agent, Domain, Persistence}; Domain has no package dependency); Sonar config includes the new paths; refactor requests to owners
- [x] T072 [P] [frontend] Help topic texts (6 topics, ui-test-ids.delta.md) and the docs impact notes for the user manual chapter "Lyric assistant"
- [x] T080 [CTO] **Decision point LanguageTool** (owner decision Q2, 2026-11-16: no for now): after T068, if Dutch grammar recall of `qwen3.5:9b` on the lyric suite is below 60 %, present the optional local LanguageTool service (LGPL-2.1, Java, ~1 GB RAM, compose profile off by default) to the owner as a separate follow-up with its own ADR note; otherwise record "closed" in CTO-DECISIONS (2026-11-24: triggered, Dutch grammar recall 0 %; presented to the owner with the CTO advice to defer until 024, because the cause is the prompt, not grammar knowledge) Owner decision 2026-11-24: **deferred**; revisit after the 024 reference run.
- [ ] T073 [CTO] Integration: merge lanes, `docs/public` user and technical manual updates (assistant, privacy, dictionaries, eval suite), PDFs regenerated, THIRD-PARTY-LICENSES (research R-10), STATUS; owner checks quickstart Q8/Q10; owner answers Q1-Q3 recorded

---

## Dependencies and order

- Phase 0 (T001-T011) blocks everything. T008 may finish during the wave but before T068.
- Foundational per lane before its stories: backend T012-T017, ai-agent T018-T021, frontend
  T022-T023, qa T024.
- US6 (T074-T078) depends on the sheet guard (T047/T048) and the assist endpoint (T028).
- T080 depends on T068.
- Stories: US1 first (it creates the assist, suggestion and accept machinery). US2 and US3 depend on
  US1's endpoints (backend T028/T030) and assistant (T032). US4 depends on US1 endpoints and the
  catalogue merge (T004). US5 can run after T028.
- Each implementation task depends on its failing-test task directly above it.

## Lane plan (at most 4 parallel agents)

| Wave | Lanes in parallel | Tasks |
|---|---|---|
| Phase 0 | architect (1) | T001-T011 |
| Wave 1 | **backend**, **ai-agent**, **frontend**, **qa** (4) | backend: T012-T017, T025-T030, T038-T039, T045-T048, T056-T057, T061-T062, T074-T075; ai-agent: T018-T021, T031-T032, T040-T041, T049-T050, T054-T055, T066-T067; frontend: T022-T023, T033-T036, T042-T043, T051-T052, T058-T059, T063-T064, T072, T076-T077; qa: T024, T037, T044, T053, T060, T065, T078 (written red against ids and fake scripts, turned green as lanes land) |
| Wave 2 | **ai-agent** (T068), **devops** (T069), **qa** (T070), **quality-gate** (T071) (4) | review, eval, E2E green |
| Integration | CTO | T080 (after T068), T073 |

Worktrees per lane: `.claude/worktrees/023-<lane>` on branches `023-lane-<lane>`; the CTO merges
in the order backend -> ai-agent -> frontend -> qa -> devops -> quality-gate fixes.

## Task counts per lane

| Lane | Tasks | Ids |
|---|---|---|
| architect | 11 | T001-T011 |
| backend | 24 | T012-T017, T025-T030, T038-T039, T045-T048, T056-T057, T061-T062, T074-T075 |
| ai-agent | 15 | T018-T021, T031-T032, T040-T041, T049-T050, T054-T055, T066-T068 |
| frontend | 17 | T022-T023, T033-T036, T042-T043, T051-T052, T058-T059, T063-T064, T072, T076-T077 |
| qa | 8 | T024, T037, T044, T053, T060, T065, T070, T078 |
| devops | 1 | T069 |
| quality-gate | 1 | T071 |
| CTO | 2 | T073, T080 |
| **Total** | **79** | |

Possible follow-up (not counted; only if decision point T080 is triggered and the owner then says
yes): optional LanguageTool service (devops compose profile, backend `IGrammarRules` adapter,
ai-agent eval comparison), as a separate 023 follow-up with its own ADR note.
