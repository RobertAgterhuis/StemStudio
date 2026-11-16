# Hand-off: ai-agent / 011 follow-up claude-key, checkpoint us-p1 (T021, T022)

Branch `ck-lane-agent` (worktree `.claude/worktrees/ck-agent`). Nothing pushed or merged. Anthropic was never called
(stub handlers only); no key-shaped literal written.

## Commits

| Commit | Task |
|---|---|
| 51e98396 | T021 tests + T022 implementation (one commit: the red state was a build failure against the old factory signature) |

## What was built

- `ClaudeChatClientFactory(IClaudeKeyProvider keys, HttpClient? http = null)`: no more direct `IEnvironmentReader` read.
  `Keys` (the provider), `ActiveKey()` (throws `BackendUnavailableException` with the readable reason of the key state),
  `Create(model)` and `Create(model, ClaudeApiKey key)`; one SDK client cached per key **fingerprint**.
  `KeyConfigured` was removed (use `Keys.Resolve().Configured`).
- `ClaudeBackendProbe.Status` fills `ClaudeStatusDto.Key` and `KeyConfigured` from `IClaudeKeyProvider.Resolve()` (no request).
- New `Backends/ClaudeKeyStatus.ToDto(ClaudeKeyResolution)` -> `ClaudeKeyStatusDto` (public; **backend can use it for the
  key endpoints' 200 body** so both status paths map identically).
- `AgentStatusService`: Claude not ready without a usable key -> `reason` = the key state's reason + "Switch to local ..."
  (or `ClaudeErrors.KeyMissing`), `fix` = the key state's fix (or `ClaudeErrors.KeyMissingFix`).
- `ClaudeErrors.KeyMissing` text changed to "No Claude key is configured: enter it in Settings or set ANTHROPIC_API_KEY.
  Switch to local ..."; new `KeyMissingFix`, `KeyUnusable(reason, fix)`. A run with an unreadable stored key fails
  `backend-unavailable` with "<unreadable reason> Enter the key again. Switch to local ...".
- `ClaudeChatSession` pins the key of a run's first turn (`RunKeyPins`, bounded to `PinnedRuns` = 16): a key change applies to
  the next command; an active run keeps its key (CK-FR-016). The SDK client cache follows the fingerprint, so a run whose
  key was replaced still holds its own client.
- `AddStemStudioAgent()` registers `IClaudeKeyProvider` -> `ClaudeKeyProvider` over `IEnvironmentReader` and the host's
  `IStoredClaudeKeyReader`; when the host registers none, `NoStoredClaudeKeyReader` (nothing stored, `storeAvailable: false`).
- Tests: `Backends/ClaudeKeyBackendTests` (15), DI tests for the provider; the SC-006 sentinel proof now runs a Claude run
  with a stored key and with an environment key and searches events, logs, failure text, status and resolution for all
  `ClaudeKeySentinels.SearchForms`. `FakeEnvironment.SentinelKey` is now the well-formed `ClaudeKeySentinels.Environment`.

## Tests and coverage

| Command | Result | Line | Branch |
|---|---|---|---|
| `dotnet test tests/StemStudio.Agent.Tests -c Release -p:CollectCoverage=true` | 698 passed, 0 failed | StemStudio.Agent **98.85 %** | **93.64 %** |

Quality: `dotnet format --verify-no-changes` exit 0 (Agent src + tests); `run_all.py` 0 errors, no finding in new source
(test-unit size and duplication warnings only); `secret_scan.py` clean.

## Requests

- **backend (T024/T033/T043)**:
  - register `IStoredClaudeKeyReader` (`ProtectedClaudeKeyStore`) in the API host; `AddStemStudioAgent` picks it up
    (before or after the call, it is resolved lazily).
  - call `IClaudeKeyProvider.Invalidate()` after every `PUT` and `DELETE` of the key (the cache otherwise only follows
    environment changes).
  - `AgentStatusProbe.Fallback` and `AgentSettingsService` still read `ANTHROPIC_API_KEY` directly: switch them to
    `IClaudeKeyProvider.Resolve()` (`Configured`, `ClaudeKeyStatus.ToDto`) so a stored key allows `backend: claude`.
  - the Api tests' env sentinel `"sk" + "-ant-api03-SENTINEL-0123456789abcdef"` is 38 characters: once the API uses the
    provider it is **not** a usable key (format rule `length`). Use `ClaudeKeySentinels.Environment`.
- **frontend**: `ClaudeStatusDto.Key` is now filled by the agent status; the 011 status texts can use `key.reason`/`key.fix`.
- **architect**: decisions of the foundational hand-off (environment value format-checked; one reason/fix pair).

## Next

T036 -> T037 (explicit key test per source), hand-off `us-p2`.
