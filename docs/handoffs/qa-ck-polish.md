# Hand-off: qa / 011 follow-up claude-key, polish (T048 audit) and lane summary

Branch `ck-lane-qa`. Read-only audit of the other lanes' branches (`git show`); no docker, no test execution, nothing in
other owners' paths changed. Earlier checkpoint: `qa-ck-us.md` (T046, T047).

## What was built

Audit of every security-relevant test of the follow-up against spec CK-FR/CK-SC and plan.md T1-T7: does it assert the
behaviour (sentinel absent in every search form, 0 Anthropic requests counted, state unchanged after a refusal, row
removed) and not only a status code; no key-shaped literal; coverage evidence of every owner.

### Key-shaped literals

`git grep -E '<prefix>[A-Za-z0-9_-]'` over `tests`, `e2e`, `src`, `scripts`, `tools` of ck-lane-backend, -agent, -frontend,
-quality, -devops: **0 hits**. `scripts/secret_scan.py` on this branch: 3496 files clean. Sentinels are built at run time
(`tests/Shared/ClaudeKey/ClaudeKeySentinels.cs`, `e2e/support/claude-key.ts`).

### Verdict per test file

| File (branch) | Verdict |
|---|---|
| Api `Agent/ClaudeKeyLeakTests` (backend) | WEAK: F1, F2 |
| Api `Agent/ClaudeKeyEndpointsTests` | OK (body + headers in all forms, status unchanged after 422/413/503, 0 Anthropic requests); F3 |
| Api `Agent/ClaudeKeyBackendTestTests` | OK (stand-in proven wired: received key == sentinel; 0 requests on 403/400/missing key) |
| Api `Agent/ClaudeKeyReplaceDeleteTests` | OK; small gaps F5 |
| Api `Agent/ClaudeKeyUnreadableTests` | OK (run fails `backend-unavailable`, 0 requests, backend stays Claude, logged once) |
| Api `Secrets/DataProtectionSetupTests` | OK (forbidden directory refused, nothing written, other name/purpose cannot decrypt) |
| Api `Secrets/ProtectedClaudeKeyStoreTests` | OK; database file scan has the F2 weakness |
| Api `Secrets/LocalOnlyKeyEntryFilterTests` | WEAK: F4 |
| Persistence `Agent/AgentSecretStoreTests` | OK (`COUNT(*) = 0` after delete; failed upsert keeps the row) |
| Agent `Backends/ClaudeKeyBackendTests`, `ClaudeKeyProviderTests`, `ClaudeKeySourceTestTests`, `Model/ClaudeApiKeyTests` | OK |
| Web `Settings/ClaudeKeySectionTests` | OK (markup without every search form after save and after failure) |
| Web `Settings/ClaudeKeyLocalOnlyTests` | WEAK: F6 |
| Web `Settings/ClaudeKeyReplaceDeleteTests`, `ClaudeKeyUnreadableTests` | OK; plain form only (F6) |
| Web `Settings/ClaudeKeyHelpAndAccessibilityTests` | OK |
| Web `Agent/ClaudeKeyApiClientTests` | WEAK: F6 |
| quality `tools/quality/tests/test_claude_key_rules.py` | WEAK on its own branch: F7 |

### Findings (owner -> request)

- **F1 backend** `ClaudeKeyLeakTests.cs` (~l.47, 61-63) + `ClaudeKeyApi.cs` (~l.84-88): Trace is enabled by configuration
  only; the test asserts `logs.ShouldNotBeEmpty()` but never that a `LogLevel.Trace` entry was captured, so it passes if
  the Trace filter does not apply. Add `Entries.Any(e => e.Level == LogLevel.Trace)` and
  `api.AnthropicKeys.ShouldContain(Stored)` (proves the run really used the key on the SSE path). *Confirmed by qa.*
- **F2 backend** `ClaudeKeyLeakTests.cs` (~l.27-34), `ProtectedClaudeKeyStoreTests.cs` (~l.97-115): the files are scanned
  after dispose + `ClearAllPools()`; `src` sets no WAL journal mode, so `-wal`/`-shm` never exist and that part of
  CK-SC-001 is vacuous. Record the journal mode in the test (or enable WAL and assert `-wal` exists), and copy the files
  with `FileShare.ReadWrite` while the host still runs. *Confirmed by qa (no `journal_mode`/WAL in `src`).*
