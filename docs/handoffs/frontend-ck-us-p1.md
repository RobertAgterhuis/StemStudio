# Hand-off: frontend / 011 follow-up claude-key, checkpoint us-p1 (T025, T026, T030, T031)

Branch `ck-lane-frontend` (worktree `.claude/worktrees/ck-frontend`, from `011-claude-key-spec`). Nothing pushed or merged; no
docker command; Anthropic never called. Built against backend-api **1.21.0** and ui-test-ids **1.21.0** with a substituted
`IAgentApiClient` (the backend lane builds the endpoints in parallel). No key-shaped literal anywhere: tests use
`ClaudeKeySentinels` (run time), linked into Web.Tests.

## What was built

- **API client** (`IAgentApiClient`/`AgentApiClient`): `GetClaudeKeyStatusAsync` (`GET`), `StoreClaudeKeyAsync(apiKey)`
  (`PUT {apiKey}` via `StoreClaudeKeyRequest`), `DeleteClaudeKeyAsync` (`DELETE`), all on `ApiRoutes.AgentClaudeKey`, returning
  `ClaudeKeyStatusDto`; problems map through the existing `ProblemReader` (code + `reason` extension).
- **`ClaudeKeySection`** (`Components/Agent/ClaudeKeySection.razor(.cs)`), inside the Claude part of `AgentSettingsSection`, on
  every level (no `LevelGate`): `settings-agent-claude-key` group (`role=group`, labelled by its "Claude API key" heading);
  `-status` (`data-state`/`data-source`/`data-stored`, `aria-live=polite`, texts only from `lastFour`/`storedLastFour`/`storedAt`);
  `-env-override` note when the environment overrides a stored (readable or unreadable) key; `-input`
  (`type=password autocomplete=off spellcheck=false maxlength=512`, label "Claude API key"); `-save` ("Save key"; disabled when
  the input is empty or whitespace, while saving, when local-only applies or when `storeAvailable` is false); `-error`
  (`role=alert`).
  - **The value is never rendered**: the input has no `value` binding (the typed value only flows browser -> circuit via
    `oninput`); it is emptied after every save attempt, success or failure, by recreating the element (`@key` generation) before
    the call returns. The page never pre-fills it.
  - **Errors are fixed texts** (`Agent/ClaudeKeyErrors`): chosen by problem code, the 422 `reason` (`empty`, `prefix`, `length`,
    `characters`, `admin-key`, fallback format text), 503 `key-store-unavailable`, 413, 400 `invalid-body`, unreachable API,
    others "(status)". The problem `detail` is never shown, so even a misbehaving server cannot make the page echo a value
    (tested with the sentinel in `detail`).
  - Saving does not change the backend or the notice acknowledgement (no `UpdateSettingsAsync`/`TestBackendAsync` call). After a
    change the parent updates `claude.key`/`keyConfigured` locally (Claude radio enabled) and refreshes the panel status.
  - `settings-agent-claude-status` configured text is now "A usable Claude API key is available (see Claude API key below).";
    the 011 "set ANTHROPIC_API_KEY ..." text stays as the alternative.
- **`PageOrigin`** (`Agent/PageOrigin.cs`, scoped, registered in `Program.cs`): the host of the circuit's base URI, kept from the
  first read; `IsLocal` only for `localhost`, `127.0.0.1`, `[::1]` (case-insensitive). Not local -> `-local-only` notice
  ("The Claude key can only be entered on the computer StemStudio runs on ...") and the key controls disabled. A `403 local-only`
  answer sets the same state (no error text); another 403 stays an error.

## Files changed

- New: `src/StemStudio.Web/Agent/{ClaudeKeyText,ClaudeKeyErrors,PageOrigin}.cs`,
  `src/StemStudio.Web/Components/Agent/ClaudeKeySection.razor(.cs)`; `tests/StemStudio.Web.Tests/Settings/{ClaudeKeySectionTests,ClaudeKeyLocalOnlyTests,PageOriginTests}.cs`,
  `tests/StemStudio.Web.Tests/Agent/ClaudeKeyApiClientTests.cs`, `tests/StemStudio.Web.Tests/Fakes/ClaudeKeyData.cs`; this note.
- Modified: `src/StemStudio.Web/Agent/{IAgentApiClient,AgentApiClient}.cs`, `src/StemStudio.Web/Components/Agent/AgentSettingsSection.razor(.cs)`,
  `src/StemStudio.Web/Program.cs`; `tests/StemStudio.Web.Tests/StemStudio.Web.Tests.csproj` (links `tests/Shared/ClaudeKey/ClaudeKeySentinels.cs` only),
  `tests/StemStudio.Web.Tests/Fakes/{PageTestContext,TestNavigationManager}.cs`; tasks.md (T025, T026, T030, T031 ticked).

## Tests and coverage

`dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` -> **Passed 1772, Failed 0**;
StemStudio.Web **line 99.55 %, branch 93.53 %** (gate 90/90 passed). New files: `PageOrigin` 100/100, `ClaudeKeyErrors` 100/93.8,
`ClaudeKeySection` 100/75 (component class; its async parts 100/100), `ClaudeKeyText` 93.3/72.2 (the unreadable text is used
from T044). Only Web.Tests was run (other agents build on this PC).

Red commits first: `bff89d30` (T025, red: compile), `1d98180f` (T030, red: compile); green `d8998e03` (T026), `e0884c20` (T031).

## Quality checks

- `dotnet build src/StemStudio.Web -c Release` -> 0 warnings, 0 errors; `dotnet format` (Web, Web.Tests) `--verify-no-changes` -> exit 0.
- `sig_check.py` on the new and changed files -> 0 errors, 1 warning (pre-existing `AgentSettingsSection.SaveAsync` 17 LOC).
- `python scripts/secret_scan.py` -> 3498 files clean.

## Dependencies added

None.

## Docs impact

User manual (Settings -> Composer agent -> Claude API key): paste the key, **Save key**; the field empties at once; the status
names the source (environment or stored) and the last four characters only; the environment variable wins and a stored key is
then shown as "not used while ANTHROPIC_API_KEY is set"; the key can only be entered with StemStudio opened as
`http://localhost:8080` on the computer itself. Saving does not select Claude.

## Open issues / requests

1. **backend**: the Web shows the 422 text from `reason`, not from `detail`; please always send `reason`. The 403 must carry
   `type .../local-only` (the page keys the notice on it). `GET /api/agent/status` must fill `claude.key` (the page takes the
   initial key state from it; no extra `GET claude-key` on load).
2. **qa**: ids are as in ui-test-ids 1.21.0. Under Playwright the page is opened as `localhost` (fake stack) so the controls are
   enabled; a LAN-host test must open the page under a non-loopback host name.
3. Hand-off naming: the coordinator asked for `frontend-ck-<checkpoint>.md`; tasks.md names `<owner>-011-claude-key-<checkpoint>.md`.

## Next owner

frontend (T034 -> T035, T040 -> T041, T044 -> T045); CTO for review.
