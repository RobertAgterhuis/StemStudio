---

description: "Task list for the 011 follow-up claude-key (Phase 0 by the architect after ratification, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Claude key in Settings, stored encrypted (011 follow-up "claude-key")

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/](contracts/)
(DELTA.md, backend-api 1.21.0-delta.2 merged into the full 1.21.0, ui-test-ids 1.21.0), constitution 1.4.0 (**ratified by the
owner 2026-11-16**), ADR-020, ADR-035 (Accepted).

**Gate**: T001 first. No other task starts before the owner has ratified constitution 1.4.0.
**Gate passed 2026-11-16** (T001 done; Q1 keep LAN access, Q2 keep the ciphertext).

**Tests**: REQUIRED (constitution I). Every implementation task has a preceding red test task of the
same owner (red -> green -> refactor); each owner reports >= 90 % line AND branch coverage of its
component in its hand-off. **No task calls Anthropic**: stub handler and "must not be called"
handler (011 pattern). **No task writes a key-shaped literal**: sentinels are built at run time
(`"sk-" + "ant-" + ...`), because `scripts/secret_scan.py` fails on the prefix followed by a key
character (research R-07).

## Format: `[ID] [P?] [Story?] [owner] Description (refs)`

- **[P]**: parallel with other [P] tasks of the same phase. Tasks of different owners are parallel
  in the wave.
- **[US1]..[US6]**: user stories of spec.md. **[sec]**: security-relevant test, reviewed by
  quality-gate (T051).
- Owners: architect, backend, frontend, devops, qa, quality-gate, ai-agent (the Claude factory,
  probe and status service live in `StemStudio.Agent`, AGENT-PROTOCOL rule 1), CTO.
- Paths are relative to the repository root.

## Checkpoint hand-offs (binding, ADR-008)

`docs/handoffs/<owner>-011-claude-key-<checkpoint>.md` at `foundational`, `us-p1` (US1, US2, US6),
`us-p2` (US3, US4, US5) and `polish`.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| ai-agent | stored key | `IStoredClaudeKeyReader` (T005) | `FakeStoredClaudeKeyReader` |
| backend | key resolution and status | `IClaudeKeyProvider` (T005) | `FakeClaudeKeyProvider` |
| frontend | key endpoints | DTOs + `ApiRoutes.AgentClaudeKey` (T004) | fake `IStemStudioApiClient` |
| qa | UI ids, endpoints | `ui-test-ids.delta.md`, DELTA.md | fake stack |
| devops | volume, env | data-model 1 | - |

---

## Phase 0: Ratification, contracts and skeleton (CTO, architect; BEFORE the wave)

