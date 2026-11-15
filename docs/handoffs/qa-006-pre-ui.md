# Hand-off: qa / 006 pre-UI (fixtures, alignment clip, strategy, karaoke and lyrics specs)

Branch `006-008-lyrics-sheet-music` from 62d107e (architect Phase 0). Combined with 008 (see
`qa-008-pre-ui.md`); the shared numbers below are the same run.

## What was built

- **006:T012 (done, ticked)** `tests/fixtures/lyrics/` (README with provenance):
  - LRC files with every T012 edge case (multi-stamp line, `[offset:+250]` and `[offset:-500]`,
    empty stamp = break, blank line, `[mm:ss]`/`.x`/`.xx`/`.xxx`, a self-written Japanese and a
    Hebrew line, padded text, a file without stamps) and the expected parse `lrc/expected.json`.
  - `lyricsfile` YAML in the F-03 shape (word-synced with the 3 s cap case, instrumental, an
    unsafe `!!python/object` tag that `safe_load` must refuse).
  - LRCLIB `/api/get` and `/api/search` responses in the F-01/F-02 shape (real ids, durations and
    record counts from research.md, placeholder text only) with the expected selection per
    duration, ties (album, lowest id), plain-within-15 s, 404 and a non-JSON body.
  - `fake-lyrics.json`: proposed behaviour table of the fake provider and fake aligner per
    `STEMSTUDIO_FAKE_LYRICS` mode (texts, credits, statuses, error text, the word-synced document
    timed for the 20 s fixtures, fake alignment rule `2000 + 500 k` ms).
  - **Alignment clip** (owner question O-2 / CTO decision): `tests/fixtures/generate_lyrics_fixtures.py`
    + `tests/fixtures/sung_voice.py` (own formant singing synthesiser) render
    `align_clip_sung_29s.wav` (isolated lead vocal, 28.97 s, 22 words of a self-written song,
    CC0) into the git-ignored audio folder and the **exact** word annotation
    `tests/fixtures/lyrics/align-clip.json` (committed). Reruns are byte-identical
    (SHA-256 `10be5fa544eca79aac99bf9667af01ebac7198171048c02ad8e568efdf2d4345` on this machine).
  - `docs/testing/TEST-STRATEGY.md` section 13 (what is tested where, landing method, mode matrix,
    fixtures, traceability US1..US3 + FR/SC).
- **Specs authored, not executed (no 006 UI/backend yet; tasks stay unticked):**
  - 006:T039 + T055 `e2e/tests/karaoke.spec.ts` (`@karaoke`): countdown -> singing -> gap
    countdown -> singing -> ended, credit, landing <= 50 ms over 30 s, 50 loops, 20 seeded seeks,
    offset +500 ms (stored, reset), frame rate (Chromium), line sync, instrumental, toggle
    persists across reload, size change keeps accuracy, remove + search again, reduced motion,
    Beginner level.
  - 006:T048 `e2e/tests/lyrics.spec.ts` (`@lyrics`): auto alignment on fake GPU, click-to-align
    with estimate on fake CPU, paste flow, not found, failed + search again, FR-010 (`no-store`,
    no text in `GET /api/tracks`). Each case imports its own WAV copy.
  - New support/page objects: `support/lyrics.ts`, `support/lyrics-api.ts`,
    `support/karaoke-probe.ts` (independent audio-clock reference; reuses the 002 alignment probe),
    `support/lyrics-test.ts` (extends `feature-test.ts`), `pages/karaoke-banner.ts`,
    `pages/lyrics-section.ts`; `npm run test:lyrics`.
  - Stack-free self-test `e2e/tests/lyrics-score-support.spec.ts` (landing maths, fake table,
    MusicXML readers): green.
  - Every 006 case skips with a reason while `GET /api/tracks/{id}/lyrics` answers 404/405.

### SC-003 measurement on the clip (qa, 2026-11-15, CPU, stable-ts 2.19.1 `align`, defaults)

| Model | Word starts within 300 ms | Median error | Transcription check |
|---|---|---|---|
| turbo (production) | **14 / 22 = 63.6 %** | 170 ms | 19 of 22 words recognised |
| base | 13 / 22 = 59.1 % | 220 ms | rough |

The misses are systematic: the word after a held note (2-3 beats) is placed 340-580 ms early
(inside the held vowel). Below the SC-003 target (>= 80 %); see the request to stem-separation.
The fixture was **not** tuned to pass (a variant with decaying held notes scored 59 %; reverted).
Whisper weights were downloaded only into the session scratch folder for this measurement.

## Files changed

- New: `tests/fixtures/{generate_lyrics_fixtures.py, sung_voice.py}`, `tests/fixtures/lyrics/**`,
  `tests/fixtures/tests/{test_generate_lyrics_fixtures.py, test_lyrics_fixtures.py}`,
  `e2e/support/{lyrics.ts, lyrics-api.ts, karaoke-probe.ts, lyrics-test.ts}`,
  `e2e/pages/{karaoke-banner.ts, lyrics-section.ts}`,
  `e2e/tests/{karaoke.spec.ts, lyrics.spec.ts, lyrics-score-support.spec.ts}`,
  `docs/handoffs/qa-006-pre-ui.md`
