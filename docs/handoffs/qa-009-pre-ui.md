# Hand-off: qa / 009 pre-UI (strategy, ethics cases, style and restyle specs, page objects)

Branch `009-010-generative` from 6accc47 (architect Phase 0 for 009 + 010). Combined with 010
(see `qa-010-pre-ui.md`); the shared numbers below are the same run. No 009 backend or UI exists
on this branch yet, so the specs are **authored, not executed**; they skip with a reason while
`GET /api/generation/status` answers 404/405.

## What was built

- **009:T011 (done)** `docs/testing/TEST-STRATEGY.md`: header paragraph and **section 15**: what
  is tested where (generator fakes, CI-skipped GPU integration test, worker file lease, backend,
  bUnit, E2E), the fake generator modes and run matrix (`ready`, `oom`, `oom-twice`, `busy`,
  `slow`, `no-gpu`; `@styles`, `@generation-status`, `@generative`, opt-in `@restart`), the
  take checks every completed case runs (SC-005), **ethics test cases E-01..E-08** (no artist
  imitation, AI labels, personal-use notice), fixtures and the traceability US1..US6 + FR/SC ->
  test files (15.5).
- **Fixture tables** (`tests/fixtures/generative/`, README with provenance, CC0):
  `ethics-cases.json` (refused imitation texts: `spec` cases quote the spec, `qa-proposal` cases
  are soft in the E2E run; accepted genre names; forbidden preset phrases; a library-artist case
  using the 005 fixture artist "StemStudio Fixtures", no real artist; FLAC tag list; consent
  statement), `fake-generator.json` (expected behaviour per fake mode, qa proposal for
  generative 009:T020/T021). Checked by `tests/fixtures/tests/test_generative_fixtures.py`.
- **Specs authored (009:T045, T050, T057, T062, T067; tasks stay unticked until executed in T081):**
  - `e2e/tests/styles.spec.ts` (`@styles`, default stack): >= 24 presets in >= 8 families,
    read-only (`403 preset-read-only`, no edit/delete ids), presets first, no imitation phrase in
    any preset (SC-003), mood filter + search + empty state, duplicate -> "(copy)" -> delete with
    confirm, field limits and `409 style-name-taken`, editor field error for an imitation phrase,
    API loop over every refused case, genre words accepted (no false positives), library artist
    refused.
  - `e2e/tests/generation-status.spec.ts`: `@generation-status` (default stack, `notInstalled`):
    banner + reason + help link, `track-restyle` disabled with `aria-describedby` -> reason,
    `409 not-installed`, the library stays usable; `@generative` mode `no-gpu`: `unavailable`
    with the probe reason, `409 gpu-required` with the reason.
  - `e2e/tests/generative.spec.ts` (`@generative`): "Jazz trio" in four interactions (SC-007)
    with progress, playable take (`readyState >= 2`), AI badge, details and the file/library
    labels; options (range 2-14, no vocals, strength 1, two takes with distinct seeds); in place
    (take = track length); range/takes limits; own style + snapshot kept after the style is
    deleted; Beginner (options not rendered); import take (mixer, origin badge, source link,
    list badge, second import `200` same track); delete generation (take audio and library item
    404, imported track kept); cancel (`slow`, lease released); `oom-twice` (readable failure,
    retry -> new generation); `busy` (`waitingForGpu` -> "GPU busy: needs X GB, Y GB free");
    generator restart -> `interrupted` -> retry (`@restart`, needs `E2E_GENERATIVE_COMPOSE_FILES`).
  - Stack-free self-test `e2e/tests/generative-support.spec.ts` (FLAC Vorbis-comment reader,
    mode parsing, fixture tables, fake microphone options): **18 passed** (Chromium + Firefox).
- **New support and page objects** (no existing page object, fixture or level file was edited):
  `support/{generative,generation-api,voice-api,generative-flow,generative-test,flac-tags,media,fake-microphone,generator-stack}.ts`,
  `pages/{test-id-page,generation-status,restyle-dialog,generations-list,style-library-page,style-editor-page,generated-track,song-page,lyrics-list-page,voice-profiles-page,use-my-voice-dialog}.ts`;
  `package.json` scripts `test:styles`, `test:generative`, `test:generative-restart`,
  `fixtures:generative` (new lines only).

## Files changed

- New: `tests/fixtures/generative/{README.md,ethics-cases.json,fake-generator.json}`,
  `tests/fixtures/tests/test_generative_fixtures.py`, the e2e files above,
  `e2e/tests/{styles,generation-status,generative,generative-support}.spec.ts`,
  `docs/handoffs/qa-009-pre-ui.md`
- Changed: `docs/testing/TEST-STRATEGY.md` (header, sections 15-16), `e2e/package.json` (4 scripts)

## Tests and coverage

