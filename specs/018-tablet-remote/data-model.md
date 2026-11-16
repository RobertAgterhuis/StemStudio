# Data Model: Tablet and Remote (feature 018)

Binding for backend (sections 2-4), frontend (sections 1, 5-7) and devops (section 1). Wire
shapes: [contracts/backend-api.remote.openapi.yaml](contracts/backend-api.remote.openapi.yaml)
(1.17.0), [contracts/remote-protocol.md](contracts/remote-protocol.md) (1.0.0).

## 1. Configuration (environment, never stored)

### 1.1 Web app (`StemStudio:Remote`, class `RemoteOptions` in `StemStudio.Web/Remote`)

| Key (env form) | Type / values | Default | Rule |
|---|---|---|---|
| `StemStudio__Remote__Mode` | `off` \| `lan-http` \| `lan-https` | `off` | anything else -> start-up error (whole app: misconfiguration is not silently ignored) |
| `StemStudio__Remote__Port` | 1024-65535 | 8443 (`lan-https`), 8090 (`lan-http`) | must differ from the main port |
| `StemStudio__Remote__ListenAddress` | IP literal | `0.0.0.0` in the container, required on the non-Docker path | must be private/link-local/loopback or `0.0.0.0` inside a container (`DOTNET_RUNNING_IN_CONTAINER=true`) |
| `StemStudio__Remote__PublicHost` | IP literal or host name | none (required when mode != off) | IP must be private (RFC 1918 / RFC 4193 / RFC 3927) or loopback; names must end in `.local`, `.lan`, `.home.arpa`, `.internal` or be a single label |
| `StemStudio__Remote__AllowedHosts` | comma list, <= 5 | empty | same rules as `PublicHost` |
| `StemStudio__Remote__CertificatePath` | file path | `/certs/stemstudio-lan.pem` | `lan-https` only |
| `StemStudio__Remote__CertificateKeyPath` | file path | `/certs/stemstudio-lan-key.pem` | `lan-https` only |
| `StemStudio__Remote__CertificatePassword` | string, env only | unset | from `STEMSTUDIO_LAN_CERT_PASSWORD`; never logged; set -> encrypted PEM key |
| `StemStudio__MainPort` | 1024-65535 | 8080 (or `ASPNETCORE_HTTP_PORTS`) | main listener |
| `AllowedHosts` (main listener) | `;` list | `localhost;127.0.0.1;[::1]` | replaces `*` |

Derived, read-only `RemoteDeployment` (exposed to the Remote control page): `mode`, `origin`
(`https://<PublicHost>:<Port>` or `http://...`), `available` (listener started), `problem`
(null or one of `certificate-missing`, `certificate-invalid`, `certificate-expired`,
`certificate-host-mismatch`, `address-not-private`, `port-in-use`), `certificateExpiresAt`.

### 1.2 API

| Key | Default | Rule |
|---|---|---|
| `AllowedHosts` | `localhost;127.0.0.1;[::1];api` | replaces `*` (FR-014) |

### 1.3 Compose (devops)

| Variable | File | Default | Meaning |
|---|---|---|---|
| `STEMSTUDIO_BIND_ADDRESS` | `docker-compose.yml` | `127.0.0.1` | host address for 8080/8081 |
| `STEMSTUDIO_LAN_ADDRESS` | `docker-compose.lan.yml` | **required** | the computer's LAN IPv4/IPv6 address; also `PublicHost` |
| `STEMSTUDIO_REMOTE_MODE` | `docker-compose.lan.yml` | `lan-https` | |
| `STEMSTUDIO_REMOTE_PORT` | `docker-compose.lan.yml` | `8443` | host and container port |
| `STEMSTUDIO_LAN_CERT_DIR` | `docker-compose.lan.yml` | `./certs` (git-ignored) | mounted read-only at `/certs` |
| `STEMSTUDIO_LAN_CERT_PASSWORD` | `docker-compose.lan.yml` | unset | bare pass-through (`- STEMSTUDIO_LAN_CERT_PASSWORD`) |
| `STEMSTUDIO_LAN_ALIASES` | `docker-compose.lan.yml` | empty | extra host names |

## 2. Remote settings (API, SQLite, one row `RemoteSettings`)

| Field | Type | Rule |
|---|---|---|
| `Id` | int | always 1 |
| `Enabled` | bool | default false |
| `UpdatedAt` | DateTime (UTC) | set on change |

Missing row = defaults. Changing `Enabled` writes an audit event `switch-changed`.

## 3. Paired device (API, SQLite `RemoteDevices`)

| Field | Type | Rule |
|---|---|---|
| `Id` | Guid | v7 |
| `Name` | string | 1-40 chars after trimming, Unicode categories L, M, N, P, Zs, S only (no control or format characters); unique case-insensitive is **not** required |
| `KeyHash` | byte[32] | SHA-256 of the 32-byte key; unique index |
| `Transport` | `http` \| `https` | mode at pairing time |
| `PairedAt` | DateTime UTC | |
| `LastUsedAt` | DateTime UTC | updated on authenticate, at most once per minute per device |

Invariants: at most 8 rows (9th approval -> 409 `device-limit`). A device whose `LastUsedAt` is
older than 30 days is deleted by the authenticate path and by a daily sweep (audit
`device-expired`). Removal deletes the row (audit `device-removed`); "remove all" deletes every
row (one audit event `all-removed` with the count).

## 4. Pairing window (API memory, singleton `PairingRegistry`, `TimeProvider`-driven)

