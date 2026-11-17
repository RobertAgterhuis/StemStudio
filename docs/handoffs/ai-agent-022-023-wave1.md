# Hand-off: ai-agent / 022 + 023 wave 1

Lane ai-agent of the 022+023 round. Worktree `.claude/worktrees/r22-agent`, branch `r22-lane-agent`, base `4486ddb7`.
Contracts used: tool catalogue 1.2.0 (contains 1.0.0, 1.3.0, 1.1.0, 1.2.0), eval set 1.1.0, lyric-assist-output 1.0.0,
backend API 1.23.0 DTOs. No contract changed. The CTO decisions of this wave are implemented: fake-script placeholders,
the factory seam (async), the security findings, and S5.

## Commits

| Commit | What |
|---|---|
| `7cd0347f` | fake agent script: `{{context.lyricsId}}`, `{{result.list_lyric_suggestions.ids:<category>}}` (CTO, qa T060) |
| `32f4fbf7` | 022 T020/T021 + T077/T078: library tools, group `library` 9, `library` context 32, group `lyric-assist`, `lyrics` context 28 |
| `94de8cad` | 023 T018/T019 (+ the validator parts of T040/T049): prompt builder, output parser, suggestion validator |
| `88c6af4a`, `9bf5871a` | seam: `ILyricAssistantFactory.ResolveAsync`, `AddLyricAssistAgent`, `LyricAssistant`, `LyricModelClient` (T021, T032) |
| `4d05cf52` | T020 tests (Ollama/Claude request shapes, outbound guard) |
| `9830de2c` | T031: assistant run loop and `AdversarialLyricModel` |
| `009f06a7` | T054/T055: 8 tools, `lyrics-edit` policy, find_rhymes provenance, `wait_for_job` phase `lyricAssist`, lyric problem codes |
| `dafdc93a` | CTO security review fixes 1-3 + S5 test |
| `1e953135` | 022 T083 + 023 T055 eval part: eval-set.json 1.1.0 (31-35, S4, S5, LIB-01..05), lyric and library fixtures |
| `60a772c9`, `6ec4a63d` | 023 T066/T067: `--suite lyric`, `lyric-eval-set.json`, performance run (`--sheet`), case filter |
| `233d13c6` | lyricModel service state tests (T088 coverage) |
| (last commit) | `--cpu` option of the lyric suite, tasks.md ticks, T008 outcome (research.md, ADR-037), this hand-off |

## What was built

**022 (catalogue 1.1.0).** `Tools/Providers/LibraryToolProvider.cs` has 4 tools. Names reach the model only as `untrusted_text`
(browse and search taint the run). Instrument labels are kept only when they are in the closed `InstrumentLabels` catalogue,
and channels only when they match the ChannelId pattern. `set_library_info` reads with GET first, then PUTs the merged
complete request: an omitted level keeps the read value, `null` means automatic. Its inverse is the same tool with the values
read before the change. A 409 `not-fileable` becomes a fixed readable error. Approval is needed only in a tainted run
(change risk). `ToolSelection.Library` no longer has `retry_track`/`delete_mix`/`rename_mix`/`duplicate_mix` (32 tools).
Nullable tool parameters (`OptionalOrNull`, schema type `["string","null"]`).

**023 model layer (`src/StemStudio.Agent/LyricAssist/`).**
- `LyricPromptBuilder`. The system prompt holds only fixed text and trusted values: licence rules R-03 step 6, the explanation
  language per sheet, the task per kind, sensitivity, rhythm target. Everything the user wrote (sheet, title, wish, misses,
  intentional texts) goes into the user prompt between `<<<NAME boundary>>>` delimiters. The boundary is 128 random bits per
  request; a boundary that occurs in the data is refused. The block lines are marked `*`.
- `LyricOutputParser`. Closed-schema check of lyric-assist-output 1.0.0. A broken root makes the output invalid (retry), and
  every broken item or miss is counted and dropped.
