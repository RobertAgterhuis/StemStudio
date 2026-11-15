# Research: Detail Separation (Phase 0)

Inputs: `docs/research/detail-separation-models.md` (models and licences, **unmeasured**
estimates), ADR-009, spec.md. This document records the **measured** feasibility spike and the
resulting decisions D-01..D-12. Binding summary: ADR-010.

## Feasibility measurements

### Environment

| Item | Value |
|---|---|
| Machine | Intel Core Ultra 9 285K (24 threads), Windows 11, Docker Desktop (WSL2 kernel 6.18) |
| Docker Desktop limit (`docker info`) | 24 CPUs, **62.43 GiB** memory: sufficient (peak need ~16 GB, D-06) |
| Runtime | worker image `stemstudio/worker:local` (Python 3.12.16, torch 2.15.0+cpu, numpy 2.5.4, demucs 4.1.0), CPU only, default torch threads (24) |
| Throwaway additions (spike only, `--target /spike/lib`) | onnxruntime 1.32.0, rotary-embedding-torch 0.9.2, beartype 0.23.1, omegaconf 2.4.0, ml_collections 1.1.0 (+ their pure-Python deps) |
| Inference code | ZFTurbo Music-Source-Separation-Training (MIT) at commit `1f700c386e0dd763fe0a57242bb9420dce987354`: `utils.settings.get_model_from_config` + `utils.model_utils.demix`; KARA 2 through onnxruntime with an MDX-Net STFT wrapper written after python-audio-separator (MIT); inagoy DrumSep through the locked `demucs.apply.apply_model` |
| Inputs | real `htdemucs_6s` stems from the worker cache: "Hero of the Day" (Metallica, 4:22, cache `ec5c42f0...`), "Monkeys Spinning Monkeys" (Kevin MacLeod, 2:10, `cfa0b8d8...`), "Zero" (Alter Bridge, 4:39) for gate statistics; 60 s excerpts = 60..120 s |
| Baseline | standard `htdemucs_6s` separation: 159.6 s for a 4-minute song (`docs/MVP-REPORT.md`), i.e. ~40 s per audio minute |

Wall time = inference only (model load 0.6-5 s and file I/O excluded; listed separately where
relevant); "s/min" = seconds per minute of audio; peak RAM = `ru_maxrss` of the single benchmark
process (includes Python, torch, model, input and outputs).

### Results per candidate

