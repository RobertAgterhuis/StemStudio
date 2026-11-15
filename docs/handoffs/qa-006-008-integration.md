# Hand-off: qa / 006+008 integration (E2E parts of 006 T061-T069 and 008 T076-T082)

Branch `006-008-lyrics-sheet-music` from `9c85819`. Separate compose project `stemstudio-e2e2` (override file
outside the repo: own image tags `stemstudio/{worker,api,web}:e2e2`, web on host port 9080, API on 9081,
`StemStudio__WebOrigin=http://localhost:9080`, `StemStudio__PublicApiBaseUrl=http://localhost:9081`), CPU images,
`STEMSTUDIO_SEPARATOR=fake`, `STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5`, fresh volumes per fake lyrics mode. Playwright
with `E2E_BASE_URL=http://localhost:9080`, `E2E_API_URL=http://localhost:9081`, retries 0. The `stemstudio` and
`stemstudio-e2e` projects (and their volumes and `:local` images) were not touched; `stemstudio-e2e2` was removed
with `down -v` at the end, the GPU model volume `stemstudio-e2e2-gpu-models` and the `:e2e2*` images were removed too.

## What was built

- Pending edits of the pre-UI hand-offs, done:
  - `support/test.ts` now holds the 006/008 fixtures (`lyricsApi`, `scoreApi`, `karaoke`, `lyrics`, `scoreDialog`,
    `scoreItems`); `support/lyrics-test.ts` imported the removed `feature-test.ts` and is gone (every 006/008 spec
    failed to load before this).
  - `support/levels.ts`: ui-test-ids 1.6.0/1.7.0 Beginner rules; new `tests/lyrics-score-levels.spec.ts` (banner,
    lyrics card, score dialog at Beginner and Advanced).
  - Help: 006 and 008 reference queries (`support/help.ts`) and help-marker cases in `tests/help.spec.ts`.
  - Page objects: `mixer-page` `seek()` / `karaokeBanner`, `track-detail-page` `lyricsCard` / `scoreStem()`.
  - a11y/visual screens: new `score-dialog`; `library` seeds a ready score item (`ux-library.ensureScoreItem`, PDF
    preview masked); `mixer` and `track` now carry the karaoke banner, lyrics card and sheet-music row.
  - `package.json`: `test:legacy` excludes `@karaoke|@lyrics|@sheet-music` (SC-006 set; the default `npm test`
    keeps them, they run green on the fake stack).
- `tests/fixtures/lyrics/fake-lyrics.json` aligned with the worker's real fake provider (four `FAKE_LINES` spread
  evenly over the track: 4/8/12/16 s on the 20 s fixture) and its fixture test updated.
- **86 visual baselines** regenerated in `mcr.microsoft.com/playwright:v1.65.1-noble` on a fresh stack and confirmed
  86/86 on a second fresh stack (for the owner review).
- `docs/testing/TEST-STRATEGY.md` 13/14: fake table, frame-work timing, SC-003 numbers, help/level coverage,
  design-quality screens, traceability rows (006 T061, 008 T076).

## Results (retries 0)

See "Tests and coverage" for the final numbers per suite; mode runs:

| Fake lyrics mode / device | `test:lyrics` | other |
|---|---|---|
| `word` / cpu (final fresh stack) | 35 passed, 15 skipped | sheet-music, export, artwork, a11y, ux, legacy, lighthouse, visual (below) |
| `line` / cpu (fresh) | 4 passed, 46 skipped | |
| `instrumental` / cpu (fresh) | 4 passed, 46 skipped | |
| `plain` / cpu (fresh, after fixes) | 6 passed, 44 skipped | |
| `plain` / gpu (fresh, after fixes) | 6 passed, 44 skipped | `test:sheet-music` 18 passed |
| `not-found` / cpu (fresh) | 4 passed, 46 skipped | |
| `failed` / cpu (fresh) | 4 passed, 46 skipped | `test:sheet-music` 18 passed |

Karaoke probe (Chromium, word mode): ball/word landing within 50 ms for 30 s, 50 A-B loops of 8 s and 20 seeded
seeks; offset +500 ms and size change keep it; frame rate 60.0 fps over 20 s of playback, rAF callback work p95
0.4 ms, max 2.4 ms (2 401 callbacks). Firefox: the same landing cases green.

### Real models on the GPU (throwaway containers, RTX 5070 Ti, 14 GB free before the runs)

GPU image built from this branch as `stemstudio/worker:e2e2-gpu` (`TORCH_VARIANT=cu132`, `ORT_VARIANT=gpu`,
`WORKER_DEVICE=auto`); models in a throwaway volume; pytest 9.2.0 installed into `/tmp` of the container only.

