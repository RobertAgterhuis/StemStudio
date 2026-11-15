# Hand-off: quality-gate / 005 foundational (005:T006, 005:T007)

Branch `worktree-agent-aec30c722165411e8`, based on 005/007 Phase 0 (d2e06a7). Run in sequence
005:T006 -> 005:T007 -> 007:T006 -> 007:T007 (see `quality-gate-007-foundational.md`).

## What was built

- **Worker rule** (`tools/quality/dependency_check.py`, `PYTHON_ALLOWED["metadata"]`):
  `metadata` may import only `ports`, `schemas`, `config`. No other module has `metadata` in its
  allowed set, so `ingest`, `jobs`, `api`, `separation`, `detail`, `analysis`, `devices` importing
  it fail the gate; `main.py` (composition root) stays unrestricted. `metadata -> main` is
  reported as a forbidden edge and as a cycle.
- **New .NET feature-folder check** (`DOTNET_FOLDER_FORBIDDEN`, `read_folder_edges`,
  `check_forbidden`): it scans every `*.cs` below `src/<folder>/` for references to
  `StemStudio.{Api,Contracts,Domain,Persistence,Web}` in `using` / `global using` /
  `using static` / alias directives and as qualified names in code. Comments, string literals
  (regular, interpolated, verbatim, raw), char literals and `namespace` declarations are ignored.
  `obj/` and generated files are skipped. Each reference is an error at its file and line, and
  the owner comes from the path (backend). Feature 005 entries: `StemStudio.Api/Metadata` and
  `StemStudio.Api/Artwork` -/- `StemStudio.Web`. Note: the csproj rule already forbids
  `Api -> Web`. The folder check is the source-level guard the plan asks for, and it also
  catches anything compiled into those folders by other means.
- **Ownership**: `worker/src/stemstudio_worker/metadata/` and `worker/tests/metadata/` ->
  audio-ingest (`common.OWNERS`).
- **SIG report**: new component row `worker/metadata` (empty until audio-ingest 005:T010+ lands).
- README rule text updated.

## Files changed

`tools/quality/{dependency_check,common,components}.py`, `tools/quality/README.md`,
`tools/quality/tests/{test_metadata_rule.py (new), test_components.py}`,
`docs/quality/SIG-REPORT.md`, `specs/005-artwork-band-info/tasks.md` (T006, T007 ticked).

## Tests and coverage

TDD: failing tests were committed first (d596376, collection failed on the missing
`DOTNET_FOLDER_FORBIDDEN`). The implementation followed (babb225).

```text
uv run --directory tools/quality pytest --cov --cov-branch     (after 005:T007)
283 passed
TOTAL 927 stmts, 0 missed, 184 branches, 0 partial -> 100.00 % line and branch
dependency_check.py 174 stmts / 66 branches, 100 %
```

(After 007:T007: 314 passed, still 100 % line and branch, see the 007 hand-off.)

## Quality checks

```text
uv run --directory tools/quality ruff check .            All checks passed!
uv run --directory tools/quality ruff format --check .   already formatted
uv run --directory tools/quality mypy                    Success: no issues found
uv run --project tools/quality python tools/quality/dependency_check.py
dependency_check: 0 errors, 0 warnings                   (real repository)
uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md
quality gate: 641 files, 4827 units, 30 clones, 1 exemptions, 1 errors, 162 warnings -> exit 1
```

The **1 error is already on the base** and comes from no change of mine: the production clone
`e2e/support/preferences.ts:12` <-> `src/web-interop/src/types.ts:8` (16 lines). The 004 qa wave
(14651f4) mirrored the web-interop preference types into the e2e support file. It was not in
the 004 Phase 0 run (611 files, 0 errors) that the architect quoted. The 007 hand-off has the
final numbers, including the removed test-only clones.

## Dependencies added

None.

## Open issues / requests

- **qa (blocking the gate, base defect from 004)**: remove the 16-line copy of the preference
  types (`Level`, `ThemePreference`, `DensityPreference`, `MotionPreference`, ...) from
  `e2e/support/preferences.ts`. Import them with `import type { ... } from
  '../../src/web-interop/src/types'` (type-only, no runtime edge), or keep only the e2e-specific
  types. Until then, `run_all.py` exits 1 on every branch based on 9678a00.
- **audio-ingest (005:T010+)**: `metadata/` may import only `ports`, `schemas` and `config`. The
  image-host allow-list belongs in `metadata/http.py`, not in `ingest`. `ingest` gets the
  `ImageNormalizer` from `main.py`.
- **backend (005:T016+)**: `src/StemStudio.Api/Metadata/**` and `src/StemStudio.Api/Artwork/**`
  must not mention `StemStudio.Web` outside comments and strings.
- **stem-separation (005:T014/T015)**: the metadata lane in `jobs/` must receive the
  `MetadataLookup` from `main.py`. Importing `metadata` from `jobs` or `api` fails the gate.

## Next owner

quality-gate 007:T006/T007 (done in the same run). After the wave merges: quality-gate 005:T057
(HO:polish).
