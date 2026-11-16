# Hand-off: backend / 011 US4 (T060-T061)

Branch `011-lane-backend`. Previous: `backend-011-us3.md`.

## What was built

- `GET /api/agent/settings` (`getAgentSettings`), `PUT /api/agent/settings` (`updateAgentSettings`),
  `POST /api/agent/settings/test` (`testAgentBackend`), all un-staged from `PendingAgentOperations`.
- `AgentSettingsRules` (pure): 400 for the request shape (`backend: fake`, empty or > 100 character model names, invalid
  JSON); 422 `validation` for a limit outside its backend-api 1.10.0 range (first broken field is named); merge of the
  partial update (model names trimmed; `acknowledgeClaudeNotice: true` stamps the acknowledgement time); DTO mapping
  (`claudeNoticeAcknowledged` only, never a key).
- `AgentSettingsService`:
  - **Ollama models are validated only when they change** (qa request 2, CI has no Ollama): a model that differs from both
    stored models must be listed by the status probe with `tools: true`, otherwise 422 `model-unavailable` with the
    `ollama pull` fix. Unchanged models, `backend: none` and limit-only updates never call the probe.
  - **Claude**, checked only when the request selects `backend: claude`: no `ANTHROPIC_API_KEY` -> 409 `claude-not-configured`;
    notice never acknowledged -> 422 `notice-not-acknowledged`. Limits stay editable while Claude is selected after the key
    was removed from the shell (the status endpoint reports not ready).
  - The key is read through `IEnvironmentReader` for presence only. `ProcessEnvironmentReader` is the `TryAddSingleton`
    default; the agent library's registration wins.
  - `POST settings/test` (`ollama`/`claude` only, else 400) goes through the new `AgentStatusProbe.TestAsync`: no 3 s cap
    (a model may need to load), but a missing or failing agent library returns `ok: false` with the fixed
    `AgentStatusProbe.FailureReason` (no exception text or URL).
- The `StemStudio__Agent__Backend=fake` environment override from US3 still wins over the stored row.

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T060 -> T061 | `510283d` | `f41fece` |

Review additions in the green commit (tests pass with it): `Test_AFailingProbeIsNotOkWithAFixedDetail`,
`Put_LimitsStayEditableWithClaudeAfterTheKeyWasRemoved`, `Put_TrimsModelNames`. The red commit had one unused `using`
(IDE0005 breaks the build), removed in the green commit.

## Tests and coverage

`dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true`: 1406 passed; StemStudio.Api **98.80 % line,
91.64 % branch**. Sentinel key test (`TheKey_NeverAppearsInResponsesLogsOrTheDatabase`): no response body, captured log
line or byte of the SQLite file contains the key.

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean; new units <= 4 parameters.

## Dependencies added

None.

## Docs impact

User manual: Settings > Agent - changing an Ollama model checks that it is installed with tool support; Claude needs
`ANTHROPIC_API_KEY` in the shell that starts StemStudio and the acknowledged notice.

## Open issues / requests

None.

## Next owner

backend (US5: T070/T071).
