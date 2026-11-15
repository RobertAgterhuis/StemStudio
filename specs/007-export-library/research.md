# Research: Export Isolated Channels (Asset Library)

Feasibility spike and decisions for feature 007 (architect, 2026-11-15). Earlier desk research:
`docs/research/roadmap-003-011.md` section 6. Binding design: ADR-016.

## Setup of the spike

| Item | Value |
|---|---|
| Machine | Intel Core Ultra 9 285K (24 threads), 127 GB RAM, Windows 11 + Docker Desktop (WSL2) |
| Container | throw-away container from the CPU worker image `stemstudio/worker:local` (numpy 2.5.4, soundfile 0.14.0 / libsndfile, lameenc 1.9.0), `--cpus 8`, removed afterwards; the running `stemstudio` compose project was not touched |
| Input | reference song "Hero of the Day" (4:22 = 261.9 s, track `447e5a4c...`): the six stored stems and the stored drum detail stems, mounted **read-only** from the `stemstudio_api-data` volume |
| Output | a Windows bind mount (scratchpad); write times below include that slow path, so they are upper bounds |
| Mix used | the settings of a saved E2E mix: vocals 0.35, drums 0.9 (solo), bass 0.8 pan -0.4 (solo), guitar 0.8, piano 0.8, other 0.8 pan +0.25 (muted), master 0.7 |

## E-01 Speed (reference song, 4:22)

| Step | Time |
|---|---|
| Decode six 16-bit FLAC stems to float32 | 1.63 s |
| Mix (gate, gain, stereo panner, master) | 0.35 s |
| Write WAV 24-bit / WAV 16-bit | 3.93 s / 1.84 s (bind mount) |
| Write FLAC 24-bit | 1.27 s |
| Encode MP3 320 kbps CBR (lameenc quality 2) / 192 kbps | 5.69 s / 5.16 s |
| Single detail stem (`drums.snare`) decode + FLAC 24-bit write | 2.17 s |

Conclusion: a single stem is ready in about 2-4 s, a six-stem mix to MP3 in about 8 s; 22
channels (6 + 16 detail stems) extrapolate to about 15 s. SC-001 (15 s) and SC-002 (30 s) have
large margins. No GPU is needed (D2).

## E-02 File sizes (4:22, stereo, 44.1 kHz)

| Format | Size |
|---|---|
| WAV 24-bit | 69.3 MB |
| WAV 16-bit | 46.2 MB |
| FLAC 24-bit (mix) | 45.6 MB |
| FLAC 24-bit (single detail stem `drums.snare`) | 12.4 MB |
| MP3 320 kbps / 192 kbps | 10.5 MB / 6.3 MB |

The stored stems are FLAC **PCM_16**, 44.1 kHz, 2 channels (`soundfile.info`). Hence: a raw
single-stem export defaults to 16-bit (lossless and bit-identical, FR-004), a processed mix
defaults to 24-bit (gain and pan create values between 16-bit steps).

## E-03 Peaks and clipping

| Render | Peak |
|---|---|
| Spike mix (solo drums + bass, master 0.7) | -5.3 dBFS |
| All six stems at unity, centre | -0.47 dBFS, 0 samples >= 1.0 |

Stems sum to roughly the original mix, which is mastered close to 0 dBFS. With the panner on a
stereo input a channel can gain up to +3 dB on one side (`L + R·cos θ`), and phase relations
between stems can add up, so clipping is possible with legal settings. Decision (ADR-016 D4):
scale the whole render to -0.1 dBFS when the peak exceeds 1.0 and report the applied gain;
never clip silently and never limit (a limiter would change the sound).

## E-04 Memory and block-wise rendering

A float32 stereo stem of 4:22 is 92.4 MB; 22 channels of a 15-minute song would be about 7 GB if
loaded whole. Reading each input in 10 s blocks (`SoundFile.read(frames)`) and processing per
block gives exactly the same samples as the whole-file render (`np.allclose` True on the bass stem
with gain 0.8 and pan -0.4). Because the clip protection needs the global peak, the renderer
makes two passes over the inputs: pass 1 computes the peak, pass 2 scales and encodes (the
decode cost of E-01 doubles, still well inside SC-002). Peak memory: one block per input
(~3.5 MB each) plus the encoder buffer.

## E-05 Matching the engine's gain and pan (FR-003, SC-003)

The engine's channel chain is `GainNode(volume x gate) -> StereoPannerNode -> master GainNode`
(audio-engine contract 2.0.0). For **stereo** input the Web Audio specification defines the
StereoPannerNode as:

- `pan <= 0`: `x = pan + 1`, `gL = cos(x·π/2)`, `gR = sin(x·π/2)`, `L' = L + R·gL`, `R' = R·gR`
- `pan > 0`: `x = pan`, `gL = cos(x·π/2)`, `gR = sin(x·π/2)`, `L' = L·gL`, `R' = R + L·gR`

(at pan 0: `L' = L`, `R' = R`). The spike implements exactly this. Verification design:

