# Hand-off: architect / 011 follow-up claude-key, Phase 0 (T002, T004, T005, T006)

Branch `011-claude-key-spec` (worktree `.claude/worktrees/011-claude-key`), on top of 21d42e6d (constitution 1.4.0 ratified,
T001). Nothing was pushed or merged. **T003 (Docker spike) was not run**: the owner's stack was being rebuilt by another agent,
so no docker command was used in Phase 0; T003 is marked "deferred - run after the stack update" in tasks.md and is the first
wave item for devops (see the lane start list). Anthropic was never called; no key-shaped literal was written.

## What was built

- **T002 contracts**
  - Full **backend-api 1.21.0**: `specs/011-ai-composer-agent/followups/claude-key/contracts/backend-api.openapi.yaml`
    = 011's 1.10.0 + the delta, merged by the DELTA.md section 1 rule (path `/api/agent/settings/claude-key` with
    `getClaudeKeyStatus`/`storeClaudeKey`/`deleteClaudeKey`; 5 responses and 7 schemas added; `ClaudeStatus`,
    `TestAgentBackendRequest`, `BackendTestResult`, `getAgentStatus`, `testAgentBackend` replaced by the delta versions;
    changelog line and tag text). The base was 1.10.0 because 012-021 are not built (ROADMAP allocation note: the CTO merges
    their deltas onto 1.21.0 in feature order).
  - **Contract fix found at the merge**: the delta's 413 response was named `PayloadTooLarge`, which 1.0.0 already defines
    for the 200 MiB upload (Redocly: duplicated mapping key). The delta is now **1.21.0-delta.2** with the response renamed
    `ClaudeKeyTooLarge`; nothing else changed. DELTA.md (status, merge rule 2, merge note) and redocly.yaml updated.
  - Full **ui-test-ids 1.21.0**: `specs/011-ai-composer-agent/followups/claude-key/contracts/ui-test-ids.md` = 1.10.0
    unchanged (title aside) + changelog line + a "Claude key in Settings" section with the 14 delta rows, help topic and qa rules.
  - Merge-rule tests in `tests/StemStudio.Api.Tests/Contract/ClaudeKeyContractTests.cs` (1.21.0 = 1.10.0 operations + exactly
    the three key operations; 1.10.0 unchanged except the five replaced entries; delta unchanged except the eight shared
    base components, which keep their 1.10.0 definitions; no other key added) and `ClaudeKeyUiTestIdsTests.cs`
    (1.10.0 body is the unchanged start of 1.21.0; all 14 delta rows present; new ids unique and not in 1.10.0).
  - Api.Tests now link 1.21.0 as `Contract/backend-api.openapi.yaml` and 1.10.0 as `Contract/011/`;
    `AgentContractTests` prove the 1.10.0 merge against `Contract/011` and its write-exactly check uses the 1.10.0 schemas
    (the 1.21.0 optional members are omitted when null). `PendingAgentOperations` = `getClaudeKeyStatus`,
    `storeClaudeKey`, `deleteClaudeKey` (backend removes them in T024/T033).
- **T004 .NET contracts** (`src/StemStudio.Contracts/Agent/`): `ClaudeKeyStatusDto` (+ `NotConfigured`),
  `ClaudeKeyStateDto`, `ClaudeKeySourceDto`, `StoredClaudeKeyStateDto`, `ClaudeKeyTestSourceDto`, `StoreClaudeKeyRequest`
  (sealed class, redacted `ToString`), `ClaudeKeyInvalidReasons`, `ClaudeStatusDto.Key`, `TestAgentBackendRequest.ClaudeKeySource`
  and `BackendTestResultDto.KeySource` (both omitted on the wire when null), `AgentProblemTypes` +4 (`claude-key-invalid`,
  `invalid-body`, `local-only`, `key-store-unavailable`), `AgentContractLimits.ClaudeKeyBodyBytes = 1024` and
  `ClaudeKeyRequestMaxLength = 512`, `ApiRoutes.AgentClaudeKey`. Tests: enums vs schema, reasons vs schema, problem types vs
  the response examples, limits, route, both `ClaudeKeyStatus` examples round-trip, DTOs validate and write exactly their
  schema properties, optional members omitted, `StoreClaudeKeyRequest.ToString()` never contains the sentinel in any of its
  search forms [sec].
