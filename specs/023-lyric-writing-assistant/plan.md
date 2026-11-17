# Implementation Plan: Lyric Writing Assistant

**Branch**: `023-lyric-writing-assistant` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)

**Input**: [spec.md](spec.md); constitution 1.4.0 (claude-key branch; main shows 1.3.1 until that
merge); ADR-020 (agent), ADR-035 (Claude key), ADR-019 (010), ADR-018 D8 (imitation guard);
specs 004, 006, 010, 011 and the 011 claude-key follow-up.

**Note**: architect design (`/speckit-plan` equivalent). Binding decisions: ADR-037
(`docs/decisions/ADR-037-lyric-writing-assistant.md`). Evidence: [research.md](research.md).
Nothing is implemented; Phase 0 (tasks.md) turns the contract deltas into code.

## Summary

The 010 lyrics editor gets an assistant pane and the 011 agent gets eight tools. On an explicit
user action, the backend runs a deterministic local layer (Hunspell spelling with OpenTaal/SCOWL
dictionaries, personal words, intentional marks, a poetic-licence lexicon, syllables, rhymes) and
then, when a model is available, a tool-less assistant model call per block of <= 8 lines through
the 011 backends (Ollama `qwen3.5:9b` default, Claude opt-in). The model answers in a fixed JSON
schema; the server validates anchors, filters by sensitivity and licence rules, verifies rhyme and
syllable claims, withholds text that matches library lyrics, and stores the result as anchored
suggestions streamed to the editor over SSE. The text changes only through accept (one revision,
one undo step). The agent can start checks and writing help and apply stored suggestions only
through an always-approved card; its results never contain lyric text. Quality is gated by a
64-case nl/en evaluation suite in `tools/agent-eval`.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (Domain, Agent, API, Persistence, Web). Worker, generator and
audio engine unchanged.

**Primary Dependencies (new)**: WeCantSpell.Hunspell 7.0.1 (MPL-1.1 of a tri-licence); vendored
data: OpenTaal Hunspell nl (BSD-3-Clause), SCOWL Hunspell en_US/en_GB (MIT-like), CMU Pronouncing
Dictionary (BSD-2-Clause-style). Versions and SHA-256 pinned in Phase 0 (T006), each >= 14 days
old. Reused: Microsoft.Extensions.AI, OllamaSharp, Anthropic SDK (011).

**Storage**: SQLite, migration `AddLyricAssist` (data-model 2-7): `LyricsSheets` + `Revision`,
`Sensitivity`, `RhythmTarget*`; owned `IntentionalMarks`, `AssistedLines`; tables
`LyricsSheetRevisions`, `LyricAssists`, `LyricSuggestions`, `LyricAssistEvents`, `LyricWords`,
`LyricAssistSettings`.

**Testing**: xUnit v3 + Shouldly + NSubstitute (as 011); pure domain tests with test vectors (anchor
resolver, applier, filters, syllable gold lists, rhyme keys, verbatim guard); fixture dictionaries
plus one real-dictionary test class; `ScriptedLyricModel` and `AdversarialLyricModel` fakes; stub
handler that fails on any outbound request (SC-006); bUnit for the pane (keyboard, live region,
levels); Playwright against the fake stack (`STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT`); axe checks.
`tools/agent-eval --suite lyric` on the reference machine (not CI).

**Target Platform**: unchanged (Docker Desktop on Windows 11 / Linux; non-Docker path). No new
container.

**Performance Goals**: SC-004 (40 lines: dictionary < 1 s, first model suggestion <= 6 s p50, full
<= 30 s p50 with 9b warm; CPU fallback <= 180 s); rhymes <= 500 ms p95; syllable gutter request
<= 50 ms for 120 lines.

**Constraints**: on demand only; no text off the machine unless Claude is selected and the notice
acknowledged; own lyrics only; SIG limits; >= 90 % line and branch coverage per component.

