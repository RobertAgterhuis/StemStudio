# Implementation Plan: Synced Lyrics with Karaoke Ball

**Branch**: `006-synced-lyrics-karaoke` | **Date**: 2026-11-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/006-synced-lyrics-karaoke/spec.md`; constitution 1.2.0;
research `docs/research/roadmap-003-011.md` section 5 and [research.md](research.md); ADR-015
(builds on ADR-005, ADR-012, ADR-014).

**Note**: Produced by the architect agent ahead of time. Contract version numbers are assigned
when the feature starts (after 005); deltas in [contracts/DELTA.md](contracts/DELTA.md). Phase 0
(incl. the alignment install spike) is listed in tasks.md but not executed yet.

## Summary

As soon as the standard job reports title, artist and duration, the backend asks the worker for
lyrics (`mode: lookup` on the 005 network lane). The worker queries LRCLIB (get with album, get,
search; +-2 s), parses LRC or the word-timed `lyricsfile`, interpolates word timing for
line-only lyrics and returns one normalised `TimedLyrics` document. Plain-only or pasted lyrics
are aligned to the vocal stem (`vocals.lead` when present) with stable-ts + Whisper
large-v3-turbo as a model step through the 003 `DeviceRunner` (auto on a GPU worker, on click on a
CPU worker). The backend stores the lyrics per track (never exported, `no-store`, never logged).
In the mixer, a banner at the bottom hosts a DOM subtree owned by a new audio-engine `karaoke`
module: on every `requestAnimationFrame` it reads the engine's audio-clock position (smoothed
with `getOutputTimestamp`, latency-compensated, plus the user offset), advances a cursor, toggles
word/line states and moves the ball on a parabola with `transform` only. No per-frame interop.

## Technical Context

**Language/Version**: unchanged (C# 14 / .NET 10; Python 3.12; TypeScript 6.0).

**Primary Dependencies (new)**: worker `stable-ts` 2.19.1 (MIT), `openai-whisper` 20250625 (MIT),
`torchaudio` 2.11.1 (BSD-2-Clause), `tiktoken` 0.14.0 (MIT), `more-itertools` 11.2.1 (MIT),
`regex` (Apache-2.0 / CNRI-Python, via tiktoken); PyYAML (MIT) promoted from transitive to
direct. uv override drops `triton` from the lock (CPU image); the GPU image keeps the triton that
torch cu132 brings (ADR-011). Model weights: Whisper large-v3-turbo (MIT), downloaded at first
use. No new .NET or npm packages.

**External services (data)**: LRCLIB (no key; no stated data licence; lyrics treated as
copyrighted, local display only).

**Storage**: SQLite table `TrackLyrics` (migration `AddTrackLyrics`), `Tracks.LyricsStatus`
column for list DTOs. Worker: `lyrics-jobs/` (results deleted after copy). Browser
`localStorage` for banner visibility and size.

**Testing**: as 005. Parsers/interpolation/selection with self-written lyrics; LRCLIB client with
recorded response shapes (text replaced); alignment adapter behind an `Aligner` port with a fake;
one `@pytest.mark.integration` alignment test on the self-made clip (deselected by default, run by
the CTO); engine karaoke module with Vitest (mocked `AudioContext` incl. `getOutputTimestamp`,
fake rAF, jsdom layout stubs); bUnit for the banner shell and controls; Playwright `karaoke.spec.ts`
with fake word-synced lyrics measuring the ball against the audio clock.

**Performance Goals**: SC-002 <= 50 ms landing error; SC-004 >= 55 fps, < 2 ms main-thread per
frame; SC-005 GPU alignment <= 2 min per 4-min song.

**Constraints**: lyrics never leave the app (FR-010); no timers for the ball (constitution V
spirit); one GPU step at a time (FR-014); alignment must not delay a waiting standard separation.

**Scale/Scope**: one user; <= 400 lines per song.

## Constitution Check

*GATE: Must pass before Phase 0. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Parsers, interpolation, selection, client, alignment adapter (fake model), job lanes, backend state machine/endpoints, engine karaoke module (mocked clock + rAF), bUnit and Playwright all test-first. stable-ts/Whisper behind the `Aligner` port; one marked integration test on a short clip. | PASS |
| II | SIG 4 stars | Worker packages `lyrics/` (lrclib, lrc, lyricsfile, interpolate, select, fake) and `alignment/` (adapter, audio prep, mapping) of small modules; engine `karaoke/` split into clock, cursor, layout, ball, dom; backend `Lyrics/` folder. | PASS |
| III | Contracts first | Worker API minor (lyrics jobs), backend API minor (lyrics endpoints, `TrackDto.lyricsStatus`), **engine contract minor** (karaoke methods/types, additive), UI ids minor; Phase 0 writes full documents, `schemas.py`, `ports.py` (`Aligner`, `LyricsLookup`), `types.ts`, Contracts DTOs; ADR-015. | PASS |
| IV | Open source, lawful use | New code deps all OSI (MIT/BSD-2/Apache-2.0). Whisper turbo weights MIT (no ADR-009 exception needed; still listed with SHA-256, downloaded at first use, never committed). triton (bundles NVIDIA binaries) excluded from the CPU image by a uv override, so the 003 CPU-image guard stays valid. **Lyrics**: LRCLIB only (no scraping, no paid tier); copyrighted text shown in-app only, stored locally, never exported/logged/shared, credited; no third-party lyrics in the repo; personal-practice notice extended (ADR-015 D6). | PASS |
| V | Simplicity and honesty | One lyrics source; one normalised document; interpolation labelled "estimated", alignment labelled "automatic"; CPU alignment is opt-in with an honest estimate; the ball follows the audio clock (rAF only for drawing, never timers; constitution V). | PASS |
| - | Tech & security | No secrets. Lyrics input limits (size, lines, words); YAML via `safe_load`; pasted text validated (length, control characters); text rendered with `textContent` (never `innerHTML`) in the engine and HTML-encoded in Blazor; subprocess-free (Python API). | PASS |
| - | Workflow | Spec Kit flow; disjoint owner paths; hand-off per checkpoint (ADR-008). | PASS |

**Post-design re-check (after Phase 1)**: PASS; refinements tracked below.

## Project Structure

### Documentation (this feature)

```text
specs/006-synced-lyrics-karaoke/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/DELTA.md     # deltas; full documents written in Phase 0
```

### Source Code (additions)

```text
worker/src/stemstudio_worker/
├── ports.py       # + LyricsQuery, LyricsLookup, Aligner (align(audio16k, text, language, progress) -> TimedLyrics), GpuNeed for whisper (architect)
├── schemas.py     # + TimedLyrics, LyricsLine, LyricsWord, LyricsJob*, LyricsOutcome (architect)
├── config.py      # + align_model, align_auto, fake_lyrics (architect)
├── lyrics/        # audio-ingest: lrclib.py, lrc.py, lyricsfile.py, interpolate.py, select.py, normalize.py, fake.py
├── alignment/     # stem-separation: stable_ts_aligner.py (lazy imports), audio_prep.py (stem pick, mono 16 kHz), mapping.py, fake.py
├── jobs/          # lyrics job store; lookup on the network lane; align as a scheduler step with priority (stem-separation)
├── api/           # lyrics-jobs routes (stem-separation)
└── main.py
src/audio-engine/src/
├── types.ts                       # + KaraokeLyrics, KaraokeOptions, KaraokeState, AudioEngine.attachKaraoke/... (architect)
├── frame-loop.ts                  # shared rAF loop for meters + karaoke (audio-engine; meters migrate onto it)
└── karaoke/{clock,cursor,layout,ball,dom,index}.ts   (audio-engine)
src/StemStudio.Contracts/Lyrics/{LyricsDto,LyricsLineDto,LyricsWordDto,LyricsStatusDto,...}.cs,
    TrackDto.LyricsStatus, ApiRoutes (+ lyrics)                                    (architect)
