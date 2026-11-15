# Quickstart and validation guide: Export Isolated Channels (Asset Library)

Builds on the 001-003 quickstarts. Contracts: [contracts/DELTA.md](contracts/DELTA.md) (full
documents from Phase 0). Entities: [data-model.md](data-model.md). Measurements:
[research.md](research.md). Design: [ADR-016](../../docs/decisions/ADR-016-asset-library-and-export-rendering.md).

## Running StemStudio

Unchanged: `docker compose up --build` (CPU) or with the GPU override. The worker now also mounts
the API data volume read-only at `/library` (`STEMSTUDIO_LIBRARY_DIR=/library`); nothing to
configure. Non-Docker path: set `STEMSTUDIO_LIBRARY_DIR` for the worker to the API's
`StemStudio:DataDir` (absolute path), e.g. in `scripts/dev.ps1`.

Check: `GET http://localhost:8081/health` stays healthy; the worker's `/health` (inside the
network) reports `library.readable: true`.

## Using it

1. Track page -> a stem row -> **Save to library** -> format (FLAC preselected) -> **Save**. The
   confirmation links to the item.
2. Mixer -> **Save to library** -> "What I hear" or "Selected channels" (Advanced) -> optional
   "Only the loop region" (Advanced) -> format -> **Save**.
3. **Library** (navigation) -> play, download, rename, delete, "Try again" for failed items.

API (also what the agent of 011 will call):

```bash
# raw bass stem as FLAC 16-bit
curl -s -X POST http://localhost:8081/api/assets/audio-exports -H "Content-Type: application/json" \
  -d '{"trackId":"<trackId>","mode":"raw","channels":["bass"],"format":{"format":"flac","bitDepth":16}}'
# what I hear with a saved mix, MP3 320, loop region 12.0-28.5 s
curl -s -X POST http://localhost:8081/api/assets/audio-exports -H "Content-Type: application/json" \
  -d '{"trackId":"<trackId>","mode":"mix","channels":null,"mixId":"<mixId>","range":{"startSeconds":12,"endSeconds":28.5},"format":{"format":"mp3"}}'
curl -s "http://localhost:8081/api/assets?kind=audio&trackId=<trackId>"
curl -OJ "http://localhost:8081/api/assets/<assetId>/files/audio"
```

## Validation (X1..X10)

| # | What | How | Expected |
|---|---|---|---|
| X1 | All automated tests (SC-003, SC-004, SC-005) | `scripts/test-all.ps1` (or `.sh`) | green; coverage gates; vectors consumed by engine, backend and worker suites |
| X2 | Raw stem is bit-identical (FR-004, SC-004) | export `bass` raw FLAC 16 of the reference song, then `python -c "import soundfile as sf, numpy as np; a=sf.read('<stored bass.flac>',dtype='int16')[0]; b=sf.read('<export>',dtype='int16')[0]; print(np.array_equal(a,b))"` | `True` |
| X3 | Speed of a single stem (SC-001) | reference song (4:22), `drums.snare` raw FLAC; time from `202` to `ready` (asset timestamps) | <= 15 s (spike: about 2-4 s) |
| X4 | Speed of a mix while a separation runs (SC-002, FR-011) | start a detail phase on another track, then export "What I hear" (all six stems + expanded drums, MP3 320) | ready <= 30 s; the detail phase continues |
| X5 | Sounds like the mixer (FR-002, FR-003) | mixer: vocals 35 %, bass pan -40 %, solo drums + bass, master 70 %, save "What I hear"; listen side by side with the mixer playback | only drums and bass, bass left of centre, same balance; no metronome |
| X6 | Clip protection (FR-005, SC-005) | all channels 100 %, pans hard left, export "What I hear" WAV 24 | item shows "Lowered by X dB"; file peak -0.1 dBFS (`max(abs(x))` = 0.98855) |
| X7 | Range (US4) | loop 12.0-28.5 s, export with "Only the loop region" | 16.500 s +/- 1 ms |
| X8 | Library management (US3, FR-007..FR-009) | save three items, rename one, delete one, delete the source track of another | list/size update without reload; renamed download file name; deleted item gone from disk (`assets/<id>/` removed); the orphaned item still plays and shows "source track deleted" |
| X9 | Restart (edge case) | start a long export (15-min test file, WAV 24, many channels) and `docker compose restart worker` while it renders | item becomes "failed (interrupted by restart)"; "Try again" makes it ready |
| X10 | Help and levels (FR-014, SC-006) | as Beginner: only "Save to library" + format; as Advanced: bit depth, bit rate, range, selected channels; every new area opens its help topic | as listed; a first-time user saves and finds a stem in under a minute |
