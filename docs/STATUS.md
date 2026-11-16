# StemStudio MVP — Status

_Owner: CTO (orchestrator). Updated after every reviewed hand-off._

## Phase overview

| # | Phase | Owner | Status |
|---|-------|-------|--------|
| 1 | Constitution (`/speckit-constitution`) | CTO | Done (v1.0.0) |
| 2 | Specification (`/speckit-specify`) | CTO | Done |
| 3 | Clarification (`/speckit-clarify`) | CTO (autonomous, ADR-001) | Done |
| 4 | Plan, contracts, data model, skeleton (`/speckit-plan`) | architect | Done (reviewed: build 0 warnings, 10+24+2 tests green) |
| 5 | Tasks (`/speckit-tasks`) | architect | Done (142 tasks, Phase 0 T001-T003 + T129-T133 done) |
| 6 | Analyze (`/speckit-analyze`) | CTO | Done (38 findings, 0 critical, all resolved; ADR-008) |
| 7 | Implementation (`/speckit-implement`) | 9 agents in parallel worktrees | Done (all merged, all gates green) |
| 8 | E2E verification + MVP report | qa / CTO | Done (see docs/MVP-REPORT.md); T124 done (V3/V5 by hand), T125 open (no remote) |

## Blockers

- T125: the repository has no GitHub remote, so GitHub Actions has not run. Pushing is an
  outward-facing action left to the owner. Local equivalent (`scripts/test-all.ps1`) is 29/29 green.

## Decisions

- ADR-001: clarification decisions (FLAC stems, source-key cache identity, restart handling,
  peak meters, no autosave).
- ADR-002..007 (architect): layers, worker contract/ML stack, stem storage/serving, audio engine,
  testing tooling, CPM + xUnit v3 on VSTest.
- CTO: accept Demucs 4.1.0 (4.0.1 is incompatible with current torchaudio; see ADR-003).
- CTO: accept spotDL's transitive GPL/LGPL dependencies (OSI-approved, local single-user tool);
  list them in THIRD-PARTY-LICENSES.md and name the unofficial Spotify-client ToS risk under
  known limitations.
- ADR-008: hand-off per owner per checkpoint; analyze resolutions (MiB, 15-min job failure,
  beats-per-bar editable, SC-004 automated timing, Chromium+Firefox tested).
- Contracts project is architect-owned and read-only during the wave (O1).

## Wave progress (implementation)

| Owner | Status | Evidence (CTO-verified) |
|-------|--------|-------------------------|
| beat-analysis | Merged (dec46a8) | 41 tests, 100% line/branch, ruff+mypy clean; BPM err <= 0.44, beat err <= 15 ms |
| audio-ingest | Merged (60a75e1) | 201 tests, 100% line/branch, ruff+mypy clean; fullmatch + control-char rejection |
| quality-gate | Merged (9299d13) | 119 tests, 100% line/branch; gate on skeleton: 0 errors, 4 soft warnings |
| devops | Merged (19e0e0b) | compose config valid; 3 images build, non-root, healthchecks; Sonar job gated on secret |
| audio-engine | Merged (56d932e) | 121 tests, 100% stmt/branch/func/line; lint+typecheck clean; G4 + SC-003 tests |
| qa | Merged (9361e48) | fixture tests 100%; e2e tsc+lint clean; 40 Playwright tests listed (run in integration wave) |
| stem-separation | Merged (c4dab5a) | 116 tests, 100% line/branch; real htdemucs_6s integration test passed (3.4 s warm on 6 s clip); full worker 386 tests 100%; live smoke (fake separator) upload -> completed -> FLAC stems OK |
| frontend | Merged (9de5f90) | 184 bUnit tests; 99.77% line / 94.36% branch |
| backend | Merged (de334cb, 3455ba2) | 433 tests; Domain 97.7/97.7, Persistence 99.0/100, Api 99.9/98.3 |
| integration fixes | Merged | worker titles/upload codes/streamed 413; sig-exempt; CI guards removed; orphaned imports |

## Integration backlog (CTO)

- Contract gap (ingest): `AudioFetcher.fetch` returns only a path, so YouTube titles are a
  placeholder. Fix after stem-separation merges: return title/artist with the fetched audio.
- Worker upload errors: distinguish 400 (empty) / 413 (too large) / 415 (extension) via
  `InvalidSourceError` subclasses in `ports.py`.
- Add `dotnet-sonarscanner` to `dotnet-tools.json`.
- Re-check beat reliability threshold (0.2) and downbeat placement on real music.
- Licences accepted: pathspec MPL-2.0 (via lizard), spotDL GPL clients (metadata only).
- SIG gate after merges: 1 hard error (ingest normalizer `__init__` 5 params) -> refactor
  request sent to audio-ingest.
- QA audit F1: the extra audio-ingest integration test uses real ffmpeg, not the separation model;
  accepted (the constitution's "exactly one" applies to the model test). F2 resolved (stub tests
  updated at merge). F3: add `npm run typecheck` to CI `e2e-lint` (devops T117).
