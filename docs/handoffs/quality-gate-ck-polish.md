# Hand-off: quality-gate / 011 follow-up claude-key, polish (T051)

Branch `ck-lane-quality`. Nothing pushed or merged; no docker; Anthropic never called. Other lanes' branches were read
only (`git show`, `git archive` into a scratch directory); none of their files was edited.

## What was done

1. **SIG measurement of the integrated lane code.** Scratch snapshot = `ck-lane-backend` (backend + merged `ck-lane-agent`)
   with `src/StemStudio.Web` and `tests/StemStudio.Web.Tests` from `ck-lane-frontend`, `scripts/` from `ck-lane-devops` and
   `tools/quality` of this branch; `run_all.py --root <snapshot>`.
2. **Coverage review** of every touched component from the owners' hand-offs.
3. **Review of every [sec] test** against the threat model rows T1-T7 of plan.md (assertion quality: does the test assert
   the behaviour, not just a status code, and can it pass vacuously?).

## SIG results (integrated snapshot)

`run_all.py`: **2198 files, 17242 units (6130 production), 249 clones, 2 exemptions, 0 errors, 804 warnings -> exit 0**;
estimated maintainability **4.8 stars** (house estimate). `secret_rules.py`: 0 errors. No production clone.

| Component | Units | LOC | Size % (<=15/16-30/>30) | Complexity % | Params % | Stars | Clones |
|---|---:|---:|---|---|---|---|---:|
| Contracts | 61 | 110 | 100.0 / 0.0 / 0.0 | 100.0 / 0.0 / 0.0 | 95.5 / 4.5 / 0.0 | 5 / 5 / 5 | 0 |
| Persistence | 127 | 758 | 90.8 / 9.2 / 0.0 | 100.0 / 0.0 / 0.0 | 92.6 / 7.4 / 0.0 | 5 / 5 / 5 | 0 |
| Api | 1117 | 8038 | 89.6 / 10.4 / 0.0 | 97.9 / 2.1 / 0.0 | 73.0 / 27.0 / 0.0 | 5 / 5 / 3 | 0 |
| Api/Agent/Secrets | 70 | 478 | 86.4 / 13.6 / 0.0 | 100.0 / 0.0 / 0.0 | 88.7 / 11.3 / 0.0 | 5 / 5 / 5 | 0 |
| Web | 1147 | 5684 | 91.9 / 8.1 / 0.0 | 98.5 / 1.5 / 0.0 | 93.4 / 6.6 / 0.0 | 5 / 5 / 5 | 0 |
| Agent | 599 | 4033 | 100.0 / 0.0 / 0.0 | 100.0 / 0.0 / 0.0 | 71.7 / 28.3 / 0.0 | 5 / 5 / 3 | 2 |
| scripts | 127 | 784 | 100.0 / 0.0 / 0.0 | 98.6 / 1.4 / 0.0 | 91.8 / 8.2 / 0.0 | 5 / 5 / 5 | 0 |
| tools/quality | 537 | 2882 | 98.9 / 1.1 / 0.0 | 100.0 / 0.0 / 0.0 | 81.4 / 18.6 / 0.0 | 5 / 5 / 4 | 1 |

Soft warnings in new production code (refactor when touched, backend): `ClaudeKeyStoreServices::ReadAsync` 16 LOC,
`DataProtectionSetup::AddClaudeKeyStore` 17, `KeyRingStatus::LogStartupWarnings` 16, `ProtectedClaudeKeyStore::Read` 16,
`AgentSecretStore::UpsertAsync` 18 (all listed in the backend hand-off). The only complexity warning in `scripts` is the
pre-existing `image_licences.py::licence` (CCN 10, devops). Test code: about 30 7-12-line clones across the new key test
classes (shared `using` blocks and fixture set-up in `tests/StemStudio.Web.Tests/Settings/ClaudeKey*Tests.cs`, the
`ClaudeKey*Tests` of Api.Tests and Agent.Tests) and 16-29-line test methods; warnings only.

## Coverage review (owners' numbers)

