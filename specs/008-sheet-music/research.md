# Research: Sheet Music per Instrument

Feasibility spike and decisions for feature 008 (architect, 2026-11-15). Earlier desk research:
`docs/research/roadmap-003-011.md` section 7. Binding design: ADR-017.

## Setup of the spike

| Item | Value |
|---|---|
| Machine | Intel Core Ultra 9 285K (24 threads), 127 GB RAM, Windows 11 + Docker Desktop (WSL2). GPU not used (the worker image is the CPU variant; the GPU may be shared, `nvidia-smi` showed ~1.1-1.3 GB used by others) |
| Container | throw-away container from `stemstudio/worker:local` (Python 3.12.16, torch 2.15.0+cpu, onnxruntime 1.32.0, librosa 1.0.1, numpy 2.5.4), `--cpus 8`; extra packages installed with `pip --target /tmp/pk --no-deps` inside the container only; removed afterwards. The running `stemstudio` project was not touched |
| Packages tried | basic-pitch 0.4.0 (for its ONNX file + post-processing), piano_transcription_inference 0.0.6 + torchlibrosa 0.1.0, music21 10.5.2, verovio 6.4.1, cairosvg 2.9.1 (+ cairocffi 1.7.1, cssselect2 0.10.1, tinycss2 1.5.1, defusedxml 0.7.1), pypdf 7.1.0, mir_eval 0.8.2 (evaluation only), fonttools (font conversion only) |
| Input | "Hero of the Day" (4:22; track `447e5a4c...`): stems `bass`, detail stems `vocals.lead`, `guitar.electric-guitar`, `drums.{kick,snare,toms,hihat,cymbals}`, its beat grid (117.7 BPM, 4/4, 478 beats, 120 downbeats, reliable), all read-only from `stemstudio_api-data`. Synthetic material generated in the spike (below) for accuracy |

## S-01 basic-pitch without TensorFlow

- The pip package requires `tensorflow<2.15.1` on Python >= 3.11 (metadata), which conflicts
  with numpy 2.5 and adds GBs. Installed with `--no-deps`, its `inference.predict()` runs the
  bundled **`nmp.onnx` (230,444 bytes, SHA-256 `2c3c1d14...59a0ec`)** with onnxruntime.
- Import chain problems: `pretty_midi` -> `six`, `importlib_resources`; `note_creation` ->
  `resampy` -> `pkg_resources` (gone from setuptools 84). Decision: vendor the post-processing
  (Apache-2.0, `note_creation.py` ~500 lines, we need the note-event part only) and the windowing
  logic of `inference.py`; drop pretty_midi/mir_eval/resampy.

## S-02 basic-pitch speed on real stems (8 CPU threads)

| Input (4:22) | Time | Raw notes | Pitch range (MIDI) |
|---|---|---|---|
| `bass` (default thresholds 0.5/0.3) | 15.1 s | 973 | 24-63 |
| `bass` (thresholds 0.6/0.4) | 10.0 s | 473 | - |
| `vocals.lead` | 16.6 s | 516 | 24-99 |
| `guitar.electric-guitar` | 27.8 s | 1,947 | 32-98 |

The out-of-range notes (24 for vocals, 99) are artefacts: hence the per-profile pitch range.

## S-03 basic-pitch accuracy on a synthetic single-voice line

Synthetic bass: 78 notes of a pentatonic set (MIDI 40-52), 8th/quarter notes at 120 BPM with
15 % rests, five-harmonic tone with exponential decay, 44.1 kHz. Metric: `mir_eval` note onset
F-measure (50 ms, pitch +/- 50 cents, offsets ignored).

| Thresholds (onset/frame/min length ms) | Raw P / R / F1 | + pitch range 28-67 | + range + monophonic (60 ms window) |
|---|---|---|---|
| 0.5 / 0.3 / 127.7 (defaults) | 0.614 / 1.0 / 0.761 | 0.886 / 1.0 / 0.940 | **1.0 / 1.0 / 1.0** |
| 0.6 / 0.4 / 127.7 | **1.0 / 1.0 / 1.0** | 1.0 / 1.0 / 1.0 | 1.0 / 1.0 / 1.0 |
| 0.5 / 0.3 / 80 | 0.386 / 1.0 / 0.557 | 0.736 / 1.0 / 0.848 | 0.951 / 1.0 / 0.975 |