```text
PairingWindow
  Id            Guid
  Code          6 digits (string), RandomNumberGenerator.GetInt32(0, 1_000_000), zero-padded
  CreatedAt     UTC
  ExpiresAt     CreatedAt + 120 s
  WrongCodes    0..5
  State         open | requested | allowed | rejected | expired | stopped | cancelled | delivered
  Request?      PairingRequest
PairingRequest
  Id            Guid
  PollSecretHash byte[32]   (SHA-256 of a 16-byte secret returned once by redeem)
  DeviceName    validated as 3.Name
  Transport     http | https (told by the web app)
  CheckNumber   3 digits = (first 4 bytes of SHA-256(Request.Id bytes), big-endian uint) mod 1000, zero-padded
  RedeemedAt    UTC
  IssuedKey?    32 bytes, kept until delivered once or 60 s after Allow, then wiped
  DeviceId?     set on Allow
```

Rules:

- At most one window; `POST /api/remote/pairings` cancels an open one (`cancelled`).
- Global attempt limiter: 20 redeem calls per rolling minute (token bucket), independent of
  windows; over the limit -> 429 `pairing-rate-limited`.
- Redeem with a wrong code -> `WrongCodes++`; at 5 -> `stopped` (audit `pairing-stopped`). Unknown,
  expired, used and wrong codes all return 404 `pairing-code-invalid` (no oracle).
- Redeem with the right code while `open` -> `requested` (code is consumed). A second redeem ->
  404 `pairing-code-invalid`.
- Allow (only `requested`) -> creates the device row (3), state `allowed`, `IssuedKey` generated.
- First successful poll with the right poll secret in state `allowed` -> returns the key once,
  state `delivered`, key wiped.
- Expiry: any state other than `delivered`/`allowed` becomes `expired` at `ExpiresAt`; an
  `allowed` window whose key was not collected within 60 s deletes the device row again and
  becomes `expired`.

State machine:

```text
open --redeem ok--> requested --allow--> allowed --poll--> delivered
open --5 wrong--> stopped          requested --reject--> rejected
open|requested --timeout--> expired allowed --60 s no poll--> expired (device row removed)
open|requested --new window/cancel--> cancelled
```

## 5. Remote audit event (API, SQLite `RemoteAuditEvents`)

| Field | Type | Rule |
|---|---|---|
| `Id` | long | identity |
| `At` | DateTime UTC | |
| `Kind` | enum | `switch-changed`, `pairing-started`, `pairing-requested`, `pairing-allowed`, `pairing-rejected`, `pairing-expired`, `pairing-stopped`, `pairing-cancelled`, `device-renamed`, `device-removed`, `device-expired`, `all-removed`, `auth-refused` |
| `DeviceId` | Guid? | |
| `DeviceName` | string? | <= 40 |
| `Detail` | string? | <= 200, from a fixed template per kind (never user free text except the device name, never codes, keys, secrets or check numbers) |

Retention: newest 500 rows (older rows deleted on insert). `auth-refused` is coalesced: at most one
row per minute with a count in `Detail`.

## 6. Web app runtime state (memory, `StemStudio.Web/Remote`)

```text
RemoteHub (singleton)
  Players       Dictionary<PlayerId, PlayerRegistration>
  ActivePlayer  PlayerId?           (rule: last play or focus; ties -> latest registration)
  Navigators    Dictionary<NavigatorId, INavigatorBridge>   (one per open StemStudio tab, US6)
  Connections   Dictionary<ConnectionId, RemoteConnection>  (<= 4)
  Snapshot      RemoteState (last published, seq)
PlayerRegistration
  PlayerId      Guid (per mixer component instance)
  TrackId       Guid
  Bridge        IRemotePlayerBridge (InvokeAsync into the component)
  LastActiveAt  UTC
RemoteConnection
  ConnectionId  Guid
  DeviceId      Guid, DeviceName
  Transport     http | https
  ConnectedAt   UTC
  Limiter       token bucket 20/s, burst 40
  Socket        WebSocket (send queue bounded to 16 messages; overflow -> close 4429)
DeviceAuthCache  key hash -> (DeviceId, Name, Transport, CheckedAt), 60 s
```

Removal or switch-off on the Remote control page calls the API first, then
`RemoteHub.Disconnect(deviceId | all, reason)`; the auth cache entry is evicted.

## 7. Remote state snapshot (`RemoteState`, wire shape in remote-protocol.md 5)

```text
seq, serverTime (ms since hub start, monotonic), mode, secure
player: null | {
  trackId, title, artist, durationSec, artworkAvailable,
  playing, positionSec (at serverTime), rate (0.5..1.2),
  loop {enabled, startSec, endSec, sectionId?},
  metronome bool, countIn bool,
  section {id, name, index} | null, sections [{id, name, startSec, endSec}] (<= 64),
  stems [{id, name, family, muted, soloed}] (<= 32), practisedFamily | null,
  masterDb (-60..+6),
  setlist {id, name, index, count, previousTitle?, nextTitle?} | null,
  content {lyrics bool, chords bool, sheets [{assetId, family, kind (score|tab|drums), title, pages?}], scoreMap bool, revision}
}
remotes: count connected
```

Families: `vocals`, `guitar`, `bass`, `keys`, `drums`, `other` (from the stem name; detail stems
inherit their parent's family; mapping table in remote-protocol.md 6). Ordering is fixed and equal
for all families (MoSCoW principle).

## 8. Browser-side preferences

| Store | Key | Content |
|---|---|---|
| main app `localStorage` (004 preferences record, additive fields) | `stemstudio.preferences` | `stand: {family, contentKind, textScale (100-200), keepAwake (bool)}` |
| remote `localStorage` | `stemstudio.remote` | `{deviceId, deviceName, key, origin, transport, prefs: {tab, family, contentKind, textScale, keepAwake, theme}}`; cleared on `revoked` |

Blocked storage -> the remote keeps the key in memory only and says "You will need to pair again
after closing the browser".