- `SuggestionValidator` + `SuggestionCandidate`:
  - every anchor is found again in the real text, exact first, then once more ignoring case;
  - the anchor must lie in the block and the requested range, and never on a marker or blank line;
  - types are limited per kind and per requested category;
  - corrections need a real one-line proposal and may change at most 60 % of the line (edit distance);
  - licences become `none`/`idea`;
  - writing kinds keep to their count, line and shape and share one `GroupId`;
  - syllables are always computed (claims ignored); rhyme claims are checked with `ILyricVerification.Rhyme` (partner word
    on the partner line); a syllable fit must move towards the rhythm target;
  - generated text goes through `IsVerbatim`; duplicates are merged; every discard is counted.
- `LyricAssistant`:
  - runs blocks in order, one stricter retry, then skips the block with a note; a timeout per block also skips with a note;
  - cancellation is checked between blocks;
  - notes for the fallback model and for verbatim text;
  - sums Claude usage; the input cap is checked before every call (also the retry, estimated as characters / 3); missing
    usage is estimated; it stops with `budget-exhausted`;
  - pins the backend: `ExpectedBackend` from the factory, and `LyricModelRequest.Model` on every call.
- `LyricModelClient` (011 selection rules, never switches to Claude):
  - Ollama: `ResponseFormat` = the schema (sent as `format`), `think:false`, `num_ctx` 16384, temperature 0.2 / 0.8,
    `num_gpu 0` for the fallback;
  - Claude: one forced tool `emit_suggestions` (input schema = output schema), thinking `disabled` (a forced tool choice does
    not allow thinking), `max_tokens` = what is left of the budget;
  - verified on the wire with stub handlers;
  - a call on a backend other than the pinned one is refused: `backend-unavailable`, no Claude request.
- `LyricAssistantFactory.ResolveAsync` + `AddLyricAssistAgent` (seam; details in my messages to the main session):
  - fake backend + `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT` -> `ScriptedLyricModel`;
  - ollama, or claude with a usable key and the notice -> the real client;
  - anything else -> `model-unavailable` with a fix text.
- `Fakes/AdversarialLyricModel`: schema-valid hostile items, plus invalid JSON first if wanted.

**023 tools (`LyricAssistToolProvider`, catalogue 1.2.0).**
- 8 text-free tools over `ToolInvocation.LyricAssist`. `AgentServices.LyricAssist` is filled from DI with
  `GetService<ILyricAssistActions>()`.
- The sheet revision is read before every write.
- Undo-only tools `undo_lyric_suggestions` and `delete_intentional_mark` are in `UndoTools`.
- `apply_lyric_suggestions`:
  - descriptor field `AlwaysApprovalReason = lyrics-edit`: always asks, never covered by a plan, never fresh;
  - pre-check: open ids of this sheet only.
- `find_rhymes`: the new descriptor field `UserWordArguments` makes the policy deny, with "Name the word you want rhymes
  for.", when the word is not in a user message.
- Both `instruction` arguments pass `artist-imitation`.
- `wait_for_job` phase `lyricAssist` reads `GetAssistAsync`; failure text is untrusted.
- `+service lyricModel`: from the 011 agent status (`ServiceStatusReader.LyricModel`, cached 10 s).
- The context line names `open lyrics sheet: <id>`.
- The lyric problem codes and `not-fileable` are fixed readable tool errors (`PortErrors`).
- Model and layer names pass `TrustedText.LayerName`.
- `ResultProjector`: forbidden names are still removed. Only `path` and `lines` may hold row lists, and only rows of numbers,
  ids, enum tokens or `untrusted_text` (security finding 2).

**Evaluation (`tools/agent-eval`).**
- `eval-set.json` 1.1.0, 45 cases (40 scored, 5 supplementary).
- New matcher `has`.
- Fixtures `FixtureLyricAssistActions` and `FixtureLibraryActions` (replaces the 501 skeleton).
- `eval-set.md` got the 022 section and the changelog line.
- Fake run: 40/40 scored, 5/5 supplementary.
- `--suite lyric`:
  - `lyric-eval-set.json` (64 cases, 11 guardrails, 2 performance sheets; a test checks that the ids match the Markdown);
  - recall / precision / licence FP per language and category, creative cases by count, latency;
  - self-test (ideal model and trap model) in CI;
  - `--backend ollama` is the T068 run, `--sheet` the performance run, `--cases` a filter.

