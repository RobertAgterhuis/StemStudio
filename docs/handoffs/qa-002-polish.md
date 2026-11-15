# Hand-off: qa / 002 polish (T086)

## What was built

- `docs/testing/TEST-STRATEGY.md`: E2E scope and the US1..US5 traceability matrix now name the
  final spec files (`detail.spec.ts` "auto", `detail-mixer`, `detail-progress`,
  `detail-split-further`, `detail-mixes`, `detail-worker-restart`).
- `docs/testing/AUDIT.md`: "Feature 002 wave audit" with findings D1 (integration-test count:
  001 F1 still open), D2 (`tracks-empty`/`track-link` used by the 001 `retry-delete.spec.ts` are
  not in any ui-test-ids version), D3 (spec US4 vs ids for cancelled -> Split further).
- Repo-wide SIG run found one clone error caused by qa (`DetailStemName` union duplicated
  `src/audio-engine/src/types.ts`); fixed by deriving the type from a const list.

## Commits (whole qa 002 sequence)

| Task | Commits |
|---|---|
| T009 (red) | `0497a9a` |
| T010 (green) | `4b6ecb4` |
| T011 | `dd466a0` |
| HO:foundational | `625d768` |
| T057 + HO:us1 | `e32e5e9`, `a122eb4` |
| T064 + HO:us2 | `51be5e5`, `d6564a6` |
| T071 + HO:us3 | `909a8af`, `c3aeef3` |
| T076 + HO:us4 | `8cfad43`, `0a3233b` |
| T081 + HO:us5 | `35c98af`, `32ec6d2`, `c8316e2` |
| T086 | `64c76c0`, `b526343`, this note |

## Tests and coverage

```text
$ uv run --project worker pytest -c tests/fixtures/pyproject.toml --cov --cov-branch \
    --cov-config=tests/fixtures/pyproject.toml tests/fixtures/tests -q
tests\fixtures\generate_detail_fixtures.py     237      0     20      0   100%
tests\fixtures\generate_fixtures.py            165      0     22      0   100%
TOTAL                                          402      0     42      0   100%
86 passed
$ (e2e) npx playwright test --list            -> Total: 68 tests in 12 files
$ (e2e) npx playwright test --list --grep @detail -> 26 tests (13 x Chromium/Firefox) in 6 files
```

## Quality checks

```text
ruff check / ruff format --check / mypy --strict tests/fixtures -> clean
(e2e) npm run lint -> 0 problems; npx tsc --noEmit -> clean
uv run --project tools/quality python tools/quality/run_all.py   (whole repo)
quality gate: 381 files, 2852 units, 17 clones, 1 exemptions, 0 errors, 105 warnings -> exit 0
```

## Dependencies added

None in the whole qa 002 sequence.

## Open issues / requests

- **CTO (T091)**: `npm run fixtures`; stack with `STEMSTUDIO_SEPARATOR=fake` and
  `STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5`; `npm test` (includes `@detail` except the worker
  restart), then `npm run test:worker-restart` on the Docker host. retries = 0.
- **devops**: add the detail generator to the CI `e2e` job and set the step-seconds env there.
- **architect**: D2, D3 in `docs/testing/AUDIT.md`.
- qa re-audits the other owners' 002 tests and coverage on the merged branch after T089.

## Next owner

CTO (T089-T091).
