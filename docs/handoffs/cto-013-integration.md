# Hand-off: CTO / 013 integration wave (T067-T072)

Branch `013-library-care` (worktree `.claude/worktrees/013`), not pushed, not merged into `main`. The owner was
unavailable: every decision is recorded in `docs/decisions/CTO-DECISIONS-2026-11-15.md` (rows "013").

## What was integrated

- All lanes (`013-lane-worker`, `-backend`, `-frontend`, `-qa`) and the CTO helper branches `013-cto-merge` (US5),
  `013-cto-frontend`, `013-cto-security` (T069) and `013-cto-switch` (switch data-integrity fix) are merged `--no-ff`.
- **Switch data-integrity fix** (blocking, found by the Firefox E2E run: an "Undo restore" that failed `io-error`
  left a 0-byte live database). Reviewed and merged (`70c55b3b`); details in `backend-013-switch.md`. Runtime
  connections open the live database read-write without create mode, the status endpoint is counted by the gate,
  move-ins put the database last and only replace fresh, empty interlopers, a rollback ends `rolledBack` only when
  every item is back, start-up repairs the observed state. Api 2143 passed / 2 skipped, 98.58 % line / 91.1 % branch.
  Open points decided (CTO-DECISIONS): sync passes are not counted as requests; an interfering `-wal`/`-shm` moves
  aside with the database; a start-up that cannot roll back stops with a clear text (runbook: technical manual,
  Operations, "Interrupted library switch").

## Findings fixed in this wave (test-first)

| Finding | Fix | Evidence |
|---|---|---|
| Modal dialogs of library care: a fast second Tab left the dialog (the sentinels moved the focus over the Blazor server round trip) | `wwwroot/js/focus-trap.js`: the focus moves inside the browser's focus event (end -> title via `aria-labelledby`, start -> anchor after the last control); the Blazor handlers stay as fallback | a11y `library-care.a11y.spec.ts` 6/6 red -> green; bUnit `FocusTrapScript_WrapsInTheBrowser_AndIsLoadedByTheApp` red -> green |
| Navigation rail taller than a 720 px window after the Library care item: Help cut off and not clickable | `.ss-rail` scrolls vertically | `help.spec` rail opener 10 red -> 54/54 green; visual unchanged |
| Song page 70 px wider than a 320 px viewport (a long style name sized the select) | `.ss-range-row` fields and selects shrink with the row | a11y `robustness.a11y.spec.ts` song 320 px dark/light red -> green |
| Visual: cancelled detail progress of the plain track is 0 % or 13 % depending on the fake worker's timing | `detail-progress` masked like the other dynamic regions | two compare runs green |
| `scripts/test-all.*` ran fixtures mypy over `tests/fixtures/backup` (CI excludes it; it has its own config) | `--exclude '^tests/fixtures/backup/'` as in CI | step green |
| `tools/quality/tests/test_library_care_rules.py` import order (ruff I001 once `library_care_rules.py` existed) | `ruff --fix` | step green |

## Verification

See `docs/STATUS.md` "Feature 013 library care" for the numbers. In short:

- `scripts/test-all.ps1 -ContinueOnError`: every step green except the manuals check before the PDF rebuild (rebuilt
  last; pre-merge gate below). Api 2143 + 2 skips, 98.58 % line / 91.07 % branch; all components >= 90 % line and branch.
  Run it from PowerShell with Git's `bin` folder first in `PATH`; otherwise the WSL stub `bash.exe` from WindowsApps
  fails the 27 devops script tests.
- E2E on the own project `e2e013` (retries 0): library-care fresh 1/1, library-care 126/126, compose 6/6, a11y 162/6
  skipped/0, default suite 616 passed / 115 skipped (its 11 failures: 10 = the rail finding, fixed and rerun green; 1 =
  karaoke L4 timing under parallel load, green alone), visual 116/6 skipped on a fresh stack.
- T068 quickstart V1-V8, V10 and T070 (scaled down, 327 MB archive, 127-155 MB/s) as recorded in STATUS.

## Follow-ups (not done in this wave)

- **V9 / T070 on the reference machine** with a ~20 GB real library (owner).
- **Restore step 8** "every referenced file exists in staging": deferred (spec decision, see CTO-DECISIONS).
- **Recovery log line**: start-up recovery of a journal writes no log line (quickstart V10 expects one). Owner: backend.
- **Merge crash leftovers**: files a merge copied into live folders before a crash stay until the Storage "leftovers"
  clean-up removes them; consider removing them at start-up. Owner: backend.
- **Agent runs active in an archive** come back as running after a restore or merge (the reconcile step covers tracks,
  assets and generations only). Owner: backend + ai-agent.
- **Focus trap** lives in a plain script; move it into web-interop if more dialogs need it. Owner: frontend.
- **Flakes** (separate flake agent): `ClaudeKeyLeakTests.StoredSentinel_NeverLeaks` (known), `AgentEndpointsTests.Executor_*`
  (`ObjectDisposedException` under load, green alone).

## Cleanup

- Compose project `e2e013` torn down with `down -v`, its `e2e013/*` images removed. The owner's `stemstudio` project,
  images and volumes were never touched.
- Helper worktrees and branches removed after their merge: `013-merge`/`013-cto-merge`, `013-sec`/`013-cto-security`,
  `013-fe`/`013-cto-frontend`, `013-switch`/`013-cto-switch`. Lane worktrees and `013-library-care` are left for the
  orchestrator.
