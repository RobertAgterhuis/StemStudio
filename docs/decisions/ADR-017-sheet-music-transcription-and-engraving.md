# ADR-017: Sheet music: transcription models, notation and engraving (feature 008)

- **Status**: Accepted (architect, 008 Phase 0, 2026-11-15; Phase 0 refinements below under
  "Phase 0 outcome")
- **Context documents**: `specs/008-sheet-music/{spec.md, research.md, data-model.md,
  contracts/DELTA.md}`, ADR-009 (model weights exception), ADR-010 (detail models), ADR-012
  (device policy), ADR-016 (asset library, lanes, read-only library mount),
  `docs/research/roadmap-003-011.md` section 7

## Context

The owner wants, per instrument, sheet music that can be generated and downloaded as PDF (spec
008). StemStudio already has the inputs: standard stems, detail stems (002), a beat grid with
BPM, downbeats and beats per bar (001), and from 007 an asset library with a worker lane model.
The constitution requires OSI code (weights may be non-commercial under ADR-009), every feature
working on the CPU (ADR-011), and honest quality statements (V).

The feasibility spike (research.md S-01..S-08, CPU only, throw-away container, reference song
read-only) measured:

1. Spotify basic-pitch's pip package pulls TensorFlow; its **ONNX model (230 KB, Apache-2.0)
   runs with our onnxruntime** in 10-28 s per 4:22 stem on 8 CPU threads. The package's
   post-processing imports `resampy`, which imports the removed `pkg_resources`.
2. On a synthetic bass line, basic-pitch with thresholds 0.6/0.4 (or 0.5/0.3 plus a pitch-range
   and monophonic filter) reaches note-onset F1 1.0; with default thresholds and no filter 0.76.
3. ByteDance piano transcription (MIT code, CC BY 4.0 weights, 172 MB) reaches F1 0.958 on a
   synthetic polyphonic piano texture, but needs about 1.5-1.7 s per second of audio on 8-16
   CPU threads (a 4:22 song: about 7 minutes). Its package downloads weights with
   `os.system('wget ...')` and imports matplotlib and audioread.
4. Onset detection on the 002 drum detail stems (kick, snare, toms, hihat, cymbals) takes 3.9 s
   for the song and gives a plausible groove (snare 86/85 hits on beats 2/4).
5. music21 -> MusicXML -> Verovio SVG -> CairoSVG PDF works (3 A4 pages for 125 bars of bass in
   0.8 s), but the tempo glyph renders as a box until the Leipzig music font is installed as a
   system font; with it, it renders correctly.

## Decisions

### D1 One transcription engine per instrument profile

| Profile | Default for | Engine | Licence (code / weights) |
|---|---|---|---|
| `vocals` (melody) | `vocals`, `vocals.lead`, `vocals.backing` | basic-pitch ONNX | Apache-2.0 / Apache-2.0 |
| `bass` | `bass` | basic-pitch ONNX | same |
| `guitar` | `guitar`, `guitar.*` | basic-pitch ONNX | same |
| `keys` | `other`, `other.*`, `piano.*` details | basic-pitch ONNX | same |
| `piano` | `piano` | **accurate**: ByteDance piano CRNN (torch, GPU via DeviceRunner); **fast**: basic-pitch | MIT / **CC BY 4.0** |
| `drums` | `drums` (requires drums split by 002) | own onset transcription on the drum detail stems | MIT (ours) |

The user may pick any pitched profile for any stem (e.g. `guitar` for `other`). Piano's default
quality is `accurate` when the worker's device is a GPU and `fast` on the CPU; the UI shows the
estimated time of both.

### D2 Licence isolation

