# Hand-off: qa / 009+010 integration (009 T081-T084 and 010 T059-T062, E2E and GPU parts)

## What was built

Integration run of features 009 (style transfer) and 010 (lyrics to song, own voice) on branch
`009-010-generative` (worktree from `21a25ce`), against a separate compose project
`stemstudio-e2e4` (override outside the repo: web/api on 9280/9281, own image tags `*:e2e4`,
fresh volumes, CPU images, `STEMSTUDIO_SEPARATOR=fake`, `STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5`;
the fake generative stack adds `docker-compose.generative.yml` + `docker-compose.generative-fake.yml`
with tags `stemstudio/{generator,singer}:e2e4-fake`). The owner's `stemstudio` stack (8080/8081) and
the 006/008 run `stemstudio-e2e2` (9080/9081) were not touched. Fake modes were switched by
recreating only `generator` and `singer` (`up -d --wait generator singer`, checked with
`printenv STEMSTUDIO_FAKE_GENERATOR_MODE` in both). Every suite is green with retries = 0.

Pending edits of TEST-STRATEGY 16.6 are done (levels, 15 help topics + `styles-new`, track page
objects with the generation ids, nav links, a11y/visual screens incl. the consent panel,
`@generative` decision); the outcome is written down in TEST-STRATEGY 16.6.

## Results (final build, retries 0)

| Suite | Stack | Result |
|---|---|---|
| Fixtures | - | `npm run fixtures` (8 + 13 + 6 + 1 + 7), `npm run fixtures:generative` (5 voice fixtures) |
| `test:legacy` (001-003, SC-006) | default, fresh | **66 / 66 passed** (33 + 33), 9.5 min |
| `test:ux` | default | **74 / 74 passed**, 1.9 min |
| `test:styles` (styles, `@generation-status`, 009/010 help) | default | **36 / 36 passed** |
| `test:styles` | fake `ready` | 27 passed, 8 skipped (`notInstalled` cases), after the help fix 8/8 x 4 repeats |
| `test:generative` mode `ready` | fake | **65 passed**, 13 skipped (other modes) |
| mode `oom` | fake | **65 passed**, 13 skipped |
| mode `oom-twice` | fake | **8 passed**, 70 skipped |
| mode `busy` | fake | **8 passed**, 70 skipped |
| mode `slow` | fake | **51 passed**, 27 skipped |
| mode `no-gpu` | fake | **4 passed**, 74 skipped |
| mode `unvoiced-sample` | fake | **8 passed**, 70 skipped |
| `test:generative-restart` (`slow`) | fake | **4 / 4 passed** (generator restart -> interrupted -> retry; SC-006 disk walk) |
| `test:generative-restart` (`ready`) | fake | 2 passed, 2 skipped |
| Ethics / consent cases | both | E-01..E-07 and C-01..C-09 inside the runs above (see "Ethics" below) |
| `test:a11y` | default | **130 passed**, 6 skipped (restyle dialog needs a ready generator); **0 axe violations** |
| a11y restyle dialog | fake `ready` | **6 / 6 passed** |
| `test:lighthouse` | default | setup 1 passed; lhci 5 URLs x 3 runs, all assertions passed |
| Visual (all baselines) | default, fresh, container | **98 written, then 98 / 98 passed** (6 skipped: restyle dialog) |
| Visual restyle dialog | fake `ready`, fresh, container | **6 written** (viewport capture), then **6 / 6 passed** twice |
| `scripts/test-all.ps1 -ContinueOnError` | - | **all 42 steps passed**, exit 0 (first run: `dotnet format` and `run_all` red, fixed below) |
| `run_all.py --report docs/quality/SIG-REPORT.md` | - | 1 828 files, 13 917 units, 144 clones, **0 errors**, 544 warnings, verdict PASS |

Lighthouse medians (desktop, `devtools` throttling, 3 runs): Import FCP 204 / LCP 320 ms / CLS 0,
Tracks 200 / 250 / 0, Track 198 / 381 / 0, Mixes 203 / 303 / 0, Mixer 213 / 413 / 0.023.

### Real GPU (throwaway containers, RTX 5070 Ti 16 GB, weights copied read-only into a throw-away volume)