## Tests and coverage

- `StemStudio.Agent.Tests`: 993 passed, 0 failed. `StemStudio.AgentEval.Tests`: 175 passed, 0 failed.
- Coverage of `StemStudio.Agent` (cobertura, package level, final run): **line 99.12 %, branch 94.33 %**.
- `dotnet format --verify-no-changes` is clean on all four projects.
- `StemStudio.Api` builds against the changes with 0 warnings.
- CI has no LLM: every test uses scripted or adversarial fakes and stub HTTP handlers.

## Security self-review

- **Risk and approvals.** The risks come from the catalogue (fixture test). `apply_lyric_suggestions` always asks through a
  descriptor field, so the model cannot influence it. `set_library_info` and `mark_lyrics_intentional` ask in a tainted run.
  A plan grant covers only `[risk-heavy]`.
- **Taint and provenance.**
  - Folder, item and sheet names are only `untrusted_text`. A test proves an injected sheet title makes apply ask for
    `[lyrics-edit, tainted]`.
  - Lyric results carry no text (FR-019); a row list with text is dropped (finding 2).
  - `find_rhymes.word` must come from the user.
- **Model output.** Nothing the model writes is trusted. The adversarial lyric model yields 0 suggestions and 0 miss verdicts
  in check, devices, alternatives and continue. Explanations are passed on as plain text; rendering escapes them (Blazor).
- **Injection through lyrics and metadata.**
  - The sheet is delimited data with a random boundary; the assistant model has no tools.
  - Titles that the factory, the tools or wait_for_job pass on are `untrusted_text`.
  - `l-inject` (S4) and the injection fixture pass.
- **Claude.**
  - No call unless Claude is selected; pinned per assist (finding 1); input cap before every call (finding 3).
  - The key is only reached through `IClaudeKeyProvider` and the factory (test: the sentinel key appears in no fix text).
  - Only prompts are sent; no lyrics of other songs, no files.
- **Known limits.**
  - A one-word string passes as a token in a whitelisted row.
  - A title idea may be any short text: only the verbatim guard checks it.
  - Ollama may switch to the CPU fallback model during an assist (011 rule).

## Requests (nobody-owned files; I did not edit them)

1. **`specs/011-ai-composer-agent/contracts/agent-script.schema.json`** (CTO decision, qa T060). Describe in the description of
   string argument values:
   - `{{context.lyricsId}}`: whole value, replaced by `PageContext.lyricsId`, empty when absent (same rule as trackId/mixId);
   - `{{result.<tool>.ids:<category>}}`: whole value, replaced per turn by a JSON array of the `suggestionId`s of the items with
     that `category` in the most recent `<tool>` result of the run; empty array when there are none; a script error when the run
     has no earlier call of `<tool>` or the tool is not a supported list tool (today only `list_lyric_suggestions`);
   - any other `{{...}}` text passes through unchanged.
2. **Port (architect):** add `ILyricAssistActions LyricAssist { get; }` to `IStemStudioActions`, or keep the separate DI
   registration. Today the agent takes `ILyricAssistActions` from DI (`GetService`, optional). The tools give a readable
   error when the host has none. **backend-023 (T056) must register `LoopbackLyricAssistActions` as `ILyricAssistActions`.**
3. **Port (architect):** no `GetStatusAsync` on `ILyricAssistActions`. I take `+service lyricModel` from the 011 agent status,
   which `GET /api/lyric-assist/status` mirrors (T061). If the status ever gets lyric-only conditions, add the read to the
   port.
4. **`StemStudio.AgentEval.csproj` (architect):** the T068 reference run measures the model layer with `HarnessVerification`
   (an approximation), because the harness references only `StemStudio.Agent`. For SC-001 numbers that include the
   dictionary layer, add a `ProjectReference` to `StemStudio.Domain` (+ the dictionaries), or run T068 through the API
   endpoint.
5. **Eval set label:** LIB-01..05 are scored in `eval-set.json` (rate = passed / 40). If the CTO wants them supplementary,
   set `"scored": false` on those 5 cases; nothing else changes.
