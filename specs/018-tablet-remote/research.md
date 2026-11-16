# Research: Tablet and Remote (feature 018)

Architect, 2026-11-16. Decisions are binding for the wave unless the CTO reverses them; the
security design is recorded in [ADR-027](../../docs/decisions/ADR-027-tablet-remote-lan-security.md).

## 1. Findings on the current system (measured from the repository at `041ca70`)

| # | Finding | Consequence |
|---|---|---|
| F-01 | `docker-compose.yml` publishes `8080:8080` (web) and `8081:8081` (API) without a host address, so Docker publishes them on **all host interfaces** (`0.0.0.0`). Whether other devices can reach them depends on the Windows firewall rule Docker Desktop installed. The API has no authentication. | Pre-existing accidental-exposure risk. 018 binds both to `127.0.0.1` by default (FR-011) and adds host filtering (FR-014). Flagged to the CTO. |
| F-02 | Both apps run with `"AllowedHosts": "*"`. | DNS rebinding: a web page whose domain is re-pointed to `127.0.0.1` can talk to `localhost:8081` as same-origin. 018 restricts host names (FR-014). Flagged. |
| F-03 | The API's CORS policy allows only `GET` from `WebOrigin`; state-changing endpoints rely on JSON bodies forcing a preflight. Endpoints with an empty body (for example `.../cancel`) are reachable by "simple" cross-site POSTs. | The new `/api/remote/*` endpoints require `application/json` bodies and reject a foreign `Origin` (R-08). The existing endpoints are out of scope; flagged as a follow-up. |
| F-04 | The Blazor app uses **global** Interactive Server rendering (`<Routes @rendermode=ServerNoPrerender>`). A circuit can navigate to every page, and the server side of a circuit calls the API with full rights. | The Blazor app must never be reachable from the LAN. The remote is a separate static client (R-02). |
| F-05 | The audio engine runs in the browser of the main device; the browser fetches stems from `PublicApiBaseUrl` (`http://localhost:8081`). | Audio can only play on the main device without exposing the API (R-01). |
| F-06 | 004 delivered responsive layouts down to 320 px, design tokens (`--ss-target-min: 1.5rem`), journey levels, the help framework and `web-interop` 1.0.0. There is no manifest, service worker or wake-lock code. | Stand view and remote reuse the tokens; PWA and wake lock are new (R-12, R-13). |
| F-07 | Sheet music (008) is delivered as PDF/MusicXML/MIDI assets; the mixer previews the PDF in an `<iframe>`. Chrome on Android does not render PDFs inline. | The remote renders PDFs with pdf.js (R-14). |
| F-08 | Keyboard shortcuts go through `PageCommands`/`KeyboardShortcuts`; 014 adds a command catalogue for shortcuts and foot pedals. | Remote commands map 1:1 onto that catalogue (R-10). |

## 2. Decisions

### R-01 Where the audio plays: always on the main device

- **Decision**: the computer running StemStudio plays the audio, in its browser tab on
  `localhost`. Phones and tablets are remotes (commands + stand view). They never receive stems.
- **Why**: stems are served by the API, which would then have to be reachable from the LAN with
  some authentication; the full UI would follow. That is a much larger attack surface and is
  close to multi-user hosting (W2). The practice setting (computer with speakers or an audio
  interface, tablet on the stand) fits the remote model.
- **Rejected**: tablet as a full client (API and Blazor on the LAN); "audio follows the device"
  hand-off (needs stems on the tablet and engine state transfer).

### R-02 Remote surface technology: separate static client + plain WebSocket

- **Decision**: a new TypeScript package `src/remote-client/` (no framework, Vite build to
  `src/StemStudio.Web/wwwroot/remote/`), served by the web app on a **separate listener**. It talks
  JSON over one WebSocket (`/remote/v1/ws`) plus a few HTTP endpoints (pairing, content). The
  server side lives in `StemStudio.Web` (`Remote/` folder), because the player (the mixer
  component) lives in the web app's Blazor circuit.
- **Why**: F-04 rules out Blazor on the LAN. A raw WebSocket (ASP.NET Core built-in
  `UseWebSockets`) needs no client library and keeps the protocol small, versioned and testable
  (remote-protocol 1.0.0).