- **basic-pitch**: we vendor only the inference post-processing we need (note creation from the
  model's onset/frame/contour outputs) under `transcription/vendor/basic_pitch/` with its
  LICENSE and NOTICE, without TensorFlow, resampy, pretty_midi or mir_eval imports. The ONNX file
  is downloaded at first use into the model cache with a pinned URL and SHA-256 (as ADR-010).
- **Piano**: we vendor the MIT model definition and the regression post-processor under
  `transcription/vendor/piano_transcription/` (LICENSE kept), drop matplotlib/audioread and the
  `os.system` download; the checkpoint is downloaded by our model manager (pinned Zenodo URL,
  SHA-256 `c3fa9730...6141`). `torchlibrosa` (MIT) is a dependency for its two feature layers.
  CC BY 4.0 attribution is shown in the score's info panel, in the help topic and in
  `docs/THIRD-PARTY-LICENSES.md`.
- **YourMT3+ (GPL-3.0 code, Apache-2.0 weights) is not used.** Isolating it would be lawful
  (separate container, HTTP, our code stays MIT), but it brings a 2024 dependency stack
  (lightning, pinned transformers), a second torch image and several GB of weights for one
  instrument, while the 002 drum detail stems already separate kick, snare, toms, hi-hat and
  cymbals. If drum quality proves insufficient, a separate `transcriber-mt3` service behind the
  same worker contract is the documented path (new ADR). ADTOF stays excluded (CC BY-NC-SA code).
- **Engraving**: music21 (BSD-3) in-process; Verovio 6.4.0 Python wheel (6.4.1 was < 2 weeks old; same Leipzig.css) (LGPL-3.0-only) and
  CairoSVG 2.9.1 (LGPL-3.0-or-later) used **unmodified** as separately installed packages
  (replaceable, so the LGPL relinking condition holds), with cairocffi (BSD-3), libcairo (system,
  LGPL-2.1/MPL-1.1, already in the image), pypdf (BSD-3). The Leipzig font (OFL-1.1, shipped
  with Verovio) is installed as a system font at image build. No GPL library is imported by our
  code.

### D3 Notes are cleaned per profile before notation

Pitch range per profile (e.g. bass MIDI 28-67, vocals 40-84), monophonic reduction for `vocals`
and `bass` (one note per onset window of 60 ms, highest amplitude wins, and every note ends at the
next onset), minimum note length one grid step, basic-pitch thresholds onset 0.6 / frame 0.4.
The numbers are profile constants in the data model, tuned in Phase 0 on the synthetic fixtures.

### D4 Our beat grid is the time base

Note times are mapped to a fractional beat index by linear interpolation over the track's
detected beats (so tempo drift and rubato land in the right bar), extrapolated with the median
beat period outside the detected beats. Bar 1 starts at the first downbeat; earlier notes form a
pickup bar. The grid resolution is a sixteenth or an eighth ("simplified"); the default is a
sixteenth, except for the vocals profile (eighth). Time
signature `<beatsPerBar>/4`, tempo mark = the grid's BPM rounded. When the grid is not reliable,
the user (or the agent) must give BPM, beats per bar and the first downbeat; the request is
refused otherwise. Triplets, swing and tempo changes are not notated (documented limitation).

### D5 Key, spelling, clefs and staves

The key is estimated from the cleaned notes (music21 Krumhansl-Schmuckler) and shown with its
confidence; the user may override it. Pitches are respelled to the key (sharps or flats by key
signature). Clef/staff per profile: vocals treble; bass bass clef 8vb; guitar treble 8vb; piano
and keys grand staff (split at middle C, chords per staff); drums one five-line percussion staff
with two voices (hands up: hi-hat, cymbals, snare, toms; feet down: kick) and x noteheads for
hi-hat and cymbals. Drums have no key.

### D6 Outputs

Three files per score asset (ADR-016 `kind = score`): `pdf` (A4 portrait, Verovio SVG pages ->
CairoSVG -> pypdf merge), `musicxml` (uncompressed MusicXML 4.0 from music21) and `midi` (from the
quantised score, constant tempo). The browser previews the PDF natively (no SVG pipeline in the
UI). Each score stores a `ScoreFacts` record: profile, engine, quality, key + confidence + source,
BPM, time signature, bars, notes, pages, resolution, warnings and the attribution text.

### D7 Runs in a `transcribe` lane of the worker

Transcription jobs use their own `LaneScheduler` lane (ADR-016 D6), so they neither wait behind
separations nor block exports. Model steps go through the 003 `DeviceRunner`: the piano model
with a `GpuNeed` measured in Phase 0 (CPU fallback as in ADR-012); basic-pitch (230 KB) and the
drum onsets always run on the CPU. Torch/ONNX thread counts in this lane are capped
(`STEMSTUDIO_TRANSCRIBE_THREADS`, default 8) so a parallel CPU separation keeps most cores.

### D8 Inputs reuse the asset library

The request names an `AudioSource` (ADR-016 D7): a track stem / detail stem or an audio asset.
For an asset the API uses its provenance track's beat grid, shifted by the recipe's range start;
without one, a manual tempo is required. The result is a library item like any export.

### D9 Fake transcriber for the fake stack

`STEMSTUDIO_SEPARATOR=fake` also selects a fake transcriber that returns deterministic notes per
profile (a scale per bar, a rock groove for drums); quantisation, notation and engraving run for
real, so E2E tests download real PDF/MusicXML/MIDI files.

## Consequences

- The worker image grows by about 150-200 MB (music21, Verovio, CairoSVG and friends, font); the
  model cache by 172 MB (piano) + 0.2 MB (basic-pitch) on first use.
- Drum notation needs the drum detail stems: the UI offers "Split drums first" when missing.
- Piano on a CPU-only machine takes several minutes per song; the UI shows the estimate and the
  fast alternative.
- Quality is a practice lead sheet, not an engraved publication; the UI says so per profile.

## Alternatives rejected

| Alternative | Why not |
|---|---|
| basic-pitch pip package | pulls TensorFlow < 2.15.1 (clashes with numpy 2.5, huge) |
| YourMT3+ for drums/multi-instrument | GPL code would need its own container; stale heavy stack; drums are covered by detail stems + onsets |
| ADTOF (drums) | CC BY-NC-SA code: not OSI |
| omnizart | TensorFlow, unmaintained, weights licence not stated |
| MT3 (Magenta) | JAX/T5X stack, checkpoint licence not verified |
| LilyPond / MuseScore CLI engraving | GPL executables, +100 MB or Qt AppImage; Verovio output is sufficient for lead sheets |
| SVG rendering in the browser + print to PDF | the agent (011) and downloads need a server-side PDF |
| Key from the beat-analysis service | the beat grid has no key; estimating from the transcribed notes uses exactly what is notated |

## Phase 0 outcome (2026-11-15, combined 006 + 008 Phase 0)

- Contracts: worker-api **1.6.0**, backend-api **1.7.0**, ui-test-ids **1.7.0** (all on top of 006's
  1.5.0 / 1.6.0 / 1.6.0, which sit on 007's documents); engine unchanged (2.1.0 from 006).
- D7 measured on the RTX 5070 Ti (throw-away container, GPU image): piano CRNN load 4.0 s, 60 s
  of audio in 4.5 s (cold = warm, 0.075 s per audio second incl. post-processing), NVML peak
  952 MiB -> `PIANO_GPU_NEED = GpuNeed("Piano transcription", 1280)`; estimate factor 0.08 s/s
  + 5 s (+ about 40 s for the first 172 MB download).
- basic-pitch tag URL verified: `.../v0.4.0/basic_pitch/saved_models/icassp_2022/nmp.onnx`
  (redirects to raw.githubusercontent.com), 230,444 B, SHA-256 `2c3c1d14...59a0ec`.
- matplotlib: **excluded** with a uv override (`matplotlib ; sys_platform == 'never'`): music21
  10.5.2 imports it lazily; key analysis, MusicXML and MIDI writing work without it (one
  informational message at import). Saves ~40 MB and its dependency tree.
- Leipzig: the wheel embeds one WOFF2 `@font-face` in `verovio/data/Leipzig.css`; fontTools
  (with brotli) converts it to a 119,356-byte **TrueType** file (SHA-256 `773c72dc...584a4b` with the PostScript name set and the wheel's timestamps kept; the image build prints the current value;
  family "Leipzig"), so the installed file is `Leipzig.ttf` (`worker/scripts/install_music_font.py`
  names it by outline format). The wheel has no OFL text: the OFL-1.1 attribution comes from the
  upstream Leipzig repository and is recorded by the CTO.
- Vendoring: piano_transcription_inference also imports **mido** (`utilities.py`); the vendored
  subset must drop mido, matplotlib and audioread.
- pypdf pinned to 6.21.4 (7.x is less than two weeks old); `PdfWriter.append` is available.
- Provenance: score items carry `Provenance.scoreRecipe`; `Provenance.recipe` is null for them
  (additive alternative to a oneOf, so the 007 DTO keeps its shape; `ProvenanceDto.Recipe`
  became nullable).