- **T005 ports and model stubs** (`src/StemStudio.Agent/`): `Ports/IClaudeKeyProvider` (`Resolve()`, `KeyFor(source)`,
  `Invalidate()`), `IStoredClaudeKeyReader` (`Read()` -> `StoredClaudeKey`), `IStoredClaudeKeyWriter` (`StoreAsync` -> replaced?,
  `DeleteAsync`), records `StoredClaudeKey`, `SelectedClaudeKey`; `Model/ClaudeApiKey` (sealed class; `Parse`, `Reveal`,
  `LastFour`, `Fingerprint` throw `NotImplementedException` until ai-agent T008; `ToString()` = `[redacted]`),
  `ClaudeKeyFormatResult` (Valid/Invalid, implemented), `ClaudeKeyResolution` (init record, `Configured`),
  `KeyStoreUnavailableReasons`. The domain reuses the Contracts enums (011 rule; data-model 3/4 updated to the real signatures).
  Shared test doubles in **`tests/Shared/ClaudeKey/`** (namespace `StemStudio.TestDoubles.ClaudeKey`, compiled into
  Agent.Tests and Api.Tests via `<Compile Include="..\Shared\ClaudeKey\*.cs" LinkBase="Shared\ClaudeKey" />`):
  `FakeClaudeKeyProvider`, `FakeStoredClaudeKeyReader`, `ClaudeKeySentinels` (well-formed sentinels built at run time:
  `Environment` ...ENV1, `Stored` ...STO1, `Replacement` ...STO2, `Admin`, `SearchForms` = plain/base64/UTF-16 base64).
  Skeleton tests: `tests/StemStudio.Agent.Tests/Model/ClaudeKeySkeletonTests.cs`, `Fakes/ClaudeKeyTestDoubleTests.cs`.

## Files changed

- New: `specs/011-ai-composer-agent/followups/claude-key/contracts/{backend-api.openapi.yaml,ui-test-ids.md}`;
  `src/StemStudio.Contracts/Agent/{ClaudeKeyStatusDto,ClaudeKeyStateDto,ClaudeKeySourceDto,StoredClaudeKeyStateDto,ClaudeKeyTestSourceDto,StoreClaudeKeyRequest,ClaudeKeyInvalidReasons}.cs`;
  `src/StemStudio.Agent/Model/{ClaudeApiKey,ClaudeKeyFormatResult,ClaudeKeyResolution,KeyStoreUnavailableReasons}.cs`;
  `src/StemStudio.Agent/Ports/{IClaudeKeyProvider,IStoredClaudeKeyReader,IStoredClaudeKeyWriter,StoredClaudeKey,SelectedClaudeKey}.cs`;
  `tests/Shared/ClaudeKey/{ClaudeKeySentinels,FakeClaudeKeyProvider,FakeStoredClaudeKeyReader}.cs`;
  `tests/StemStudio.Api.Tests/Contract/{ClaudeKeyContractTests,ClaudeKeyUiTestIdsTests}.cs`;
  `tests/StemStudio.Agent.Tests/Model/ClaudeKeySkeletonTests.cs`, `tests/StemStudio.Agent.Tests/Fakes/ClaudeKeyTestDoubleTests.cs`; this note.
- Modified: `.../claude-key/contracts/{backend-api.claude-key.openapi.yaml (delta.2),DELTA.md,redocly.yaml}`,
  `.../claude-key/{data-model.md,tasks.md}`; `src/StemStudio.Contracts/Agent/{ClaudeStatusDto,TestAgentBackendRequest,BackendTestResultDto,AgentProblemTypes,AgentContractLimits,ApiRoutes.Agent}.cs`,
  `src/StemStudio.Contracts/StemStudio.Contracts.csproj` (comment); `tests/StemStudio.Api.Tests/StemStudio.Api.Tests.csproj`,
  `tests/StemStudio.Agent.Tests/StemStudio.Agent.Tests.csproj`; `tests/StemStudio.Api.Tests/Contract/{AgentContractTests,ContractJson,BackendContractTests.StemsAndMixes}.cs`.

## Tests and coverage

Only the affected projects were run (another heavy test run was in progress on this PC).

| Command | Result | Line | Branch |
|---|---|---|---|
| `dotnet test tests/StemStudio.Agent.Tests -c Release -p:CollectCoverage=true` | 620 passed, 0 failed | StemStudio.Agent **98.74 %** | **93.23 %** |
| `dotnet test tests/StemStudio.Api.Tests -c Release -p:CollectCoverage=true` | 1483 passed, 0 failed | Api 98.91 %, **Contracts 100 %** | Api 91.61 %, **Contracts 100 %** |
| `dotnet test tests/StemStudio.Web.Tests -c Release --filter FullyQualifiedName~Agent` (Web constructs `ClaudeStatusDto`) | 234 passed | - | - |
| `dotnet test tests/StemStudio.AgentEval.Tests -c Release` (references StemStudio.Agent) | 134 passed | - | - |

Of these, 138 are the claude-key and 011 agent contract tests (`ClaudeKey*`, `AgentContractTests`) and 20 the new Agent
skeleton tests. The 90 % line+branch gates of Agent.Tests and Api.Tests passed. The only uncovered new lines are the empty
private constructor of the `ClaudeApiKey` stub (replaced in T008). Worker, audio engine and web-interop were not touched.

## Quality checks