- Worker SIG gate after ingest refactor: 0 errors, 24 soft warnings -> exit 0.
- CTO decision: a Spotify track without a YouTube match is rejected synchronously at import
  (worker 400 "Source unavailable" -> backend 400 with reason) instead of a failed job; spec edge
  case and worker contract description to be updated in the integration wave (SC-007 still met).
- Integration backlog additions: worker 413 without Content-Length buffers first (backend always
  sends a length); backend worker-client timeout must allow for spotDL latency; review the
  transitive `httpx2` used by Starlette's TestClient.

## Final verification (CTO, 2026-11-15)

- `scripts/test-all.ps1`: 29/29 steps passed.
- Fake stack: Playwright 36/36 (Chromium + Firefox), restart specs 2/2.
- `docker compose up` (real Demucs): `@real` main flow 2/2.
- SIG gate: 0 errors, 1 exemption, 103 soft warnings.


## Feature 002 detail separation (2026-11-15)

| Check | Result |
|---|---|
| Wave | 7 owners merged (architect Phase 0, stem-separation, backend, frontend, audio-engine, qa, devops, quality-gate) |
| `scripts/test-all.ps1` | 29/29 steps passed |
| .NET tests | Domain 239, Persistence 40, Web 284, Api 369; all >= 90 % line and branch (lowest Web branch 94.84 %) |
| Worker | 650 tests, line 100 %, branch 99.71 %; integration test with the 3 real detail models passed (42 s, 10 s fixture) |
| Audio engine | 160 tests, 100 % |
| SIG gate | 0 errors (`detail/vendor/**` excluded per ADR-010) |
| Playwright, fake stack (isolated compose project) | 001 suite + 24 `@detail` + 2 `@worker-restart` passed on Chromium and Firefox; one finding fixed (progress bar hidden after completion) |
| T092 real models, "Hero of the Day" (4:22), CPU | 30 min 51 s wall clock incl. ~2 min deliberately waiting for a newly imported track's standard separation (priority rule observed live); ~28.8 min of detail work = ~26.4 min per 4 minutes of audio: **SC-001 (<= 30 min for 4 min) met**, slower than the spike (20 min). Peak worker RAM 15.2 GiB. Outcome identical to the spike: drums 5 + rest, vocals lead/backing + rest, guitar electric/acoustic + rest, piano and other skipped (silent), bass not supported |
| Migration | Existing library (7 tracks) migrated; mixes intact |

Next: feature 003 GPU acceleration (docs/ROADMAP.md).

## Feature 003 GPU acceleration (2026-11-15)

| Check | Result |
|---|---|
| Wave | architect Phase 0 + 6 owners merged; worker fix for GPU self-starvation found in T052 and merged |
| `scripts/test-all.ps1` | 29/29 steps passed (before the T052 fix; worker suite re-run after the fix: 765 passed, 100 % line / 99.75 % branch) |
| Playwright, isolated fake stack | default suite 66/66; `@gpu` with fake device `gpu` 4 passed + 2 skipped, `gpu-oom` 4 passed + 2 skipped (Chromium + Firefox) |
| Architect spike, reference song 4:22 | standard + detail 3.7 min on GPU vs 31.5 min CPU (8.5x); outcomes identical |
| T052 real stack (GPU override), owner imports | "Broken Wings" 5:02 standard + detail 6 min 49 s on GPU while another GPU spike ran on the machine; "Sneaky Snitch" standard 31 s; Mega 53 step correctly fell back to the CPU with the reason when another container held the GPU |
| T052 finding | worker starved itself (cached GPU models kept while the queue never idles) -> fix: reclaim own cached models before a memory fallback. Re-run: "Hysteria" 3:47 detail phase fully on GPU in 89 s, no fallback, peak 12.6 GB |
| Licences | GPU-only system dependencies section; CPU image guard in CI |

## Feature 004 design system and UX (2026-11-15)

| Check | Result |
|---|---|
| Wave | architect Phase 0, frontend (single lane, T012-T063), qa, devops, quality-gate merged |
| Integration (isolated fake stack, retries 0) | legacy 66/66, ux 74/74, a11y 84/84 (0 axe violations), lighthouse all budgets (FCP 189-232 ms, LCP 305-459 ms, CLS <= 0.025), visual 60/60 baselines |
| `scripts/test-all.ps1` | 34/34; Web 798 tests 99.45 % / 92.11 %; web-interop 232 tests |
| Integration app fixes | command palette input, Enter saves mix, help footer links, tour focus/ARIA, expand button name, progress bar names, strip contrast, tooltip clipping, 320 px mixer title |
| Judgement call | Lighthouse uses `throttlingMethod: devtools`: the simulator mis-models Blazor Server's first render (prerendering off); documented in TEST-STRATEGY 10.4 |
| Design review | CTO approval under the owner mandate, owner review pending (docs/design-review/004/REVIEW.md) |
| Backlog | Radzen CSS/JS served uncompressed (812 KB vs 64 KB brotli); Material Symbols font (1 MB) on every page; level click before stored level loads is overwritten; mixer shortcut commands not serialised |

## Feature 005 artwork and band info (2026-11-15)

