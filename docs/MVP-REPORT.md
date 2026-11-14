# StemStudio MVP — Report

_Date: 2026-11-15 · Branch: `001-stemstudio-mvp` · Author: CTO (orchestrator)_

## 1. What was delivered

A local web application for practising along with songs, startable with one command
(`docker compose up`).

| Capability | Status | Spec |
|---|---|---|
| Import from YouTube URL (yt-dlp + ffmpeg) | Delivered; unit/contract-tested with fakes (live download not exercised in automated tests) | US1, FR-001 |
| Import from Spotify track URL (spotDL metadata → YouTube match, no DRM circumvention) | Delivered; tested with fakes; a track without a match is rejected at import | US1, FR-002 |
| Local upload MP3/WAV/FLAC up to 200 MiB, streamed, with 400/413/415 errors | Delivered; E2E-verified (incl. 4 MiB upload) | US1, FR-003/004 |
| Personal-practice / copyright notice and stem-limitation help | Delivered; E2E-verified | FR-005, FR-010 |
| Demucs `htdemucs_6s` (6 stems) with fallback `htdemucs`, FLAC 44.1 kHz stereo | Delivered; real model verified E2E | FR-006, FR-009b |
| BPM / beat / downbeat / beats-per-bar detection (librosa) | Delivered | FR-007 |
| Background jobs with stage + progress, per-source cache, restart reconciliation, retry | Delivered; E2E-verified (cache re-import < 2 s, corrupt upload → failed + retry) | FR-008/009/009a |
| Mixer: fader, pan, mute, solo, post-fader peak meters, master, transport, seek, A-B loop | Delivered; E2E-verified on Chromium and Firefox | US2, FR-011..013 |
| Metronome on the beat grid: accent, volume, BPM/offset/beats-per-bar correction, look-ahead scheduling on the audio clock | Delivered; scheduler tests prove clicks within 1 ms | US3, FR-014/015 |
| Mixes: save, save-as, load, edit, duplicate, delete; unsaved-changes guard | Delivered; E2E-verified incl. survival of an API/Web restart | US4, FR-016/017, FR-011a |

### Final verification (performed by the CTO on the merged tree)

| Check | Result |
|---|---|
| `scripts/test-all.ps1` (local equivalent of CI: build, format, lint, types, tests + coverage gates, SIG gate, contracts, fixtures, e2e lint/typecheck) | 29/29 steps passed |
| `STEMSTUDIO_SEPARATOR=fake docker compose up --build` + Playwright full suite (retries 0) | 36/36 passed (Chromium 18, Firefox 18) |
| `docker compose restart api web` + restart specs (SC-005) | 2/2 passed |
| `docker compose up` (default: real Demucs, CPU) + `@real` main flow import → separate → mix → save → reload | 2/2 passed (Chromium 34.4 s incl. separation; Firefox reused the cached track) |
| QA measurements (T123) | 20 s clip 25–33 s; 4-minute song 159.6 s on CPU; 6 playable FLAC stems |

## 2. Coverage per component (line / branch)

| Component | Tests | Line | Branch | Gate |
|---|---:|---:|---:|---|
| StemStudio.Domain | 173 | 97.73% | 97.72% | ≥ 90% ✔ |
| StemStudio.Persistence | 27 | 99.04% | 100% | ≥ 90% ✔ |
| StemStudio.Api (+ Contracts) | 233 | 99.89% | 98.26% | ≥ 90% ✔ |
| StemStudio.Web (bUnit) | 184 | 99.77% | 94.36% | ≥ 90% ✔ |
| Python worker (`stemstudio_worker`) | 413 (+2 integration) | 100% | 100% | ≥ 90% ✔ |
| Audio engine (TypeScript, Vitest) | 121 | 100% | 100% | ≥ 90% ✔ |
| Quality tools (`tools/quality`) | 148 | 100% | 100% | ≥ 90% ✔ |
| Fixture generator | 31 | 100% | 100% | ≥ 90% ✔ |
| End-to-end (Playwright) | 36 + 2 restart + 2 real | behavioural | – | all green |

## 3. SIG maintainability results

Measured with `tools/quality` (lizard + jscpd + dependency check); full report in
[`docs/quality/SIG-REPORT.md`](quality/SIG-REPORT.md). Star ratings are an **internal estimate**
using SIG-style risk categories, not an official Sigrid rating.