src/StemStudio.Domain/Lyrics/{TrackLyrics,LyricsStatus,TimedLyricsDocument,LyricsCredit}.cs (backend)
src/StemStudio.Persistence/Migrations/*_AddTrackLyrics.cs                            (backend)
src/StemStudio.Api/Lyrics/{LyricsSynchronizer,LyricsJobApplier,LyricsEndpoints,LyricsService}.cs (backend)
src/StemStudio.Web/Components/Mixer/Karaoke/{KaraokeBanner,KaraokeControls,LyricsOffset}.razor,
    Audio/IAudioEngine (+ karaoke methods), TrackDetail lyrics section + paste dialog, Help topics (frontend)
docker/worker.Dockerfile (whisper cache dir, triton override honoured, GPU variant unchanged), compose env (devops)
e2e/tests/karaoke.spec.ts, lyrics.spec.ts; tests/fixtures/lyrics/**                (qa)
```

**Dependency direction**: worker `api -> jobs -> ports <- {lyrics, alignment}`; `lyrics` and
`alignment` import only `ports`, `schemas`, `config` (`lyrics` may import `metadata.http`,
`metadata.ratelimit`, `metadata.user_agent` from 005: shared egress); `alignment` receives the
`DeviceRunner` from the composition root. Engine: `karaoke -> frame-loop, transport (read-only
TransportClock)`; `transport` never imports `karaoke`. .NET: `Api.Lyrics -> Worker client`;
Web -> API client and `IAudioEngine`.

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/006-*/**`, ADR-015, `ports.py`, `schemas.py`, `config.py`, `pyproject.toml`/`uv.lock`, `StemStudio.Contracts/**`, `src/audio-engine/src/types.ts`, project files | architect |
| `worker/src/stemstudio_worker/lyrics/**`, `worker/tests/lyrics/**` | audio-ingest |
| `worker/src/stemstudio_worker/{alignment,jobs,api}/**`, `main.py`, their tests, `worker/scripts/**` | stem-separation |
| `src/audio-engine/**` (except `types.ts`) | audio-engine |
| `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | backend |
| `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | frontend |
| `tests/fixtures/**`, `e2e/**`, `docs/testing/**` | qa |
| `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | devops |
| `tools/quality/**`, `docs/quality/**` | quality-gate |
| `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md` | CTO |

beat-analysis has no work in this feature.

## Key flows

1. **Lookup**: standard poll reports `durationSeconds` -> `TrackLyrics.Status none -> searching`
   -> `POST /v1/lyrics-jobs {mode: lookup, title, artist, album, durationSeconds}` (names from
   005's cleaned lookup fields) -> worker network lane: LRCLIB get/get/search -> select ->
   parse -> interpolate -> `TimedLyrics` -> backend stores, `DELETE /v1/lyrics-jobs/{id}`.
2. **Auto alignment** (plain result, worker device GPU, standard job completed):
   `POST /v1/lyrics-jobs {mode: align, standardJobId, text, language}` -> scheduler step
   (priority above detail) -> `DeviceRunner.run(work, GpuNeed(whisper))` -> stable-ts align on
   16 kHz mono lead/vocals -> mapping -> `TimedLyrics(origin aligned)`.
3. **Paste**: `PUT /api/tracks/{id}/lyrics {text}` -> plain document (source user) -> flow 2 by
   the auto rule, else `alignPending` with the estimate.
4. **Mixer**: `GET /api/tracks/{id}/lyrics` when `lyricsStatus == found` -> `KaraokeBanner`
   renders the host element -> `engine.attachKaraoke(hostId, lyrics, {offsetMs})` -> shared rAF
   loop draws; offset slider -> `engine.setKaraokeOffset(ms)` immediately, debounced
   `PUT .../lyrics/offset` (500 ms); `dispose()` / navigation -> `detachKaraoke()`.
5. **Old tracks**: opening the mixer of a completed track with `lyricsStatus none` -> flow 1.

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| `torchaudio 2.11.1` next to `torch 2.15.0` (version skew) | stable-ts requires torchaudio; no newer torchaudio exists; we use none of its compiled ops directly | A separate aligner container with its own torch doubles image size (+1-3 GB) and adds a service; kept as the fallback if the Phase 0 spike (P0-1) fails |
| uv override removing `triton` | openai-whisper requires triton on Linux; triton wheels bundle NVIDIA binaries (would break the 003 CPU-image guard) | Allowing triton in the CPU image would need an ADR-011 extension; Whisper does not import triton for CPU tensors |
| Engine owns a DOM subtree (karaoke host) | 60 fps updates must not go through Blazor rendering or .NET interop | Blazor-driven updates at 10 Hz (position callback) are visibly jerky; per-frame interop costs far more than 2 ms |
| Shared `FrameLoop` (meters migrate onto it) | Two independent rAF loops per page waste frames and can desynchronise | Leaving meters untouched would keep two loops; the change is internal to the engine (behaviour unchanged, existing meter tests stay green) |