GPU checked first with `nvidia-smi` (13.3 GB free, nobody else on it). Images built from this
branch as `stemstudio/generator:e2e4-gpu` (ACE-Step 1.5) and `stemstudio/singer:e2e4-gpu`
(SoulX-Singer-SVC); one container at a time, `--gpus all`, port on 127.0.0.1 only, `/data` tmpfs.
Weights pre-present (no download). VRAM = whole-GPU `memory.used` sampled every 0.5 s (baseline
about 2.9 GB of desktop use).

| Job | Input | Wall time | VRAM peak (delta) | Take | FLAC tags |
|---|---|---|---|---|---|
| Song (`song`, cold load) | own lyrics (2 blocks, en), "Pop rock ... female lead", 45 s, seed 7, 8 steps | **50.6 s** | 13 464 MiB (**+10 534**) | 45.0 s, 48 kHz, `vocal: synthetic`, profile `planned` | `AI_GENERATED=true`, `GENERATOR=ACE-Step 1.5 (19671f4...)`, `STEMSTUDIO_GENERATION`, `STEMSTUDIO_TAKE=1`, `DESCRIPTION=AI-generated with StemStudio`, `AI_VOICE=synthetic` |
| Restyle (`range` 5-35 s of that song take, "Jazz trio", strength 0.55, seed 42) | 30 s excerpt | **15.7 s** (model reloaded after idle unload) | 7 835 MiB (**+4 848**) | 30.0 s | same labels, no `AI_VOICE` |
| Voice conversion (SVC) | target `sung-target-10s.wav`, prompt `own-voice-20s.wav` (synthetic consent fixture), 32 steps | **25.5 s** | 6 349 MiB (**+3 415**) | 10.0 s mix + `/vocal`, `vocal: converted` | `AI_GENERATED=true`, `GENERATOR=SoulX-Singer-SVC (40493ad...)`, `STEMSTUDIO_GENERATION`, `STEMSTUDIO_TAKE=1`, `AI_VOICE=converted`, `AI_VOICE_PROFILE=QA synthetic voice` (both files) |

All three: `device.kind=gpu`, `cpuFallback=false`, no warnings. Containers removed afterwards.

## Fixes

### App faults (minimal, test-first)

| # | Fault (found by) | Fix | Test first |
|---|---|---|---|
| A1 | **Every mixer page crashed** (`NullReferenceException` in `LyricsText.MixerNotice`): the 006 karaoke banner rendered before its lyrics were loaded (legacy/ux mixer specs) | `KaraokeBanner.razor.cs`: "Loading the lyrics..." with `data-status="none"` until loaded | `KaraokeBannerTests.WhileTheLyricsLoad_TheNoticeSaysSo_InsteadOfFailingTheMixer` |
| A2 | **Generator and singer containers (fake and GPU) exited at start**: the image CMD named `main:create_app`, which is the re-exported `api.app.create_app(context)` | `docker/generator.Dockerfile`: `uvicorn --factory stemstudio_generator.main:create` (devops/generative lane, one word) | `generator/tests/test_image_entrypoint.py` (reads the CMD, checks the factory takes no arguments) |
| A3 | A recorded voice profile never saved (`FormatException: 'audio/webm;codecs=opus'`), and an `.lrc` upload on the song page did nothing (browsers give `""` for `.lrc`) | `StemStudioApiClient.MultipartBody`: `MediaTypeHeaderValue.TryParse`, else `application/octet-stream` | `CreateVoiceProfile_AcceptsTheRecordersMimeTypeWithParameters` (2 cases), `UploadLyricsSheet_SendsAFileTheBrowserGaveNoTypeAsOctetStream` (2 cases); `StubHttpMessageHandler` now captures the part `Content-Type` |
| A4 | Restyle dialog (Advanced) taller than 720 px: Start was outside the viewport and unreachable (both browsers) | `dialog.css`: `.ss-modal` max-height + scroll (as the export dialog already had) | E2E restyle cases (CSS) |
| A5 | `/voices` with the new-profile panel 368 px wide at 320 px (WCAG 1.4.10) | `generation.css`: labels, fieldset and inputs of `.ss-new-voice` may shrink | E2E `robustness.a11y` "voices has no horizontal overflow at 320 px" (red 48 px, then green) |
| A6 | Library overflow at 320 px and broken status words (already fixed on `main` by the 005/007 run) | cherry-picked `67a9db1`, `aaf962f`, `913825d` from `main` | as on `main` |
| A7 | Quality gate and format (see "Quality checks") | `LanguageNames.cs` compact pairs, `PageTestContext.cs` using, 5 theory tests on `TheoryData` | the same tests pass |

