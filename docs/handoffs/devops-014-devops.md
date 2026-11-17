# Hand-off: devops / 014 T014 + 015 T010 (L2, HO:devops)

Lane L2, branch `014-015-round-lane-l2-worker`. Covers 014 T014 and 015 T010 plus the requests of other lanes
(L3, L6) collected in the orchestrator's pending list. The foundational worker part (014 T030-T033) is in
`stem-separation-014-foundational.md`, the rules (014 T015-T016, 015 T007-T008) in `quality-gate-014-devops.md`.

## What was built

- **Worker image** (`docker/worker.Dockerfile`, both variants): `fonts-dejavu-core` (DejaVu Sans / Bold in
  `/usr/share/fonts/truetype/dejavu`) for the setlist PDFs. `scripts/check-worker-fonts.{sh,ps1}` also require
  `fc-list` to list "DejaVu Sans" and `import reportlab` as the worker user (failing check first: the current image
  has neither the font nor the check). CI job `cpu-image-guard` runs it.
- **Compose** (`docker-compose.yml`): worker `STEMSTUDIO_STRUCTURE_ANALYZER` (empty = automatic: fake with the
  fake separator, else real); api `StemStudio__Practice__SectionDetectOnComplete=${STEMSTUDIO_SECTION_DETECT_ON_COMPLETE:-true}`
  and `StemStudio__Recording__MaxUploadBytes=${STEMSTUDIO_RECORDING_MAX_UPLOAD_BYTES:-268435456}`; web
  `StemStudio__Recording__MaxStreamBytes` with the same value (the browser never streams more than the api takes).
  Header documents the variables.
- **E2E override** (`docker-compose.e2e.yml`): web `StemStudio__Recording__TestLoopback=true`; header documents
  `E2E_RECORDING_LOOPBACK=1`, the fixture generator and the no-detect pass.
- **`.env.example`** (new): every optional compose variable incl. the 014/015 ones; no secrets.
- **Contracts in CI and `scripts/test-all.{sh,ps1}`**: `CONTRACTS_DIR`/`$Contracts` -> `specs/015-play-along-recording/contracts`
  (backend 1.14.0 + `recording-api.openapi.yaml`); `RENDER_VECTORS` -> 015's generator (vectors 1.1.0); new
  `PRACTICE_FLOW_CONTRACTS` = 014: Redocly lint of the four 014 documents with the 014 `redocly.yaml`;
  `check_superset.py` of 014 (default 1.23.0 -> 1.13.0, and `practice-api.openapi.yaml` -> 1.13.0) and of 015
  (default 1.13.0 -> 1.14.0, and `recording-api.openapi.yaml` -> 1.14.0); `merge_full.py --check` (014) and
  `merge_recording_delta.py --check` (015). The 008 worker 1.6.0 lint is replaced by 014's worker 1.8.0 (which
  contains it).
- **Playwright** (`e2e/playwright.config.ts`, `e2e/package.json`): project `recording` (Desktop Chrome,
  `channel: 'chromium'` = full build, `permissions: ['microphone']`, `--use-fake-device-for-media-stream`,
  `--use-fake-ui-for-media-stream`, `grep: /@recording/`; the specs set their own capture WAV per describe);
  project `chromium` gets `grepInvert: /@recording/`; `npm test` also inverts `@recording`. Scripts
  `test:recording` (`--project=recording --grep @recording`), `test:practice-flow` (`@practice-flow`, both
  browsers) and `test:practice-flow-no-detect` (chromium, `@practice-flow` + "scenario 4").
- **CI e2e job**: fixture step also runs `python -m recording.generate_recording_fixtures` (from `tests/fixtures`);
  new steps "Play-along recording specs" (`E2E_RECORDING_LOOPBACK=1 npm run test:recording`) and "Practice flow
  without detection on complete" (api recreated with `STEMSTUDIO_SECTION_DETECT_ON_COMPLETE=false`, then
  `E2E_SECTION_DETECT_ON_COMPLETE=false npm run test:practice-flow-no-detect`, then the api back to default).
  `generator-image` job: jq assertions for the new compose variables and that only the e2e override sets
  TestLoopback (exactly one service).
