# Quickstart: Tablet and Remote (feature 018)

Validation walk-through for the integration wave (CTO) and the owner. Automated equivalents are in
tasks.md (E2E and SV tests). Reference machine: Windows 11 + Docker Desktop, plus a tablet and a
phone on the same Wi-Fi.

## Q1 Default: nothing on the network

```powershell
docker compose up -d --build --wait
docker compose ps --format "{{.Name}} {{.Ports}}"      # expect 127.0.0.1:8080->8080, 127.0.0.1:8081->8081
```

From the tablet's browser open `http://<PC-LAN-IP>:8080` and `:8081/health`: both must fail.
Open `http://localhost:8080/remote-control` (Advanced level): "Remote control is not set up".

## Q2 Stand view and install on the computer

1. Import a fixture song (fake stack: `STEMSTUDIO_SEPARATOR=fake`), open the mixer, click
   **Stand view**. Check landscape (1280x800), then narrow the window to portrait (768x1024) and to
   320 px: no horizontal scroll, transport buttons large.
2. Pick each part that has content; play; the lyric line, chord and sheet page follow; "Keep screen
   on" shows `on` (Chrome/Edge).
3. Click **Install app** in the shell; the app opens in its own window. Stop the stack
   (`docker compose stop web`) and reopen the app: "StemStudio is not running" page within 2 s.
   Start it again and click **Try again**.

## Q3 Remote mode over HTTP (quick test, not encrypted)

```powershell
$env:STEMSTUDIO_LAN_ADDRESS = "192.168.1.20"     # your PC's LAN address (scripts/lan-check.ps1 lists them)
$env:STEMSTUDIO_REMOTE_MODE = "lan-http"; $env:STEMSTUDIO_REMOTE_PORT = "8090"
pwsh scripts/lan-check.ps1
docker compose -f docker-compose.yml -f docker-compose.lan.yml up -d --wait
```

Windows asks about the firewall: allow **Private networks** only.

1. Remote control page: mode `lan-http`, address `http://192.168.1.20:8090`, "Not encrypted"
   warning. Turn on **Allow remote control**, click **Pair a device**.
2. On the phone scan the QR (or open the address and type the code), enter "Phone". Compare the
   check number on both screens, click **Allow** on the computer.
3. On the phone: Transport tab shows the open song; Play, Next section, loop a section, Tempo 80 %;
   the computer follows within a quarter second. Mix tab: "Practise my part" for bass mutes the
   bass on the computer.
4. Stand tab: content follows playback; wake lock shows "unavailable without HTTPS".
5. Computer: remove the device -> the phone shows "Removed on the computer" within a second.
6. Type a wrong code five times on a new pairing window -> "Pairing stopped after 5 wrong codes".
7. From the phone open `http://192.168.1.20:8090/` and `/_blazor` -> 404; `:8080` -> no answer.

## Q4 Remote mode over HTTPS with mkcert (recommended)

mkcert (BSD-3-Clause, https://github.com/FiloSottile/mkcert) is installed and run by you; StemStudio
does not ship it.

```powershell
mkcert -install                                    # creates your local CA (keep CAROOT private)
mkdir certs
mkcert -cert-file certs/stemstudio-lan.pem -key-file certs/stemstudio-lan-key.pem 192.168.1.20 <pc-name>.local
mkcert -CAROOT                                     # folder with rootCA.pem to install on the tablet
$env:STEMSTUDIO_REMOTE_MODE = "lan-https"; $env:STEMSTUDIO_REMOTE_PORT = "8443"
docker compose -f docker-compose.yml -f docker-compose.lan.yml up -d --wait
```

- iPad/iPhone: send `rootCA.pem` to the device, install the profile (Settings -> General -> VPN &
  Device Management), then enable full trust (Settings -> General -> About -> Certificate Trust
  Settings).
- Android: Settings -> Security -> Encryption & credentials -> Install a certificate -> CA
  certificate; Chrome then trusts it.
- Optional hardening: after issuing, move `rootCA-key.pem` out of `CAROOT` to offline storage (you
  need it again only to issue a new certificate, for example when the IP changes).

Repeat Q3 steps 1-5 on `https://192.168.1.20:8443/remote/`. Additionally: install the remote as an
app ("StemStudio Remote"), Stand tab keeps the screen on, the "Not encrypted" badge is gone.
Replace the certificate with an expired one -> the Remote control page shows
`certificate-expired` and the port is closed (no HTTP fallback).

## Q5 Two remotes and the latency check

Pair the tablet and the phone; send commands from both alternately; both show the same state. In
Expert level open the audit log: pairing and removal events, no codes or keys. For SC-001 on Wi-Fi
run the E2E latency probe against the LAN address (`E2E_REMOTE_URL=https://192.168.1.20:8443
npx playwright test remote-latency`) and record the p95 in the integration hand-off.

## Q6 Port scan from another device

From a second computer on the LAN: `nmap -p 1-65535 192.168.1.20` (or
`Test-NetConnection 192.168.1.20 -Port 8080/8081/8443`). Remote mode off: no StemStudio port open.
Remote mode on: only the remote port.
