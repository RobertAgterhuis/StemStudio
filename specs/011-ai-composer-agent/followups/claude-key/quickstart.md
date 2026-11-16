# Quickstart / validation: Claude key in Settings (011 follow-up)

Validation script for the CTO after the integration wave. Q1-Q6 run anywhere (fake stack, no
Anthropic call); Q7-Q9 need the reference machine; Q10 is optional, paid, owner only.

## Prerequisites

- Constitution 1.4.0 ratified (task T001).
- Fake stack: `STEMSTUDIO_SEPARATOR=fake STEMSTUDIO_AGENT_BACKEND=fake docker compose up -d --build --wait`
  **without** `ANTHROPIC_API_KEY` in the shell.
- Tests build their sentinel key at run time; never paste a real key into a file, a command line
  or this document.

## A. Automated (CI-equivalent)

| # | Step | Expected |
|---|---|---|
| Q1 | `dotnet build StemStudio.slnx`; `dotnet test` with coverage | 0 warnings; green; Agent, Api, Persistence, Web >= 90 % line and branch |
| Q2 | `npx @redocly/cli@2.78.0 lint` of the full backend document 1.21.0 (and of `followups/claude-key/contracts/backend-api.claude-key.openapi.yaml` with its `redocly.yaml`) | valid OpenAPI 3.1 |
| Q3 | `python tools/quality/run_all.py`; `python scripts/secret_scan.py` | 0 errors; Data Protection only in `StemStudio.Api/Agent/Secrets`; no key-shaped literal |
| Q4 | Playwright `e2e/tests/agent-claude-key.spec.ts` on the fake stack | save -> status `stored` with last four; DOM and input without the sentinel; replace; delete with confirmation; `stored-unreadable` fixture; local-only notice when the page is opened under a host name outside the allow-list (the container host name `web`, Playwright `baseURL` override) |
| Q5 | Sentinel scan test (`ClaudeKeyLeakTests`) | 0 hits in responses, Trace logs, SSE, `stemstudio.db`/`-wal`/`-shm`, DOM (CK-SC-001) |
| Q6 | Must-not-call Anthropic handler over start-up, page load, status polling, save, replace, delete | 0 requests; 1 per **Test key** (CK-SC-002) |

## B. Reference machine

| # | Step | Expected |
|---|---|---|
| Q7 | `docker volume inspect stemstudio_api-keys`; `docker compose exec api stat -c '%a %u' /keys` | volume exists, `700` and the app uid; `docker compose exec worker ls /keys` fails (not mounted) |
| Q8 | Save a well-formed dummy key in Settings (the prefix plus 100 random letters typed by hand; saving never contacts Anthropic), then `docker compose down` and `docker compose up -d --build --wait` | status still `stored`, same last four (CK-SC-003) |
| Q9 | `docker compose down`, `docker volume rm stemstudio_api-keys`, `docker compose up -d --wait` | API healthy; status `stored-unreadable` with "Enter the key again"; Ollama agent and the full E2E suite work (CK-SC-004); saving a key again -> `stored` |
| Q9b | From a second device on the LAN open `http://<computer-ip>:8080/settings` (before 018; the owner kept LAN access, Q1 (b) 2026-11-16) | key section disabled with the local-only notice; `curl -X DELETE http://<computer-ip>:8081/api/agent/settings/claude-key` -> `403 local-only` |

## C. Optional, paid, owner only

| # | Step | Expected |
|---|---|---|
| Q10 | Paste the real key in Settings, **Test key**; select Claude, acknowledge the notice, send one 011 A10 case; then export `ANTHROPIC_API_KEY` with a different key and restart | test ok within 10 s; run works; status shows `environment` as active and the stored key as "not in use"; delete the stored key at the end if wanted |
