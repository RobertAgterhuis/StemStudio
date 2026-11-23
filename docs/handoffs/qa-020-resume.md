# Resume note, lane QA-OPS of feature 020 (qa, quality-gate, devops)

Branch `020-lane-qa-ops`, worktree `.claude/worktrees/020-lane-qa-ops`. State: green and committed.

| Owner | Done | Left |
|---|---|---|
| devops | T011, T012 (`docs/handoffs/devops-020-foundational.md`); `insights` E2E pass | - |
| quality-gate | T009, T010 (`docs/handoffs/quality-gate-020-foundational.md`) | T100 after the lanes merge: `run_all.py` on all 020 code, refactor requests, SIG-REPORT |
| qa | T013, T028, T029, T048, T055, T080, T087, T101 authored (`docs/handoffs/qa-020-wave.md`) | run the specs on a real fake stack at the mid-wave checkpoint; T102 at integration |

Isolated smoke stack (never 8080/8081): `source .claude/scratchpad/020-round/qa-ops/env.sh` from the worktree root
(project `e2e020`, ports 38080/38081, images `e2e020/{worker,api,web}:local`); tear down with
`docker compose down -v`. Fixtures first: `generate_fixtures.py`, `generate_detail_fixtures.py`,
`practice-flow/generate_practice_flow_fixture.py`, `families/generate_family_fixtures.py`.
