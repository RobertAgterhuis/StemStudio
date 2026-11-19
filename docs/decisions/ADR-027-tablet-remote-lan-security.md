# ADR-027: Tablet and remote - LAN remote surface, pairing and transport security (feature 018)

- **Status**: Accepted (owner, 2026-11-19, via the Tipoca project site; proposed by the architect, 2026-11-16).
- **Context documents**: `specs/018-tablet-remote/` (spec, research R-01..R-16, data-model,
  contracts), constitution 1.3.1 (local, single-user; secrets; open source), MoSCoW C4 and W2.
- **Numbering**: ADR number = feature number + 9 (011 -> ADR-020), so 012-017 keep ADR-021..026.

## Context

StemStudio is a local, single-user tool. Until now every port is meant for `localhost` and there is
no authentication. Feature 018 lets a phone or tablet on the home network control playback on the
computer and show a music-stand view, and makes the app installable (PWA). That is the first
deliberate network exposure of StemStudio, so it needs an explicit design and threat model.

Findings that shape the design (research F-01..F-08): the compose file already publishes 8080/8081
on all interfaces; both apps accept any `Host`; the Blazor app uses global interactive rendering,
so any circuit can reach every page and the server side of a circuit has full API rights; the
audio engine runs in the computer's browser and streams stems from the API.

## Decisions

- **D1 Audio stays on the computer.** Remotes send commands and show content; they never receive
  stems or the app UI (R-01).
- **D2 A separate remote surface.** A static TypeScript client plus one WebSocket and a few HTTP
  endpoints (remote-protocol 1.0.0), served by the web app on a **separate listener** that exists
  only in remote mode. Requests are classified by the local socket port; a gate middleware in
  front of routing enforces a path allow-list (everything else 404, including `/_blazor`), and
  remote endpoints re-check the port with an endpoint filter (R-02, R-09).
- **D3 Exposure is opt-in twice and minimal by default.** (a) Deployment: `StemStudio__Remote__Mode`
  (`off` default) and `docker-compose.lan.yml`, which publishes only the remote port on the
  explicitly given `STEMSTUDIO_LAN_ADDRESS` (no default). (b) In the app: "Allow remote control",
  off by default, switchable only from the computer. The base compose file now publishes 8080/8081
  on `127.0.0.1` (`STEMSTUDIO_BIND_ADDRESS` override). The API is never on the LAN (R-03).
- **D4 Pairing with human approval.** One 120 s window, 6-digit single-use code, QR with the code in
  the URL fragment, 5 wrong codes stop the window, 20 attempts/min overall, identical answers for
  every failure, approval on the computer with a 3-digit check number shown on both screens
  (R-06).
- **D5 Device keys, not sessions or cookies.** 256-bit random key delivered once through a
  poll secret, stored as SHA-256, sent in the first WebSocket message and as a Bearer header,
  never in URLs or cookies; 30-day idle expiry; at most 8 devices and 4 connections; removal and
  switch-off disconnect within 1 s (R-07).
- **D6 Browser-side attack defences.** Exact `Origin` checks (WebSocket and POST), host allow-lists
  on all listeners and the API (DNS rebinding), strict CSP and security headers on the remote,
  JSON-only bodies and foreign-`Origin` refusal on the new API endpoints (R-08).
- **D7 Least privilege.** A fixed list of 24 practice commands (no save, delete, import, export,
  generate, agent, settings); content only for the song the computer has open; library search
  returns titles/artists/durations only.
- **D8 Transport: `lan-https` recommended, `lan-http` fallback.** HTTPS with an owner-supplied
  certificate (mkcert documented; any PEM pair works); no automatic fallback from HTTPS to HTTP; no
  HSTS. HTTP mode is labelled "Not encrypted" everywhere and loses install and wake lock on the
  device (R-04).
- **D9 The TLS key** is stored outside repository, image and database, mounted read-only, optional
  password from `STEMSTUDIO_LAN_CERT_PASSWORD` only (R-05; Complexity Tracking in plan.md).
- **D10 PWA = shell only.** Manifest and service worker on both surfaces; the service worker
  answers only navigations (network first, offline page) and the precached shell; it never touches
  `/_blazor`, `/_framework`, API origins, stems, assets or cross-origin requests (R-12).
- **D11 Never the internet.** No cloud relay, tunnel, UPnP, port forwarding or remote access
  service; start-up refuses public addresses for the remote surface.

## Threat model

**Assets**: A1 control of playback on the computer; A2 content of the open song (lyrics, chords,
sheets, artwork); A3 library titles (US6 search); A4 the full app (Blazor UI and API: delete,
import, generate, agent, files); A5 device keys and pairing codes; A6 the TLS key and the
owner's mkcert root key; A7 the computer itself.

**Adversaries**: T1 another device or person on the home Wi-Fi (guest, compromised IoT device);
T2 a passive Wi-Fi eavesdropper; T3 a malicious web page open in a browser on the computer or the
tablet; T4 an internet attacker (port forwarding, UPnP, laptop on a public network); T5 a lost or
stolen paired phone; T6 malicious content imported into StemStudio (lyrics, titles) rendered on
the remote.

