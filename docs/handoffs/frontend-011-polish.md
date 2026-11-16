# Hand-off: frontend / 011 polish (T079d) - frontend lane complete

Branch `011-lane-frontend`. All 16 frontend tasks of feature 011 are done: T022, T023, T036-T039, T047, T048,
T056, T057, T062, T063, T066, T072, T073, T079d. Earlier checkpoints: `frontend-011-{foundational,us1,us2,us3,us4,us5}.md`.

## What was built (T079d)

- Keyboard: Enter sends, Shift+Enter keeps a new line (US1); **Escape closes the panel** (anywhere inside it).
- Focus: opening the panel captures focus (`IWebInterop.CaptureFocusAsync`) and moves it to the panel heading
  (`tabindex="-1"`); closing returns it. The approval card focuses its heading when it appears, **never Approve**.
- Live regions: streamed text (`agent-streaming`, polite) and the last changed step's status in a visually hidden polite
  region (`.ss-agent-step-live`, e.g. "start_detail_separation: waiting job"); unavailable state `role="status"`, errors
  `role="alert"`.
- Every button, input and textarea of the panel has an accessible name (tested).
- `agent-tool-log` (Expert, feature `agent.tool-log`): every step's tool, decision, status, exact arguments and result
  summary in a `details` element.
- `ConversationList` logic moved to a code-behind (SIG razor-code rule).

## qa lane requests (coordinator message) - status

| # | Request | Where | Test |
|---|---|---|---|
| 1 | `mixId` in the page context when a saved mix is loaded | `AgentPanelState.PageContext` (address `?mixId=` or the open mixer's `MixerState.MixId`) | `MixerAgentToolsTests.CommandFromTheMixer_CarriesTheLoadedMixId_EvenWithoutItInTheAddress`, `AgentPanelTests.Send_CreatesAConversation_AndPostsTheTextWithThePageContext` |
| 2 | agent mixer changes set `unsaved-indicator` | tools call the page's own handlers | `MixerAgentToolsTests.AgentChannelChange_GoesThroughThePageHandler_ToTheEngineAndTheFader` |
| 3 | panel stays open across `open_mixer` | panel state is circuit-scoped, rendered by the layout | `AgentAccessibilityTests.Panel_StaysOpen_AcrossTheOpenMixerNavigation` |
| 4 | `agent-input` disabled while unavailable | `AgentPanel.razor` | `AgentUnavailableTests.NotReady_ShowsTheReasonAndTheFix_AndDisablesTheInput` |
| 5 | history shows plan and steps | `AgentPanelState.RestoreRunAsync` | `AgentHistoryTests.Reload_OpensTheNewestConversation_AndShowsTheFinishedRun` (plan item + `agent-step-1` + `agent-step-error-1`) |
| 6 | Claude status names `ANTHROPIC_API_KEY` and the shell export | `AgentSettingsSection` | `AgentSettingsTests.ClaudeWithoutKey_IsDisabled_AndSaysNotConfigured` |

## Files changed

New: `src/StemStudio.Web/Components/Agent/{ToolLog.razor,ConversationList.razor.cs}`,
`tests/StemStudio.Web.Tests/Components/Agent/AgentAccessibilityTests.cs`.
Modified: `Agent/AgentRunView.cs` (`LastStepId`/`LastStep`), `Components/Agent/{AgentPanel.razor(.cs),ApprovalCard.razor,ConversationList.razor,AgentSettingsSection.razor.cs}`,
`wwwroot/css/components/agent.css`, tests `AgentPanelTests` (cancel waits for the call), `AgentHistoryTests`,
`AgentSettingsTests`, `MixerAgentToolsTests`; tasks.md (T079d).

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`: **Passed 1692, Failed 0** (three consecutive runs
green). `StemStudio.Web` line **99.39 %**, branch **94.11 %** (gate 90/90 passed). Agent code (`Agent/`,
`Components/Agent/`): line 2128/2140 (99.4 %), branch 1044/1136 (91.9 %). Only `tests/StemStudio.Web.Tests` was run
(no `test-all`, no Playwright, per the machine constraint).

## Quality checks

`dotnet build` 0 warnings (TreatWarningsAsErrors); `dotnet format --verify-no-changes` clean for Web and Web.Tests.
`tools/quality/sig_check.py` on `Agent/`, `Components/Agent/`, `Components/Settings/`, `Mixer.Agent.cs`: 36 files,
196 units, **0 errors**, 14 warnings (unit size 16-20 LOC, complexity 6-7; all below the hard limits 30 / 10).

## Dependencies added

None in the whole lane.

## Docs impact

User-visible (for T084a, user manual): Agent button and panel on every page (commands in Dutch/English, plan, steps
from Advanced, Expert tool log, cancel, undo, conversations with rename/delete from Advanced, restored after reload);
confirmation cards for heavy/destructive steps; the unavailable state with reason and fix; the new Settings page
(Ollama default, Claude opt-in and paid per use with a notice, None; model pickers; connection test); Claude token
usage and "Switch to local". Keyboard: Enter sends, Shift+Enter new line, Escape closes. Help topics
`agent-panel`, `agent-approvals`, `agent-backends` (screen topic of `/settings`).
Setup (technical manual): Claude needs `ANTHROPIC_API_KEY` exported in the shell that starts the API.

## Open issues / requests

- ai-agent / architect: mixer UI tools have no `trackId` argument; the host accepts an optional one (see us1).
- ~~`show_karaoke` (+mixer) is not handled by the Web app yet~~ - done, see "Follow-up: show_karaoke" below.
- The spec's "Continue" for interrupted runs has no contract operation and is not built.
- backend: resume replays the stream from the first event (`GET .../events` without `after`).
- qa: E2E can use only ui-test-ids 1.10.0; the extra hooks used in bUnit are an `aria-label="Conversation title"`
  input and `aria-label="Save title"` button for rename, and the class `.ss-agent-step-live`.

## Follow-up: show_karaoke

`show_karaoke {enabled}` (tool catalogue 1.0.0, feature 006, `+mixer`) is now a mixer UI tool (T039 pattern):
`MixerUiTools.ShowKaraokeAsync` -> `IMixerControls.SetKaraokeVisibleAsync` -> `KaraokeBanner.SetVisibleAsync`, the
same show/hide choice as `karaoke-toggle` (remembered in `localStorage["stemstudio.karaoke"]`; the banner attaches or
detaches the engine banner on its next render). Result `{previous, current}` (booleans). `enabled: true` without
synced (word/line timed) lyrics fails with "This track has no synced lyrics, so the karaoke banner cannot be shown.";
`enabled: false` always works (the inverse). No open mixer: "The mixer of the track is not open" (host, as for the
other mixer tools). `mixer_get_state` still omits the optional `karaoke?` field (shape not specified).
Tests: `MixerUiToolsTests` (5 new), `MixerAgentToolsTests` (2 new, page with the real banner), `AgentUiToolHostTests`
(no-mixer case). `dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`: Passed 1700, Failed 0; line
99.39 %, branch 94.15 % (gate 90/90). `dotnet format --verify-no-changes` clean (also fixed two pre-existing
whitespace findings); `sig_check.py` on `Agent/`, `Mixer.Agent.cs`, `Karaoke/`: 0 errors, no new warnings.

## Next owner

CTO (integration wave: merge `011-lane-frontend`, T084a manuals), qa (E2E T040/T049/T058/T067/T074 against the fake backend).