### Test faults (e2e, qa-owned)

1. `support/lyrics-test.ts` imported the removed `feature-test.ts` (005/007 fold): every 006-010
   spec failed to load, so `npm test` and every 009/010 script found no tests. Now extends `support/test.ts`.
2. `styles.spec.ts`: the editor case named its style "Imitation check" (the guard refuses `imitat*`,
   rightly); the search result count was read once (now polled); the library-artist case now uses
   the artist the library knows (track artist, else the band-info artist).
3. `support/fake-microphone.ts`: Chromium's default headless shell has no audio capture
   (`getUserMedia` -> `NotSupportedError` even for the fake device); `channel: 'chromium'` (full
   build, new headless) is used for the recording spec.
4. `pages/song-page.ts`, `pages/voice-profiles-page.ts`: uploads use `setBlazorInputFiles`.
5. `support/generation-api.ts` `libraryItem`: the asset's own `status` ("ready") overwrote the HTTP
   status; now `httpStatus`.
6. `song.spec.ts`: the 009 catalogue has no "Pop rock" preset (see open issues); uses "Classic soul".
   The limit text is "4,096" (accepted with or without the separator).
7. `help.spec.ts` 009/010 cases: wait for the styles page and for focus in the panel; click a result
   only once the list shows the current query's results.
8. `package.json` `test:legacy` excludes the 006-010 tags and `@self-test` (the stack-free self-tests),
   so it is again exactly the 66 tests of 1.0.0-1.2.0.

## Ethics and consent (TEST-STRATEGY 15.3 / 16.3), all green

E-01 presets without imitation phrases; E-02 every `spec` imitation case refused with
`imitation` (all `qa-proposal` cases refused too, no soft miss); E-03 library artist refused (see
note); E-04 genre words accepted; E-05 free caption / delivery refused; E-06 AI labels on every
completed take (badge, track origin, list badge, library origin `generation`, FLAC tags, also on
the real GPU files above); E-07 personal-use notice. C-01/C-02 Save disabled without consent and
`400 consent-required` / `consent-outdated` / missing name with nothing stored; C-03 consent
record EN + NL with version; C-04 a downloaded stem and a take refused `422 library-audio`; C-05
`too-short` / `too-long` / `unvoiced` (+ mode `unvoiced-sample`); C-06 no "Use my voice" on
instrumental and restyle takes, `409 no-vocals`; C-07 converted take labelled with the profile;
C-08 delete with and without takes; C-09 disk walk after deletion (`@restart`).

## Files changed

- web: `Components/Mixer/Karaoke/KaraokeBanner.razor.cs`, `Api/StemStudioApiClient.cs`,
  `wwwroot/css/components/{dialog,generation,library}.css`
- web tests: `Mixer/Karaoke/KaraokeBannerTests.cs`, `Api/{VoiceApiClientTests,SongApiClientTests}.cs`,
  `Fakes/StubHttpMessageHandler.cs`
- web `Songs/LanguageNames.cs`; web tests `Fakes/PageTestContext.cs`; domain/api tests (parameters only)
  `Styles/StyleTests.cs`, `Voices/VoiceProfileTests.cs`, `Generations/SongGenerationTests.cs`,
  `Generations/RestyleEndpointTests.cs`, `Voices/VoiceProfileEndpointTests.cs`
- docker / generator: `docker/generator.Dockerfile`, `generator/tests/test_image_entrypoint.py` (new)
- e2e: `package.json`, `pages/{app-shell,track-detail-page,track-list-page,use-my-voice-dialog,song-page,voice-profiles-page}.ts`,
  `pages/generated-track.ts` (removed), `support/{generative-test,generative,generation-api,generative-flow,help,levels,lyrics-test,robustness,screens,fake-microphone}.ts`,
  `tests/{generation-status,generative,generative-support,help,lyrics-score-support,song,styles,voice}.spec.ts`,
  `a11y/robustness.a11y.spec.ts` (cherry-pick), `visual/__screenshots__/screens.spec.ts/*.png` (98 + 6)