- Changed: `tests/fixtures/pyproject.toml` (coverage sources, first-party names),
  `e2e/package.json` (`test:lyrics`, `test:sheet-music`, `fixtures` runs the new generators),
  `docs/testing/TEST-STRATEGY.md` (header, sections 13 and 14),
  `specs/006-synced-lyrics-karaoke/tasks.md` (T012 ticked)

## Tests and coverage

```text
uv run --project worker pytest -c tests/fixtures/pyproject.toml --cov --cov-branch \
  --cov-config=tests/fixtures/pyproject.toml tests/fixtures/tests
  216 passed
  generate_lyrics_fixtures.py   120 stmts  12 branches  100 % line, 100 % branch
  sung_voice.py                 135 stmts  12 branches  100 % line, 100 % branch
  generate_score_fixtures.py    172 stmts  22 branches  100 % line, 100 % branch (008)
  TOTAL (all generators)        974 stmts 106 branches  100 %
e2e: npx playwright test tests/lyrics-score-support.spec.ts tests/audio-check.spec.ts tests/panner-law.spec.ts
  26 passed, 8 skipped (panner law is Chromium-only), retries 0
e2e: npx playwright test --list   425 tests in 33 files (was 355 in 29)
```

Not run: the `@karaoke` / `@lyrics` specs against a stack (no 006 UI/backend on this branch);
the E2E DoD (green against the real stack with retries=0) is open until the integration wave
(CTO 006:T065).

## Quality checks

- `ruff check`, `ruff format --check`, `mypy --strict` (tests/fixtures): clean.
- e2e `eslint . --max-warnings 0` and `tsc --noEmit`: clean.
- `tools/quality/sig_check.py` on all new files: 0 errors, 9 warnings (test helpers with CC 6-8 in
  `test_lyrics_fixtures.py` / `test_generate_score_fixtures.py`, one 18-line spec callback).
- `tools/quality/run_all.py`: 886 files, 0 errors, 219 warnings, no duplication warning in a new
  file (two blocks were refactored: the karaoke probe reuses `alignment-probe.ts`; the score
  generator reuses `cli_args`/`write_manifest`).
- No secrets; all lyric text self-written (asserted by `test_lyrics_fixtures.py`).

## Dependencies added

None (numpy, scipy, soundfile, PyYAML already in the worker environment; no npm change).

## Open issues / requests

- **stem-separation (006:T040-T042)**: SC-003 on the CC0 clip is 63.6 % with default stable-ts
  options (target 80 %); words following held notes start ~0.4 s early. Please evaluate a fix
  (e.g. the `align_words` refinement inside segments, or post-processing word starts to the next
  voiced onset) and report the number in your hand-off. Integration test inputs: the clip from
  `generate_lyrics_fixtures.py` and `align-clip.json` (`plainText`, `language`, `sc003`).
  Fake aligner: please implement the `fakeAlignment` rule of `fake-lyrics.json`.
- **audio-ingest (006:T013/T027/T028)**: implement `lyrics/fake.py` to return exactly
  `fake-lyrics.json`; confirm the LRC offset convention in `lrc/expected.json`
  (`startMs = stamp - offset`) or report a different reading.
- **audio-engine (006:T035/T036)**: the probe assumes `data-word-index` counts words inside
  `data-line-index`'s line, and the frame-rate check reads `performance.measure` entries whose name
  starts with `karaoke` (L5). Please confirm or tell qa the names.
- **frontend (006:T037/T046/T053)**: the specs assume `karaoke-offset` accepts
  `setNumericValue` (native input or ARIA slider), `transport-seek` takes seconds, and
  `karaoke-unavailable` replaces the host (host count 0).
- **backend**: specs skip only on 404/405 of `GET .../lyrics`; a track without lyrics must answer
  200 with `status: none` (contract).
- **Pending edits to existing e2e files (after the 004 merge; not touched now)**:
  1. `support/levels.ts`: add the 1.6.0 ids with their Beginner rule (`karaoke-size`,
     `karaoke-offset`, `karaoke-offset-reset`, `lyrics-remove`, `lyrics-align-device` not rendered).
  2. `support/help.ts` / `tests/help.spec.ts`: help topics `karaoke`, `karaoke-offset`,
     `lyrics-sources`, `lyrics-align`.
  3. `pages/mixer-page.ts`: a `seek(seconds)` helper and the banner accessor;
     `pages/track-detail-page.ts`: link to the lyrics card (now in `pages/lyrics-section.ts`).
  4. `support/test.ts`/`feature-test.ts`: optionally fold `lyrics-test.ts` into the main fixtures.
  5. `a11y/screens.a11y.spec.ts` + `support/screens.ts`: mixer with banner, track page lyrics card;
     `visual/` baselines with the banner (reduced motion).
  6. `package.json` `test`: decide whether `@karaoke|@lyrics` stay in the default run (they skip
     without the 006 backend).
- **devops**: optional, add `generate_lyrics_fixtures.py` to the `fixtures generate` step of
  `scripts/test-all.*` (the E2E specs use only the 001 fixtures; the integration test needs the clip).
- **CTO**: owner may replace the synthetic clip by a self-sung CC0 recording later (same
  annotation format); README documents the R-07 limitation.

## Next owner

qa continues with 006:T039/T048/T055 execution once the 006 frontend lands (after the 004 merge);
stem-separation for the SC-003 request; CTO 006:T065 runs `npm run test:lyrics` per mode.