| Component | Line | Branch | Source |
|---|---:|---:|---|
| StemStudio.Api | 98.97 % | 92.04 % | backend-ck-us-p2 (1618 passed) |
| StemStudio.Contracts | 100 % | 100 % | backend-ck-us-p2 |
| StemStudio.Persistence | 98.91 % | 95 % | backend-ck-us-p2 (114 passed) |
| StemStudio.Agent | 98.91 % | 94.18 % | ai-agent-ck-us-p2 (714 passed) |
| StemStudio.Web | 99.57 % | 93.79 % | frontend-ck-polish (1839 passed) |
| scripts (key_volume_checks) | 100 % | 100 % | devops-ck-polish (45 passed) |
| tools/quality | 100 % | 100 % | this lane (666 passed) |

All >= 90 % line and branch. Not re-run here (other agents build on this PC).

## [sec] review against the threat model

| Row | Evidence (tests) | Verdict |
|---|---|---|
| T1 database copy | `ProtectedClaudeKeyStoreTests` (row holds only ciphertext/last four/time; `stemstudio.db*` without the plaintext; ciphertext from another ring, corrupted, tampered, revoked -> unreadable), `ClaudeKeyLeakTests` (db, `-wal`, `-shm`), `DataProtectionSetupTests` (ring refused inside DataDir, BackupDir, content root, git tree) | covered; R1, R2, R4 |
| T2 backup/export | 013 not built; rules sit in 013 T035/T048/T062 | nothing to test yet |
| T3 `api-data` volume | `scripts/tests/test_key_volume_checks.py` (`api-keys` only in `api`, nothing else at `/keys`, env, Dockerfile 0700/`$APP_UID`) | covered; R5 |
| T4 logs, problems, SSE, UI | `ClaudeKeyEndpointsTests` (422/400/413/503 without the value, bad JSON never quoted, `UseHttpLogging(All)` logs nothing of the route, endpoint metadata), `ClaudeKeyLeakTests`, ai-agent `ClaudeKeyBackendTests.Sentinel_...` (stub echoes the key back; events, logs, failure, status, resolution clean), `ClaudeKeySourceTestTests.Test_RejectedKey_...`, `ClaudeApiKeyTests` (redacted `ToString`, `Reveal` only), `ClaudeKeyContractTests.StoreRequest_ToString...`, Web `ClaudeKeySectionTests` (password, `autocomplete=off`, emptied after success and failure, echoed problem detail never rendered) + static rule (c) | strong; R1, R3, R6, R7 |
| T5 CSRF | `LocalOnlyKeyEntryFilterTests.ForeignOrigin_IsRefused` (`http://evil.test`, `null`) | partly; R8, R9 |
| T6 018 remote device | 018 not built | nothing to test yet |
| T7 LAN before 018 | `LocalOnlyKeyEntryFilterTests` (loopback v4/v6/mapped, internal caller, other peers, no peer, host allow-list, filter before the body), `ClaudeKeyReplaceDeleteTests.Delete_IsLocalOnly`, `ClaudeKeyBackendTestTests.ClaudeTestFromAnotherDevice_...`, Web `PageOriginTests`, `ClaudeKeyLocalOnlyTests` | covered; R9, R10 |

The ai-agent tests are the strongest set (positive control through the stub's `x-api-key`, an echoing error body, every
output channel searched); the backend leak test should follow the same pattern (R2).

## Open issues / requests (test requests; none blocks the merge)

- **R1 architect (`tests/Shared/ClaudeKey/ClaudeKeySentinels.cs`)**: `SearchForms` finds base64 only when the sentinel starts
  on a 3-byte boundary. A key inside a larger base64 payload (e.g. base64 of the JSON body `{"apiKey":"..."}` in a log or
  a row) is not found. Add the three base64 alignments (encode `pad + sentinel` for pad of 0, 1, 2 bytes and drop the first
  and last 4 characters) and a distinctive core (`"-sentinel-" + new string('x', 38)`) that also catches truncated or
  masked forms. Every leak search uses this helper, so one change strengthens all [sec] tests.
