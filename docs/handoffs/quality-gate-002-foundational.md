# Hand-off: quality-gate / 002-foundational (T006, T007)

## What was built
- **Vendored MSST code is excluded from SIG.** Scope is `worker/src/stemstudio_worker/detail/vendor/**`, as set in plan.md Complexity Tracking and accepted by the CTO on 2026-11-15.
  - `common.EXCLUDED_PATHS` now has the vendor prefix. `sig_check.py` and `dependency_check.py` skip it through `is_excluded`, in the same way as `Migrations/`.
  - `.jscpd.json` ignores `**/stemstudio_worker/detail/vendor/**`. As a second safeguard, `duplication.parse_report` drops any clone where either side is an excluded path.
  - Vendored files add no edges to the worker import graph. Third-party imports inside them can therefore never cause a dependency error.
- **Dependency rule for `detail`.** `PYTHON_ALLOWED["detail"] = {ports, schemas, config}`.
  - `detail` importing its own `vendor`, e.g. `from .vendor.msst import demix`, resolves to `detail -> detail` and is allowed.
  - `detail -> jobs|separation|api|...` is a hard error.
  - `jobs -> detail` stays forbidden, because `jobs` only allows the adapter set.
  - `main.py` stays unrestricted as the composition root.
  - This removes the warning "detail is not in the dependency map".
- **Ownership.** `worker/src/stemstudio_worker/detail/` and `worker/tests/detail/` now map to `stem-separation` in `common.OWNERS`. Before this change they fell through to `architect`.
- **Report.** There is a new `worker/detail` row in the component table in `components.py`. The README documents the vendor exclusion and the worker dependency rule.

Red to green:
- `bbce219`: `tests/test_detail_vendor.py` with 17 tests, 15 failed and 2 passed.
- `d0c930f`: implementation.
- `984022f`: component-row test, 2 failed.
- `9c7a3da`: implementation and regenerated report.

## Files changed
- `tools/quality/common.py`, `dependency_check.py`, `duplication.py`, `components.py`, `.jscpd.json`, `README.md`
- `tools/quality/tests/test_detail_vendor.py` (new), `tools/quality/tests/test_components.py`
- `docs/quality/SIG-REPORT.md` (regenerated)
- `specs/002-detail-separation/tasks.md` (T006 and T007 ticked)

## Tests and coverage
```
uv run --directory tools/quality pytest --cov --cov-branch
TOTAL  763 stmts  0 miss  140 branches  0 partial  100%
Required test coverage of 90.0% reached. Total coverage: 100.00%
166 passed
```
Line coverage is 100% and branch coverage is 100% (140/140).

## Quality checks
```
uv run --directory tools/quality ruff check .          -> All checks passed!
uv run --directory tools/quality ruff format --check . -> 24 files already formatted
uv run --directory tools/quality mypy                  -> Success: no issues found in 23 source files
uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md
quality gate: 366 files, 2657 units, 17 clones, 1 exemptions, 0 errors, 105 warnings -> exit 0
```
Warnings went from 106 to 105. The only one removed is the `detail` unmapped warning. Dependency direction: PASS.

## Dependencies added
None.

## Open issues / requests
- **stem-separation:** keep everything under `detail/vendor/` third-party only, meaning the vendored MSST files plus `LICENSE`/`NOTICE`. Our own wrapper code, such as `runtimes.py` and the MDX STFT wrapper, must live outside `vendor/` so that it stays under the SIG, duplication and coverage gates. Anything put in `vendor/` is invisible to the gate.
- **architect/devops (info):** the worker's own ruff, mypy and coverage exclusions for `detail/vendor/**` (T002) match this exclusion. No CI change is needed, because `run_all.py` is still the single command.

## Next owner
CTO (merge). The quality-gate continues with T088 (HO:polish) after the other owners have merged.
