# Hand-off: backend / 011 follow-up claude-key, foundational (T011-T016)

Branch `ck-lane-backend` (worktree `.claude/worktrees/ck-backend`, from `011-claude-key-spec`), with `ck-lane-agent` merged
(ai-agent T007-T010: `ClaudeApiKey`, `ClaudeKeyProvider`). Nothing pushed. No docker command run, Anthropic never called, no
key-shaped literal written (sentinels from `tests/Shared/ClaudeKey/ClaudeKeySentinels`).

## What was built

- **T011/T012 persistence**: `AgentSecretEntity` (`Name` PK <= 40, `Ciphertext` <= 2048, `LastFour` exactly 4,
  `PurposeVersion`, `StoredAt` UTC), `AgentSecretConfiguration` with SQLite check constraints (SQLite ignores declared lengths),
  `IAgentSecretStore`/`AgentSecretStore` (`Find`, `UpsertAsync` in one explicit transaction -> replaced?, `Reprotect` updates
  only ciphertext and time, `DeleteAsync` idempotent), registered scoped in `AddStemStudioPersistence`; migration
  `20261116165940_AddAgentSecrets` (applies on an 011 database, tested).
- **T013/T014 Data Protection** (`src/StemStudio.Api/Agent/Secrets/`): `DataProtectionSetup.AddClaudeKeyProtection`
  (application name `StemStudio.Api`, `PersistKeysToFileSystem(dir)`, `ProtectKeysWithDpapi()` on Windows, ephemeral provider
  when the directory is unusable), constants `ClaudeKeyPurpose = "StemStudio.Agent.ClaudeApiKey.v1"`, `KeyRingDirKey =
  StemStudio:DataProtection:KeyRingDir`, `BackupDirKey = StemStudio:BackupDir`; `KeyRingDirectory.Prepare` (guard: library,
  backups, content root, any ancestor with `.git` dir or file; create + write probe -> `not-writable`; 0700 tightening via
  the `IUnixFileModes` seam, warning when not owner); `KeyRingStatus` singleton (logs `KeyStoreUnavailable` 4104 /
  `KeyRingPermissionsWide` 4103 once at start, from `UseStemStudioApiAsync`); `KeyRingDefaults` (per-user default:
  `%LOCALAPPDATA%\StemStudio\keys`, `$XDG_DATA_HOME/stemstudio/keys`, `~/.local/share/stemstudio/keys`). The API host starts
  with a misplaced directory (tested).
- **T015/T016 store**: `ClaudeKeyProtector` (base64url payloads, `IPersistedDataProtector.DangerousUnprotect` with
  revocation errors, failures -> type name only), `ClaudeKeyRow` (scope per operation), `ProtectedClaudeKeyStore`
  (`IStoredClaudeKeyReader` + `IStoredClaudeKeyWriter`, one singleton; unreadable for foreign ring, corrupted, tampered,
  revoked, decrypts-to-no-key; re-protects a payload of a non-default key and saves it; `StoreAsync` throws
  `InvalidOperationException` when the store is unavailable - endpoints answer 503 first; events 4100/4101 (once per process,
  exception type only)/4102 without value, last four or ciphertext). Registered by `AddClaudeKeyStore()` in `AddAgentStores`.
- `ApiFactory` (tests) now sets `StemStudio:DataProtection:KeyRingDir` to `<DataDir>-keys` and deletes it, so no test writes
  into the user's profile.

## Tests and coverage

| Command | Result | Line | Branch |
|---|---|---|---|
| `dotnet test tests/StemStudio.Persistence.Tests -c Release -p:CollectCoverage=true` | 114 passed | Persistence **98.91 %** | **95 %** |
| `dotnet test tests/StemStudio.Api.Tests -c Release -p:CollectCoverage=true` | 1533 passed, 2 skipped (Linux-only) | Api **98.89 %**, Contracts 100 % | Api **91.69 %**, Contracts 100 % |

Skipped on this Windows machine: `NonWindows_HasNoAtRestEncryptor`, `Linux_RealModesAreTightened` (OS-conditional, run in
the Linux CI). Uncovered on Windows: the three lines of the real Unix-mode implementation. Domain was not touched.

## Quality

Build 0 warnings; `dotnet format --verify-no-changes` clean; `run_all.py` 0 errors (soft unit-size warnings at 16-18 lines
in `KeyRingStatus.LogStartupWarnings`, `ProtectedClaudeKeyStore.Read`, `AgentSecretStore.UpsertAsync`);
`scripts/secret_scan.py` clean.

## Dependencies added

None (Data Protection is in the shared framework `Microsoft.AspNetCore.App`).

## Notes for other owners

- **devops**: the API reads `StemStudio__DataProtection__KeyRingDir` (compose: `/keys`); unset = per-user default above.
  `StemStudio__BackupDir` is honoured by the guard when 013 sets it.
- **quality-gate (T017a)**: `Microsoft.AspNetCore.DataProtection` is used only in `src/StemStudio.Api/Agent/Secrets/`.
- **ai-agent**: `IStoredClaudeKeyReader` is registered as a singleton in the API; `Read()` is synchronous and hits SQLite
  once per call (the provider caches). No `IClaudeKeyProvider` is registered by the backend yet (T024 adds a `TryAdd`).
