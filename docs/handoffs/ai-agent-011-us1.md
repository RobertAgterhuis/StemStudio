# Hand-off: ai-agent / 011 US1 (T024-T031)

Branch `011-lane-agent`. No LLM called; no key anywhere. Builds on `ai-agent-011-foundational.md`.

## What was built

- **Runtime** (`src/StemStudio.Agent/Runtime`):
  - `AgentServices`: one parameter object with the ports (`IRunStore`, `IConversationStore`, `IAgentSettingsStore`,
    `IRunEventSink`, `IStemStudioActions`, `IUiToolGateway`, `IUndoStore`), the `AgentToolRegistry`, the chat session, a
    `TimeProvider` and an `ILogger` (exception details are logged only there).
  - `IAgentChatSession.NextTurnAsync(ChatTurnContext, ct)` is asked **once per model turn** and returns a `ChatTurn`
    (client, model, usingFallback, change reason, backend `ChatOptions`, per-turn timeout). The first turn publishes
    `run.started`, a different model on a later turn publishes `model.changed` (US3's `AgentModelSelector` plugs in here).
  - `AgentRunner.ExecuteAsync`: own tool loop (no `FunctionInvokingChatClient`): turn -> streamed answer (`text.delta`
    coalesced per 100 ms) -> calls executed in order -> next turn; limits `MaxModelTurns`, `MaxToolCalls` (checked before
    each call), `MaxInvalidCalls` (stop after the 3rd), per-turn timeout via `TimeProvider` (`timeout`); usage summed
    (`usage.updated`); the answer is appended as the assistant message (<= 16,000 chars) and the conversation's
    `ActiveRunId` is cleared by the runner.
  - `StepExecutor`: lookup **among the offered tools only** (a registered tool that is not offered on the page, e.g. a
    mixer tool without an open mixer, is `rejected_invalid`), schema validation, `ToolPolicy.Decide`, then server handler /
    UI round trip / meta tool. `PageTransitions`: after `open_mixer`, `open_track` or `navigate` the next turn offers the
    tools of that page (needed for "search -> open mixer -> mute drums" in one command).
  - `TurnBuilder`: stable system prompt (`SystemPrompt.Text`, English, "Answer in the user's language"), history newest
    first within the budget (chars/3.5, default 12,288 tokens = num_ctx 16,384 - 4,096), page context as a short system
    line directly before the command, the run's transcript always kept; tools sorted by name (byte-stable prefix).
  - `PlanTracker` / `RunPlan` / `MetaToolRunner`: `set_plan` replaces pending items; calls link to the first pending item
    of their tool, unplanned calls are appended; done/failed per step; `plan.updated` on every change.
  - `JobWaiter` / `JobStatusReader`: polls every 2 s via `TimeProvider` without model turns (track, detail, lyrics, asset,
    generation endpoints; `waitingForStems`/`waitingForGpu`/`downloadingModel` = running), `step.updated` with progress and
    status `waiting_job`, `MaxJobWaitMinutes` -> `timeout`, cancellation honoured.
  - `ServiceStatusReader`: `+service` availability from `GET /api/generation/status`, cached 10 s; a failed read = unknown.
- **Providers** (`src/StemStudio.Agent/Tools/Providers`): `MetaToolProvider`, `Core001ToolProvider` (21 tools),
  `Detail002ToolProvider` (estimate GPU 1 min / CPU 7.5 min per audio minute, `Estimator` for the heavy card, undo
  `cancel_detail_separation`), `Device003ToolProvider`. Names, kinds, risks, undo kinds, groups and availability are
  asserted against catalogue 1.0.0 (`MatchesCatalogue`).
- **Backends/Fakes**: `ScriptedChatSession` for the E2E fake stack (qa requests 1 and 2, below).

### UI tool contract details the frontend needs

- **`mixer_get_state` page result (confirmed)**: `{trackId, mixId?, unsaved, position, playing, karaoke?, settings}` where
  `settings` is exactly the **`MixSettingsDto`** shape (camelCase, linear volumes 0-1: `name`, `masterVolume`, `channels`,
  `metronome`, `loop`, `expanded`, `detailChannels`). `save_mix_as` / `update_mix` deserialize `settings` into
  `MixSettingsDto` and post it; the model sees a dB projection (`MixProjections.State`). A page without `settings` is passed
  through unchanged; a missing mixer gives "No mixer is open; use open_mixer first.".
- Server tools that need the page (`save_mix_as`, `update_mix`) call `mixer_get_state` through a UI callback on the
  invocation; that round trip uses the **step id of the server step** (`ui.requested` carries `tool: mixer_get_state`).
- **The runner publishes `ui.requested`** (DELTA.md section 4 step 1) and then calls `IUiToolGateway.RequestAsync`; the
  backend's gateway must not publish it a second time.

## Security fixes in this checkpoint (coordinator findings)

1. **Run never stuck** (finding on T025): `ExecuteAsync` catches `RunStopException` (code + fixed message),
   `OperationCanceledException` (-> `cancelled`, `run.cancelled`) and any other exception (-> `failed`/`internal` with
   "The run failed because of an internal error.", detail only in the log). The terminal save, the conversation release
   and the terminal event run in a `finally` with `CancellationToken.None`, each guarded separately, so release happens
   exactly once even when the store throws. Model-client exceptions no longer carry their raw text. In the
   `StepExecutor`, handler/UI errors fail only the step with fixed reasons (`JsonException` -> "The page returned an
   invalid result.", `HttpRequestException` -> "StemStudio could not be reached.", timeouts -> "The step timed out.",
   anything else -> "The step failed because of an internal error." + log). Red 02a868c, green a006b00.
2. **External text in job/page errors** (finding on `RunPlan`): job errors are a fixed trusted message
   (`"The job failed."`, or one fixed text per `GenerationErrorCodeDto`) and the job's own text only as `untrusted_text`
   (cleaned, <= 300 chars) -> the run is tainted. Same for a page's UI error (`"The page could not execute the step."` +
   `untrusted_text`; the step keeps the page text for the panel). Track titles/artists, mix names (incl. the duplicated
   name) and `get_track.error` (nested `{untrusted_text}`) are untrusted in every projection. Red 1fad076, green 49d8b8b.

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T024 / T025 | 9323b52 | 220405c |
| T026 / T027 | 5caa981 | 5d24380 |
| robustness fix | 02a868c | a006b00 |
| external-text fix | 1fad076 | 49d8b8b |
| T028 / T029 | acc2f93 | 8fab3ac |
| T030 / T031 | e181d8f | dab3e21 |
| qa: scripted session | 82cb96c | 96d4d99 |
| SIG refactor | - | 330fc92 |