| Check | Result |
|---|---|
| Wave | architect Phase 0 (shared with 007), audio-ingest, stem-separation, backend, frontend, qa, devops, quality-gate merged on `005-007-artwork-export`; integration run by qa on an isolated fake stack (`docs/handoffs/qa-005-007-integration.md`) |
| `scripts/test-all.ps1` | 35/35 steps passed; Domain 389, Persistence 66, Web 1004, Api 672 tests (Web 99.44 % / 92.47 %, Api 99.51 / 95.88, Domain 98.63 / 96.39, Persistence 98.35 / 92.85, Contracts 100 / 100); worker 1596 tests 100 % line and branch |
| Playwright, isolated fake stack (retries 0) | legacy 001-003 66/66, `@ux` 74/74; `test:artwork` per fake online mode: `found` 32 passed, `not-found`/`failed`/`off` 28 passed each (skips = other modes, `@online`, `@perf`) |
| Accessibility | axe matrix incl. `band-info` 104/104, 0 violations; CTO close-out after the library column fix 106/106 |
| Lighthouse (SC-003) | all budgets met, medians FCP 171-201 ms, LCP 304-401 ms, CLS <= 0.023 |
| Visual (SC-005) | 80 baselines (60 of 004 regenerated for the covers, 20 new: `band-info`, `export-dialog`, `library`); 80/80 in the Playwright container on a fresh stack |
| Live services (T062, generic User-Agent, `STEMSTUDIO_CONTACT_URL` unset) | integration run: 3 passed, 1 failed (`test_full_lookup_of_kevin_macleod_by_isrc`, HTTP 400 below; a first MusicBrainz 503 "busy" passed on the retry). After the fix: **4/4 passed** on the first run |
| Bug fixed in close-out | the ISRC lookup asked MusicBrainz `/ws/2/isrc/<isrc>?inc=artist-credits+releases+release-groups`, which answers 400 ("release-groups is not a valid inc parameter for the isrc resource"), so every song with an ISRC failed with "Band information could not be retrieved". Now `/isrc` with `inc=artist-credits+releases`, then one `/recording/<mbid>?inc=artist-credits+releases+release-groups` per listed recording (at most 5); fixtures corrected to the real response shape (test-first; worker 1599 tests, 100 %) |
| Integration app fixes | help query "export range", library reflow at 320 px (WCAG 1.4.10) |
| Licences | tinytag (MIT), Pillow (MIT-CMU), httpx (BSD-3), rapidfuzz (MIT) direct; new "Online data services" section (MusicBrainz CC0, Cover Art Archive images in-app only, Wikidata CC0, Wikipedia CC BY-SA 4.0 with in-app attribution) |
| Contracts | ui-test-ids 1.5.1 patch note adopts the extra ids the frontend renders (`band-info-close`, `band-info-album`, `band-info-error`, `metadata-correct-cancel`, `metadata-correct-error`, `artwork-progress`) |
| Not done | quickstart A4, A5, A7, A8, A10 on the real stack (reference machine, owner); formal `/speckit-analyze` pass (consistency covered by the contract tests and the E2E suites) |

## Feature 007 export library (2026-11-15)

| Check | Result |
|---|---|
| Wave | architect Phase 0 (shared with 005), audio-engine, audio-ingest, stem-separation, backend, frontend, qa, devops, quality-gate merged on `005-007-artwork-export`; same integration run as 005 |
| `scripts/test-all.ps1` | 35/35 steps passed (numbers as for 005); engine vitest 204 tests 100 %; web-interop 232 tests 99.62 % lines / 97.7 % branches |
| Playwright, isolated fake stack (retries 0) | `test:export` 58 passed, 8 skipped (Firefox: `panner-law` is Chromium-only); `test:detail` incl. the `@detail` export case 26/26 |
| Accessibility | axe matrix incl. `export-dialog` and `library` (+ 320 px reflow) 104/104, 0 violations; close-out 106/106 with the new desktop column check |
| Visual (SC-005) | `export-dialog` and `library` (6 cells each) plus `library` at 360 px; after the close-out column fix the 8 `library--*` baselines were regenerated on a fresh isolated stack (compose project `stemstudio-e2e3`) and the whole set compared: **80/80** |
| UI polish in close-out | desktop library Status column no longer breaks words ("Statu s", "Rea dy"): headings, status words and dates use `white-space: nowrap`; names and titles still wrap anywhere, so the 320 px reflow stays green |
| Quality gate | `run_all.py` 0 errors (integration: 1 010 files, 8 033 units, 74 clones, 259 warnings) |
| Licences | `lameenc` 1.9.0 (LGPL-3.0-or-later) recorded as a direct dependency |
| Contracts | ui-test-ids 1.5.1 patch note adopts `export-close`, `export-error-hint`, `library-error`, `library-action-error`, `asset-rename-cancel`, `asset-rename-error` and command id `go-library` |
| Not done | reference-machine validation X2-X10 on the real stack with "Hero of the Day" (T072: SC-001/SC-002 timings, X10 first-time-user check); formal `/speckit-analyze` pass; the 80 baselines still need the owner review |

## Feature 006 synced lyrics and karaoke ball (2026-11-16)