| Check | Device | Result |
|---|---|---|
| 006 T066 SC-003 `worker/tests/alignment/test_align_clip.py` (Whisper large-v3-turbo, 1.51 GB first download) | cuda | **20 / 22 = 90.9 %** word starts within 300 ms (target >= 80 %): pass; align phase 1.4 s for 28.97 s of audio, test 17.4 s warm |
| same | cpu (CPU image) | 20 / 22 = 90.9 %; align phase 7.5 s, test 20.4 s |
| 008 T080 `worker/tests/transcription/test_accuracy_integration.py` | cuda | bass line F1 **1.000** (0.019 s per audio s); piano texture F1 **0.933** (target 0.85), **0.184 s per audio s** warm (2.39 s/s on the first run incl. the 172 MB checkpoint download) |
| same | cpu (CPU image, 8 threads) | bass F1 1.000 (0.011 s/s); piano F1 0.933, **0.367 s per audio s** |

The 20 s clips include model load, so these are upper bounds per audio second. `StemStudio:ScoreEstimates`
defaults (`PianoCrnnCpu` 1.7, `PianoCrnnGpu` 0.08, `BasicPitchCpu` 0.10 s/s) are more than 50 % above the measured
CPU piano value; refining them is the CTO's T080 on the reference machine (a 4-minute song: about 90 s on this CPU).

## Fixes

### Test faults (qa-owned)

1. `support/lyrics-test.ts` imported a removed module; fixtures folded into `support/test.ts`.
2. Fake lyrics table and karaoke spec followed the qa proposal, not the worker's fake (lines 4/8/12/16 s, no gap
   > 5 s): table aligned; L2 asserts countdown -> singing -> ended (gap countdown covered by engine unit tests);
   loop landing count from the words in the region; `restoreLyrics` waits for `found` before setting the offset (409).
3. L5: the engine has no `performance.measure` entries; the frame work is timed by wrapping `requestAnimationFrame`.
4. Reduced motion: the engine keeps `.karaoke-ball` but hides it (audio-engine.md); assert hidden.
5. Seek case waits until `data-position-ms` shows the seek target before recording (the jump was counted once).
6. Paste case: waits for the settled lookup (on a fake GPU the found text is auto-aligned; a paste meanwhile is 409)
   and for the pasted text in the API.
7. Sheet music: drums "before the split" uses the 004 plain track (cancelled detail phase; the fake stack splits
   every other track); fake detail gives kick/snare/toms/rest only, so no x noteheads in the fake drum score (asserts
   unpitched notes, two voices, percussion clef); `asset-source` shows the track title only (ui-test-ids 1.7.0), the
   audio item is asserted via `provenance.sourceAssetIds`.
8. 007 specs on a used stack: `selectChannels` waits for the rendered channel boxes and checks each; item names may
   carry ` (n)` (names are unique across kinds, a score of the bass shares the default name); the deleted-track case
   waits for the auto detail phase (409 while active); `playerDuration` sets `preload=metadata` + `load()` (rows use
   `preload="none"`); 'save to library' expects `export-dialog` (the 007 polish titled it so on purpose).
9. Landing-maths self-test values follow the aligned table.
10. `band-info.spec.ts` correction case: both browsers correct the same track, so the status/flag/title are polled
    together (Firefox read the state of the Chromium correction).

### App faults (minimal, test-first)

| # | Fault (found by) | Fix | Test first |
|---|---|---|---|
| A1 | Mixer circuit crashed: `KaraokeBanner` rendered `karaoke-unavailable` before the first lyrics answer -> NRE in `LyricsText.MixerNotice` (every karaoke case) | `KaraokeBanner.razor(.cs)`: notice only after the first answer (`HasLoaded`) | `KaraokeBannerTests.WhileTheLyricsLoad_NeitherTheBannerNorTheNoticeIsRendered` |
| A2 | Help query "export range" returned `export-dialog` (007 help spec) | `export-range.md`: title "Save a range of a song", keyword "export range" | `HelpCoverageTests.FeatureReferenceQueries_ReturnTheExpectedTopicFirst` (9 queries of 006/007/008) |
| A3 | Library 623 px wide at 320 px: the track filter select sized to a long title (robustness a11y, WCAG 1.4.10) | `library.css`: filter select `width: 100%; min-width: 0` below 48rem | E2E `robustness.a11y` library 320 px (CSS) |
| A4 | Lyrics card opened while `alignPending` had no estimate yet never showed the CPU estimate (nor the GPU start) until reload (lyrics.spec US2-2, Chromium) | `LyricsSection.razor.cs`: poll while `alignPending` without estimate | `LyricsSectionTests.AlignPending_BeforeTheBackendDecided_IsPolledUntilTheEstimateArrives`, `..._WithTheEstimate_IsNotPolled` |
| A5 | **Domain (backend lane, trivial)**: automatically aligned LRCLIB lyrics were credited "Lyrics: entered by you; timing: automatic alignment" — `CompleteAlign` replaced the document with the worker's (source user) | `TrackLyrics.CompleteAlign` keeps `Source`, `SourceId`, `SourceDurationSeconds` of the aligned text | `TrackLyricsTests.CompleteAlign_KeepsTheSourceOfTheAlignedText`; `LyricsAlignTests.ACompletedAlignment_IsFoundWithAlignedTiming` encoded the wrong credit and now expects "Lyrics: LRCLIB; timing: automatic alignment" |
| A6 | Score dialog: a long title ran past the dialog edge (visual baselines) | `dialog.css`: `.ss-modal { overflow-wrap: anywhere }` | visual baselines (CSS) |

