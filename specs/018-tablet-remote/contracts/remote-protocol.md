# Contract: remote protocol (v1.0.0)

Normative contract between the **remote client** (`src/remote-client`, browser on a phone or
tablet) and the **remote surface** of the web app (`StemStudio.Web/Remote`). Owner: frontend
(both sides). Parts:

- this document (normative rules, command catalogue, behaviour);
- [remote-lan.openapi.yaml](remote-lan.openapi.yaml) (OpenAPI 3.1, the HTTP endpoints of the
  remote listener);
- [remote-messages.schema.json](remote-messages.schema.json) (JSON Schema 2020-12, every
  WebSocket message);
- [remote-examples/](remote-examples/) (example messages; each must validate against the schema,
  tested by the frontend's contract tests and by `T004`).

Security rationale: [ADR-027](../../../docs/decisions/ADR-027-tablet-remote-lan-security.md).
Versioning: semantic; additive messages, fields or commands -> minor; a server MUST ignore
unknown fields; a client MUST ignore unknown fields and unknown `type`s; a removed or changed
field -> major + ADR.

## 1. Listener and surface

The remote listener exists only when `StemStudio__Remote__Mode` is `lan-http` or `lan-https`
(data-model 1.1). Origin = `<scheme>://<PublicHost>:<Port>`.

| Path | Method | Auth | Served when switch off |
|---|---|---|---|
| `/remote/`, `/remote/index.html` | GET | none | yes |
| `/remote/assets/*` (hashed JS/CSS, pdf.js worker), `/remote/icons/*` | GET | none | yes |
| `/remote/manifest.webmanifest`, `/remote/sw.js`, `/remote/offline.html` | GET | none | yes |
| `/css/tokens.css`, `/fonts/archivo/*` (shared design tokens/fonts) | GET | none | yes |
| `/remote/v1/info` | GET | none | yes |
| `/remote/v1/pairings/redeem` | POST | code | no (403 `remote-disabled`) |
| `/remote/v1/pairings/requests/{requestId}` | GET | poll secret | no |
| `/remote/v1/ws` | GET (upgrade) | device key (first message) | no |
| `/remote/v1/content/*` | GET | Bearer device key | no |

Rules (binding, tested):

- **R1** Any other path or method on the remote listener -> `404` with an empty body, decided
  **before routing** (no Blazor, no `/_blazor`, no `/_framework`, no API path, no `/health`).
- **R2** The main listener returns `404` for `/remote/v1/*`.
- **R3** `Host` must equal `PublicHost[:Port]` or an alias (case-insensitive) -> else `400`.
- **R4** `Origin` must equal the remote origin exactly on the WebSocket upgrade and on every
  `POST` -> else `403` `origin-refused`. A missing `Origin` on these requests is refused too.
- **R5** Every response carries:
  `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self';
  img-src 'self' blob: data:; connect-src 'self'; worker-src 'self'; font-src 'self';
  object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'`,
  `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`,
  `Permissions-Policy: camera=(), microphone=(), geolocation=(), screen-wake-lock=(self)`,
  `Cross-Origin-Opener-Policy: same-origin`, `Cross-Origin-Resource-Policy: same-origin`.
  Non-static responses also `Cache-Control: no-store`.
- **R6** Requests and messages never carry the device key, pairing code or poll secret in a URL;
  the server never logs them (request logging on the remote listener logs method, path template,
  status and duration only).
- **R7** When the peer address is visible (non-Docker path) and not private/loopback/link-local,
  the connection is refused (`403`). In a container this check is skipped (proxy address).
- **R8** All JSON is UTF-8, camelCase, enums lower-case kebab-case; times in seconds (float) for
  media positions, milliseconds (integer) for `serverTime`.

## 2. Pairing (HTTP, details in remote-lan.openapi.yaml)

1. The device opens `/remote/` (from the QR: `/remote/#pair=123456`). The client reads the code
   from the fragment, removes the fragment from the address bar (`history.replaceState`), and
   calls `GET /remote/v1/info`.
2. `POST /remote/v1/pairings/redeem` `{code, deviceName}` -> `202 {requestId, pollSecret,
   checkNumber, expiresAt}` or `404 pairing-code-invalid` / `403 remote-disabled` /
   `429 pairing-rate-limited` / `422 device-name-invalid`.
3. The client shows the check number and polls `GET /remote/v1/pairings/requests/{requestId}`
   with header `X-Pairing-Secret: <pollSecret>` every 1 s -> `{status: waiting|allowed|rejected|
   expired}`; with `allowed` the response also carries `{deviceId, deviceName, key}` **once**;
   later polls return `404`.
4. The client stores `{deviceId, deviceName, key, origin, transport}` (data-model 8) and connects
   the WebSocket.

The web app forwards steps 2-3 to the API (`/api/remote/pairings/redeem`,
`/api/remote/pairings/requests/{id}`) and adds `transport` from the listener mode; it never stores
the key.

## 3. WebSocket session

- URL `/remote/v1/ws` (`wss://` in `lan-https`). Subprotocol **`stemstudio.remote.v1`** is
  required (`Sec-WebSocket-Protocol`); missing -> handshake refused (`400`).
- Text frames only, one JSON message per frame, <= 4096 bytes UTF-8 (server -> client snapshots
  may be up to 8192 bytes).
- Every message has `"type"`; client messages that need an answer carry `"id"` (1-36 chars,
  `^[A-Za-z0-9_-]+$`), echoed in the reply.

### 3.1 Handshake

```text
client                                  server
  |-- hello {key, client:{name,version}} -->|  (within 5 s of open, else close 4408)
  |                                          |  verify key (API, cache 60 s)
  |<-- welcome {deviceId, deviceName, protocol:"1.0.0", transport, limits, commands[]} --|
  |<-- state {...}                           |  immediately, then on every change
```

Failure: unknown/expired key -> close `4401`; switch off -> `4403`; more than 4 connections ->
`4409`; a second connection of the same device replaces the first (`bye {reason: replaced}` then
close `4000` on the old one).

### 3.2 Messages

| Direction | `type` | Fields | Notes |
|---|---|---|---|
| C -> S | `hello` | `key`, `client {name, version}` | first message only |
| C -> S | `command` | `id`, `name`, `args?` | section 4 |
| C -> S | `ping` | `id` | server answers `pong {id, serverTime}` (clock offset estimate) |
| S -> C | `welcome` | `deviceId`, `deviceName`, `protocol`, `transport`, `limits {maxMessageBytes, commandsPerSecond}`, `commands` (names this server accepts) | |
| S -> C | `state` | `seq`, `serverTime`, `player`, `remotes`, `mode`, `secure` | data-model 7; full snapshot |
| S -> C | `ack` | `id`, `result` (`ok` \| `rejected`), `reason?` (`invalid-args`, `unknown-command`, `no-player`, `unavailable`, `busy`, `waiting-for-computer`), `message?` | one per command, after dispatch |
| S -> C | `pong` | `id`, `serverTime` | |
| S -> C | `searchResult` | `id`, `items` | answer to `library.search` (section 4) |
| S -> C | `bye` | `reason` (`revoked`, `disabled`, `replaced`, `server-stopping`, `expired`) | followed by close |

### 3.3 Close codes

| Code | Meaning | Client behaviour |
|---|---|---|
| 1000 / 1001 | normal / server going away | reconnect with backoff |
| 4000 | replaced by a newer connection of this device | do not reconnect from this tab |
| 4400 | malformed message, schema violation, binary frame, oversized | reconnect with backoff |
| 4401 | not authenticated (unknown, removed or expired key) | delete stored key, show "Pair again" |
| 4403 | remote control is off on the computer | show "off", retry every 10 s while visible |
| 4408 | no `hello` within 5 s | reconnect |
| 4409 | too many remotes | show "Too many remotes", retry every 10 s |
| 4410 | device removed while connected (after `bye revoked`) | delete stored key, show "Removed on the computer" |
| 4429 | rate limit or send queue overflow | reconnect after 2 s |

Backoff: 1, 2, 4, 8, 10, 10, ... seconds, reset after a `welcome`. Commands are never queued or
replayed across connections.

## 4. Command catalogue (1.0.0)

The server validates `args` against the schema (`$defs/command`), then against the live state
(ids exist), then dispatches to the active player (or navigator for `track.open`). Numbers outside
the range -> `rejected invalid-args`. Commands needing a feature that is not delivered or not
available for the track -> `rejected unavailable`.

| Name | Args | Effect on the main device | 014 command id (Phase 0 reconciles) |
|---|---|---|---|
| `transport.play` | - | play from the current position (count-in if enabled) | `transport.play` |
| `transport.pause` | - | pause | `transport.pause` |
| `transport.toggle` | - | play/pause | `transport.toggle` |
| `transport.stop` | - | stop and return to loop start or 0 | `transport.stop` |
| `transport.seek` | `positionSec` 0..duration | seek | - |
| `transport.skip` | `deltaSec` -30..30, not 0 | relative seek | `transport.back` / `transport.forward` |
| `section.previous` / `section.next` | - | jump to the previous/next section start | `section.previous` / `section.next` |
| `section.goto` | `sectionId` | jump to that section | - |
| `section.restart` | - | jump to the start of the current section | `section.restart` |
| `loop.toggle` | - | loop on/off with the current loop range | `loop.toggle` |
| `loop.section` | `sectionId` | loop that section (014 one-click loop) | `loop.section` |
| `tempo.set` | `percent` 50..120, integer | playback speed (012) | - |
| `tempo.nudge` | `deltaPercent` -10..10, not 0 | relative speed, clamped 50..120 | `tempo.slower` / `tempo.faster` |
| `metronome.toggle` | - | metronome on/off | `metronome.toggle` |
| `countIn.toggle` | - | count-in on/off (012) | `countin.toggle` |
| `stem.mute` | `stemId`, `muted` bool | mute/unmute one stem | - |
| `stem.solo` | `stemId`, `soloed` bool | solo/unsolo one stem | - |
| `family.practise` | `family` (section 6) or `null` | mute exactly that family's stems; `null` restores the mute states saved when it started | - |
| `master.volume` | `db` -60..6, step 0.5 | master fader | - |
| `setlist.previous` / `setlist.next` | - | open the previous/next setlist song | `setlist.previous` / `setlist.next` |
| `track.open` | `trackId` | the computer opens that track's mixer (asks first with unsaved changes -> `rejected waiting-for-computer` until answered, then a new `state`) | - |
| `library.search` | `query` 0..60 chars | answered with `searchResult` (below) | - |

`library.search` answer (S -> C): `searchResult {id, items: [{trackId, title, artist,
durationSec}] (<= 25)}`; only completed tracks; no file names or lyrics.

Commands that are deliberately **absent**: save, delete, import, export, transcribe, generate,
agent, settings, pairing, anything that reads files.

## 5. State snapshot

Exact schema: `$defs/state` in the JSON schema; semantics in data-model 7. Rules:

- Sent on every change (coalesced to <= 10/s), at least every 2 s while `playing`, and right after
  `welcome`. `seq` increases by 1 per snapshot within a server run.
- `positionSec` is the engine position at `serverTime`. The client's display position =
  `positionSec + rate * (nowServer - serverTime) / 1000` while playing, where `nowServer` is the
  client's estimate from `pong` offsets (median of the last 5). The display never runs past the
  loop end or duration (it wraps to the loop start while looping).
- `player: null` = no mixer open on the computer (client shows "No song open on the computer").
- `content.revision` changes when the track or its content changes; the client then refetches
  content.

## 6. Instrument families

| Stem name (001/002/detail) | Family |
|---|---|
| `vocals`, detail `lead-vocals`, `backing-vocals` | `vocals` |
| `guitar`, detail children of guitar | `guitar` |
| `bass` | `bass` |
| `piano`, `keys`, detail children of piano | `keys` |
| `drums`, detail `kick`, `snare`, `toms`, `hihat`, `cymbals` | `drums` |
| `other` and any unmapped stem | `other` |

Order in every list: vocals, guitar, bass, keys, drums, other (equal prominence). The frontend's
mapping function is the single source (`RemoteFamilies`), unit-tested against every stem name in
the backend's catalogue.

## 7. Content (HTTP, `Authorization: Bearer <key>`)

All endpoints answer only for the **active player's track** (FR-030); otherwise `404`
`content-not-available`. Shapes in remote-lan.openapi.yaml.

| Endpoint | Content |
|---|---|
| `GET /remote/v1/content/lyrics` | `{revision, synced, lines: [{index, startSec?, endSec?, text}]}` (006) |
| `GET /remote/v1/content/chords` | `{revision, events: [{startSec, endSec, symbol}]}` (016) |
| `GET /remote/v1/content/sheets/{assetId}` | `application/pdf` bytes of a sheet asset of that track (008/016), max 20 MB |
| `GET /remote/v1/content/sheets/{assetId}/map` | `{revision, pages: [{page, startSec, endSec}]}` from 014's score map; `404` when none |
| `GET /remote/v1/content/artwork` | `image/webp` or `image/jpeg` thumbnail (005), max 512 px |

## 8. Client behaviour (binding for `src/remote-client`)

- Tabs: **Transport**, **Stand**, **Mix**, **Songs** (setlist/library). Transport buttons >= 64 px.
- Connection state in a polite live region (`connecting`, `connected`, `reconnecting`, `off`,
  `removed`, `no-player`); command rejections in an assertive live region.
- `lan-http` (`secure: false`): show the "Not encrypted" badge; do not register the service worker
  or offer install; wake-lock toggle shows "unavailable without HTTPS".
- Text from the server is set with `textContent` only; no `innerHTML`, no `eval`, no inline
  handlers. pdf.js: `isEvalSupported: false`, `enableScripting: false`.
- Wake lock: requested while the Stand tab is visible and `keepAwake` is on; re-requested on
  `visibilitychange`.

## 9. Changelog

- **1.0.0** (feature 018): listener surface and rules R1-R8, pairing, WebSocket session, 24
  commands, state snapshot, families, content endpoints, client rules.
