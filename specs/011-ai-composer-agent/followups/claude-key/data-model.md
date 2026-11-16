# Data model delta: Claude key in Settings (011 follow-up)

Base: [../../data-model.md](../../data-model.md) (011). Only the changes are listed. Persistence is
the API's SQLite database (EF Core), owned by the backend agent; the rules live in
`StemStudio.Agent` (ports and plain records, no EF and no Data Protection dependency).

## 1. Configuration (environment)

Replaces the `ANTHROPIC_API_KEY` row of 011 data-model 1 and adds rows.

| Variable | Default | Meaning |
|---|---|---|
| `ANTHROPIC_API_KEY` | unset | Claude key from the environment. **Wins over a stored key.** Presence, source and last four characters are reported; never logged, never persisted, never returned. |
| `StemStudio__DataProtection__KeyRingDir` | Docker: `/keys` (compose); Windows: `%LOCALAPPDATA%\StemStudio\keys`; Linux/macOS: `$XDG_DATA_HOME/stemstudio/keys`, else `~/.local/share/stemstudio/keys` | Data Protection key ring of the API. Must be outside `StemStudio:DataDir`, `StemStudio:BackupDir`, the content root and any git work tree (else key storage is disabled with `key-store-unavailable`). |
| `StemStudio__Agent__KeyEntry__InternalCaller` | compose: `web`; non-Docker: empty | Host name of the trusted internal caller (the Web app) for the local-only filter; its resolved addresses are accepted besides loopback. |

Fixed values (code constants, not configurable): application name `StemStudio.Api`; protector
purpose `StemStudio.Agent.ClaudeApiKey.v1`; secret name `claude-api-key`; request body limit
1,024 bytes; key length 40-256 after trimming.

## 2. New entity `AgentSecret` (table `AgentSecrets`)

One row per secret name; today only `claude-api-key`. A separate table (not columns on
`AgentSettings`) so that 013 can treat secrets as one unit (snapshot, merge skip) and so the
settings DTO mapping can never pick the ciphertext up by accident.

| Field | Type | Rules |
|---|---|---|
| `Name` | string <= 40, primary key | `claude-api-key` |
| `Ciphertext` | string <= 2,048 | Data Protection payload (base64url); never decoded outside `ProtectedClaudeKeyStore` |
| `LastFour` | string, exactly 4 | last four characters of the trimmed key (key characters only) |
| `PurposeVersion` | int | `1` (= purpose `...ClaudeApiKey.v1`); a future purpose change migrates rows |
| `StoredAt` | timestamp (UTC) | time of the last save or re-protection |

Migration: `AddAgentSecrets` (backend). No data migration (the key was never stored before).

Write rules: `PUT` upserts the row in one transaction (replace overwrites `Ciphertext`, `LastFour`,
`StoredAt`); `DELETE` removes it. Re-protection after a key-ring rotation updates `Ciphertext` and
`StoredAt` only.

## 3. Domain records (`StemStudio.Agent`)

```text
ClaudeApiKey            sealed class; Reveal() for the SDK only; ToString() = "[redacted]";
                        LastFour; Fingerprint (SHA-256 hex, memory only, client cache key);
                        Parse(string) -> ClaudeKeyFormatResult (Ok | Error(code)), codes:
                        empty, prefix, length, characters, admin-key
ClaudeKeyFormatResult   sealed class: Valid(key) | Invalid(reason); Key?, Error?, IsValid; never the raw value
ClaudeKeySource         = Contracts ClaudeKeySourceDto      (Environment | Stored)
StoredClaudeKeyState    = Contracts StoredClaudeKeyStateDto (Absent | Readable | Unreadable)
ClaudeKeyState          = Contracts ClaudeKeyStateDto       (NotConfigured | Environment | Stored | StoredUnreadable)
ClaudeKeyResolution     record, init properties (Key: ClaudeApiKey?, Source?, State, LastFour?, Stored,
                        StoredLastFour?, StoredAt?, StoreAvailable, StoreUnavailableReason?, Reason?, Fix?);
                        Configured => Key is not null
KeyStoreUnavailableReasons  constants not-writable | inside-library | inside-backups | inside-app | inside-repository
```

The domain reuses the Contracts enums, as the 011 model does (no duplicated enums). Skeleton (Phase 0,
T005): `ClaudeApiKey` members throw `NotImplementedException` until the ai-agent's T008; its
`ToString()` already returns `[redacted]`.

State rule (one function, unit-tested over all combinations):

