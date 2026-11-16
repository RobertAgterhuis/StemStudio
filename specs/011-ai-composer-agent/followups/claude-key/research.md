# Research: Claude key in Settings, stored encrypted (011 follow-up)

Sources: Microsoft Learn (fetched 2026-11-16 through the Microsoft Learn MCP), the 011 code on
`main` (`ClaudeChatClientFactory`, `AgentStatusService`, `AgentSettingsService`, `ApiComposition`,
`docker/api.Dockerfile`, `docker-compose.yml`, `scripts/secret_scan.py`), the 013 and 018 designs.
No Anthropic request was made for this research.

## R-01 Encryption: ASP.NET Core Data Protection

**Decision**: `IDataProtectionProvider.CreateProtector("StemStudio.Agent.ClaudeApiKey.v1")` in the
API; `Protect(string)` before writing, `Unprotect(string)` when a client is created. Ciphertext is
stored as the Data Protection base64url string.

Facts (Microsoft Learn):

- Default payload protection is AES-256-CBC for confidentiality and HMACSHA256 for authenticity,
  sub-keys derived per payload from a 512-bit master key that changes every 90 days; keys have a
  90-day lifetime and "as long as retired keys remain on the system, your app can decrypt any data
  protected with them".
  https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/default-settings?view=aspnetcore-10.0#key-lifetime
- Deleting a key "makes its protected data permanently inaccessible"; Microsoft recommends not
  deleting keys. StemStudio never deletes keys from the ring.
  https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/default-settings?view=aspnetcore-10.0#delete-keys
- The Data Protection APIs "aren't primarily intended for indefinite persistence of confidential
  payloads ... That said, [they] can be used for long-term protection of confidential data."
  A single API key that the owner can always re-enter is an acceptable long-term payload; the
  `stored-unreadable` path covers the failure mode.
  https://learn.microsoft.com/aspnet/core/security/data-protection/introduction?view=aspnetcore-10.0#design-philosophy
- Data Protection ships in the shared framework `Microsoft.AspNetCore.App`, which the API already
  uses: **no new package, no new licence**.

Re-protection after rotation: `IPersistedDataProtector.DangerousUnprotect(..., ignoreRevocationErrors: false,
out requiresMigration, out wasRevoked)` reports when the payload was protected with a non-default
key; the store then re-protects and saves (spec edge case). A revoked key still throws and maps to
`stored-unreadable`.

Alternatives rejected: own AES-GCM with a key file (re-implements key management, rotation and
format versioning that Data Protection already provides); SQLCipher (encrypts the whole database,
new native dependency, changes 013 backups, the key would still sit next to it);
OS keychains (no portable container story).

## R-02 Key ring at rest: not encrypted in a Linux container

**Fact**: "If the developer overrides the rules outlined above and points the Data Protection
system at a specific key repository, automatic encryption of keys at rest is disabled."
https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/default-settings?view=aspnetcore-10.0#key-management
and "If you change the key persistence location, the system no longer automatically encrypts keys
at rest, since it doesn't know whether DPAPI is an appropriate encryption mechanism."
https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0#persist-keys-to-the-file-system-persistkeystofilesystem

**Fact**: "When hosting in a Docker container, keys should be persisted in a folder that's a Docker
volume ... that persists beyond the container's lifetime."
(default-settings page, Key management).

**Fact**: on Windows, `ProtectKeysWithDpapi()` without parameters makes the key ring decryptable
only by the current Windows user; the API is Windows-only.
https://learn.microsoft.com/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-10.0

**Decision**:

| Host | Key ring location | At-rest protection of the ring |
|---|---|---|
| Docker (`api` container, Linux) | named volume `api-keys` at `/keys`, only in `api` | none by Data Protection; directory `0700`, owner `$APP_UID`; the volume is separate from `api-data` |
| Non-Docker Windows | `%LOCALAPPDATA%\StemStudio\keys` | `ProtectKeysWithDpapi()` (current user) |
| Non-Docker Linux/macOS | `$XDG_DATA_HOME/stemstudio/keys` or `~/.local/share/stemstudio/keys` | none; directory `0700` |
| Tests | `EphemeralDataProtectionProvider` or a temp directory | n/a |