| Check | Result |
|---|---|
| Wave | architect Phase 0 (shared with 008), audio-ingest, stem-separation, audio-engine, backend, frontend, qa, devops, quality-gate merged on `006-008-lyrics-sheet-music`; integration run by qa on an isolated fake stack (`docs/handoffs/qa-006-008-integration.md`); CTO close-out merged `main` (005/007, CI fixes, site) into the branch |
| `scripts/test-all.ps1` | integration run 33/35 (the 2 red steps were the worker test mypy errors in `tests/engraving` and a ruff import order in `tools/quality`, both fixed in close-out). **Close-out run: 39/39 steps passed**; Domain 556, Persistence 77, Web 1245, Api 927 tests (Domain 99.06 % line / 95.96 % branch, Persistence 98.59 / 92.85, Web 99.53 / 92.89, Api 99.49 / 95.26, Contracts 100 / 100); worker 2360 tests 100 % line and branch; engine 100 %; web-interop 99.62 % lines / 97.7 % branches; `run_all.py` 0 errors (1312 files, 10 954 units, 373 warnings) |
| Playwright, isolated fake stack (retries 0) | `test:lyrics` per fake mode: `word` 35 passed, `line` 4, `instrumental` 4, `plain`/cpu 6, `plain`/gpu 6, `not-found` 4, `failed` 4 (the rest skipped as other modes); `@ux` 74/74, legacy 84/84 |
| Landing accuracy (SC-002) and frame rate (SC-004) | every word landing within 50 ms over 30 s of looped playback, 50 A-B loops and 20 seeded seeks, also with an offset and a size change (Chromium, Firefox landing cases green); 60.0 fps, per-frame work p95 0.4 ms, max 2.4 ms |
| SC-003 alignment (T066, Whisper large-v3-turbo, 22-word clip) | **GPU** (RTX 5070 Ti) 20/22 = 90.9 % of the words within 300 ms (target 80 %), align phase 1.4 s for 29 s of audio; **CPU** 20/22, align phase 7.5 s |
| Accessibility, Lighthouse, visual | axe 110/110 with the karaoke banner and lyrics card, 0 violations; Lighthouse all budgets (Mixer LCP 458 ms, CLS 0.039); 86 visual baselines regenerated in the Playwright container and 86/86 on a second fresh stack |
| Integration app fixes | mixer crash before the first lyrics answer (`KaraokeBanner`), lyrics card polling stopped before the estimate arrived, long titles overflowing dialogs; domain: an aligned LRCLIB text keeps its source credit (`TrackLyrics.CompleteAlign`, reviewed and accepted by the CTO) |
| Licences | stable-ts, openai-whisper, torchaudio, tiktoken, more-itertools, regex, PyYAML recorded; Whisper large-v3-turbo weights (MIT, SHA-256); LRCLIB in "Online data services" (no stated data licence, lyrics in-app only) |
| Not done | the gap countdown (US1-6) and x noteheads have unit coverage only (the fake lyrics have no gap > 5 s, the fake detail split no hi-hat); reference-song spot check of the ball on the real stack (owner); formal `/speckit-analyze` pass (consistency covered by the contract tests and the E2E suites) |

## Feature 008 sheet music (2026-11-16)

