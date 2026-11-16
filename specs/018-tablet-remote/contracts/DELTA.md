# Contract delta: Tablet and Remote (feature 018)

Every change is **additive**. Base versions are those of feature 017 (written in parallel with
012-016); the full documents are produced in 018 Phase 0 (tasks.md T001) on the versions current
when 018 starts. Nothing existing is renamed, removed or changed in meaning, except the
**deployment defaults** in section 7 (bind address, allowed hosts), which are a security
hardening flagged to the CTO.

## 1. Versions and merge plan

| Contract | File(s) now | Phase 0 result |
|---|---|---|
| backend-api **1.17.0** | [backend-api.remote.openapi.yaml](backend-api.remote.openapi.yaml) `1.17.0-delta.1` (standalone, Redocly-valid) | `backend-api.openapi.yaml` 1.17.0 = 017's 1.16.0 + the `remote` tag, unchanged |
| ui-test-ids **1.17.0** | [ui-test-ids.delta.md](ui-test-ids.delta.md) | `ui-test-ids.md` 1.17.0 = 1.16.0 + delta |
| remote-protocol **1.0.0** (new) | [remote-protocol.md](remote-protocol.md), [remote-lan.openapi.yaml](remote-lan.openapi.yaml), [remote-messages.schema.json](remote-messages.schema.json), [remote-examples/](remote-examples/) | unchanged (final) |
| web-interop (next minor) | section 4 | merged into `web-interop.md` by the architect; number = next free minor at merge (not in the roadmap table) |
| help-content (next minor) | section 5 | area `remote` added to the area list |
| audio-engine, worker-api, generator-api, agent catalogue | - | unchanged |

Redocly: `npx @redocly/cli@2.78.0 lint --config specs/018-tablet-remote/contracts/redocly.yaml
<both files>` -> valid, 3 known warnings (GET settings and GET devices have no 4XX; the WebSocket
upgrade answers 101). JSON Schema: every file in `remote-examples/valid` validates and every file in
`remote-examples/invalid` fails (Draft 2020-12 with format checks), checked in T004 and by the
frontend's contract tests.

## 2. Backend API 1.17.0 (`/api/remote/*`, tag `remote`)

Operations: `getRemoteSettings`, `updateRemoteSettings`, `startRemotePairing`,
`getCurrentRemotePairing`, `cancelRemotePairing`, `allowRemotePairing`, `rejectRemotePairing`,
`redeemRemotePairingCode`, `pollRemotePairingRequest`, `listRemoteDevices`,
`removeAllRemoteDevices`, `renameRemoteDevice`, `removeRemoteDevice`, `authenticateRemoteDevice`,
`listRemoteAuditEvents` (15).

C# (architect, Phase 0, `src/StemStudio.Contracts/Remote`): `RemoteSettingsDto`,
`UpdateRemoteSettingsRequest`, `PairingStatusDto` (enum, kebab-case wire names), `PairingWindowDto`,
`PairingRequestSummaryDto`, `RedeemPairingRequest`, `PairingRequestTicketDto`,
`PollPairingRequest`, `PairingRequestOutcomeDto`, `RemoteTransportDto`, `RemoteDeviceDto`,
`RemoteDeviceListDto`, `RemovedDevicesDto`, `RenameRemoteDeviceRequest`,
`AuthenticateRemoteDeviceRequest`, `RemoteAuditKindDto`, `RemoteAuditEventDto`,
`RemoteAuditListDto`; `ApiRoutes.RemoteSettings`, `RemotePairings`, `RemotePairingsCurrent`,
`RemotePairing(id)`, `RemotePairingAllow(id)`, `RemotePairingReject(id)`, `RemotePairingRedeem`,
`RemotePairingPoll(requestId)`, `RemoteDevices`, `RemoteDevice(id)`, `RemoteDevicesAuthenticate`,
`RemoteAudit`. `IStemStudioApiClient` gains matching methods (frontend).

