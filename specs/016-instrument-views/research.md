# Research: Instrument Views

Phase 0 research of the 016 plan (architect, 2026-11-16). Decisions are binding through
ADR-031 (chords), ADR-032 (singing pitch) and ADR-033 (views architecture, tablature, parts).
Facts marked **verified** were checked on 2026-11-16 (GitHub API, downloads hashed in a
scratch directory, nothing executed); facts marked **P0** are measured by the architect in the
Phase 0 tasks (tasks.md T005-T007) before the wave starts.

## R-01 Chord recognition: options and licences

Constitution IV: code and libraries OSI only, no GPL import into the MIT worker; weights may use
the ADR-009 exception. Candidates:

| Candidate | Code licence | Weights | Runtime deps | Verdict |
|---|---|---|---|---|
| **BTC** - Bi-directional Transformer for Chord recognition (Park et al., ISMIR 2019), `jayg996/BTC-ISMIR19` | **MIT** (verified, GitHub licence API) | two checkpoints in the repo under the same MIT licence (verified, R-02) | PyTorch + librosa CQT (both already in the worker); the repo's pandas, pyrubberband, mir_eval, pretty_midi are training/evaluation only | **chosen** (default engine `btc`) |
| librosa chroma + HMM templates (own code) | ISC (librosa) + ours (MIT) | none | librosa, numpy (present) | **chosen as fallback** (`chroma-hmm`: offline first use, fake-free unit tests, CPU baseline) |
| madmom (`CPJKU/madmom`) deep chroma + CRF | BSD (code) | **CC BY-NC-SA 4.0** (verified, README) | Cython build; no release since 2018, does not build on Python 3.12 / numpy 2.5 without the unreleased master | rejected: unmaintained build, and the weights would need the exception for no gain over BTC |
| crema (`bmcfee/crema`) | BSD-2-Clause (verified) | in the package | Keras/TensorFlow | rejected: TensorFlow (GBs, second ML stack; same reason basic-pitch is vendored without TF in 008) |
| autochord | MIT (PyPI) | in the package | TensorFlow + the NNLS-Chroma Vamp plugin (GPL-2.0) | rejected: TF and GPL plugin |
| Chordino / NNLS-Chroma (`c4dm/nnls-chroma`), chord-extractor wrapper | **GPL-2.0** (verified) | n/a | Vamp host | rejected: GPL |
| Essentia `ChordsDetection` (`MTG/essentia`) | **AGPL-3.0** (verified) | n/a | C++ build | rejected: AGPL |
| Omnizart chord (Harmony Transformer) | MIT | in the package | TensorFlow, stale | rejected: TF |
| Chord-CNN-LSTM (Jiang et al. 2019) and apps built on it | no licence stated | unclear | PyTorch | rejected: no code licence (the ADR-009 exception covers weights only) |
| Chords from the 008 note transcription (music21 `chordify`) | BSD | basic-pitch Apache-2.0 | present | rejected as engine: misses voicings/bass, slow for all stems; kept as an idea only |

Accuracy expectations (published): BTC majmin WCSR about 0.83 and large-vocabulary about 0.75
on its cross-validation sets; template + HMM chroma methods typically 0.6-0.7 majmin on pop
songs. On our input the harmonic stems remove drums and vocals, which helps both methods. The
spec promises a practice aid (FR-064) and SC-001 is measured on synthetic material.

## R-02 BTC facts (verified 2026-11-16)

- Repository `https://github.com/jayg996/BTC-ISMIR19`, last commit
  `2682317be668032e6e4b269ded36adaa2ad57df0` (2020-05-23), licence MIT.
- Checkpoints (raw URLs at that commit, `test/` folder):

  | File | Bytes | SHA-256 | Vocabulary |
  |---|---|---|---|
  | `btc_model.pt` | 12,154,754 | `71c2c5db17e8c43b8a9a9da5db36ef2d667158c07a214eba16344c154c00bf54` | 25 classes (12 major, 12 minor, N) |
  | `btc_model_large_voca.pt` | 12,229,576 | `1673d23f8f9a55ae7f9e8b80a51da616debb22675b8d8b67ea6ce0ef37b0ab51` | 170 classes (12 roots x 14 qualities, N, X) |

