# 003 Close the unauthenticated API to callers other than the Web app
- Status: done (2026-11-23; commits `ee4a5cc4`..`874686a8` and `55dc9824` on branch `sec-003-004`, see "Outcome") · Date: 2026-11-19 · Area: Api / security (StemStudio.Api, ApiComposition, Security) · Priority: P1

## Problem statement
The API has no authentication and, apart from the Claude-key endpoints, no check of who is calling. The one app-wide guard lets
any request without an `Origin` header through, and the host filter accepts every host name. In the default deployment the API
port is published on every network interface, so any device on the same network can read and change the whole library.

Evidence:
- No authentication or authorisation anywhere in the API: `grep UseAuthentication|UseAuthorization|RequireAuthorization|AddAuthentication`
  in `src/StemStudio.Api` finds nothing. The only peer check is `src/StemStudio.Api/Agent/Secrets/LocalOnlyKeyEntryFilter.cs:51-74`
  (loopback or the configured internal caller), and it applies only to the two key-entry endpoints
  (`src/StemStudio.Api/Agent/Secrets/ClaudeKeyEndpoints.cs:48-64`).
- `src/StemStudio.Api/Security/CrossSiteRequestGuard.cs:47-60`: a state-changing request is refused only when an `Origin` header is
  present and foreign, or when `Sec-Fetch-Site: cross-site` is sent. The type comment (lines 17-22) says on purpose that requests
  without an `Origin` (curl, scripts) pass. GET, HEAD and OPTIONS are never checked.
- `src/StemStudio.Api/appsettings.json:8` has `"AllowedHosts": "*"`. No host filtering anywhere, although
  `KeyEntryPolicy.cs:15` already keeps a loopback host list for key entry. ADR-027 D6 lists "host allow-lists on all listeners
  and the API (DNS rebinding)" as a requirement.
- `docker-compose.yml:182` publishes `"8081:8081"` on all interfaces. ADR-027 D3 says the base compose file "now publishes
  8080/8081 on 127.0.0.1", but that is not in the file yet (it belongs to feature 018, which has not been built).
- Size of the exposed surface: 81 `MapPost`/`MapDelete` and 75 `MapGet` calls in `src/StemStudio.Api`. Among them are
  `restoreBackup`, `revertRestore`, `executeCleanup` and `deleteBackup` (`LibraryCare/LibraryCareEndpoints.cs:29-45`), and the
  agent endpoints that send messages and therefore use the stored Claude key (`Agent/AgentEndpoints.cs:52-84`).

## Impact
- A device on the home network (a guest phone, a compromised IoT device) can delete or overwrite the library, start a restore or a
  cleanup, and start agent runs that use the owner's Claude key and cost money, all with a plain `curl` and no `Origin` header.
- A web page in the owner's browser can use DNS rebinding to read every GET resource: tracks, stems, lyrics, agent
  conversations. The host is never checked, and a same-origin GET carries no `Origin`.
- Feature 018 (tablet remote) depends on the assumption "the API is never on the LAN" (ADR-027 D3). Today that assumption is not
  true.

## Risk
- Leaving it: confidentiality and integrity of the owner's data, and cost exposure through the Claude key. Likelihood is moderate
  (it needs network access), impact is high.
- The change: the API also serves the browser directly (stems, through CORS from `http://localhost:8080`) and the Web container
  calls it as `http://api:8081`. A host allow-list or peer rule that is too strict breaks playback or server-side calls. Behind
  Docker Desktop port forwarding the remote IP of a host browser is the bridge gateway, not loopback, so a peer-IP rule alone does
  not separate "host" from "LAN". The fix therefore combines binding, the host list and the guard. Rollback means reverting one
  middleware and one configuration value.

## Effort
M (2-4 days). Assumes the compose binding change is done by the CI/docker owner in the same round, and that the E2E suite covers the
browser-to-API stem path.

## Step-by-step plan
1. Safety net: extend `tests/StemStudio.Api.Tests/Security/CrossSiteRequestGuardTests.cs` with characterisation tests that pin
   today's behaviour (an Origin-less POST passes, a foreign Host passes). Mark the ones that must flip, so the change becomes visible
   as a test diff.
