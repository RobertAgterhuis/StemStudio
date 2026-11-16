# Hand-off: backend / 011 follow-up claude-key, us-p1 (T023, T024, T027, T028, T029)

Branch `ck-lane-backend`; `ck-lane-agent` merged again (ai-agent T021/T022, T036/T037). Nothing pushed. No docker, no
Anthropic call (stub handlers only), no key-shaped literal.

## What was built

- **`ClaudeKeyEndpoints`** (`src/StemStudio.Api/Agent/Secrets/`): `GET` (`getClaudeKeyStatus`, not filtered) and `PUT`
  (`storeClaudeKey`) on `/api/agent/settings/claude-key`, group with `WithHttpLogging(HttpLoggingFields.None)`. `PUT` reads
  the body itself (`ClaudeKeyRequestReader`: `Content-Length` > 1 KiB or more than 1,024 bytes read -> 413; not a JSON object
  with a string `apiKey` (name case-insensitive) -> 400 `invalid-body` with the fixed text), then `ClaudeApiKey.Parse`
  (422 `claude-key-invalid` + `reason`, fixed texts), then 503 `key-store-unavailable` (detail =
  `ClaudeKeyProvider.StoreUnavailableReason(code)`), then store, `IClaudeKeyProvider.Invalidate()`, 200 `ClaudeKeyStatus`
  (agent library's `ClaudeKeyStatus.ToDto`). Never touches the backend selection or the notice.
- **`KeyEndpointLoggingInterceptor`** (`IHttpLoggingInterceptor`): sets `LoggingFields = None` for the key path, so a host
  with `UseHttpLogging` in front of routing logs nothing of the route (test proves it fails without the interceptor).
- **`LocalOnlyKeyEntryFilter`** (shared singleton, endpoint filter factory `LocalOnly()`): peer loopback (IPv4-mapped
  normalised) or an address of `StemStudio:Agent:KeyEntry:InternalCaller` (resolved per refused-loopback request through
  `IHostAddressResolver`/`DnsHostAddressResolver`), `Host` in `localhost`/`127.0.0.1`/`[::1]`/`api` (port ignored),
  `Origin` absent or `StemStudio:WebOrigin`; else 403 `local-only`; runs before the body is read; `KeyEntryRefused` (4105)
  at most once a minute with reason and peer, never the body. `KeyEntryPolicy` holds the configuration.
- **Wiring**: `AddClaudeKeyStore(configuration)` (in `AddAgentHost`) registers the store, `TryAdd` of the provider (the
  agent library's registration wins), policy, resolver, filter, interceptor. `AgentStatusProbe` fallback fills
  `claude.key`/`keyConfigured` from the provider. `AgentSettingsService` accepts `backend: claude` when the provider has a
  usable key (environment or stored); 409 text now "enter the key in Settings or export ANTHROPIC_API_KEY ...".
- `PendingAgentOperations` = `["deleteClaudeKey"]` (T033 removes it).
- Tests: `Agent/ClaudeKeyEndpointsTests`, `Agent/ClaudeKeyLeakTests` (CK-SC-001: responses, Trace logs, SSE and run of a
  Claude run against a stub, `stemstudio.db*`), `Agent/Secrets/LocalOnlyKeyEntryFilterTests`; fixture `Agent/ClaudeKeyApi`
  (real store + provider, stub Anthropic via `ActivatorUtilities`, optional HTTP logging / Trace); `ApiFactory` gets
  `TestPeerStartup` (header `X-Test-Peer`, default loopback). `SettingsEndpointTests` now use `ClaudeKeySentinels.Environment`
  (the provider format-checks the environment value).

## Tests and coverage

| Command | Result | Line | Branch |
|---|---|---|---|
| `dotnet test tests/StemStudio.Api.Tests -c Release -p:CollectCoverage=true` | 1591 passed, 2 skipped (Linux-only) | Api **98.97 %**, Contracts 100 % | Api **92.08 %**, Contracts 100 % |

Persistence unchanged since the foundational hand-off (98.91 % / 95 %).

## Notes / requests

- **devops**: compose must set `StemStudio__Agent__KeyEntry__InternalCaller=web` (data-model 1) and the Web app must call the
  API as `http://api:8081` (Host `api`); a direct LAN call to 8081 is refused (peer). T003 spike should confirm the peer
  address the API sees for the `web` container.
- **frontend**: the Web server calls must not send an `Origin` other than `StemStudio:WebOrigin`; 403 `local-only` detail
  is `LocalOnlyKeyEntryFilter.Detail`.
- **architect (T053)**: the 503 detail text is the provider's `StoreUnavailableReason` ("The key store is not available: the key
  ring directory ..."), slightly different from the contract example; 422 details per reason are in `ClaudeKeyEndpoints`.