- Both files are **legacy torch pickles** (first bytes `80 02 8a 0a`, the pre-1.6 format), a dict
  with `model` (state dict), `mean` and `std` (feature normalisation). Security rule: they are
  loaded only with `torch.load(..., weights_only=True)`; when the numpy scalars/arrays of
  `mean`/`std` need it, exactly those numpy reconstruct globals are allow-listed with
  `torch.serialization.safe_globals` (P0, T005 verifies which). `weights_only=False` is forbidden
  (quality-gate rule).
- Model (`run_config.yaml`): input CQT of 144 bins, 24 bins per octave, hop 2048 at 22,050 Hz
  (92.9 ms per frame), instances of 108 frames (10.0 s), 8 self-attention layers, 4 heads, hidden
  size 128. Inference in `test.py`: `(feature - mean) / std`, zero-pad to a multiple of 108,
  argmax per frame, run-length segments. Small model (about 3 M parameters by the checkpoint
  size): CPU inference is expected in seconds per song (P0 measures, T005).
- Vendoring: `btc_model.py` (7,361 B) plus the transformer modules and the `idx2chord` /
  `idx2voca_chord` tables of `utils/` into `harmony/vendor/btc/` with the MIT LICENSE and a header
  naming the commit; no import of mir_eval, pretty_midi, pandas or pyrubberband (Rubber Band is
  GPL; it is used only by the training augmentation and is not vendored).


**Phase 0 outcome (2026-11-18, this PC: 24 cores, torch 2.15 CPU, numpy 2.5.4).** Both checkpoints download from the pinned commit with the recorded SHA-256 (12,154,754 / 12,229,576 bytes). They are legacy (non-zip) pickles; `torch.load(weights_only=True)` fails without help and succeeds with exactly `numpy.core.multiarray.scalar`, `numpy.dtype`, `numpy.dtypes.Float64DType` allow-listed (`mean`, `std` are numpy scalars); keys `model`, `mean`, `std`; 3,018,137 / 3,036,842 parameters. CQT (144 bins, 24/oct, hop 2048) of 5 min: 1.31 s, 3,230 frames = 30 windows of 108. The BTC forward pass was not run in the design phase (executing the downloaded model code was not permitted there); T063 measures it, estimate < 2 s on 8 threads. `BTC_GPU_NEED` stays 512 MiB until the T140 NVML peak.

## R-03 Fallback chord method `chroma-hmm`

`librosa.feature.chroma_cqt` (hop 2048 at 22,050 Hz, the BTC frame rate so both engines share
post-processing), templates for 24 major/minor triads plus "N" (energy below -50 dB), cosine
similarity to emission probabilities, `librosa.sequence.transition_loop(25, 0.9)` and
`librosa.sequence.viterbi`. Majmin only (the Full vocabulary needs BTC; the UI says so). Cost
about 1-2 s per 5-minute song (P0). Used when BTC weights are not downloaded and cannot be
(offline), when the user picks "fast" at Expert, and as the deterministic engine of many unit
tests.


**Phase 0 outcome (2026-11-18).** Prototype chroma (librosa `chroma_cqt`, hop 2048) + 25-state template HMM (`transition_loop(25, 0.9)`, Viterbi) on 5 min: 3.72 s. Estimate factor chroma-hmm 0.013 s/s (data-model 7.7).

## R-04 Harmonic input and post-processing

- Input: sum of the standard stems except `drums` and `vocals` (`bass`, `guitar`, `piano`,
  `other`; with the 4-stem fallback model `bass` + `other`), mono, 22,050 Hz. Summing is done in
  the worker from the cached stem files (no render job, no new file kept).
- Beat snapping (reliable grid): every frame label is accumulated per beat (sum of frame
  posteriors, or votes for argmax engines), each beat gets its best label, consecutive equal
  beats merge; a change can only happen on a beat, so every chord lasts at least one beat
  (FR-011). Unreliable grid: segments shorter than 0.5 s are merged into the neighbour with the
  higher confidence.