- `dotnet build StemStudio.slnx -c Release` -> **0 warnings, 0 errors** (two analyzer findings fixed while building:
  CA1716 on `IClaudeKeyProvider.Select` -> renamed `KeyFor`; CA1716 on namespace `StemStudio.Tests.Shared` -> `StemStudio.TestDoubles`).
- `dotnet format StemStudio.slnx --verify-no-changes --no-restore` -> exit 0.
- `uv run --project tools/quality python tools/quality/run_all.py` -> **0 errors**, 742 warnings, exit 0; no finding in any
  file of this change.
- `python scripts/secret_scan.py` -> 3487 files clean, exit 0; `--self-test` passed.
- `npx @redocly/cli@2.78.0 lint --config specs/011-ai-composer-agent/followups/claude-key/contracts/redocly.yaml <full> <delta>`
  -> both **valid** (OpenAPI 3.1); warnings only `operation-4xx-response`: 9 in the full document (the 8 of 1.10.0 +
  `getClaudeKeyStatus`), 2 in the delta (`getAgentStatus`, `getClaudeKeyStatus`), as accepted in the design.

## Dependencies added

None.

## Docs impact

None from Phase 0 (contracts and stubs only; no user-visible or setup change yet). The manual changes are T055 (CTO).

## Open issues / requests

1. **devops (first wave item): T003 spike** with the architect after the stack update (research R-04, R-05: peer address
   for host browser / LAN device / direct LAN call to 8081; `/keys` volume inherits 0700 and `$APP_UID`; key ring survives
   `docker compose up --build`; results into research.md F-01..). Not run in Phase 0, no docker command used.
2. **devops**: add the follow-up contracts to the contract lint of `.github/workflows/ci.yml` and `scripts/test-all.{sh,ps1}`
   (devops-owned): `npx @redocly/cli@2.78.0 lint --config specs/011-ai-composer-agent/followups/claude-key/contracts/redocly.yaml
   specs/011-ai-composer-agent/followups/claude-key/contracts/backend-api.openapi.yaml
   specs/011-ai-composer-agent/followups/claude-key/contracts/backend-api.claude-key.openapi.yaml`.
3. **ai-agent**: do T007 -> T008 first: backend T015 (`ProtectedClaudeKeyStore` round trip) and T023 (422 reasons) need a
   working `ClaudeApiKey.Parse`. Replace the stub tests in `ClaudeKeySkeletonTests` (`ClaudeApiKey_StubThrows...`) with
   `ClaudeApiKeyTests`; keep `ClaudeApiKey` a sealed class (quality-gate rule T017b). `ClaudeStatusDto.Key` defaults to
   `ClaudeKeyStatusDto.NotConfigured` so 1.10.0 call sites compile; `ClaudeBackendProbe.Status` must fill it (T021/T022).
   `FakeEnvironment.SentinelKey` (33 characters) is not a well-formed key; use `ClaudeKeySentinels` for the new tests.
4. **backend**: `AgentStatusProbe` (API) builds `ClaudeStatusDto` without `Key` (default) - fill it from `IClaudeKeyProvider`
   in T024/T043; the 413 response is `ClaudeKeyTooLarge`; remove the three ids from `PendingAgentOperations` as they are served.
5. **architect, T053**: `updateAgentSettings`'s description in 1.21.0 still reads "`backend: claude` needs `ANTHROPIC_API_KEY`
   (409 `claude-not-configured`)": the merge rule keeps every 1.10.0 entry unchanged. Its meaning is now "a usable key";
   fold the wording into the integration wave (or a 1.21.1 text-only patch) together with the 011 spec FR-018/FR-019 fold.
6. **quality-gate**: `tests/Shared/` is a new folder of test-only code compiled into two test projects (no project of its
   own); add it to the dependency map if the map lists test folders. T017 rules can target the new types now.
7. **CTO**: nothing to add to THIRD-PARTY-LICENSES (no new dependency).

## Lane start list (the wave may start)

| Lane | Starts with | Notes |
|---|---|---|
| devops | **T003** (with the architect, after the stack update), then T019 -> T020 | request 2 above |
| ai-agent | T007 -> T008 now (unblocks backend T015/T023), T009 -> T010 | `FakeStoredClaudeKeyReader`, `ClaudeKeySentinels` |
| backend | T011 -> T012, T013 -> T014 now; T015 -> T016 once T008 is available | `FakeClaudeKeyProvider` for endpoint tests |
| frontend | T025 -> T026 now | DTOs + `ApiRoutes.AgentClaudeKey`; ui-test-ids 1.21.0 in the follow-up folder |
| qa | T047 red against the fake stack now (T002 merged) | sentinels built in TypeScript at run time, never literal |
| quality-gate | T017 -> T018 now | |

## Next owner

CTO: review the delta.2 rename and this hand-off, then start the wave (devops T003 first after the stack update).
