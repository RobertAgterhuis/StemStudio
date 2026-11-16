# Hand-off: architect / 018 spec (Tablet and remote)

## What was built

The complete Spec Kit design for feature 018 (MoSCoW C4), specs and design only, no production
code. The Phase 0 skeleton is not built yet: it is planned as tasks T001-T007 and runs at feature
start, after 012-017 have fixed their contract minors (backend 1.16.0, ui-test-ids 1.16.0).

- `specs/018-tablet-remote/spec.md` (6 user stories, FR-001..FR-038, SC-001..SC-009, edge cases,
  Won't have) and `checklists/requirements.md`
- `research.md` (findings F-01..F-08 on the current system, decisions R-01..R-16, licences,
  risks K-01..K-08)
- `plan.md` (technical context, constitution check, structure, ownership, key flows, security
  verification list SV-01..SV-22, contract versions, complexity tracking, flags)
- `data-model.md` (configuration, settings, devices, pairing state machine, audit, hub state,
  snapshot, browser preferences)
- `contracts/`: `backend-api.remote.openapi.yaml` (1.17.0-delta.1), `remote-protocol.md` +
  `remote-lan.openapi.yaml` + `remote-messages.schema.json` + `remote-examples/` (remote-protocol
  1.0.0), `ui-test-ids.delta.md` (1.17.0), `DELTA.md`, `redocly.yaml`, `check_examples.py`
- `quickstart.md` (Q1-Q6), `tasks.md` (120 tasks, TDD pairs, per lane, `[P]` and `[sec]` markers)
- `docs/decisions/ADR-027-tablet-remote-lan-security.md` (decisions D1-D11, STRIDE threat model)

## Files changed

All new: `specs/018-tablet-remote/**`, `docs/decisions/ADR-027-tablet-remote-lan-security.md`,
this note.

## Tests and coverage

No code, so no coverage numbers. Contract checks run:

- `npx @redocly/cli@2.78.0 lint --config specs/018-tablet-remote/contracts/redocly.yaml
  backend-api.remote.openapi.yaml remote-lan.openapi.yaml` -> valid (OpenAPI 3.1), 3 known warnings
  (GET settings and GET devices have no 4XX response; the WebSocket upgrade answers 101).
- `uv run --no-project --with "jsonschema[format]==4.25.1" python -I contracts/check_examples.py ...`
  -> schema is valid Draft 2020-12; 20 valid examples validate, 8 invalid examples fail
  (`failures 0`).

## Quality checks

Not applicable (documents only).

## Dependencies added (planned, pinned in Phase 0)

| Name | Version | Licence | URL |
|---|---|---|---|
| QRCoder | 1.8.1 (2026-10-13) | MIT | https://github.com/Shane32/QRCoder |
| pdfjs-dist | 6.5.438 (2026-11-01) | Apache-2.0 | https://github.com/mozilla/pdf.js |
| mkcert (owner-run tool, not a dependency) | - | BSD-3-Clause | https://github.com/FiloSottile/mkcert |

## Docs impact

User manual chapter "Tablet and remote"; technical manual deployment, security, APIs, data model,
operations; `SECURITY.md`; licences (DELTA.md 9). Done by the CTO in the integration wave.

## Open issues / requests

1. **Network exposure (flag)**: remote mode opens one port on an explicitly configured LAN address
   (opt-in at deployment and in the app, both off by default).
2. **Exposure reduction (flag)**: the base compose file publishes 8080/8081 on all interfaces today;
   018 moves them to `127.0.0.1` and restricts `AllowedHosts` (research F-01, F-02).
3. **Pre-existing CSRF** exposure of empty-body POST endpoints of the API (F-03): not fixed by 018,
   recommended follow-up.
4. Contract allocation gaps: web-interop and help-content need additive minors not in the roadmap
   table (numbers assigned at merge).
5. TLS key in a file vs. the constitution's secrets rule: interpretation recorded in plan.md
   Complexity Tracking for CTO acceptance.
6. Phase 0 T003 must verify Docker Desktop host-IP publishing and mkcert trust on iPad/Android on
   the reference machine before devops merges the compose change.

## Next owner

CTO (review), then architect Phase 0 (T001-T007) at feature start.