- Key for spelling: the key region a chord starts in (R-13); major keys F, Bb, Eb, Ab, Db, Gb
  (and Cb) spell with flats, minor keys likewise by their relative major; otherwise sharps. The
  same rule is applied after transpose/capo by the
  backend (C#) and the engine (TS) from shared test vectors (data-model 2.5,
  `contracts/chord-spelling-vectors.json`, written in Phase 0).

## R-05 Live pitch tracking in the browser

| Candidate | Licence | Cost / fit | Verdict |
|---|---|---|---|
| **YIN** (de Cheveigné & Kawahara 2002), own TypeScript implementation in an AudioWorklet | ours (MIT); algorithm, no patent claims known | O(W x tau) per frame; with 16 kHz decimation, W = 512, tau 14..246: about 0.12 M multiply-adds per frame, 100 frames/s = 12 M/s (well under 1 % of a core) | **chosen** |
| pYIN (Mauch & Dixon 2014) live | ours | adds an HMM over many candidates; better octave stability, but buffering adds latency and the HMM costs more per frame | rejected live (used offline for the reference, R-06) |
| McLeod Pitch Method via `pitchy` | **0BSD** (verified) | good, small dependency | rejected: equal quality to own YIN; YIN keeps the same algorithm family on both sides (reference and live) and avoids a dependency in the worklet bundle |
| `pitchfinder` (npm) | **GPL-3.0** ("GNU v3" in its package.json, verified) | | rejected: GPL |
| aubio (WASM builds) | **GPL-3.0** (verified) | | rejected: GPL |
| CREPE / ml5.js (TF.js), torchcrepe | MIT (verified for `marl/crepe` and `maxrmorrison/torchcrepe`) | neural, accurate; TF.js or ONNX-Web runtime cannot run inside an AudioWorklet (no WASM threads, no WebGL), needs a Worker + SharedArrayBuffer and cross-origin isolation headers | rejected for 016 (complexity, page-wide COOP/COEP) |
| PESTO (`SonyCSLParis/pesto`) | **LGPL-3.0** code, weights not stated (verified) | ONNX, ~0.7 ms per inference, streaming | rejected for now: same Worker/ONNX-Web constraints as CREPE; kept as the upgrade path in ADR-032 |

YIN parameters (binding, data-model 3.2): input = the 015 input node, mono; decimation to
16 kHz with a 31-tap windowed-sinc low-pass (any context rate 44.1/48/96 kHz handled by a
fractional-phase resampler); window 512 samples (32 ms); hop 160 (10 ms); tau 14..246 (65 Hz -
1143 Hz); absolute threshold 0.15 on the cumulative-mean-normalised difference; parabolic
interpolation; voiced when the frame RMS >= -45 dBFS and the minimum normalised difference
<= 0.25. The worklet posts `{t, cents | null, clarity}` per hop over its `MessagePort`; the main
thread applies a 5-frame median (50 ms) before drawing and scoring.

CPU budget (SC-004): the worklet's `process()` must stay at <= 0.5 ms average and <= 2 ms worst
case per 128-frame block (2.67 ms at 48 kHz). The worklet measures itself with `currentTime`
deltas of overruns reported by the main thread (missed hops); after 50 missed hops in 5 s the
engine stops tracking (FR-045). P0 (T007) benchmarks the YIN core in Node on the reference
machine and in Chromium with a fake input.


**Phase 0 outcome (2026-11-18).** Node 22 (V8): the R-05 core (31-tap windowed-sinc decimator, W 512, hop 160, tau 14..246, threshold 0.15, parabolic interpolation) takes 0.021 / 0.019 / 0.010 ms average and 0.21 / 0.33 / 0.18 ms worst per 128-frame block at 44.1 / 48 / 96 kHz (20 s of a 220 Hz + 440 Hz tone; median 5700 cents = A3, correct). Chromium 159 headless (main thread, `page.evaluate`, no fake capture): 0.017 ms average, 0.30 ms worst at 48 kHz. SC-004 budget (0.5 ms average, 2 ms worst) holds with a margin of about 25x; the AudioWorklet run with a fake capture device is part of the qa fake-mic projects (T107).

