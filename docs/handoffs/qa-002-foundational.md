# Hand-off: qa / 002 foundational (T009-T011)

## What was built

- **T009/T010** `tests/fixtures/generate_detail_fixtures.py` + tests. Writes 13 deterministic
  10 s, 44.1 kHz stereo PCM16 WAVs into `tests/fixtures/audio/` (git-ignored, the same policy as
  001: audio is generated, only the manifest is committed) and the committed
  `tests/fixtures/detail-manifest.json`:
  - `detail_silent_parent.wav` (noise at -79.97 dBFS, 0 % active frames) and
    `detail_bleed_parent.wav` (4.08 % active frames): expected `skipped-silent`.
  - `detail_drums_{kick,snare,hihat}.wav` + `detail_drums_sum.wav` (expected `split` ->
    kick -4.07 dB, snare -4.59 dB, hihat -6.02 dB, rest), `detail_vocals_{lead,backing}.wav` +
    `detail_vocals_sum.wav` (lead -0.33 dB, backing -11.38 dB, backing panned left),
    `detail_instruments_{strings,brass}.wav` + `detail_instruments_sum.wav` (parent `other`;
    strings -0.46 dB, brass -9.71 dB). Every sum is the exact 16-bit sample sum of its
    components (reconstruction error recorded as the -200 dB floor = exact).
  - `detail_10s_band.wav`: the 10 s input of the detail integration test (T050) and of every
    `@detail` E2E spec.
  The manifest records level and active share per file, per-component energy and active share,
  the expected outcome per parent and the research.md D-04 constants (`gating`) it was derived
  with. The tests re-implement the D-04 measurements independently and check the manifest within
  0.1 dB.
- Refactor: the determinism, committed-manifest and command-line tests of both generators moved
  into one parametrised `tests/fixtures/tests/test_generator_contract.py` plus a shared
  `conftest.py` (`generated` fixture), which removed every duplicated block between the two test
  files.
- **T011** `docs/testing/TEST-STRATEGY.md`: detail fakes per port, the second integration test
  (one per model family), `@detail` E2E scope and stack settings
  (`STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5`, per-run unique audio, opt-in `@worker-restart`), the
  browser alignment probe, the detail fixture catalogue and the US1..US5 traceability matrix.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T009 -> T010 | `0497a9a` (collection error: `ModuleNotFoundError: No module named 'generate_detail_fixtures'`) | `4b6ecb4` |

T011 (docs): `dd466a0`.

## Files changed

- `tests/fixtures/{generate_detail_fixtures.py,detail-manifest.json,pyproject.toml}`
- `tests/fixtures/tests/{conftest.py,test_generate_detail_fixtures.py,test_generator_contract.py,test_generate_fixtures.py}`
- `docs/testing/TEST-STRATEGY.md`
- `specs/002-detail-separation/tasks.md` (ticked T009-T011 only)

## Tests and coverage

```text
$ uv run --project worker pytest -c tests/fixtures/pyproject.toml --cov --cov-branch \
    --cov-config=tests/fixtures/pyproject.toml tests/fixtures/tests -q
Name                                         Stmts   Miss Branch BrPart  Cover   Missing
tests\fixtures\generate_detail_fixtures.py     237      0     20      0   100%
tests\fixtures\generate_fixtures.py            165      0     22      0   100%
TOTAL                                          402      0     42      0   100%
Required test coverage of 90.0% reached. Total coverage: 100.00%
86 passed in 10.50s
```

Line 237/237 = 100 %, branch 20/20 = 100 % for the detail generator (001 generator unchanged at
100 %/100 %).

## Quality checks

```text
$ uv run --project worker ruff check tests/fixtures            -> All checks passed!
$ uv run --project worker ruff format --check tests/fixtures   -> 6 files already formatted
$ uv run --project worker mypy --config-file tests/fixtures/pyproject.toml tests/fixtures
Success: no issues found in 6 source files
$ uv run --project worker mypy --strict tests/fixtures         -> Success (CI form)
$ uv run --project tools/quality python tools/quality/run_all.py tests/fixtures
quality gate: 6 files, 124 units, 0 clones, 0 exemptions, 0 errors, 1 warnings -> exit 0
```

The one warning is `detail:0 [dependency] detail is not in the dependency map` (quality-gate,
T006/T007, not in qa paths).

## Dependencies added

None (numpy, soundfile, pytest come from the worker environment).

## Open issues / requests

- **devops (T008/T087)**: CI `e2e` job generates only `generate_fixtures.py`; add
  `uv run --project worker --frozen python tests/fixtures/generate_detail_fixtures.py` before the
  Playwright step, and start the E2E stack with `STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5` (the
  `@detail` specs size their windows on it; `E2E_FAKE_DETAIL_STEP_SECONDS` tells the suite).
- **stem-separation (T050)**: `tests/fixtures/audio/detail_10s_band.wav` is available as the
  integration input; TEST-STRATEGY G6 still lets you generate your own instead.
- Note: the committed manifest stores dB values rounded to 0.01; numpy's transcendental
  functions can differ in the last ulp between platforms, which could in principle flip a
  rounding digit on Linux CI. If `test_committed_manifest_is_up_to_date[detail]` ever fails only
  on CI, regenerate there and report it to qa.

## Next owner

qa continues with T057 (`detail.spec.ts` "auto"); CTO consumes the fixtures in T091.
