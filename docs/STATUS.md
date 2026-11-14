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
| 7 | Implementation (`/speckit-implement`) | 9 agents in parallel worktrees | In progress |
| 8 | E2E verification + MVP report | qa / CTO | Pending |

## Blockers

None.

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
| backend, frontend | In progress | - |

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