- **F3 backend** `ClaudeKeyEndpointsTests.cs` (~l.142-179): the invalid-JSON body carrying the key and the 413 path are not
  run with Trace log capture (they are the log-risk paths); the 400 theory does not assert the status is unchanged.
- **F4 backend** `LocalOnlyKeyEntryFilterTests.cs` (~l.69-104): host/origin refusals and "filter before body" assert only
  the status code (one not even the problem type); no test refuses a foreign PUT over a stored key and then asserts
  `StoredLastFour == "STO1"` (exists for DELETE). The E2E test "local-only (API)" now covers this end to end.
- **F5 backend** `ClaudeKeyReplaceDeleteTests.cs`: delete of an unreadable key does not assert `RowAsync()` is null;
  replace does not assert exactly one row.
- **F6 frontend** `ClaudeKeyLocalOnlyTests.cs` (~l.51-62): after input no Save click + `DidNotReceive().StoreClaudeKeyAsync`
  and no markup scan; several tests search only the plain form instead of `SearchForms`; `ClaudeKeyApiClientTests`
  lacks a test that an `ApiException` built from a `detail` echoing the sentinel does not carry it.
- **F7 quality-gate** `test_claude_key_rules.py` (~l.395): `test_real_sources_are_clean` scans `REPO_ROOT`, but
  `src/StemStudio.Api/Agent/Secrets/` does not exist on `ck-lane-quality`; re-run after the integration merge.
- **F8 coverage gap**: Web Trace logs (circuit, Web -> API HttpClient) are scanned by no unit test (the E2E log scan reads
  the web container at its configured level only); CK-SC-002 "status polling: 0 requests" is proven at agent level
  (`ClaudeKeyBackendTests` probe without request) but not through the API, whose test host substitutes
  `IAgentStatusService`.

Priority: F1, F2 (vacuous), then F3, F7, then F4-F6, F8. None blocks the integration wave if the owners (or quality-gate
T051) accept them as follow-ups; F1 and F2 are one-to-three-line test changes.

### Coverage evidence per owner (latest hand-off)

| Owner | Source | Line | Branch | >= 90/90 | Raw output pasted |
|---|---|---|---|---|---|
| backend | `backend-ck-us-p2.md` | Api 98.97 %, Contracts 100 %, Persistence 98.91 %, Domain 98.97 % | Api 92.04 %, Persistence 95 %, Domain 94.52 % | yes | summary table only |
| ai-agent | `ai-agent-ck-us-p2.md` (ck-lane-agent) | Agent 98.91 % | 94.18 % | yes | summary table only |
| frontend | `frontend-ck-polish.md` | Web 99.57 % | 93.79 % | yes | summary line ("Passed 1839") |
| devops | `devops-ck-polish.md` | 100 % | 100 % | yes | yes (pytest-cov excerpt) |
| quality-gate | no `quality-gate-ck*` hand-off on `ck-lane-quality` yet | - | - | unknown | - |

The protocol asks for the real output; backend, ai-agent and frontend give numbers without the tool output. The CTO's Q1
run at integration is the authoritative evidence.

## Files changed

New: this note. Modified: `specs/011-ai-composer-agent/followups/claude-key/tasks.md` (T048 ticked).

## Tests and coverage

No code in this checkpoint. Validation of the qa lane (see `qa-ck-us.md`): `tsc --noEmit` 0, ESLint 0 warnings,
`playwright test --list` 26 tests in the two new files, secret scan clean.

## Quality checks

Not applicable (documentation only).

## Dependencies added

None.

## Docs impact

None.

## Open issues / requests

F1-F8 above (backend F1-F5, frontend F6, quality-gate F7, backend/qa F8); quality-gate T051 may fold them into its review.

## Next owner

CTO: integration wave (T053-T056); run `cd e2e && npm run test:claude-key` on the fake stack started without
`ANTHROPIC_API_KEY` (TEST-STRATEGY 17.10).