## Files changed

- e2e: `package.json`, `pages/{export-dialog,karaoke-banner,library-page,mixer-page,track-detail-page}.ts`,
  `support/{help,levels,screens,test,ux-data,ux-library,visual,musicxml}.ts`, `support/lyrics-test.ts` (deleted),
  `tests/{band-info,export,help,karaoke,library,lyrics,lyrics-score-levels (new),lyrics-score-support,sheet-music}.spec.ts`,
  `visual/__screenshots__/screens.spec.ts/*.png` (86)
- fixtures: `tests/fixtures/lyrics/fake-lyrics.json`, `tests/fixtures/tests/test_lyrics_fixtures.py`
- web: `Components/Mixer/Karaoke/KaraokeBanner.razor(.cs)`, `Components/Lyrics/LyricsSection.razor.cs`,
  `Help/topics/tracks/export-range.md`, `wwwroot/css/components/{library,dialog}.css`
- domain: `src/StemStudio.Domain/Lyrics/TrackLyrics.cs`
- tests: `StemStudio.Web.Tests/{Mixer/Karaoke/KaraokeBannerTests,Help/HelpCoverageTests,Pages/Lyrics/LyricsSectionTests}.cs`,
  `StemStudio.Domain.Tests/Lyrics/TrackLyricsTests.cs`, `StemStudio.Api.Tests/Lyrics/LyricsAlignTests.cs`
- docs: `docs/testing/TEST-STRATEGY.md`, `docs/quality/SIG-REPORT.md`, this note;
  `specs/006-synced-lyrics-karaoke/tasks.md` (T039, T048, T055, T061), `specs/008-sheet-music/tasks.md`
  (T045, T050, T056, T065, T070, T076) ticked

## Tests and coverage

Final run on a fresh `word` / `found` / cpu stack (final images), retries 0, Chromium + Firefox:

| Suite | Command | Result |
|---|---|---|
| Fixtures | `npm run fixtures` | 8 + 13 + 6 fixtures, clip (22 words), 7 score fixtures written |
| Visual | container, `--update-snapshots` on a fresh stack, then compare on a second fresh stack | **86 written, then 86 / 86 passed** |
| Lyrics + karaoke (`word`) | `npm run test:lyrics` | **35 passed**, 15 skipped (other modes), 19.0 min |
| Lyrics, other modes | see the mode table above | all green (line, instrumental, plain cpu/gpu, not-found, failed) |
| Sheet music | `npm run test:sheet-music` | **18 passed** (also 18/18 on `plain`/gpu and `failed`/cpu) |
| Sheet music drums (`@detail`) | `npx playwright test tests/sheet-music.spec.ts -g drums` | **4 passed** |
| Export + library (007) | `npm run test:export` | **58 passed**, 8 skipped (other online modes) |
| Artwork + band info (005, `found`) | `npm run test:artwork` | **32 passed**, 14 skipped (after the band-info fix; the run before it: 31 + 1 Firefox race) |
| Accessibility | `npm run test:a11y` | **110 passed** (88 axe cells incl. score-dialog, library with a score item, mixer with the banner, track with the lyrics card; 22 robustness); 0 axe violations |
| UX (004) | `npm run test:ux` | **74 passed** |
| 001-003 legacy (SC-006) | `npm run test:legacy` | **84 passed** (42 per browser: the 33 legacy specs + 9 stack-free 006/008 self-tests) |
| Lighthouse | `npm run test:lighthouse` | setup 1 passed; lhci 5 URLs x 3 runs, all assertions passed |

