# Contract delta: 011 follow-up "claude-key"

The follow-up adds one resource to the backend API (`/api/agent/settings/claude-key`), extends two
011 schemas additively, adds UI test ids and changes no other contract.

## Status (merged in Phase 0; follow-up integrated 2026-11-16, folder merged)

| Contract | Base | Follow-up document | State |
|---|---|---|---|
| Backend API | 1.10.0 (011; 012-021 not built yet) | [backend-api.claude-key.openapi.yaml](backend-api.claude-key.openapi.yaml) **1.21.0-delta.2** -> full [backend-api.openapi.yaml](backend-api.openapi.yaml) **1.21.0** | **merged** (T002); Redocly 2.78.0 valid (OpenAPI 3.1): delta 2 expected `operation-4xx-response` warnings (`getAgentStatus`, `getClaudeKeyStatus`), full document 9 (the 8 of 1.10.0 + `getClaudeKeyStatus`); `ClaudeKeyContractTests` prove 1.21.0 = 1.10.0 + this delta |
| UI test ids | 1.10.0 (011) | [ui-test-ids.delta.md](ui-test-ids.delta.md) -> full [ui-test-ids.md](ui-test-ids.md) **1.21.0** | **merged** (T002); `ClaudeKeyUiTestIdsTests` prove 1.21.0 = 1.10.0 + the delta rows |
| Tool catalogue | 1.0.0 (011) | unchanged: the agent gets **no** tool for the key (a key must never pass through a model) | - |
| Worker, generator, audio engine, web-interop, backup-archive, remote-protocol | - | unchanged | - |
| C# | - | `src/StemStudio.Contracts/Agent`: `ClaudeKeyStatusDto`, `ClaudeKeyStateDto`, `ClaudeKeySourceDto`, `StoredClaudeKeyStateDto`, `ClaudeKeyTestSourceDto`, `StoreClaudeKeyRequest` (class, redacted `ToString`), `ClaudeStatusDto.Key`, `TestAgentBackendRequest.ClaudeKeySource`, `BackendTestResultDto.KeySource`, `AgentProblemTypes` (+4), `ClaudeKeyInvalidReasons`, `AgentContractLimits.ClaudeKeyBodyBytes`/`ClaudeKeyRequestMaxLength`, `ApiRoutes.AgentClaudeKey` | **done** (T004) |

## 1. Merge rule (architect, T002)

1. Add the path `/api/agent/settings/claude-key` (operations `getClaudeKeyStatus`, `storeClaudeKey`,
   `deleteClaudeKey`) unchanged.
2. Add the responses `InvalidBody`, `LocalOnly`, `ClaudeKeyTooLarge`, `ClaudeKeyInvalid`,
   `KeyStoreUnavailable` and the schemas `ClaudeKeyInvalidProblem`, `ClaudeKeySource`,
   `ClaudeKeyState`, `StoredClaudeKeyState`, `ClaudeKeyStatus`, `StoreClaudeKeyRequest`,
   `ClaudeKeyTestSource` unchanged.
3. Replace `ClaudeStatus`, `TestAgentBackendRequest`, `BackendTestResult` with this document's
   versions (each is a superset; `ClaudeStatus.key` is a new **required** response member,
   `claudeKeySource` and `keySource` are optional).
4. Add `'403': $ref LocalOnly` to `testAgentBackend`; replace the description of
   `testAgentBackend` and add the `getAgentStatus` description sentence about `key`.
5. Keep `ProblemDetails`, `ValidationProblemDetails`, `AgentBackend`, `ClaudeModel`,
   `OllamaModelInfo`, `OllamaStatus`, `AgentStatus` of the base (copied verbatim here).
6. Bump `info.version` to 1.21.0 with the changelog line of this delta; update
   `AgentContractTests` so they prove the full document contains the base and this delta
   (`PendingAgentOperations` gets the three new operation ids until the endpoints exist).

**Merge note (T002, delta.2)**: delta.1 named its 413 response `PayloadTooLarge`, which 1.0.0
already defines for the 200 MiB upload; delta.2 renames it `ClaudeKeyTooLarge` (no other change).
The base was 1.10.0 because 012-021 are not built; the CTO merges their deltas onto 1.21.0 in
feature order (ROADMAP allocation note).

**Compatibility**: additive. Old clients ignore `claude.key` and `keySource`; old requests without
`claudeKeySource` keep their 011 behaviour (the active key, now possibly a stored one).
`keyConfigured` keeps its name; its meaning widens from "environment variable set" to "a usable key
is available". The Web app is the only client and is updated in the same change.

## 2. Endpoints

| Method + path | operationId | Request | Responses | Local-only |
|---|---|---|---|---|
| `GET /api/agent/settings/claude-key` | `getClaudeKeyStatus` | - | `200 ClaudeKeyStatus` | no |
| `PUT /api/agent/settings/claude-key` | `storeClaudeKey` | `StoreClaudeKeyRequest {apiKey}` (<= 1 KiB) | `200 ClaudeKeyStatus`; `400 invalid-body`; `403 local-only`; `413`; `422 claude-key-invalid` (`reason`); `503 key-store-unavailable` | yes |
| `DELETE /api/agent/settings/claude-key` | `deleteClaudeKey` | - | `200 ClaudeKeyStatus` (idempotent); `403 local-only` | yes |
| `POST /api/agent/settings/test` | `testAgentBackend` (changed) | `+ claudeKeySource?: active \| stored \| environment` | `200 BackendTestResult (+ keySource)`; `400`; `403 local-only` (Claude only) | Claude case |
| `GET /api/agent/status` | `getAgentStatus` (changed) | - | `200 AgentStatus` with `claude.key` | no |

## 3. Schemas (summary; the OpenAPI document wins)

```yaml
ClaudeKeyStatus:
  configured: boolean                       # usable key available (= claude.keyConfigured)
  source: environment | stored | null       # active key
  state: not-configured | environment | stored | stored-unreadable
  lastFour: string(4) | null                # of the active key
  stored: absent | readable | unreadable    # the stored key, independent of the environment
  storedLastFour: string(4) | null
  storedAt: date-time | null
  storeAvailable: boolean                   # key ring directory usable
  reason: string | null                     # readable, e.g. stored-unreadable explanation
  fix: string | null                        # e.g. "Enter the key again."
StoreClaudeKeyRequest: {apiKey: string (writeOnly, 1..512; server: trimmed 40..256, prefix, charset, not admin)}
ClaudeStatus: + key: ClaudeKeyStatus        # required
TestAgentBackendRequest: + claudeKeySource?: active | stored | environment
BackendTestResult: + keySource?: environment | stored
```

## 4. Local-only filter (binding, spec CK-FR-012)

Applied to `storeClaudeKey`, `deleteClaudeKey` and `testAgentBackend` with `backend: claude`:
accept when (peer is loopback **or** peer is an address of `StemStudio:Agent:KeyEntry:InternalCaller`)
**and** `Host` is `localhost` / `127.0.0.1` / `[::1]` / `api` (port ignored) **and** `Origin` is
absent or equals `StemStudio:WebOrigin`; else `403 local-only`. Order: the filter runs before body
reading, so a refused request's body is never read.

## 5. Problem types

`claude-key-invalid` (422), `invalid-body` (400), `local-only` (403), `key-store-unavailable`
(503); see [../data-model.md](../data-model.md) section 5. None of them ever quotes the request.