Problem codes (new): `remote-disabled`, `device-limit`, `pairing-not-requested`,
`pairing-code-invalid`, `pairing-rate-limited`, `device-name-invalid`, `remote-key-invalid`,
`origin-refused`, `pairing-not-found`, `device-not-found`.

Request rules for every non-GET `/api/remote/*` operation: JSON body required (415 otherwise),
foreign `Origin` -> 403 `origin-refused`.

## 3. Remote protocol 1.0.0

Listener rules R1-R8, pairing over HTTP, WebSocket session (subprotocol `stemstudio.remote.v1`),
24 commands, state snapshot, families, content endpoints, client behaviour. 014 command-id mapping
column of section 4 is completed in Phase 0 (T002) without changing remote command names.

## 4. web-interop (additive exports)

```ts
/** PWA install and service worker (main app). */
export function registerServiceWorker(url: string, scope: string): Promise<"registered" | "unsupported" | "insecure" | "failed">;
export function onInstallAvailable(dotnetRef: DotNetRef, method: string): Disposable; // calls method(true|false)
export function promptInstall(): Promise<"accepted" | "dismissed" | "unavailable">;
export function isStandalone(): boolean;
/** Screen wake lock (stand view). */
export function acquireWakeLock(dotnetRef: DotNetRef, method: string): Promise<"on" | "unsupported" | "refused">; // re-acquires on visibility; calls method(state) on release/re-acquire
export function releaseWakeLock(): Promise<void>;
/** Orientation for the stand layout. */
export function onOrientationChange(dotnetRef: DotNetRef, method: string): Disposable; // "landscape" | "portrait"
```

C# mapping: `IWebInterop` gains `RegisterServiceWorkerAsync`, `OnInstallAvailableAsync`,
`PromptInstallAsync`, `IsStandaloneAsync`, `AcquireWakeLockAsync`, `ReleaseWakeLockAsync`,
`OnOrientationChangeAsync` (frontend; architect adds the signatures in Phase 0 T005).

## 5. Help content

New area **`remote`** (help-content area list, next minor). Topics:

| id | area | route / anchors | levels |
|---|---|---|---|
| `remote-control` | remote | route `/remote-control` (screen topic); anchors `remote-*` | all |
| `remote-pairing` | remote | `remote-pair-*` | all |
| `remote-security` | remote | `remote-insecure-warning`, `remote-audit-log` | all |
| `remote-https-setup` | remote | `remote-certificate`, `remote-not-configured` | advanced, expert |
| `stand-view` | mixer | `stand-*` | all |
| `install-app` | start | `install-app`, `install-app-hint` | all |

Glossary additions (area `glossary`): `pwa` ("installable app"), `lan`, `pairing`. The remote client
links to the manual chapter instead of the in-app help (no help framework on the LAN surface).

## 6. UI test ids 1.17.0

See [ui-test-ids.delta.md](ui-test-ids.delta.md): shell (`install-app`, `remote-indicator`), mixer
Stand view (`stand-*`), Remote control page (`remote-*`), remote client (`rc-*`).

Feature catalogue entries (frontend, `FeatureCatalog`): `remote.page` (Advanced; Beginner when
remote mode is set up - evaluated by the page, not the catalogue), `remote.audit` (Expert),
`remote.rename` (Advanced), `remote.details` (Advanced: certificate, transport column),
`stand.text-size` (Advanced).

## 7. Configuration and deployment (devops, frontend)

- `docker-compose.yml`: `ports` of `api` and `web` become
  `"${STEMSTUDIO_BIND_ADDRESS:-127.0.0.1}:8081:8081"` and `"...:8080:8080"`; web env adds
  `StemStudio__Remote__Mode: off` (explicit) and `AllowedHosts: localhost;127.0.0.1;[::1]`; api env
  `AllowedHosts: localhost;127.0.0.1;[::1];api`. Header comment documents the change.
