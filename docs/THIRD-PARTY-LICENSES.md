# Third-party licences

StemStudio uses only open-source components with OSI-approved licences (constitution IV). This
file is CTO-owned and consolidated from the agents' hand-off notes. Exact versions are pinned in
`Directory.Packages.props`, `worker/uv.lock`, `src/audio-engine/package-lock.json`,
`e2e/package-lock.json`, `tools/quality/` and `dotnet-tools.json`. CI produces a machine-generated
licence report (pip-licenses, nuget-license, license-checker) as a build artifact.

## Runtime: .NET (API + Web)

| Component | Version | Licence | Source |
|---|---|---|---|
| .NET 10 runtime / ASP.NET Core | 10.0 | MIT | https://github.com/dotnet/aspnetcore |
| Microsoft.EntityFrameworkCore.Sqlite (+ Design) | 10.0.15 | MIT | https://github.com/dotnet/efcore |
| SQLite (bundled via SQLitePCLRaw) | 3.x | Public domain | https://sqlite.org/copyright.html |
| Microsoft.AspNetCore.OpenApi | 10.0.15 | MIT | https://github.com/dotnet/aspnetcore |
| Microsoft.Extensions.Http.Resilience | 10.11.0 | MIT | https://github.com/dotnet/extensions |
| Radzen.Blazor | 13.1.0 | MIT | https://github.com/radzenhq/radzen-blazor |

### Web UI (feature 004 design system)

| Component | Version | Licence | Source |
|---|---|---|---|
| Markdig (help content rendering, raw HTML disabled) | 1.5.1 | BSD-2-Clause | https://github.com/xoofx/markdig |
| YamlDotNet (help front matter; now also runtime) | 18.1.0 | MIT | https://github.com/aaubry/YamlDotNet |
| driver.js (guided tours, bundled in web-interop) | 1.10.2 | MIT | https://github.com/nilbuild/driver.js |
| Archivo variable font `Archivo[wdth,wght]` (self-hosted) | google/fonts | SIL OFL-1.1 | https://github.com/Omnibus-Type/Archivo |
| Radzen.Blazor free `software` base theme (customised via `--rz-*` / `--ss-*` tokens; no premium themes, no Radzen Studio) | 13.1.0 | MIT | https://github.com/radzenhq/radzen-blazor |

### AI composer agent (feature 011, `StemStudio.Agent`, used by the API)

