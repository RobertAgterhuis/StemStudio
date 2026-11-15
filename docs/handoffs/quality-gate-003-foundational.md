# Hand-off: quality-gate / 003-foundational (T006, T007)

## What was built
- **Dependency rule for `devices`** (plan.md dependency direction). `PYTHON_ALLOWED["devices"] = {ports, schemas, config}`.
  - `devices` importing `separation`, `detail`, `jobs`, `api` or `main` is a hard error. `devices -> main` is also reported as a cycle, because `main` composes `devices`.
  - No other mapped module allows `devices`, so `separation`, `detail`, `jobs`, `api`, `ingest` and `analysis` importing `devices` are hard errors. They receive the `DeviceRunner` from the composition root.
  - `main.py` stays unrestricted.
  - Before this change `devices` was unmapped. That gave only a warning, and its own outgoing edges were not checked at all.
- **Ownership.** `worker/src/stemstudio_worker/devices/` and `worker/tests/devices/` now map to `stem-separation` in `common.OWNERS`. Before, they fell through to `architect`.
- **Report.** `components.py` adds a `worker/devices` component row. Until stem-separation adds the package it shows 0 units. Before this change the package would have appeared in no production row.
- The README documents the worker dependency rule. The `dependency_check.py` module docstring is updated.

Red to green:
- `96de3ee`: `tests/test_devices_rule.py` with 16 tests, all 16 failed.
- `d602eb1`: rule, ownership and regenerated report. The `devices -> main` case moved into its own test, because the gate correctly reports it as forbidden edge plus cycle.
- `95ad157`: component-row tests, 2 failed.
- `0ee40ee`: `worker/devices` row and regenerated report.

## Files changed
- `tools/quality/dependency_check.py`, `common.py`, `components.py`, `README.md`
- `tools/quality/tests/test_devices_rule.py` (new), `tools/quality/tests/test_components.py`
- `docs/quality/SIG-REPORT.md` (regenerated)
- `specs/003-gpu-acceleration/tasks.md` (T006 and T007 ticked)

## Tests and coverage
```
uv run --directory tools/quality pytest --cov --cov-branch
TOTAL  763 stmts  0 miss  140 branches  0 partial  100%
Required test coverage of 90.0% reached. Total coverage: 100.00%
183 passed
```
Line coverage is 100% and branch coverage is 100% (140/140).

## Quality checks
```
uv run --directory tools/quality ruff check .          -> All checks passed!
uv run --directory tools/quality ruff format --check . -> 25 files already formatted
uv run --directory tools/quality mypy                  -> Success: no issues found in 24 source files
uv run --project tools/quality python tools/quality/dependency_check.py
dependency_check: 0 errors, 0 warnings
uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md
quality gate: 496 files, 3897 units, 24 clones, 1 exemptions, 0 errors, 135 warnings -> exit 0
```
Compared with the report before this wave (490 files, 132 warnings), the 3 new warnings all come from the Phase 0 skeleton (399a81a). All of them are in test code:
- `tests/StemStudio.Api.Tests/Contract/DeviceContractTests.cs:104`: 8 lines duplicated with `DetailContractTests.cs:100`. The path is owned by backend; the code was written by the architect.
- `worker/tests/test_smoke.py:20` `test_health_reports_settings`: 19 LOC (architect).
- `worker/tests/api/test_jobs_api.py:32` `test_health_reports_fake_separator`: 17 LOC (stem-separation).

## Dependencies added
None.

## Open issues / requests
- **stem-separation:** keep `devices/` importing only `ports`, `schemas` and `config`. Inject the runner into `separation` and `detail` from `main.py`, and do not import it from them. Only use torch, pynvml or onnxruntime behind the adapters inside `devices/` (or the existing runtime adapters). The test-code warning in `test_jobs_api.py:32` is a refactor request for when you next touch that file.
- **backend:** extract the shared contract-test helper behind the `DetailContractTests`/`DeviceContractTests` clone when you touch those files. It is test-only, so it is a warning and does not block.
- **T050 (HO:polish) left open:** the gate has to run on the merged wave. The CTO re-runs `run_all.py` after the merge and ticks T050, or calls the quality-gate back for per-owner refactor requests.

## Next owner
CTO (merge). Then quality-gate T050 on the merged branch.