1. `contracts/render-vectors.json` (Phase 0) lists (a) gate cases: a mix snapshot + selection ->
   the expected effective gain per channel, including expanded groups, solo on hidden channels,
   mute, `channels = null` vs explicit; (b) pan cases: pan -> the 2x2 matrix `[[a, b], [c, d]]`
   with `L' = a·L + b·R`, `R' = c·L + d·R`.
2. Consumers: engine Vitest (gate cases through the real `gate.ts` / `levelGate`), backend xUnit
   (`RenderPlanner`), worker pytest (`pan_matrix`, mixing a two-sample signal).
3. qa Playwright: in Chromium an `OfflineAudioContext` renders a constant stereo DC signal
   `(L, R) = (1, 0.5)` through a `StereoPannerNode` at each vector's pan; the output must match
   the vector matrix to within 1e-6. This proves the formula is the browser's, not ours.

Ramps (`setTargetAtTime`, 3 ms) do not apply: exports use static settings.

## E-06 Where to render (ADR-016 D2)

| Criterion | Worker (numpy) | Browser OfflineAudioContext | API (.NET) |
|---|---|---|---|
| Agent / pipelines without a browser (011, 008, 009) | yes | no | yes |
| Uses the stored stems without transfer | yes (read-only mount) | no (decode in page) | yes |
| Encoders available (FLAC, MP3) | libsndfile + lameenc already in the image | WAV only without extra code | none OSI without ffmpeg |
| Exact match with the engine | by vectors (E-05) | by construction | by vectors |
| Unit-testable, deterministic | yes | mocks only | yes |

Decision: worker.

## E-07 How the worker reads its inputs (ADR-016 D5)

Options: (a) the API uploads every input per job (multipart); (b) the worker reads its own cache
(standard job id), as detail jobs do; (c) a **read-only mount** of the API data directory.
(a) moves up to 56 x ~30 MB per export; (b) fails for wiped caches, detail stems and library
assets; (c) costs one compose line. Chosen: (c) with relative paths chosen by the API and
validated by the worker (pattern allow-list, no `..`, real path inside the root). The volume is
`api-data`, mounted at `/library:ro` in the worker; the non-Docker path sets
`STEMSTUDIO_LIBRARY_DIR` to the API's `StemStudio:DataDir`.

## E-08 Scheduling (ADR-016 D6)

The worker's `PriorityScheduler` runs standard and detail separation on one thread; a CPU detail
phase takes about 29 minutes for 4:22 (002 baseline). An export queued behind it would break
FR-011. Decision: a small `LaneScheduler` per light job type (FIFO, one thread) next to the
existing scheduler; render jobs run in the `render` lane. Rendering uses at most two numpy
threads' worth of CPU and does not touch the GPU, so it does not disturb separation noticeably.

## E-09 Formats and tags

- WAV/FLAC: libsndfile subtypes `PCM_16`, `PCM_24`; string tags `title`, `artist`, `comment`
  through `soundfile.SoundFile` attributes (supported for WAV and FLAC).
- MP3: lameenc 1.9.0 (LGPL-3.0+, already in the image through demucs; becomes a direct
  dependency), CBR 192/256/320, `set_quality(2)`. No ID3 tags: lameenc writes none and mutagen
  (present) is GPL-2.0+, which would pull a GPL library into our own code path. Documented
  limitation; the file name carries the information.
- 16-bit output of processed renders: TPDF dither (two uniform random values, +/- 1 LSB, seeded
  from the job id for reproducibility). Raw single inputs written at the source depth: no dither.

## E-10 Decisions (summary; binding text in ADR-016)

| # | Decision |
|---|---|
| D1 | Generic `Asset` entity and `assets/<id>/` folder owned by the API; `kind = audio` now, `score` in 008 |
| D2 | Render in the worker with numpy/libsndfile/lameenc |
| D3 | API resolves channels (gate rule v2, raw/mix, explicit/audible) into a flat render plan; shared vectors keep C#, TS and Python equal |
| D4 | Engine panner law for stereo input; peak > 1.0 -> scale to -0.1 dBFS and report; TPDF dither for processed 16-bit |
| D5 | Read-only library mount, API-chosen relative paths, worker-side validation |
| D6 | Separate `render` lane; block-wise two-pass rendering |
| D7 | `/api/assets` + `POST /api/assets/audio-exports` + `POST /api/assets/stem-packs`; closed JSON schemas for the agent |
| D8 | FLAC (default), WAV, MP3; tags for WAV/FLAC only |

## Commands used

```text
docker run -d --name ss-arch-spike-007-008 --cpus 8 -v stemstudio_api-data:/a:ro \
  -v <scratchpad>/spike:/spike --entrypoint sleep stemstudio/worker:local infinity
docker exec ss-arch-spike-007-008 python /spike/s007_render.py   # E-01..E-04
docker rm -f ss-arch-spike-007-008
```

`s007_render.py` decodes the six stems with `soundfile.read(dtype="float32", always_2d=True)`,
applies the gate and the stereo panner formula of E-05, sums, scales by the master volume and
writes WAV/FLAC with soundfile and MP3 with `lameenc.Encoder` (320 and 192 kbps, quality 2); it
also compares a 10 s block-wise render of one stem with the whole-file render.
