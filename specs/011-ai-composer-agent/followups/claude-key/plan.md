# Implementation Plan: Claude key in Settings, stored encrypted (011 follow-up "claude-key")

**Branch**: `011-claude-key-spec` (design) -> `011-claude-key` (build) | **Date**: 2026-11-16 |
**Spec**: [spec.md](spec.md)
**Input**: owner decision 2026-11-16 and the binding design agreed by the coordinator with the
owner; 011 spec/plan/data-model/contracts; 013 and 018 designs; constitution 1.3.1.

**Note**: produced by the architect agent (`/speckit-plan` equivalent). **Gate**: no
implementation task starts before the owner ratifies the constitution amendment 1.4.0 below
(task T001). **Gate passed**: the owner ratified 1.4.0 on 2026-11-16 and answered Q1 and Q2 (see
[Owner answers](#owner-answers-2026-11-16)). **Shipped** (integration 2026-11-16): the key comes
from `ANTHROPIC_API_KEY` (wins) or from Settings, stored encrypted.

## Summary

The owner can paste the Anthropic key on the Settings page. The API checks its format, encrypts
it with ASP.NET Core Data Protection (fixed purpose `StemStudio.Agent.ClaudeApiKey.v1`, fixed
application name `StemStudio.Api`) and stores only the ciphertext, the last four characters and the
save time in a new `AgentSecrets` table. The Data Protection key ring lives in its own Docker
volume `api-keys` (mounted only into `api`, directory `0700` for the app user), never in the
library volume and never in a 013 backup; on the non-Docker path in a per-user directory (DPAPI
on Windows). The key resource is write-only (`PUT`/`DELETE`, plus a status `GET`), local-only,
never logged, never returned; `ANTHROPIC_API_KEY` keeps working and wins. A key that cannot be
decrypted (restore on another computer, lost key volume) becomes `stored-unreadable` with "Enter
the key again", never a crash. Claude stays opt-in: Ollama remains the default, storing a key
selects nothing, the 011 notice still has to be acknowledged, and only **Test key** or a Claude
run contacts Anthropic.

## Technical Context

**Language/Version**: C# / .NET 10 (API, Agent library, Web), Blazor Interactive Server + Radzen.
**Primary Dependencies**: ASP.NET Core Data Protection (shared framework, no new package);
EF Core SQLite (existing); Anthropic 13.3.0 (existing, unchanged).
**Storage**: SQLite table `AgentSecrets` (ciphertext only); Data Protection key ring as XML files
in `StemStudio:DataProtection:KeyRingDir` (`/keys` volume `api-keys` in Docker).
**Testing**: xUnit v3 + coverlet; `EphemeralDataProtectionProvider` and temp key directories;
stub and "must not be called" Anthropic handlers (011 pattern); bUnit for the Settings section;
Playwright on the fake stack; a byte scan for a run-time sentinel key over responses, logs, SQLite
files, DOM (CK-SC-001).
**Target Platform**: Docker Desktop (Linux containers) on Windows 11 reference machine; non-Docker
Windows/Linux/macOS dev path.
**Performance Goals**: save -> new status <= 1 s p95; status call adds <= 5 ms (one row read, one
decrypt, cached per API process until the row or the environment changes).
**Constraints**: constitution 1.4.0 condition (2) (ratified 2026-11-16); SIG limits; >= 90 % line and branch;
no Anthropic call in CI; no secret-shaped literal in the repository (`scripts/secret_scan.py`).
**Scale/Scope**: one secret row, three endpoints, one Settings section, one volume.

## Constitution Check

| # | Principle | Assessment | Result |
|---|---|---|---|
| I | Test-first, >= 90 % | every task is a red/green pair; sentinel scan and must-not-call handler are executable criteria; no Anthropic call in tests | PASS |
| II | SIG 4 stars | small units: format parser, state function, store, filter, endpoint group; Data Protection confined to `StemStudio.Api/Agent/Secrets` (quality-gate dependency rule) | PASS |
| III | Contracts first | backend API delta 1.21.0-delta.1 (Redocly valid), ui-test-ids delta 1.21.0, DTOs before the wave (T002-T004) | PASS |
| IV | Open source, lawful use | no new dependency (Data Protection is MIT, shared framework). **Condition (2) of the paid-service exception says the key "never appears in files, the database ...": storing an encrypted key in the database and showing its last four characters was not allowed under 1.3.1** -> amendment 1.4.0 below, ratified by the owner 2026-11-16 | **PASS** (1.4.0; Complexity Tracking row 1) |
| V | Simplicity and honesty | one secret, no own crypto, no key-ring certificates (YAGNI, research R-02); the threat model states what is not protected; local-only limit before 018 stated (research R-05) | PASS |
| - | Tech and security constraints | no secrets in files/command lines (the key arrives in a request body over loopback / the compose network); validated input; body limit; no logs of the value | PASS |
| - | Public manuals (1.3.1) | user manual (Settings, Claude key), technical manual (volume `api-keys`, key ring dir, backup note, recovery) - CTO task T055 | PASS (planned) |

## Threat model

Assets: the Anthropic API key (spending on the owner's account, access to their Anthropic
organisation's Messages API). Single local user, no accounts.

| # | Threat / scenario | Protected? | How / why not |
|---|---|---|---|
| T1 | Someone gets a copy of the SQLite database file (copied folder, synced disk, shared support bundle) | **yes** | only ciphertext + last four; the key ring is in another volume / directory |
| T2 | Someone gets a 013 backup archive or an export | **yes** | archives contain the database snapshot (ciphertext) but never the key ring (013 FR-004 + CK-FR-017) |
| T3 | Someone reads the `api-data` volume (e.g. through the worker's read-only `/library` mount, a misconfigured bind mount) | **yes** | the worker mounts only `api-data`; `api-keys` is mounted only into `api` |
| T4 | Logs, problem details, SSE events, screenshots of the UI, browser history, password managers | **yes** | write-only API, fixed problem texts, HTTP logging disabled for the route, `autocomplete="off"`, the page never renders the value |
| T5 | A malicious web page in the owner's browser calls the API (CSRF) | **yes** | `PUT`/`DELETE` with JSON need a CORS preflight; the API allows only `GET` from the web origin; the key cannot be read back anyway |
| T6 | A paired 018 remote device | **yes** (after 018) | no remote command; remote listener 404s API paths; API bound to `127.0.0.1` |
| T7 | Another device on the LAN **before 018** (ports published on all interfaces, owner decision to keep LAN as is) | **partly** | it can never **read** the key (write-only). The API refuses direct calls (peer is not loopback/web). Through the Web page it is stopped only by a host-name guard, which a hostile client can forge: it could overwrite or delete the stored key, and - as today, independent of this feature - select Claude and spend tokens with whatever key is configured. **Accepted residual risk until 018** (owner decision Q1, 2026-11-16: keep LAN access, no `127.0.0.1` binding before 018; T052 not selected). Closed by 018 (`127.0.0.1` binding, FR-011/FR-014) |
| T8 | Someone who can read **both** volumes or the per-user directory and the database (full host access: admin/root on the computer, Docker Desktop's VM disk, a full disk image, malware running as the owner) | **no** | on Linux the key ring is not encrypted at rest (research R-02); anyone with both decrypts the key. On Windows non-Docker, DPAPI binds the ring to the Windows user, which does not stop malware running as that user. This is stated in the help topic and the technical manual |
| T9 | Memory of the running API / Web process (debugger, crash dump) | **no** | the key is in memory while a Claude client exists and briefly in the Blazor circuit during entry |
| T10 | Key in transit browser -> Web -> API | localhost only | Blazor circuit over `http://localhost` and plain HTTP on the internal compose network; never on the LAN after 018 |
| T11 | Lost key volume / restore on another computer | **availability**, not secrecy | `stored-unreadable` with "Enter the key again"; nothing else breaks |

Residual risks accepted in the amendment: T8, T9; T7 until 018. T7 was accepted explicitly by the
owner on 2026-11-16 (Q1 (b)): until 018 is built, a device on the LAN that forges the host name can
overwrite or delete the stored key through the Web page, but never read it. Spike T003 still
records the Docker Desktop peer-address behaviour this rests on (research R-05).

## Design

### Components and ownership

```text
src/StemStudio.Contracts/Agent/                    # architect (T004): DTOs, enums, problem types, routes
src/StemStudio.Agent/Model/ClaudeApiKey.cs         # ai-agent: value type (redacted ToString, Reveal, LastFour, Fingerprint, Parse)
src/StemStudio.Agent/Ports/IClaudeKeyProvider.cs, IStoredClaudeKeyReader.cs   # architect stub (T005), ai-agent impl
src/StemStudio.Agent/Backends/ClaudeKeyProvider.cs # ai-agent: precedence + state function (data-model 3)
src/StemStudio.Agent/Backends/{ClaudeChatClientFactory,ClaudeBackendProbe,AgentStatusService}.cs  # ai-agent: read via IClaudeKeyProvider
src/StemStudio.Api/Agent/Secrets/                  # backend
├── DataProtectionSetup.cs        # AddDataProtection().SetApplicationName("StemStudio.Api").PersistKeysToFileSystem(dir)
│                                 #   (+ ProtectKeysWithDpapi() on Windows); KeyRingDirectory guard + 0700 check
├── ProtectedClaudeKeyStore.cs    # IStoredClaudeKeyReader/Writer: Protect/Unprotect, re-protect on migration, unreadable mapping
├── ClaudeKeyEndpoints.cs         # GET/PUT/DELETE, 1 KiB limit, own body read, WithHttpLogging(None)
├── LocalOnlyKeyEntryFilter.cs    # peer / host / origin rule (DELTA.md 4)
└── KeyEndpointLoggingInterceptor.cs   # IHttpLoggingInterceptor: nothing for the key path
src/StemStudio.Persistence/Agent/AgentSecretEntity.cs, AgentSecretStore.cs + migration AddAgentSecrets   # backend
src/StemStudio.Web/Components/Settings/ClaudeKeySection.razor (+ .cs)   # frontend
src/StemStudio.Web/Security/PageOrigin.cs           # frontend: loopback host captured at the first request of the circuit
src/StemStudio.Web/Api/StemStudioApiClient.cs       # frontend: GetClaudeKeyStatus/StoreClaudeKey/DeleteClaudeKey
docker/api.Dockerfile, docker-compose.yml, scripts/dev.*  # devops: /keys 0700, volume api-keys, env, non-Docker dir
tests/...                                           # each owner; e2e/tests/agent-claude-key.spec.ts (qa)
tools/quality/                                      # quality-gate: DataProtection confinement rule, sentinel-scan helper review
```

`StemStudio.Agent` gets no Data Protection or EF reference; it sees only `IStoredClaudeKeyReader`.
The ai-agent owner is added to the owner list because `ClaudeChatClientFactory` and
`AgentStatusService` live in its library (AGENT-PROTOCOL rule 1).

### Flow: save

```text
Settings (Blazor, localhost circuit) --PUT /api/agent/settings/claude-key {apiKey}--> API
  LocalOnlyKeyEntryFilter (peer/host/origin) -> 403 | continue
  read body <= 1 KiB -> 400/413 | ClaudeApiKey.Parse -> 422 reason | ok
  key ring usable? -> 503 key-store-unavailable | Protect -> upsert AgentSecrets (tx)
  invalidate the provider cache -> 200 ClaudeKeyStatus
Settings: clear input, render status (never the value)
```

### Flow: use

`ClaudeChatClientFactory.Create` asks `IClaudeKeyProvider.Resolve()`: environment value if set,
else the decrypted stored key, else `BackendUnavailableException` with the state's reason. The
client cache is keyed by the key's SHA-256 fingerprint. A run creates its client at start
(011 behaviour), so a key change applies to the next command (CK-FR-016).

### Configuration of Data Protection (normative)

```csharp
services.AddDataProtection()
    .SetApplicationName("StemStudio.Api")
    .PersistKeysToFileSystem(keyRingDirectory)   // validated: outside DataDir, BackupDir, content root, git trees
    .ProtectKeysWithDpapiIfWindows();             // ProtectKeysWithDpapi() on OperatingSystem.IsWindows()
// default key lifetime (90 days) and algorithms (AES-256-CBC + HMACSHA256) unchanged; keys are never deleted
```

When the directory is unusable the API still starts: it registers an ephemeral Data Protection
provider (nothing is persisted with it), refuses `PUT` with `503 key-store-unavailable`, reports
`storeAvailable: false`, and an existing ciphertext reads as `stored-unreadable`. The API's other
features do not use Data Protection today (no cookies, no antiforgery), so nothing else is
affected.

### Local-only (CK-FR-012) and 018

Before 018: API filter (hard against direct LAN calls) + Web host-name guard (soft, research R-05).
With 018: the main listener accepts only loopback host names and both ports are bound to
`127.0.0.1` (018 FR-011, FR-014); the remote listener never forwards API paths (018 FR-013); the
remote protocol gets no secret command. There is no "owner session" in 018's design (pairing gives
devices fewer rights than the computer's tab), so "the authenticated owner session" of the brief
maps to "the browser on the computer itself", which 018 makes a real boundary.

### Cross-feature changes made in this design

- 013 `spec.md`: cross-feature note (key ring excluded, ciphertext in snapshot, merge skips
  `AgentSecrets`, restore report line); 013 `tasks.md`: rules appended to T035 (backup), T048 (restore
  report) and T062 (merge); 013 `research.md` R-01 gets the `api-keys` row (013 is not built).
- 018 `spec.md`: cross-feature note (no remote command for secrets; the key endpoints are covered by
  FR-011/013/014).
- `docs/ROADMAP.md`: contract allocation row "011 follow-up claude-key: backend-api 1.21.0,
  ui-test-ids 1.21.0".
- `docs/decisions/CTO-DECISIONS-2026-11-15.md`: one row, ratified by the owner 2026-11-16.
- `docs/decisions/ADR-035-claude-key-encrypted-storage.md`: Accepted (2026-11-16).

## Constitution amendment 1.4.0 (ratified by the owner 2026-11-16)

**Version**: 1.3.1 -> **1.4.0** (MINOR). Rationale: principle IV and its paid-service exception
stay; one condition of the exception is materially widened, with new safeguards. Governance names
MINOR for "principle or section added" and MAJOR for "principle removed or redefined"; a widened
condition inside an existing exception is closer to an added rule than to a redefinition of
principle IV. If the owner reads it as a redefinition, the same text ships as 2.0.0.

**Replace** condition (2) of the "Optional paid AI service exception" in principle IV:

> (2) the key comes only from the `ANTHROPIC_API_KEY` environment variable and never appears in
> files, the database, logs, command lines, responses or the UI;

**with**:

> (2) the key comes from the `ANTHROPIC_API_KEY` environment variable or, when the owner enters it
> in Settings on the computer StemStudio runs on, from the database, encrypted with ASP.NET Core
> Data Protection; the encryption key ring is stored separately from the database (its own Docker
> volume or a per-user directory), restricted to the app, and is never part of the library, a
> backup, an export or the repository; the environment variable wins when both exist; the key is
> write-only: it never appears in plain text in files, the database, logs, command lines, events,
> responses, backups or the UI, which may show only its source, its last four characters and when
> it was saved; it cannot be entered, changed or tested from another device; an unreadable stored
> key disables Claude with a readable fix and never stops StemStudio. Encryption protects against
> a leaked database, backup or export, not against someone with full access to the computer;
> this limit is documented for the user;

**Sync impact report** (for `/speckit-constitution`, task T001):

- Version 1.3.1 -> 1.4.0; Last Amended 2026-11-16 (applied to `.specify/memory/constitution.md`, T001).
- Templates: none change.
- Follow-ups after ratification: 011 `spec.md` FR-018 key sentence, FR-019 ("key configured"),
  SC-006 ("never appears in any ... database row" -> "never in plain text"); 011 `data-model.md`
  section 1; `.claude/agents/ai-agent.md` rule "Never read, log or echo `ANTHROPIC_API_KEY` beyond
  passing it to the SDK client" -> "... the key (from the environment or the stored-key port)";
  `docker-compose.yml` comment block; `SECURITY.md`; user and technical manuals (tasks T053-T055).

**CTO-DECISIONS row** (added to `docs/decisions/CTO-DECISIONS-2026-11-15.md` with this design as
proposed; text as ratified on 2026-11-16):

> | 011 | Claude API key entered in Settings and stored encrypted in SQLite (owner decision 2026-11-16) | **Ratified by the owner 2026-11-16**: constitution 1.4.0 (condition (2) of the paid-service exception widened); ASP.NET Core Data Protection, key ring in its own volume `api-keys` excluded from 013 backups; environment variable wins; write-only, local-only API; design `specs/011-ai-composer-agent/followups/claude-key/`, ADR-035 |

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
|---|---|---|
| Constitution IV condition (2) (key in the database, last four shown) | owner decision 2026-11-16: environment variables in the shell that starts Docker are awkward and lost per shell | keeping env-only is the owner's current pain; storing in plain text or in a file would weaken the condition instead of replacing it with safeguards |
| A second Docker volume (`api-keys`) | the key ring must not sit next to the ciphertext or in backups | storing the ring in `api-data` would put both in every backup and in the worker's read-only mount |
| Two-layer local-only check with a soft layer before 018 | the Docker NAT hides the browser's address (research R-05) | binding ports to `127.0.0.1` now is 018's job and the owner kept LAN access until 018 (Q1 (b), 2026-11-16) |

## Owner questions (real decisions)

| # | Question | Options | Recommendation |
|---|---|---|---|
| Q0 | Ratify constitution 1.4.0 (text above)? | yes / no / as 2.0.0 | yes, 1.4.0 |
| Q1 | Until 018 is built, any device on the LAN can open StemStudio and, with a forged host name, overwrite or delete (never read) the stored key. Bind the web and API ports to `127.0.0.1` now (018 FR-011 pulled forward; LAN use of the app stops until 018)? | (a) bind to `127.0.0.1` with this follow-up; (b) keep LAN access, accept T7 until 018 | (a): one compose change, no app logic; LAN use returns with 018's remote |
| Q2 | 013 backups: keep the ciphertext in the database snapshot (same-computer restore keeps the key, other computer -> "enter again"), or strip it (every restore asks for the key)? | keep / strip | keep: the archive is useless without the key ring, which is never archived |

### Owner answers (2026-11-16)

| # | Answer | Effect |
|---|---|---|
| Q0 | **Ratified as 1.4.0** (MINOR) | text above applied to `.specify/memory/constitution.md`; ADR-035 Accepted; CTO-DECISIONS row ratified; T001 done |
| Q1 | **(b) keep LAN access**; no `127.0.0.1` binding before 018 | threat T7 is an accepted residual risk until 018 (a LAN device forging the host name can overwrite or delete the stored key, never read it); T052 not selected; spike T003 stays (Docker Desktop peer-address behaviour); 018 closes the gap (cross-feature note in 018 `spec.md`) |
| Q2 | **keep** the ciphertext in 013 backups | restore on the same computer keeps the key; restore on another computer asks for the key again (`stored-unreadable`); the archive never contains the key ring (013 `spec.md` cross-feature note, tasks T035, T048, T062) |

## Agent definition changes

None new. `.claude/agents/ai-agent.md` rule update after ratification (CTO, T054).
