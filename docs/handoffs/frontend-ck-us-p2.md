# Hand-off: frontend / 011 follow-up claude-key, checkpoint us-p2 (T034, T035, T040, T041, T044, T045)

Branch `ck-lane-frontend`. Nothing pushed or merged; no docker; Anthropic never called; sentinels built at run time.
Built against backend-api 1.21.0 / ui-test-ids 1.21.0 with a substituted `IAgentApiClient`.

## What was built

- **US3 replace / delete** (`ClaudeKeySection`, `ClaudeKeyDeleteDialog`): with a stored key (`data-stored` != `absent`, also
  overridden or unreadable) the field is hidden and `-replace` / `-delete` are shown. **Replace key** opens an empty field with
  **Save new key** and `-cancel` (cancel hides and empties it). **Delete key** opens `-delete-confirm` (`role=alertdialog`,
  labelled and described; focus moves to "Keep key"; Escape keeps); `-no` keeps, `-yes` calls `DELETE` and shows the resulting
  status. Neither changes the selected backend or the acknowledgement (a deleted key with Claude selected leaves Claude
  selected; the panel then offers Switch to local). Errors use the fixed texts of `ClaudeKeyErrors`.
- **CK-FR-016 note**: `AgentPanelState.NoteKeyChanged()` (called after every key change) remembers the active run;
  `KeyChangedDuringRun` is true only while that run is still active, and the agent panel shows "Key changed - applies to the
  next command. The active run keeps the key it started with; cancel it to use the new key now." (`role=status`,
  class `ss-agent-key-changed`); it disappears when the run ends. No note without an active run.
- **US4 Test key** (`ClaudeKeyTest`, client `TestClaudeKeyAsync(source)` = `POST /api/agent/settings/test`
  `{backend: claude, claudeKeySource}`): `-test` only when `data-state` is `environment`/`stored` or `data-stored=readable`;
  `-test-source` radio group (`-active` default, `-stored`) only when the environment key and a readable stored key both exist;
  `-test-result` (`role=status`, `data-ok`, `data-key-source` = wire name or empty) "Anthropic accepted the stored key (412 ms)."
  or the API's readable failure. Only on an explicit click: loading the page calls neither `TestClaudeKeyAsync` nor
  `TestBackendAsync`. A key change drops the old result (`@key` = status). `403 local-only` -> the local-only notice.
- **US5 unreadable**: `-unreadable` (when `data-stored=unreadable`, with or without the environment key) = the API's `reason`
  (fixed fallback text) + "Enter the key again: press Replace key and paste the key from the Anthropic console."; saving a new
  key replaces it. Agent panel: when the selected backend is Claude and not ready (no key, deleted or unreadable),
  `agent-unavailable` now also has **Switch to local** (`data-testid="agent-unavailable-switch-to-local"`, see request 1).
- All key controls (field, save, cancel, replace, delete, confirm-yes, test, source radios) are disabled under local-only or
  while a call runs.

## Files changed

- New: `src/StemStudio.Web/Agent/AgentPanelState.Key.cs`, `src/StemStudio.Web/Components/Agent/{ClaudeKeyDeleteDialog.razor,ClaudeKeyTest.razor,ClaudeKeyTest.razor.cs}`;
  `tests/StemStudio.Web.Tests/Settings/{ClaudeKeyReplaceDeleteTests,ClaudeKeyTestTests,ClaudeKeyUnreadableTests,ClaudeKeyTextTests}.cs`; this note.
- Modified: `src/StemStudio.Web/Agent/{IAgentApiClient,AgentApiClient,ClaudeKeyText}.cs`,
  `src/StemStudio.Web/Components/Agent/{ClaudeKeySection.razor,ClaudeKeySection.razor.cs,AgentPanel.razor,AgentSettingsSection.razor.cs}`,
  `src/StemStudio.Web/wwwroot/css/components/agent.css`; `tests/StemStudio.Web.Tests/Agent/ClaudeKeyApiClientTests.cs`.

## Tests and coverage

At `8f6041de` (end of US5): `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` -> **Passed 1823, Failed 0**;
StemStudio.Web **line 99.55 %, branch 93.73 %**. Final numbers (after T049) are in `frontend-ck-polish.md`.
Red first per task: `8e043dbe` -> `85657dd5` (T034/T035), `3c824435` -> `f8dca815` (T040/T041, + refactor `90d46668`),
`cfea9fe0` -> `8f6041de` (T044/T045).

## Quality checks

`dotnet build src/StemStudio.Web -c Release` 0 warnings; `dotnet format` Web and Web.Tests clean; `sig_check.py` on
`Components/Agent` and `Agent`: 0 errors (the 33-line `@code` of `ClaudeKeyTest` was moved to a code-behind);
`css_tokens_check.py` 0 errors; `secret_scan.py` clean.

## Dependencies added

None.

## Docs impact

User manual: **Replace key** / **Delete key** (with confirmation; does not switch the backend), **Test key** (free check, only
on click; choice of key when both exist), the "cannot be read on this installation - enter the key again" recovery, and the
panel note that a key change applies to the next command.

## Open issues / requests

1. **architect (ui-test-ids)**: `agent-unavailable-switch-to-local` is a new id (button inside `agent-unavailable` when the
   selected backend is Claude and not ready). ui-test-ids 1.21.0 defines `agent-switch-to-local` only inside `agent-run-error`,
   so I did not reuse it. Please add the row (1.21.1) or tell me the id to use. The key-changed note has no id yet either
   (class `ss-agent-key-changed`); propose `agent-key-changed` if qa needs it.
2. **backend**: `GET /api/agent/status` with backend Claude and no usable key should answer `ready: false` with a reason such
   as "Claude has no key." / the unreadable reason and a fix; the panel shows `reason`/`fix` as given.
   `POST /api/agent/settings/test` must return `keySource` for Claude.

## Next owner

frontend (T049, polish); CTO for review.
