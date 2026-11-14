# Hand-off: qa / integration (T122, T123, re-audit)

## What was built

- **T122** full Playwright suite against the fake-separator stack (`docker compose -p stemstudio-qa`),
  Chromium + Firefox, retries 0, twice on a fresh stack, then `docker compose restart api web` and
  `npm run test:restart` (SC-005 across a restart). Includes the corrupt-upload, `/tracks` -> mixer
  (FR-018), 4 MiB streamed upload (G1) and SC-004 timing specs.
- **Spec fix (qa-owned, not a product defect)**: `e2e/pages/import-page.ts` `uploadFile` now waits
  until Blazor's `InputFile` JS `init` has run (marker `_blazorInputFileNextFileId` on the input)
  before `setInputFiles`. Interactive Server renders the input before the JS listener is attached;
  a selection in that window is lost (seen once on Firefox: `.ogg` reject spec, no `import-error`
  after 10 s, the screenshot shows the file name selected but no request). A human cannot select a
  file within milliseconds of render, so the spec, not the product, was wrong.
- **T123** real Demucs on a fresh stack (`down -v`, `STEMSTUDIO_SEPARATOR=demucs up -d --wait`),
  `@real` on Chromium, plus a 4-minute song and a model-cache reuse check after a second start.
- **Re-audit** of backend, frontend, audio-engine, stem-separation: `docs/testing/AUDIT.md`
  section "Re-audit after T119" (R1-R8), with coverage re-measured by qa.
- `docs/testing/TEST-STRATEGY.md` section 6: the InputFile readiness rule.

## Files changed

`e2e/pages/import-page.ts`, `docs/testing/AUDIT.md`, `docs/testing/TEST-STRATEGY.md`,
`specs/001-stemstudio-mvp/tasks.md` (T122, T123 ticked), this note.

## Tests and coverage

### T122 (fake separator, `npm test` = `--grep-invert "@real|@restart"`, workers 1, retries 0)

Stack: `STEMSTUDIO_SEPARATOR=fake docker compose -p stemstudio-qa up -d --build --wait` (worker
health: `separator: fake, contractVersion 1.0.2`). 18 tests per browser.

| Run | Stack | Chromium | Firefox | Total | Duration |
|---|---|---|---|---|---|
| 1 (before fix) | fresh | 18/18 passed | 18/18 passed | 36 passed | 54.1 s |
| 2 (before fix) | same | 18/18 passed | **17/18** (`rejects an unsupported .ogg file`, import-error not visible) | 35 passed, 1 failed | 53.0 s |
| stress (after fix) `tests/import.spec.ts --repeat-each 10` | same | 90/90 | 90/90 | 180 passed | 2.4 min |
| A (after fix) | `down -v` + fresh `up --build` | 18/18 | 18/18 | 36 passed, 0 failed, 0 flaky | 53.0 s |
| B (after fix) | same | 18/18 | 18/18 | 36 passed (junit: tests=36 failures=0 skipped=0 errors=0) | 41.8 s |
| restart: `docker compose -p stemstudio-qa restart api web`, then `npm run test:restart` | same volumes | 1/1 | 1/1 | 2 passed | 4.4 s |

Specs requested by the CTO, all green on both browsers in runs A and B: corrupt MP3 -> failed +
reason + retry (`import.spec.ts:58`), track list -> track -> mixer (`import.spec.ts:75`), streamed
`upload_4mb.wav` (G1, `import.spec.ts:49`), SC-004 < 50 ms (`mixer.spec.ts:67`).

Note: the Firefox upload specs reuse the source-key cache filled by the Chromium project (same
fixture), so the full fake separation runs on Chromium only per run; the SC-006 cache path is what
Firefox exercises. Accepted (idempotent-suite rule, TEST-STRATEGY section 6).

### T123 (real Demucs, Chromium, `npx playwright test --grep @real --project=chromium`)

Fresh volumes (model cache empty), worker health `separator: demucs, device: cpu`.

| Measurement | Result |
|---|---|
| `@real` main flow | **1 passed** (35.8 s, retries 0) |
| 20 s FLAC fixture, import -> completed incl. first download of `htdemucs_6s` weights (53 MB, HF Hub) | 32.6 s (spec annotation); track `createdAt` -> `updatedAt` 31.4 s |
| Model | `htdemucs_6s` |
| Stems | 6: vocals, drums, bass, guitar, piano, other; each `GET /api/tracks/{id}/stems/{name}` -> 200 `audio/flac`, ffprobe: flac 44100 Hz stereo 20.0 s; played in the mixer (master meter > -60 dB) and the mix saved/reloaded with every setting equal |
| 4-minute song (scratchpad clip, 240 s, warm model, upload via API) | completed in **159.6 s** (2 min 40 s, CPU), `htdemucs_6s`, 6 stems |
| Second start (`down` without `-v`, `up` again), new 20 s clip | completed in 25.4 s; weights blob timestamp 22:58:50 < container start 23:05:49 -> cache reused, no re-download; 3 tracks persisted |

Stack torn down at the end: `docker compose -p stemstudio-qa down -v` (no stemstudio containers left).

### Re-audit coverage (re-measured, see AUDIT.md)

```text
Domain       173 passed  97.73 % line / 97.72 % branch
Persistence   21 passed  99.03 % / 100 %
Api          220 passed  Api 99.88 % / 98.18 %, Contracts 100 % / 100 %
Web          184 passed  99.77 % / 94.36 %
worker       413 passed, 2 deselected  1591 stmts / 188 branches 100 % / 100 %
audio-engine 121 passed  lines 100 % (432/432), branches 100 % (147/147)
```

qa's own component (fixture generator) is unchanged since qa-polish: 31 passed, 100 % line
(165/165), 100 % branch (22/22).

## Quality checks

```text
(e2e) npm run lint       (eslint . --max-warnings 0) -> exit 0
(e2e) npm run typecheck  (tsc --noEmit)              -> exit 0
```

## Dependencies added

None.

## Open issues / requests (defects)

No product defect was found: every failure traced back to the spec (InputFile race, fixed in the
page object). Findings for owners (details in `docs/testing/AUDIT.md`):

1. **audio-ingest + architect (R1, F1 still open)**: two `@pytest.mark.integration` tests
   (`worker/tests/ingest/test_normalizer.py:245`, real ffmpeg). Either a separate marker or amend
   the "exactly one" rule text.
2. **devops (R2)**: the Persistence branch coverage differs between `scripts/test-all.ps1`
   (83.33 %, devops-polish) and the per-project command (100 %, measured here). Make both use the
   same measurement and state which one CI gates on.
3. **stem-separation (R8, low)**: with cached weights the worker still contacts the HF Hub on every
   start; consider `HF_HUB_OFFLINE=1` when the weights are present, so an offline start is reliable.
4. **frontend (R7, informational)**: no prerendering; the import page has no server HTML before
   the circuit connects.
5. T123 manual remainder for T124: YouTube/Spotify imports (network) are not part of this run.

## Next owner

CTO: review and merge; route items 1-3. Then T124 (manual checks).
