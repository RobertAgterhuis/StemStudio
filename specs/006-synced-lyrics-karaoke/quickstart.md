# Quickstart and validation guide: Synced Lyrics with Karaoke Ball

Builds on the 001-005 quickstarts. Contracts: [contracts/DELTA.md](contracts/DELTA.md). Entities:
[data-model.md](data-model.md). Checks: [research.md](research.md). Design: ADR-015.

## Running StemStudio

Unchanged start commands. Lyrics are looked up online when online lookups are on (005,
`STEMSTUDIO_ONLINE_LOOKUPS`). Optional worker variables:

```bash
STEMSTUDIO_ALIGN_AUTO=gpu      # gpu (default) | always | never: when plain lyrics are aligned without a click
STEMSTUDIO_ALIGN_MODEL=turbo   # Whisper model for alignment (1.6 GB download at first use)
```

What leaves the machine: title, artist, album and duration, sent to LRCLIB with the 005
User-Agent. Lyrics are stored locally, shown only in the app, never exported or logged.

## Validation (L1..L10)

| # | What | How | Expected |
|---|---|---|---|
| L1 | Unit, contract and engine tests | `scripts/test-all.ps1` (or `.sh`) | green; >= 90 % line and branch per component (worker, API, Web, engine) |
| L2 | Fake stack, word sync (US1) | `STEMSTUDIO_SEPARATOR=fake STEMSTUDIO_FAKE_LYRICS=word docker compose up -d --build --wait`; Playwright `karaoke.spec.ts` | `karaoke-banner` `data-state` goes `countdown` -> `singing` -> `ended`; `data-word-index` follows the fake timings |
| L3 | Line sync, plain, instrumental, not found, failed (US1-2, US1-5, US2) | same stack with each `STEMSTUDIO_FAKE_LYRICS` mode; `lyrics.spec.ts` | `data-sync="line"` and credit "word timing estimated"; plain -> `lyrics-align` (fake CPU) then fake alignment -> banner; instrumental -> no banner, `lyrics-status` `instrumental`; failed -> `lyrics-error` + `lyrics-search` |
| L4 | Ball accuracy against the audio clock (SC-002) | `karaoke.spec.ts` "landing": on every frame for 30 s the test reads `data-position-ms` and `data-word-index` (and the engine's audio-clock position via a test hook) and checks that the word index changes within 50 ms of each fake word start; then 50 loops of an 8 s A-B region and 20 random seeks | max landing error <= 50 ms in all cases |
| L5 | Frame rate (SC-004) | `karaoke.spec.ts` "perf" (Chromium, performance trace over 20 s of playback with all meters attached) and, on the reference machine, Chrome DevTools performance panel | >= 55 fps; karaoke frame work < 2 ms (`performance.measure` around the draw) |
| L6 | Reference song, real stack (SC-001) | import "Hero of the Day" (YouTube or Spotify); note the end of the download step | within 10 s `lyrics-status` `found`, `data-sync="word"`, credit "Lyrics: LRCLIB"; in the mixer, mute vocals and spot-check 10 words: the ball lands on each word as it is sung; seek/pause/loop keep it in time |
| L7 | Instrumental (US1-5) | import "Monkeys Spinning Monkeys" (Spotify link) | `lyrics-status` `instrumental`; no banner in the mixer |
| L8 | Alignment on the GPU (US2, SC-003, SC-005) | GPU stack; on the reference song choose "Remove" then paste the plain lyrics (copied from the track's own LRCLIB result before removal, or own text for an own recording) | alignment starts automatically, `lyrics-align-device` "GPU (...)", completes <= 2 min (model cached); ball usable (no word > 1 s off on spot-checks); `pytest -m integration worker/tests/alignment/test_align_clip.py` gives >= 80 % of word starts within 300 ms on the test clip |
| L9 | Alignment on the CPU (US2-2, SC-005) | CPU stack, same paste | `lyrics-align` with `lyrics-align-estimate`; after clicking, measured time within 50 % of the estimate |
| L10 | No lyrics outside the app (FR-010, SC-006) | automated: backend test that runs a fake import with lyrics and scans all log output and `GET /api/tracks` for the fixture words; manual: `docker compose logs` after L6 | zero occurrences |

## Developer commands

```bash
cd worker && uv run pytest tests/lyrics tests/alignment --cov
cd worker && uv run pytest -m integration tests/alignment/test_align_clip.py     # real Whisper, manual
cd src/audio-engine && npm run test:coverage                                     # karaoke module
npx @redocly/cli@2.78.0 lint --config specs/006-synced-lyrics-karaoke/contracts/redocly.yaml \
  specs/006-synced-lyrics-karaoke/contracts/worker-api.openapi.yaml \
  specs/006-synced-lyrics-karaoke/contracts/backend-api.openapi.yaml
```
