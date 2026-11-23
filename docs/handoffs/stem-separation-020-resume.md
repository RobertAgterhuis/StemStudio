# Resume note: lane WORKER-IO (stem-separation + audio-ingest), feature 020

Worktree `E:\PatricksSoundStudio\.claude\worktrees\020-lane-worker-io`, branch `020-lane-worker-io`.
This note covers both owners (audio-ingest points here).

## Done (all lane tasks)

- stem-separation: T016/T017, T040/T041, T074/T075 (hand-offs foundational, us1, us3).
- audio-ingest: T018/T019, T068-T073, T083/T084, T104 (hand-offs foundational, us3, us4, polish).
- G2 T088-T092: deferred, not built.

## Open

- Integration: confirm the analyzer class names with beat-analysis (`main.QUALITY_ANALYZERS`).
- Questions in the hand-offs: outline order vs the contract example (audio-ingest us3),
  `threadpoolctl`/`fontTools` as direct dependencies (architect).

## How to run

- Windows host: `uv run --frozen pytest ...` (WeasyPrint tests skip: no Pango).
- Linux image (WeasyPrint, coverage of `handouts`):
  `.claude/scratchpad/020-round/worker-io/run-linux.sh -m pytest tests/handouts -p no:cacheprovider`
  (image `wio020/worker:test` = local tag of `e2e020/worker:pre020`; pinned WeasyPrint 70 wheels in
  `.claude/scratchpad/020-round/worker-io/linux-site`).
- Logs: `.claude/scratchpad/020-round/worker-io/`.
