# Hand-off: ai-agent / 011 follow-up claude-key, checkpoint us-p2 (T036, T037) - lane complete

Branch `ck-lane-agent` (worktree `.claude/worktrees/ck-agent`). Nothing pushed or merged. Anthropic was never called
(stub handlers only); no key-shaped literal written. All ai-agent tasks of the follow-up are done (T007-T010, T021, T022,
T036, T037).

## Commits

| Commit | Task |
|---|---|
| d92f8601 | T036 red: `ClaudeKeySourceTestTests` (+ throwing overloads) |
| d3e97c89 | T037 implementation |

## What was built

- `ClaudeBackendProbe.TestAsync(AgentSettings, ClaudeKeyTestSourceDto source, CancellationToken)`: takes the key from
  `IClaudeKeyProvider.KeyFor(source)` and sends exactly one `GET /v1/models/{id}` with it; `BackendTestResultDto.KeySource`
  = the source of the tested key (null when none). A source without a usable key answers `ok: false` without a request:
  `NoStoredKey` ("There is no readable stored Claude key to test: save the key in Settings first."), `NoEnvironmentKey`,
  or for `active` the key state's reason (`ClaudeErrors.KeyUnusable`, e.g. the unreadable text, or `KeyMissing`).
  401/403 -> "Claude rejected the API key (HTTP 401/403)"; network failure and the 2 s timeout -> `ClaudeErrors.Unreachable`;
  a caller cancellation is rethrown. The old `TestAsync(settings, ct)` = `active`.
- `AgentStatusService.TestAsync(AgentBackendDto, ClaudeKeyTestSourceDto claudeKeySource, CancellationToken)` (public on the
  class); the port method `IAgentStatusService.TestAsync(backend, ct)` delegates with `active`. The source is ignored for
  non-Claude backends.

## Tests and coverage

| Command | Result | Line | Branch |
|---|---|---|---|
| `dotnet test tests/StemStudio.Agent.Tests -c Release -p:CollectCoverage=true` | **714 passed**, 0 failed | StemStudio.Agent **98.91 %** | **94.18 %** |

Quality: `dotnet format --verify-no-changes` (Agent src + tests) exit 0; `tools/quality/run_all.py` 0 errors, no finding in
the new or changed `StemStudio.Agent` source; `scripts/secret_scan.py` clean (3500 files).

## Requests

- **architect (port, needed by backend T038/T039)**: `IAgentStatusService` (architect-owned `Ports/`) has no way to pass
  `claudeKeySource`. Proposal: add `Task<BackendTestResultDto> TestAsync(AgentBackendDto backend, ClaudeKeyTestSourceDto
  claudeKeySource, CancellationToken cancellationToken);` to the port; `AgentStatusService` already implements exactly this
  signature, so adding it to the interface needs no ai-agent change. Until then the backend can only test `active`.
- **backend (T038/T039)**: pass `request.ClaudeKeySource ?? ClaudeKeyTestSourceDto.Active` through `AgentStatusProbe.TestAsync`
  once the port has the overload; `keySource` comes back in the result.
- Earlier requests stand (us-p1): register `IStoredClaudeKeyReader`, `Invalidate()` after `PUT`/`DELETE`, replace the
  direct `ANTHROPIC_API_KEY` reads in `AgentSettingsService`/`AgentStatusProbe`, use a well-formed env sentinel.
- **architect (T053)**: decisions of the foundational hand-off (environment value format-checked; one reason/fix pair;
  `ClaudeErrors.KeyMissing` text now mentions Settings).

## Dependencies added

None in the whole lane.

## Next owner

CTO: integration wave after the other lanes' hand-offs.
