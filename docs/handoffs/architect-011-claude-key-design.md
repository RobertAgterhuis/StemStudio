# Hand-off: architect / 011-claude-key-design

## What was built

Design only (no product code) for the 011 follow-up "Claude key in Settings, stored encrypted":
spec (6 user stories, CK-FR-001..018, CK-SC-001..006), plan (threat model T1-T11, constitution
check, proposed amendment 1.4.0, owner questions), research (Microsoft Learn facts on Data
Protection key management, application name, at-rest encryption, HTTP logging; Docker NAT limit
of "local only"), data-model delta (`AgentSecrets`, ports, state table, problem types, events),
contracts delta (backend API 1.21.0-delta.1, ui-test-ids 1.21.0, DELTA.md), quickstart, tasks
(56 tasks, red-green per owner), ADR-035 (Proposed), cross-feature notes in 013 and 018, ROADMAP
allocation and a proposed CTO-DECISIONS row.

## Files changed

- `specs/011-ai-composer-agent/followups/claude-key/{spec,plan,research,data-model,quickstart,tasks}.md`
- `specs/011-ai-composer-agent/followups/claude-key/contracts/{DELTA.md,backend-api.claude-key.openapi.yaml,ui-test-ids.delta.md,redocly.yaml}`
- `docs/decisions/ADR-035-claude-key-encrypted-storage.md` (new)
- `docs/decisions/CTO-DECISIONS-2026-11-15.md` (one proposed row)
- `docs/ROADMAP.md` (allocation row and note, owner decision line)
- `specs/013-library-care/{spec.md,research.md,tasks.md}` (cross-feature rules; T035, T048, T062)
- `specs/018-tablet-remote/spec.md` (cross-feature note)

## Tests and coverage

No code. Contract validation: `npx @redocly/cli@2.78.0 lint --config
specs/011-ai-composer-agent/followups/claude-key/contracts/redocly.yaml
specs/011-ai-composer-agent/followups/claude-key/contracts/backend-api.claude-key.openapi.yaml`
-> "Your API description is valid", 2 expected warnings (`operation-4xx-response` on
`getAgentStatus` and `getClaudeKeyStatus`, which have no client errors; same rule level as 011).
`python scripts/secret_scan.py` -> "3461 files clean".

## Quality checks

Not applicable (documents only). The design confines Data Protection to
`StemStudio.Api/Agent/Secrets` (quality-gate rule T017/T018).

## Dependencies added

None (ASP.NET Core Data Protection is part of the shared framework `Microsoft.AspNetCore.App`, MIT).

## Docs impact

When built: user manual (Settings -> Claude key, recovery from "stored-unreadable"), technical
manual (volume `api-keys`, key ring directory, never copy it with backups, threat model limits),
`SECURITY.md`, README note (tasks T054, T055).

## Open issues / requests

- Owner: Q0 ratify constitution 1.4.0; Q1 bind ports to `127.0.0.1` before 018; Q2 keep or strip
  the ciphertext in 013 backups (plan.md).
- CTO: the CTO-DECISIONS row and ROADMAP edits are outside the architect's owned paths; they were
  made on the coordinator's explicit request and are marked proposed.
- Spike T003 must confirm the Docker Desktop peer-address behaviour assumed in research R-05.

## Next owner

CTO (owner ratification, T001), then architect Phase 0 (T002-T006).

## Owner decisions recorded (2026-11-16)

- Q0: constitution **1.4.0** ratified (MINOR) and applied to `.specify/memory/constitution.md`
  (condition (2) of the paid-service exception replaced; version line 1.4.0, Last Amended
  2026-11-16; the file has no Sync Impact Report comment or history section, so the amendment is
  recorded in the exception's provenance and in this commit). ADR-035 Accepted; CTO-DECISIONS row
  ratified; T001 ticked.
- Q1: keep LAN access until 018; threat T7 recorded as an accepted residual risk (plan.md, ADR-035,
  CTO-DECISIONS, 018 note); T052 not selected; spike T003 kept.
- Q2: 013 backups keep the ciphertext; 013 spec cross-feature note and T048 now state: same
  computer keeps the key, another computer asks again, the archive never contains the key ring.
- Superseded notes (no behaviour rewrite): 011 spec clarification, FR-018, SC-006; 011 plan
  amendment quote; 011 tasks DoD line; ADR-020 D3; technical manual chapter 5 (PDFs rebuilt,
  `node tools/docs/check.mjs` green). README, SECURITY.md and the other manual chapters describe
  delivered behaviour and stay for T054/T055.
- Next: architect Phase 0 (T002-T006), then the owner wave.
