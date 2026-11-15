# Hand-off: quality-gate / 007 foundational (007:T006, 007:T007)

Same branch as `quality-gate-005-foundational.md`. This run follows the 005 rules and extends
the same mechanisms.

## What was built

- **Worker rule** (`PYTHON_ALLOWED["render"]`): `render` may import only `ports`, `schemas` and
  `config`. `jobs` (including `jobs/render_*`, `jobs/lanes.py`), `api` and every other adapter
  (`ingest`, `metadata`, `detail`, ...) importing `render` fail the gate. `render` importing
  `jobs`, `api`, `ingest`, `metadata`, `separation` or `devices` also fails. `main.py` composes it.
- **.NET feature folders** (`DOTNET_FOLDER_FORBIDDEN`, same source-level check as in 005):
  - `StemStudio.Domain/Rendering` -/- `StemStudio.Api`, `StemStudio.Persistence`,
    `StemStudio.Web`, `StemStudio.Contracts`. That is every non-Domain project, consistent with
    the csproj rule "Domain references no StemStudio project". The task names Api and
    Persistence; Web and Contracts are added for consistency.
  - `StemStudio.Api/Assets` -/- `StemStudio.Web` (plan.md line 131 "dependency rules for render/,
    Assets/").
- **Ownership**: `worker/src/stemstudio_worker/render/` and `worker/tests/render/` go to
  audio-ingest. `jobs/render_*` and `jobs/lanes.py` stay with stem-separation through `jobs/`,
  and a test pins that.
- **SIG report**: new component row `worker/render`.
- **Test hygiene**: the new rule tests share `tools/quality/tests/repo_tree.py` (`write_file`,
  `write_module`, `WORKER_PACKAGE`). `pyproject.toml` pytest `pythonpath = [".", "tests"]`.
  `test_css_tokens_check.py` reuses the helper too, which removes the test-only clone that my
  first version created.

## Files changed

`tools/quality/{dependency_check,common,components}.py`, `tools/quality/{README.md,pyproject.toml}`,
`tools/quality/tests/{test_render_rule.py (new), repo_tree.py (new), test_metadata_rule.py,
test_css_tokens_check.py, test_components.py}`, `docs/quality/SIG-REPORT.md`,
`specs/007-export-library/tasks.md` (T006, T007 ticked).

## Tests and coverage

TDD: failing tests were committed first (6dfb73e, 28 failed / 286 passed). The implementation
followed (869f313).

```text
uv run --directory tools/quality pytest --cov --cov-branch
314 passed in 1.6s
TOTAL 927 stmts, 0 missed, 184 branches, 0 partial -> 100.00 % line and branch
(every module 100 %: common, components, css_tokens_check, dependency_check, duplication,
 priorities, razor, report, risk, run_all, sig_check, sonar_args)
```

## Quality checks

```text
uv run --directory tools/quality ruff check .            All checks passed!
uv run --directory tools/quality ruff format --check .   31 files already formatted
uv run --directory tools/quality mypy                    Success: no issues found in 30 source files
python tools/quality/dependency_check.py                 dependency_check: 0 errors, 0 warnings
uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md
quality gate: 643 files, 4840 units, 29 clones, 1 exemptions, 1 errors, 161 warnings -> exit 1
```

Dependency direction in the report: **PASS**. The single error is the existing production clone
`e2e/support/preferences.ts:12` <-> `src/web-interop/src/types.ts:8`, owned by qa and inherited
from the 004 qa wave; see the 005 hand-off. The architect's Phase 0 baseline was 142 warnings;
the warning count here is 161 because the 004 waves are merged into the base.

### Render-vector generator (`specs/007-export-library/contracts/generate_render_vectors.py`)

It lives under `specs/`, which is outside the gate's roots (`src`, `tests`, `worker`, `e2e`,
`tools/quality`) and is owned by the architect, so it is **not** gated. Measured on request:

```text
python tools/quality/sig_check.py specs/007-export-library/contracts/generate_render_vectors.py
error   [parameters] _case has 6 parameters (max 4)                       line 160
error   [unit-size]  gate_cases has 95 lines of code (limit 30)           line 194
warning [complexity] plan 10, _audible 8, _settings 7, _channel_order 6 (limit 5)
warning [unit-size]  _case 18 LOC
sig_check: 1 files, 18 units, 2 errors, 5 warnings
worker ruff: 2 errors (E501 long string, PLR0913 _case)
```

## Dependencies added

None.

## Open issues / requests

- **qa**: the base production clone in `e2e/support/preferences.ts` (see the 005 hand-off). It
  is the only thing that keeps `run_all.py` from exiting 0.
- **architect**: if the vector generator should be gated (it is executable contract tooling),
  turn `gate_cases` into a data table (list of tuples or a JSON-like literal) that a short loop
  feeds to `_case`, and pass `_case` a small `CaseSpec` dataclass instead of 6 parameters.
  Then I add `specs/*/contracts/*.py` to the gate roots (`common.DEFAULT_ROOTS`) and to the
  Sonar sources. Until you decide, it stays out of scope.
- **audio-ingest (007:T011+)**: `render/` may import only `ports`, `schemas` and `config`. The
  path allow-list comes from `ports.is_allowed_library_path`.
- **stem-separation (007:T021+)**: `jobs/render_*` and `jobs/lanes.py` must receive the
  `RenderExecutor` and `LibraryReader` from `main.py`. Importing `render` fails the gate.
- **backend (007:T025+)**: `src/StemStudio.Domain/Rendering/**` uses Domain types only; no
  Contracts DTOs (map them in `Api/Assets`). `src/StemStudio.Api/Assets/**` must not reference
  Web.
- `sonar-project.properties`: no change needed. The new folders sit under the existing
  `src`/`worker/src` sources and `worker/tests`.

## Next owner

After the combined 005/007 wave merges: quality-gate 005:T057 + 007:T070 (HO:polish, `run_all.py`
on the whole merged repo, refactor requests per owner). They are left unticked on purpose. CTO
for the integration merge (`docs/quality/SIG-REPORT.md` will conflict with the 004 branches and
should be regenerated, not hand-merged).