Quantising the onsets to the 8th grid gave the same F1 (every hit landed on its grid slot).
False positives are harmonics/ghost notes; recall is perfect. Decision (ADR-017 D3): thresholds
0.6/0.4 plus range and monophonic cleaning for single-voice profiles; synthetic tests in the
suite (SC-002) guard the pipeline. Synthetic material is easier than real stems: real-song
quality is communicated as limits, not promised as a number.

## S-04 Piano model (ByteDance high-resolution piano transcription, CPU)

| Item | Result |
|---|---|
| Checkpoint | `note_F1=0.9677_pedal_F1=0.9186.pth`, 171,966,578 bytes, SHA-256 `c3fa9730725bf4a762f1c14bc80cd5986eacda01b026f5a4a2525cd607876141`, Zenodo record 4034264 (`/records/4034264/files/CRNN_note_F1%3D0.9677_pedal_F1%3D0.9186.pth`), CC BY 4.0 |
| Load | 6.2 s |
| Speed, 8 threads | 60 s of audio in 99.8 s (1.66 s per audio second) |
| Speed, 16 threads | 30 s in 44.4 s (1.48 s per audio second) |
| Estimate for 4:22 on the CPU | about 6.5-7.3 min; 15 min songs about 22-25 min |
| Synthetic polyphonic texture (20 s: triads + a melody note every 0.5 s, inharmonic piano-like partials; 156 notes) | **P 0.967 / R 0.949 / F1 0.958** |
| Real piano stems available | none with real piano content: "Hero of the Day" piano stem is -69 dB RMS (0 notes detected in 60 s, correct); "Monkeys Spinning Monkeys" piano stem -50 dB (0 notes in 30 s) |
| Package problems | `models.py` imports matplotlib, `utilities.py` imports audioread, the inference class downloads with `os.system('wget ...')` (shell) -> vendor model + post-processor, own download with SHA-256 |

GPU speed was **not measured** (CPU image; GPU shared). The model is a ~20M-parameter CRNN on
10 s segments; a GPU run is expected to take seconds. Phase 0 measures it on the GPU image and
records `GpuNeed` (ADR-017 D7).

## S-05 Drums from detail stems (onset detection)

librosa `onset_strength` + `onset_detect` (delta 0.2 for kick/snare, 0.3 otherwise) on each drum
detail stem at 22,050 Hz, then mapped onto the beat grid (S-06). Total time **3.9 s**.

| Piece | Hits | Mean distance to the 16th grid (in 16ths) | Most frequent positions in the bar (beats from the downbeat: count) |
|---|---|---|---|
| kick | 546 | 0.10 | 2.0: 69, 1.5: 62, 0.0: 60, 0.5: 56 |
| snare | 331 | 0.06 | **1.0: 86, 3.0: 85** (beats 2 and 4), 0.0: 61, 2.0: 49 |
| toms | 39 | 0.09 | 0.5: 7, 0.0: 6, 1.0: 5 |
| hihat | 136 | 0.14 | 0.0: 37, 2.0: 35 |
| cymbals | 35 | 0.15 | 2.0: 18, 3.0: 9 |

The backbeat and the kick pattern are recognisable; onsets sit close to the grid (on average
within 0.15 of a 16th). Hi-hat is sparse in this song (ride/cymbal-heavy arrangement), and the
first bars are correctly empty (clean guitar intro). Bleed between detail stems (e.g. snare in
toms) shows up as extra hits; a per-piece loudness gate relative to the stem's peak is a Phase 0
tuning item. Rendered 16 bars: single staff, readable; missing rests and voices (fixed by D5 in
ADR-017).

## S-06 Quantisation on our beat grid

Method: fractional beat index `i(t) = interp(t, beats, 0..n-1)` (extrapolated with the median
period outside), minus the index of the first downbeat; snap to 1/4 beat (16th) or 1/2 beat
(8th); single-voice: each note ends at the next onset; minimum one grid step; notes before bar 1
form a pickup. On the bass stem: 473 raw notes -> 386 placed notes in **125 bars**; time for
cleaning + quantisation + music21 score: **0.11 s**; MusicXML + MIDI write **0.35 s**.

## S-07 Key estimation

music21 `analyze('key')` (Krumhansl-Schmuckler) on the cleaned bass notes: **A-flat major,
correlation 0.825**. The recording is tuned down a half step (E-flat tuning), so the notes are
a semitone below the written E-based key; the estimate fits the sounding pitches. Consequences:
show the confidence, allow an override, respell to the key. The beat grid has no key; estimating
from the notes is the only consistent source (ADR-017 D5).

## S-08 Engraving: music21 -> MusicXML -> Verovio -> PDF

