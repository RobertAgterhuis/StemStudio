# ADR-031: Chord recognition model, chord chart, song key and capo mode (feature 016)

- **Status**: Accepted (owner, 2026-11-19, via the Tipoca project site; proposed by the architect, 2026-11-16).
  Architect Phase 0 notes (2026-11-18): both checkpoints download with the pinned
  SHA-256 and load with `torch.load(weights_only=True)` plus exactly three allow-listed globals
  (`ports.BTC_SAFE_GLOBALS`); 3.02 M / 3.04 M parameters; CQT of 5 min 1.3 s, chroma-hmm 3.7 s. The BTC
  forward time itself is measured in T063 (running the vendored model code was not possible in the design
  phase; 30 windows of 108 frames for 5 min, estimate < 2 s on CPU). `BTC_GPU_NEED` 512 MiB stays until the
  NVML peak of T140.
- **Numbering**: ADR-031..033 are taken by feature 016. Features 012-015 are specified in
  parallel and may take ADR-021..030; the CTO renumbers at merge only on a collision.
- **Context documents**: `specs/016-instrument-views/{spec.md, research.md R-01..R-04,
  data-model.md 2}`, ADR-009 (weights exception), ADR-012 (device policy), ADR-015 (karaoke
  banner), ADR-017 (008 notation)

## Context

MoSCoW S1 asks for chord detection with a synced chord chart (chord symbols above the lyrics and a
chords lane in the mixer) for keys, guitar and singers. The constitution allows only OSI code and
libraries (no GPL import into the MIT worker); weights may use the ADR-009 exception; every
feature must work on the CPU. The worker already ships PyTorch, librosa and onnxruntime; it must
not grow a TensorFlow stack (rejected in 008 for the same reason).

Research R-01 compared BTC, madmom, crema, autochord, Chordino/NNLS-Chroma, Essentia, Omnizart,
Chord-CNN-LSTM and chords derived from note transcription.

## Decisions

### D1 BTC as the default engine, vendored

The Bi-directional Transformer for Chord recognition (Park et al., ISMIR 2019) is MIT-licensed
code with MIT-licensed checkpoints in its repository (verified 2026-11-16: commit `2682317b`,
`btc_model.pt` 12,154,754 B SHA-256 `71c2c5db...00bf54` for major/minor, `btc_model_large_voca.pt`
12,229,576 B SHA-256 `1673d23f...b0ab51` for 170 classes). It runs on PyTorch with a librosa CQT,
both present. Only the model definition, the transformer layers and the label tables are vendored
into `worker/src/stemstudio_worker/harmony/vendor/btc/` with the LICENSE and the commit id; the
repository's training/evaluation imports (pandas, mir_eval, pretty_midi, and pyrubberband, which
wraps the GPL Rubber Band) are not vendored. The weights are not even an ADR-009 case (MIT), but
follow its download rules: model manager, pinned URL at the commit, SHA-256, first use, never
committed.

### D2 Safe loading

The checkpoints are legacy torch pickles. They are loaded with `torch.load(weights_only=True)`
only; the numpy globals needed for `mean`/`std` are allow-listed explicitly with
`torch.serialization.safe_globals` (Phase 0 names them). A quality-gate rule forbids
`weights_only=False` in the worker.

### D3 A built-in fallback without weights: `chroma-hmm`

librosa chroma + major/minor templates + Viterbi smoothing (research R-03). It is used when the
BTC weights are missing and cannot be downloaded, on explicit choice at Expert, and in unit
tests. It knows only the Simple vocabulary; the UI says so (`vocabulary-reduced`).

### D4 Input is the harmonic stems, changes snap to beats

The engine reads the sum of all standard stems except drums and vocals, which removes the two
largest sources of chord-recognition errors. Labels are snapped to the beat grid (a change only on
a beat, at least one beat per chord) when the grid is reliable. Slash basses come from the bass
stem's chroma (Full vocabulary).

### D5 Harte labels in the data, display spelling at the edge

The worker stores Harte labels with root/quality/bass pitch classes and the estimated key. The
backend (C#, chord sheet, ChordPro) and the engine (TS, lane and banner) format and spell
symbols for the current transpose (012) and capo from one rule with shared test vectors
(`contracts/chord-spelling-vectors.json`), so no view stores pre-transposed text.

### D6 Runs on the `instrument-analysis` worker lane through the DeviceRunner

A new lane (one job at a time) for the three 016 analyses so they never wait behind a long CPU
piano transcription (008 `transcribe` lane) or a separation. BTC is small; the CPU is the
expected path; the GPU is used when the 003 policy allows (`BTC_GPU_NEED`, measured in Phase 0).

### D7 Automatic after separation, editable, out of date on re-separation

Chords start without a click when the standard separation completes (cheap; FR-010). Users at
Advanced can edit a segment; re-detection asks before overwriting edits. A changed stem
fingerprint marks the chart stale and re-runs it.

### D8 Key detection inside the chords job (scope addition S11, CTO 2026-11-16)

The song key and key regions come from an own HMM over beat-synchronous chroma of the same
harmonic sum, with Krumhansl-Kessler/Temperley profiles and a chord-evidence term from the chart
(research R-13); regions shorter than 8 bars are merged. No separate job, lane or kind: the key is
part of `ChordChart` (`key`, `keyRegions`), exposed by the API as `Track.key` and
`GET /api/tracks/{id}/key`. Spelling (D5) uses the key of the region a chord starts in.
Rejected: libKeyFinder (GPL-3.0), Essentia KeyExtractor (AGPL-3.0), madmom's key CNN (NC weights,
unbuildable), music21 key analysis (symbolic only; kept for 008 scores).

### D9 Capo mode: one advisor in the backend, display in the views (scope addition S12)

Capo mode is a property of the guitar family's chord display: shape = sounding chord lowered by
the capo (the D5 rule with `capo`). A deterministic `CapoAdvisor` in the backend Domain ranks
capo 0-7 by the duration-weighted share of open-position shapes (research R-14) and offers the
"original shapes" capo after a transpose; the engine and the chord sheet only apply the chosen
capo. Vectors in `chord-spelling-vectors.json` pin shapes and rankings for C# and TS. Bass and keys
never show a capo.

## Consequences

- One more small model in the model cache (about 12 MB per vocabulary), no new Python package.
- Vendored third-party code under `harmony/vendor/btc/` is excluded from coverage/SIG like the
  008 vendor folders; thin adapters are tested.
- Chord accuracy is a practice aid (about 0.8 on pop songs by the paper); limits are shown.
- The chart feeds three views and two exports (ChordPro, chord symbols in 008 lead sheets).

## Alternatives rejected

| Alternative | Why not |
|---|---|
| madmom deep chroma + CRF | Weights CC BY-NC-SA (would need the exception), no release since 2018, does not build on Python 3.12/numpy 2.5 |
| crema, autochord, Omnizart | TensorFlow stack; autochord also needs the GPL NNLS-Chroma plugin |
| Chordino / NNLS-Chroma, chord-extractor | GPL-2.0 |
| Essentia ChordsDetection | AGPL-3.0 |
| Chord-CNN-LSTM | No code licence |
| Chords from 008 note transcription | Misses voicings and bass, much slower over all stems |