Lighthouse medians (final run): Import FCP 190 / LCP 324 ms, Tracks 179 / 257, Track 202 / 417, Mixes 196 / 314,
Mixer 190 / 458 ms (CLS 0.039); all others CLS 0.

```text
scripts/test-all.ps1 -ContinueOnError: 33 of 35 steps passed, 2 failed (both outside this change, see Open issues)
  dotnet test: Domain 556, Persistence 77, Web 1242, Api 927 passed
    StemStudio.Web 99.53 % line | 92.88 % branch; Domain 99.06 / 95.96; Api 99.49 / 95.26; Persistence 98.59 / 92.85
  worker pytest 2357 passed, 3 skipped, 10 deselected, 100 % line and branch (9418 stmts / 1070 branches)
  engine vitest 100 % (921/921 statements, 357/357 branches); web-interop 99.64 % statements, 97.7 % branches
  tools/quality 437 passed, 100 %; fixtures 216 passed, 100 % line and branch (974 stmts / 106 branches)
  e2e lint, typecheck: clean; e2e list: 549 tests in 36 files
  FAILED worker mypy: 10 errors in worker/tests/engraving/{scores,test_fonts}.py (audio-ingest; already listed in
         stem-separation-008-polish "the mypy errors in tests/engraving")
  FAILED quality ruff check: I001 import order in tools/quality/tests/test_vendor_check.py (quality-gate; --fix)
```

## Quality checks

- e2e `eslint . --max-warnings 0` and `tsc --noEmit`: 0 problems.
- `dotnet build` (warnings as errors) and `dotnet format --verify-no-changes`: clean (test-all).
- `tools/quality/css_tokens_check.py`: 0 errors, 0 warnings.
- `tools/quality/run_all.py --report docs/quality/SIG-REPORT.md`: 1312 files, 10 935 units, 114 clones,
  **0 errors**, 370 warnings (no new warning in a file of this change beyond test-callback unit sizes under 30 LOC);
  report committed.
- fixtures `ruff check`, `ruff format --check`, `mypy --strict`: clean.

## Dependencies added

None (pytest 9.2.0 was installed only inside the throwaway GPU/CPU test containers, as the image has no dev tools).

## Open issues / requests

- **backend / CTO (A5)**: I fixed `CompleteAlign` in the domain (one method, test-first) because the credit was
  plainly wrong (data-model 3.3); please review. The worker still returns aligned documents with `source: user`.
- **audio-ingest / stem-separation**: the fake LRCLIB document has no gap > 5 s, so the gap countdown (US1-6, FR-008)
  has no E2E case; a fourth line after a 6 s gap in `lyrics/fake.py` would let L2 cover it. The fake detail phase has
  no hi-hat/cymbals, so x noteheads (008 T065) are covered by unit tests only.
- **frontend**: (1) score rows in the library have no row Download (files are in the facts panel), the Status column
  wraps "Statu s" / "Rea dy" next to long titles; (2) Advanced score dialog: no gap between "Simplified rhythm" and the
  "Key" label; (3) library rows use `preload="none"` (fine), so with ~100 items nothing loads until play.
- **architect / CTO**: 008 T070 says the score item's source "shows both the audio item and the track", ui-test-ids
  1.7.0 `asset-source-<id>` shows the track only; the spec is met through `provenance.sourceAssetIds`. The 006 T055
  wording "shows no ball" means hidden (the element stays). 008 `test:sheet-music` excludes `@detail`; the drums cases
  run with `-g drums` (or `test:detail`).
- **CTO (T080)**: `StemStudio:ScoreEstimates` defaults vs the numbers above.
- **qa 005/007 integration (other agent)**: the 005/007 spec fixes in item 8 above and the band-info correction poll
  (`export.spec.ts`, `library.spec.ts`, `band-info.spec.ts`, `pages/export-dialog.ts`, `pages/library-page.ts`,
  `support/help.ts`) touch files of that wave; expect a merge.
- **audio-ingest**: `worker mypy` is red on `worker/tests/engraving/{scores,test_fonts}.py` (10 errors); **quality-gate**:
  `ruff check` I001 in `tools/quality/tests/test_vendor_check.py`. Both predate this run and are outside qa's lane.
- Transient: twice during long runs the browser could not reach the stack (stem "Failed to fetch", one `page.goto`
  timeout) while other heavy work (image build, a parallel Playwright run) used the host; both cases were green in
  the reruns and in the final run.

## Next owner

CTO: review A5 and the 86 baselines with the owner, merge, 006 T064-T069 / 008 T079-T082 (README, licences,
STATUS, estimates).
