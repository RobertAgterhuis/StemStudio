# Security policy

StemStudio is a local, single-user tool. Do not expose its ports (8080, 8081) to the internet.

## Reporting a vulnerability

Please report vulnerabilities privately through GitHub's
[private vulnerability reporting](https://github.com/RobertAgterhuis/StemStudio/security/advisories/new).
Do not open a public issue for security problems. You can expect an acknowledgement within a week.

## Supported versions

Only the latest `main` is supported.

## Secrets

StemStudio never stores secrets in plain text. The optional Claude backend takes its key from the
`ANTHROPIC_API_KEY` environment variable (wins) or from a key entered in Settings, which the API
stores encrypted (constitution 1.4.0, ADR-035). CI needs no secrets for its quality gates;
any secret it does use comes from repository secrets only. Secret scanning with push protection is enabled on this repository; the
repository secret scan (`scripts/secret_scan.py`) also fails on any Claude-key-shaped literal.

### Stored Claude key: what is protected

The key is encrypted with ASP.NET Core Data Protection; the database (`AgentSecrets`) holds only the
ciphertext, the last four characters and the save time. The key ring lives in its own place: the
Docker volume `api-keys` at `/keys` (mounted only into `api`, `0700`, app user) or, without Docker,
a per-user directory (`%LOCALAPPDATA%\StemStudio\keys` with DPAPI on Windows,
`~/.local/share/stemstudio/keys` on Linux/macOS). The API refuses a key ring inside the library,
the backups, the app folder or a git work tree. The key API is write-only (`GET` status only,
`PUT`/`DELETE` local-only, HTTP logging off for the route).

| Threat | Protected | How |
|---|---|---|
| Copy of the database file, a backup or an export | yes | ciphertext only; the key ring is never in the library or a backup |
| The library volume (also the worker's read-only mount) | yes | the key ring volume is mounted only into `api` |
| Logs, problem details, events, screenshots, password managers | yes | the value is never returned, logged or rendered; the field is `autocomplete="off"` and emptied after saving |
| A malicious web page in your browser (CSRF) | yes | writes need a CORS preflight; the API allows only `GET` from the web origin |
| Another device on your LAN, before feature 018 | partly | it can never read the key, and direct API calls are refused; through the Web page it could overwrite or delete the stored key (accepted until 018 binds the ports to `127.0.0.1`) |
| Full access to the computer (admin/root, the Docker VM disk, malware running as you) | **no** | with both the database and the key ring the key can be decrypted; on Linux the key ring is not encrypted at rest |
| Memory of the running API or Web process | **no** | the key is in memory while a Claude client exists |

Losing the key ring (`docker compose down -v`, a restore on another computer) makes the stored key
unreadable by design: Settings says so and you enter the key again. After a leak, revoke the key in
the Anthropic console first, then replace it in Settings.