| Property (production code) | Low risk | Moderate | High | Very high | Estimate |
|---|---:|---:|---:|---:|---:|
| Unit size | 98.3% | 1.7% | 0% | 0% | 5 ★ |
| Unit complexity | 99.0% | 1.0% | 0% | 0% | 5 ★ |
| Unit interfacing | 80.4% | 19.6% | 0% | 0% | 4 ★ |
| Duplication | 0.41% of lines (all 15 clones in test code) | | | | 5 ★ |
| Module coupling / component independence | dependency-direction check PASS, no cycles | | | | – |

Gate status: 0 errors, 1 documented exemption (`ILogger.Log`, interface-mandated), 103 soft
warnings (mostly test functions of 16–23 LOC). Weakest spot: 3–4-parameter units in
worker/separation, worker/jobs and Api. Overall estimate ≈ 4.5–4.8 stars (target ≥ 4).

## 4. Licence overview

Open-source only; full list in [`THIRD-PARTY-LICENSES.md`](THIRD-PARTY-LICENSES.md).

- Shipped runtime: MIT (.NET, EF Core, Radzen.Blazor, FastAPI, pydantic, Demucs, spotDL, Deno),
  BSD-3-Clause (PyTorch, NumPy, uvicorn, soundfile), ISC (librosa), Unlicense (yt-dlp),
  Apache-2.0 (python-multipart), public domain (SQLite).
- Copyleft, reviewed and accepted (unmodified, separate components, local single-user use):
  FFmpeg (LGPL/GPL build), lameenc (LGPL-3+), soxr / libsndfile (LGPL-2.1+), and via spotDL
  mutagen, unidecode, pykakasi, spotapi (GPL). Redistribution of the worker image must honour them.
- Build-only: dotnet-sonarscanner (LGPL-3.0), pathspec/certifi (MPL-2.0).
- Rejected: FluentAssertions 8 (commercial), JsonSchema.Net 9 (non-OSI EULA).

## 5. Known limitations

- Six stems maximum; separating every instrument individually is not reliably possible with
  current open-source models (parts end up in "other"). Guitar/piano stem quality varies.
- CPU separation takes ~40% of the song length (159.6 s for 4 minutes); one job at a time. GPU
  override exists but is not built or tested.
- Beat detection was tuned on synthetic material; songs with tempo changes or strong backbeats may
  need manual BPM/offset correction or show downbeats on the wrong beat.
- YouTube and Spotify imports are verified with fakes and contract tests only; no automated test
  downloads from the live platforms (by design: network, terms of service). spotDL relies on
  unofficial Spotify clients; yt-dlp needs regular updates.
- The worker contacts the Hugging Face Hub at start-up even with cached weights (QA R8).
- No prerendering: pages show "Loading…" until the Blazor circuit is connected.
- Single local user; no authentication; ports must not be exposed publicly.
- **CI on GitHub has not run**: the repository has no remote. The workflow is validated with
  actionlint and its exact steps pass locally via `scripts/test-all.ps1`; the first push will be
  the first real CI run (T125).
- Manual quickstart scenarios V1–V10 (T124) are covered by automated E2E where possible; the live
  link imports (V3) and the worker-down scenario in the browser (V5) were not checked by hand.

## 6. Prioritised backlog (next iteration)

1. **Export/render a mix to an audio file** (WAV/MP3/FLAC; offline render of the current mix,
   optionally with metronome) — first post-MVP feature.
2. Push to GitHub, run CI for real, configure `SONAR_TOKEN`, and run Sigrid for an official
   maintainability rating (T125).
3. Live import smoke tests (YouTube/Spotify) in a scheduled, opt-in workflow; monitor yt-dlp/spotDL
   breakage.
4. GPU path: build and test `docker-compose.gpu.yml` (CUDA torch), auto device selection; report
   expected speed-up.
5. Beat analysis on real music: evaluation set, tune the reliability threshold and downbeat
   detection; tempo-map (variable BPM) support for the metronome.
6. Set `HF_HUB_OFFLINE=1` when weights are cached; pre-warm command in the image.
7. Reduce 3–4-parameter units in worker/separation, worker/jobs and Api (SIG unit interfacing),
   and the test-code clones in backend tests.
8. Playback-speed control (slow down without pitch change) and pitch shift for practice.
9. Prerendering / faster first paint; keyboard shortcuts beyond space = play/pause.
10. Track deletion with cleanup of stems and mixes; disk-usage overview.
11. Waveform display with loop-region drag handles.
12. Safari testing (WebKit project in Playwright).
