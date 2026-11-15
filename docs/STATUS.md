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