| Component | Version | Licence | Source |
|---|---|---|---|
| Microsoft.Extensions.AI (+ `.Abstractions`; pulls in Caching/DependencyInjection/Logging abstractions 10.0.13 and System.Numerics.Tensors 10.0.13, all MIT) | 10.11.0 | MIT | https://github.com/dotnet/extensions |
| OllamaSharp (Ollama client, `IChatClient` implementation) | 5.5.0 | MIT | https://github.com/awaescher/OllamaSharp |
| Anthropic (official C# SDK; optional paid backend, constitution 1.3.0 principle IV exception, only called when Claude is selected and `ANTHROPIC_API_KEY` is set) | 13.3.0 | Apache-2.0 | https://github.com/anthropics/anthropic-sdk-csharp |

### Lyric writing assistant (feature 023, ADR-037; dictionaries vendored in the API image)

The dictionaries are copied byte-identical into `src/StemStudio.Api/LyricAssist/Dictionaries/` (7,740,587
bytes), with their licence files; `PROVENANCE.md` and `manifest.json` there record the source, pin and
SHA-256 of every file, and `VendoredDictionaryTests` checks them. Nothing is downloaded at run time.

| Component | Version | Licence | Source |
|---|---|---|---|
| WeCantSpell.Hunspell (managed Hunspell port; NuGet, used unmodified) | 7.0.1 | **MPL-1.1** (chosen from the MPL-1.1 / GPL-2.0 / LGPL-2.1 tri-licence; file-level copyleft, no effect on StemStudio's MIT code) | https://github.com/aarondandy/WeCantSpell.Hunspell |
| OpenTaal Hunspell `nl` word list | 2.20.23 (commit `1c22bc3d`, 2022-11-29) | BSD-3-Clause (chosen from "BSD-3-Clause and/or CC BY 3.0"; `nl/LICENSE.txt` kept with the data) | https://github.com/OpenTaal/opentaal-hunspell |
| SCOWL English word lists (Hunspell `en_US`, `en_GB-ise`, `en_GB-ize`, size 60) | `rel-2026.02.25` (SCOWL commit `7e99eda`) | SCOWL notice (MIT/HPND-style permissive licence, Kevin Atkinson; affix files under Geoff Kuenning's Ispell BSD-style licence; `README_<dict>.txt` kept with the data) | https://github.com/en-wl/wordlist |
| CMU Pronouncing Dictionary (CMUdict) | commit `74790861` (2025-10-24) | BSD-2-Clause wording (Carnegie Mellon University; `cmudict/LICENSE` kept with the data) | https://github.com/cmusphinx/cmudict |

LanguageTool is not used. No new model weights: the lyric model is `qwen3.5:9b` via Ollama (Apache-2.0, pulled by
the user, as for the 011 agent).

### Practice tempo time stretch (feature 012, ADR-021; separate `soundtouch.wasm` in the web image)

| Component | Version | Licence | Source |
|---|---|---|---|
| SoundTouch (time stretch / pitch shift, compiled unmodified into the separately served and replaceable `js/soundtouch/soundtouch.wasm`; complete corresponding source shipped at `/third-party/soundtouch/index.html` per LGPL-2.1 section 6) | 2.4.1 (release tarball SHA-256 `e07abf20...6733`, pinned in `third_party/soundtouch/soundtouch.lock`) | **LGPL-2.1** | https://www.surina.net/soundtouch/ , https://codeberg.org/soundtouch/soundtouch |
| StemStudio SoundTouch shim and ABI header (`third_party/soundtouch/shim/`, `include/`; StemStudio's own code, licensed like the library because it is compiled into the module) | ABI 1 | **LGPL-2.1-or-later** | `third_party/soundtouch/` |

The AudioWorklet processor (`js/time-stretch-processor.js`) and the engine remain MIT and contain no
SoundTouch code; they load the module through the documented ABI (`third_party/soundtouch/README.md`).

## Runtime: Python worker

| Component | Version | Licence | Source |
|---|---|---|---|
| FastAPI | 0.163.0 | MIT | https://github.com/fastapi/fastapi |
| uvicorn[standard] | 0.23.2 | BSD-3-Clause | https://github.com/encode/uvicorn |
| python-multipart | 0.0.33 | Apache-2.0 | https://github.com/Kludex/python-multipart |
| pydantic | 2.14.3 | MIT | https://github.com/pydantic/pydantic |
| Demucs (code) | 4.1.0 | MIT | https://github.com/adefossez/demucs |
| Demucs pretrained weights (`htdemucs_6s`, `htdemucs`) | - | **Unclear / likely CC BY-NC** (not OSI). Demucs README: "The license of the weights is not clear"; `htdemucs_6s` "was trained only on Meta internal dataset, which likely means CC-NC-BY license is applicable". Open constitution-IV issue, see below | https://github.com/adefossez/demucs#license |
| PyTorch (CPU build) | 2.15.0+cpu | BSD-3-Clause (bundled parts Apache-2.0/MIT/Zlib/BSL-1.0) | https://github.com/pytorch/pytorch |
| librosa | 1.0.1 | ISC | https://github.com/librosa/librosa |
| soundfile (+ libsndfile) | 0.14.0 | BSD-3-Clause (libsndfile LGPL-2.1+) | https://github.com/bastibe/python-soundfile |
| NumPy | 2.5.4 | BSD-3-Clause | https://github.com/numpy/numpy |
| yt-dlp | 2026.10.26 | Unlicense | https://github.com/yt-dlp/yt-dlp |
| spotDL | 4.6.0 | MIT | https://github.com/spotDL/spotify-downloader |
| FFmpeg / ffprobe (Debian package, separate executable) | distro | LGPL-2.1+ / GPL-2.0+ (Debian build) | https://ffmpeg.org/legal.html |
| Deno (JS runtime required by yt-dlp) | 2.9.8 | MIT | https://github.com/denoland/deno |
| tinytag (embedded cover and tags of uploads; feature 005) | 2.3.2 | MIT | https://github.com/tinytag/tinytag |
| Pillow (artwork normalisation; feature 005, was transitive) | 13.0.0 | MIT-CMU | https://github.com/python-pillow/Pillow |
| httpx (online lookups; feature 005, was a dev dependency; pulls in httpcore 1.0.9 BSD-3-Clause, h11 0.16.0 MIT, anyio 4.16.0 MIT, idna 3.20 BSD-3-Clause, certifi MPL-2.0) | 0.28.1 | BSD-3-Clause | https://github.com/encode/httpx |
| rapidfuzz (recording matcher; feature 005, was transitive) | 3.14.6 | MIT | https://github.com/rapidfuzz/RapidFuzz |
| lameenc (MP3 encoding of exports; feature 007, ADR-016; was transitive via demucs) | 1.9.0 | **LGPL-3.0-or-later** (bundles LAME, LGPL) | https://github.com/chrisstaite/lameenc |
| nvidia-ml-py (NVML bindings, GPU memory checks; both images) | 13.615.71 | BSD-3-Clause | https://pypi.org/project/nvidia-ml-py/ |
| stable-ts (lyrics alignment; feature 006, ADR-015) | 2.19.1 | MIT | https://github.com/jianfch/stable-ts |
| openai-whisper (alignment model code; triton dropped from the CPU lock) | 20250625 | MIT | https://github.com/openai/whisper |
| torchaudio | 2.11.1 | BSD-2-Clause | https://github.com/pytorch/audio |
| tiktoken (Whisper tokenizer) | 0.14.0 | MIT | https://github.com/openai/tiktoken |
| more-itertools | 11.2.1 | MIT | https://github.com/more-itertools/more-itertools |
| regex (via tiktoken) | 2026.9.29 | Apache-2.0 AND CNRI-Python | https://github.com/mrabarnett/mrab-regex |
| PyYAML (lyricsfile parsing, `safe_load` only; was a dev dependency) | 6.0.3 | MIT | https://github.com/yaml/pyyaml |
| music21 (notation, MusicXML/MIDI; feature 008, ADR-017; matplotlib excluded) | 10.5.2 | BSD-3-Clause | https://github.com/cuthbertLab/music21 |
| music21 dependencies: chardet / joblib / jsonpickle / webcolors | 7.7.0 / 1.6.0 / 4.1.4 / 25.10.0 | 0BSD / BSD-3-Clause / BSD-3-Clause / BSD-3-Clause | https://pypi.org/project/music21/ |
| verovio (engraving to SVG; unmodified wheel) | 6.4.0 | **LGPL-3.0-only** | https://github.com/rism-digital/verovio |
| cairosvg (SVG to PDF; unmodified) + libcairo2 (Debian) | 2.9.1 | **LGPL-3.0-or-later** (libcairo LGPL-2.1 / MPL-1.1) | https://github.com/Kozea/CairoSVG |
| cairocffi / cssselect2 / tinycss2 / webencodings (via cairosvg) | 1.7.1 / 0.10.1 / 1.5.1 / 0.6.1 | BSD-3-Clause | https://github.com/Kozea |
| defusedxml (via cairosvg) | 0.7.1 | PSF-2.0 | https://github.com/tiran/defusedxml |
| pypdf (PDF assembly) | 6.21.4 | BSD-3-Clause | https://github.com/py-pdf/pypdf |
| torchlibrosa (piano CRNN feature layers) | 0.1.0 | MIT | https://github.com/qiuqiangkong/torchlibrosa |
| soxr (resampling for alignment and transcription; direct pin, was transitive via librosa) | 1.1.0 | **LGPL-2.1-or-later** | https://github.com/dofuuz/python-soxr |
| Vendored inference code: Spotify basic-pitch subset (v0.4.0) | - | Apache-2.0 (LICENSE + NOTICE in `worker/src/stemstudio_worker/transcription/vendor/basic_pitch/`) | https://github.com/spotify/basic-pitch |
| Vendored inference code: piano_transcription_inference subset (ByteDance, Kong et al.) | - | MIT (LICENSE + NOTICE in `worker/src/stemstudio_worker/transcription/vendor/piano_transcription/`) | https://github.com/qiuqiangkong/piano_transcription_inference |
| Leipzig music font (shipped with verovio; converted to `Leipzig.ttf` at image build) | from verovio 6.4.0 | **OFL-1.1** | https://github.com/rism-digital/leipzig |
| fontTools[woff] + brotli (**build stage only**, converts the font; not in the runtime image) | 4.68.0 | MIT | https://github.com/fonttools/fonttools |
| ReportLab (setlist PDFs; feature 014, ADR-023; no extras: no accel, pycairo, bidi or shaping) | 5.1.0 | BSD-3-Clause-style ReportLab licence | https://www.reportlab.com/dev/opensource/ |
| DejaVu Sans / DejaVu Sans Bold (Debian `fonts-dejavu-core`, system fonts of the worker image for the setlist PDFs; feature 014) | distro | Bitstream Vera font licence (permissive) + public-domain changes by the DejaVu authors | https://dejavu-fonts.github.io/License.html |

| onnxruntime (CPU) | 1.32.0 | MIT | https://github.com/microsoft/onnxruntime |
| rotary-embedding-torch | 0.9.2 | MIT | https://github.com/lucidrains/rotary-embedding-torch |
| flatbuffers / protobuf (via onnxruntime) | 25.12.19 / 7.36.2 | Apache-2.0 / BSD-3-Clause | https://github.com/google/flatbuffers, https://github.com/protocolbuffers/protobuf |
| Vendored inference code: ZFTurbo Music-Source-Separation-Training (commit 1f700c38; MDX23C, BS-RoFormer) | - | MIT (LICENSE + NOTICE in `worker/src/stemstudio_worker/detail/vendor/`) | https://github.com/ZFTurbo/Music-Source-Separation-Training |

### Detail separation model weights (feature 002, ADR-009 model weights exception)

Downloaded at first use into `STEMSTUDIO_MODEL_DIR/detail/<id>/` (Docker: the `model-cache`
volume), verified by SHA-256, never committed or baked into an image.

| Id | File | Bytes | SHA-256 | Source | Licence status |
|---|---|---|---|---|---|
| `mdx23c-drumsep-6s` | MDX23C-DrumSep-aufr33-jarredou.ckpt | 437,652,699 | `d2a4aa53eb584d21eead358a4e66d1882ad182911be018f052b5da73be9096d0` | https://github.com/nomadkaraoke/python-audio-separator/releases/download/model-configs/MDX23C-DrumSep-aufr33-jarredou.ckpt | No licence stated upstream (original release removed); a mirror of the related 5-stem model states CC BY-NC-SA 4.0. Used under the ADR-009 exception. |
| `uvr-mdxnet-kara-2` | UVR_MDXNET_KARA_2.onnx | 52,786,726 | `bf32e15105a09c0f7dddd2b67346146334d6f3ecb399ed7638eba2ab07cbf5f4` | https://github.com/TRvlvr/model_repo/releases/download/all_public_uvr_models/UVR_MDXNET_KARA_2.onnx | MIT per the UVR README statement. Credit: **Ultimate Vocal Remover (UVR) and its developers**. |
| `mvsep-mega-53` | mvsep_mega_model_bs_roformer_53_stems_v1.ckpt | 1,368,919,887 | `c62820893bbf86d4e734f966bd142d9157cfc8bb8e79e9d8f9ea553f3ff3519f` | https://github.com/ZFTurbo/Music-Source-Separation-Training/releases/download/v1.0.21/mvsep_mega_model_bs_roformer_53_stems_v1.ckpt | No licence stated (repository MIT, training data undisclosed). Used under the ADR-009 exception. |

**Not covered:** commercial use of StemStudio, and redistributing images or volumes that contain
these weights or the Demucs weights. They are for local, personal, non-commercial use only
(constitution 1.1.0, principle IV).

### Lyrics and sheet-music model weights (features 006 and 008)

Downloaded at first use into `STEMSTUDIO_MODEL_DIR` (Docker: the `model-cache` volume), verified
by SHA-256, never committed or baked into an image. All three have a stated, open licence.

| Model | File | Bytes | SHA-256 | Source | Licence |
|---|---|---|---|---|---|
| Whisper large-v3-turbo (lyrics alignment, 006) | large-v3-turbo.pt | 1,617,941,637 | `aff26ae408abcba5fbf8813c21e62b0941638c5f6eebfb145be0c9839262a19a` | https://openaipublic.azureedge.net/main/whisper/models/aff26ae408abcba5fbf8813c21e62b0941638c5f6eebfb145be0c9839262a19a/large-v3-turbo.pt | MIT (OpenAI) |
| basic-pitch ICASSP 2022 (bass, vocal and guitar notes, 008) | nmp.onnx | 230,444 | `2c3c1d144bfa61ad236e92e169c13535c880469a12a047d4e73451f2c059a0ec` | https://raw.githubusercontent.com/spotify/basic-pitch/v0.4.0/basic_pitch/saved_models/icassp_2022/nmp.onnx | Apache-2.0 (Spotify) |
| High-resolution piano transcription CRNN (note + pedal, 008) | CRNN_note_F1=0.9677_pedal_F1=0.9186.pth | 171,966,578 | `c3fa9730725bf4a762f1c14bc80cd5986eacda01b026f5a4a2525cd607876141` | https://zenodo.org/records/4034264 | **CC BY 4.0**. Attribution: Q. Kong, B. Li, X. Song, Y. Wan, Y. Wang, "High-resolution Piano Transcription with Pedals by Regressing Onset and Offset Times", Zenodo record 4034264 |

### Chord recognition: vendored BTC code and its weights (feature 016, ADR-031)

**Vendored code** (`worker/src/stemstudio_worker/harmony/vendor/btc/`, with `LICENSE` and `NOTICE`): a subset of
BTC, "A Bi-directional Transformer for Musical Chord Recognition" (J. Park, K. Choi, S. Jeon, D. Kim, J. Park,
ISMIR 2019), https://github.com/jayg996/BTC-ISMIR19 at commit `2682317b` (`2682317be668032e6e4b269ded36adaa2ad57df0`),
**MIT**, Copyright (c) 2019 Jonggwon Park. Only `btc_model.py`, `transformer_modules.py` and the label tables are
vendored, with small changes listed in `NOTICE`; the training and evaluation code (and with it `mir_eval`,
`pretty_midi`, `pandas` and the GPL `pyrubberband`) is not. The folder is excluded from the coverage and SIG gates as
third-party code.

**Weights**: downloaded at first use into `STEMSTUDIO_MODEL_DIR` (Docker: the `model-cache` volume), verified by
SHA-256, loaded with `torch.load(weights_only=True)` and an allow-list of three globals; never committed or baked into
an image.

| Model | File | Bytes | SHA-256 | Source | Licence |
|---|---|---|---|---|---|
| `btc-majmin` (major/minor chords) | btc_model.pt | 12,154,754 | `71c2c5db17e8c43b8a9a9da5db36ef2d667158c07a214eba16344c154c00bf54` | https://raw.githubusercontent.com/jayg996/BTC-ISMIR19/2682317be668032e6e4b269ded36adaa2ad57df0/test/btc_model.pt | MIT (same repository) |
| `btc-large-voca` (170 chord classes) | btc_model_large_voca.pt | 12,229,576 | `1673d23f8f9a55ae7f9e8b80a51da616debb22675b8d8b67ea6ce0ef37b0ab51` | https://raw.githubusercontent.com/jayg996/BTC-ISMIR19/2682317be668032e6e4b269ded36adaa2ad57df0/test/btc_model_large_voca.pt | MIT (same repository) |

Feature 016 adds **no new package** in any component: the chroma-HMM fallback, the key estimate, tablature, parts,
drum MIDI and singing pitch (librosa pYIN in the worker, a YIN core in the browser) use dependencies that were already
listed above.

### System dependencies: GPU image variant only (constitution 1.2.0, ADR-011)

Present only in the optional GPU worker image (`docker-compose.gpu.yml`, build args
`TORCH_VARIANT=cu132`, `ORT_VARIANT=gpu`). The default CPU image and CI contain none of them; the
CI job `cpu-image-guard` (`scripts/cpu_image_guard.py`) fails if they appear there.

| Component | Version | Licence | Notes |
|---|---|---|---|
| PyTorch CUDA build | 2.15.0+cu132 | BSD-3-Clause (code) | pulls in the NVIDIA runtime below |
| NVIDIA CUDA runtime wheels (`nvidia-*`: cuBLAS, cuDNN, cuFFT, cuSPARSE, NCCL, NVRTC, ...; `cuda-bindings`, `cuda-pathfinder`) | as locked by torch 2.15.0+cu132 | **NVIDIA proprietary (free)**, NVIDIA Software License Agreement / CUDA EULA | system dependency like the GPU driver |
| `cuda-toolkit[curand]` (cuRAND for onnxruntime-gpu) | 13.2.2 | **NVIDIA proprietary (free)**, CUDA EULA | system dependency |
| onnxruntime-gpu | 1.32.0 | MIT | replaces `onnxruntime` in the GPU image |
| triton | as locked by torch | MIT | |

### Copyleft transitive dependencies (reviewed and accepted)

All are OSI-approved. They are used unmodified, as separate libraries or executables, in a tool
that runs locally for a single user; StemStudio is not redistributed as a closed binary.

| Component | Licence | Pulled in by |
|---|---|---|
| lameenc 1.9.0 | LGPL-3.0+ | **direct** since feature 007 (MP3 export, used unmodified through its Python API as a separate library; no GPL library is imported by StemStudio code); also demucs |
| soxr 1.1.0 | LGPL-2.1+ | librosa; direct dependency since 006/008 (resampling for alignment and transcription) |
| mutagen 1.48.1 | GPL-2.0+ | spotdl |
| unidecode 1.4.0 | GPL-2.0+ | spotdl |
| text-unidecode 1.3 | Artistic-1.0 / GPL | spotdl |
| pykakasi 2.3.0 | GPL-3.0+ | spotdl |
| spotapi 1.2.8 | GPL-3.0 | spotdl (unofficial Spotify web client, metadata only) |
| certifi, pathspec | MPL-2.0 | requests stack / lizard; certifi also in the singer image |
| frozendict 2.5.1 | LGPL-3.0-only | ACE-Step 1.5 (generator image only, used unmodified as a separate library) |
| soxr 1.1.0 (singer image) | LGPL-2.1+ | librosa in the singer image |
| WeCantSpell.Hunspell 7.0.1 | MPL-1.1 (file-level copyleft) | **direct** since feature 023 (lyric spelling check in the API; used unmodified through its NuGet package, chosen from its MPL-1.1 / GPL-2.0 / LGPL-2.1 tri-licence) |

**Distribution note:** anyone who redistributes the worker image must comply with the GPL/LGPL
terms of these components (offer source, keep notices).

## Runtime: Python generator and singer (features 009 and 010, optional, GPU only)

The base install (`generator/pyproject.toml` dependencies; the `fake` image, CI and E2E) uses only
OSI licences (fastapi, uvicorn, python-multipart, pydantic, soundfile, numpy: MIT/BSD;
nvidia-ml-py: BSD-3-Clause); CI writes the full report (`licences` job). The GPU images are built
only on the owner's machine; `scripts/image-licences.sh` lists every distribution in them
(generator 125, singer 82 distributions, 2026-11-15).

| Component | Version / pin | Licence | Notes |
|---|---|---|---|
| ACE-Step 1.5 (code, `generator` image) | git `ca1e85fe9430179831e6bc6be790c332190a3866` | MIT | https://github.com/ace-step/ACE-Step-1.5; vendored nano-vllm: MIT |
| SoulX-Singer (code, `singer` image) | git `81aeb3ae772c70093c3de74dc23c92d983801ae4` | Apache-2.0 | https://github.com/Soul-AILab/SoulX-Singer; fetched at build time, not copied into this repository |
| torch / torchvision / torchaudio | 2.10.0 (+cu128) | BSD-3-Clause | |
| transformers, tokenizers, diffusers, accelerate, safetensors, huggingface-hub, peft | as pinned per image | Apache-2.0 | singer: transformers 4.41.2 / tokenizers 0.19.1 |
| torchao | 0.16.0 | **BSD-3-Clause** (upstream `LICENSE` of pytorch/ao; the wheel carries no licence metadata) | verified by the CTO in the 009/010 closeout |
| pytorch-wavelets | 1.3.0 | **MIT** upstream (https://github.com/fbcotter/pytorch_wavelets; PyPI classifier says "Free To Use") | ACE-Step dependency |
| frozendict | 2.5.1 | **LGPL-3.0-only** | ACE-Step dependency; see copyleft table |
| vector-quantize-pytorch, lycoris-lora, typer-slim, numba, scipy, matplotlib, tensorboard, pywavelets, lightning, modelscope, librosa, omegaconf, tqdm | as pinned | MIT / BSD / Apache-2.0 | |
| NVIDIA CUDA 12.8 runtime wheels, `cuda-bindings` 12.9.4 | as locked by torch 2.10.0+cu128 | **NVIDIA proprietary (free)** | system dependency per ADR-011 / constitution 1.3.0, GPU images only |

### Generative model weights (features 009 and 010)

Downloaded once into the `model-cache` volume (`/models/acestep`, `/models/soulx`) by the
pre-seed step, verified against the `SHA256SUMS` file next to them, never committed or baked into
an image. All have a stated, open licence; none forbids the use. Generated audio is tagged
`AI_GENERATED=true` (and `AI_VOICE`), and the SoulX README's "do not impersonate" disclaimer is
enforced by the consent model of 010 (ADR-019).

| Model | Revision | Main file and SHA-256 | Source | Licence |
|---|---|---|---|---|
| ACE-Step 1.5 bundle: DiT `acestep-v15-turbo` | `19671f406d603126926c1b7e2adc169acbcade22` | `acestep-v15-turbo/model.safetensors` `3f6e0797fad420a39bd33979eb6e840e30989e34a3794e843d23b60ec6e422d7` | https://huggingface.co/ACE-Step/Ace-Step1.5 | MIT |
| ACE-Step 1.5 bundle: lyrics LM `acestep-5Hz-lm-1.7B` (010) | same | `acestep-5Hz-lm-1.7B/model.safetensors` `f161689da73e5ecefa28ff780d51c2d92a00f056d021d7933c779ed5c6cd7db8` | same | MIT |
| ACE-Step 1.5 bundle: text encoder `Qwen3-Embedding-0.6B` | same | `Qwen3-Embedding-0.6B/model.safetensors` `0437e45c94563b09e13cb7a64478fc406947a93cb34a7e05870fc8dcd48e23fd` | same (upstream https://huggingface.co/Qwen/Qwen3-Embedding-0.6B) | MIT in the bundle; Apache-2.0 upstream |
| ACE-Step 1.5 bundle: VAE | same | `vae/diffusion_pytorch_model.safetensors` `da17edb604c40deaf09e9b24974e590d1ca83a374070e5d0884cfa4bed9a99b0` | same | MIT |
| SoulX-Singer SVC | `40493ad90286056c7a9095035164434a79daa8c9` | `model-svc.pt` `38e72d9e15be6eef3cc937daaf81421a7b5b546816786530bb9aa346cee5d43c` | https://huggingface.co/Soul-AILab/SoulX-Singer | Apache-2.0 |
| RMVPE pitch extractor (SoulX preprocess) | `83dc50289d22a81b1e9998f5b9e111aef7c1fdcd` | `rmvpe/rmvpe.pt` `6d62215f4306e3ca278246188607209f09af3dc77ed4232efdd069798c4ec193` | https://huggingface.co/Soul-AILab/SoulX-Singer-Preprocess | Apache-2.0 |
| Whisper base (SoulX content encoder) | `e37978b90ca9030d5170a5c07aadb050351a65bb` | `whisper-base/model.safetensors` `07cadb9f25677c8d50df603e66a98fbd842cce45047139baeb16e6219a1e807b` | https://huggingface.co/openai/whisper-base | Apache-2.0 |

The full per-file checksums are the `SHA256SUMS` files in the volume (28 files for ACE-Step,
2 + 11 for SoulX and Whisper base). VoiceStudio (AGPL-3.0, CC BY-NC weights) was reviewed and is
**not used** (010 research S-02).

## Online data services (feature 005, FR-014)

The worker queries these services only when online lookups are on (`STEMSTUDIO_ONLINE_LOOKUPS`,
default `on`) and sends only the song title, artist, duration and ISRC (MusicBrainz) or the ids
found there (LRCLIB, feature 006: title, artist, album and the rounded duration); the User-Agent names StemStudio and, when set, `STEMSTUDIO_CONTACT_URL` (never an
e-mail address). Nothing they return is bundled with StemStudio, and pictures and band texts are
never written into exports.

| Service | Data used | Licence / terms | How StemStudio complies |
|---|---|---|---|
| MusicBrainz WS/2 (https://musicbrainz.org/doc/MusicBrainz_API) | core data: recordings, releases, release groups, artists, URL relations | **CC0 1.0** (core data; supplementary data such as tags and annotations is not used) | 1 request/s, descriptive User-Agent, stored with the track |
| Cover Art Archive (https://coverartarchive.org) | front cover of a release group | **No licence stated**; images belong to their rights holders | images are shown **in the app only** (cover, thumbnails), never exported or redistributed; personal-practice notice |
| Wikidata (https://www.wikidata.org) | genres, members, founding year, country, website, Wikipedia site links | **CC0 1.0** (structured data) | ids and facts only |
| Wikipedia REST `page/summary` (https://en.wikipedia.org/api/rest_v1/) | the summary excerpt of the artist article (`STEMSTUDIO_WIKIPEDIA_LANGUAGES`) | **CC BY-SA 4.0** (text) | excerpt shown unmodified with **in-app attribution** (article link, "Wikipedia contributors" history link, licence link, "Excerpt, unmodified"; `band-info-attribution`); never exported |
| LRCLIB (https://lrclib.net, feature 006) | synced or plain lyrics of the recording (`/api/get`, `/api/search`) | **No data licence stated**; lyrics are copyrighted by their rights holders | lyrics are shown **in the app only** (karaoke banner, lyrics card), never exported, logged or bundled; at most 3 requests per lookup, >= 500 ms apart; personal-practice notice |

## Local AI model host and weights (feature 011, optional, installed by the user)

Not part of StemStudio, its images or this repository. The agent panel works without them and
says why it is off.

| Component | Version / tag | Licence | Notes |
|---|---|---|---|
| Ollama (host prerequisite; runs on the host, not in compose) | reference machine 0.40.16 | MIT | https://github.com/ollama/ollama; reached at `STEMSTUDIO_OLLAMA_URL` (default `http://host.docker.internal:11434`) |
| Qwen3.5 9B weights (`qwen3.5:9b`, default agent model, ~6.6 GB, Q4_K_M) | Ollama library tag | Apache-2.0 | pulled by the user (`ollama pull qwen3.5:9b`); https://ollama.com/library/qwen3.5 |
| Qwen3.5 4B weights (`qwen3.5:4b`, local fallback while a GPU job runs, ~3.3 GB, Q4_K_M) | Ollama library tag | Apache-2.0 | pulled by the user (`ollama pull qwen3.5:4b`) |

## Optional external services (feature 011, constitution 1.3.0)

| Service | When it is called | Data sent | Terms |
|---|---|---|---|
| Anthropic Claude API (https://api.anthropic.com), default model `claude-opus-5-5`, configurable `claude-sonnet-5-5` | only when the user selects Claude in Settings, acknowledges the "paid per use" notice, and `ANTHROPIC_API_KEY` is set in the API's environment (shell only, never in a file); 0 requests otherwise (SC-006, tested with a failing HTTP handler) | text only: the system prompt, the tool definitions, the user's messages of the bounded context and the tool results (ids, names, statuses, numbers, short descriptions). Never audio, files, the API key, online lyrics (006) or full biographies (005) | paid per use under the user's own Anthropic account and its commercial terms (https://www.anthropic.com/legal); per-run token cap (default 60,000 input + 8,000 output tokens) |

## Frontend audio engine (build-time only; the shipped bundle contains only StemStudio code)

| Component | Version | Licence |
|---|---|---|
| TypeScript | 6.0.3 | Apache-2.0 |
| Vite | 8.3.8 | MIT |
| Vitest + @vitest/coverage-v8 | 5.1.0 | MIT |
| ESLint, @eslint/js | 10.15.0 / 10.0.1 | MIT |
| typescript-eslint | 8.72.2 | MIT |
| eslint-plugin-jsdoc | 65.4.2 | BSD-3-Clause |
| jsdom | 31.0.1 | MIT |
| globals, @types/node | 17.14.0 / 22.20.5 | MIT |

## Test and quality tooling (not shipped)

| Component | Version | Licence |
|---|---|---|
| xunit.v3, xunit.runner.visualstudio | 4.0.3 / 4.0.1 | Apache-2.0 |
| Microsoft.NET.Test.Sdk | 18.12.0 | MIT |
| coverlet.collector / coverlet.msbuild | 10.1.0 | MIT |
| bUnit | 2.15.2 | MIT |
| Microsoft.AspNetCore.Mvc.Testing, TimeProvider.Testing | 10.0.15 / 10.11.0 | MIT |
| Microsoft.Extensions.DependencyInjection (real container in the `StemStudio.Agent` DI tests, feature 011; test-only) | 10.0.15 | MIT |
| NSubstitute (+ Analyzers) | 6.2.0 / 1.0.17 | BSD-3-Clause / MIT |
| Shouldly | 4.3.0 | BSD-3-Clause |
| YamlDotNet | 18.1.0 | MIT |
| dotnet-ef (local tool) | 10.0.15 | MIT |
| pytest, pytest-cov, pytest-timeout | 9.2.0 / 7.1.0 / 2.4.0 | MIT |
| httpx, jsonschema, PyYAML | 0.28.1 / 4.26.0 / 6.0.3 | BSD-3-Clause / MIT / MIT |
| ruff, mypy (+ types-* stubs) | 0.17.5 / 2.4.1 | MIT (stubs Apache-2.0) |
| lizard | pinned in tools/quality | MIT |
| jscpd (via npx) | pinned in tools/quality | MIT |
| @playwright/test (+ browsers) | 1.65.1 | Apache-2.0 |
| @axe-core/playwright, axe-core (accessibility tests) | 4.14.0 | MPL-2.0 |
| @lhci/cli (Lighthouse CI, pulls lighthouse 12.6.1) | 0.15.1 | Apache-2.0 |
| Playwright container image `mcr.microsoft.com/playwright:v1.65.1-noble` (CI visual/a11y only) | 1.65.1 | Apache-2.0 |
| eslint-plugin-playwright | 2.12.2 | MIT |
| @redocly/cli (via npx) | 2.78.0 | MIT |
| Emscripten SDK Docker image `emscripten/emsdk` (build-time only, builds `soundtouch.wasm`; pinned by digest in `soundtouch.lock`; not distributed, https://hub.docker.com/r/emscripten/emsdk) | 6.0.13 | MIT / University of Illinois NCSA |
| actions/download-artifact (CI only, feature 012 served-module check, https://github.com/actions/download-artifact) | 8.0.2 | MIT |
| PyYAML (CI / `scripts/test-all.*` only, ephemeral `uv --with` for the 012 contract lint, https://github.com/yaml/pyyaml) | 6.0.3 | MIT |

### Documentation build (`tools/docs`, not shipped; builds docs/public PDFs)

Pinned exactly in `tools/docs/package.json`; the lock file was resolved with
`npm install --before=2026-11-01` so every package is at least two weeks old.

| Component | Version | Licence | URL |
|---|---|---|---|
| markdown-it | 15.0.2 | MIT | https://github.com/markdown-it/markdown-it |
| argparse (via markdown-it) | 3.0.4 | PSF-2.0 | https://github.com/nodeca/argparse |
| entities (via markdown-it) | 8.1.0 | BSD-2-Clause | https://github.com/fb55/entities |
| linkify-it, mdurl, punycode.js, uc.micro (via markdown-it) | 6.1.0 / 2.1.0 / 2.3.1 / 3.0.0 | MIT | https://github.com/markdown-it |
| pdf-lib (puts the cover in front of the body) | 1.17.1 | MIT | https://github.com/Hopding/pdf-lib |
| @pdf-lib/standard-fonts, @pdf-lib/upng | 1.0.0 / 1.0.1 | MIT | https://github.com/Hopding/pdf-lib |
| pako (via pdf-lib) | 1.0.11 | MIT AND Zlib | https://github.com/nodeca/pako |
| tslib (via pdf-lib) | 1.14.1 | 0BSD | https://github.com/microsoft/tslib |
| playwright-core (+ Chromium headless shell, print to PDF) | 1.64.1 | Apache-2.0 | https://github.com/microsoft/playwright |
| Archivo font (embedded in the PDFs, from the web app) | variable, Latin subset | OFL-1.1 | https://github.com/Omnibus-Type/Archivo |

The website generator (`tools/site`, not shipped with the app; builds `site/dist` for GitHub Pages)
adds **no dependencies**: it loads the markdown-it above from `tools/docs/node_modules`, and its
local review script (`tools/site/review.mjs`) reuses the e2e suite's `@playwright/test` 1.65.1 and
`@axe-core/playwright` 4.14.0 (listed under test tooling). The published site contains only
StemStudio files plus the self-hosted Archivo font (OFL-1.1, licence text published next to it as
`assets/archivo-OFL.txt`). Deployment uses GitHub's own actions (`.github/workflows/pages.yml`):

| Component | Version | Licence | URL |
|---|---|---|---|
| actions/configure-pages | 6.0.0 | MIT | https://github.com/actions/configure-pages |
| actions/upload-pages-artifact | 5.0.0 | MIT | https://github.com/actions/upload-pages-artifact |
| actions/deploy-pages | 5.0.1 | MIT | https://github.com/actions/deploy-pages |

## Open licence issue: Demucs weights

The Demucs code is MIT, but the pretrained weights have no clear licence and `htdemucs_6s` is
likely CC BY-NC (author's statement above). This was recorded incorrectly as MIT-covered until
2026-11-15. Non-commercial, local, single-user use is the intended use of StemStudio. Owner decision
2026-11-15: documented exception for model weights (constitution 1.1.0 principle IV, ADR-009).

## Rejected for licence reasons

| Component | Reason |
|---|---|
| FluentAssertions 8.x | Commercial licence (Xceed) since v8 |
| JsonSchema.Net 9.x | OSMF EULA, not OSI-approved |

## Container base images

| Image | Licence notes |
|---|---|
| `python:3.12-slim` (Debian) | Debian packages under their respective free licences |
| `mcr.microsoft.com/dotnet/aspnet:10.0` / `sdk:10.0` | MIT (.NET), Debian packages |