A certificate (`ProtectKeysWithCertificate`) was considered for Linux: the certificate's private
key would have to live on the same host with the same file permissions, so it adds management
without changing who can read it. Rejected (YAGNI); the threat model states the limit.

## R-03 Application name

**Fact**: by default Data Protection isolates apps by their content-root path; `SetApplicationName`
sets `DataProtectionOptions.ApplicationDiscriminator`; apps with different discriminators cannot
read each other's payloads.
https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0#set-the-application-name-setapplicationname
and #per-application-isolation.

**Decision**: `SetApplicationName("StemStudio.Api")`. The content root differs between the
container (`/app`) and the non-Docker path, and could change with a Dockerfile edit; a fixed name
keeps ciphertexts valid across rebuilds. It is deliberately different from the Web app's ring
(`web-keys` volume, circuits and antiforgery only), and the two rings are in different volumes.

## R-04 Key ring directory permissions and volume initialisation

- The .NET runtime image runs as `$APP_UID` (non-root, `docker/api.Dockerfile`). A named volume
  that is empty on first use is initialised from the image's directory at the mount point, including
  ownership and mode. So the Dockerfile creates `/keys` with `install -d -m 0700 -o "$APP_UID" -g
  "$APP_UID" /keys` (devops task T020); `VOLUME ["/keys"]` is declared.
- The API checks at start (Linux only, `File.GetUnixFileMode`): mode wider than `UserRead |
  UserWrite | UserExecute` -> `File.SetUnixFileMode` to `0700` when it owns the directory, else one
  warning `KeyRingPermissionsWide` (path only). Windows: inherited ACL of `%LOCALAPPDATA%` is
  per-user already.
- Placement guard: the resolved full path MUST NOT equal or be below `StemStudio:DataDir`,
  `StemStudio:BackupDir` (013), the content root or a directory that contains `.git`; violation ->
  key storage disabled with reason `key-store-unavailable`.

## R-05 "Local only" in the Docker set-up (verification needed)

Today compose publishes `8080` (web) and `8081` (api) on all host interfaces, the API has no
authentication, and the owner decided on 2026-11-16 to keep LAN access as is until 018
(CTO-DECISIONS, row 018). Findings:

1. The Settings page is a Blazor Server component; its API calls go server-to-server from the `web`
   container to `http://api:8081`. The API therefore sees the **web container's** address for every
   Settings call, whether the browser is on the computer or on the LAN.