6. **Owner-facing:** the forced Claude tool runs with thinking disabled. This could not be verified against the live API
   (CI has no Claude); if a Claude model rejects `thinking: disabled`, the call fails readably (`backend-unavailable`).

## T008 spike (S-01)

- **Where it ran.** One throwaway container of this lane: `ollama/ollama:0.40.13` (19 days old), name
  `r22-agent-ollama-spike`, port `127.0.0.1:11534`, own volume, GPU shared.
  - Free before the start: 70 GB RAM and 12.7 GB VRAM.
  - The container, volume and image were removed afterwards; GPU memory went back to 3.8 GB.
  - The owner's stack and the host Ollama were not used.
- Numbers and outcome: research.md "Phase 0 results" (the agreed one-time edit) and ADR-037 "Phase 0 outcome" (bullet
  T008).
- **9b warm:**
  - first model suggestion 1.2-5.8 s (nl), 3.9-22.4 s (en);
  - full check 24-32 s (nl, SC-004 met), 36-56 s (en, about 39 s p50: missed);
  - JSON validity 70/71, the invalid reply recovered on its retry;
  - **BlockLines = 8 kept.**
- **4b CPU:** 0/19 valid replies, about 60 s per attempt: every block skipped (SC-004 CPU target missed; to diagnose in T068).
- **Dutch first look:** weak on the model layer alone (0/8). A prompt variant gave 1/8 but more licence false positives;
  it was reverted, so the prompt is unchanged.
- **CTO decisions needed:**
  - merge short sections into 8-line blocks, or set the en target to 40 s;
  - the fallback block timeout and behaviour;
  - Q2 after T068.

## Mid-wave checkpoint answers

1. **Embedded schema**: noted. `StemStudio.Agent.csproj` keeps embedding the file from `specs/023-.../contracts/`, and I did
   not move it.
2. **Fake script reader**: `LyricAssistantFactory.ResolveAsync` reads `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT` (a file or a
   folder, e.g. `/lyric-scripts`) through `IEnvironmentReader` when the agent backend is `fake`, and picks the script with
   `LyricScript.Resolve(path, title, kind)`. Tests in `tests/StemStudio.Agent.Tests/LyricAssist/LyricAssistantFactoryTests.cs`:
   - `Fake_PlaysTheScriptOfTheSheetAndKind`
   - `Fake_WithoutAMatchingScript_AnswersEmptyBlocks`
   - `Fake_WithoutTheVariableOrWithABrokenScript_IsModelUnavailable`

   It is in commit `88c6af4a` and later; the checkpoint heads did not contain these commits yet. backend-023 must call
   `ResolveAsync` per assist instead of building a `ScriptedLyricModel` itself.
3. **Flaky API tests** (`tests/StemStudio.Api.Tests`, backend-owned; I did not edit them):
   - The tests: `ClaudeKeyUnreadableTests.OtherKeyRing_ClaudeRunFailsBackendUnavailableWithTheReason` and
     `AgentEndpointsTests.Executor_ClearsTheActiveRunWhenTheRunEnded`.
   - Both wait with `TestSupport/Eventually.WaitUntilAsync`: 200 attempts x 25 ms, so a budget of about 5 s of wall-clock time.
   - Under parallel load (several lanes building and testing, the host starting a second key ring), the background run
     executor needs longer than that, so the wait throws `TimeoutException`. Nothing in the agent library is
     timing-dependent here: the runner has no real-time waits.
   - Suggested fix (backend): make `Eventually` deadline-based, e.g. 30 s with `Stopwatch` and a 25 ms poll, instead of a
     fixed attempt count. Passing tests stay just as fast.

## Dependencies added

None (no new package; the Ollama spike image is a throwaway container, see T008).

## Next owner

- backend-023: merge `88c6af4a` + `9bf5871a` (and the commits before them), register `ILyricVerification` and
  `ILyricAssistActions`, call `AddLyricAssistAgent()`.
- qa: the placeholders are in `7cd0347f`.
- CTO: requests above. Wave 2 T068: `dotnet run --project tools/agent-eval -- --suite lyric --backend ollama --model qwen3.5:9b --runs 3`.
