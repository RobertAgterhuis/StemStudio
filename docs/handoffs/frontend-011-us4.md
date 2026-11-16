# Hand-off: frontend / 011 US4 (T062, T063, T066 [amend])

Branch `011-lane-frontend`. Contracts: backend API 1.10.0 (`getAgentStatus`, `getAgentSettings`,
`updateAgentSettings`, `testAgentBackend`), ui-test-ids 1.10.0 (`settings-agent-*`, `nav-settings`,
`agent-usage`, `agent-switch-to-local`).

## What was built

- New page **`/settings`** (`Components/Settings/SettingsPage.razor`) with the agent section
  (`Components/Agent/AgentSettingsSection.razor(.cs)`, form logic in `Agent/AgentSettingsForm.cs`) and the rail link
  `nav-settings` (before Help). Screen help topic = `agent-backends` (`route: /settings`, anchor on the radio group).
- `settings-agent-backend` (`role="radiogroup"`) with `-ollama`, `-claude` (disabled + "not configured" while
  `claude.keyConfigured` is false), `-none`. `settings-agent-ollama-status` (reachable, version, models; "(no tools)"
  marks models without tool calling). Model pickers `settings-agent-ollama-model` / `-fallback-model` (Advanced+,
  feature `agent.models`) list only tool-capable models, plus the stored one when it is not pulled.
  `settings-agent-claude-status` (`data-key-configured`, text never contains any key), Claude pickers
  `settings-agent-claude-model` / `-effort` (Expert, feature `agent.claude-options`).
- Choosing Claude shows `settings-agent-claude-notice` (paid per use, text only, what is never sent) and
  `settings-agent-claude-notice-ack`; saving Claude without the acknowledgement (stored or ticked) is refused in the
  page ("Acknowledge the notice ..."), otherwise `acknowledgeClaudeNotice: true` is sent. `settings-agent-test` (disabled
  for None) shows `settings-agent-test-result` ("Connected/Failed in N ms: detail", `data-ok`). `settings-agent-save`
  shows "Saved"; 409/422 and load errors appear in `settings-agent-error` (`role="alert"`). After a save the panel's
  status is reloaded so the badge follows.
- T066: `agent-usage` (Claude runs only, Advanced+, feature `agent.usage`): "Tokens: 12,345 input (9,000 cached),
  678 output" from `usage.updated` / the snapshot. `agent-switch-to-local` inside `agent-run-error` when the failed
  run's backend is Claude: `PUT /api/agent/settings {backend: ollama}` then status reload; a failure shows in
  `agent-error`. StemStudio never switches by itself (FR-020).

## Files changed

New: `src/StemStudio.Web/Agent/AgentSettingsForm.cs`, `src/StemStudio.Web/Components/Settings/SettingsPage.razor`,
`src/StemStudio.Web/Components/Agent/AgentSettingsSection.razor(.cs)`,
`tests/StemStudio.Web.Tests/Components/Agent/{AgentSettingsTests,AgentUsageTests}.cs`.
Modified: `Agent/{AgentPanelState,AgentWire}.cs`, `Components/Agent/AgentPanel.razor(.cs)`, `Components/Layout/NavMenu.razor`,
`Components/_Imports.razor`, `Experience/FeatureCatalog.cs` (+ test), `Help/topics/start/agent-backends.md`,
`wwwroot/css/components/agent.css`, tasks.md (T062, T063, T066).

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true`: Passed 1669, Failed 0.
`StemStudio.Web` line **99.43 %**, branch **94.15 %**. Agent code: line 1902/1908 (99.7 %), branch 926/1008 (91.9 %).

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean.

## Dependencies added

None.

## Docs impact

User-visible: new **Settings** page (rail link) to choose the agent backend (Ollama default, Claude opt-in and paid
per use with a notice to acknowledge, None), see Ollama/Claude status, pick models (Advanced/Expert), test the
connection. Claude runs show token usage (Advanced+); a failed Claude run offers "Switch to local".
Setup: Claude needs `ANTHROPIC_API_KEY` in the API's environment (the page says so; the key is never shown).

## Open issues / requests

- qa: the E2E level-visibility and screen-topic suites should include `/settings` (new route).

## Next owner

frontend (US5: T072, T073).
