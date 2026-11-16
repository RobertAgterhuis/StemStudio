# Hand-off: quality-gate / 012 polish (T068)

Branch `012-lane-quality`. The rules of T009 (`quality-gate-012-foundational.md`) were run on every
012 lane head, because the integrated tree does not exist yet (lanes still in progress, no merge
allowed in this lane). Each head was exported read-only with `git archive` into the scratchpad and
measured with this branch's `tools/quality/run_all.py --root <export>`. The owner was unavailable:
doing T068 as a per-lane preview is a quality-gate decision; the CTO repeats the run on the
integrated tree in T070 (one command, see below).

## What was built

Measured results per lane head (2026-11-16, gate of 3b631dc6):

| Lane head | Files | Units | Clones | Errors | Warnings | Exit |
|---|---:|---:|---:|---:|---:|---:|
| `012-practice-tempo` 010c3e6d (base) | 2222 | 17463 | 252 | 0 | 817 | 0 |
| `012-lane-quality` 3b631dc6 (this lane) | 2223 | 17492 | 252 | 0 | 816 | 0 |
| `012-lane-engine` aa77db0d | 2252 | 17995 | 257 | **2** | 840 | 1 |
| `012-lane-frontend` 02440bca | 2247 | 17680 | 258 | **1** | 825 | 1 |
| `012-lane-backend` a3bdd6be | 2229 | 17529 | 252 | 0 | 817 | 0 |
| `012-lane-devops` 1b9a9e80 | 2224 | 17499 | 253 | 0 | 821 | 0 |
| `012-lane-qa` 010c3e6d (no commits yet) | 2222 | 17463 | 252 | 0 | 817 | 0 |

New component rows on the engine head (production code, % of unit LOC per band):

| Component | Units | LOC | Size % | Complexity % | Params % | Stars | Clones |
|---|---:|---:|---|---|---|---|---:|
| audio-engine | 490 | 2418 | 98.4 / 1.6 / 0.0 | 100.0 / 0.0 / 0.0 | 89.0 / 11.0 / 0.0 | 5 / 5 / 5 | 2 |
| audio-engine/stretch | 79 | 397 | 100.0 / 0.0 / 0.0 | 100.0 / 0.0 / 0.0 | 90.2 / 9.8 / 0.0 | 5 / 5 / 5 | 0 |
| audio-engine/practice | 46 | 261 | 92.0 / 8.0 / 0.0 | 100.0 / 0.0 / 0.0 | 100.0 / 0.0 / 0.0 | 5 / 5 / 5 | 1 |
| soundtouch shim | 14 | 77 | 100.0 / 0.0 / 0.0 | 100.0 / 0.0 / 0.0 | 72.7 / 27.3 / 0.0 | 5 / 5 / 3 | 0 |

Frontend head: Web 1220 units / 5946 LOC (91.7 / 8.3 / 0.0 size), web-interop 136 / 678.

Dependency direction: **PASS on every head** (0 `[dependency]` findings): the transport, mixer,
metronome and karaoke modules do not import `practice/` or `stretch/`, `practice/` does not import
`karaoke/`, and `stretch/processor.ts` imports only `abi`, `constants` and `core`.

## Refactor requests per owner

Errors must be fixed before the lane's HO:polish / merge; warnings are requests for when the code
is touched.

### audio-engine (2 errors, 2 warnings)

1. **error [duplication]** `src/audio-engine/vite.config.ts:13` = `vite.processor.config.ts:14`
   (9 lines: `outDir`/`emptyOutDir`/`sourcemap`/`target`/`minify` block). Production code. Extract
   the shared build options into one module (e.g. `vite.shared.ts` exporting `sharedBuild`) and
   spread it in both configs.
2. **error [duplication]** `src/audio-engine/src/practice/settings.ts:9` = `tests/mocks/practice-rig.ts:9`
   (8 lines). The test mock copies production settings; import them from `src/practice/settings`
   (or a shared fixture) instead of copying.
3. warning [unit-size] `src/practice/controller.ts:236` `liveChange` 21 LOC (limit 15): split the
   anchor computation from the scheduling of the rate/pitch change.
4. warning [unit-size] `src/engine.ts` `dispose` 17 LOC (limit 15).

### frontend (1 error, 3 warnings)

1. **error [parameters]** `tests/StemStudio.Web.Tests/Mixer/Practice/SpeedTrainerPanelTests.cs:204`
   `Status_ShowsEveryState` has 5 parameters (max 4; the limit applies to test code too). Use one
   `[MemberData]` row record (e.g. `TrainerStatusCase`) or split the theory.
2. warning [unit-size] `src/StemStudio.Web/Components/Mixer/Mixer.razor.cs` `StartEngineAsync` 16 LOC.
3. warning [razor-code] `src/StemStudio.Web/Components/Shell/ShortcutOverlay.razor` `@code` 32 lines
   (limit 30): move it to `ShortcutOverlay.razor.cs`.
4. warning [complexity] `src/StemStudio.Web/Help/HelpRenderer.cs` `Rewrite` CCN 7 (limit 5).

### devops (warnings only)

`scripts/tests/test_build_soundtouch.py` `lock_text` 16 LOC, `test_package_writes_the_lgpl_distribution_folder`
17 LOC; `scripts/tests/test_check_soundtouch_served.py` `site` 17 LOC; a 7-line clone between the two
test files (shared helper in a `conftest.py`).

### backend, qa

None (backend adds no finding; qa has no commits yet).

New test-code warnings of the engine and frontend lanes (unit size 16-26 LOC in Vitest/xUnit
bodies, clones between `shortcuts-practice.test.ts` and `shortcuts.test.ts`, between
`tests/mocks/worklet-node.ts` and `worklet-scope.ts`) are reported in the regenerated report; no
action required.

## Files changed

- `docs/handoffs/quality-gate-012-polish.md` (this note), `specs/012-practice-tempo/tasks.md` (T068 ticked)

## Tests and coverage

```text
uv run --directory tools/quality pytest --cov --cov-branch     716 passed; line 100 %, branch 100 % (1370 / 282)
uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md
  quality gate: 2223 files, 17492 units, 252 clones, 3 exemptions, 0 errors, 816 warnings -> exit 0
per lane: uv run --project tools/quality python tools/quality/run_all.py --root <git archive of the head>
  (numbers in the table above)
```

## Quality checks

ruff, ruff format, mypy clean (see the foundational note).

## Dependencies added

None.

## Docs impact

None.

## Open issues / requests

- **CTO (T070)**: re-run `uv run --project tools/quality python tools/quality/run_all.py --report
  docs/quality/SIG-REPORT.md` on the integrated tree and commit the report; it must exit 0 after
  the audio-engine and frontend errors above are fixed. The lane heads moved during this run
  (engine and frontend commit every few minutes), so later commits are not covered here.
- **audio-engine, frontend**: the requests above.

## Next owner

audio-engine and frontend (fix the 3 errors), then the CTO (integration, T070).
