# CTO 020 integration: resume note

Branch `020-insights-handouts` (worktree `.claude/worktrees/020-insights-handouts`). Logs:
`.claude/scratchpad/020-round/integration/`. Binding: `.claude/scratchpad/020-round/orchestrator-log.md`.

## Done
- [x] Merges (--no-ff): qa-ops, beat, worker-io, backend, frontend (tasks.md union), main bf7df94e -> d8b455f5

## Todo (in order)
- [ ] Build check after merges
- [ ] Backend: remove RequireGateOpen + x-stemstudio-write-gate (POST quality, POST handouts, PUT practice-notes)
- [ ] 409 `handouts-active`; options 409 `too-many-songs` > 50; ContractLimits mirror 4 worker request limits
- [ ] Warning codes `notation-omitted`, `artwork-omitted` (worker, backend, UI); POST /v1/handout-jobs description; outline example order
- [ ] Architect T108 items (data-model 4.2-4.4, R-02/R-03), dependency map `insights`, threadpoolctl/fontTools pins, libharfbuzz-subset0, licence rows
- [ ] LyricAssistEdgeTests flake
- [ ] Central logs (CTO decisions, STATUS/ROADMAP/CHANGELOG, backlog), LESSONS
- [ ] Verification: .NET, worker, lint, contracts, run_all, full E2E (isolated stack), migration copy test
- [ ] Help + manuals, rebuild LAST; test-all.sh full