- [x] T001 [CTO] Owner ratification of constitution **1.4.0** (plan.md text, Q0) through `/speckit-constitution` with the sync impact report; ADR-035 -> Accepted; CTO-DECISIONS row -> decided; record the owner's answers to Q1 and Q2 in plan.md (and enable or drop T052) - done 2026-11-16: ratified as 1.4.0; Q1 (b) keep LAN access (T052 dropped), Q2 keep the ciphertext
- [x] T002 [architect] Merge `contracts/backend-api.claude-key.openapi.yaml` into the full backend document **1.21.0** and `contracts/ui-test-ids.delta.md` into the full `ui-test-ids.md` **1.21.0** (DELTA.md section 1) on the versions current at build time; Redocly 2.78.0 lint clean; extend `AgentContractTests` (full document contains base and delta; three new ids in `PendingAgentOperations`) - done 2026-11-16: full `contracts/backend-api.openapi.yaml` 1.21.0 (= 1.10.0 + delta.2; delta.2 renames the colliding 413 response to `ClaudeKeyTooLarge`) and `contracts/ui-test-ids.md` 1.21.0; `ClaudeKeyContractTests`, `ClaudeKeyUiTestIdsTests`; Api.Tests link 1.21.0 as the current document
- [ ] T003 [architect] **Deferred - run after the stack update** (2026-11-16: the owner's stack was being rebuilt during Phase 0; first wave item for devops with the architect, no docker command was run in Phase 0). Spike with devops on the reference machine (research R-04, R-05): (a) peer address seen by `api` for a Settings call from the host browser, from a LAN device and for a direct LAN call to 8081; (b) a named volume mounted at `/keys` inherits `0700`/`$APP_UID` from the image on first use; (c) the key ring and a protected payload survive `docker compose up --build`; record results in research.md (F-01..)
- [x] T004 [P] [architect] .NET contracts `src/StemStudio.Contracts/Agent/`: `ClaudeKeyStatusDto`, `ClaudeKeyStateDto`, `ClaudeKeySourceDto`, `StoredClaudeKeyStateDto`, `ClaudeKeyTestSourceDto`, `StoreClaudeKeyRequest` (sealed class, redacted `ToString`), `ClaudeStatusDto.Key`, `TestAgentBackendRequest.ClaudeKeySource`, `BackendTestResultDto.KeySource`, `AgentProblemTypes` (+4), `ApiRoutes.AgentClaudeKey`, `AgentContractLimits.ClaudeKeyBodyBytes = 1024`; DTO round-trip tests against the contract examples; a test that `StoreClaudeKeyRequest.ToString()` never contains the value [sec] - done 2026-11-16 (+ `ClaudeKeyInvalidReasons`, `AgentContractLimits.ClaudeKeyRequestMaxLength`; `ClaudeStatusDto.Key` defaults to `ClaudeKeyStatusDto.NotConfigured` so 1.10.0 call sites compile, the status service must fill it)
- [x] T005 [P] [architect] Port stubs `src/StemStudio.Agent/Ports/IClaudeKeyProvider.cs`, `IStoredClaudeKeyReader.cs`, `IStoredClaudeKeyWriter.cs` with XML docs; model signatures `ClaudeApiKey`, `ClaudeKeyResolution` and enums (data-model 3) throwing `NotImplementedException`; test doubles `FakeClaudeKeyProvider`, `FakeStoredClaudeKeyReader` in the shared test helpers - done 2026-11-16: `tests/Shared/ClaudeKey/` (namespace `StemStudio.TestDoubles.ClaudeKey`, linked into Agent.Tests and Api.Tests) with `ClaudeKeySentinels` (run-time sentinels); ports `IClaudeKeyProvider` (`Resolve`, `KeyFor`, `Invalidate`), `IStoredClaudeKeyReader` (`Read` -> `StoredClaudeKey`), `IStoredClaudeKeyWriter` (`StoreAsync`, `DeleteAsync`); the domain uses the Contracts enums (011 rule), data-model 3/4 updated
- [x] T006 [architect] Verify the skeleton (build 0 warnings, all tests and gates, `tools/quality/run_all.py`, `scripts/secret_scan.py`); hand-off `docs/handoffs/architect-011-claude-key-phase0.md` (dependencies: none new) - done 2026-11-16 (numbers in the hand-off)

**Checkpoint**: skeleton green; the wave may start (2026-11-16). T003 runs first in the wave, after the stack update.

---

## Phase 1: Foundational (blocks every story)

### ai-agent (tests first)

- [ ] T007 [P] [ai-agent] Failing tests `tests/StemStudio.Agent.Tests/Model/ClaudeApiKeyTests.cs`: trimming, each format reason (`empty`, `prefix`, `length` 39/257, `characters` incl. inner whitespace, `admin-key`), `LastFour`, SHA-256 `Fingerprint`, `ToString()` = `[redacted]`, `Reveal()` only path to the value [sec] (CK-FR-006, CK-FR-009)
- [ ] T008 [ai-agent] Implement `ClaudeApiKey` (format regex assembled from constants) to pass T007
- [ ] T009 [P] [ai-agent] Failing tests `Backends/ClaudeKeyProviderTests.cs`: the full state table of data-model 3 (env yes/no x stored absent/readable/unreadable), environment wins, whitespace env = unset, `storeAvailable` false passthrough, readable reason/fix texts (CK-FR-001, CK-FR-008, CK-FR-011)
- [ ] T010 [ai-agent] Implement `ClaudeKeyProvider` (over `IEnvironmentReader` + `IStoredClaudeKeyReader`, cached until `Invalidate()` or an env change) to pass T009

### backend (tests first)

- [ ] T011 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/Agent/AgentSecretStoreTests.cs`: upsert/replace in one transaction, delete idempotent, column limits, migration `AddAgentSecrets` applies on an 011 database (CK-FR-002)
- [ ] T012 [backend] Implement `AgentSecretEntity`, `AgentSecretStore`, migration `AddAgentSecrets` to pass T011
- [ ] T013 [P] [backend] Failing tests `tests/StemStudio.Api.Tests/Agent/Secrets/DataProtectionSetupTests.cs`: application name `StemStudio.Api` (payload from a provider with the same dir and name decrypts, a different name does not); directory guard refuses `DataDir`, `BackupDir`, content root and a git work tree (temp dirs) -> `storeAvailable` false and the API host still builds; Linux mode tightening to 0700 and the one-time warning when not owner (OS-conditional); DPAPI configured on Windows (OS-conditional) [sec] (CK-FR-003..005)
- [ ] T014 [backend] Implement `DataProtectionSetup` + `KeyRingDirectory` to pass T013
- [ ] T015 [P] [backend] Failing tests `Secrets/ProtectedClaudeKeyStoreTests.cs`: round trip; ciphertext from another key ring -> `Unreadable`; corrupted ciphertext -> `Unreadable`; revoked key -> `Unreadable`; `requiresMigration` -> re-protected and saved; `ClaudeKeyUnreadable` logged once per process with the exception type only (captured logger, no message, no ciphertext); the plaintext never in the row [sec] (CK-FR-002, CK-FR-011)
- [ ] T016 [backend] Implement `ProtectedClaudeKeyStore` (`IStoredClaudeKeyReader`/`Writer`) to pass T015

### quality-gate (tests first)

- [ ] T017 [P] [quality-gate] Failing rule tests in `tools/quality/tests/`: (a) `Microsoft.AspNetCore.DataProtection` referenced only under `src/StemStudio.Api/Agent/Secrets/`; (b) `StoreClaudeKeyRequest` and `ClaudeApiKey` are not `record` types; (c) no logging call with an argument named `apiKey`/`key` in `Agent/Secrets` and `Backends/Claude*` [sec]
- [ ] T018 [quality-gate] Implement the three rules in `tools/quality/` to pass T017

### devops (tests first)

- [ ] T019 [P] [devops] Failing checks `scripts/check-key-volume.{sh,ps1}` (+ `key_volume_checks.py` with pytest): `docker compose config` has volume `api-keys` mounted at `/keys` only in `api` (not `worker`, `web`, generator, singer), `StemStudio__DataProtection__KeyRingDir=/keys`, `StemStudio__Agent__KeyEntry__InternalCaller=web`; `docker/api.Dockerfile` creates `/keys` with mode 0700 owned by `$APP_UID` and declares `VOLUME ["/keys"]` (CK-FR-003, CK-FR-005)
- [ ] T020 [devops] Implement compose, Dockerfile and the compose comment block (key in Settings or `ANTHROPIC_API_KEY`; volume `api-keys` must not be copied with the library) to pass T019

**Checkpoint**: foundational hand-offs.

---

## Phase 2: User Stories 1, 2 - Enter the key, see which key is in use (P1) MVP

- [ ] T021 [P] [US1] [ai-agent] Failing tests: `ClaudeChatClientFactory`, `ClaudeBackendProbe` and `AgentStatusService` read the key only through `IClaudeKeyProvider`; client cache keyed by fingerprint; `ClaudeStatusDto.Key` filled; `keyConfigured` true for a readable stored key; a key change applies to the next run, an active run keeps its client; extend the 011 SC-006 sentinel test to stored keys [sec] (CK-FR-001, CK-FR-008, CK-FR-016)
- [ ] T022 [US1] [ai-agent] Implement to pass T021
- [ ] T023 [P] [US1] [backend] Failing tests `Agent/ClaudeKeyEndpointsTests.cs`: `GET` status for all states; `PUT` 200 with the status, 422 per reason with `reason` and without the value, 400 fixed text for bad JSON and missing field, 413 over 1 KiB, 503 when the store is unavailable; no request to the must-not-call Anthropic handler; backend and notice acknowledgement unchanged; `WithHttpLogging(None)` metadata and the logging interceptor (with a global `AddHttpLogging(All)` in the test host nothing of the key route is logged) [sec] (CK-FR-006, CK-FR-008..010, CK-FR-015)
- [ ] T024 [US1] [backend] Implement `ClaudeKeyEndpoints` and `KeyEndpointLoggingInterceptor` to pass T023
- [ ] T025 [P] [US1] [frontend] Failing bUnit tests `tests/StemStudio.Web.Tests/Settings/ClaudeKeySectionTests.cs`: status texts and `data-state`/`data-source`/`data-stored` for every state; env-override note; input `type=password`, `autocomplete=off`, empty after success **and** after failure; save disabled when empty or saving; 422/503/413 messages never contain the value; Beginner level shows the section (CK-FR-014, ui-test-ids 1.21.0)
- [ ] T026 [US1] [frontend] Implement `ClaudeKeySection` and the API client methods to pass T025
- [ ] T027 [P] [US1] [backend] Failing test `Agent/ClaudeKeyLeakTests.cs` (written at the start of the story, green at its end): sentinel saved through the API; 0 hits of the sentinel, its base64 and UTF-16 forms in all responses, Trace logs, SSE of a fake Claude run, `stemstudio.db`, `-wal`, `-shm` [sec] (CK-SC-001)

## Phase 3: User Story 6 - Key entry only on the computer itself (P1)

- [ ] T028 [P] [US6] [backend] Failing tests `Secrets/LocalOnlyKeyEntryFilterTests.cs`: peer loopback v4/v6 accepted; peer = resolved `InternalCaller` accepted; other peer -> 403 `local-only`; `Host` outside the allow-list -> 403; foreign `Origin` -> 403; the filter runs before the body is read; `KeyEntryRefused` log rate-limited, without body; GET status not filtered [sec] (CK-FR-012)
- [ ] T029 [US6] [backend] Implement `LocalOnlyKeyEntryFilter` to pass T028
- [ ] T030 [P] [US6] [frontend] Failing tests: `PageOrigin` captures the loopback host of the first request (`localhost`, `127.0.0.1`, `[::1]` accepted; IP, `web`, other names not); non-loopback circuit -> `settings-agent-claude-key-local-only` shown and controls disabled; a `403 local-only` answer shows the same notice [sec] (CK-FR-012)
- [ ] T031 [US6] [frontend] Implement `PageOrigin` and the notice to pass T030

## Phase 4: User Story 3 - Replace or delete the stored key (P2)

- [ ] T032 [P] [US3] [backend] Failing tests: `PUT` over an existing row replaces ciphertext, last four and time in one transaction; `DELETE` removes, is idempotent, deletes an unreadable row, keeps backend `claude` and the agent status then says "no key" with "Switch to Local"; local-only applies to `DELETE` (CK-FR-006, CK-FR-015)
- [ ] T033 [US3] [backend] Implement to pass T032
- [ ] T034 [P] [US3] [frontend] Failing tests: replace shows input + "Save new key" + cancel; cancel empties; delete opens the `alertdialog`, `no` keeps, `yes` deletes; panel note "Key changed - applies to the next command" while a run is active (CK-FR-014, CK-FR-016)
- [ ] T035 [US3] [frontend] Implement to pass T034

## Phase 5: User Story 4 - Test the key on request (P2)

- [ ] T036 [P] [US4] [ai-agent] Failing tests: `ClaudeBackendProbe.TestAsync` with `claudeKeySource` `active`/`stored`/`environment` uses the right key (the stub handler receives the `x-api-key` sentinel of the requested source), `keySource` in the result, missing requested source -> `ok: false` readable detail; exactly one request per test; 401/403/timeout/network texts (CK-FR-007, CK-SC-002)
- [ ] T037 [US4] [ai-agent] Implement to pass T036
- [ ] T038 [P] [US4] [backend] Failing tests: `POST /api/agent/settings/test` passes `claudeKeySource`, answers `403 local-only` for the Claude case from a non-local caller, Ollama case unchanged [sec]
- [ ] T039 [US4] [backend] Implement to pass T038
- [ ] T040 [P] [US4] [frontend] Failing tests: **Test key** shown per ui-test-ids rules, source choice only when both keys exist, result with `data-ok` and `data-key-source`
- [ ] T041 [US4] [frontend] Implement to pass T040

## Phase 6: User Story 5 - Recover from an unreadable stored key (P2)

- [ ] T042 [P] [US5] [backend] Failing tests (API host with a second, different key ring): API starts, status `stored-unreadable` with reason/fix, all other agent and library endpoints answer normally, Claude run fails `backend-unavailable` with the reason, `PUT` replaces the unreadable row and status becomes `stored` (CK-FR-011, CK-SC-004)
- [ ] T043 [US5] [backend] Implement the remaining wiring to pass T042
- [ ] T044 [P] [US5] [frontend] Failing tests: `settings-agent-claude-key-unreadable` text and fix; agent panel unavailable state offers "Switch to Local" for this reason
- [ ] T045 [US5] [frontend] Implement to pass T044
- [ ] T046 [P] [US5] [qa] E2E `e2e/tests/agent-claude-key.spec.ts` part 2: make the stored key unreadable by removing the key ring files (`docker compose exec api sh -c 'rm -f /keys/*.xml'` + `docker compose restart api` in `scripts/agent-e2e.*`), then assert the unreadable state and recovery by saving a new key

## Phase 7: Polish and cross-cutting

- [ ] T047 [P] [US1] [qa] E2E `agent-claude-key.spec.ts` part 1: save, status with last four, DOM and input value without the sentinel, replace, delete with confirmation, local-only notice under a non-allow-listed host name; must-not-call proof via the API's request counter of the fake Anthropic handler (CK-SC-001, CK-SC-002, CK-SC-005)
- [ ] T048 [qa] Audit: every [sec] test asserts the behaviour (not only status codes); no key-shaped literal in tests or fixtures (`scripts/secret_scan.py`); coverage evidence of all owners; hand-off
- [ ] T049 [P] [frontend] Help topic `agent-claude-key` (sources, precedence, what is and is not protected in plain words, recovery) with its help-content test; axe checks of the section (WCAG 2.2 AA) (CK-FR-018, CK-SC-006)
- [ ] T050 [P] [devops] Non-Docker path: `scripts/dev.{ps1,sh}` print the key ring directory; check script asserts the default per-user directory is outside the repository and the library; technical notes for the CTO's manual edit
- [ ] T051 [quality-gate] SIG report and coverage review of all touched components; review of every [sec] test against plan.md threat model rows T1-T7; `run_all.py` 0 errors; hand-off
- [ ] T052 **not selected (owner Q1, 2026-11-16: keep LAN access until 018)**. [devops] **Only if the owner chooses Q1 (a)**: publish `8080` and `8081` on `127.0.0.1` (018 FR-011 pulled forward, `STEMSTUDIO_BIND_ADDRESS` override) with the T019 check extended; Playwright container reachability re-checked (018 T003 note)

## Integration wave (CTO, architect)

- [ ] T053 [architect] Fold the follow-up into the 011 documents after ratification: `spec.md` FR-018 (key sentence -> CK-FR-001..003), FR-019 ("key configured" -> key status), SC-006 ("never in plain text"); `data-model.md` section 1 (pointer to this delta); `contracts/DELTA.md` status row; mark this folder "merged"
- [ ] T054 [CTO] `.claude/agents/ai-agent.md` key rule (plan.md sync impact), `SECURITY.md` (threat model summary), `README.md` quick note, compose comment review
- [ ] T055 [CTO] Public manuals: user manual (Settings -> Claude key, recovery), technical manual (volume `api-keys`, key ring directory, never copy it with backups, threat model limits); regenerate PDFs (`tools/docs`), commit sources, PDFs and `build-manifest.json`
- [ ] T056 [CTO] Run quickstart Q1-Q9b (Q10 by the owner if wanted); security sign-off of the threat model; `docs/STATUS.md`; close the follow-up

## Execution plan per owner (parallel wave)

| Owner | Sequence |
|---|---|
| ai-agent | T007 -> T008, T009 -> T010 (HO foundational) -> T021 -> T022 (HO us-p1) -> T036 -> T037 (HO us-p2) |
| backend | T011 -> T012, T013 -> T014, T015 -> T016 (HO foundational) -> T027 (red), T023 -> T024, T028 -> T029, T027 green (HO us-p1) -> T032 -> T033, T038 -> T039, T042 -> T043 (HO us-p2) |
| frontend | T025 -> T026, T030 -> T031 (HO us-p1) -> T034 -> T035, T040 -> T041, T044 -> T045 (HO us-p2) -> T049 (HO polish) |
| devops | T019 -> T020 (HO foundational) -> T050 (HO polish; T052 not selected, owner Q1) |
| qa | T047 (red against the fake stack as soon as T002 is merged) -> T046 -> T048 (HO polish) |
| quality-gate | T017 -> T018 (HO foundational) -> T051 (HO polish) |

## Dependencies & Execution Order

- T001 -> T002..T006 -> wave. Inside the wave only the owner sequences above apply; cross-owner
  dependencies go through the ports and DTOs of T004/T005 and their fakes.
- Integration wave after every polish hand-off: T053 -> T054, T055 -> T056.

## Definition of Done checklist (mirrors the constitution)

- [x] Constitution 1.4.0 ratified before implementation (T001, 2026-11-16)
- [ ] Tests green; >= 90 % line and branch per component; exact numbers in every hand-off
- [ ] Analyzers, `dotnet format`, SIG limits, `run_all.py`, `secret_scan.py` clean
- [ ] Contracts merged and Redocly-valid (backend 1.21.0, ui-test-ids 1.21.0)
- [ ] CK-SC-001..006 evidenced (tests or quickstart rows)
- [ ] Threat model rows T1-T7 covered by [sec] tests; T8-T11 documented for the user
- [ ] Public manuals updated and PDFs regenerated (T055); `docs/STATUS.md` updated
- [ ] Hand-off notes written; Conventional Commits
