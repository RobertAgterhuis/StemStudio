# Hand-off: qa / 010 pre-UI (voice and lyrics fixtures, consent cases, song and voice specs)

Branch `009-010-generative` from 6accc47 (architect Phase 0). Combined with 009 (see
`qa-009-pre-ui.md`: shared support files, page objects, quality and coverage numbers). No 010
backend, UI or singer exists on this branch yet: the specs are **authored, not executed** and skip
with a reason while `GET /api/generation/status` answers 404/405 or the generator/singer is not
`ready`.

## What was built

- **010:T009 (done)** fixtures and strategy:
  - `tests/fixtures/generate_voice_fixtures.py` + committed `tests/fixtures/voice-manifest.json`:
    synthetic "own voice" samples sung by the 006 formant synthesiser (`sung_voice.py`) on
    self-written vowel melodies, CC0, **no real person's voice**: `own-voice-20s.wav` (voiced
    share 0.84, expected check `ok`; Chromium's fake microphone), `own-voice-6s.wav`
    (`too-short`), `own-voice-70s.wav` (`too-long`), `unvoiced-20s.wav` (noise, `unvoiced`),
    `sung-target-10s.wav` (conversion target for generative 010:T043). 44.1 kHz mono PCM16 in the
    git-ignored `tests/fixtures/audio/`; reruns byte-identical (shared generator contract test).
    Tests `tests/fixtures/tests/test_generate_voice_fixtures.py` measure the voicing with an
    independent autocorrelation check (own voice >= 0.7, noise <= 0.05) and derive every expected
    check from length + measured voicing + the contract limits.
  - Self-written lyrics (`tests/fixtures/lyrics/`, CC0): `sample-en.txt` (4 blocks, repeated
    chorus, no markers), `sample-nl.txt` (Dutch with markers), `sample.lrc` (stamps + break) and
    `own-lyrics-expected.json` (words, density, proposed markers, normalised LRC text); lyrics
    README updated.
  - `docs/testing/TEST-STRATEGY.md` **section 16**: what is tested where, the fake microphone
    (Chromium file capture + `microphone` permission; Firefox fake tone), run matrix incl. mode
    `unvoiced-sample` and the SC-006 disk walk, **ethics test cases C-01..C-11** (consent
    required and checked first, consent record, own voice only / exact-copy guard, sample rules,
    "Use my voice" only on AI vocals, converted-take labels, deletion with or without takes, no
    file left on disk, local-only voice data, personal-use notice), fixtures, traceability
    US1..US5 + edge cases (16.5), **pending edits** (16.6) and commands (16.7).
- **Specs authored (010:T033, T038, T052, T056, T057; unticked until executed in T059):**
  - `e2e/tests/song.spec.ts` (`@generative`): paste -> "Add sections" -> "Pop rock" -> generate ->
    `data-vocal="synthetic"`, AI badge, playable, 30 s, file/library labels; section proposal
    (verse, chorus, verse, chorus); take details; empty / 4,097 characters / `.pdf` refused;
    density warning at 10 s but generation allowed (`warnings[]`); imitation in free caption and
    delivery refused; Dutch (`nl`); `.lrc` upload normalised (API + UI); Beginner (expert options
    not rendered); lyrics list: two takes from an edited sheet keep their own text after the sheet
    is deleted; song take imported -> mixer, origin + vocal badge, lyrics link; instrumental
    (`data-vocal="none"`, no "Use my voice", no `AI_VOICE`).
  - `e2e/tests/voice.spec.ts` (`@generative`): consent statement EN + NL with `data-version`;
    Save disabled until sample + ticked statement + both names; API `400 consent-required`,
    `consent-outdated`, missing person name, nothing stored; uploaded profile with its consent
    record and playable sample; `422 too-short|too-long|unvoiced` for every refused fixture + UI
    reason; a downloaded stem and a downloaded take refused `422 library-audio` (API + UI); mode
    `unvoiced-sample`; "Use my voice" (waiting for stems -> `data-vocal="converted"` with the
    profile name, vocal player, same length, `AI_VOICE=converted`); `409 no-vocals` and no button
    on instrumental and restyle takes; delete keeping takes ("(profile deleted)", sample 404) and
    with takes (take audio + library item 404); SC-006 disk walk (`@restart`).
  - `e2e/tests/voice-recording.spec.ts` (`@generative`): record 15 s with the fake microphone ->
    profile `source: recorded`; denied microphone -> `voice-record-unavailable`, upload still
    enabled (Chromium).
  - `generation-status.spec.ts` 010 cases: song generation and voice-profile creation disabled
    with the reason on the default stack (`409 not-installed`, `409 voice-not-installed`), lyrics
    still saved; `no-gpu`: song refused with the reason.