```text
uv run --project worker pytest -c tests/fixtures/pyproject.toml --cov --cov-branch \
  --cov-config=tests/fixtures/pyproject.toml tests/fixtures/tests
  248 passed (was 216)
  generate_voice_fixtures.py     80 stmts   8 branches  100 % line, 100 % branch (010)
  TOTAL (all generators)       1054 stmts 114 branches  100 % line, 100 % branch
e2e: npx playwright test tests/generative-support.spec.ts tests/lyrics-score-support.spec.ts --project=chromium --project=firefox
  36 passed, retries 0
e2e: npx playwright test --list   573 tests in 41 files (was 447 in 34); the 7 new 009/010 files: 126 tests
  (63 per browser), 80 tagged @generative, 2 opt-in @restart
```

Not run: the 009 specs against a stack (no 009 backend/UI/generator on this branch). The E2E DoD
(green against the real stack with retries=0) stays open until the integration wave (CTO
009:T081, per fake mode, command list in TEST-STRATEGY 16.7).

## Quality checks

- e2e `eslint . --max-warnings 0` and `tsc --noEmit`: clean.
- `ruff check`, `ruff format --check`, `mypy --strict` (tests/fixtures): clean.
- `tools/quality/sig_check.py` on the 30 new files: 0 errors; 8 unit-size warnings, all spec test
  callbacks of 16-24 lines (hard max 30) in `generative.spec.ts`, `song.spec.ts`, `voice.spec.ts`.
- `tools/quality/run_all.py`: 1272 files, 0 errors, exit 0; no duplication warning in a new file
  (the one clone found in `generate_voice_fixtures.main` was removed).
- No secrets; no real artist, song or person in any fixture or spec.

## Dependencies added

None (no npm or Python change).

## Open issues / requests

- **generative (009:T020/T021, T032/T033)**: please make the fake engine follow
  `tests/fixtures/generative/fake-generator.json`, in particular: the `no-gpu` reason mentions
  GPU or CUDA; `busy` fails with `gpuBusy` and "GPU busy: needs X GB, Y GB free" (after
  `STEMSTUDIO_GPU_MEMORY_WAIT_SECONDS`, 5 s on the fake stack); `oom-twice` fails `outOfMemory`
  with a message containing "memory"; `slow` stays `running` for several seconds; a restart ends
  running jobs as `interrupted`. The specs read the take tags `AI_GENERATED=true`, `GENERATOR`,
  `STEMSTUDIO_GENERATION`, `STEMSTUDIO_TAKE` and expect **`STEMSTUDIO_TAKE` = the take index**
  ("1", "2"); tell qa if it is something else.
- **backend (009:T024, T037-T042, T046, T053, T058, T063)**: the guard cases of
  `ethics-cases.json` may be reused as test vectors; please report any `qa-proposal` case you do
  not refuse (Dutch phrases, extra spaces, upper case, diacritics). The specs expect: problem
  type `imitation` with `errors` keyed by the camelCase field (`name`, `description`,
  `subGenre`); `409 style-name-taken` case-insensitive; `403 preset-read-only` on `PUT`/`DELETE`
  of a preset; `GET /api/generations?sourceTrackId=` newest first (used to find a generation
  started in the UI); after `DELETE /api/generations/{id}` the take audio and its library item
  answer 404; `TrackDto.artist` carries the embedded artist (005) for the library-artist case.
- **frontend (009:T043-T077)**: the page objects assume native `<select>` elements
  (`selectOption({ label })`) for `restyle-style-select`, `style-filter-*` (with an "All" option),
  `style-genre-select`; numeric ids (`restyle-range-*`, `restyle-takes`, `restyle-strength`)
  accept `setNumericValue` (native input or ARIA slider); tag inputs take one tag per Enter;
  `aria-describedby` of a disabled control points at the element that contains the reason; the
  take details disclosure contains the style name, seed, source track title and model id; the
  Advanced default range of the dialog is the whole track.
- **devops (009:T009/T010)**: fake compose with `STEMSTUDIO_FAKE_GENERATOR_STEP_SECONDS=1` and
  `STEMSTUDIO_GPU_MEMORY_WAIT_SECONDS=5`; pass `STEMSTUDIO_FAKE_GENERATOR_MODE` through to the
  generator and the fake singer; the CI fixture step should also run
  `generate_voice_fixtures.py` (010).
- **Pending edits to existing e2e files**: TEST-STRATEGY 16.6 (levels, help topics, track page and
  list page objects, nav links, folding `generative-test.ts`, `TrackDto.origin`, a11y/visual
  screens, the default `npm test` grep).
- **CTO (009:T081)**: run `npm run test:styles` on the default stack and `npm run
  test:generative` once per fake mode (TEST-STRATEGY 15.2/16.7); `test:generative-restart` with
  `E2E_GENERATIVE_COMPOSE_FILES` on the `slow` stack.

## Next owner

qa continues with 009:T045/T050/T057/T062/T067 execution once the 009 backend and UI land
(then T078 polish with the pending edits); CTO 009:T081 runs the matrix.