- **Rejected**: Blazor page on the LAN listener (global interactivity, server-side rights); SignalR
  hub (`@microsoft/signalr`, MIT, works, but negotiate/transport fallbacks add surface and a
  dependency for no gain on a LAN); hub inside the API (the API does not host the player; it would
  need a second channel to the web app).

### R-03 Network exposure: bind to one address, opt-in override

- Docker publishes `host:container` ports on all interfaces unless a host IP is given
  (`127.0.0.1:8080:8080`). Docker Desktop for Windows supports host-IP-specific publishing;
  **Phase 0 verifies** on the reference machine that `192.168.x.y:8443:8443` is reachable from a
  second device and `127.0.0.1:8080:8080` is not (task T003). Fallback if not: the non-Docker path
  only, documented.
- **Decision**: base compose publishes `${STEMSTUDIO_BIND_ADDRESS:-127.0.0.1}` for 8080/8081.
  New `docker-compose.lan.yml` adds only the remote port on `${STEMSTUDIO_LAN_ADDRESS:?}` (required,
  no default, so `0.0.0.0` is never chosen by accident) and sets the remote options of the web app.
  The non-Docker path binds the main listener to `localhost` and the remote listener to
  `StemStudio__Remote__ListenAddress`.
- Windows firewall: Docker Desktop asks once; the manual says to allow **Private networks only**.
- Inside the container the source address of a connection is the Docker proxy, not the device, so
  an "is the client on a private network" check is only meaningful on the non-Docker path. It is
  kept as defence in depth (FR-012 checks the configured addresses at start-up; the listener
  refuses non-private peers when it can see them).
- The playwright container uses `--network host`; on Docker Desktop host networking reaches the
  VM's loopback. If a `127.0.0.1` binding breaks local E2E runs, `STEMSTUDIO_BIND_ADDRESS=0.0.0.0`
  is the documented test-only override (CI on Linux is unaffected). Verified in T003.

### R-04 Secure context and TLS on a LAN

Browser features needed and their requirement:

| Feature | Needs secure context | Notes |
|---|---|---|
| Service worker (offline shell, install on Android/desktop Chrome) | yes | Chrome does not register service workers on pages with certificate errors, even after clicking through. |
| Install (Chrome/Edge) | yes | manifest + icons + secure origin |
| Add to Home Screen as web app (iOS Safari 16.4+) | manifest is honoured on any origin, service worker only on secure origins | |
| Screen Wake Lock | yes | Chrome 84+, Safari 16.4+ (fixed for home-screen apps in iOS 18.4), Firefox 126+ |
| WebSocket | no (`ws://` allowed) | `wss://` from https pages is required (mixed content) |

`http://localhost` and `http://127.0.0.1` are potentially trustworthy origins, so the main app on
the computer gets all features without TLS.