2. Host filtering: set `AllowedHosts` to `localhost;127.0.0.1;[::1];api` (configurable through `StemStudio:AllowedHosts`), and
   reuse the `KeyEntryPolicy` list instead of keeping a second copy. Check: integration tests send `Host: evil.example` and get 400;
   E2E stays green.
3. Origin-less writes: refuse a state-changing `/api` request without `Origin` unless the peer is loopback or the configured internal
   caller. Extract that rule from `LocalOnlyKeyEntryFilter.IsTrustedPeerAsync` into one shared `TrustedPeerPolicy`, used by both the
   guard and the key filter. Check: the agent loopback client (`Agent/Loopback/LoopbackClient.cs`) and the Web server-side calls
   still work in E2E.
4. Ask the CI/docker owner to publish `127.0.0.1:8081:8081` (with `STEMSTUDIO_BIND_ADDRESS` as in ADR-027 D3). Record it in the
   018 plan so that no second listener is added without these checks.
5. Add a start-up warning when the API listens on a non-loopback address outside a container, the same way `KeyRingStatus` logs its
   start-up warnings.
6. Verify the end state: from a second machine, `curl -X POST http://<host>:8081/api/...` gets 403 or no connection; a DNS-rebinding
   style request (`Host` header not in the list) gets 400; the full E2E suite passes. Roll back by reverting the middleware commit
   and the `AllowedHosts` value.

## Dependencies and related proposals
ADR-027 (D3, D6) is the design source. The compose binding change belongs to the CI/docker audit. Do this before feature 018
starts.

## Outcome (2026-11-23)
1. `tests/StemStudio.Api.Tests/Security/ApiExposureTests.cs` pinned today's behaviour (`ee4a5cc4`); the "003 flips" cases
   were turned around test-first (`11f192c2`, `12ce23e7`).
2. Host filtering from `StemStudio:AllowedHosts` (`ApiExposure`), default `KeyEntryPolicy.LocalHosts`
   (`localhost;127.0.0.1;[::1];api`), empty `Host` refused, `"AllowedHosts": "*"` removed (`92293df5`). Compose passes
   `STEMSTUDIO_API_ALLOWED_HOSTS` (`55dc9824`). The e2e and generative overrides use only `localhost` and `api`, so
   nothing was added.
3. `TrustedPeerPolicy` (loopback or the internal caller, socket address only) is shared by `CrossSiteRequestGuard` and
   `LocalOnlyKeyEntryFilter`; an Origin-less (or empty-Origin) write from another peer gets 403 (`eb2a7adb`). The
   Playwright API contexts now send the Web app's `Origin` (`27d30a21`), because behind Docker port forwarding the
   runner's requests arrive from the bridge gateway.
4. Compose binding done in refactor 004 (same branch); recorded in `specs/018-tablet-remote/plan.md` (flag 2).
5. `ListenerExposure` logs `ApiListensBeyondLoopback` (4106) outside a container (`9915d1de`, `c1235f64`).
6. Verified on the isolated E2E stack: a host `curl -X POST` without `Origin` and one with `X-Forwarded-For: 127.0.0.1`
   get 403, `Host: evil.example` gets 400, the web container and in-container loopback pass, the worker container
   gets 403. E2E on that stack: Chromium 392 passed (the 2 failures, a missing practice-flow fixture and an export
   timing flake, passed on rerun), `@recording` 18 passed / 1 skipped, `@agent-unavailable` Chromium and Firefox
   passed. A second machine cannot connect at all (127.0.0.1 binding).

Residual risk: `Host` and `Origin` are browser defences; a non-browser client that can reach the port forges both. With
`STEMSTUDIO_BIND_ADDRESS` opened, a LAN client sending `Origin: http://localhost:8080` can still change data (there is
no authentication; feature 018's remote listener is the supported LAN path). Reads are unguarded by design. The web
app has no host allow-list yet (018). Rollback: revert `eb2a7adb` (Origin rule) and `92293df5` (host list), or set
`STEMSTUDIO_API_ALLOWED_HOSTS=*`.