## R-06 Reference melody (worker)

- `librosa.pyin` on the lead vocal (`vocals/lead` detail stem when split, else `vocals`), resampled
  to 16 kHz, `fmin` 65 Hz, `fmax` 1100 Hz, `frame_length` 1024, `hop_length` 160 (10 ms); frames
  with `voiced_prob < 0.5` unvoiced. Output: integer centi-semitones (MIDI x 100) per 10 ms,
  0 = unvoiced; 15 minutes = 90,000 frames (about 0.5 MB of JSON, compressed by the API).
- Cost: pYIN is the slowest analysis of the feature (the Viterbi over 100 pitch bins x 2 voicing
  states per frame). P0 (T006) measures 5 minutes of audio on the reference CPU; target <= 90 s.
  If it is slower, the documented fallback is `librosa.yin` + the same 5-frame median and the
  voicing rule of R-05 (about 10x faster), switched by `STEMSTUDIO_PITCH_ENGINE=yin` (data-model 1).
- Same algorithm family as the live tracker (YIN), so systematic offsets between reference and
  singer are small; scoring folds octaves anyway (data-model 3.4).


**Phase 0 outcome (2026-11-18).** `librosa.pyin` (fmin 65, fmax 1047, frame 1024, hop 160, 16 kHz) on 30 s took 0.99 s, about 10 s for 5 min; `librosa.yin` on 5 min 1.23 s. `pyin` stays the default (<= 90 s); estimate factors pyin 0.035 s/s, yin 0.005 s/s (data-model 7.7).

## R-07 Latency alignment

The singer reacts to what they hear: a sound scheduled at context time `T` is heard at
`T + outputLatency`; their voice reaches the worklet at `T + outputLatency + inputLatency`. A
pitch frame stamped at context time `c` therefore belongs to the song position
`transportPosition(c - roundTrip)` with `roundTrip = outputLatency + inputLatency`, which is
what 015's calibration measures (a loopback or clap round trip in ms). Without calibration the
engine uses `ctx.outputLatency + track.getSettings().latency` (or 0.03 s when not reported) and
the UI shows "not calibrated". The worklet stamps frames at the window centre:
`c = currentTime_at_block_end - (bufferedSamples + 256) / 16000`.

## R-08 Tablature

- MusicXML 4.0 supports tab staves: `<clef><sign>TAB</sign>`, `<staff-details>` with
  `<staff-lines>` and `<staff-tuning line=..><tuning-step/><tuning-octave/>`, `<capo>`, and per
  note `<notations><technical><string>n</string><fret>f</fret></technical>`. music21 10.5 writes
  `StringIndication`/`FretIndication` as technical elements and `TabClef`; the staff tuning and
  capo are written by a small post-processor over our own MusicXML (`xml.etree`, no user XML).
