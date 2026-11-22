# ADR-032: Singing feedback: live pitch tracking and scoring (feature 016)

- **Status**: Accepted (owner, 2026-11-19, via the Tipoca project site; proposed by the architect, 2026-11-16).
  Architect Phase 0 notes (2026-11-18): `librosa.pyin` needs about 10 s for 5 min
  (16 kHz, hop 160; `pyin` stays the default, `yin` 1.2 s); the R-05 YIN core takes 0.019 ms average and
  0.33 ms worst per 128-frame block at 48 kHz in Node 22 and 0.017 / 0.30 ms in Chromium 159 (budget 0.5 /
  2 ms). Latency source = the 015 `LatencyMethod` (the engine keeps no calibration; Blazor passes it).
- **Context documents**: `specs/016-instrument-views/{spec.md US4, research.md R-05..R-07,
  data-model.md 3}`, ADR-005 (engine), ADR-015 (karaoke clock), the 015 recording/latency ADR
  (written in parallel), constitution V (sample-accurate timing, no timers for audible timing)

## Context

MoSCoW S9: a pitch line of the singer against the lead vocal and a per-line score in the karaoke
banner, using the 015 input and latency compensation. The singer's pitch must be tracked in real
time in the browser; the reference must come from the separated lead vocal. Code must be OSI and
not GPL; the engine is MIT TypeScript; CPU budget must leave the mixer and other views smooth.

## Decisions

### D1 Live: own YIN in an AudioWorklet

A TypeScript YIN (de Cheveigné & Kawahara 2002) in a dedicated AudioWorklet processor
(`pitch-tracker`), fed by the 015 input node: 16 kHz decimation, 512-sample window, 10 ms hop,
65-1143 Hz, threshold 0.15, parabolic interpolation, RMS and clarity voicing gate (research R-05).
Cost about 12 M multiply-adds per second (< 1 % of a core). No dependency: `pitchy` (0BSD, MPM)
would also be acceptable but gives no quality gain; `pitchfinder` and aubio are GPL;
CREPE/PESTO need a neural runtime outside the worklet and cross-origin isolation (upgrade path,
not 016).

### D2 Reference: pYIN once per track in the worker

`librosa.pyin` (already a dependency) on the lead vocal detail stem when vocals were split, else
on the vocals stem, 10 ms frames, stored as integer centi-semitones (`vocal-pitch` analysis).
On demand (first open), not automatic (it is the slowest analysis). Fallback `yin` switch when
Phase 0 measures pYIN above 90 s per 5 minutes on the reference CPU.

### D3 Time alignment with the 015 round trip

A live frame stamped at context time `c` maps to the song position `transportPosition(c -
roundTrip)` (research R-07), reusing the ADR-015 karaoke clock formula with the calibrated
round trip of 015 instead of the output latency; uncalibrated, the browser's latency estimates are
used and the UI says so. No timer measures time; frames carry their context time.

### D4 Octave-independent, transpose-aware frame scoring per lyric line

Per voiced reference frame, hit when the folded cent error is within the tolerance (100/50/25 by
level); score = hits / voiced frames, per 006 lyric line (fallback: 014 sections, 4-bar blocks,
10 s blocks); the 012 transpose shifts the reference. Scores are computed in the engine, shown in
the banner, announced politely to screen readers, summarised on stop, and never sent to the
server (privacy; FR-047). The same scorer runs offline over a decoded 015 take (P2).

### D5 Fail safe

The worklet reports missed hops; after 50 in 5 s the engine stops tracking with a readable state
(`failed`, "too slow on this device"); playback continues. While feedback runs, the lead vocal is
muted by default so bleed of the original singer into the microphone cannot raise the score.

## Consequences

- A second worklet module in the engine build (`audio-engine-pitch.worklet.js`) next to any 012
  (SoundTouch) or 015 (recorder) worklet; served as a static file.
- The engine gains its first microphone consumer; permissions, device choice and calibration stay
  in 015.
- E2E determinism needs Chromium's fake audio capture with generated fixtures (research R-11).

## Alternatives rejected

| Alternative | Why not |
|---|---|
| Live pYIN | HMM over candidates adds look-ahead latency and cost; YIN + 50 ms median is enough live |
| CREPE (TF.js/ONNX-Web), PESTO (LGPL, ONNX) | Cannot run inside an AudioWorklet; a Worker + SharedArrayBuffer needs COOP/COEP on the whole app; PESTO weights licence unstated |
| pitchfinder, aubio | GPL-3.0 |
| Sending microphone audio to the worker | Latency, privacy, and the worker is not real-time |
| Absolute-octave scoring | Penalises every singer whose range differs from the original |