- **test-all**: new option `--recording-e2e` / `-RecordingE2e` (fixtures, fake stack with the e2e override as
  project `stemstudio-e2e`, `npm run test:recording` with the loopback, `down -v`).
- `tests/fixtures/pyproject.toml`: coverage source `recording.generate_recording_fixtures` (L6 request).

## Files changed

`docker/worker.Dockerfile`, `docker-compose.yml`, `docker-compose.e2e.yml`, `.env.example` (new),
`scripts/check-worker-fonts.{sh,ps1}`, `scripts/test-all.{sh,ps1}`, `.github/workflows/ci.yml`,
`e2e/playwright.config.ts`, `e2e/package.json` (scripts only), `tests/fixtures/pyproject.toml`.

## Tests and checks (run in the lane; no image built, no stack started)

- `docker compose config -q`: base, base+e2e, base+gpu, base+generative, base+generative-fake: all valid. The four
  new jq assertions of CI run locally against `docker compose config --format json`: all `true`.
- Contract checks locally: 014 superset (both), 014 `merge_full.py --check`, 015 superset (both), 015
  `merge_recording_delta.py --check`, 015 `generate_render_vectors.py --check`: exit 0. Redocly 2.78.0 lint of the
  four 014 documents and the two 015 documents: exit 0.
- Acceptance "fails on a broken copy" (SC-008): a copy of 014 1.13.0 with `/api/setlists` renamed -> 014 delta
  superset exit 1 ("changed or missing: paths//api/setlists"); a 015 copy with `/api/recording*` renamed -> exit 1.
- CI YAML and both compose files parse (PyYAML). `bash -n scripts/test-all.sh`; PowerShell parser 0 errors and
  PSScriptAnalyzer 0 findings for `test-all.ps1` and `check-worker-fonts.ps1`.
- `e2e`: `npm run typecheck` and `eslint playwright.config.ts --max-warnings 0` clean; a probe spec confirmed
  `@recording` tests list only in project `recording` and untagged tests only in `chromium`.
- Not run: image build and `check-worker-fonts.sh` against a built image (orchestrator builds at the checkpoint;
  expected: OK Leipzig, OK DejaVu Sans, OK imports).

## Dependencies added

| name | version | licence | url |
|---|---|---|---|
| fonts-dejavu-core (Debian package of the python:3.12-slim base) | distro version | Bitstream Vera Fonts licence + public domain (DejaVu changes) | https://dejavu-fonts.github.io/ |

ReportLab 5.1.0 was locked in Phase 0 (worker `uv.lock`), nothing new in Python or npm.

## Docs impact

Technical manual: `STEMSTUDIO_STRUCTURE_ANALYZER`, `STEMSTUDIO_SECTION_DETECT_ON_COMPLETE`,
`STEMSTUDIO_RECORDING_MAX_UPLOAD_BYTES` (api `StemStudio:Recording:MaxUploadBytes`, web `MaxStreamBytes`),
`.env.example`, DejaVu fonts in the worker image, `scripts/test-all.* --recording-e2e`, the e2e override's
TestLoopback (test stacks only).

## Decisions / open issues

- One variable `STEMSTUDIO_RECORDING_MAX_UPLOAD_BYTES` feeds both the api limit and the web stream limit (no
  mismatch possible); default = `RecordingLimits.DefaultMaxUploadBytes` (256 MiB).
- `.env.example` did not exist; created with the optional variables only (no secret, `ANTHROPIC_API_KEY` stays a
  shell pass-through).
- The 014 T018 practice-flow fixture step is not yet in CI; it is added with T018 (later in this lane).
- `test:practice` (`--grep @practice`) also matches `@practice-flow` (substring); harmless (the CI default `npm
  test` runs both anyway), left unchanged.

## Next owner

Orchestrator (mid-wave checkpoint: build the worker image, run `scripts/check-worker-fonts.sh`, the `recording`
project and the no-detect pass).