| # | Threat (STRIDE) | Adversary | Mitigation | Residual risk |
|---|---|---|---|---|
| 1 | Reach the full app or API from the LAN (E) | T1, T4 | D2 separate listener + gate before routing, endpoint port filter; D3 API and main web on 127.0.0.1; security test list | none known; regression guarded by tests SV-01..SV-05 |
| 2 | Accidental exposure of 8080/8081 on all interfaces (I, E) | T1, T4 | D3 `127.0.0.1` default (pre-existing issue fixed) | owner overrides `STEMSTUDIO_BIND_ADDRESS` on purpose |
| 3 | Pair without permission by guessing the code (S) | T1 | D4: 6 digits, 5 tries, 120 s, 20/min, approval + check number | owner approves a wrong device despite the check number (user error) |
| 4 | Race the real device during pairing (S) | T1 | single-use code; check number on both screens | as 3 |
| 5 | Use the remote without pairing (S) | T1 | key required for WebSocket and content; 4401 | none |
| 6 | Steal the device key on the network (I) | T2 | `lan-https`; in `lan-http` labelled and accepted | `lan-http`: an eavesdropper can control playback and read the open song's content until the device is removed |
| 7 | Cross-site WebSocket hijacking / CSRF against the remote (T, E) | T3 | D5 no cookies; D6 exact Origin checks | none |
| 8 | CSRF against the new API endpoints from a page on the computer (T) | T3 | JSON-only bodies (preflight), foreign Origin 403, pairing codes unknown to the attacker | existing API endpoints keep their pre-018 exposure (flagged follow-up) |
| 9 | DNS rebinding against localhost or the remote listener (S, I) | T3 | D6 host allow-lists (`AllowedHosts`, remote gate) | none |
| 10 | XSS in the remote via song content or device names (T, I) | T6 | text-only rendering, strict CSP, no inline script, pdf.js without scripting/eval | a pdf.js vulnerability (dependency updates) |
| 11 | Flooding / resource exhaustion (D) | T1 | 4 KB messages, 20 cmd/s, 4 connections, bounded send queue, 20 pairing attempts/min, content size caps | a LAN device can still stop remote use by occupying connections (requires a key) |
| 12 | Lost or stolen phone (S) | T5 | remove device / remove all; 30-day idle expiry | access until removed (practice commands only) |
| 13 | Exposure on a public network (laptop travels) (E) | T4 | bind to one explicit address; host allow-list; switch; pairing; keys | same IP on the other network: protected by switch, pairing and keys |
| 14 | Internet exposure via router (E) | T4 | D11 no UPnP, docs warn against port forwarding, start-up refuses public addresses | owner configures port forwarding manually (documented as unsupported) |
| 15 | Stolen TLS leaf key (S, I) | local malware | D9 outside repo/image/DB, read-only mount, optional password | impersonation of the remote address on the LAN |
| 16 | Stolen mkcert root key (S, I) | local malware | manual: keep `CAROOT` private or delete the CA key after issuing; upgrade path below | MITM of any site on devices that trust the root (owner's responsibility, documented prominently) |
| 17 | Remote commands repudiation / unexplained changes (R) | T1, T5 | audit log for pairing and device events; the computer shows the remote indicator and the controlling device | individual commands are not logged (privacy, volume) |
| 18 | Stale or poisoned PWA cache serving old code (T) | - | D10 shell only, versioned caches, `skipWaiting`, network-first navigations | one reload |
| 19 | Information leak through logs (I) | - | no keys, codes, secrets or check numbers in logs or audit; path templates only | none |

## Consequences

- One new TypeScript package (`src/remote-client`), one new web-app area (`StemStudio.Web/Remote`),
  backend endpoints `/api/remote/*` (1.17.0), new contract remote-protocol 1.0.0, two new
  dependencies (QRCoder MIT, pdfjs-dist Apache-2.0).
- The default compose binding changes to `127.0.0.1`. Anyone who opened StemStudio from another
  device before must use remote mode instead (release note).
- Docker Desktop host-IP publishing and firewall behaviour must be verified on the reference
  machine before the wave (tasks.md T003).
- The constitution's "no secrets in files" rule gets an interpretation for the TLS key (plan.md
  Complexity Tracking); no amendment is proposed.

## Alternatives considered

Blazor on the LAN listener; SignalR; hub in the API; tablet as a full client with stems; cookies or
sessions; self-signed certificates with click-through; a CA built into StemStudio; public CA with
DNS-01; tunnels or cloud relays. Reasons in research R-01, R-02, R-04, R-07.

## Upgrade path (not in 018)

A one-shot, name-constrained local CA generated by StemStudio (the CA key is discarded after
signing the leaf for the LAN address) would remove the powerful mkcert root key from the owner's
machine. It needs verified name-constraint support on iOS, Android Chrome and desktop browsers and
its own ADR.