| Environment key | Stored row | Decrypts | `state` | `source` | `configured` | `lastFour` |
|---|---|---|---|---|---|---|
| yes | any | any | `environment` | `environment` | true | of the env key |
| no | yes | yes | `stored` | `stored` | true | of the stored key |
| no | yes | no | `stored-unreadable` | null | false | null (the row's `LastFour` is shown as "stored key ...WXYZ") |
| no | no | - | `not-configured` | null | false | null |

`stored` is reported independently (`absent` / `readable` / `unreadable`), so the UI can say "saved
key not in use while ANTHROPIC_API_KEY is set" and "saved key unreadable".

## 4. Ports

| Port (`StemStudio.Agent.Ports`) | Implementation (owner) | Purpose |
|---|---|---|
| `IClaudeKeyProvider` `Resolve()` -> `ClaudeKeyResolution`; `KeyFor(ClaudeKeyTestSourceDto)` -> `SelectedClaudeKey?` (explicit test); `Invalidate()` | `ClaudeKeyProvider` (`StemStudio.Agent.Backends`, ai-agent) over `IEnvironmentReader` + `IStoredClaudeKeyReader` | the single source of the key for `ClaudeChatClientFactory`, `ClaudeBackendProbe` and `AgentStatusService` (replaces their direct `IEnvironmentReader` reads) |
| `IStoredClaudeKeyReader` `Read()` -> `StoredClaudeKey` (State, Key?, LastFour?, StoredAt?, StoreAvailable, StoreUnavailableReason?) | `ProtectedClaudeKeyStore` (`StemStudio.Api/Agent/Secrets`, backend) | decrypts with Data Protection; maps `CryptographicException` and bad payloads to `Unreadable`; re-protects on `requiresMigration` |
| `IStoredClaudeKeyWriter` `StoreAsync(ClaudeApiKey, ct)` -> replaced?, `DeleteAsync(ct)` | same class (backend) | write side, used only by the key endpoints |
| `IAgentSecretStore` (Persistence) `Get/Upsert/Delete(name)` | `AgentSecretStore` (`StemStudio.Persistence/Agent`, backend) | EF access to `AgentSecrets` |

Dependency direction (quality-gate rule): only `StemStudio.Api/Agent/Secrets` references
`Microsoft.AspNetCore.DataProtection`; `StemStudio.Agent` and `StemStudio.Persistence` do not.

## 5. Problem types (added)

| `type` suffix | Status | When | `detail` (fixed texts, never the value) |
|---|---|---|---|
| `claude-key-invalid` | 422 | format check failed; extension `reason` = `empty` \| `prefix` \| `length` \| `characters` \| `admin-key` | e.g. "The key must start with sk-ant- and be 40-256 characters (letters, digits, - and _)." |
| `invalid-body` | 400 | body is not JSON or lacks `apiKey` | "Send a JSON object with the field apiKey." |
| `local-only` | 403 | local-only filter refused | "The Claude key can only be entered on the computer StemStudio runs on." |
| `key-store-unavailable` | 503 | key ring directory missing, not writable or misplaced | "The key store is not available: <reason>. The ANTHROPIC_API_KEY environment variable still works." |
| (413) | 413 | body > 1 KiB | Kestrel/endpoint limit, standard problem |

`<reason>` is one of fixed texts (`not-writable`, `inside-library`, `inside-backups`,
`inside-app`, `inside-repository`) rendered in words; never a stack trace.

## 6. Events and logs

| Event (EventId) | Level | Fields | Never contains |
|---|---|---|---|
| `ClaudeKeyStored` (4100) | Information | `source=stored`, `lastFourPresent=true`, `replaced` bool | key, last four, ciphertext |
| `ClaudeKeyDeleted` (4102) | Information | - | same |
| `ClaudeKeyUnreadable` (4101) | Warning, once per process | exception type name | message, ciphertext, key id |
| `KeyRingPermissionsWide` (4103) | Warning, once | directory path, mode | - |
| `KeyStoreUnavailable` (4104) | Warning, once | reason code | - |
| `KeyEntryRefused` (4105) | Information, rate-limited 1/min | reason (`peer`, `host`, `origin`) | peer address is logged, body is not |

No SSE run event is added. The 011 `model_changed` / run events are unchanged; a run that fails
because the stored key is unreadable uses the 011 failure code `backend-unavailable` with the
readable reason of section 3.

## 7. 013 and 018 notes (cross-feature, binding for those features)

- 013 backup: the `api-keys` volume / key ring directory is never read; `AgentSecrets` travels in
  the `database` category snapshot; manifest and README never list secret values. Merge restore:
  `AgentSecrets` rows are never imported (like settings). Replace restore: after the switch the
  restore report includes "The stored Claude key must be entered again" when the key status became
  `stored-unreadable`.
- 018: no remote command for secrets; the remote listener answers 404 for every API path (018
  FR-013) and the API port is bound to `127.0.0.1` (018 FR-011).