## Files changed

- New: `tests/fixtures/{generate_voice_fixtures.py,voice-manifest.json}`,
  `tests/fixtures/lyrics/{sample-en.txt,sample-nl.txt,sample.lrc,own-lyrics-expected.json}`,
  `tests/fixtures/tests/test_generate_voice_fixtures.py`,
  `e2e/tests/{song,voice,voice-recording}.spec.ts`, `docs/handoffs/qa-010-pre-ui.md`
  (support files and page objects: see `qa-009-pre-ui.md`)
- Changed: `tests/fixtures/pyproject.toml` (coverage source, first-party name),
  `tests/fixtures/tests/test_generator_contract.py` (voice generator added to the shared
  determinism/manifest/CLI contract), `tests/fixtures/lyrics/README.md`

## Tests and coverage

```text
uv run --project worker python tests/fixtures/generate_voice_fixtures.py
  wrote 5 voice fixtures to tests/fixtures/audio
uv run --project worker pytest -c tests/fixtures/pyproject.toml --cov --cov-branch \
  --cov-config=tests/fixtures/pyproject.toml tests/fixtures/tests
  248 passed
  generate_voice_fixtures.py     80 stmts   8 branches  100 % line, 100 % branch
  TOTAL (all generators)       1054 stmts 114 branches  100 % line, 100 % branch
e2e: lint + typecheck clean; playwright --list 573 tests in 41 files; stack-free self-tests 36 passed
```

Not run: the 010 specs against a stack (no 010 backend/UI/singer). E2E DoD open until CTO 010:T059.

## Quality checks

As `qa-009-pre-ui.md`: eslint/tsc clean, ruff/ruff format/mypy --strict clean, sig_check 0 errors
(8 spec-callback size warnings, hard max respected), run_all 0 errors.

## Dependencies added

None.

## Open issues / requests

- **generative (010:T010/T011, T039-T042)**: `FakePitchTracker` on `own-voice-20s.wav` must give a
  voiced ratio >= 0.5 and on `unvoiced-20s.wav` (noise) < 0.5 (the qa reference check measures
  0.88 and 0.00); duration limits are checked for real on the fake singer too (`own-voice-6s`,
  `own-voice-70s`); conversion takes carry `AI_VOICE=converted`; instrumental song takes carry no
  `AI_VOICE` tag. `sung-target-10s.wav` + `own-voice-20s.wav` are the inputs for the GPU test
  010:T043.
- **backend (010:T027-T049)**: the specs expect `POST /api/lyrics/upload` -> `201` with the
  normalised text of `own-lyrics-expected.json` (`sample.lrc`), `415` for a `.pdf`; a 4,097-
  character text answered `400` with "4096" in the problem; `createSong` returns `202` with a
  non-empty `warnings[]` for 93 words in 10 s; free caption/delivery imitation -> `400
  imitation` keyed `freeCaption` / `delivery`; consent checks before anything is stored (the spec
  asserts no new profile); `library-audio` for a downloaded `vocals` stem and for a take file;
  after deletion without takes the conversion's `voiceProfileName` ends with "(profile
  deleted)"; `GET /api/generations?kind=song|voiceConversion` newest first.
- **frontend (010:T031-T058)**: `song-lyrics-count` text "n / 4096" (the spec accepts the raw or
  trimmed length); `song-language-select` has the label "English"; `song-lead-voice` option
  values `female|male|any`; `voice-record-timer` shows `m:ss`; `voice-preview` appears after an
  upload or a recording; `take-vocal-badge-*` is rendered with `data-vocal="none"` on
  instrumental takes; the "Use my voice" dialog closes after `take-voice-convert-start`; the
  consent text element contains both language texts verbatim.
- **devops (010:T008)**: the fake singer must run with the same `STEMSTUDIO_FAKE_GENERATOR_MODE`
  as the fake generator (mode `unvoiced-sample` is a singer behaviour); add
  `generate_voice_fixtures.py` to the fixture step of `scripts/test-all.*` and the e2e CI job (the
  voice specs and the Chromium fake microphone need the WAV files).
- **Pending edits to existing e2e files**: TEST-STRATEGY 16.6.
- **CTO**: the task text named `tests/fixtures/voice/own-voice-20s.wav`; the file is generated
  into `tests/fixtures/audio/` like every other generated fixture (git-ignored audio, committed
  manifest). Tasks in `specs/**` were not ticked (architect-owned files).

## Next owner

qa executes 010:T033/T038/T052/T056/T057 when the 010 UI lands, then T058 polish (pending
edits); CTO 010:T059 runs the fake-stack matrix incl. the fake microphone.
