# Hand-off: qa / foundational (T019-T022)

## What was built

- **T019** `docs/testing/TEST-STRATEGY.md`: test pyramid, tools and gates per component, fakes per
  port, fake vs real separator and the real-Demucs run policy, E2E scope, the SC-004 method,
  manual checks (YouTube/Spotify import V3 is manual and network-dependent), retries = 0,
  fixture catalogue, traceability matrix acceptance scenario -> test file, commands.
- **T020/T021** fixture generator `tests/fixtures/generate_fixtures.py` + tests. Writes
  `tests/fixtures/audio/` (git-ignored) and the committed `tests/fixtures/manifest.json`
  (bpm, beatsPerBar, beat and downbeat times, duration, sample rate, channels per file):
  `click_120bpm_4-4_12s.wav`, `click_90bpm_3-4_12s.wav`, `music_100bpm_20s.mp3`,
  `music_100bpm_20s.flac`, `upload_4mb.wav` (4,233,644 bytes, 110 BPM, G1), `silence_5s.wav`,
  `corrupt.mp3` (ID3 header + bytes without any MPEG sync), `unsupported.ogg`. Deterministic:
  fixed seeds, ffmpeg `+bitexact` without metadata; a test regenerates and compares SHA-256 of
  every file and checks that the committed manifest is up to date. ffmpeg is called with an
  argument list (resolved via `shutil.which`), never a shell.
- **T022** Playwright project `e2e/`: pinned `@playwright/test` 1.65.1; scripts `test` (all
  except `@real` and `@restart`), `test:real`, `test:restart`, `list`, `lint`, `typecheck`,
  `fixtures`; `playwright.config.ts` with Chromium + Firefox, `retries: 0`, one worker, base URL
  from `E2E_BASE_URL` (default `http://localhost:8080`), API from `E2E_API_URL` (default
  `http://localhost:8081`); page objects in `e2e/pages/` using only ui-test-ids.md ids; an API
  client for set-up (`e2e/support/api.ts`); the SC-004 AudioParam probe
  (`e2e/support/engine-probe.ts`); `tests/health.spec.ts` harness smoke test.

## Red -> green evidence

| Task pair | Failing test commit | Passing implementation commit |
|---|---|---|
| T020 -> T021 | `dff6e55` (collection error: `ModuleNotFoundError: No module named 'generate_fixtures'`) | `e88ece8` (+ `8d2230b` LF manifest fix) |

## Files changed

- `docs/testing/TEST-STRATEGY.md`
- `tests/fixtures/{generate_fixtures.py,manifest.json,pyproject.toml,audio/.gitignore,tests/test_generate_fixtures.py}`
- `e2e/{package.json,package-lock.json,.gitignore,tsconfig.json,eslint.config.mjs,playwright.config.ts}`
- `e2e/support/{env,fixtures,api,controls,engine-probe,test}.ts`, `e2e/pages/*.ts`, `e2e/tests/health.spec.ts`
- `specs/001-stemstudio-mvp/tasks.md` (ticked T019-T022 only)

## Tests and coverage

```text
$ uv run --project worker pytest -c tests/fixtures/pyproject.toml --cov --cov-branch \
    --cov-config=tests/fixtures/pyproject.toml tests/fixtures/tests -q
Name                                  Stmts   Miss Branch BrPart  Cover   Missing
tests\fixtures\generate_fixtures.py     162      0     20      0   100%
Required test coverage of 90.0% reached. Total coverage: 100.00%
30 passed in 1.67s
```

Line 162/162 = 100 %, branch 20/20 = 100 %.

Harness proof against the skeleton (Api and Web started with `dotnet run --urls` on 18081/18080
so the compose ports of other agents stay free; `E2E_BASE_URL`/`E2E_API_URL` pointed there):

```text
$ npx playwright test tests/health.spec.ts
  ✓  1 [chromium] › tests\health.spec.ts:6:3 › health › web front end reports ok (29ms)
  ✓  2 [chromium] › tests\health.spec.ts:12:3 › health › API reports ok (11ms)
  ✓  3 [firefox] › tests\health.spec.ts:6:3 › health › web front end reports ok (28ms)
  ✓  4 [firefox] › tests\health.spec.ts:12:3 › health › API reports ok (10ms)
  4 passed (817ms)
```

## Quality checks

```text
$ uv run --project worker ruff check tests/fixtures          -> All checks passed!
$ uv run --project worker ruff format --check tests/fixtures -> 2 files already formatted
$ uv run --project worker mypy --config-file tests/fixtures/pyproject.toml tests/fixtures
Success: no issues found in 2 source files
$ uvx lizard -C 5 -L 15 -a 4 -w tests/fixtures             -> no warnings (exit 0)
$ (e2e) npx tsc --noEmit                                     -> clean
$ (e2e) npm run lint                                         -> clean
```

`tools/quality/sig_check.py` does not exist in this worktree yet (quality-gate builds it in the
wave); lizard with the constitution limits was run instead.

## Dependencies added

| Name | Version | Licence | URL |
|---|---|---|---|
| @playwright/test | 1.65.1 | Apache-2.0 | https://github.com/microsoft/playwright |
| typescript | 6.0.3 | Apache-2.0 | https://github.com/microsoft/TypeScript |
| eslint | 10.15.0 | MIT | https://github.com/eslint/eslint |
| @eslint/js | 10.0.1 | MIT | https://github.com/eslint/eslint |
| typescript-eslint | 8.72.2 | MIT | https://github.com/typescript-eslint/typescript-eslint |
| eslint-plugin-playwright | 2.12.2 | MIT | https://github.com/playwright-community/eslint-plugin-playwright |
| eslint-plugin-jsdoc | 65.4.2 | BSD-3-Clause | https://github.com/gajus/eslint-plugin-jsdoc |
| @types/node | 22.20.5 | MIT | https://github.com/DefinitelyTyped/DefinitelyTyped |

Playwright browser builds (Chromium, Firefox) are downloaded by `npx playwright install`.
The generator uses numpy, soundfile, pytest, pytest-cov, ruff and mypy from the worker
environment (no new Python dependency); ffmpeg must be on `PATH`.

## Open issues / requests

- ESLint config is `e2e/eslint.config.mjs` (tasks.md says `.js`): the package is CommonJS so
  Playwright and `__dirname` work without ESM loaders; `.mjs` keeps the flat config ESM.
- Coverage of the generator must be run with `-c`/`--cov-config tests/fixtures/pyproject.toml`
  from the repository root (coverage only reads `pyproject.toml` from the working directory).
  devops: use exactly the command above in CI.

## Next owner

qa continues with T081 (import spec). devops: CI `e2e` job can call `npm ci`, `npm run lint`,
`npm run typecheck`, `npm test` in `e2e/` after `npm run fixtures`.