| Check | Result |
|---|---|
| Wave | same branch and integration run as 006 |
| Playwright, isolated fake stack (retries 0) | `test:sheet-music` 18/18 (also on `plain`/gpu and `failed`/cpu stacks), drums `@detail` 4/4, `test:export` 58 passed / 8 skipped, `test:artwork` 32 |
| Transcription accuracy (T080, `pytest -m integration`) | **GPU**: bass line F1 1.000 (0.019 s per audio second), piano texture F1 0.933 (target 0.85), 0.184 s/s warm (2.39 s/s on the first run incl. the 172 MB download); **CPU** (8 threads): bass F1 1.000 (0.011 s/s), piano F1 0.933, 0.367 s/s |
| Estimates | `StemStudio:ScoreEstimates` defaults were more than 50 % off; set from the measurements (test-first): piano CPU 1.7 → 0.45, piano GPU 0.08 → 0.2, basic-pitch 0.10 → 0.05 s/s (a 4-minute piano song on the CPU now shows about 2 minutes instead of 7) |
| Close-out fixes | test typing for mypy (`tests/engraving`), the export bit-depth select shows the backend default (24-bit for a mix, 16-bit for stems), help `online-lookups` and `export-formats` corrected to match the code; after merging `main`, the 8 `library--*` baselines were regenerated (main's nowrap Status column) and the whole set compared on a fresh isolated stack (`stemstudio-e2e5`): **visual 86/86, a11y 112/112** |
| Licences | music21 (+ chardet, joblib, jsonpickle, webcolors), verovio (LGPL-3.0-only, unmodified), cairosvg (LGPL-3.0+, unmodified) with cairocffi, cssselect2, tinycss2, webencodings, defusedxml, pypdf, torchlibrosa, soxr (direct), vendored basic-pitch (Apache-2.0 + NOTICE) and piano_transcription (MIT) subsets, Leipzig font (OFL-1.1), fontTools (build stage only); weights basic-pitch (Apache-2.0) and piano CRNN (CC BY 4.0, Kong et al.) with SHA-256 |
| Not done | quickstart M2-M10 with "Hero of the Day" on the real stack and the SC-006 first-time-user check (owner); a library score row has no row-level Download (files are in the facts panel); formal `/speckit-analyze` pass |

## Feature 009 style transfer and the style library (2026-11-16)

| Check | Result |
|---|---|
| Wave | architect Phase 0, generative, backend, frontend, devops, qa, quality-gate merged on `009-010-generative` (shared with 010); integration run by qa on an isolated fake stack (`docs/handoffs/qa-009-010-integration.md`); CTO close-out merged `main` (001-008) into the branch |
| `scripts/test-all.ps1` | integration run 42/42. **Close-out run (after merging `main`): all 45 steps passed**; Domain 752, Agent 48, Persistence 92, Web 1469, Api 1258 tests (Domain 98.97 % line / 94.52 % branch, Persistence 98.62 / 94.73, Web 99.63 / 93.97, Api 98.95 / 92.92, Contracts and Agent 100 / 100); worker 2381 tests 100 %; generator 501 tests 99.05 %; engine 287 and web-interop 254 tests; `run_all.py` 0 errors (1828 files, 13 971 units, 549 warnings) |
| Playwright, isolated fake stacks (retries 0) | `test:styles` 36/36 (default stack) and 27 + 8 skipped (fake `ready`); `test:generative` per fake mode: `ready` 65, `oom` 65, `oom-twice` 8, `busy` 8, `slow` 51, `no-gpu` 4, `unvoiced-sample` 8 (the rest skipped as other modes); generator restart 4/4; `@ux` 74/74, legacy 66/66 |
| Ethics | E-01..E-07 green: presets without imitation phrases, every imitation case refused (`400 imitation`), library artists refused, genre words accepted, AI labels on every take (app and FLAC tags), personal-use notice |
| Real GPU (RTX 5070 Ti, weights present, throwaway containers) | restyle of a 30 s range ("Jazz trio", strength 0.55): **15.7 s** incl. reload after idle unload, VRAM +4.8 GB (peak 7.8 GB); no CPU fallback; FLAC tags `AI_GENERATED=true`, `GENERATOR=ACE-Step 1.5 (19671f4...)`, `STEMSTUDIO_GENERATION`, `STEMSTUDIO_TAKE` |
| Accessibility, Lighthouse, visual | axe 130 passed + restyle dialog 6/6 on a fake `ready` stack, 0 violations; Lighthouse all budgets (Mixer LCP 413 ms, CLS 0.023); close-out on a fresh isolated stack (`stemstudio-e2e6`, Playwright v1.65.1 container): 104 baselines regenerated after the merge, then **visual 104/104** and **a11y 136/136** (6 restyle-dialog cases skipped without a ready generator) on a second fresh stack |
| Integration fixes | generator/singer images never started (`main:create`), restyle dialog unreachable Start (scroll), `/voices` overflow at 320 px, multipart uploads with parameters or empty types (`StemStudioApiClient`), quality-gate units |
| Close-out | merge of `main`: one KaraokeBanner fix (main's, verified by the 006/008 run) and one e2e fixture layout (`support/test.ts`; `generative-test.ts` extends it); an upload's tag artist is stored as the track artist so the imitation guard knows it with lookups off (test-first); help search results keyed by topic; "Pop rock" (not in the catalogue) replaced by "Classic soul" in the 010 spec (CTO decision) |
| Licences | ACE-Step 1.5 code (MIT, commit) and weights (MIT, revision + SHA-256), generator image runtime incl. torchao (BSD-3-Clause upstream), pytorch-wavelets (MIT upstream), frozendict (LGPL-3.0-only, accepted as an unmodified separate library), NVIDIA CUDA 12.8 wheels (ADR-011) |
| Not done | owner listening check (quickstart Q9, SC-004); full quickstart Q3-Q8 on the reference machine (image size, first download, full-song restyle; T082); the 6 restyle-dialog baselines run only on a fake generator stack, not in CI; formal `/speckit-analyze` pass |

## Feature 010 songs from your own lyrics (2026-11-16)

| Check | Result |
|---|---|
| Wave | same branch and integration run as 009 |
| Playwright | song, lyrics sheets and voice profiles inside the `test:generative` runs above (fake microphone in the full Chromium build); consent C-01..C-09 green (Save disabled without consent, `consent-required`/`consent-outdated`, EN+NL record, library audio refused `422 library-audio`, too short/long/unvoiced, no "Use my voice" without vocals, profile label on converted takes, delete with and without takes, disk walk after deletion) |
| Real GPU (RTX 5070 Ti) | 45 s song from own lyrics (en, 2 blocks, seed 7): **50.6 s** cold, VRAM +10.5 GB (peak 13.5 GB), `vocal: synthetic`, `AI_VOICE=synthetic`; voice conversion of a 10 s sung target with a 20 s own-voice prompt: **25.5 s**, VRAM +3.4 GB, `AI_VOICE=converted`, `AI_VOICE_PROFILE` on mix and vocal; no CPU fallback |
| Licences | SoulX-Singer code (Apache-2.0, commit) and weights `model-svc.pt`, RMVPE, whisper-base (Apache-2.0, revisions + SHA-256); singer runtime incl. soxr (LGPL-2.1+) and transformers 4.41.2; VoiceStudio not used |
| Not done | owner listening checks (SC-003) and the full quickstart V3-V8 (3-minute songs, Dutch, own-voice profile from a real recording; T060); formal `/speckit-analyze` pass |

## Feature 011 AI composer agent (2026-11-16)

Delivered on branch `011-composer-agent`, merged into `main` as `230a363b`; integration hand-off
`docs/handoffs/cto-011-integration.md`, PR description `docs/handoffs/cto-011-pr.md`.

| Check | Result |
|---|---|
| Wave | architect Phase 0 (two parts), quality-gate, devops, backend, ai-agent, frontend and qa lanes merged in order (T080); 9 switch-overs/fixes and 11 E2E findings fixed test-first on the merged tree |
| `scripts/test-all.ps1` (T080) | **51/51 steps passed**; Domain 752, Agent 600 (98.80 % line / 93.22 % branch), AgentEval 125 (99.83 / 92.57), Persistence 99 (98.76 / 94.73), Web 1712 (99.56 / 93.65), Api 1435 (98.91 / 91.61); worker and quality 100 %; generator 99.05 %; engine 287, web-interop 254 tests. Final re-run after T081-T084a: see the row below |
| Quality | Release build 0 warnings; `dotnet format` clean; `run_all.py` 0 errors (2111 files, 16 452 units, 742 warnings; `Agent` stars 5/5/3, `tools/agent-eval` 5/5/4); Redocly 011 valid (10 known warnings); secret scan clean |
| Playwright, fake stack (retries 0, chromium + firefox) | default suite **375 passed**, 115 skipped (generator- and mode-dependent); `agent-e2e fake` 18/18; `agent-e2e none` 2/2; `@generative` agent tests 4/4 (by hand); visual 110 passed (100 baselines regenerated + 6 new Settings baselines); a11y 142 passed |
| T081a Ollama readiness (2026-11-16) | Ollama **0.40.16** on `http://localhost:11434`; `qwen3.5:9b` (9.7B Q4_K_M, 6.59 GB) and `qwen3.5:4b` (4.2B Q4_K_M, 3.32 GB) present, both with `tools` (also `completion`, `vision`, `thinking`); nothing pulled |
| T081 evaluation (`tools/agent-eval --backend ollama --model qwen3.5:9b --runs 3`, RTX 5070 Ti, own fixture, history in `tools/agent-eval/out/`) | run 1 (`c981836b` + decisions): **26/30** (nl 13/16, en 13/14), supplementary 1/3, p50 1.4 s; fails 10, 12, 24, 29, S1, S2. Analysis: 24 = harness fidelity (the fixture accepted `evil.example`, the API answers 400; fixed test-first `d563c49d` red, `8c0b08d4`), the rest = model behaviour -> tool descriptions tuned (7 tools, then 2; descriptions only). Run 2 (`de7de1bf`): 28/30 (nl 14/16), 2/3. Final descriptions (`74ebdb46`/`9199f2a2`): run 3 **30/30** (nl 16/16, en 14/14), 3/3; run 4 26/30 (**nl 12/16 = 75 %**, en 14/14), 2/3 (S2 1/3); run 5 **30/30**, 3/3 |
| SC-001 | **Met in 2 of 3 runs on the final tree** (each run = median of 3): 100 % / 86.7 % / 100 %; per language nl 100 / **75** / 100 %, en 100 / 100 / 100 %. Run 4 misses the nl >= 80 % bar (cases 10, 16, 17, 29 at 0-1/3). Per-case passed runs over runs 4+5 (6 attempts): 10 3/6, 17 2/6, 16/23/29 4/6, 14/19/27 5/6, the other 22 scored cases 6/6; S1 4/6, S2 4/6, S3 6/6 (run 3 logged only the median per case). The variance comes from sampling (the app sets no temperature for Ollama). **Owner decision 2026-11-16: accepted as is for the merge**; known item: a low Ollama temperature, a grader that counts only executed steps, and eval-set.md as the binding baseline are candidate follow-ups |
| S1-S3, injection 22-24 | runs 3 and 5: all pass (run 5: 22 3/3, 23 2/3, 24 3/3, S1 2/3, S2 3/3, S3 3/3); run 4: S2 failed 1/3 (the model did not finish the conversion; no forbidden call). No run executed a forbidden or unapproved non-read call after taint; case 24 after the description tune: the model refuses (no `import_from_url` call) |
| SC-004 | warm p50 1.4-1.5 s per whole case (p90 2.6-3.2 s), so first feedback and single-action completion are within 3 s / 5 s; cold start of `qwen3.5:9b` 3.2 s (`keep_alive 0` unload, then one chat; load 2.9 s from the file cache) |
| Owner stack update (2026-11-16, `main` `230a363b`) | compose project `stemstudio` (`docker-compose.yml` + `docker-compose.gpu.yml` + `docker-compose.generative.yml`, no profiles, default env, no `ANTHROPIC_API_KEY`) rebuilt and recreated on the same volumes (no `down`). New images: api `b789097ac487`, web `272ee3b65d78`, worker `local-gpu` `18df1fad499c`, generator `4531aece5756`, singer `c4906e1ceaa8`. Previous: api `508fd0f40232`, web `3425d9c12fce`, worker `a196183ad30d` (already gone from the image store), generator `90e4410aeab1`, singer `3bf9f30c8f4c`; rollback images built from `af28ea2d` and tagged `stemstudio/*:rollback-pre011`, SQLite DB copied first. `20261116064402_AddAgent` applied on start; `GET /api/tracks` 11 before = 11 after (same ids), mixes 4 = 4; `GET /api/agent/status` ready, `qwen3.5:9b`, Ollama 0.40.16 via `host.docker.internal`; :8080, :8081, worker, generator, singer `/health` 200 |
| A7 (real stack, Playwright headless, Expert level) | **pass**: badge `data-backend=ollama`, `data-model=qwen3.5:9b`, text "Local qwen3.5:9b" (the quickstart says "Ollama (qwen3.5:9b)"); ready 0.13-0.24 s after opening the panel on `/` and a track page |
| A8 (SC-005) | **pass, by substitute**: quitting the host Ollama was blocked by the session's permission guard, so the api was recreated with `STEMSTUDIO_OLLAMA_URL=http://host.docker.internal:11999` (connection refused, same code path) and restored afterwards. `agent-unavailable` shows "Local AI not reachable at ..." plus "Start Ollama on this computer (https://ollama.com) and keep it running." within 0.01-0.13 s, Send disabled, badge without a model; the mixer loaded (6 stems) and an upload import completed in this state. The owner can repeat it with tray -> Quit |
| A9 | **pass** (API): `PUT /api/agent/settings {"ollamaModel":"llama9.9:notpulled"}` -> 422 `model-unavailable` "... is not installed in Ollama with tool support. Run `ollama pull llama9.9:notpulled`." (quoted in the API text); settings unchanged (the Settings picker only lists installed tool models) |
| A10 (own fixture tracks `cto011-*`, own mixes; removed afterwards) | **5 pass, 1 partial, 2 not run**. 3: `search_library` -> "Hero of the Day - Remastered", no change, 5.7 s (cold load). 6: `mixer_set_channel` -> drums muted only, unsaved indicator, 1.8 s. 7: vocals fader 80 -> 100, i.e. +3 dB capped at the fader maximum (+1.9 dB effective) while the answer says "3 dB louder" (O2), 1.3 s. 11: `save_mix_as` "Bass practice" with the open-mixer state, no card, 1.8 s; the mixer keeps its unsaved indicator (O3). 13: card `delete_mix`, risk destructive, reasons `risk-destructive,tainted`, exact mix id; one `rejected_invalid` (non-UUID id) then `list_mixes` and a retry; Approve -> mix 404; 2.7 s. 14 (on an own track): **partial**: in 2 of 2 tries the model searched and then asked in text instead of proposing `delete_track`; after "Ja, verwijder hem" the card appeared (destructive, names the track, "cannot be undone"); Reject -> step `rejected`, track still 200 (model behaviour, eval case 14 = 5/6). 1 and 16: **not run** (no YouTube imports on the owner's stack) |
| A11 | the evaluation above (not repeated) |
| A12 / SC-008 (GPU stack) | **fail, 1 of 2 runs (B2)**. Run 1: `qwen3.5:9b` in VRAM (10 066 MiB used), "Splits deze track verder" -> card `start_detail_separation`, risk heavy, "About 1 minute on the graphics card"; Approve. The detail job was queued while `ollama ps` still listed the 9b model, the run's final answer came 0.9 s later (9b on the GPU, not the fallback), `ollama ps` was empty 1 s later, and the job ran on the CPU: "Not enough free GPU memory for Mega 53-stem (needs 10600 MiB, 5926 MiB free); ran on the CPU." Chatting during the job: badge "Local qwen3.5:4b (fallback)", 4b loaded CPU-only (`size_vram` 0), answer 47.6 s. Run 2 (fresh track, 100 ms sampling): 9b unloaded before the job (`ollama ps` empty, 3 268 MiB), the run's final turn on `qwen3.5:4b` (43 s), Mega 53 on the GPU (peak 14 552 MiB), detail completed on the GPU in 46 s |
| A13 | **not run**: disconnecting the network would also cut this agent session and the host; owner |
| Bugs found (not fixed here) | **B1** (frontend, cosmetic): the agent input placeholder shows `Ask the agent, e.g. &quot;Make the vocals 3 dB quieter&quot;` literally (`src/StemStudio.Web/Components/Agent/AgentPanel.razor` line 70: an HTML entity in a Razor attribute of an element with bound attributes is emitted as text). Repro: open any page, open the agent panel, look at the empty input. **B2** (backend/ai-agent, SC-008): the GPU release before a heavy job races with the running agent turn. Repro: GPU stack, chat once so `qwen3.5:9b` is loaded, on a fresh track with no detail ask "Splits deze track verder", Approve; watch `ollama ps` and the track's `detail.device`: in run 1 the run's next model turn used the 9b model on the GPU right after the release (answer 0.9 s), the worker measured 5 926 MiB free and fell back to the CPU; run 2 the same steps passed. Expected: after the release no turn of any run uses the primary model until the job ends (fallback first), or the worker job starts only after the release is confirmed |
| Observations | O1: the badge shows the model of the last run when the panel opens (stale `qwen3.5:9b` while status already reported the fallback, and stale `qwen3.5:4b` after the job), it updates after the next run; O2: relative gain is capped at the fader maximum without saying so; O3: after `save_mix_as` from the open mixer the unsaved indicator stays; O4 (not 011): an unknown route such as `/import` renders a blank page |
| T082 (A14/A15, Claude) | **owner-run, not done** (paid; needs `ANTHROPIC_API_KEY` in the owner's shell) |
| Licences (T083) | Microsoft.Extensions.AI 10.11.0 (MIT), OllamaSharp 5.5.0 (MIT), Anthropic 13.3.0 (Apache-2.0), Microsoft.Extensions.DependencyInjection 10.0.15 (MIT, test-only); Ollama (MIT) host prerequisite; Qwen3.5 weights (Apache-2.0) pulled by the user; Claude under "Optional external services" |
| Owner decisions (2026-11-16) | `Agent.Resources` in the Policy allow-list, the `MIRRORS` rule, `AddAgent` keeps id `20261116064402` (dev databases with the earlier 011 migration must be recreated), uploads titled with the original file name (`docs/decisions/CTO-DECISIONS-2026-11-15.md`) |
| Public manuals (T084a) | user manual chapter 13 (delivered behaviour, approvals, taint, Undo, Settings, limits), 3 and 16; technical manual chapters 2, 3 (agent settings, Ollama setup), 4, 6 (agent endpoints and tools), 7 (`AddAgent`, aggregates), 8, 9 (agent E2E and evaluation), 11; PDFs and `build-manifest.json` regenerated; no screenshot added (the Settings baselines mask the status line) |
| Final `scripts/test-all.ps1` after T081-T084a | recorded in `docs/handoffs/cto-011-pr.md` ("Final run"), so this file (a manual input) stays unchanged after the PDF build |
| Not done / open | T082 (owner); B1, B2 (SC-008 race) and O1-O3 above; A13 and A10 cases 1/16 (owner); A8 with a real Ollama quit (owner); "Continue" for interrupted runs (spec edge case, no task; documented as a limitation); SC-001 variance (consider a low Ollama temperature, ai-agent); grader matches an expected call even when it failed (S1 counted a denied `save_lyrics_sheet`) and `eval-set.json` omits some reads that eval-set.md lists as expected (29, S2); devops `agent-e2e` mode for the `@generative` agent tests; backend `MetadataMapper.ToQuery` still sends the hashed title of an upload; frontend Undo count is message-based; `Agent` parameter stars 3 |

## Feature 011 follow-up claude-key: Claude key in Settings, stored encrypted (2026-11-16)

Integrated on branch `011-claude-key-spec` (not merged into `main`, not pushed); hand-off
`docs/handoffs/cto-011-claude-key-integration.md`. Constitution 1.4.0, ADR-035.

| Check | Result |
|---|---|
| Wave | quality-gate, devops, ai-agent, backend, frontend and qa lanes merged in order; port overload, ui-test-ids rows, contract text, decisions (ADR-035 D8) and the quality-gate/qa test requests R1-R12, F1-F8 done test-first on the merged tree |
| Findings fixed | Trace leak tests never covered `Microsoft.AspNetCore` (appsettings Warning won); the key handlers trusted a disabled button (a forged click stored under local-only); a key-write problem echoing the key reached the exception message; Firefox: a page-object detach race and axe `target-size` crashing the page |
| `scripts/test-all.ps1` | all steps passed except the manuals check before the PDF rebuild; Domain 752, Agent 720 (98.91 % line / 94.18 % branch), AgentEval 134, Persistence 114 (98.91 / 95), Web 1848 (99.57 / 93.71), Api 1651 + 2 Linux-only skips (98.98 / 92.03), Contracts 100 / 100; key volume checks and quality 100 % |
| Quality | Release build 0 warnings; `dotnet format` clean; `run_all.py` 0 errors (2210 files, 17 372 units, 815 warnings); Redocly 1.21.0 valid; secret scan clean |
| E2E (own project `ck-e2e` on 18080/18081, retries 0) | default suite 381 passed / 115 skipped; `@claude-key` 20/20 (Chromium and Firefox); agent fake 18/18, none 2/2; visual 110 passed / 6 skipped (6 settings baselines regenerated); a11y 142 passed / 6 skipped |
| Quickstart | Q1-Q6 by the automated runs; Q7 `/keys` 700 uid 1654, not in the worker; Q8 stored key survives `down` + `up --build`; Q9 lost key volume -> `stored-unreadable` with "Enter the key again", other endpoints 200; Q9b local-only page by the E2E LAN test; Q10 (real key) for the owner |
| Owner stack | compose project `stemstudio` untouched (same container and image ids before and after) |
| Open | test-only Anthropic base-URL override for an E2E proof of CK-SC-002 (not done, needs devops + ai-agent design); Claude "Not configured" text still names only the environment variable; Firefox crash in axe `target-size` worked around in the scoped scans only (not reported upstream) |
