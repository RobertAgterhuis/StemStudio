# ADR-010: Detail separation models and pipeline

- **Status**: Accepted (architect; plan accepted by the CTO 2026-11-15)
- **Date**: 2026-11-15
- **Context documents**: `specs/002-detail-separation/{spec,plan,research,data-model}.md`,
  `docs/research/detail-separation-models.md`, ADR-009 (model weights exception), ADR-003
  (worker contract and polling), ADR-005 (audio engine).

## Context

Feature 002 splits the standard stems further (drums, vocals, other/guitar/piano) automatically
per song, in the background, without delaying standard imports, and lets the mixer expand a
parent into its detail stems. ADR-009 allows any available weights (local, non-commercial use)
but code must stay OSI. The research document gave unmeasured CPU estimates; the plan required
a measured feasibility spike on the reference machine (Intel Core Ultra 9 285K, 24 threads,
Docker Desktop with 62 GB, CPU only) against SC-001 (<= 30 min for a 4-min song).

## Decision

### 1. Models (measured, research.md D-01/D-02)

| Split | Model | Why | Licence status (ADR-009) |
|---|---|---|---|
| drums -> kick, snare, toms, hihat, cymbals | MDX23C DrumSep 6-stem (aufr33 & jarredou), overlap 2; ride + crash folded into `cymbals` | Best published per-part SDR, separate hi-hat; 50 s per audio minute | none stated upstream (original release gone; mirror) |
| vocals -> lead, backing | UVR MDX-Net KARA 2 (ONNX) | 18 s per audio minute; agrees with Mel-RoFormer Karaoke to 28 dB at 1/14 of the cost | MIT per UVR README (credit UVR) |
| other, guitar, piano -> instrument families | MVSep Mega 53-stem BS-RoFormer v1, overlap 2, **once on the sum** of the active instrument parents, folded into 8 families and re-assigned per parent with STFT power masks | Only model covering strings, brass, woodwinds, keys, synth, acoustic/electric guitar, percussion; 202-271 s per audio minute, so per-parent runs would break SC-001 | none stated (ZFTurbo release, MIT repo) |
| bass | not split | FR-002 | - |

Rejected after measurement: inagoy DrumSep (fastest, 14 s/min, but 4 stems, no hi-hat, weaker
snare/toms; kept as documented fallback if CPU time must drop), Mel-RoFormer Karaoke (118-256
s/min for the same result as KARA 2), Mega 53 with overlap 1 (2x faster but only ~10 dB
agreement with overlap 2, seam artefacts).

### 2. Pipeline

1. **Parent gate** (FR-003): mono frame RMS (2048/512); skip when fewer than 8 % of frames
   exceed -40 dBFS. Measured: every real stem >= 66 %, every bleed stem <= 6.4 %.
2. **Model step** per parent (drums, vocals) or per instrument group (sum of active
   other/guitar/piano).
3. **Folding** of model outputs into the closed `DetailStemName` set (one umbrella output per
   family; `strings` comes from `bowed_strings` because the model's `strings` output contains
   guitars, found by the double-count guard on the reference song).
4. **Re-assignment** (instrument group with >= 2 active parents): family share for parent p =
   iSTFT(STFT(family) x |P_p|^2 / sum_q |P_q|^2); shares sum to the family exactly.
5. **Sub-stem gate** (FR-004): keep when energy >= -20 dB relative to the parent and >= 5 % of
   frames above max(-50 dBFS, parent level - 25 dB).
6. **Checks** (FR-005/006): `rest = parent - sum(kept)`; not reliable when the model residual
   (drums/vocals: parent - sum of all model outputs) is above -15 dB or the family sum exceeds the
   parent by more than 1 dB (double counting); reconstruction of the written 16-bit FLACs must be
   <= -30 dB; fewer than 2 kept sub-stems -> nothing further.
7. **Checkpoint** per parent; final result is renamed into place atomically.

All thresholds are named constants in `detail/gating.py` / `detail/checks.py`, tuned on QA
fixtures; `separatorVersion` in the cached result changes when models or thresholds change.

### 3. Worker scheduling and lifecycle

- New job type "detail job" with its own store and endpoints (worker contract 1.1.0); idempotent
  per `audioSha256`; result cached under `cache/by-audio/<sha>/detail/`.
- One worker thread with a **priority scheduler**: a waiting standard job always runs before the
  next detail step; a running detail step finishes first (FR-011).
- Cancel is cooperative per inference chunk. Restart: non-terminal detail jobs become
  `failed` "interrupted by restart"; checkpoints are kept so Retry resumes.
- Models are downloaded on first use into `STEMSTUDIO_MODEL_DIR` (model-cache volume) via
  `.part` + SHA-256 verification; offline -> readable error; no network at import time.
- Inference code: vendored MIT code from ZFTurbo MSST (pinned commit, licence header and NOTICE
  kept, decorators/config loaders stripped); KARA 2 through `onnxruntime` with an STFT wrapper
  re-implemented after python-audio-separator (MIT, credited). `python-audio-separator` is not a
  dependency.
- `demix` accumulates only family sums (not 53 stems) to keep RAM at ~16 GB for 4 min.

### 4. Backend and UI

- `Track.DetailPhase` (owned JSON) with its own state machine; never changes the standard status,
  stems or mixes. Auto-queued on standard completion; `POST .../detail` (start/retry),
  `POST .../detail/cancel`; delete 409 while queued/running; detail stems served with the same
  Range/ETag semantics (backend contract 1.2.0).
- Mix settings gain optional `expanded` and `detailChannels` (default empty -> old mixes load
  collapsed and unchanged).
- Engine 2.0.0: channels addressed as `<parent>.<detail>`; groups loaded lazily on first expand;
  expand/collapse is a gate swap at one context time; the solo rule counts only the audible level.

## Consequences

- SC-001 is met on the reference machine with 30-40 % headroom (measured 19 min 58 s for the
  4:22 reference song, research.md D-03); machines ~1.5x slower will exceed it. The UI says the detail phase takes long.
- First detail phase downloads ~1.86 GB of weights; the worker needs ~16 GB RAM (4 min) up to
  ~24 GB (15 min) during the instrument step.
- Disk grows by up to ~3x per track.
- Two exceptions are tracked in plan.md Complexity Tracking: vendored code excluded from SIG and
  unit coverage with the detail integration test as compensation (accepted by CTO 2026-11-15),
  and a second marked integration test (accepted by CTO 2026-11-15).
- If StemStudio is ever distributed or used commercially, all three detail models must be
  re-evaluated (ADR-009).
