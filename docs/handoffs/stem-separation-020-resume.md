# Resume note: lane WORKER-IO (stem-separation + audio-ingest), feature 020

Worktree `E:\PatricksSoundStudio\.claude\worktrees\020-lane-worker-io`, branch `020-lane-worker-io`.
This note covers both owners (audio-ingest points here).

## Done

- Foundational: T016/T017 (insights lane, stores), T018/T019 (allow-list, fetcher).

## Next (in order)

1. T040/T041 quality routes/service/composition/health (stem-separation).
2. T068-T073 renderer (audio-ingest), T074/T075 handout routes/service (stem-separation).
3. T083/T084 setlist templates, T104 timing.

## How to run

- Windows host: `uv run --frozen pytest ...` (WeasyPrint tests skip: no Pango).
- Linux image (WeasyPrint, coverage of `handouts`):
  `.claude/scratchpad/020-round/worker-io/run-linux.sh -m pytest tests/handouts -p no:cacheprovider`
  (image `wio020/worker:test` = local tag of `e2e020/worker:pre020`; pinned WeasyPrint 70 wheels in
  `.claude/scratchpad/020-round/worker-io/linux-site`).
- Logs: `.claude/scratchpad/020-round/worker-io/`.