| Option for the tablet | Secure context | Owner effort | Risk | Verdict |
|---|---|---|---|---|
| A. Plain HTTP on the LAN | no | none | traffic and device key readable on the Wi-Fi; no install, no wake lock | **kept as `lan-http` fallback**, clearly labelled, control + stand view only |
| B. mkcert (BSD-3-Clause), run by the owner: local CA, leaf cert for the LAN IP/host name, root installed on the tablet | yes | ~10 min once per device (iOS: install profile + enable full trust; Android: install CA certificate, Chrome trusts user CAs) | mkcert's root key on the owner's PC can sign certificates for any site trusted by the devices that installed the root | **chosen as `lan-https`**; manual tells the owner to keep `CAROOT` private and offers the "issue, then delete the CA key" variant |
| C. Self-signed cert generated by StemStudio, click through the warning | partially: no service worker | low | trains users to ignore warnings; no install/offline | rejected |
| D. CA built into StemStudio (one-shot, name-constrained root for the LAN IP, CA key discarded after signing) | yes | same as B | safer root (constrained, no key kept), but PKI code to own and verify; name-constraint support on all target platforms unverified | rejected for 018; recorded as the upgrade path in ADR-027 |
| E. Public CA (Let's Encrypt DNS-01 for a real domain pointing to a private IP) | yes | needs a domain, DNS API credentials, internet renewals | secret on disk, internet dependency | rejected (W2, secrets) |
| F. Tunnel/relay (Tailscale, ngrok, cloud) | yes | account | internet/cloud | rejected (W2, FR-038) |

- **Decision**: modes `off` (default), `lan-https` (recommended) and `lan-http`. `lan-https` uses
  any PEM certificate + key the owner supplies ("bring your own"; the manual documents mkcert).
  Kestrel loads them with `X509Certificate2.CreateFromPemFile` or, when
  `STEMSTUDIO_LAN_CERT_PASSWORD` is set, `CreateFromEncryptedPemFile`. Start-up validates: file
  readable, key matches, not expired, SAN contains the public host; on failure the remote surface
  stays off (no fallback to HTTP).
- No HSTS (IP-address hosts; it would break the HTTP fallback and is pointless for IP literals).

### R-05 The TLS private key and the "no secrets in files" rule

- The leaf key is a per-installation credential. It cannot come from an environment variable in
  practice (Kestrel needs a file or a store), so the rule is met in spirit: the key lives **outside
  the repository, the image and the database**, in a host folder mounted read-only
  (`${STEMSTUDIO_LAN_CERT_DIR}` -> `/certs:ro`), optionally encrypted with a password that comes
  only from `STEMSTUDIO_LAN_CERT_PASSWORD`. `.gitignore` gets `*.pem`, `*.key`, `*.p12`. Recorded
  as a Complexity Tracking item in plan.md for CTO acceptance.
- Impact of a stolen leaf key: impersonating the StemStudio remote address on the owner's LAN for
  devices that trust the CA. Much smaller than a stolen mkcert root key (R-04 B).

### R-06 Pairing

- 6-digit numeric code (typed on a phone easily), cryptographic RNG, 120 s window, single use,
  5 wrong codes end the window, 20 attempts/min globally. Guessing chance per window <= 5 in
  1,000,000, and even a correct guess only creates a **request** that the owner must Allow.
- **Check number** (3 digits, derived from the request id, shown on both screens) prevents the
  owner from approving an attacker who raced the real device with the same name ("numeric
  comparison" as in Bluetooth pairing). The real device's later attempt fails because the code
  was used, which is a second signal.
- QR content: `https://<public-host>:<port>/remote/#pair=<code>` (fragment: never sent to the
  server, never logged). The QR is rendered server-side as SVG with **QRCoder 1.8.1 (MIT)**.
- Pairing windows live in API memory only (restart ends them); devices, settings and the audit
  log are persisted in SQLite (backend 1.17.0).
- The device fetches its key once with a poll secret (128 bits) returned by redeem, so the key
  never travels to anyone who did not redeem; the key is deleted from memory after delivery or
  after 60 s.

### R-07 Device key handling

- 256-bit key, base64url, stored on the device in `localStorage` (the remote has no other state).
  Stored server-side as SHA-256 (high-entropy secret, no slow hash needed). Verified by the web app
  through `POST /api/remote/devices/authenticate` (server to server), cached for 60 s, re-verified
  on every connect and when the cache expires; removal pushes an immediate disconnect.
- **Not a cookie**: cookies would bring CSRF and cross-site WebSocket hijacking into play and
  cannot be `Secure` in `lan-http`. The key is sent in the first WebSocket message and as
  `Authorization: Bearer` for content; never in a URL (logs, history, Referer).
- XSS is the main threat to a `localStorage` key: the remote has a strict CSP without inline
  script, renders all server text with `textContent`, loads nothing third-party, and pdf.js runs
  with scripting disabled (`isEvalSupported: false`, no annotation scripting).

### R-08 Origin, host and browser-side attacks

- **Cross-site WebSocket hijacking**: WebSockets are not covered by CORS; the server compares
  `Origin` with the remote origin exactly and refuses otherwise (also on pairing POSTs).
- **DNS rebinding**: host allow-lists on both listeners and on the API (FR-014) through the
  built-in `HostFilteringMiddleware` (`AllowedHosts`) for the main listener and API, and a check in
  the remote gate for the remote listener.
- **CSRF on the API's new endpoints**: JSON bodies required (`{}` at minimum) and a foreign
  `Origin` refused (403). Server-to-server calls from the web app send no `Origin`.
- **Private Network Access** (Chrome) adds preflights for public sites calling private addresses;
  helpful but not relied on.
- **Clickjacking**: `frame-ancestors 'none'` and `X-Frame-Options: DENY` on the remote.

### R-09 Two listeners in one ASP.NET Core app

- Kestrel endpoints configured in code override `ASPNETCORE_HTTP_PORTS`; the web app therefore
  configures both endpoints explicitly from options: main (`StemStudio__MainPort`, default 8080,
  plus the existing env) and remote (`StemStudio__Remote__Port`, default 8443 for https, 8090 for
  http) only when the mode is not `off`.
- Requests are classified by `HttpContext.Connection.LocalPort` (the socket the request arrived
  on, not the `Host` header). A **remote gate** middleware runs first: on the remote port it
  enforces host, origin, security headers and the path allow-list and returns 404 for anything
  else **before routing**; on the main port it returns 404 for `/remote/v1/*`. Remote endpoints
  also carry an endpoint filter that re-checks the port (defence in depth), so a routing mistake
  cannot expose them on the main listener or Blazor on the remote one.
- `MapWhen` branches are avoided: with `WebApplication` the routing middleware runs before the
  branch and endpoint matching is global, which makes branch isolation easy to get wrong.

### R-10 Player bridge and command routing

- A singleton `RemoteHub` in the web app holds the registered players (mixer components) and the
  remote connections. The mixer registers through a scoped `RemotePlayerBridge` when remote mode
  is effective; commands are dispatched with the component's `InvokeAsync` (Blazor
  synchronisation context) to the **same** handlers that the mixer controls and the 014 command
  catalogue use, so remote and local actions behave identically (and are covered by the same
  bUnit tests).
- Active player: the mixer that last started playback or received focus; ties broken by
  registration time. A mixer that closes unregisters; the shell (any page) registers a
  **navigator** so US6 can open a song.
- Command names in remote-protocol 1.0.0 are canonical; the mapping to 014's command ids is a
  table in the contract, completed in Phase 0 against 014's final contract (T002).

### R-11 State sync and latency

- Full snapshot (<= 8 KB) on every change, coalesced to <= 10/s, and every 2 s while playing
  (keeps late joiners and dropped packets simple; no patch protocol). Each snapshot carries
  `serverTime` and `position` at that time plus `rate` (tempo); the remote interpolates with
  `performance.now()` offsets and corrects on the next snapshot. The server never trusts a device
  clock.
- Budget for SC-001: WebSocket on a LAN ~5-30 ms, hub dispatch < 5 ms, Blazor `InvokeAsync` + JS
  interop to the engine ~10-40 ms on the existing circuit, engine start with look-ahead ~25 ms;
  well under 250 ms. The E2E measures the main context's engine state change against the
  remote's send time on loopback (150 ms p95).

### R-12 Installable app (PWA)

- Main app: `wwwroot/manifest.webmanifest`, `wwwroot/sw.js` (scope `/`), `wwwroot/offline.html`,
  icons generated once from `favicon.svg` (192, 512, maskable 512, Apple touch 180, PNG) and
  committed. Remote: the same under `/remote/` with its own `id`/`scope`.
- Service worker rules (FR-007): precache a versioned shell list; `fetch` handler only answers
  (a) navigations: network first, offline page on network failure, and (b) requests for the
  precached shell files: cache first. Everything else (`/_blazor`, `/_framework`, API origin, stems,
  any cross-origin request, any non-GET) is not intercepted (`respondWith` not called). New
  versions: `skipWaiting` + `clients.claim`, old caches deleted on activate.
- Blazor caveat: the circuit needs the server; the offline page is static HTML (no Blazor) with a
  "Try again" link. The SW must not cache `blazor.web.js` to avoid version skew between shell and
  server.
- Installability: Chrome/Edge show their own install UI; the shell's "Install app" button uses
  `beforeinstallprompt` where available (Chromium); Safari users get an "Add to Home Screen" hint.
  Interop for these and the wake lock is an additive minor of `web-interop` (number assigned at
  merge, see DELTA.md).

### R-13 Screen wake lock

- `navigator.wakeLock.request("screen")` while the stand view is visible; the sentinel is
  released automatically when the page is hidden, so the code re-requests on
  `visibilitychange` -> visible. Unsupported or refused -> state `unsupported`/`off` shown; no
  "silent video" tricks (constitution V honesty).
- Main app: through web-interop (additive). Remote: in the remote client.

### R-14 Sheet music on the remote

- Browser PDF viewers are inconsistent (Android Chrome: none inline; iOS: first page only in an
  iframe). **pdf.js** (`pdfjs-dist` **6.5.438**, Apache-2.0, published 2026-11-01) renders pages to
  a canvas in the remote client, lazy-loaded only when a sheet is shown; the pdf.js worker is a
  same-origin file (`worker-src 'self'`). Scripting and eval are disabled.
- Page follow uses 014's score map (measure/time -> page) when present; otherwise manual turning.
- Rejected: server-side rasterising (native PDF libraries in the web image), MusicXML engraving
  on the tablet (heavy, duplicates 008).

### R-15 Content proxy

- The remote never talks to the API. The web app's content endpoints call the API server-side
  (existing `IStemStudioApiClient` methods for lyrics, sections, chords, assets, artwork) **only**
  for the active player's track and only for asset ids that belong to that track and have a
  sheet kind; responses are re-serialised to the small shapes in the remote contract (no file
  names, paths or internal ids beyond the asset id).

### R-16 Testing

- Unit: backend xUnit for pairing/devices/audit/time; web xUnit/bUnit for the gate, hub,
  validator, projector, content proxy, pages; remote client Vitest with jsdom and a fake
  WebSocket; SW logic tested as pure functions (routing decision table) in Vitest.
- E2E (qa): stack with `docker-compose.lan.yml` + `STEMSTUDIO_LAN_ADDRESS=127.0.0.1`,
  `StemStudio__Remote__Mode=lan-http`; two browser contexts: A = `http://localhost:8080`,
  B = `http://127.0.0.1:8090/remote/`. Insecure-context behaviour is tested with Chromium
  `--host-resolver-rules="MAP stemstudio-lan.test 127.0.0.1"` (B on `http://stemstudio-lan.test:8090`,
  not a secure context). HTTPS: a CI job creates a throwaway CA and leaf with `openssl`, starts
  `lan-https`, and runs the wss smoke in Chromium with `ignoreHTTPSErrors` (handshake, wss,
  headers; service-worker checks run on the secure loopback origin instead).
- Security suite: a Playwright API project plus xUnit integration tests covering the list in
  plan.md "Security verification".
- Manual (integration wave): real iPad/Android tablet + phone on the owner's Wi-Fi with mkcert;
  SC-001 Wi-Fi latency; port scan from a second device (`nmap`, or the documented PowerShell
  `Test-NetConnection` loop).

## 3. Licences (new dependencies)

| Name | Version | Licence | Where | URL |
|---|---|---|---|---|
| QRCoder | 1.8.1 (2026-10-13) | MIT | `StemStudio.Web` (QR as SVG) | https://github.com/Shane32/QRCoder |
| pdfjs-dist | 6.5.438 (2026-11-01) | Apache-2.0 | `src/remote-client` runtime | https://github.com/mozilla/pdf.js |
| vite, vitest, @vitest/coverage-v8, typescript, eslint, typescript-eslint, jsdom | same pins as `src/web-interop` | MIT / Apache-2.0 / BSD-2 | `src/remote-client` dev | (as in web-interop) |
| mkcert | owner-installed tool, not a dependency | BSD-3-Clause | documentation only | https://github.com/FiloSottile/mkcert |

No new worker, generator or engine dependency. ASP.NET Core WebSockets, host filtering and
`X509Certificate2` are part of the shared framework.

## 4. Open risks

| # | Risk | Mitigation |
|---|---|---|
| K-01 | Docker Desktop host-IP publishing or Windows firewall behaves differently than expected | T003 verification on the reference machine; non-Docker path documented |
| K-02 | `127.0.0.1` binding breaks someone who used the app from another device | Release note + `STEMSTUDIO_BIND_ADDRESS` override; the supported way is remote mode |
| K-03 | mkcert root key on the owner's PC is a powerful secret | Manual: keep `CAROOT` private or delete the CA key after issuing; ADR-027 upgrade path D |
| K-04 | `lan-http` device key readable on the Wi-Fi | Low impact (practice commands, current song's lyrics/sheets); labelled; key bound to `http` transport and shown in the device list; HTTPS recommended |
| K-05 | 014/016 contracts differ from the assumed names | Phase 0 reconciliation (T002); remote contract names are ours, mapping table only |
| K-06 | iOS wake lock and install quirks | Documented; graceful states; manual test on a real iPad |
| K-07 | pdf.js bundle size (~1.5 MB incl. worker) on first open | Lazy load, cached by the remote shell in HTTPS mode, LAN speeds |
| K-08 | Remote commands racing local edits | Arrival order, same handlers, full snapshots; nothing saved remotely |
