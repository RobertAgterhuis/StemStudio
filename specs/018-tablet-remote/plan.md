# Implementation Plan: Tablet and Remote

**Branch**: `018-tablet-remote` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/018-tablet-remote/spec.md`; research
[research.md](research.md); security design [ADR-027](../../docs/decisions/ADR-027-tablet-remote-lan-security.md).

## Summary

A music-stand layout (Stand view) in the mixer, an installable PWA with an offline shell, and LAN
remote control: a paired phone or tablet runs a small separate remote client that talks to the web
app's **remote listener** over one WebSocket (remote-protocol 1.0.0); commands are dispatched in
the web app to the mixer component of the computer's tab, which plays the audio. Pairing, device
keys, settings and the audit log live in the API (`/api/remote/*`, backend 1.17.0). Remote mode is
off by default at two levels; the default compose binding moves to `127.0.0.1`.

## Technical Context

**Language/Version**: C# / .NET 10 (API, web); TypeScript 6.0 (new `src/remote-client`, additive
`src/web-interop`); Python untouched.
**Primary Dependencies**: ASP.NET Core (Kestrel multi-endpoint, WebSockets, host filtering), Blazor
Interactive Server + Radzen (existing), **QRCoder 1.8.1** (MIT, web), **pdfjs-dist 6.5.438**
(Apache-2.0, remote client); dev tooling as `src/web-interop`.
**Storage**: SQLite (EF Core migration `AddRemoteControl`: `RemoteSettings`, `RemoteDevices`,
`RemoteAuditEvents`); pairing windows and the hub in memory; browser `localStorage` on both
surfaces.
**Testing**: xUnit v3 + Shouldly + NSubstitute (API, web), bUnit (pages, mixer stand mode, bridge),
`WebApplicationFactory` with a configurable port classifier for the gate, Vitest + jsdom (remote
client, service-worker routing table, web-interop additions), Playwright with two browser contexts,
axe, Lighthouse installability.
**Target Platform**: computer: Windows 11 + Docker Desktop (reference) or the non-Docker path;
remotes: current iPadOS/iOS Safari, Android Chrome, desktop browsers.
**Project Type**: web (existing multi-project solution).
**Performance Goals**: remote command -> engine p95 <= 150 ms loopback / 250 ms Wi-Fi; state push
<= 200 ms; snapshot <= 8 KB at <= 10/s; remote first load <= 300 KB gzip without pdf.js.
**Constraints**: nothing reachable from the LAN except the remote surface; no cloud; no secrets in
files beyond the owner's TLS key (Complexity Tracking); SIG limits; >= 90 % coverage per component.
**Scale/Scope**: 1 user, <= 8 paired devices, <= 4 connections, 24 commands, 6 help topics.

## Constitution Check

| Principle | How 018 complies | Result |
|---|---|---|
| I Test-first, >= 90 % | Every task is a red/green pair; new coverage gate for `src/remote-client`; security verification list is executable (SV-01..SV-22); E2E with two contexts; no real network beyond loopback in CI | PASS |
| II SIG 4 stars | Small units: gate, classifier, validator, projector, router, registry, limiter are separate classes; command dispatch as a table (name -> handler), not a switch; families as data; tools/quality extended to `src/remote-client` | PASS |
| III Contracts first | backend-api 1.17.0 (delta now, merge in Phase 0), remote-protocol 1.0.0 (markdown + OpenAPI 3.1 + JSON Schema + examples), ui-test-ids 1.17.0; web-interop and help-content get additive minors at merge (DELTA.md) | PASS |
| IV Open source, lawful | QRCoder (MIT), pdfjs-dist (Apache-2.0); mkcert is an owner-run tool (BSD-3-Clause), not shipped; no paid or cloud service | PASS |
| V Simplicity, honesty | Audio stays on the computer (no stem transfer, no sync engine); full snapshots instead of patches; HTTP mode labelled "Not encrypted"; wake lock state shown honestly (no tricks) | PASS |
| Security constraints | Opt-in twice; default exposure reduced; validated input on every message; no subprocess; secrets: device keys hashed, never logged; TLS key per Complexity Tracking | PASS with one tracked interpretation |
| Docs (1.3.1) | user manual chapter + technical manual deployment/security/API sections in the integration wave | PASS |

## Project Structure

### Documentation (this feature)

```text
specs/018-tablet-remote/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/
    ├── DELTA.md                          # all contract changes and the merge plan
    ├── backend-api.remote.openapi.yaml   # 1.17.0-delta.1 (standalone, additive)
    ├── remote-protocol.md                # remote-protocol 1.0.0 (normative)
    ├── remote-lan.openapi.yaml           # remote-protocol 1.0.0, HTTP part
    ├── remote-messages.schema.json       # remote-protocol 1.0.0, WebSocket messages
    ├── remote-examples/{valid,invalid}/  # example messages (schema tests)
    ├── ui-test-ids.delta.md              # 1.17.0 (additive)
    └── redocly.yaml
docs/decisions/ADR-027-tablet-remote-lan-security.md
```

### Source Code (additions)

```text
src/StemStudio.Contracts/Remote/            # architect (Phase 0): DTOs of backend 1.17.0 + ApiRoutes.Remote*
src/StemStudio.Domain/Remote/               # backend: PairingWindow, PairingCode, CheckNumber, DeviceName, DeviceKey (pure)
src/StemStudio.Persistence/Remote/          # backend: entities, configurations, migration AddRemoteControl, stores
src/StemStudio.Api/Remote/                  # backend: RemoteEndpoints, PairingRegistry, PairingRateLimiter,
                                            #   RemoteDeviceService, RemoteAuditService, RemoteOriginFilter, expiry sweep
src/StemStudio.Web/Remote/                  # frontend: server side of the remote surface
├── RemoteOptions.cs, RemoteDeployment.cs   #   configuration + validation (data-model 1.1)
├── RemoteListener.cs                       #   Kestrel endpoints (main + remote), certificate loading
├── ListenerClassifier.cs, RemoteGate.cs    #   port classification, host/origin/path allow-list, headers (R1-R8)
├── RemoteSocketEndpoint.cs                 #   WebSocket handshake, hello, receive loop, close codes
├── RemoteHub.cs, PlayerRegistry.cs         #   players, navigators, connections, snapshot fan-out
├── RemoteCommandValidator.cs               #   schema + live-state validation
├── RemoteCommandRouter.cs                  #   name -> handler table (014 command catalogue)
├── RemoteStateProjector.cs, RemoteFamilies.cs
├── RemoteContentEndpoints.cs               #   lyrics/chords/sheets/map/artwork proxy (active track only)
├── PairingProxyEndpoints.cs                #   redeem/poll forwarders, info
├── DeviceAuthenticator.cs                  #   API authenticate + 60 s cache + eviction
└── CommandRateLimiter.cs
src/StemStudio.Web/Components/Remote/       # frontend: RemoteControlPage (/remote-control), PairingDialog, DeviceList, AuditLog
src/StemStudio.Web/Components/Mixer/Stand/  # frontend: StandView, StandTransport, StandContent, PartPicker, WakeLockSwitch
src/StemStudio.Web/Components/Mixer/Mixer.Remote.cs   # frontend: RemotePlayerBridge registration + command handlers
src/StemStudio.Web/Components/Shell/        # frontend: InstallAppButton, RemoteIndicator (additions)
src/StemStudio.Web/wwwroot/manifest.webmanifest, sw.js, offline.html, icons/   # frontend
src/StemStudio.Web/Help/topics/remote/*.md, topics/mixer/stand-view.md, topics/start/install-app.md
src/web-interop/src/{pwa,wakelock}.ts       # frontend: additive exports (install prompt, SW registration, wake lock)
src/remote-client/                          # NEW TS package (skeleton: architect Phase 0; code: frontend)
├── package.json, tsconfig.json, vite.config.ts, vitest.config.ts, eslint.config.js
├── src/types.ts                            #   protocol types generated by hand from remote-messages.schema.json
├── src/{connection,pairing,store,clock,commands,ui/*,stand/*,sheet/*,sw/*}.ts
└── tests/
docker-compose.yml (bind address), docker-compose.lan.yml (NEW), scripts/lan-*.{ps1,sh}   # devops
e2e/tests/remote-*.spec.ts, e2e/tests/stand-*.spec.ts, e2e/tests/pwa.spec.ts, e2e/support/remote-context.ts  # qa
```

**Structure Decision**: no new .NET project. The remote server side belongs to the web app
because the player is a Blazor component there; persistence and pairing state belong to the API,
which already owns the database. The remote client is a separate TS package like `web-interop`
and the audio engine.

### Ownership map (additions)

| Path | Owner |
|---|---|
| `src/StemStudio.Contracts/Remote/**`, `src/remote-client/` skeleton (package files, `src/types.ts`), `tests/StemStudio.Api.Tests/Contract/RemoteContractTests.cs` skeleton | architect (Phase 0) |
| `src/StemStudio.Domain/Remote/**`, `Persistence/Remote/**`, `Api/Remote/**` + tests | backend |
| `src/StemStudio.Web/**` additions, `src/web-interop/**` additions, `src/remote-client/**` code + tests | frontend |
| `docker-compose.yml`, `docker-compose.lan.yml`, `.github/**`, `scripts/**`, `.gitignore` cert patterns | devops |
| `e2e/**`, `docs/testing/**` | qa |
| `tools/quality/**` (`src/remote-client` in SIG + duplication + dependency rules) | quality-gate |
| `docs/public/src/**`, PDFs, `docs/THIRD-PARTY-LICENSES.md`, `SECURITY.md`, `docs/STATUS.md` | CTO (integration wave) |

No changes for audio-engine (engine 2.4.0 untouched), worker, generator, singer or ai-agent.

## Key flows

### Pairing

```text
Computer (Blazor, localhost)          Web remote surface (LAN)        API (127.0.0.1)
 Pair a device ---------------------------------------------------> POST /api/remote/pairings
 shows code + QR (QRCoder SVG) + countdown <----------------------- 201 {code, expiresAt}
                                    tablet: POST /remote/v1/pairings/redeem {code,name}
                                      gate: host, origin, rate --> POST /api/remote/pairings/redeem {..., transport}
                                      <-- 202 {requestId, pollSecret, checkNumber}
 polls GET /api/remote/pairings/current (1 s) -> "Allow 'iPad'? check 417"
 Allow -----------------------------------------------------------> POST .../allow -> device row
                                    tablet polls GET /remote/v1/pairings/requests/{id} (X-Pairing-Secret)
                                      --> POST /api/remote/pairings/requests/{id}/poll -> key once
                                    tablet stores key, opens wss://.../remote/v1/ws
```

### Command

```text
tablet --command--> RemoteSocketEndpoint -> limiter -> validator (schema, live state)
  -> RemoteHub.ActivePlayer -> RemotePlayerBridge.InvokeAsync(handler from RemoteCommandRouter)
  -> same Mixer handler as the UI / 014 shortcut -> IAudioEngine (JS interop) -> sound
  -> mixer state change -> RemoteStateProjector -> snapshot -> every connection; ack to sender
```

### Content

```text
tablet GET /remote/v1/content/sheets/{assetId} (Bearer)
  -> gate -> DeviceAuthenticator -> active player's TrackId -> asset belongs to track and is a sheet?
  -> IStemStudioApiClient (server side) -> stream PDF (<= 20 MB, no-store)
```

## Security verification (binding list; each item is a test, SC-004)

| Id | Check | Where |
|---|---|---|
| SV-01 | Mode `off`: only the main port is bound (Kestrel endpoints enumerated) | web xUnit |
| SV-02 | Remote listener: `/`, `/tracks`, `/_blazor`, `/_blazor/negotiate`, `/_framework/blazor.web.js`, `/health`, `/api/tracks`, `/css/base.css` -> 404 | web xUnit + E2E |
| SV-03 | Main listener: `/remote/v1/info`, `/remote/v1/ws`, `/remote/v1/content/lyrics` -> 404 | web xUnit |
| SV-04 | Foreign `Host` on main, remote and API -> 400 | web/API xUnit + E2E (`Host: evil.example`) |
| SV-05 | Default compose: 8080/8081 bound to 127.0.0.1 (compose config check), remote port only on `STEMSTUDIO_LAN_ADDRESS` | devops script + CI |
| SV-06 | WebSocket upgrade with foreign or missing `Origin` -> 403; missing subprotocol -> 400 | web xUnit + E2E |
| SV-07 | No `hello` in 5 s -> 4408; bad key -> 4401; switch off -> 4403; fifth connection -> 4409 | web xUnit |
| SV-08 | Remove device while connected -> `bye revoked` + 4410 within 1 s; switch off -> all closed within 1 s | web xUnit + E2E |
| SV-09 | Wrong code x5 -> window `stopped`; 6th attempt with the right code -> 404 | API xUnit + E2E |
| SV-10 | 21st redeem in a minute -> 429 with `Retry-After` | API xUnit |
| SV-11 | Unknown, expired, used and wrong codes give byte-identical problem bodies | API xUnit |
| SV-12 | Key delivered exactly once; wrong poll secret -> 404; uncollected key -> device removed after 60 s | API xUnit |
| SV-13 | Keys stored only as SHA-256; no key/code/secret/check number in logs or audit (log capture test) | API + web xUnit |
| SV-14 | `/api/remote/*` non-GET with `text/plain` or form body -> 415; with `Origin: http://evil.example` -> 403 | API xUnit |
| SV-15 | Message > 4 KB, binary frame, invalid JSON, schema violation -> 4400; 21 commands in 1 s -> 4429 | web xUnit |
| SV-16 | Every `remote-examples/invalid` message is rejected and changes nothing | web xUnit |
| SV-17 | Content for another track id, a non-sheet asset or with no player -> 404 | web xUnit + E2E |
| SV-18 | Security headers and CSP present on every remote response (R5); no inline script in `/remote/index.html` | web xUnit + Vitest |
| SV-19 | Device name with `<script>` is rendered as text on both surfaces | bUnit + Vitest |
| SV-20 | `lan-https` with expired / mismatched / missing certificate -> remote surface off, `problem` reported, no HTTP listener | web xUnit |
| SV-21 | Public `PublicHost` or `ListenAddress` (e.g. 8.8.8.8) -> remote surface off | web xUnit |
| SV-22 | Service worker never answers `/_blazor`, `/_framework/*`, API origin, stems, non-GET or cross-origin requests | Vitest (routing table) + E2E |

## Contract versions

| Contract | Before | 018 | Note |
|---|---|---|---|
| backend-api | 1.16.0 (017) | **1.17.0** | delta now; merged in Phase 0 |
| ui-test-ids | 1.16.0 (017) | **1.17.0** | delta now; merged in Phase 0 |
| remote-protocol | - | **1.0.0** | new |
| web-interop | 1.0.0 (+ whatever 012-017 add) | next minor at merge | additive exports `pwa.*`, `wakeLock.*` (DELTA.md 4); not in the roadmap table, flagged |
| help-content | 1.0.0 (+ 012-017) | next minor at merge | new area `remote` (DELTA.md 5); flagged |
| audio-engine, worker-api, generator-api, agent tool catalogue | unchanged | - | |

## Complexity Tracking

| Deviation / tension | Why needed | Simpler alternative rejected because |
|---|---|---|
| TLS private key in a file (constitution: "secrets are never stored in files") | Kestrel needs the leaf key from a file or store; HTTPS is required for install and wake lock on the tablet | Env-var-only key: impractical PEM in an env var and still a secret on disk in the compose environment; HTTP only: no secure context. Mitigation: outside repo/image/DB, read-only mount, optional password from env, `.gitignore` patterns (ADR-027 D9) |
| Second listener in the web app | Separates the LAN surface physically from the Blazor app | Path filtering on one listener: one routing mistake exposes the app; separate container: duplicate hosting of the hub that must live next to the player |
| Content proxy in the web app | The remote must never reach the API | Exposing read-only API routes on the LAN: widens the API's exposure and needs auth in the API |
| New TS package | The remote is a separate app (no Blazor on the LAN) | Blazor page: F-04 |
| Default bind change to 127.0.0.1 touches every install | Closes an existing accidental exposure that would otherwise undermine the remote design | Leaving `0.0.0.0`: the whole app and API stay reachable on the LAN without authentication |

## Flags for the CTO / owner (network exposure)

1. **Opt-in exposure**: remote mode opens one port on the given LAN address only when configured
   and only serves the remote surface; the in-app switch is off by default.
2. **Exposure reduction**: base compose ports move from all interfaces to `127.0.0.1`
   (`STEMSTUDIO_BIND_ADDRESS` override); `AllowedHosts` restricted on web and API.
   *Done ahead of 018 (2026-11-23, refactors 003 and 004):* the base compose publishes 8080/8081 on
   `${STEMSTUDIO_BIND_ADDRESS:-127.0.0.1}` (checked by `scripts/check-loopback-ports.{sh,ps1}`, SV-05 for
   the base ports), the API has a host allow-list (`StemStudio__AllowedHosts`, default
   `localhost;127.0.0.1;[::1];api`; SV-04 for the API) and refuses Origin-less writes from peers other
   than loopback or the `web` service (shared `TrustedPeerPolicy`; this also closes item 4 for those
   requests). Still for 018: the web app's own host allow-list and the remote listener. No second
   listener may be added without these checks.
3. **`lan-http` exists** as a labelled, unencrypted fallback; the owner may prefer to remove it
   (then the tablet needs mkcert). Default in `docker-compose.lan.yml` is `lan-https`.
4. Pre-existing CSRF exposure of empty-body POST endpoints in the API (research F-03) is not fixed
   by 018; recommended follow-up.