## Tests and coverage

`dotnet test tests/StemStudio.Agent.Tests -p:CollectCoverage=true`: **379 passed**, 0 failed.
`StemStudio.Agent`: **line 98.91 %, branch 95.65 %, method 98.5 %** (gate 90/90 passed).

## Quality checks

`dotnet format --verify-no-changes` (test project incl. src): clean. Build: 0 warnings (TreatWarningsAsErrors).
`uv run --with lizard python tools/quality/sig_check.py src/StemStudio.Agent`: 91 files, 262 units, **0 errors,
28 warnings** (unit size 16-24 LOC / complexity 6-8; refactor planned in T079b). `OllamaSharp`/`Anthropic` are not used
outside `Backends`; `Policy` references neither `Runtime` nor `Backends`.

## Dependencies added

None (Microsoft.Extensions.Logging.Abstractions comes with Microsoft.Extensions.AI).

## Docs impact

None (library internals).

## Open issues / requests

1. **qa** (requests): (1) `{{context.trackId}}` / `{{context.mixId}}` in script arguments are replaced with the page
   context of the run's command (empty when absent); (2) every run plays the script from turn 1 (`ScriptedChatSession`,
   one client per run id). Tests: `Backends/ScriptedChatSessionTests`. (3) "undo runs make no model turn" comes with US5
   (T068/T069).
2. **backend**: register `ScriptedChatSession(File.ReadAllText(STEMSTUDIO_AGENT_FAKE_SCRIPT))` as `IAgentChatSession` for
   the fake stack (DI registration in `AddStemStudioAgent()` follows with US2/US3, when `IAgentRunService` is complete).
   Your `IUiToolGateway` should not publish `ui.requested` (the runner does). `AgentActionException.Message` must stay
   free of external text (remote titles, URLs): the model sees it as trusted text.
3. **frontend**: the `mixer_get_state` result shape above.
4. Still open from the foundational hand-off: architect items 1-2, quality-gate item 3.

## Next owner

ai-agent continues with US2 (T041-T044).