- New `docker-compose.lan.yml` (web only):

  ```yaml
  services:
    web:
      environment:
        StemStudio__Remote__Mode: ${STEMSTUDIO_REMOTE_MODE:-lan-https}
        StemStudio__Remote__Port: ${STEMSTUDIO_REMOTE_PORT:-8443}
        StemStudio__Remote__PublicHost: ${STEMSTUDIO_LAN_ADDRESS:?Set STEMSTUDIO_LAN_ADDRESS to this computer's LAN IP}
        StemStudio__Remote__AllowedHosts: ${STEMSTUDIO_LAN_ALIASES:-}
        STEMSTUDIO_LAN_CERT_PASSWORD:            # bare pass-through, no value in any file
      ports:
        - "${STEMSTUDIO_LAN_ADDRESS:?}:${STEMSTUDIO_REMOTE_PORT:-8443}:${STEMSTUDIO_REMOTE_PORT:-8443}"
      volumes:
        - ${STEMSTUDIO_LAN_CERT_DIR:-./certs}:/certs:ro
  ```

  (`lan-http` users set `STEMSTUDIO_REMOTE_MODE=lan-http`, `STEMSTUDIO_REMOTE_PORT=8090`; the empty
  cert folder is then not read.)
- `.gitignore`: `certs/`, `*.pem`, `*-key.pem`, `*.p12`, `*.pfx`.
- `scripts/lan-check.{ps1,sh}`: prints the computer's private addresses, checks that
  `STEMSTUDIO_LAN_ADDRESS` is one of them, that the certificate files exist and name that address,
  and that 8080/8081 are not reachable on the LAN address (connect test).
- `scripts/dev.{ps1,sh}` (non-Docker): main listener `http://localhost:8080`, remote options from the
  same variables.
- CI: build/test/coverage of `src/remote-client`; Redocly lint of both 018 OpenAPI files; schema
  example check; compose binding check (SV-05); E2E job with `docker-compose.lan.yml`
  (`STEMSTUDIO_LAN_ADDRESS=127.0.0.1`, `STEMSTUDIO_REMOTE_MODE=lan-http`) and one `lan-https` smoke
  with a throwaway CA generated by `openssl` in the job (never committed).

## 8. Assumed names from 012, 014, 016 (reconciled in Phase 0, T002)

| Need | Assumed source | If different |
|---|---|---|
| Sections (id, name, start, end) | 014 backend 1.13.0 `GET /api/tracks/{id}/sections` | adapt the web projector; remote shape unchanged |
| Setlists (active setlist, order, titles) | 014 backend 1.13.0 `/api/setlists/*` | same |
| Score map (asset -> pages with times) | 014 engine 2.4.0 / backend 1.13.0 score-follow data | page follow degrades to manual turning |
| Command catalogue (ids, handlers) | 014 web shortcut catalogue | mapping column in remote-protocol 4 |
| Tempo %, count-in | 012 engine 2.3.0 / mixer | `rejected unavailable` |
| Chord events, tablature/drum sheets | 016 backend 1.15.0 (chords; sheet assets with a family/kind) | content kind hidden |

## 9. Docs impact (integration wave, CTO)

User manual: new chapter "Tablet and remote" (stand view, install, pairing, remote use, HTTP vs
HTTPS, removing devices, troubleshooting: client isolation, IP changes, iOS limits); keyboard
shortcuts chapter cross-reference; FAQ entries. Technical manual: deployment (remote mode,
`docker-compose.lan.yml`, mkcert walkthrough incl. iOS/Android root install and the CA-key advice,
bind address change), security (threat model summary of ADR-027, network exposure section rewritten),
APIs (backend 1.17.0, remote-protocol 1.0.0), data model (three tables), operations
(`lan-check`). `SECURITY.md`: remote mode paragraph. `docs/THIRD-PARTY-LICENSES.md`: QRCoder,
pdfjs-dist; mkcert mentioned as an owner tool.