**Scale/Scope**: sheets up to 4,096 characters (~120 lines); up to 400 suggestions per assist;
one assist at a time.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I Test-first, >= 90 % | Every task pair red -> green; fakes for model, dictionaries (fixtures), corpus and clock; no LLM in CI; eval harness outside CI | PASS |
| II SIG 4 stars | Pure, small domain services (one responsibility each); parameter objects (`LyricAssistJob`, `AnchorSpan`); quality-gate run over the new paths | PASS |
| III Contracts first | OpenAPI 3.1 delta (Redocly valid), UI ids delta, tool catalogue delta, model output JSON schema, eval set, all before the wave | PASS |
| IV Open source, lawful | MPL-1.1 / BSD-3 / MIT-like / BSD-2-style, all OSI or OSI-compatible permissive data licences; no new model weights; Claude only via the existing exception (the assistant is part of the AI composer agent's runtime, D1); own lyrics only, 006 lyrics never accepted, verbatim guard | PASS (see Complexity Tracking 1 for the dictionary data) |
| V Simplicity, honesty | No LanguageTool or new container (YAGNI, measured follow-up); layer and model shown per suggestion; quality note when Dutch misses the bar; limits of the verbatim check documented | PASS |
| Security | Sheet text treated as untrusted data for the model (no tools, delimiters, schema, anchor verification); explanations rendered as plain text; no key handling changes; inputs bounded | PASS |
| Docs (1.3.1) | User manual: lyric assistant chapter; technical manual: dictionaries, eval suite, privacy; hand-offs list the docs impact | PASS |

## Project Structure

### Documentation (this feature)

```text
specs/023-lyric-writing-assistant/
  spec.md  plan.md  research.md  data-model.md  quickstart.md  tasks.md
  checklists/requirements.md
  contracts/DELTA.md  backend-api.delta.openapi.yaml  redocly.yaml
            ui-test-ids.delta.md  tool-catalogue.delta.md  lyric-assist-output.schema.json
  evaluation/lyric-eval-set.md
docs/decisions/ADR-037-lyric-writing-assistant.md
```

### Source code (owners)

```text
src/StemStudio.Contracts/LyricAssist/*                       (architect T005, DTOs, routes, limits)
src/StemStudio.Domain/Lyrics/Assist/                         (backend)
  Ports: ISpellChecker, ILyricsCorpus, IPronunciationLexicon, IWordList
  SyllableCounter{Dutch,English}, RhymeKey{Dutch,English}, RhymeFinder, LicenceLexicon,
  SheetBlocks, AnchorResolver, SuggestionApplier, SuggestionFilter, VerbatimGuard,
  IntentionalMarkMatcher, RhythmTarget, LanguageGuesser, Resources/licence-lexicon.{nl,en}.json
src/StemStudio.Api/LyricAssist/                              (backend)
  LyricAssistEndpoints, LyricAssistService, SuggestionService, LyricsRevisionService,
  HunspellSpellChecker, CmuPronunciationLexicon, LyricsCorpusIndex, LyricAssistEventStream,
  Dictionaries/{nl,en_US,en_GB,cmudict}/ + PROVENANCE.md
src/StemStudio.Persistence/                                  (backend: entities, migration AddLyricAssist)
src/StemStudio.Agent/LyricAssist/                            (ai-agent)
  ILyricAssistant, LyricAssistant, LyricPromptBuilder, LyricOutputParser, SuggestionValidator,
  LyricModelClient (Ollama format / Claude forced tool), Fakes/{ScriptedLyricModel,AdversarialLyricModel}
src/StemStudio.Agent/Tools/Providers/LyricAssistToolProvider.cs  (ai-agent, 8 tools)
src/StemStudio.Agent/Policy/ToolPolicy.cs                    (ai-agent: lyrics-edit rule)
src/StemStudio.Web/Components/Lyrics/Assist/*                (frontend: pane, list, highlights overlay,
  syllable gutter, rhyme finder, marks, words, notice, revisions; agent approval diff)
tools/agent-eval/                                            (ai-agent: --suite lyric, lyric-eval-set.json)
e2e/lyric-assist.spec.ts, e2e/lyric-scripts/*.json           (qa)
tests/**                                                     (each owner, mirrors src)
```

**Structure Decision**: no new project. Pure language logic in Domain (no package dependency);
Hunspell, file loading and the corpus index in the API (infrastructure); everything that talks to a
model in `StemStudio.Agent`; UI in Web. Dependencies stay one-way (Web -> API client -> API ->
{Agent, Domain, Persistence}; Agent -> Domain ports only).

## Phases

- **Phase 0 (architect, before the wave)**: merge contract deltas into full documents (backend-api
  1.23.0, ui-test-ids 1.23.0, catalogue 1.2.0, eval set 1.1.0); Contracts DTOs; skeleton of ports and
  stubs; vendor and pin dictionaries; **spike T008** on the reference machine (block latency with 9b
  and 4b, JSON-format reliability, first Dutch impressions on 10 eval cases) -> adjust SC-004 and
  block size in research.md with an ADR-037 note.
- **Wave 1 (max 4 parallel lanes)**: backend, ai-agent, frontend, qa (tasks.md lane plan).
- **Wave 2**: quality-gate review, qa audit, ai-agent eval run on the reference machine (SC-001/002/004),
  CTO integration, docs/public update.

## Complexity Tracking

| # | Item | Why needed | Simpler alternative rejected because |
|---|---|---|---|
| 1 | ~10 MB vendored dictionary data (OpenTaal, SCOWL, CMUdict) in the repo | offline, reproducible, identical in Docker, non-Docker and CI; licences allow redistribution with notices | download at build: network in CI and non-Docker path, SHA drift; no deterministic layer: spelling would depend on a 9B model (Dutch quality unknown) |
| 2 | Suggestion persistence with revisions and re-anchoring | accept/reject must survive reloads and agent use; undo as one step; text must never change silently | client-only suggestions: lost on reload, the agent could not apply them, no undo across tabs |
| 3 | Second prompt family next to the agent loop | the sheet text must not enter the agent model's context; anchored structured output | run everything through the agent: violates the 011 sheet-text rule and gives free text |

## Owner decisions (2026-11-16, recorded in docs/decisions/CTO-DECISIONS-2026-11-15.md)

- **Q1 - Override for "matches imported lyrics": YES, with explicit confirmation.** For a false
  positive (e.g. a song of the owner's own band that was imported) the user confirms in a dialog
  that the text is their own work (statement `own-work.v1`, typed name, tick box). Stored per sheet
  (name, time, matched track), shown on the sheet, revocable; covers only the matched track; never
  disables the 7-word verbatim hold-back on generated text nor the artist-imitation refusal; no
  agent tool (spec US6, FR-031; data-model 2.3; tasks T074-T078).
- **Q2 - LanguageTool: NO for now.** No LanguageTool service in 023. **Decision point T080**
  (CTO with the owner) after the reference-machine lyric eval run T068: only if Dutch grammar recall
  of `qwen3.5:9b` is below 60 %, the optional local LanguageTool service (LGPL-2.1, Java, ~1 GB RAM,
  own compose profile, off by default) is proposed again as a separate follow-up with its own ADR
  note; otherwise the question is closed.
- **Q3 - Explanations in the sheet's language: YES.** Dutch sheet -> Dutch explanations; the UI,
  category names and help stay English (W7).

## Phase 0 outcome (2026-11-17): dependencies and licences

| Dependency | Version | Licence | Bundled how |
|---|---|---|---|
| WeCantSpell.Hunspell (NuGet) | 7.0.1 | MPL-1.1 (chosen from the MPL/GPL/LGPL tri-licence; unmodified) | package reference of `StemStudio.Api` |
| OpenTaal Hunspell nl (Dutch dictionary) | 2.20.23 (commit `1c22bc3d`) | BSD-3-Clause (chosen from BSD-3-Clause / CC BY 3.0) | vendored data + `LICENSE.txt` |
| SCOWL Hunspell en_US, en_GB-ise, en_GB-ize (English dictionaries) | 2026.02.25 | SCOWL permissive notice (MIT/HPND-style) + Kuenning BSD-style affix files | vendored data + `README_<dict>.txt` |
| CMU Pronouncing Dictionary (English syllables, rhymes) | commit `74790861` | BSD-2-Clause | vendored data + `LICENSE` |

No GPL or LGPL code or data is bundled; LanguageTool (LGPL-2.1) stays out (owner decision Q2). Details,
URLs and SHA-256: `src/StemStudio.Api/LyricAssist/Dictionaries/PROVENANCE.md`; research.md "Phase 0
results". THIRD-PARTY-LICENSES: CTO at integration (T073). Contract versions and refinements:
contracts/DELTA.md section 5. Spike T008 moves into the ai-agent lane of the wave (research.md).
