# Quickstart and validation guide: Sheet Music per Instrument

Builds on the 001-003 and [007](../007-export-library/quickstart.md) quickstarts. Contracts:
[contracts/DELTA.md](contracts/DELTA.md) (full documents from Phase 0). Entities:
[data-model.md](data-model.md). Measurements: [research.md](research.md). Design:
[ADR-017](../../docs/decisions/ADR-017-sheet-music-transcription-and-engraving.md).

## Running StemStudio

Unchanged (`docker compose up --build`, optional GPU override). First use of the piano model
downloads 172 MB into the model cache; basic-pitch downloads 0.2 MB. The fake stack
(`STEMSTUDIO_SEPARATOR=fake`) uses the fake transcriber; notation and PDF are real.

## Using it

1. Track page -> a stem -> **Sheet music** -> the dialog suggests a profile and shows its limits
   and an estimated time -> **Generate**.
2. Drums: split the drums first (detail separation) if the dialog says so.
3. Library -> the item (kind "Sheet music") -> preview the PDF, download PDF / MusicXML / MIDI,
   see key (with confidence), tempo, time signature, warnings.
4. Advanced: key override, "simplified" rhythm, manual tempo, range; for piano "accurate"/"fast".

API (what the agent of 011 calls):

```bash
curl -s "http://localhost:8081/api/scores/options?trackId=<trackId>&channel=bass"
curl -s -X POST http://localhost:8081/api/assets/scores -H "Content-Type: application/json" \
  -d '{"source":{"trackId":"<trackId>","channel":"bass"}}'
curl -s -X POST http://localhost:8081/api/assets/scores -H "Content-Type: application/json" \
  -d '{"source":{"trackId":"<trackId>","channel":"drums"},"resolution":"eighth"}'
curl -OJ "http://localhost:8081/api/assets/<assetId>/files/pdf?inline=false"
```

## Validation (M1..M10)

| # | What | How | Expected |
|---|---|---|---|
| M1 | All automated tests | `scripts/test-all.ps1` (or `.sh`); plus `uv run pytest -m integration worker/tests/transcription` once on the reference machine (downloads models) | green; coverage gates; synthetic F1 >= 0.90 single voice, >= 0.85 piano (SC-002) |
| M2 | Image font (SC-004) | `docker run --rm --entrypoint fc-list stemstudio/worker:local \| grep -i leipzig` | one line "Leipzig" |
| M3 | Bass part (US1, SC-001) | reference song, `bass`, defaults; time from `202` to `ready` | <= 2 min (spike: ~10 s + ~1 s); PDF: bass clef 8vb, 4/4, quarter = 118, 120 bars + pickup if any |
| M4 | Bars on downbeats (SC-003) | open `score.musicxml`; compare measure start times (from `q` = measure index x 4 beats) with the grid's downbeats | every bar line on a downbeat |
| M5 | Key override and simplified (US2) | regenerate the bass with key "E minor" and "simplified" | key signature 1 sharp; no notes/rests shorter than an eighth |
| M6 | Vocals melody (US1) | `vocals.lead`, defaults | treble clef, single voice, `detail-bleed` warning shown |
| M7 | Drums (US4) | reference song (drums split), `drums` | percussion staff, two voices, x noteheads, snare mostly on 2 and 4; ready <= 2 min |
| M8 | Drums not split (US4-2) | a track without detail phase, `drums` | notice + "Split drums first" starts the detail phase |
| M9 | Piano (US3) | a song with a real piano part, `piano` accurate on the GPU stack and fast on the CPU stack; record times next to the estimates | grand staff; GPU accurate in well under a minute (measure and record `GpuNeed`), CPU fast ~0.1 x song length |
| M10 | Not blocking (FR-011, SC-005) | start a CPU piano "accurate" job, then export a single stem (007) | export ready <= 15 s while the piano job continues |
