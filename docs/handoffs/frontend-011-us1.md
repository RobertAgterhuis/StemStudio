# Hand-off: frontend / 011 US1 (T036-T039)

Branch `011-lane-frontend`. Contracts: backend API 1.10.0, ui-test-ids 1.10.0, tool catalogue 1.0.0.

## What was built

- **Panel** (`Components/Agent/`): `AgentPanel` (complementary `aside`, `aria-label="Composer agent"`,
  `data-help-id="agent-panel"`), `AgentToggle` in the context bar (`agent-toggle`, `aria-expanded`), `BackendBadge`
  (`data-backend`/`data-model`/`data-fallback`, tooltip = `model.changed.reason`), `ConversationList`
  (`aria-current`), `MessageList` (`agent-message-<seq>`, `data-role`; `agent-streaming` polite live region while
  text streams), `PlanView` (`agent-plan`, `agent-plan-item-<index>` `data-status`), `StepList`/`StepRow`/`StepDetails`
  (rows `agent-step-<seq>` from Advanced; progress `agent-step-progress-<seq>` and errors `agent-step-error-<seq>` at
  every level), `agent-run-error` (message + fix), `agent-error` (failed panel call), `agent-input` (max 4,000;
  Enter sends, Shift+Enter new line - done server-side by stripping the newline the browser inserts after an Enter
  keydown, no JS), `agent-send`, `agent-cancel`, `agent-new-conversation`. Rendered by `MainLayout` while open
  (`ss-shell-agent-open` reflows like help at >= 1280 px). CSS `wwwroot/css/components/agent.css` (tokens only).
- **State** (`Agent/AgentPanelState*.cs`, scoped): status + conversations load, send (creates a conversation when
  none is open, page context from the address via `AgentPageContexts` + the open mixer's mix id), follows the run
  stream with an immutable `AgentRunView` (all 14 event types), reloads the stored answer and list after the terminal
  event, recovers from a lost stream with the run snapshot, cancel (`cancelJobs: false`).
- **UI tools** (`Agent/AgentUiToolHost`, `MixerUiTools`, `NavigationUiTools`, `UiToolArguments`, `Gain`): the host
  routes `ui.requested` to the layout tools (`open_track`, `open_mixer`, `navigate`, `open_help`, `search_help`) or the
  open mixer's tools (`mixer_get_state`, `mixer_set_channel`, `mixer_set_master`, `mixer_reset`, `transport`,
  `set_loop` by seconds or bars from the beat grid downbeats, `set_metronome`) and posts `{ok, result | error}`.
  The mixer page registers its tools after the engine starts (`Mixer.Agent.cs`, adapter over the page's own control
  handlers, so the engine, unsaved indicator and leave guard are unchanged) and removes them on dispose. Navigation
  goes through `NavigationManager`, so the 001 unsaved-changes guard asks the user. A `409 ui-result-not-expected`
  is ignored silently.
- Feature catalogue: `agent.steps` (A, E), `agent.steps-compact` (B), `agent.conversation-actions` (A, E),
  `agent.usage` (A, E), `agent.tool-log` (E). Help topics `agent-panel`, `agent-approvals`, `agent-backends`
  (area `start`; the last two get their anchors with US2/US4).

## Files changed

- New: `src/StemStudio.Web/Agent/{AgentPageContexts,AgentRunView,AgentPanelState,AgentPanelState.Conversations,AgentWire,AgentUiToolHost,IAgentUiToolHandler,MixerUiTools,MixerUiTools.Playback,NavigationUiTools,UiToolArguments,Gain}.cs`,
  `src/StemStudio.Web/Components/Agent/*.razor(.cs)`, `src/StemStudio.Web/Components/Mixer/Mixer.Agent.cs`,
  `src/StemStudio.Web/Help/topics/start/agent-{panel,approvals,backends}.md`, `src/StemStudio.Web/wwwroot/css/components/agent.css`,
  tests `tests/StemStudio.Web.Tests/{Agent/{AgentPageContextTests,AgentRunViewTests,AgentUiToolHostTests,MixerUiToolsTests}.cs,Components/Agent/{AgentPanelTests,AgentPanelStateTests}.cs,Mixer/MixerAgentToolsTests.cs,Fakes/{AgentRunFeed,FakeMixerControls}.cs}`.
- Modified: `Program.cs` (scoped `AgentPanelState`, `AgentUiToolHost`, `NavigationUiTools`), `Components/_Imports.razor`,
  `Components/App.razor` (sheet `agent`), `Components/Layout/MainLayout.razor(.cs)`, `Components/Shell/ContextBar.razor`,
  `Components/Mixer/Mixer.razor.cs` (register/release), `Experience/FeatureCatalog.cs`, `Fakes/PageTestContext.cs`
  (substituted `IAgentApiClient` with ready defaults and a pushable run feed), `Experience/FeatureCatalogTests.cs`, tasks.md.

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`: Passed 1619, Failed 0.
`StemStudio.Web` line **99.34 %**, branch **94.38 %** (gate 90/90 passed). Agent code (`Agent/` + `Components/Agent/`):
line 1472/1486 (99.1 %), branch 748/804 (93.0 %).

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean. `sig_check` on the agent paths: 0 errors, 8 warnings
(unit size 16-18 LOC: `FollowAsync`, `AgentRunView` handler table, `NavigationUiTools.ExecuteAsync`, two in
`SseReader`; complexity 6-7: `SetLoopAsync`, `LoopRange`) - all below the hard limits (30 LOC / 10).

## Dependencies added

None.

## Docs impact

User-visible: **Agent** button in the context bar on every page opens the composer agent panel (conversations,
messages, plan, steps, cancel); the agent can change the open mixer (faders in dB, pan, mute, solo, master, reset,
transport, A-B loop in seconds or bars, metronome) and navigate/open help. Three help topics added.

## Open issues / requests

- ai-agent / architect: mixer UI tools carry no `trackId`; the host accepts an optional `trackId` argument and
  answers "The mixer of that track is not open (the mixer of <title> is)" for another track, otherwise
  "The mixer of the track is not open" (the Web app does not know the other track's title).
- `show_karaoke` (+mixer, 006) is not handled yet ("No open page can run show_karaoke"); planned with polish if time.
- `mixer_reset` sets faders to 0 dB (gain 1.0) as the catalogue says, not to the 0.8 default of a new mixer.
- `open_track`/`open_mixer`/`navigate` return `opened: true` once navigation was requested; when the user declines
  the unsaved-changes guard the page stays (the result cannot observe the guard's answer).

## Next owner

frontend (US2: T047, T048).
