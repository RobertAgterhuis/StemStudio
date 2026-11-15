# Research: Style Transfer with a Style Library (feature 009)

Architect, 2026-11-15. Builds on `docs/research/roadmap-003-011.md` section 8 (candidate models,
recommendation ACE-Step 1.5) and the 003 device policy (ADR-012). This file records the decisions
and the **feasibility spike** run on the reference machine. Feature 010's measurements
(lyrics-to-song, voice conversion) are in `specs/010-lyrics-to-song/research.md`; both spikes
used the same environment.

## Spike set-up (S-00)

| Item | Value |
|---|---|
| Machine | Intel Core Ultra 9 285K, 127 GB RAM, RTX 5070 Ti 16,303 MiB (sm_120), driver 610.88, Windows 11, Docker Desktop (WSL2) |
| GPU idle before the spike | 1,095-1,343 MiB used (desktop; less than the 3.2 GB of the roadmap measurement, it varies) |
| Environment | throwaway container from `python:3.11-slim` + ffmpeg/libsndfile/build-essential; ACE-Step 1.5 source at commit `ca1e85fe9430179831e6bc6be790c332190a3866` installed with `uv sync --frozen --no-dev` (its own lockfile) into `/app/.venv`; committed as a temporary image, deleted afterwards |
| Weights | downloaded once into a throwaway volume; `HF_HUB_OFFLINE=1` during runs |
| Input | reference song "Hero of the Day" (4:22), worker cache `ec5c42f0...`, mounted **read-only**: the six cached `htdemucs_6s` stems summed with ffmpeg `amix normalize=0` (the worker keeps no original mix; same proxy as 003 research G-00). Excerpt 60-105 s (45 s) and the full 261.9 s |
| Measurement | wall clock per call; `torch.cuda.max_memory_allocated/reserved`; NVML device memory used, sampled every 200 ms in-process (includes the desktop's ~1.1 GB and the CUDA context) |
| Clean-up | containers removed; temporary images and volumes deleted after the spike (see S-09) |

## S-01 Licences (verified 2026-11-15)

| Component | Licence | Source checked |
|---|---|---|
| ACE-Step 1.5 code | **MIT** (`Copyright (c) 2026 ACEStep`) | `LICENSE` at commit `ca1e85f`; GitHub licence API `MIT`; `pyproject.toml` `license = {text = "MIT"}` |
| ACE-Step 1.5 weights bundle `ACE-Step/Ace-Step1.5` rev `19671f406d603126926c1b7e2adc169acbcade22` | **MIT** | HF API `cardData.license = mit`, model card front matter `license: mit`; not gated |
| - contains `acestep-v15-turbo` (DiT 2B, 4.79 GB) | MIT (bundle) | SHA-256 `3f6e0797fad420a39bd33979eb6e840e30989e34a3794e843d23b60ec6e422d7` (`model.safetensors`) |
| - contains `acestep-5Hz-lm-1.7B` (3.71 GB, used by 010 only) | MIT (bundle) | SHA-256 `f161689da73e5ecefa28ff780d51c2d92a00f056d021d7933c779ed5c6cd7db8` |
| - contains `Qwen3-Embedding-0.6B` (text encoder, 1.19 GB) | MIT in the bundle; upstream `Qwen/Qwen3-Embedding-0.6B` is **Apache-2.0** | HF API of both repos |
| - contains `vae` (0.34 GB) | MIT (bundle) | - |
| nano-vllm (vendored in ACE-Step, `acestep/third_parts/nano-vllm`) | MIT | its `LICENSE` |
| Other ACE-Step runtime deps (torch 2.10.0+cu128, torchaudio 2.10.0, transformers 4.57.6, diffusers 0.37.1, accelerate, flash-attn 2.8.3 prebuilt wheel, torchao, vector-quantize-pytorch, gradio, modelscope, peft, lightning, ...) | OSI licences per PyPI metadata (BSD-3, Apache-2.0, MIT) - **not individually re-verified here**; CTO records the exact list from the generator image (tasks.md T083) | `uv.lock` of ACE-Step |
| NVIDIA CUDA 12.8 runtime wheels (cuBLAS, cuDNN, NCCL, ...) | NVIDIA licence (non-OSI) | system dependency per ADR-011, generator image only |

ACE-Step has **no forbidden-use clause**; its README disclaimer asks users to "verify the
originality of generated works, clearly disclose AI involvement, and obtain appropriate
permissions when adapting protected styles or materials". StemStudio's AI labels (FR-008) and
personal-use notice (FR-016) follow that.

## S-02 Environment and image size

| Measurement | Value |
|---|---|
| `uv sync --frozen` (ACE-Step lock, Python 3.11) | 262 s, **8.6 GB** `.venv` (torch + CUDA 12.8 wheels + flash-attn + triton + gradio ...) |
| Image (python:3.11-slim + apt + venv) | **15.4 GB** on disk, 5.16 GB compressed content |
| torch reported | `2.10.0+cu128`, CUDA 12.8, arch list includes `sm_120` (Blackwell works; no build needed) |
| `docker build` of ACE-Step's own Dockerfile (`nvidia/cuda:12.8.1-runtime` base) | aborted: the 2 GB base layer stalled at 1.31 GB on the network; not needed because the torch wheels bring the CUDA runtime |
| Weights download (HF, anonymous) | `Ace-Step1.5` bundle 10.09 GB in **147 s** |

Decision: own `generator.Dockerfile` on `python:3.12-slim` (or 3.11 if the lock does not resolve
on 3.12, T003) without the CUDA base image; generator image expected about 13-15 GB (devops can
trim gradio/training packages if the lock allows it: `uv sync --no-install-package`).

## S-03 Style transfer (cover task) - speed and VRAM per profile

Turbo DiT, 8 steps, shift 3.0, `audio_cover_strength` 0.5-0.55, batch 1, bf16, sdpa attention.

| Run | Wall time | torch reserved peak | NVML used peak | NVML minus idle (~1.1 GB) |
|---|---|---|---|---|
| DiT + text encoder + VAE load (from local disk) | **8-13 s** | 7.5 GB | 9.0-10.6 GB | 7.9-9.5 GB |
| Cover 45 s, first call after load | **4.6 s** | 8.9 GB | 12.0 GB | 10.9 GB |
| Cover 45 s, second call (warm) | **3.1-3.3 s** | 9.2 GB | 12.3 GB | 11.2 GB |
| Cover **full song 261.9 s** (two runs) | **9.8 / 10.0 s** | 10.6 GB | 13.7 GB | **12.6 GB** |
| Repaint (in-place) 15-30 s of a 45 s source | **3.1 s** | 9.5 GB | 12.6 GB | 11.5 GB |
| Low-memory profile (CPU offload of DiT/VAE/text encoder) load | 7.8 s | ~0 | 3.0 GB | 1.9 GB |
| Low-memory cover 45 s (two runs) | **19.4 / 14.7 s** | 5.1 GB | 8.5 GB | **7.4 GB** |
| Unload (drop handlers, `empty_cache`) | 0.3 s | - | - | torch free afterwards 14.8 GB of 16.3 GB (memory returned) |

Outputs: FLAC 48 kHz stereo 24-bit, exactly the source length (45.000 s / 60.000 s), RMS -17.9 dBFS
vs source -17.8 dBFS. Listening quality was **not** judged in the spike (owner check SC-004).

Consequences:

- SC-001 targets are met with large margins (45 s: 3-5 s warm, about 15-20 s cold incl. load;
  full song 10 s). The cold path is dominated by the one-time 10 GB download.
- **`GpuNeed`** (NVML-delta + 1 GiB margin, ADR-018 D4): `COVER_TURBO` = 13.6 GB for the worst case
  (full song); a 45 s cover needs about 12.2 GB. `COVER_TURBO_LOWMEM` = 8.4 GB. On the reference
  machine with ~15 GB free the standard profile fits; with a 3.2 GB desktop (roadmap measurement,
  13.1 GB free) a full-song cover selects the low-memory profile (about 4x slower, still < 2 min).
  Refinement of ADR-018 D4: the gate **chooses the profile by free memory** (standard -> low-memory
  -> wait) instead of only waiting; the OOM retry remains as second line.
- The "8 GB free VRAM" minimum in the spec (Clarifications) corresponds to the low-memory profile.
- ACE-Step has its own VRAM guard ("Insufficient free VRAM ...", seen in the 010 spike): the
  engine adapter maps it to `outOfMemory`/`gpuBusy` like a CUDA OOM.

## S-04 Lyrics-to-song

See `specs/010-lyrics-to-song/research.md` S-04 (same environment). Summary relevant to 009: the
language model is **not** loaded for covers (ACE-Step skips it for `cover`/`repaint`), so 009
never pays its memory.

## S-05 Parameter mapping (decision)

| StemStudio | ACE-Step `GenerationParams` / `GenerationConfig` |
|---|---|
| range restyle | `task_type="cover"`, `src_audio=<summed range>` |
| in-place section | `task_type="repaint"`, `src_audio=<whole summed source>`, `repainting_start/end`, `chunk_mask_mode="explicit"` |
| style caption (<= 512) | `caption` |
| strength Subtle/Light/Balanced/Strong/Free | `audio_cover_strength` 0.85/0.70/0.55/0.40/0.25 (README: 1.0 strong adherence, 0.2 "for style transfer") |
| takes 1-2 | `batch_size` 1-2, `seeds` explicit, `use_random_seed=False` |
| fixed | `inference_steps=8`, `shift=3.0` (turbo recommendation), `audio_format="flac"`, `thinking=False` |

## S-06 Alternatives considered for the integration

| Option | Verdict |
|---|---|
| ACE-Step's `acestep-api` REST server (`/release_task`, `/query_result`, `/v1/audio`) | rejected (ADR-018): not our contract, exposes model init/switch and Gradio-era options, no GPU lease |
| Python API (`AceStepHandler.initialize_service`, `generate_music`) inside our FastAPI | **chosen**; documented in ACE-Step `docs/en/INFERENCE.md`; spike used exactly this API |
| vLLM backend for the LM | n/a for 009 (no LM); for 010 see its research S-04 (rejected) |

## S-07 Cross-container GPU lease (`flock` on a shared volume)

Two `alpine` containers on Docker Desktop (WSL2) with the same named volume: container A holds
`flock /l/gpu.lock` for 8 s; container B started 1 s later blocked and acquired the lock after
**~7 s** (when A released), and a non-blocking attempt afterwards succeeded. The kernel releases the
lock when the holder exits. This is the basis of ADR-018 D3. (Both containers run in the same
Docker Desktop VM kernel; on Linux hosts it is the host kernel; a bind mount on a Windows path is
**not** used for the lock.)

## S-08 Levers if targets slip on other GPUs

- Low-memory profile (measured 7.4 GB, 15-19 s for 45 s).
- Fewer takes (batch 1).
- Range limit per job (already 8 min).
- INT8 quantisation / `torch.compile` (ACE-Step options; not measured; off by default).

## S-09 Spike clean-up and caches

Removed after the spike: containers `spike-*`, image `spike-acestep:tmp` (15.4 GB), volumes
`spike-gen-models` (13.4 GB weights), `spike-gen-io`, `spike-gen-uvcache` (uv cache ~9 GB); source
clones in the scratchpad. Not touched: the running `stemstudio` compose project and its volumes
(read-only mounts only). Cached afterwards: the `python:3.11-slim` and `alpine` base images (small).
Note: a `docker builder prune --filter until=3h` run during clean-up removed 2.62 GB of BuildKit
cache entries **older than 3 h** (shared build cache, not images or volumes); later builds of other
images may re-download layers once.

## R-01 Decision: model and engine

ACE-Step 1.5 turbo (MIT/MIT), cover/repaint tasks, Python API wrapped by the generator (ADR-018 D1,
D6). YuE2 not used (CTO). Rationale: only candidate with MIT code + weights, native cover task,
measured 10 s for a full-song restyle.

## R-02 Decision: GPU-only + amendment

CPU fallback not offered (CTO); constitution 1.3.0 amendment (ADR-018 D9). The low-memory profile
gives a GPU path for cards with >= 8 GB free.

## R-03 Decision: style library in the API with a deterministic prompt builder

ADR-018 D7: presets as an embedded JSON resource (versioned), user styles in SQLite, one pure
builder (unit-testable, reproducible: the caption is stored with each take).

## R-04 Curated presets v1 (24 presets, 13 genre families)

No artist, band, song or label names; tags are genre/instrument/mood words only. Default strength
in brackets.

| Key | Name | Family | Tempo feel | Instrumentation | Moods |
|---|---|---|---|---|---|
| `jazz-trio` | Jazz trio | Jazz | Swing | piano, upright bass, brushed drums | relaxed, warm |
| `big-band` | Big band swing | Jazz | Swing | brass section, saxophones, walking bass, drums | lively, bright |
| `bossa-nova` | Bossa nova | Latin | Laidback | nylon guitar, soft percussion, flute | breezy, intimate |
| `salsa` | Salsa | Latin | Driving | piano montuno, congas, timbales, horns | festive |
| `reggae` | Reggae | World | Laidback | offbeat guitar skank, organ, deep bass, one-drop drums | sunny, mellow |
| `delta-blues` | Acoustic blues | Blues | Laidback | slide guitar, harmonica, foot stomp | raw, soulful |
| `electric-blues` | Electric blues | Blues | Keep | overdriven guitar, Hammond organ, bass, drums | gritty |
| `unplugged` | Unplugged acoustic | Folk | Keep | acoustic guitars, cajon, upright bass | intimate, warm |
| `folk-americana` | Folk / Americana | Folk | Keep | acoustic guitar, banjo, fiddle, mandolin | earthy |
| `country` | Country | Country | Driving | twangy electric guitar, pedal steel, fiddle | upbeat |
| `orchestral` | Symphonic orchestra | Classical | Keep | strings, brass, woodwinds, timpani | cinematic, grand |
| `string-quartet` | String quartet | Classical | Keep | two violins, viola, cello | elegant |
| `solo-piano` | Solo piano | Classical | Laidback | grand piano | reflective |
| `synthwave` | Synthwave | Electronic | Driving | analog synthesizers, gated reverb drums, pulsing bass | nostalgic, neon |
| `house` | House | Electronic | Driving | four-on-the-floor kick, piano chords, synth bass | euphoric |
| `drum-and-bass` | Drum and bass | Electronic | Driving | fast breakbeats, reese bass, pads | energetic |
| `lofi-hiphop` | Lo-fi hip hop | HipHop | Laidback | dusty drums, Rhodes piano, vinyl crackle | chill, nostalgic |
| `boom-bap` | Boom bap | HipHop | Keep | sampled drums, upright bass, scratches | gritty |
| `funk` | Funk | Soul | Driving | slap bass, clavinet, horn stabs, tight drums | groovy |
| `classic-soul` | Classic soul | Soul | Keep | horns, electric piano, bass, backing choir | warm |
| `punk-rock` | Punk rock | Rock | Driving | distorted guitars, fast drums, bass | raw, rebellious |
| `heavy-metal` | Heavy metal | Metal | Driving | high-gain guitars, double kick drums, bass | aggressive |
| `ambient` | Ambient | Ambient | HalfTime | evolving pads, textures, soft piano | calm, spacious |
| `chiptune` | Chiptune | Electronic | Driving | 8-bit square waves, noise drums | playful |

Families used: Jazz, Latin, World, Blues, Folk, Country, Classical, Electronic, HipHop, Soul, Rock,
Metal, Ambient (13 >= 8, FR-003). Vocal handling: all presets `Keep` except `solo-piano`,
`string-quartet`, `orchestral`, `ambient`, `chiptune` (`Instrumental`). Strength: Balanced for all,
except `unplugged`, `electric-blues` (Light) and `chiptune`, `drum-and-bass` (Strong).

## R-05 Decision: imitation guard

ADR-018 D8 (phrases EN/NL, library artist names, curated deny-list). Genre words that are also
common words ("soul", "rock", "country") are **not** in the deny-list; artist names that collide
with genre words are excluded from the deny-list to avoid false positives (test in T024).

## Open questions (non-blocking)

| # | Question | Default if not answered |
|---|---|---|
| 1 | Constitution 1.3.0 amendment wording (ADR-018 D9) | as proposed; CTO executes T001 |
| 2 | Should the generator also run when Docker Desktop has a non-NVIDIA GPU (AMD ROCm build of ACE-Step exists)? | no (NVIDIA only, like 003) |