| Candidate | Input | Wall time | s/min | Peak RAM | Download | Stems produced | Energy per output (dB rel. input) | Model residual | Sanity |
|---|---|---|---|---|---|---|---|---|---|
| MDX23C DrumSep 6-stem, overlap 4 (config default) | Metallica drums 60 s | 92.2 s | 92.2 | 1.7 GB | 437,652,699 B | kick, snare, toms, hh, ride, crash | -3.7, -5.0, -10.4, -12.9, -27.4, -11.9 | -26.0 dB | plausible, all non-empty |
| MDX23C DrumSep 6-stem, **overlap 2** | Metallica drums 60 s | 49.9 s | 49.9 | 1.6 GB | (same) | (same) | -3.7, -5.0, -10.4, -13.0, -27.4, -11.9 (identical to overlap 4 within 0.1 dB) | -25.9 dB | plausible |
| MDX23C DrumSep 6-stem, overlap 2 | **Metallica drums, full 4:22** | 234.2 s | 53.7 | 2.6 GB | | | -2.7, -7.2, -11.5, -14.9, -26.9, -14.6 | -25.8 dB | plausible |
| inagoy DrumSep (HDemucs `.th`, demucs 4.1.0, `weights_only=False`) | Metallica drums 60 s | 13.8 s | 13.8 | 2.8 GB | 167,400,043 B | bombo (kick), redoblante (snare), platillos (cymbals incl. hi-hat), toms | -4.0, -7.4, -8.6, -8.1 | -21.4 dB | runs unchanged on the locked demucs; agreement with MDX23C: kick 17.3 dB, snare 7.0 dB |
| UVR MDX-Net KARA 2 (ONNX, n_fft 5120, dim_f 2048, dim_t 2^8, compensate 1.065; UVR hash `1d64a6d2c30f709b8c9b4ce1366d96ee` confirmed) | Metallica vocals 60 s | 18.2 s | 18.2 | 2.1 GB | 52,786,726 B | primary = backing (model's "Instrumental"), lead = input - primary | backing -15.2, lead -0.2 | exact (difference) | plausible |
| UVR MDX-Net KARA 2 | **Metallica vocals, full 4:22** | 53.1 s | 12.2 | 2.5 GB | | | backing -15.9, lead -0.2 | exact | plausible |
| Mel-RoFormer Karaoke aufr33-viperx, overlap 4 | Metallica vocals 60 s | 255.7 s | 255.7 | 3.1 GB | 913,096,801 B | Vocals (lead); rest = input - lead | lead -0.2, rest -15.2 | exact | **agrees with KARA 2 to 28.1 dB** (lead) |
| Mel-RoFormer Karaoke, overlap 2 | Metallica vocals 60 s | 118.2 s | 118.2 | 3.1 GB | | | lead -0.2, rest -15.2 | exact | same result |
| MVSep Mega 53-stem BS-RoFormer v1, overlap 2 (config default) | Monkeys **guitar** 60 s | 270.8 s | 270.8 | **12.9 GB** | 1,368,919,887 B | 53 (overlapping hierarchy: e.g. strings ⊃ bowed_strings ⊃ cello) | top: bowed_strings -2.2, strings -3.4, bass -3.6, double-bass -4.2, cello -9.2, organ -18.8, keys -19.2, acoustic-guitar -30.1 | sum of 53 = +6.2 dB (overlap, as expected) | plausible: the Demucs "guitar" stem of this song is pizzicato strings |
| Mega 53, overlap 2 | Monkeys **other + guitar + piano summed** 60 s | 229.6 s | 229.6 | 12.8 GB | | 53 | strings -1.7, double-bass -4.5, woodwind -6.7, flute -7.1, synth -16.9, brass -29.6 | +8.2 dB | plausible (strings, whistle/woodwind) |
| Mega 53, overlap 1 | Monkeys guitar 60 s | 147.5 s | 147.5 | 12.5 GB | | 53 | strings agreement with overlap 2: 9.9 dB, woodwind 10.9 dB, keys 0 dB | | 2x faster but audible seam/consistency loss |
| Mega 53, overlap 2 | **Metallica guitar, full 4:22** | 881.8 s | 202.0 | **16.6 GB** | | 53 | guitar -0.3, strings -0.3, electric-guitar -0.7, acoustic-guitar -14.5, bass -23.6, keys -28.2, bowed_strings -39.7 | +5.7 dB | plausible (the song has an acoustic intro); shows that `strings` = *all* string instruments incl. guitars (D-05) |

SHA-256 and URLs of every file: D-07. All candidates could be downloaded and run; none failed.

### Full-song pipeline (reference song, chosen models)

"Hero of the Day" (4:22 = 261.9 s); drums, vocals and guitar pass the parent gate, other
(4.8 % active) and piano (0.0 %) are skipped. Run as three sequential processes (cold start,
model load and file I/O included in the wall clock):

| Step | Inference | s/min |
|---|---|---|
| drums, MDX23C overlap 2 | 234.2 s | 53.7 |
| vocals, KARA 2 | 53.1 s | 12.2 |
| guitar, Mega 53 overlap 2 | 881.8 s | 202.0 |
| **Total inference** | **1169.1 s = 19.5 min** | 267.8 |
| **Wall clock incl. 3 process starts, model loads, I/O** (08:29:42 -> 08:49:40 UTC) | **19 min 58 s** | |

Gates and checks of D-04 applied to these outputs (`outcome.py`; 16-bit FLAC round trip):

| Parent | Kept (dB rel. parent, active share) | Dropped | Model residual | Kept sum | Rest | Reconstruction | Outcome |
|---|---|---|---|---|---|---|---|
| drums | kick -2.7 (48 %), snare -7.2 (27 %), toms -11.5 (22 %), hihat -14.9 (47 %), cymbals -14.0 (33 %) | - | -25.8 dB | -0.1 dB | -25.8 dB | < -170 dB | split (5 + rest) |
| vocals | lead -0.2 (67 %), backing -15.9 (22 %) | - | exact | 0.0 dB | -108.7 dB | < -170 dB | split (2 + rest) |
| guitar | electric-guitar -0.7 (98 %), acoustic-guitar -14.5 (30 %) | strings (bowed) -39.7, keys -28.2, synth -38.2, woodwinds -46.0, brass -79.1, percussion -60.0 | n/a | -0.2 dB | -15.6 dB | < -170 dB | split (2 + rest) |

With the first folding draft (`strings` umbrella instead of `bowed_strings`) the guitar parent
kept "strings" at -0.3 dB **and** electric guitar at -0.7 dB (kept sum +5.7 dB): the
double-count guard (`MAX_DOUBLE_COUNT_DB`) rejected it as not reliable, which is the intended
behaviour and led to the corrected folding table in D-05. Reconstruction is exact because the
rest is computed from the already quantised kept stems (D-04).

### Run once on the sum versus per parent (Mega 53)

Families of the sum run were re-assigned to each parent with STFT power masks
(`share_p = iSTFT(STFT(family) x |P_p|^2 / sum_q |P_q|^2)`, n_fft 4096, hop 1024) and compared
with the per-parent run of the guitar stem (Monkeys, 60 s):

| Family | Sum run level | Share in guitar | Share in other | Share in piano | Per-parent guitar run | Agreement (SDR of share vs per-parent) |
|---|---|---|---|---|---|---|
| strings | -20.4 dBFS | -21.0 | -29.9 | -68.0 | -23.8 | 4.7 dB |
| woodwind | -25.4 | -66.1 | -25.6 | -41.6 | -61.9 | (both inaudible in guitar) |
| synth | -35.6 | -58.9 | -35.6 | -58.5 | -54.4 | (both inaudible in guitar) |

The gate decisions agree (guitar: strings yes, woodwind/synth no); waveform agreement is moderate
(4.7 dB) because the mask re-assignment cannot undo Demucs' own mis-assignment between parents.
Cost: one run (230-270 s/min) instead of one per active parent.

### Parent gate statistics (all real cached songs, mono frames 2048/512)

| Song | Stem | Level dBFS | Frames > -40 dBFS | Verdict with D-04 gate |
|---|---|---|---|---|
| Monkeys | vocals / drums / bass / piano | -88.3 / -70.5 / -83.4 / -50.1 | 0.0 / 0.0 / 0.0 / 0.7 % | skipped (correct: instrumental, no drums) |
| Monkeys | guitar / other | -24.2 / -26.9 | 88.8 / 85.6 % | split |
| Zero | other / piano | -42.9 / -76.1 | 6.4 / 0.0 % | skipped (bleed) |
| Zero | vocals / drums / guitar / bass | -18.0 .. -15.5 | 68.2 .. 95.4 % | split (bass not supported) |
| Hero of the Day | other / piano | -46.3 / -69.5 | 4.8 / 0.0 % | skipped (bleed) |
| Hero of the Day | vocals / drums / guitar | -24.6 / -23.1 / -23.1 | 66.9 / 68.2 / 98.3 % | split |

Real content: >= 66 % active frames; bleed: <= 6.4 %. A gate at 8 % separates them with margin.

### Commands used

```bash
docker volume create stemstudio-spike
docker run -d --name stemstudio-spike --user root --entrypoint sleep \
  -v stemstudio_worker-data:/src:ro -v stemstudio-spike:/spike stemstudio/worker:local infinity
docker exec stemstudio-spike /usr/local/bin/python -m pip install --no-deps --target /spike/lib \
  rotary-embedding-torch beartype onnxruntime coloredlogs humanfriendly flatbuffers protobuf sympy \
  mpmath omegaconf "antlr4-python3-runtime==4.9.3" ml_collections absl-py
docker exec stemstudio-spike /app/.venv/bin/python -I /spike/fetch.py      # weights + MSST zip, SHA-256
docker exec stemstudio-spike /app/.venv/bin/python -I /spike/prep.py       # stems -> WAV, 60 s excerpts
# per candidate (PYTHONPATH=/spike/lib:/spike):
python bench_msst.py mdx23c config_drumsep_mdx23c.yaml MDX23C-DrumSep-aufr33-jarredou.ckpt OUT LABEL drums.wav [--overlap 2]
python bench_msst.py mel_band_roformer mel_band_roformer_karaoke_..._config.yaml mel_band_roformer_karaoke_....ckpt OUT LABEL vocals.wav [--overlap 2]
python bench_msst.py bs_roformer mvsep_mega_model_bs_roformer_53_stems.yaml mvsep_mega_..._v1.ckpt OUT LABEL other.wav guitar.wav piano.wav [--overlap 1]
python bench_mdx.py UVR_MDXNET_KARA_2.onnx vocals.wav OUT
python bench_demucs.py model_drumsep.th drums.wav OUT
python gate_stats.py; python compare.py A.flac B.flac; python reassign.py; python outcome.py
docker rm -f stemstudio-spike && docker volume rm stemstudio-spike   # cleanup (done)
```

The spike scripts lived in the session scratchpad and the throwaway volume only; nothing was added
to the repository, `worker/pyproject.toml` was not changed, and the volume (weights, venv,
outputs, ~6 GB) was removed afterwards.

## D-01 Measured cost model

Seconds of inference per minute of audio on the reference machine (overlap as chosen):

| Step | s/min | 4:00 song | Notes |
|---|---|---|---|
| drums (MDX23C, overlap 2) | 50-54 | 3.4-3.6 min | |
| vocals (KARA 2) | 12-18 | 0.8-1.2 min | |
| instruments (Mega 53, overlap 2, one run) | 202 (full song) - 271 (60 s clip) | 13.5-18.1 min | regardless of 1, 2 or 3 active parents (sum run) |
| gating, re-assignment, checks, FLAC writing | ~2 | ~0.2 min | numpy/STFT, estimate from the scripts |
| model load (cold, from local disk) | - | ~0.2 min | |

## D-02 Model per split

| Split | Chosen | Rationale | Rejected |
|---|---|---|---|
| drums | MDX23C DrumSep 6-stem, overlap 2; `ride + crash -> cymbals`, `hh -> hihat` | best published per-part SDR (kick 14.5, snare 9.8, toms 10.6), separate hi-hat, overlap 2 is identical to overlap 4 at half the cost | inagoy DrumSep: 3.6x faster but 4 stems, snare agreement only 7 dB, weak toms (kept as fallback lever L2) |
| vocals | UVR MDX-Net KARA 2 | 6.5-14x cheaper than Mel-RoFormer Karaoke for the same result (28 dB agreement) | Mel-RoFormer Karaoke (cost) |
| other/guitar/piano | Mega 53 BS-RoFormer, overlap 2, one run on the sum of the active instrument parents (single active parent: run on it directly), folded into families (D-05) | only model with strings/brass/woodwinds/keys/synth/acoustic/electric guitar/percussion; sum run keeps SC-001 | per-parent runs (3x cost); overlap 1 (consistency loss) |
| bass | none | FR-002 | - |

## D-03 SC-001 verdict and time budget

**Measured end to end for the reference song "Hero of the Day" (4:22, drums + vocals + guitar
active): 19 min 58 s wall clock** (19.5 min inference), see "Full-song pipeline".

Scaled linearly to a 4:00 song: **about 18.5 minutes** when drums, vocals and at least one
instrument parent are active (the instrument step costs the same for 1, 2 or 3 active parents
thanks to the sum run); with the 60 s rates of the sum run (229.6 s/min, shorter clips carry more
overlap padding) the upper bound is about 21 minutes. **SC-001 (<= 30 min) is met on the
reference machine with about 30-40 % headroom.** It will not be met on machines roughly 1.5x
slower than the reference CPU; the UI notice says the phase can take a long time. The integrated
worker adds only gating/re-assignment/FLAC writing (~0.2 min) and keeps models loaded between
steps of one job.

Progress weights per step (for the overall percentage, from the measured s/min): drums 0.20,
vocals 0.05, instruments 0.74, finalise 0.01, renormalised over the steps that actually run.

Levers if a later measurement exceeds the budget (apply in order, re-measure each):
L1 Mega 53 overlap 1 (-45 % on the largest step; quality loss, D-01 table); L2 inagoy DrumSep
instead of MDX23C (-3 min); L3 skip the instrument step when only `guitar` is active and the
previous song outcome was "nothing further" (backlog, not planned).

## D-04 Gating and checks (constants in `detail/gating.py`, `detail/checks.py`)

| Constant | Value | Source |
|---|---|---|
| `FRAME`, `HOP` | 2048, 512 samples (mono mix-down) | research doc 5 |
| `PARENT_ACTIVE_DBFS` | -40 dBFS | gate statistics |
| `PARENT_MIN_ACTIVE_SHARE` | 0.08 | real >= 0.66, bleed <= 0.064 |
| `SUB_MIN_REL_DB` | -20 dB (energy rel. parent) | spike: weakest musically real sub-stem kept: backing -15.9 dB, hihat -14.9 dB; ride -26.9 dB dropped into cymbals |
| `SUB_ACTIVE_FLOOR` | max(-50 dBFS, parent level - 25 dB) | |
| `SUB_MIN_ACTIVE_SHARE` | 0.05 | |
| `MIN_KEPT` | 2 | FR-006 |
| `MAX_MODEL_RESIDUAL_DB` | -15 dB (drums, vocals: parent - sum of all outputs) | measured -25.8 dB (MDX23C), -21.4 dB (inagoy) on real drums |
| `MAX_DOUBLE_COUNT_DB` | +1 dB (instrument families: energy of the kept families' sum rel. parent) | overlap guard for Mega 53 umbrellas |
| `MAX_RECONSTRUCTION_DB` | -30 dB (written 16-bit FLACs: parent - (kept + rest)) | FR-005; measured < -170 dB on the full-song outputs |

The kept sub-stems are quantised to 16-bit first and `rest = parent - sum(quantised kept)` is
computed on the same grid, so reconstruction is exact unless the rest clips; the check therefore
catches clipping and implementation errors.

Order per parent: parent gate -> model -> folding/re-assignment -> sub-stem gate ->
`len(kept) < 2` -> nothing-further; model residual / double count -> not-reliable; rest =
parent - sum(kept); write FLACs; reconstruction check -> not-reliable (files discarded) ->
split. Thresholds are tuned on the QA fixtures (T010) and re-checked on the D1/D2 songs; changing
any constant bumps `separatorVersion`.

## D-05 Mega 53 folding table

The 53 outputs form an overlapping hierarchy. Each family takes exactly one umbrella output (no
summing of children, which would double count):

| Family (`DetailStemName`) | Mega 53 output |
|---|---|
| strings | `bowed_strings` (the `strings` output contains every string instrument including guitars; measured -0.3 dB on a distorted guitar stem) |
| brass | `brass` |
| woodwinds | `woodwind` |
| keys | `keys` (contains piano, digital piano, organ as the model defines them; organ is therefore not a separate family) |
| synth | `synth` |
| acoustic-guitar | `acoustic-guitar` |
| electric-guitar | `electric-guitar` |
| percussion | `percussion` |

All other outputs (vocals, drums, bass, individual instruments) are ignored and end up in `rest`.
Family accumulation happens inside the vendored `demix` (8 accumulators instead of 53).

## D-06 Memory

Mega 53 peak RSS 12.9 GB for 60 s and **16.6 GB for the full 4:22 song** with 53 accumulators (MSST `demix` as is); MDX23C 2.6 GB, KARA 2 2.5 GB.
With 8 family accumulators the output buffers shrink from 53 to 8 x 2 x frames x 4 bytes (4 min:
0.7 GB instead of 4.5 GB; 15 min: 2.5 GB instead of 16.8 GB). Planning ceiling: <= 16 GB for a
4-minute song, <= 24 GB for 15 minutes; the quickstart requires >= 20 GB for Docker Desktop and
the worker reports "Not enough memory for detail separation." on `MemoryError`.

## D-07 Models: sources, SHA-256, licence status (ADR-009)

Downloaded at first use into `STEMSTUDIO_MODEL_DIR/detail/<id>/`, verified, never committed.

| Id | File | Bytes | SHA-256 | URL | Licence status |
|---|---|---|---|---|---|
| `mdx23c-drumsep-6s` | MDX23C-DrumSep-aufr33-jarredou.ckpt | 437,652,699 | `d2a4aa53eb584d21eead358a4e66d1882ad182911be018f052b5da73be9096d0` | https://github.com/nomadkaraoke/python-audio-separator/releases/download/model-configs/MDX23C-DrumSep-aufr33-jarredou.ckpt | None stated upstream (original jarredou release removed); a mirror of the related 5-stem model states CC BY-NC-SA 4.0. Allowed under ADR-009 (local, non-commercial). |
| `uvr-mdxnet-kara-2` | UVR_MDXNET_KARA_2.onnx | 52,786,726 | `bf32e15105a09c0f7dddd2b67346146334d6f3ecb399ed7638eba2ab07cbf5f4` | https://github.com/TRvlvr/model_repo/releases/download/all_public_uvr_models/UVR_MDXNET_KARA_2.onnx | MIT per the UVR README statement; credit "Ultimate Vocal Remover (UVR) and its developers" required |
| `mvsep-mega-53` | mvsep_mega_model_bs_roformer_53_stems_v1.ckpt | 1,368,919,887 | `c62820893bbf86d4e734f966bd142d9157cfc8bb8e79e9d8f9ea553f3ff3519f` | https://github.com/ZFTurbo/Music-Source-Separation-Training/releases/download/v1.0.21/mvsep_mega_model_bs_roformer_53_stems_v1.ckpt | None stated (release notes without licence; repo MIT; training data undisclosed). Allowed under ADR-009. |

Configuration files were also measured but are **not downloaded at runtime**: their
hyper-parameters become constants in the vendored code (`config_drumsep_mdx23c.yaml` SHA-256
`17d1649a...89dd6`, 2,417 B; `mvsep_mega_model_bs_roformer_53_stems.yaml` SHA-256
`7e198062...51103`, 4,184 B; KARA 2 parameters from UVR `model_data_new.json` entry
`1d64a6d2c30f709b8c9b4ce1366d96ee`). Spike-only (not chosen): inagoy `model_drumsep.th`
167,400,043 B, `aefaa854...1423f`, https://github.com/ZFTurbo/Music-Source-Separation-Training/releases/download/v1.0.5/model_drumsep.th;
Mel-RoFormer Karaoke 913,096,801 B, `1de20d45...79813`,
https://github.com/TRvlvr/model_repo/releases/download/all_public_uvr_models/mel_band_roformer_karaoke_aufr33_viperx_sdr_10.1956.ckpt.

Total first-use download: 1,859,359,312 B (~1.86 GB). Download time on the reference
connection: 37 s + 4 s + 113 s.

THIRD-PARTY-LICENSES (CTO, T090) must list the three rows above verbatim plus the notice
"These weights are used under the model weights exception (constitution IV, ADR-009) for local,
personal, non-commercial use. Commercial use and redistributing images that contain these weights
are not covered." README carries the same notice and the UVR credit.

## D-08 Runtime code and dependencies

| Item | Version | Licence | Use |
|---|---|---|---|
| onnxruntime | 1.32.0 | MIT | KARA 2 |
| rotary-embedding-torch | 0.9.2 | MIT | BS-RoFormer |
| einops | already locked | MIT | RoFormer, MDX23C |
| Vendored MSST code (`bs_roformer.py`, `attend.py`, `mdx23c_tfc_tdf_v3.py`, `demix`) | commit `1f700c38` | MIT (Copyright (c) 2024 Roman Solovyev (ZFTurbo)) | inference; header + NOTICE kept; `beartype` decorators, `omegaconf`/`ml_collections` config loading and training code removed |
| MDX-Net STFT wrapper | own code after python-audio-separator `mdx_separator.py` | MIT (credit in NOTICE) | KARA 2 |

Not used: `python-audio-separator` (hard dependency `diffq`, CC BY-NC 4.0), `beartype`,
`omegaconf`, `ml_collections`, `PoPE_pytorch` (optional import in MSST, not needed by these
checkpoints).

## D-09 Worker scheduling, cache, restart, cancel

- Priority scheduler replaces the FIFO `JobQueue` (one thread): standard queue first; detail work
  in units of steps (plan, drums, vocals, instruments, finalise). FR-011 is satisfied; the
  longest unit is the instrument step (D-01).
- Idempotency and cache per `audioSha256`; a cached `detail/result.json` with the current
  `separatorVersion` completes a new job in one unit (SC-007).
- Restart: non-terminal detail jobs -> failed "interrupted by restart"; checkpoints are kept
  (unlike standard partial results, they are valid parent results).
- Cancel: queued -> cancelled immediately; running -> flag checked in the progress callback per
  inference chunk (MDX23C 3 s, KARA 2 ~6 s, Mega 53 20 s chunks) -> `DetailCancelledError`.
- Errors: data-model.md 4.2 fixed texts; no partial `detail/` is ever written.
- Fake mode: `STEMSTUDIO_SEPARATOR=fake` also selects fake detail models (tasks T042).

## D-10 Backend

`Track.DetailPhase` owned JSON (null = none); auto-queue in the completion transaction;
`DetailSynchronizer` in the existing `JobSyncService` loop; submission retried while the worker is
unreachable; detail stems copied to the backend data dir and served with `Results.File` range
processing; start/retry share one endpoint; cancel 503 only when a worker job exists; delete 409
while queued/running. Two EF migrations (`AddDetailPhase`, `AddMixDetailSettings`).

## D-11 Web and engine

Engine 2.0.0 channel ids `<parent>.<detail>`; lazy group loading on first expand (a 4-minute
stereo stem is ~85 MB decoded; loading all detail stems up front would more than double the
browser's memory); expand/collapse = gate swap at one context time; solo over the audible level
only. Web: grouped strips under the parent, expand toggle only for outcome `split`, detail
settings kept while collapsed; mixes without the new fields load collapsed.

## D-12 Fixtures

Synthetic fixtures for gating and reconstruction are generated inside the owners' tests (numpy);
`tests/fixtures/generate_detail_fixtures.py` (qa) provides E2E/integration inputs and a manifest of
expected outcomes. The fake detail models (T042) produce a fixed outcome table so Playwright can
assert split, nothing-further and not-reliable deterministically; "skipped (almost) silent" is
covered by unit tests and quickstart D2, because the fake standard separator copies the mix into
every stem (no silent parent exists in fake mode).
