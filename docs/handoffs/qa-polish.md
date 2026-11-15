# Hand-off: qa / polish (T110, T118)

## What was built

- **T110** `docs/testing/AUDIT.md`: audit of the hand-offs merged into `001-stemstudio-mvp` at
  audit time (architect, audio-ingest, beat-analysis, devops, quality-gate). Findings: F1 a second
  `@pytest.mark.integration` test (audio-ingest, real ffmpeg) against the "exactly one" rule; F2
  two deliberately failing worker cases waiting for CTO decisions; F3 CI command drift for the
  fixture generator and a missing `npm run typecheck` in `e2e-lint`; F4 beat-analysis calibration
  note. backend, frontend, audio-engine and stem-separation are re-audited after T119.
- Generator hardened for the CI invocations (commit `b7fe6e0`): plain `mypy --strict
  tests/fixtures` and `pytest tests/fixtures/tests --cov=tests/fixtures` (devops' ci.yml) now pass
  without the local config; the `__main__` guard is covered.

## Suite inventory

| Spec | Tests per browser | Tag | Task |
|---|---|---|---|
| `e2e/tests/health.spec.ts` | 2 | | T022 |
| `e2e/tests/import.spec.ts` | 9 | | T081 |
| `e2e/tests/mixer.spec.ts` | 6 | | T097 |
| `e2e/tests/main-flow.spec.ts` | 1 + 1 | `@restart` | T107 |
| `e2e/tests/main-flow.real.spec.ts` | 1 | `@real` | T108 |

`npx playwright test --list` -> `Total: 40 tests in 5 files` (Chromium + Firefox);
`npm test` selects 36, `npm run test:real` 2, `npm run test:restart` 2.

## Red -> green evidence (whole wave)

| Pair | Red | Green |
|---|---|---|
| T020 -> T021 | `dff6e55` | `e88ece8`, `8d2230b`, `b7fe6e0` |
| T081, T097, T107, T108 (E2E) | red against the skeleton (missing UI ids), see qa-us1.md | in T122/T123 |

## Files changed

`docs/testing/AUDIT.md`, `tests/fixtures/{generate_fixtures.py,pyproject.toml,tests/test_generate_fixtures.py}`,
`specs/001-stemstudio-mvp/tasks.md` (ticked T110, T118).

## Tests and coverage

```text
$ uv run --project worker pytest -c tests/fixtures/pyproject.toml --cov --cov-branch \
    --cov-config=tests/fixtures/pyproject.toml tests/fixtures/tests -q
tests\fixtures\generate_fixtures.py     165      0     22      0   100%
Required test coverage of 90.0% reached. Total coverage: 100.00%
31 passed in 2.03s

$ uv run --project worker pytest tests/fixtures/tests --cov=tests/fixtures --cov-branch \
    --cov-report=term --cov-fail-under=90 -q                       # devops CI form
TOTAL                                              289      0     22      0   100%
31 passed in 2.04s
```

Generator: line 165/165 = 100 %, branch 22/22 = 100 %.

## Quality checks

```text
ruff check tests/fixtures                                  -> All checks passed!
ruff format --check tests/fixtures                         -> 2 files already formatted
mypy --strict tests/fixtures                               -> Success: no issues found in 2 source files
mypy --config-file tests/fixtures/pyproject.toml tests/fixtures -> Success: no issues found in 2 source files
lizard -C 5 -L 15 -a 4 -w tests/fixtures                   -> exit 0
(e2e) npx tsc --noEmit                                     -> exit 0
(e2e) npm run lint   (eslint . --max-warnings 0)           -> exit 0
```

## Dependencies added

None in this checkpoint (see qa-foundational.md for the e2e devDependencies and licences).

## Open issues / requests

- F1-F3 of `docs/testing/AUDIT.md` (audio-ingest, architect, devops, CTO).
- Contract clarifications requested in qa-us1.md, qa-us2.md, qa-us4.md (toggle state attribute
  `aria-pressed`, native range inputs for faders, save-dialog behaviour of `mix-save`).

## Next owner

CTO: T119 merge. qa in the integration wave: T122 (generate fixtures, `STEMSTUDIO_SEPARATOR=fake
docker compose up -d --build --wait`, `npm test` retries 0 on Chromium + Firefox, then
`docker compose restart api web` + `npm run test:restart`, defects per owner), T123 (real Demucs,
`npm run test:real`, timings, model, cache reuse), and the post-merge re-audit of the remaining
owners.
