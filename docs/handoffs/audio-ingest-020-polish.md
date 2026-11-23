# Hand-off: audio-ingest / 020 polish (T104)

Lane WORKER-IO, branch `020-lane-worker-io`.

## SC-004 timing (T104)

Measured in the worker image (`e2e020/worker:pre020`, Python 3.12, Pango 1.56.3, with the
pinned WeasyPrint 70.0 wheels; the rebuilt 020 image of devops T012 has the same libraries),
script `.claude/scratchpad/020-round/worker-io/exp/timing.py`, one run each:

| Case | Pages | Time | Budget |
|---|---|---|---|
| Song handout, every ingredient, 2 notation files (4 + 1 pages) | 7 | **0.67 s** | 15 s |
| 20-song setlist handout, every song with the same 2 notation files | 123 | **2.98 s** (336 KiB) | 90 s |

Both well within budget (P0-4 spike: 0.9 s and 8.0 s). The default suite also asserts the
20-song budget (`test_a_20_song_setlist_renders_within_the_budget`, 2.0 s without notation).

## Final checks (whole lane)

- Worker default suite (Windows host): 3983 passed, 27 skipped, 12 deselected; package 99 %
  (the misses are `handouts/fetcher.py` and `handouts/layout.py`, which need Pango).
- Linux image: `tests/handouts` + input guard 98 passed, `handouts` 100 % line / 100 % branch.
- `ruff check`, `ruff format --check`, `mypy --strict src tests scripts` (511 files): clean.
- `tools/quality/run_all.py` (jobs, handouts, api, main + tests): PASS, 0 errors; jscpd output
  read: no production clones; remaining warnings are soft complexity (6-7) in `notation._open`,
  `notation._checked_sizes`, `glyphs.missing_characters`, `document.setlist_view`,
  `input_guard.resolve_contained`, and route-test clones like the 014 ones.
- `worker/scripts/sync_handout_style.py --check`: assets untouched (no edits under `assets/`).

## Docs impact (for T105)

Help/user manual: handouts print the fixed notice on every page, missing characters are listed
as a warning, unreadable notation files become a note page, notation on another paper size is
scaled. Technical: renderer reads only listed files, no network; WeasyPrint needs Pango
(`libpango-1.0-0`, `libpangoft2-1.0-0`, plus `libharfbuzz-subset0`) and DejaVu Sans.