- docs: `docs/testing/TEST-STRATEGY.md` (15.3 E-03, 15.5, 16.5, 16.6), `docs/quality/SIG-REPORT.md`,
  this note; task ticks in `specs/009-style-transfer/tasks.md` (T045, T050, T057, T062, T067, T078)
  and `specs/010-lyrics-to-song/tasks.md` (T033, T038, T052, T056, T057)

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests   Passed! Failed 0, Passed 1455
generator: tests/test_image_entrypoint.py 1 passed (red before the Dockerfile fix)
e2e: npm run lint (0 problems), npm run typecheck (clean); playwright --list 709 tests in 42 files
sig_check.py e2e: 125 files, 1399 units, 0 errors, 24 warnings (spec callbacks 16-19 lines, hard max 30)
scripts/test-all.ps1 -ContinueOnError: 42/42 steps passed
  dotnet: Agent 48, Domain 751, Persistence 92, Web 1455, Api 1255 passed (0 failed)
    line/branch: Web 99.63 % / 93.95 %, Api 98.95 % / 92.91 %, Domain 98.97 % / 94.61 %,
    Persistence 98.62 % / 94.73 %, Agent 100 % / 100 %
  worker pytest 2378 passed (100 % line/branch); generator pytest 503 passed (99 %, incl. the new entrypoint test)
  engine vitest 287 (100 %); web-interop vitest 254 (99.69 % / 97.95 %); tools/quality 534 (100 %);
  fixtures 248 (100 %); e2e lint/typecheck clean, list 709 tests in 42 files
```

## Quality checks

- `dotnet build -c Release` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` exit 0
  (after formatting `LanguageNames.cs` and the using order of `PageTestContext.cs`).
- `run_all.py`: 0 errors. The first run had 7 errors, all in merged 009/010 code or caused by its
  formatting: `LanguageNames::new` a 54-line unit once formatted, a 7-line using block duplicated
  between `Program.cs` and `PageTestContext.cs`, and five theory tests with 5-6 parameters
  (`StyleTests.Filter_CombinesCriteria`, `VoiceProfileTests.Check_RefusesMissingOutdatedOrNamelessConsent`,
  `SongGenerationTests.Options_AreValidated`, `RestyleEndpointTests.Create_InvalidRequestsAre400`,
  `VoiceProfileEndpointTests.Create_ConsentIsCheckedBeforeAnythingIsStored`), now `TheoryData` with
  the same cases and assertions.
- e2e `eslint --max-warnings 0` and `tsc --noEmit` clean; `sig_check.py e2e` 0 errors.

## Dependencies added

None.

## Open issues / requests

- **architect / backend (spec vs catalogue)**: 010 spec US1 and quickstart V3 name a "Pop rock"
  preset; `presets.v1.json` (24 presets) has none. Either add the preset (guard-checked like the
  others) or change the spec/quickstart wording. The suite uses "Classic soul" meanwhile.
- **backend (009 FR-005, observation)**: an upload's embedded artist tag is never stored in
  `Track.Artist` (the tag only feeds the 005 lookup), so with online lookups off or not found the
  guard does not know a library artist that only exists as a file tag. With the fake `found`
  lookup the band-info artist is refused correctly.
- **frontend**: rapid result clicks in the help search can open another topic while the list is
  being replaced (no `@key` on the result buttons); the tests now wait, users rarely click that fast.
  The same race may touch the 005/007 reference-query cases.
- **devops / CI**: the CI `visual` and `a11y` jobs run the default stack, so the 6 `restyle-dialog`
  baselines (generated on a fresh fake `ready` stack) are skipped there; a fake-generative visual
  job (or `--grep restyle-dialog` on the fake stack) would check them. The e2e CI job should also
  install the full `chromium` (Playwright installs it with `npx playwright install chromium`) for
  the recording spec.
- **CTO**: this branch does not contain the 005/007 integration commits of `main`; the three
  library fixes were cherry-picked (same SHAs content), the rest (ISRC fix, ui-test-ids 1.5.1,
  80 baselines) come with the merge. Baselines will need one more regeneration after merging
  the 006/008 integration (their screens).
- 010 T058 (shared polish) is not ticked (other owners); CTO tasks T081-T084 / T059-T062 left to the CTO.
- Clean-up: `stemstudio-e2e4` taken down with `down -v`; images `stemstudio/*:e2e4`, `*:e2e4-fake`,
  `*:e2e4-gpu` and the volume `stemstudio-e2e4-gpu-models` removed. The web image of the E2E runs
  predates the last two code commits, which only reformat `LanguageNames` (same table) and refactor
  test code.

## Next owner

CTO: review of the regenerated baselines with the owner, merge, 009 T082-T084 / 010 T060-T062
(the numbers above cover one song, one restyle and one SVC, not the full quickstart).