- **R2 backend (`ClaudeKeyLeakTests`)**: add a positive control: `api.AnthropicKeys.ShouldBe([ClaudeKeySentinels.Stored])`
  (and the run's failure code). Without it the zero-hit result in SSE and logs would also pass if the run never decrypted
  or used the stored key.
- **R3 backend (`TestSupport/ListLoggerProvider`)**: `BeginScope` returns null and records nothing, so a key in a log scope is
  never searched. Record the scope state (`ToString()`) and include it in the leak searches (rule (c) only guards the
  secrets folder and the Claude backends statically).
- **R4 backend (`DataProtectionSetupTests.MisplacedDirectory_...`)**: add rows with non-normalised paths (`data/../data/keys`,
  `data/./keys`, a trailing separator) and, on Windows, a case variant (`DATA\keys`), all -> `inside-library`; decide and pin
  the ancestor case (key ring directory that contains DataDir or BackupDir, e.g. the temp root itself).
- **R5 devops (`scripts/tests/test_key_volume_checks.py`)**: most negative tests assert only `is not None`
  (`test_volume_check_without_top_level_volumes`, `test_api_mount_must_be_a_named_volume`, `test_missing_api_service_...`,
  `test_api_must_not_mount_the_volume_twice`, `test_short_syntax_strings_...`, `test_key_ring_env_missing`,
  `test_internal_caller_must_be_web`, the Dockerfile tests). Assert that the problem names the service, path or line that is
  wrong, so a check failing for another reason does not pass.
- **R6 backend (`ClaudeKeyBackendTestTests.ClaudeTest_UsesTheKeyOfTheRequestedSource`)**: the body check uses only the plain
  sentinels; use `SearchForms` for both sentinels and also search the Trace logs of the probe path.
- **R7 backend (`ClaudeKeyUnreadableTests.OtherKeyRing_ClaudeRunFailsBackendUnavailableWithTheReason`)**: also assert that
  the run failure and its SSE events do not contain the stored ciphertext.
- **R8 backend (new test, T5)**: pin the CORS policy of the key route: `OPTIONS /api/agent/settings/claude-key` with
  `Origin: <web origin>` and `Access-Control-Request-Method: PUT` (and `DELETE`) must not list the method in
  `Access-Control-Allow-Methods`; GET stays allowed. Today `WithMethods("GET")` is untested for this route, and widening it
  for another feature would silently remove the first line of T5.
- **R9 backend (`LocalOnlyKeyEntryFilterTests`)**: `ForeignOrigin_IsRefused` asserts only the status code and
  `HostOutsideTheAllowList_IsRefused` does not check the store. For every refused row assert the `local-only` problem
  (`ShouldBeLocalOnlyAsync`) and `(await _api.StatusAsync()).Stored == Absent`.
- **R10 backend (new test, T7)**: regression guard against forwarded-header spoofing: a `PUT` from peer `192.168.1.20` with
  `X-Forwarded-For: 127.0.0.1` and `X-Forwarded-Host: localhost` is `403 local-only`. The API has no `UseForwardedHeaders`
  today; the test keeps it that way for this route.
- **R11 frontend (`PageOriginTests`, optional)**: add `http://localhost.:8080/` (trailing dot) as not local, to pin the strict
  match.
- **R12 ai-agent (`ClaudeApiKeyTests`, optional)**: `JsonSerializer.Serialize` of `ClaudeApiKey`, `ClaudeKeyResolution` and
  `StoredClaudeKey` contains none of the `SearchForms` (guards against a future public value property).
- **CTO**: regenerate `docs/quality/SIG-REPORT.md` after the merge; `run_all.py` on the integrated code is 0 errors.

## Tests and coverage

`cd tools/quality && uv run --frozen pytest -q --cov --cov-branch` -> 666 passed, line 100 %, branch 100 % (no code change
in this checkpoint besides the hand-off and task ticks).

## Quality checks

`run_all.py` on this branch: 2143 files, 16657 units, 220 clones, 0 errors, 745 warnings -> exit 0. On the integrated
snapshot: 2198 files, 17242 units, 249 clones, 0 errors, 804 warnings -> exit 0.

## Dependencies added

None.

## Docs impact

None.

## Next owner

CTO (integration wave T053-T056); backend, architect, devops for the test requests R1-R10 (frontend R11, ai-agent R12 optional).