| Step | Bass (125 bars) | Drums (16 bars) |
|---|---|---|
| Verovio 6.4.1 `loadFile` + `renderToSVG` per page (A4, scale 40) | 0.14 s, 3 pages | 0.02 s, 1 page |
| CairoSVG 2.9.1 `svg2pdf` per page + pypdf merge | 0.63 s | 0.05 s |
| PDF size | 179 KB | 22 KB |

Visual check (first pages rendered to PNG and inspected):

- Clefs (bass 8vb, percussion), key and time signatures, bar numbers, beams, ties, x noteheads:
  correct.
- **Tempo mark glyph rendered as a box**: Verovio writes SMuFL text glyphs with
  `font-family="Leipzig"`; Cairo has no such system font. Fix verified: converting Verovio's
  bundled Leipzig WOFF2 (`verovio/data/Leipzig.css`) to OTF with fontTools and installing it as a
  font (fontconfig) makes the note glyph render. Decision: install Leipzig (OFL-1.1) at image
  build; a test (SC-004) checks the SVG/PDF for the font.
- Spelling: sharps under a flat key signature (G# instead of A-flat) -> respell to the key.
- Rhythm at 1/16: readable but busy, many ties -> "simplified" eighths and next-onset durations
  for single-voice lines.
- Drums: no rests between hits and one voice -> fill rests, two voices.

## S-09 Dependencies and licences (checked from installed metadata)

| Package | Version | Licence | Role |
|---|---|---|---|
| music21 | 10.5.2 | BSD-3-Clause | score model, key analysis, MusicXML/MIDI writer |
| verovio | 6.4.1 | LGPL-3.0-only | MusicXML -> SVG (unmodified wheel, 30 MB) |
| cairosvg | 2.9.1 | LGPL-3.0-or-later | SVG -> PDF (unmodified) |
| cairocffi | 1.7.1 | BSD-3-Clause | cairo binding (libcairo2 already in the image) |
| cssselect2 / tinycss2 / webencodings | 0.10.1 / 1.5.1 / 0.6.1 | BSD | CairoSVG deps |
| defusedxml | 0.7.1 | PSF-2.0 | CairoSVG dep |
| pypdf | 7.1.0 | BSD-3-Clause | merge pages |
| torchlibrosa | 0.1.0 | MIT | piano model feature layers |
| music21 deps: chardet 7.7.0 (0BSD), jsonpickle 4.1.4 (BSD-3), more-itertools 11.2.1 (MIT), webcolors 25.10.0 (BSD-3), joblib/requests (present) | - | OSI | music21 also declares **matplotlib** (PSF-style licence) but imports it lazily: `import music21` works without it (0.5 s, one warning). Phase 0 decides: lock it, or exclude it with a uv override |
| basic-pitch (vendored subset + ONNX) | 0.4.0 | Apache-2.0 (code and model) | single-voice/polyphonic transcription |
| piano_transcription_inference (vendored subset) | 0.0.6 | MIT; weights CC BY 4.0 | piano |
| Leipzig font | Verovio 6.4.1 data | OFL-1.1 | text glyphs in the PDF |
| fonttools | latest | MIT | build-time font conversion only (not in the runtime image) |

Not used: TensorFlow, pretty_midi, mido, mir_eval, resampy (spike/evaluation only), YourMT3+
(GPL-3.0 code), ADTOF (CC BY-NC-SA code), LilyPond, MuseScore.

## S-10 Decisions (summary; binding text in ADR-017)

| # | Decision |
|---|---|
| D1 | Engines per profile: basic-pitch ONNX (vocals, bass, guitar, keys, piano-fast), ByteDance piano CRNN (piano-accurate), own onset transcription on drum detail stems |
| D2 | Vendor Apache/MIT subsets, download weights with SHA-256; LGPL engraving libs unmodified; no GPL code; YourMT3+ deferred to an optional separate service |
| D3 | Per-profile cleaning: thresholds 0.6/0.4, pitch range, monophonic reduction |
| D4 | Beat-grid quantisation (interpolated beats), bar 1 at the first downbeat, 1/16 or 1/8 |
| D5 | Key from notes + override + respelling; clefs/staves per profile; two-voice drum staff |
| D6 | PDF (Verovio/CairoSVG/pypdf), MusicXML, MIDI; PDF preview in the browser |
| D7 | `transcribe` lane, piano through DeviceRunner, capped threads |
| D8 | `AudioSource` inputs incl. library items, grid shifted by the source range |
| D9 | Fake transcriber; real notation/engraving in E2E |

## Commands used

```text
docker run -d --name ss-arch-spike-007-008 --cpus 8 -v stemstudio_api-data:/a:ro \
  -v <scratchpad>/spike:/spike --entrypoint sleep stemstudio/worker:local infinity
docker exec ss-arch-spike-007-008 /usr/local/bin/python3 -m pip install --target /tmp/pk --no-deps \
  music21 verovio cairosvg cairocffi cssselect2 tinycss2 webencodings defusedxml pretty_midi mido \
  basic-pitch piano_transcription_inference torchlibrosa mir_eval chardet jsonpickle more_itertools \
  webcolors six importlib_resources zipp pypdf fonttools
docker exec -e PYTHONPATH=/tmp/pk:/tmp/stub ss-arch-spike-007-008 python /spike/s008_pitch.py    # S-02, S-03
docker exec ... python /spike/s008_filter.py      # S-03 table
docker exec ... python /spike/s008_engrave.py     # S-05..S-08
docker exec ... python /spike/s008_piano.py       # S-04 (8 threads, downloads the checkpoint)
docker exec ... python /spike/s008_piano2.py      # S-04 (16 threads, synthetic polyphonic F1)
docker rm -f ss-arch-spike-007-008
```

(`/tmp/stub/resampy.py` is an empty module standing in for resampy; the piano scripts stub
`matplotlib` and `audioread` the same way.)

## Phase 0 measurements (2026-11-15, T005)

Throw-away containers from `stemstudio/worker:local-gpu` (torch 2.15.0+cu132) and
`stemstudio/worker:local` (CPU), packages installed with `pip --target /tmp/pk --no-deps` inside
the container only, volumes read-only, the running `stemstudio` project untouched; `nvidia-smi`
showed 13.6-13.7 GB free before every GPU run.

| Item | Result |
|---|---|
| Piano CRNN on the GPU (60 s synthetic polyphonic audio, 11 segments) | load 4.0 s; cold 4.46 s, warm 4.47 s = **0.075 s per audio second** (incl. post-processing and MIDI write) |
| Piano CRNN GPU memory | NVML peak delta **939-952 MiB** (CUDA context included); torch max allocated 340 MiB, reserved 612 MiB -> `GpuNeed` **1280 MiB** |
| Piano checkpoint download | 171,966,578 B in 37.8 s, SHA-256 `c3fa9730...76141` matches |
| Extra import found | `piano_transcription_inference/utilities.py` imports **mido** (vendored subset drops it with matplotlib and audioread) |
| basic-pitch ONNX at tag v0.4.0 | `https://github.com/spotify/basic-pitch/raw/v0.4.0/basic_pitch/saved_models/icassp_2022/nmp.onnx` -> `raw.githubusercontent.com/...`, 230,444 B, SHA-256 `2c3c1d14...59a0ec` matches |
| music21 10.5.2 without matplotlib | `import music21` 0.45 s; `analyze('key')` G major (0.928); MusicXML 4,176 B and MIDI (`MThd`) written; matplotlib never imported; one informational message at import -> **excluded by uv override** |
| Leipzig in verovio 6.4.1 and 6.4.0 | identical `verovio/data/Leipzig.css` (58,604 B, SHA-256 `aacdcb5d...1f8c`): one `@font-face` with `src: url(data:application/font-woff2;charset=utf-8;base64,...) format('woff2')`; WOFF2 43,824 B |
| WOFF2 -> sfnt (fontTools 4.68.0 + brotli) | Phase 0: 119,416 B, SHA-256 `9606f129...ef08` (fontTools restamped `head.modified`, so not reproducible). After the fix (`recalcTimestamp=False`, PostScript name `Leipzig` set; reconciliation 2026-11, two runs in a throw-away `python:3.12-slim` container with verovio 6.4.0): 119,356 B, SHA-256 `773c72dc26eb633b28d054f9880d19921ab2a1bbd28940a8df0441e377584a4b`, deterministic (the image build prints the current value), 0.07 s; sfnt 0x00010000 = TrueType -> `Leipzig.ttf`; `fc-scan`: "Leipzig / Medium / TrueType"; fontconfig 2.15.0 and libcairo2 1.18.4 already in the CPU image |
| Wheel licence files | `COPYING`, `COPYING.LESSER` (LGPL); no OFL text and no licence name records in the font |
| Version rule (>= 2 weeks) | verovio 6.4.1 (2026-11-03) and pypdf 7.x (2026-11-06..13) too new -> **verovio 6.4.0** (2026-10-30), **pypdf 6.21.4** (2026-10-28) |

