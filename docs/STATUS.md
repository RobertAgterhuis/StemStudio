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
| Not done | quickstart A4, A5, A7, A8, A10 on the real stack (reference machine, owner); README section on online lookups (T061); formal `/speckit-analyze` pass (consistency covered by the contract tests and the E2E suites) |

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
| Not done | reference-machine validation X2-X10 on the real stack with "Hero of the Day" (T072: SC-001/SC-002 timings, X10 first-time-user check); README "Library and exports" section (T073 README part); formal `/speckit-analyze` pass; the 80 baselines still need the owner review |
