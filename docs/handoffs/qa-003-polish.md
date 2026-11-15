# Hand-off: qa / 003 polish (T048)

## What was built

- `docs/testing/TEST-STRATEGY.md`: final 003 traceability (US1-1..US3-1, FR-005, FR-009 -> unit,
  component, contract, E2E and manual evidence), E2E scope rows for `device.spec.ts` and
  `gpu.spec.ts` with the `GET /api/device` checks, and the exact commands of the two `@gpu` runs.
- Summary of the 003 E2E additions (all checkpoints):

| Spec / test | Tags | Stack | Asserts |
|---|---|---|---|
| `device.spec.ts` "the import page shows the CPU worker" | `@device` | default (`cpu`) | `/api/device` cpu, no reason; `worker-device` `data-device="cpu"`, no reason element |
| `device.spec.ts` "the track page shows the CPU for both jobs" | `@device` | default | `Track.device`/`DetailPhase.device` cpu, no fallback; `track-device`/`detail-device` `data-device="cpu"`, `data-fallback="false"`, no notice |
| `gpu.spec.ts` "worker device on the import page" | `@gpu` | `gpu` or `gpu-oom` | `/api/device` gpu "Fake GPU"; `worker-device` `data-device="gpu"` "GPU (Fake GPU)", no reason |
| `gpu.spec.ts` "device on the track page" | `@gpu @fake-gpu` | `gpu` | both jobs gpu "Fake GPU", no fallback, in the API and on the page |
| `gpu.spec.ts` "fallback notice" | `@gpu @fake-gpu-oom` | `gpu-oom` | both jobs complete with `cpuFallback` and "ran out of memory" in the API and in `track-device-fallback`/`detail-device-fallback` |

## Commits (feature 003, qa)

| Task | Commit |
|---|---|
| T010 | `6b72128` |
| T033 | `54a297d` (HO:us1 `d7a394e`) |
| T038 | `fa3079b` (HO:us2 `2f7b16d`) |
| T044 | `13abf00` (HO:us3 `eb9957c`) |
| T048 | `2c26404` |

No red/green pairs: the E2E specs are authored in the wave and executed by the CTO in T051.

## Files changed

`docs/testing/TEST-STRATEGY.md`, `specs/003-gpu-acceleration/tasks.md` (T048).

## Tests and coverage

```text
$ (e2e) npx playwright test --list
Total: 78 tests in 14 files      (68 from 001/002 + 10 new = 5 tests x Chromium/Firefox)
$ uv run --project worker pytest -c tests/fixtures/pyproject.toml --cov --cov-branch \
    --cov-config=tests/fixtures/pyproject.toml tests/fixtures/tests
86 passed; generate_detail_fixtures.py 237 stmts / 20 branches, generate_fixtures.py 165 / 22:
TOTAL line 100 %, branch 100 % (fail_under 90)
```

Fixtures unchanged in 003 (the device specs reuse `detail_10s_band.wav` via per-run copies).

## Quality checks

```text
$ (e2e) npm run lint       -> clean (0 warnings)
$ (e2e) npm run typecheck  -> clean
$ uv run --project tools/quality python tools/quality/run_all.py e2e
quality gate: 35 files, 327 units, 0 clones, 0 exemptions, 0 errors, 3 warnings -> exit 0
$ uv run --project worker ruff check tests/fixtures         -> All checks passed!
$ uv run --project worker ruff format --check tests/fixtures -> 6 files already formatted
$ uv run --project worker mypy --config-file tests/fixtures/pyproject.toml tests/fixtures -> no issues
```

The 3 SIG warnings are pre-existing 001 units (`engine-probe.ts` x2, `main-flow.real.spec.ts`).

## Dependencies added

None.

## How the CTO runs the device specs (T051)

From the repository root, after devops T008 passes `STEMSTUDIO_FAKE_DEVICE` to the worker:

```bash
export STEMSTUDIO_SEPARATOR=fake STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5
docker compose up -d --build --wait                    # default: STEMSTUDIO_FAKE_DEVICE=cpu
(cd e2e && npm run fixtures && npm test)               # 001/002 suite + @device
STEMSTUDIO_FAKE_DEVICE=gpu docker compose up -d --wait
(cd e2e && STEMSTUDIO_FAKE_DEVICE=gpu npm run test:gpu)        # import page + track page
STEMSTUDIO_FAKE_DEVICE=gpu-oom docker compose up -d --wait
(cd e2e && STEMSTUDIO_FAKE_DEVICE=gpu-oom npm run test:gpu)    # import page + fallback notice
```

PowerShell: `$env:STEMSTUDIO_FAKE_DEVICE = 'gpu'` before both commands. The suite reads
`E2E_FAKE_DEVICE`, else `STEMSTUDIO_FAKE_DEVICE`, else `cpu`. Each `@gpu` run passes 4 tests and
skips 2 (the other mode's test, annotated); in mode `cpu` every `@gpu` test fails fast with a hint.

## Deviations

- Default CPU assertions are in `device.spec.ts`, not in `main-flow.spec.ts` (cached main-flow
  imports carry no device; see `qa-003-us1.md`).
- `@gpu` tests are also tagged `@fake-gpu` / `@fake-gpu-oom` and gated on the declared mode, so
  the `@gpu` suite needs two stack starts; tasks.md T039 describes a single `gpu-oom` run.

## Open issues / requests

- **devops (T039)**: two `@gpu` runs (`gpu`, `gpu-oom`) with the mode exported to the Playwright
  step as well; see above.
- **stem-separation (T016)**: fake GPU name exactly `Fake GPU`; `gpu-oom` falls back on the first
  step of both the standard and the detail job with the "ran out of memory" reason.
- **frontend (T032/T043)**: `detail-device` inside `detail-section`; `data-fallback` on both job
  badges.

## Next owner

CTO (T051).