2. With Docker Desktop's published ports, connections from the host and from the LAN reach the
   containers through Docker's port forwarding; the peer address inside the container is not the
   browser's address and is not loopback. The container cannot tell "browser on the computer" from
   "browser on the LAN" by address. (Expected behaviour of Docker Desktop's port forwarding; to be
   confirmed in spike T003 together with 018's T003.) **Confirmed for the host (F-01); the LAN
   device row stays for quickstart Q9b.**
3. A direct call to `http://<host>:8081/api/agent/settings/claude-key` from the LAN arrives from
   the forwarding address, not from the web container, so the API filter (loopback or the `web`
   service address) refuses it.
4. `PUT`/`DELETE` with `application/json` from another web origin trigger a CORS preflight, which
   the API's policy (GET only for the web origin) rejects, so a malicious web page cannot call the
   endpoints from the owner's browser.

**Decision**: two layers, documented honestly:

- **API** (`LocalOnlyKeyEntryFilter`): peer is loopback **or** one of the addresses of the
  configured internal caller (`StemStudio__Agent__KeyEntry__InternalCaller=web`, resolved by DNS,
  cached 60 s; empty on the non-Docker path); `Host` in `localhost`, `127.0.0.1`, `[::1]`, `api`;
  `Origin`, when present, equal to `StemStudio:WebOrigin`. Else `403 local-only`.
- **Web**: the key controls are enabled only when the page request that opened the circuit had a
  loopback `Host` (captured in a scoped `PageOrigin` service at the first request, since
  `HttpContext` is not available inside a circuit afterwards).
- **Limit before 018**: the Web layer is a host-name guard. A hostile client on the LAN can send a
  forged `Host: localhost` to port 8080 and drive the page. What it can then do is limited: it can
  overwrite or delete the stored key (it can never read it; the API is write-only), and it can
  already today do anything else the unauthenticated app allows (including selecting Claude with
  the owner's environment key). The hard boundary is 018 FR-011/FR-014 (ports bound to
  `127.0.0.1`, host-name allow-list on both listeners). Owner decision Q1 (2026-11-16): the
  binding is not pulled forward; LAN access stays until 018 and this limit is an accepted residual
  risk (plan.md threat T7).

### Spike T003 findings (devops, 2026-11-16)

Reference machine: Windows 11, Docker Desktop with the WSL2 backend (Engine 29.9.2). A throwaway
compose project `ck-spike` on host ports 18080/18081 ran a probe ASP.NET Core app (.NET 10,
`mcr.microsoft.com/dotnet/aspnet:10.0`, non-root `$APP_UID`) as `api` and `web`, with the same
`/keys` lines as `docker/api.Dockerfile` (T020) and a named volume `api-keys` at `/keys`. The
probe returned `Connection.RemoteIpAddress`, the `Host` header and `X-Forwarded-For`, protected
and unprotected a payload with `SetApplicationName("StemStudio.Api")` and
`PersistKeysToFileSystem("/keys")`. The owner's `stemstudio` stack was not touched; the project,
its volumes and the probe image were removed afterwards (`down -v`).

| # | Question | Observed | Consequence |
|---|---|---|---|
| F-01 | Peer seen by `api` for a request from the host through a published port | `http://localhost`, `127.0.0.1`, `[::1]` **and the host's own LAN address** all arrived from the compose network's **gateway** (`::ffff:10.200.5.1`, the network was `10.200.5.0/24`); never loopback; no `X-Forwarded-For` | the API cannot tell "browser on the computer" from "browser on the LAN" by address. The gateway address must **not** be allow-listed (it would admit the LAN too). A second LAN device was not available in the spike; it uses the same port forwarding and is the quickstart Q9b row |
| F-02 | Peer seen by `api` for a call from the `web` service (`http://api:8081`) | `::ffff:10.200.5.3` = the `web` container; `Dns.GetHostAddresses("web")` returned `10.200.5.3`; `Host` was `api:8081` | `LocalOnlyKeyEntryFilter` (backend T028/T029) must compare with `MapToIPv4()` (the peer is an IPv4-mapped IPv6 address, DNS gives plain IPv4) and must compare the `Host` **without the port** (`api:8081`, `localhost:8081`) |
| F-03 | Is the loopback host name the only browser-side signal? | yes: the `Host` header is passed through unchanged (`Host: localhost` sent to the LAN address arrived as `localhost`); the peer address is the same for every host-side origin (F-01) | the Web layer's `PageOrigin` loopback check is the only signal and a LAN client can forge it: threat T7 as accepted (owner Q1 (b)) |
| F-04 | Mode and owner of a named volume at `/keys` for the non-root app user | empty volume on first mount: `700 1654:1654` (`app`, `$APP_UID` = 1654), initialised from the image's `install -d -m 0700 -o "$APP_UID" -g "$APP_UID" /keys`; key files are written `0600` by Data Protection. A volume that was first mounted (still empty) into an image **without** `/keys` was still initialised `700 1654:1654` at the next mount into the api image (Docker copies up into any **empty** volume) | the Dockerfile lines are enough for new and empty volumes; a non-empty volume with another owner is the API's warning path (`KeyRingPermissionsWide`, backend T013/T014) |
| F-05 | The key ring and a protected payload survive `docker compose down` + `up --build --force-recreate` with a fixed application name | yes: the payload unprotected after `down` (no `-v`) and a rebuild that also moved the content root from `/app` to `/app2`; the same single key file was reused. The same payload with application name `Other` -> `CryptographicException` | `SetApplicationName("StemStudio.Api")` is required (R-03) and sufficient; only `down -v` or `docker volume rm` loses the ring (quickstart Q9) |
| F-06 | Side effect of `VOLUME ["/keys"]` | a container from the api image **without** an explicit `/keys` mount gets an anonymous volume there (seen on the probe's `web`, which reused the image) | harmless in compose (`web`/`worker` use other images); a one-off `docker compose run api` has its own empty ring, so a stored key reads as `stored-unreadable` there |
| F-07 | Git Bash | MSYS path conversion rewrote a container path in an environment variable into `C:/Program Files/Git/...` (CoreCLR failed to start) | scripts that pass container paths from Git Bash set `MSYS_NO_PATHCONV=1` (known rule, as for `/scripts`) |

## R-06 Logging, exceptions and DTOs

- HTTP logging is not enabled in the API today (`ApiComposition`, no `AddHttpLogging`). The key
  endpoint still gets `.WithHttpLogging(HttpLoggingFields.None)` and the API registers an
  `IHttpLoggingInterceptor` that disables all fields for the key path, so a later global
  `AddHttpLogging(All)` cannot log the body. Endpoint-specific configuration overrides the global
  options, and an interceptor runs after both.
  https://learn.microsoft.com/aspnet/core/fundamentals/http-logging/?view=aspnetcore-10.0#logging-configuration-order-of-precedence
  and #ihttplogginginterceptor
- C# records generate a `ToString()` that prints every property. The request type
  `StoreClaudeKeyRequest` is a sealed class (not a record) whose `ToString()` returns
  `StoreClaudeKeyRequest { ApiKey = [redacted] }`; the in-memory value type `ClaudeApiKey` does the
  same and exposes the value only through `Reveal()` for the SDK. A test asserts both.
- Minimal API binding errors: `RouteHandlerOptions.ThrowOnBadRequest = false` is set already; the
  key endpoint reads the body itself (`ReadFromJsonAsync` within a 1 KiB limit inside try/catch)
  and answers a fixed `400 invalid-body`, so `JsonException` text (which can quote positions, not
  values, but is not worth the risk) never reaches a response or log.
- `CryptographicException` messages name key ids, not secrets; they are still not logged. The
  store logs the fixed event `ClaudeKeyUnreadable` (EventId 4101) with the exception **type** only,
  once per API process.
- The SDK receives the key as before (`AnthropicClient.ApiKey`); `ClaudeChatClientFactory` caches
  one client per key value (011 code) and keeps that behaviour; the cache key becomes the key
  fingerprint (SHA-256 of the key, in memory only) instead of the plain value.

## R-07 Existing secret scan

`scripts/secret_scan.py` (rule `anthropic-key`) fails on the prefix `sk-ant-` followed by a key
character anywhere in the repository. Consequences: tests build sentinel keys at run time
(`"sk-" + "ant-" + RandomPart()`), docs write the prefix only as `` `sk-ant-` `` followed by a
non-key character, and the format regex in code is assembled from constants. The 011 SC-006 grep
test is extended to the new stores (T021, T027).

## R-08 Key format

Anthropic console keys start with the prefix `sk-ant-`, followed by a type/version segment and
base64url characters, about 108 characters in total today; admin keys (prefix `sk-ant-` plus
`admin`) are for the Admin API and do not work for Messages. Validation: trimmed length 40-256, the
prefix, characters `[A-Za-z0-9_-]` only, not an admin key. The live check is the 011 model lookup
(`GET /v1/models/{id}`), free, only on **Test key**.

## R-09 Backups (013) and restore

013 FR-004 already excludes "Data Protection keys" and secrets; 013's backup reads only the
`api-data` volume and the database snapshot. The separate `api-keys` volume is therefore outside
any archive by construction; the cross-feature note makes it explicit and adds a test (013 T035
extension: no entry from the key ring directory, no plaintext sentinel). Decision on the ciphertext
row: kept in the snapshot (owner decision Q2, 2026-11-16: keep, do not strip).

## Dependencies

| Name | Version | Licence | URL | Note |
|---|---|---|---|---|
| Microsoft.AspNetCore.DataProtection (shared framework) | .NET 10 | MIT | https://github.com/dotnet/aspnetcore | already part of `Microsoft.AspNetCore.App`; no new reference |
