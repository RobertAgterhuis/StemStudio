# 004 Publish the web and api ports on localhost only by default
- Status: done (2026-11-23; commits `bd9e5ff2`, `2f9a6d0e`, `8814be29` on branch `sec-003-004`, see "Outcome") · Date: 2026-11-19 · Area: delivery / docker compose · Priority: P1

## Problem statement
- `docker-compose.yml:181-182` (`"8081:8081"`, api) and `docker-compose.yml:217-218` (`"8080:8080"`, web) publish both ports on
  every host interface (Docker's default `0.0.0.0`). Every device on the same network can reach them, not only this computer.
- The compose header itself names the consequence (`docker-compose.yml:72-75`): key entry is meant for "this computer only", but
  "until feature 018, a LAN device that forges the host name can overwrite or delete the stored key" (threat T7). The api also
  serves the library, backups (`/backups`) and the agent. Whether those endpoints check access is outside this audit (the
  backend auditor covers it). Either way the network exposure comes from the compose file.
- The documented URLs are `localhost` everywhere (`docker-compose.yml:2`, `StemStudio__WebOrigin=http://localhost:8080` at
  `:160`, `StemStudio__PublicApiBaseUrl` at `:214`). Nothing in the design needs LAN access by default.
- The non-Docker path already binds to loopback: `scripts/dev.sh:120` starts the worker with `--host 127.0.0.1`.

## Impact
- Security exposure on every install on a shared or public network (cafe, venue, studio Wi-Fi). Another device can overwrite or
  delete the paid Claude key, and can reach the api and the web UI without the owner knowing.
- The planned feature 018 fix only lands later. This mitigation costs two lines today.

## Risk
- Leaving it: the exposure stays until 018 ships, and stays for every user who never upgrades.
- The change: low. Users who deliberately open StemStudio from a tablet on the LAN lose that until they opt in. Mitigation: a
  variable `STEMSTUDIO_BIND_ADDRESS` (default `127.0.0.1`), documented in the compose header and `docs/public`. Docker Desktop on
  Windows and macOS forwards `127.0.0.1` bindings to the host browser as before. Rollback: set the variable to `0.0.0.0`.
- CI: `curl http://localhost:8080` and `:8081` (`ci.yml:683-684`) keep working, because the runner is the host.

## Effort
S (0.5 day): two port lines, one `jq` assertion in CI, and the docs. No application code changes.

## Step-by-step plan
1. Safety net: add a CI assertion to the "Every compose combination validates" step (`ci.yml:538-563`). Every published port of
   api and web has `host_ip == "127.0.0.1"` unless `STEMSTUDIO_BIND_ADDRESS` is set. It fails on today's file (red), which proves
   that the check works.
2. Change the ports to `"${STEMSTUDIO_BIND_ADDRESS:-127.0.0.1}:8081:8081"` and `"${STEMSTUDIO_BIND_ADDRESS:-127.0.0.1}:8080:8080"`.
   Check every override combination with `docker compose config` (the same step).
3. Add `STEMSTUDIO_BIND_ADDRESS` to `.env.example` (commented out, with the T7 warning) and to the compose header. Update the
   user manual in `docs/public` and regenerate the PDFs (constitution rule).
4. Optional hardening in the same file (P3, separate commit): `security_opt: ["no-new-privileges:true"]` and `cap_drop: [ALL]`
   for worker, api and web, since all three already run as non-root (`docker/*.Dockerfile` `USER`). Check: the E2E stack stays
   healthy.
5. Verify: from a second device on the LAN, `http://<host-ip>:8080` is refused, and `http://localhost:8080` works. Roll back by
   reverting, or by setting the variable.

## Dependencies and related proposals
- Feature 018 (key-entry hardening) is the application-side fix. This proposal is defence in depth and does not replace it.
- ADR-027 D3 already decides "publish 8080/8081 on 127.0.0.1" with `STEMSTUDIO_BIND_ADDRESS`. This proposal implements exactly
  that compose part ahead of 018. `003-api-exposure.md` (step 4) asks for the same change from the backend side. Merge
  the two, or schedule them together.
- `014-supply-chain-pinning.md` (container hardening in general).

## Outcome (2026-11-23)
1. `scripts/loopback_ports_checks.py` (pytest, 100 % line and branch) with `scripts/check-loopback-ports.{sh,ps1}`: every
   api/web port on `127.0.0.1` with the variable unset, on `0.0.0.0` with it set, and no other service publishes a
   port. Wired into the CI compose validation (all five combinations incl. the e2e override), the devops script
   checks and `test-all.{sh,ps1}`. Red on the old file (`bd9e5ff2`).
2. Ports `"${STEMSTUDIO_BIND_ADDRESS:-127.0.0.1}:8081:8081"` and `:8080:8080` (`2f9a6d0e`).
3. `.env.example` (commented out, T7 warning), compose header, user and technical manuals; PDFs regenerated.
4. `security_opt: ["no-new-privileges:true"]` and `cap_drop: [ALL]` for worker, api and web (`8814be29`); the E2E stack
   stayed healthy and green with it.
5. Verified: the isolated stack publishes on `127.0.0.1` only (`docker compose config`, `docker inspect`); from the host
   `http://localhost:18080` and `:18081` work. A second LAN device was not available in this environment; the binding
   itself is what refuses it.
