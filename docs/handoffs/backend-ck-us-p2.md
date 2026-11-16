# Hand-off: backend / 011 follow-up claude-key, us-p2 (T032, T033, T038, T039, T042, T043) - lane complete

Branch `ck-lane-backend` (worktree `.claude/worktrees/ck-backend`), with `ck-lane-agent` merged (ai-agent lane complete).
Nothing pushed. No docker, Anthropic never called (local stand-in handlers only), no key-shaped literal. All backend tasks
of the follow-up are done: T011-T016, T023, T024, T027-T029, T032, T033, T038, T039, T042, T043.

## What was built

- **T032/T033**: `DELETE /api/agent/settings/claude-key` (`deleteClaudeKey`, local-only, idempotent, removes a readable or
  unreadable row, invalidates the provider, returns the status; backend selection untouched). `PendingAgentOperations` is now
  empty: all three key operations are served. Replace = `PUT` over a row (one row, ciphertext/last four/time replaced).
- **T038/T039**: `POST /api/agent/settings/test` passes `claudeKeySource` (default `active`) through
  `AgentStatusProbe.TestAsync(backend, source, ct)`; the Claude case gets the local-only filter (`LocalOnlyForClaude()`,
  endpoint filter on the bound request), Ollama stays open. Because the port `IAgentStatusService` has no key-source
  overload yet, the probe calls `AgentStatusService.TestAsync(backend, source, ct)` when the registered service is the
  agent library's class, else the port method (active key).
- **T042/T043**: a host with another key ring starts, reports `stored-unreadable` with the provider's reason/fix, every other
  endpoint answers 200, a Claude run fails `backend-unavailable` with the reason and without any Anthropic request, `PUT`
  repairs it. The tests were green on the first run: the wiring of T016/T024 and ai-agent T022 already covers it, so T043
  needed no production change (recorded in the commit message).

## Tests and coverage (final numbers of the lane)

| Command | Result | Line | Branch |
|---|---|---|---|
| `dotnet test tests/StemStudio.Api.Tests -c Release -p:CollectCoverage=true` | 1618 passed, 2 skipped (Linux-only) | Api **98.97 %**, Contracts **100 %** | Api **92.04 %**, Contracts **100 %** |
| `dotnet test tests/StemStudio.Persistence.Tests -c Release -p:CollectCoverage=true` | 114 passed | Persistence **98.91 %** | **95 %** |
| `dotnet test tests/StemStudio.Domain.Tests -c Release -p:CollectCoverage=true` | 752 passed (Domain not touched) | Domain **98.97 %** | **94.52 %** |

## Quality

`dotnet build StemStudio.slnx -c Release` 0 warnings / 0 errors; `dotnet format --verify-no-changes` clean for the owned
projects; `tools/quality/run_all.py` **0 errors** (soft unit-size warnings of 16-18 lines in `KeyRingStatus.LogStartupWarnings`,
`ProtectedClaudeKeyStore.Read`, `ClaudeKeyRequestReader.ReadAsync`, `DataProtectionSetup.AddClaudeKeyStore`,
`AgentSecretStore.UpsertAsync`); `scripts/secret_scan.py` clean (3530 files). Data Protection is referenced only under
`src/StemStudio.Api/Agent/Secrets/`.

## Dependencies added

None (Data Protection and HTTP logging are in the shared framework `Microsoft.AspNetCore.App`).

## Cross-owner requests

1. **architect**: add `Task<BackendTestResultDto> TestAsync(AgentBackendDto backend, ClaudeKeyTestSourceDto claudeKeySource,
   CancellationToken cancellationToken)` to the port `IAgentStatusService` (same request as the ai-agent's us-p2 hand-off);
   then `AgentStatusProbe.TestWithAsync` drops its concrete-type check (one line).
2. **architect (T053)**: `updateAgentSettings` description ("needs `ANTHROPIC_API_KEY`") -> "a usable key"; the API now
   accepts `backend: claude` with a stored key and the 409 text names Settings. The 503 detail is the provider's
   `StoreUnavailableReason` text, not the contract example word for word.
3. **devops**: compose `StemStudio__DataProtection__KeyRingDir=/keys`, `StemStudio__Agent__KeyEntry__InternalCaller=web`; the
   Web app must call the API as `http://api:8081`. T003 spike: confirm the peer address the API sees for the `web` container
   (the filter resolves `web` by DNS per refused non-loopback write).
4. **quality-gate (T051)**: [sec] tests to review: `Agent/Secrets/{DataProtectionSetupTests,ProtectedClaudeKeyStoreTests,
   LocalOnlyKeyEntryFilterTests}`, `Agent/{ClaudeKeyEndpointsTests,ClaudeKeyLeakTests,ClaudeKeyBackendTestTests,
   ClaudeKeyReplaceDeleteTests,ClaudeKeyUnreadableTests}`; Persistence `Agent/AgentSecretStoreTests`.
5. **CTO**: the first merge commit of `ck-lane-agent` (d600b5ed) carries no Co-Authored-By trailer (git's default merge
   message); every other commit of the lane does.

## Next owner

CTO: integration wave (T053-T056) after the other lanes' polish hand-offs.
