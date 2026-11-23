# CTO 020 integration: resume note

Branch `020-insights-handouts` (worktree `.claude/worktrees/020-insights-handouts`). Logs:
`.claude/scratchpad/020-round/integration/`. Binding: `.claude/scratchpad/020-round/orchestrator-log.md`.

## Done
- [x] Merges (--no-ff): qa-ops, beat, worker-io, backend, frontend (tasks.md union), main bf7df94e
- [x] Contracts c5cb7f5f (backend 1.19.0-delta.3, worker 1.11.0-delta.3; merge_full --check superset yes)
- [x] Backend 818f4683/80f6de98: no write gate on the 3 writes, 409 handouts-active, options 409 too-many-songs,
      HandoutJobLimits (4 worker limits, 422 document), warning enums notation-omitted/artwork-omitted
- [x] Flake LyricAssistEdgeTests 46c265f7 (+ FLAKES.md row)
- [x] Wording review 4b8668fa; MaxSetlistHandoutSongs validated 1-50 b04ebc28
- [x] Docs agent: b1f998c0 (data-model/research/licences), c4cdf03f (help + manuals sources), 0bde9017 (central logs, LESSONS)
- [x] Copy-first migration test (scratch `integration/migration/`): Tracks 13, Mixes 4, Assets 15 before = after =
      after down+up; integrity ok; rollback = `MigrateAsync("20261118055631_AddTrackAnalyses")`, verified
- [ ] Worker agent: warning codes in worker, threadpoolctl/fontTools pins, libharfbuzz-subset0, dependency map `insights`

## Todo (in order)
- [ ] Full .NET coverage run (running), then the full E2E on stack `e2e020i` (39080/39081), images e2e020i/*:local
- [ ] Fix E2E findings test-first
- [ ] Manuals rebuild LAST (`node tools/docs/build.mjs`, `check.mjs`), then `scripts/test-all.sh` (full)
- [ ] cto-020-integration.md hand-off; tear down e2e020i