- Verovio renders guitar tablature from MEI (`notationtype="tab.guitar"`) and imports MusicXML
  tab staves; Phase 0 (T006) engraves a fixture with notation + TAB and TAB only (6, 5 and 4
  lines) on 6.4.0 and checks the SVG contains fret numbers and six/five/four lines. **Fallback**
  if 6.4.0 renders tab incorrectly: audio-ingest draws the TAB staff as SVG itself (lines and
  fret numbers per measure, measure widths taken from Verovio's notation staff), a bounded task
  recorded in ADR-033 D6.
- Fingering: dynamic programming (Viterbi) over note events; candidates (string, fret) per
  note, chord candidates = assignments to distinct strings (pruned to the 64 cheapest); cost =
  |hand position change| + 0.3 x stretch + 0.05 x fret above 12 + 2 for a chord span > 4
  fretted frets (rejected when another candidate exists); open strings are free of position.
  Hand position = mean fretted fret of the event. Known-good on scales and the open-position
  chord shapes of the golden fixtures (SC-002).


**Phase 0 outcome (2026-11-18, T006).** Verovio 6.4.0 engraves hand-written MusicXML with a `TAB` clef, `<staff-lines>` 6/5/4, `<staff-tuning>` and `<technical><string/><fret/>`: notation + TAB and TAB only render, the TAB staff has the right number of lines, fret numbers are text (`tabGrp` per note). `<capo>` is accepted but not printed. Decision ADR-033 D6: native Verovio; the notator writes "Capo <n>" as a `<direction>` above bar 1.

## R-09 Transposed parts

music21 `interval.Interval` transposition from sounding to written pitch with the key signature
transposed by the same interval (`Score.transpose` on a copy, then `KeySignature.transpose`),
part metadata `Instrument.transposition` exported as MusicXML `<transpose><diatonic/><chromatic/>
<octave-change/></transpose>`. Spelling after transposition uses the written key (008 rule 2.5).
Enharmonic key limits: written keys beyond 7 accidentals are respelled to the enharmonic key
(e.g. concert E major for an E-flat instrument -> C# major written, respelled to D-flat major).
MIDI is written from the concert-pitch score before transposition (FR-051).

## R-10 Drum hits

Reuse of the 008 `DrumTranscriber` (`analysis/drum_onsets.py`: onset detection per detail stem
with the bleed gate), without notation; the result keeps time, piece and strength. 3.9 s per
4:22 song in the 008 spike, so the 30 s target (SC-003) holds. General MIDI percussion map for
drum MIDI (FR-023): kick 36, snare 38, toms 47 (Low-Mid Tom; one stem for all toms), hi-hat 42
(closed), cymbals 49 (Crash 1), channel 10.

## R-11 Fake stack and deterministic E2E

- Fake chords: on the fake beat grid, one chord per bar cycling `C:maj, G:maj, A:min, F:maj`
  (Full vocabulary: `C:maj7, G:7, A:min7, F:maj/3`); confidence 0.9.
- Fake drum hits: the 008 fake pattern (kick on 1 and 3, snare on 2 and 4, hi-hat on every
  eighth, a crash on beat 1 of every fourth bar).
- Fake reference melody: during every fake lyric line (006 fake lyrics), one note per beat walking
  the C major scale from MIDI 60 to 72 and back; unvoiced between lines.
- Fake microphone: Chromium `--use-fake-device-for-media-stream
  --use-file-for-fake-audio-capture=<wav>` per Playwright project; fixtures generated by
  `tests/fixtures/generate_instrument_fixtures.py` (qa): `sing-on-pitch.wav` (sines with two
  harmonics following the fake melody one octave lower, to prove octave independence),
  `sing-semitone-off.wav` (+100 cents), `silence.wav`. The fake input is fed after the 015 fake
  calibration (round trip fixed by the fixture) so latency is deterministic.
- Tab, parts and chords-in-scores use the real notation and engraving on the 008 fake notes.

## R-12 Frame budget of the views

All engine views draw on the one 006 frame loop. Chord lane and banner chords: DOM text
elements (accessible, selectable), at most 40 lane items and 2 changed `data-state` per frame;
hit lanes and the pitch view: one `<canvas>` each, redrawn per frame with at most a few hundred
primitives (4-8 s of hits; 600 pitch points); layout is measured only on resize. Budget per
frame for all views together: <= 4 ms on the reference machine (engine benchmark test with fake
canvas counts primitives; SC-007 measured with a Chromium performance trace by the CTO).

## R-13 Key detection (scope addition S11)

Candidates (licences verified 2026-11-16 with the GitHub licence API):

| Candidate | Licence | Input | Verdict |
|---|---|---|---|
| **Own key HMM over chroma** (Krumhansl-Kessler and Temperley-Kostka-Payne profiles, librosa `chroma_cqt` + `sequence.viterbi`) | ours (MIT) + librosa ISC | the harmonic sum of R-04 (already computed for chords) | **chosen** |
| Chord-evidence term from the chart (root/third/fifth pitch classes of the segments) | ours | chord chart | **chosen** as the second emission term |
| music21 `analyze('key')` (Krumhansl-Schmuckler) | BSD-3-Clause (verified) | symbolic notes only | kept for 008 scores; not for audio |
| libKeyFinder (`mixxxdj/libkeyfinder`, `ibsh/libKeyFinder`) | **GPL-3.0** (verified) | audio | rejected: GPL |
| Essentia `KeyExtractor` | **AGPL-3.0** (verified, R-01) | audio | rejected: AGPL |
| madmom `CNNKeyRecognitionProcessor` | BSD code, CC BY-NC-SA weights | audio | rejected: does not build on Python 3.12/numpy 2.5 (R-01) |

Method (binding, data-model 2.8):

1. Beat-synchronous chroma (mean `chroma_cqt` per beat; 1 s frames without a reliable grid),
   L1-normalised, from the same mono 22,050 Hz harmonic sum as the chords.
2. 24 key states; emission = 0.5 x Pearson correlation with the rotated mean of the
   Krumhansl-Kessler and Temperley profiles + 0.5 x correlation of the chord-evidence pitch-class
   histogram (root, third, fifth weighted by duration) over the surrounding 4 bars; softmax with
   temperature 0.1.
3. Viterbi with a high self-transition probability (`p_stay = 1 - 1e-4` per beat; P0 T005 tunes it
   on the modulation fixtures).
4. Regions shorter than 8 bars (8 x beatsPerBar beats; 16 s without a grid) are merged into the
   neighbour with the higher mean emission.
5. Global key = the region key with the longest total duration; confidence = mean softmax
   probability of the chosen state over its frames; `alternative` = the relative major/minor when
   its mean probability is within 0.1.

Expected accuracy: profile-based key finding reaches about 0.7-0.8 (MIREX weighted score) on pop
material; harmonic stems and the chord term help. SC-010 is measured on synthetic cadences in all
24 keys and modulation fixtures (I-IV-V-I in C, then in D) rendered in the tests. Cost: below 1 s
per 5-minute song on top of the chords job (shared mixdown and CQT).


**Phase 0 outcome (2026-11-18).** Prototype key HMM over 24 Krumhansl profiles on beat chroma smoothed over +-8 beats, emission `exp(beta x correlation)`: with beta 2-3 and `p_stay` 0.995 per beat the C -> D fixture (8 bars each, then C again) is reported at bar 8 (one bar early) and back at bar 16, with no spurious region; beta 8 gave three short spurious regions at every `p_stay`, beta 1 lost the change at `p_stay` >= 0.995. Start values for T019/T020: beta 3, `p_stay` 0.995, +-8 beats; the >= 8-bar region merge stays.

## R-14 Capo advisor (scope addition S12)

- Shape of a chord with capo `c` at transpose `t`: root `(r + t - c) mod 12`, quality and bass
  interval unchanged (data-model 2.5: a capo raises every string by `c` semitones, so the shape is
  the sounding chord lowered by `c`).
- Open-position shapes (standard tuning, the common open shapes): major `C, A, G, E, D`; minor
  `Am, Em, Dm`; seventh `C7, A7, G7, E7, D7, B7`; minor seventh `Am7, Em7, Dm7`; major seventh
  `Cmaj7, Amaj7, Gmaj7, Emaj7, Dmaj7, Fmaj7`; sus `Asus2, Asus4, Dsus2, Dsus4, Esus4`; `N` is
  ignored. Everything else counts as a barre shape (F and Bm too).
- Score per capo `c` in 0..7: `open = duration of segments with an open shape / duration of all
  non-N segments`; rank by `open - 0.02 x c` (lower frets on near ties); top three with the shape
  key (global key lowered by `c`, spelled by the data-model 2.5 rule) and `open`; label "few open
  shapes" when the best `open < 0.5`. Other 6-string tunings use the same table (approximation,
  stated in the help).
- Transpose shortcut (FR-077): `c = t` for `t` in 1..7 keeps the original shapes; `c = 12 + t` for
  `t` in -5..-1 when <= 7; otherwise none.
- Implemented once in the backend Domain (`CapoAdvisor`); the engine only applies the chosen capo.
  Vectors: a `capo` section in `chord-spelling-vectors.json` (every root x quality x capo 0-12) and
  four fixture progressions (E-flat, A-flat, B-flat, F-sharp major) with the expected rankings
  computed by the generator from this rule (SC-011).