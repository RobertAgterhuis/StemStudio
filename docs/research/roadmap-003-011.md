# Research: roadmap features 003-011 (design, GPU, metadata, lyrics, export, notation, generative, agent)

Status: research only, no code changed. Date: 2026-11-15. Scope: `docs/ROADMAP.md` items 003-011,
checked against constitution 1.1.0 principle IV (code and libraries OSI-only; model weights may be
non-commercial or unclear under ADR-009, but weights whose licence forbids this use stay excluded).

How claims were checked: licences were read from the LICENSE file, model card metadata (Hugging Face
API `cardData.license`), PyPI/NuGet/npm metadata or the project README. Terms and rate limits come
from the provider's own documentation page. Every row gives its source. **"Not verified"** means
exactly that: the primary source could not be read or does not state it. Machine facts were measured
read-only on the reference machine (`nvidia-smi`, `docker version`, `ollama /api/version`).

## 0. TL;DR

| # | Recommendation | Main risk / open item |
|---|---|---|
| 003 | Custom theme = MIT Radzen SCSS (`Radzen.Blazor/themes/*.scss`) compiled with our own `$rz-*` values, or a thin `--rz-*` CSS-variable override on a free base theme (`software-dark` / `material-dark`). Fonts Inter (OFL-1.1) + JetBrains Mono (OFL-1.1), icons Material Symbols (Apache-2.0, already bundled by Radzen). Guided tours with **driver.js 1.14.0 (MIT)**. Help content as Markdown files rendered with **Markdig (BSD-2-Clause)**. | Premium Radzen themes (`material3*`, `fluent*`, `zen*`), swatches and the Radzen Blazor Studio designer are paid: excluded. **Shepherd.js 16 and Intro.js are AGPL-3.0 + commercial**: excluded. |
| 004 | **torch 2.15.0 has only `cu132` (stable) and `cu134` (experimental) CUDA wheels.** Use `cu132`. Both include SASS for Blackwell sm_120. Replace `onnxruntime` with `onnxruntime-gpu==1.32.0` (MIT, CUDA 13) in the GPU image only. | **Bug in the current repo:** `docker-compose.gpu.yml` defaults `TORCH_VARIANT` to `cu128`, and there is no `torch==2.15.0` on the cu128 index, so the GPU build fails. The GPU image grows about 2.5-3.5 GB (estimate). |
| 005 | Cover art: YouTube `info["thumbnails"]` / `--write-thumbnail`; Spotify `Song.cover_url` (spotDL); uploads `tinytag` 2.3.2 (MIT, `images.front_cover`), with mutagen (GPL, already present) as fallback; Cover Art Archive `release-group/{mbid}/front-500` as the general fallback. Band info: MusicBrainz (core data CC0) + Wikidata (CC0) + Wikipedia REST summary (CC BY-SA 4.0, show attribution). | MusicBrainz and Wikimedia **require contact info in the User-Agent**: use the public project URL, never a personal e-mail. TheAudioDB (free key "123", 30 req/min, images "mostly fan created", no licence) and Last.fm (non-commercial only, **API images excluded**, 100 MB cache cap) are optional or skipped. |
| 006 | **LRCLIB** `GET /api/get?track_name&artist_name&album_name&duration` (±2 s match), no key. It now has **word-level timing** (`hasWordSync`, `lyricsfile` YAML `lines[].words`) for some records; otherwise interpolate per word. Fallback: forced alignment on the Demucs vocal stem with **stable-ts 2.19.1 (MIT) + Whisper large-v3-turbo (MIT weights)**. | LRCLIB states **no data licence**. Lyrics are copyrighted: show them only in-app for personal practice, cache locally, never redistribute. stable-ts needs `torchaudio` and `openai-whisper<=20250625`: run it in a separate venv or container. |
| 007 | WAV (soundfile), FLAC (soundfile), MP3 (lameenc 1.9.0, LGPL, already present). Render **server-side in the worker with numpy** from the saved mix model. | Must match the engine's gain/pan law exactly. Add a golden test against an OfflineAudioContext render. |
| 008 | Per stem: piano -> ByteDance piano_transcription_inference (MIT code, **CC BY 4.0** weights); vocals/bass/guitar/other -> Spotify basic-pitch ONNX (Apache-2.0 code + weights) via our onnxruntime; drums -> YourMT3+ (GPL-3.0 code, Apache-2.0 weights). Then quantise on our beat grid -> music21 10.5.2 (BSD-3) MusicXML -> Verovio 6.4.1 (LGPL-3.0) SVG -> CairoSVG (LGPL-3.0) PDF. Optional "engraved" PDF with LilyPond (GPL-3.0, separate executable). | **ADTOF is CC BY-NC-SA 4.0 for the code itself**, so it fails the OSI-only code rule. basic-pitch's pip package pulls TensorFlow <2.15.1 on Py 3.12, so vendor the ONNX model instead of installing the package. Expect readable lead sheets, not publishable scores. |
| 009/010 | **ACE-Step 1.5 (MIT code and MIT weights)** for both features: cover/style transfer from reference audio, repaint, lyrics-to-song with vocals, 10 s-10 min, runs in under 4 GB VRAM (2B turbo) up to about 12-16 GB (XL with offload). Run it in its **own container** (it pins torch 2.10.0+cu128). | Other families have NC weights (YuE2, MusicGen, MuLaCover), a revenue-gated licence with forbidden uses (Stable Audio Open) or are no longer reachable (Tencent SongBloom / SongGeneration). Section 9.4 covers VoiceStudio: it has **no singing synthesis in code today**. |
| 011 | MEAI `IChatClient` (Microsoft.Extensions.AI 11.0.0, MIT) with **OllamaSharp 5.5.2 (MIT)** and the **official Anthropic C# SDK 13.8.0 (Apache-2.0, implements IChatClient)**. Local default model `qwen3.5:9b` (Apache-2.0, 6.6 GB, tools). Claude default `claude-opus-5-5`, key only in `ANTHROPIC_API_KEY`. Safety with `ApprovalRequiredAIFunction` for destructive tools. | The model needs a constitution amendment (optional paid service). Ollama runs on the host and shares the GPU: needs the GPU job policy in section 12. |

## 1. Machine facts (measured 2026-11-15, read-only)

| Item | Value | Source |
|---|---|---|
| GPU | NVIDIA GeForce RTX 5070 Ti, 16,303 MiB, compute capability **12.0** (sm_120) | `nvidia-smi --query-gpu=...` |
| Driver | **610.88** (WDDM), CUDA UMD 13.3. R610 is the CUDA 13.3 branch, so CUDA 13.2 (torch cu132) is covered. cu134 asks for R615; minor-version compatibility probably covers it, **not verified**. | `nvidia-smi`; [CUDA release notes](https://docs.nvidia.com/cuda/cuda-toolkit-release-notes/index.html) (13.2 = R595, 13.3 = R610, 13.4 = R615) |
| VRAM already in use with the desktop idle | **about 3.2 GB** (3,263 MiB). Plan with about **12.5 GB usable** for StemStudio + Ollama together. | `nvidia-smi` |
| Docker | Engine 29.9.2 (Docker Desktop) | `docker version` |
| Ollama | 0.40.16 on the host; `qwen3.5:9b`, `gemma4:e4b`, `gpt-oss:20b` and `qwen3.8:27b` are already pulled | `GET http://localhost:11434/api/version`, `/api/tags` |

---

## 2. Feature 003: design system and UX foundation

### 2.1 Radzen.Blazor 13.1.0 theming

| Fact | Detail | Source |
|---|---|---|
| Free vs premium themes | Free: `material`, `material-dark`, `standard`, `standard-dark`, `default`, `humanistic`, `humanistic-dark`, `software`, `software-dark` (and `dark`). **Premium (`Premium = true`)**: `material3`, `material3-dark`, `material3expressive`, `material3expressive-dark`, `zen`, `zen-dark`, `fluent`, `fluent-dark`. | [`ThemeService.cs`](https://github.com/radzenhq/radzen-blazor/blob/master/Radzen.Blazor/ThemeService.cs) |
| Licence of the free themes | "These themes are free for commercial use"; the source SCSS is "licensed under MIT". The Radzen.Blazor repo is MIT. | [blazor.radzen.com/themes](https://blazor.radzen.com/themes); [LICENSE](https://github.com/radzenhq/radzen-blazor/blob/master/LICENSE) |
| Premium | "Premium themes can be used with an active Radzen Blazor subscription"; "Swatches are a premium feature of Radzen Blazor Studio." **Excluded** (paid). The Radzen Blazor Studio theme designer is part of the paid product: **excluded**. Whether a free online designer exists was **not verified**; we don't need one. | [blazor.radzen.com/themes](https://blazor.radzen.com/themes) |
| SCSS structure | Each theme is `<name>-base.scss` + `<name>.scss` with `!default` variables (`$rz-primary`, `$rz-secondary`, `$rz-base`, `$rz-series-1..`, ...). `_css-variables.scss` turns the maps into `:root { --rz-<token>: ... }` custom properties. | [themes folder](https://github.com/radzenhq/radzen-blazor/tree/master/Radzen.Blazor/themes) |
| Loading | `RadzenTheme` emits `<link href="{Path}/{Theme}-base.css">` (+ `-wcag.css` when `Wcag=true`). The path is `_content/Radzen.Blazor/css` for embedded themes, otherwise `ThemeService.SetCssPath(...)` or `css/`. The icon font `MaterialSymbolsOutlined.woff2` ships with the package. | [`RadzenTheme.razor(.cs)`](https://github.com/radzenhq/radzen-blazor/blob/master/Radzen.Blazor/RadzenTheme.razor.cs), [`ThemeService.cs`](https://github.com/radzenhq/radzen-blazor/blob/master/Radzen.Blazor/ThemeService.cs) |
| Current StemStudio state | `App.razor` uses `<RadzenTheme Theme="material" ...>`. Components also hand-use `rz-*` classes on raw inputs. | `src/StemStudio.Web/Components/App.razor` |

**Two ways to build "our own" theme:**

| Option | How | Pros / cons |
|---|---|---|
| A. Token override (recommended first) | Keep a free base theme, for example `software-dark` (dark studio look). Add `wwwroot/css/stemstudio-theme.css` with `:root { --rz-primary: ...; --rz-base-background-color: ...; --rz-border-radius: ...; }` plus our own `--ss-*` tokens (meter colours, channel colours, spacing). Load it after `RadzenTheme`. | No build step and no copied SCSS. Easy to unit-test (bUnit checks the link order). Limited to what the variables expose. |
| B. Compiled theme | Copy `software-base.scss` + the partials (MIT, keep the notice) into `src/StemStudio.Web/Styles/`, set our `$rz-*` values, compile with Dart Sass (MIT) at build time into `wwwroot/css/stemstudio-base.css`, then `ThemeService.SetCssPath("css")` + `Theme="stemstudio"`. | Full control (component SCSS too). Adds a Sass build step and drifts from Radzen updates. |

Recommendation: A in 003, B only if A cannot express the design. Offer dark (default for a studio) and
light, and keep `Wcag` toggleable.

### 2.2 Fonts and icons (self-host, no runtime calls to Google)

| Asset | Licence (OSI?) | Source |
|---|---|---|
| Inter (UI text, variable font) | SIL OFL-1.1 (OSI-approved) | [rsms/inter LICENSE.txt](https://github.com/rsms/inter/blob/master/LICENSE.txt); [OSI OFL-1.1](https://opensource.org/license/ofl-1-1) |
| JetBrains Mono (timecodes, BPM, numbers) | OFL-1.1 | [JetBrains/JetBrainsMono](https://github.com/JetBrains/JetBrainsMono) (GitHub licence API) |
| Material Symbols | Apache-2.0; already bundled by Radzen | [google/material-design-icons LICENSE](https://github.com/google/material-design-icons/blob/master/LICENSE) |
| Fontsource packaging (optional) | MIT (each font keeps its own licence) | [fontsource/fontsource](https://github.com/fontsource/fontsource) |

Put the `.woff2` files in `wwwroot/fonts` and record them in THIRD-PARTY-LICENSES (fonts section).

### 2.3 Journey modes: beginner / advanced / expert

| Aspect | Proposal |
|---|---|
| Model | `UserExperienceLevel { Beginner, Advanced, Expert }` stored in a single-user settings row (API), with first-run onboarding that asks the question once. Switchable from the header at any time. |
| What changes per level | **Beginner**: guided import wizard, presets ("practice drums": mute drums, metronome on), hides pan, offsets, detail-model choice and export formats; inline explanations visible. **Advanced**: full mixer, loop, metronome, detail separation, export. **Expert**: everything plus per-model options, raw job logs, keyboard shortcuts overlay, agent tool traces (011). |
| Implementation | A cascading `ExperienceContext` and a `<LevelGate Min="Advanced">` component. Features declare their minimum level in one registry (`FeatureCatalog`), so the help (2.4) and the agent (011) reuse it. Never hide data, only controls. The API stays the same for all levels. |
| Tests | bUnit: each gate per level. Playwright: a beginner path and an expert path. |

### 2.4 In-app help

| Piece | Choice | Licence / source |
|---|---|---|
| Help content | Markdown per feature in `src/StemStudio.Web/Help/<feature>/<topic>.md` with front matter (`id`, `level`, `anchor`), rendered server-side with **Markdig 1.6.0** | BSD-2-Clause ([xoofx/markdig](https://github.com/xoofx/markdig)) |
| Contextual help panel | Radzen `RadzenSidebar`/`RadzenPanel` on the right, opened by a `?` button or F1. It shows the topic for the focused area (`data-help-id`). Searchable index (simple in-memory). | Radzen MIT |
| Tooltips | `RadzenTooltip` with short text; "Learn more" links to the panel topic | Radzen MIT |
| Guided tours | **driver.js 1.14.0** via a small TS interop module (`help-tour.ts`); tour steps defined in JSON next to the Markdown | **MIT** ([npm](https://www.npmjs.com/package/driver.js), [license](https://github.com/kamranahmedse/driver.js/blob/master/license)) |
| Rejected | **Shepherd.js 16.0.0**: dual "AGPL-3.0 (for open source and non-commercial use) and a Commercial License"; the AGPL network clause plus the commercial upsell don't fit. **Intro.js**: AGPL-3.0 + commercial licence for v2+. | [shepherd LICENSE.md](https://github.com/shipshapecode/shepherd/blob/main/LICENSE.md), npm `license: AGPL-3.0`; [intro.js license.md](https://github.com/usablica/intro.js/blob/master/license.md) |

Cross-cutting: each later feature adds help topics and a tour; CI checks that every `data-help-id`
has a topic.

---

## 3. Feature 004: GPU acceleration

### 3.1 PyTorch 2.15 CUDA wheels (Blackwell sm_120)

| Fact | Detail | Source |
|---|---|---|
| Compatibility matrix 2.15 | Stable CUDA **13.2** (cuDNN 9.26.0.51, NCCL 2.30.7); experimental CUDA **13.4**. CUDA 12.x is no longer built for 2.15. | [pytorch RELEASE.md](https://github.com/pytorch/pytorch/blob/main/RELEASE.md#release-compatibility-matrix) |
| Architectures | CUDA 13.2.2 and 13.4.3 builds: Turing 7.5, Ampere 8.0/8.6, Hopper 9.0, **Blackwell 10.0, 12.0**; "Release wheels ship SASS only" | same, "PyTorch CUDA Support Matrix" |
| Indexes that have `torch==2.15.0` cp312 | `https://download.pytorch.org/whl/cu132` and `.../cu134` (Linux x86_64, aarch64, win_amd64), plus `cpu` and `xpu`. **None on cu126/cu128/cu129/cu130.** | Directory listing of `download.pytorch.org/whl/<variant>/torch/` |
| PyPI default | `torch-2.15.0` on PyPI (Linux) is the CUDA build: `cuda-toolkit[cublas,cudart,cufft,...]==13.2.2`, `nvidia-cudnn-cu13==9.26.0.51`, `nvidia-nccl-cu13`, `nvidia-cusparselt-cu13`, `nvidia-nvshmem-cu13`, `triton~=3.9.0` | [PyPI JSON torch 2.15.0](https://pypi.org/pypi/torch/2.15.0/json) |

**Action:** change the default in `docker-compose.gpu.yml` from `cu128` to `cu132`. The Dockerfile's
`--index-url https://download.pytorch.org/whl/${TORCH_VARIANT}` logic is otherwise fine. Add a smoke
test in the GPU path: `torch.cuda.get_arch_list()` contains `sm_120`, and a small matmul runs.

### 3.2 Docker Desktop GPU passthrough (Windows 11, WSL2)

| Requirement | Detail | Source |
|---|---|---|
| Windows + driver | Up-to-date Windows 11, current NVIDIA driver with WSL 2 GPU paravirtualisation (610.88 present) | [Docker Desktop GPU](https://docs.docker.com/desktop/features/gpu/) |
| WSL | `wsl --update` (latest WSL 2 kernel); Docker Desktop WSL 2 backend on | same |
| Linux driver inside WSL | **Do not install one**: "must not install any NVIDIA Linux GPU driver within WSL" | [NVIDIA CUDA on WSL guide](https://docs.nvidia.com/cuda/wsl-user-guide/index.html) |
| NVIDIA Container Toolkit | NVIDIA's table: "Supported with the latest NVIDIA Container Toolkit, using Docker CLI or Docker Desktop". Docker's page lists no manual toolkit install for Docker Desktop. Docker Desktop's WSL VM ships the GPU hook itself. Whether a separate toolkit install is needed was **not verified by test** on this machine. | both pages above |
| Verify | `docker run --rm -it --gpus=all nvcr.io/nvidia/k8s/cuda-sample:nbody nbody -gpu -benchmark` | [Docker Desktop GPU](https://docs.docker.com/desktop/features/gpu/) |
| Compose syntax | `deploy.resources.reservations.devices: [{driver: nvidia, count: 1, capabilities: [gpu]}]` (`capabilities` is required). Already used in `docker-compose.gpu.yml`. The short `gpus: all` attribute is **not documented** on that page (not verified), so keep the long form. | [Compose GPU support](https://docs.docker.com/compose/how-tos/gpu-support/) |

### 3.3 onnxruntime-gpu (detail models, basic-pitch ONNX)

| Fact | Detail | Source |
|---|---|---|
| Version / licence | `onnxruntime-gpu` **1.32.0**, MIT. Extras: `[cuda]` -> `nvidia-cuda-runtime~=13.0`, `nvidia-cufft~=12.0`, ...; `[cudnn]` -> `nvidia-cudnn-cu13~=9.0` | [PyPI onnxruntime-gpu](https://pypi.org/project/onnxruntime-gpu/) (JSON metadata) |
| CUDA build | From 1.27 the PyPI GPU packages "are built with CUDA 13.0 by default"; "compatible with PyTorch for CUDA 13.x". Works with torch's CUDA/cuDNN libs when "built against the same major version of CUDA and cuDNN": import torch first or call `onnxruntime.preload_dlls()`. | [ORT CUDA EP requirements](https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html) (the table on that page lists up to 1.30; 1.32 metadata confirms CUDA 13) |
| Packaging | `onnxruntime` and `onnxruntime-gpu` both install the `onnxruntime` module. Install **one or the other** (GPU image: uninstall CPU ORT, install GPU ORT without extras so it reuses torch's cu13 libs). The ORT page doesn't cover this conflict; it is the known packaging behaviour, **not verified here**. | - |
| Wheel size | 239 MB (manylinux x86_64) vs about 17 MB for CPU ORT | PyPI JSON |

### 3.4 Image size and CPU fallback

| Item | Estimate |
|---|---|
| torch cu132 wheel | 539 MB (vs CPU 210 MB) |
| CUDA runtime deps (cuDNN 495 MB, NCCL 205 MB, cuSPARSELt 162 MB, NVSHMEM 129 MB, cuda-toolkit libs (cuBLAS etc.), triton) | about 1.5-2 GB more compressed, about 4-6 GB installed. **Estimate**; measure with `docker image ls`. |
| onnxruntime-gpu | +239 MB |
| Net | GPU worker image roughly **+3 GB compressed / +5-7 GB on disk** compared with CPU |

CPU fallback: keep `TORCH_VARIANT=cpu` as the default build (no GPU needed for CI/E2E). At runtime
`STEMSTUDIO_DEVICE=auto` (already in `config.py`) picks CUDA when `torch.cuda.is_available()`,
otherwise CPU. Log the choice and expose it on `/health` (`device`, `gpu_name`, `vram_total`). On CUDA
OOM, retry the job once on CPU (for separation and transcription) and show the fallback in the UI.
Generative jobs (009/010) are **GPU-only**: on CPU they are disabled with a clear message.

---

## 4. Feature 005: artwork and band info

### 4.1 Cover art per import source

| Source | How | Licence of the tool | Note |
|---|---|---|---|
| YouTube (yt-dlp) | `info_dict["thumbnails"]` (list of URLs/sizes) or `--write-thumbnail --convert-thumbnails jpg`; pick the largest square-ish one or crop 16:9 to square | yt-dlp Unlicense ([README](https://github.com/yt-dlp/yt-dlp#readme)) | Video thumbnails, not album art. Prefer Cover Art Archive when MusicBrainz matches. |
| Spotify (spotDL) | `Song.cover_url: Optional[str]` (album image from the Spotify album metadata) | spotDL MIT ([song.py](https://github.com/spotDL/spotify-downloader/blob/master/spotdl/types/song.py)) | Metadata only, no DRM touched (constitution IV) |
| Upload (MP3/FLAC/M4A/OGG) | **tinytag 2.3.2**: `TinyTag.get(path, image=True).images.front_cover.data` / `.mime_type` | **MIT** (PyPI classifier "OSI Approved :: MIT License"; [tinytag](https://github.com/tinytag/tinytag)) | No new copyleft. mutagen (GPL-2.0+, already present via spotDL) is the fallback. `mediafile` (MIT) depends on mutagen, so it gains nothing. |
| Fallback for all | Cover Art Archive `https://coverartarchive.org/release-group/{mbid}/front-500` (307 redirect to archive.org; also `-250`, `-1200`) | - | Tested: 307 to an archive.org thumbnail |

Store one `cover.jpg` (500 px) + `cover_thumb.jpg` (128 px) per track in local storage. Serve it via
the API with caching headers. Cover images are copyrighted by their owners: show them in-app only.

### 4.2 Open data APIs

| API | Licence / terms | Rate limit | Identification | Source |
|---|---|---|---|---|
| **MusicBrainz WS/2** (`/ws/2/recording?query=...&fmt=json`, artist lookup with `url-rels`) | Core data **CC0**; supplementary data CC BY-NC-SA 3.0 | about **1 req/s per IP**; 503 on excess | **Required** UA: `App/<version> ( contact-url )` or `( contact-email )`; anonymous UAs are throttled | [Data licence](https://musicbrainz.org/doc/About/Data_License), [Rate limiting](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting) |
| **Cover Art Archive** | No image licence stated: "Use the images at your own risk", "Be respectful of the rights of the artists and labels" | API page: "no rate limiting rules in place" (but also lists 503s) | Same UA | [CAA](https://musicbrainz.org/doc/Cover_Art_Archive), [CAA API](https://musicbrainz.org/doc/Cover_Art_Archive/API) |
| **Wikidata** (entity via MusicBrainz artist ID P434 / sitelink) | Structured data **CC0** | Wikimedia limits below | Wikimedia UA policy | [Wikidata:Licensing](https://www.wikidata.org/wiki/Wikidata:Licensing) |
| **Wikipedia REST** `https://{lang}.wikipedia.org/api/rest_v1/page/summary/{title}` | Text **CC BY-SA 4.0**. Attribution "includes the title, author, source, and license"; state the licence, link to it, and mark changes | Unauthenticated bot with a compliant UA: **200 req/min**; unidentified: 10/min | UA `<client>/<version> (<contact>) <library>/<version>`; contact = URL or e-mail | [Content reuse](https://www.mediawiki.org/wiki/Wikimedia_APIs/Content_reuse), [Access policy](https://www.mediawiki.org/wiki/Wikimedia_APIs/Access_policy), [Rate limits](https://www.mediawiki.org/wiki/Wikimedia_APIs/Rate_limits). Endpoint returned 200 on 2026-11-15 (no deprecation header). |
| TheAudioDB | Free key `123` (page also mentions "2"), **30 req/min**, v2 premium only; images "Most are fan created"; **no data/image licence stated**; terms page not read | 30/min | key in URL | [free_music_api](https://www.theaudiodb.com/free_music_api) |
| Last.fm | Free licence **only for non-commercial use** (cl. 3.1); "powered by AudioScrobbler" attribution; **images/artwork excluded** (cl. 5.1.8); 100 MB storage cap; API key + account required | not stated numerically | key | [Last.fm API ToS](https://www.last.fm/api/tos) |

**User-Agent without personal data:** use
`StemStudio/<version> ( https://github.com/<org>/StemStudio )`, so the contact is the public project
URL, which both MusicBrainz and Wikimedia accept. Never send a personal e-mail. Make the contact URL
configurable (`StemStudio__MetadataContactUrl`) with the repository URL as default. If the repo is
private, the owner must pick a public page; flag this to the owner.

**Recommendation:** MusicBrainz (match by ISRC from spotDL, or artist + title + duration) -> CAA cover ->
Wikidata QID -> Wikipedia summary (show "From Wikipedia, CC BY-SA 4.0" + link + "text shortened").
Skip TheAudioDB (no licence for images, unclear terms) and Last.fm (no images, account required). If
the owner wants one later, the key comes only via env var and the feature is off without it.
Throttle in one `MetadataHttpClient` (1 req/s to MusicBrainz) with `Microsoft.Extensions.Http.Resilience`.

---

## 5. Feature 006: synced lyrics with karaoke ball

### 5.1 LRCLIB

| Item | Detail | Source |
|---|---|---|
| Endpoint | `GET https://lrclib.net/api/get?track_name=&artist_name=&album_name=&duration=` (duration in s, 1-3600). Lyrics are returned only when the duration matches **±2 s**. Also `GET /api/get/{id}`, `GET /api/search?q=` (max 20), beta `GET /api/v2/search` (no lyrics payload). | [docs.md](https://lrclib.net/docs.md) |
| Fields | `syncedLyrics` (LRC, line timestamps), `plainLyrics`, `instrumental`, **`hasWordSync`** + `lyricsfile` (YAML, word timings in `lines[].words` when word-synced) | same |
| Auth | No API key; anonymous access allowed. Optional app registration for higher limits (not needed). | same |
| Rate limits | Generic client about 45 req/min/IP; **identified `User-Agent` about 90 req/min/IP**; honour `Retry-After` on 429/503 with jitter; send requests sequentially with 200-500 ms gaps | same |
| User-Agent | Required: app name, version, homepage link (for example `StemStudio v1.0 (https://github.com/...)`) | same |
| Licence of the data / ToS | **Not stated** in the docs, `llms.txt` or the home page (the dumps and Lyricsfile pages render via JS and could not be read). Code repo is MIT, which says nothing about the data. | [lrclib repo](https://github.com/tranxuanthang/lrclib); [llms.txt](https://lrclib.net/llms.txt) |
| Alternatives | NetEase/Musixmatch-style unofficial endpoints: no (ToS / scraping). Musixmatch official API is a paid tier for synced lyrics (**not verified**, not pursued). The LRCLIB DB dump can be used offline (licence unknown). | - |

**Copyright note (UI + README):** lyrics are copyrighted works. StemStudio shows them only locally,
for personal practice with a track the user imported. They are cached in the local DB per track, never
exported or shared, and a "lyrics via LRCLIB" credit is shown. The existing personal-use notice
(constitution IV) is extended to lyrics.

### 5.2 Word-level timing for the bouncing ball

| Case | Approach |
|---|---|
| `hasWordSync=true` | Use `lyricsfile` word times directly. |
| Line-level LRC only | Interpolate inside each line: split words, weight duration by syllable count (simple vowel-group heuristic, language-agnostic) or by character count. Ball arcs from word to word; the line end is the next line's timestamp (or +3 s for the last line). |
| Drift | One global offset slider (like the metronome offset) + "tap to sync" on the first line. |
| Timing | The ball position is derived from the audio engine's `AudioContext.currentTime` via `requestAnimationFrame` (visual only; constitution V forbids timers for *audible* timing, and visual rAF is fine). |

### 5.3 Automatic alignment when no synced lyrics exist

| Option | Code licence | Weights licence | Fit | Source |
|---|---|---|---|---|
| **stable-ts 2.19.1** `model.align(audio, text, language)` on the Demucs **vocals** stem | MIT | Whisper large-v3-turbo **MIT**; large-v3 Apache-2.0 | Word-level timestamps for given text, many languages (incl. Dutch). Needs `torch`, **`torchaudio`**, `openai-whisper<=20250625`, so a separate venv/container, or add torchaudio to the GPU image. GPU about 6 GB for turbo (estimate); CPU works but is slow. | [stable-ts](https://github.com/jianfch/stable-ts) LICENSE MIT; PyPI metadata; [HF whisper-large-v3-turbo](https://huggingface.co/openai/whisper-large-v3-turbo) |
| whisperX | BSD-2-Clause | Whisper MIT; alignment uses per-language wav2vec2 checkpoints whose licences vary (**not verified** per language) | Good word timing, but more deps (pyannote etc.) | [whisperX](https://github.com/m-bain/whisperX) |
| Qwen3-ForcedAligner-0.6B | (transformers, Apache-2.0) | **Apache-2.0** | Card: up to 5 min, 11 languages (**no Dutch**), audio type "Speech" only (singing not claimed). Needs transformers from source. | [model card](https://huggingface.co/Qwen/Qwen3-ForcedAligner-0.6B-hf) |
| HeartTranscriptor-oss (lyrics ASR, whisper-based, for "no lyrics at all") | Apache-2.0 (heartlib) | **Apache-2.0** | Tuned for lyrics transcription; output then aligned with stable-ts | [heartlib](https://github.com/HeartMuLa/heartlib), HF `HeartMuLa/HeartTranscriptor-oss` |

Recommendation: LRCLIB first. If it has nothing or only plain lyrics, run stable-ts alignment of the
plain text (from LRCLIB or pasted by the user) to the vocals stem as a GPU job (004). Store the result
as word-timed LRC in our DB and mark it "auto-aligned".

---

## 6. Feature 007: export isolated channels

| Format | Library | Licence | Note |
|---|---|---|---|
| WAV 16/24-bit | soundfile (libsndfile) | BSD-3 / LGPL-2.1+ (already present) | lossless |
| FLAC | soundfile | same | default for the export library (already used for stems) |
| MP3 (CBR/VBR) | **lameenc 1.9.0** | LGPL-3.0+ (already present via demucs) | Add ID3 tags + cover via mutagen (GPL, present) or skip tags |

**Offline rendering: server-side numpy in the worker (recommended) vs browser `OfflineAudioContext`.**

| Criterion | Server-side numpy | Browser OfflineAudioContext |
|---|---|---|
| Works for the agent (011) and for 008/009 pipelines with no browser open | **yes** | no |
| Uses the stems already on disk | yes (no transfer) | must have all stems decoded in the browser (they are for playback, but 6-12 stems x 4 min x 32-bit is 0.5-1 GB) |
| Exact match with what the user hears | must re-implement the gain/pan law and mute/solo exactly; verify with a golden test | identical graph by construction |
| Testability (pytest, 90 % coverage) | easy, deterministic | Vitest with a mocked AudioContext tests wiring only |
| Long files / memory | stream in blocks | whole render in memory |

Use a worker job `POST /exports {sources: [stemIds], mixSnapshot, format, range?}` that sums the
float32 stems with the same equal-power pan law as the TS engine (define it once in the contract
docs). Then dither/convert, encode, and write to `exports/` with a DB row in a personal export library.
Keep one Playwright/Vitest golden comparison: browser OfflineAudioContext render vs worker render,
RMS diff below -60 dB.

---

## 7. Feature 008: sheet music per instrument

### 7.1 Transcription models (audio -> MIDI)

| Model | Instruments | Code licence | Weights licence | Runtime / deps | Verdict | Source |
|---|---|---|---|---|---|---|
| **Spotify basic-pitch** 0.4.0 | any pitched, single instrument (polyphonic), with pitch bends | Apache-2.0 | Apache-2.0 (shipped in the repo/package, also as ONNX) | pip package on Py >= 3.11 pulls **`tensorflow<2.15.1`** (clashes with numpy 2.5) -> **vendor the ONNX model + MIT/Apache post-processing, run with our onnxruntime** | **Use** for vocals melody, bass, guitar, "other" | [basic-pitch](https://github.com/spotify/basic-pitch) LICENSE + README; PyPI metadata |
| **ByteDance piano_transcription_inference** | piano (notes + sustain pedal) | MIT (inference package); research repo README says Apache 2.0 | **CC BY 4.0** (Zenodo record 4034264) | torch, about 170 MB checkpoint, GPU optional | **Use** for the piano stem; attribution needed (CC BY) | [repo](https://github.com/qiuqiangkong/piano_transcription_inference) LICENSE; [Zenodo 4034264](https://zenodo.org/records/4034264) API `license: cc-by-4.0` |
| **YourMT3+** | multi-instrument incl. **drums** | **GPL-3.0** (GitHub LICENSE) | Apache-2.0 (HF card "solely to host the model checkpoints") | torch + lightning; repo last pushed 2024-11, so old deps likely; isolate it | **Use for drums** (and optional multi-instrument), in its own venv/container; copyleft is OK (OSI) | [GitHub](https://github.com/mimbres/YourMT3), [HF](https://huggingface.co/mimbres/YourMT3) |
| MT3 (Magenta) | multi-instrument | Apache-2.0 | checkpoints in a GCS bucket; licence **not verified** | JAX/T5X, heavy | Skip (stack mismatch) | [magenta/mt3](https://github.com/magenta/mt3) |
| **ADTOF** | drums | **CC BY-NC-SA 4.0 (the repo LICENSE, so the code too)** | same | TF | **Excluded**: code is not OSI (constitution IV); the ADR-009 exception covers weights only | [ADTOF LICENSE](https://github.com/MZehren/ADTOF/blob/master/LICENSE) |
| omnizart 0.6.3 | piano, vocal melody, chords, drums, beat | MIT | checkpoints via `omnizart download-checkpoints`, licence **not stated**; README: drum model "has unknown bugs" for training | TensorFlow | Skip (TF + stale) | [omnizart](https://github.com/Music-and-Culture-Technology-Lab/omnizart) |
| SheetSage2 (m-a-p, used by YuE2 covers) | lead sheet (melody + chords) | - | **CC BY-NC 4.0** | - | Possible later for lead sheets (NC is allowed under ADR-009) | [HF card](https://huggingface.co/m-a-p/SheetSage2) |

### 7.2 MIDI -> MusicXML -> PDF

| Step | Tool | Licence | Implication |
|---|---|---|---|
| Quantise | Our own code: snap onsets/durations to the **existing beat grid** (BPM, downbeats, time signature from the beat-analysis agent), 1/16 grid default, triplet detection optional; drop notes < 1/32; merge ties | MIT (ours) | Biggest quality lever; uses data we already have |
| MIDI I/O | pretty_midi 0.2.11 | MIT | - |
| MusicXML + MIDI out | **music21 10.5.2** (key/clef/part naming, drum map to percussion staff) | BSD-3-Clause ([cuthbertLab/music21](https://github.com/cuthbertLab/music21)) | - |
| Render | **Verovio 6.4.1** Python wheel (MusicXML -> SVG per page) | **LGPL-3.0-only** ([repo COPYING](https://github.com/rism-digital/verovio/blob/develop/COPYING), PyPI) | Used unmodified as a dynamic library, so OK (like libsndfile) |
| SVG -> PDF | CairoSVG 2.9.1 (+ cairo) | LGPL-3.0-or-later | same reasoning; alternative: render SVG in the browser and print |
| Optional engraving | LilyPond (`musicxml2ly` + `lilypond`), Debian package, run as a **separate executable** | GPL-3.0 ([COPYING](https://github.com/lilypond/lilypond/blob/master/COPYING)) | Separate program via subprocess (argument arrays), so no licence effect on our MIT code; adds about 100+ MB to the image (estimate) |
| Not chosen | MuseScore Studio CLI | GPL-3.0 (+ fonts under GNU FreeFont exception) ([LICENSE.txt](https://github.com/musescore/MuseScore/blob/main/LICENSE.txt)) | Qt AppImage, heavy, headless rendering awkward in Docker |

**Pipeline:** stem (FLAC) -> model per stem type -> raw MIDI -> beat-grid quantise -> music21 score ->
MusicXML + MIDI (download) -> Verovio SVG (preview in UI) -> PDF (CairoSVG).

**Expected quality (honest, constitution V):** monophonic lines (bass, lead vocal melody) are good to
usable. Piano is good on clean piano stems (the checkpoint's own name reports onset F1 0.9677 on
MAESTRO; separated stems will be worse). Guitar/keys in "other" are rough (polyphony + separation
bleed). Drums give a usable kick/snare/hi-hat groove, while cymbal and tom details are unreliable.
Output is a practice lead sheet, not an engraved score. Show this in-app. GPU is optional: CPU
transcription of a 4-min stem takes about tens of seconds to a few minutes (estimate, not measured).

---

## 8. Features 009 / 010: generative models

### 8.1 Candidates (status 2026-11-15)

| Family | Code licence | Weights licence | Style transfer / cover | Lyrics -> song with vocals | Duration | VRAM / speed (16 GB card) | Forbidden uses / notes | Source |
|---|---|---|---|---|---|---|---|---|
| **ACE-Step 1.5** (2B DiT base/sft/turbo, XL 4B, LM planner 0.6/1.7/4B) | **MIT** | **MIT** (HF cards `Ace-Step1.5`, `acestep-v15-sft`, `-xl-sft`, `-xl-turbo`, `acestep-5Hz-lm-0.6B/4B`; 1.7B card not readable) | **Yes**: reference audio, **Cover**, **Repaint**, Vocal2BGM; base models also extract/lego/complete; LoRA training "from just a few songs" | **Yes**, 50+ languages, LRC timestamps | 10 s - 10 min, batch of 8 | "<4 GB" (2B turbo, no LM); **12-16 GB: 2B turbo/sft + 1.7B LM**; XL needs >= 12 GB with offload + quantisation, 20 GB without. "Under 10 s per song on an RTX 3090." | No explicit forbidden-use clause; disclaimer: verify originality, disclose AI, get permission for protected styles; authors not liable. Pins **torch 2.10.0+cu128** (Linux), so it needs its **own container**. Has a REST server (`acestep-api`). | [ACE-Step-1.5 README](https://github.com/ace-step/ACE-Step-1.5), [LICENSE](https://github.com/ace-step/ACE-Step-1.5/blob/main/LICENSE), [pyproject](https://github.com/ace-step/ACE-Step-1.5/blob/main/pyproject.toml), HF API |
| ACE-Step v1 (3.5B) | Apache-2.0 | Apache-2.0 | audio2audio, repaint, edit | yes | up to about 4 min | about 8-16 GB (not re-verified) | superseded by 1.5 | [ACE-Step](https://github.com/ace-step/ACE-Step), HF `ACE-Step/ACE-Step-v1-3.5B` |
| **YuE2** (Sept 2026) | Apache-2.0 | **CC BY-NC 4.0 + "additional creator permission"** (personal users may monetise outputs; companies need a licence) | **Yes**: zero-shot covers via SheetSage2 transcription; editable symbolic plan | **Yes**, very high quality ("competitive with Suno v5/v6" per authors) | full songs | Default "24 GB VRAM"; `--low-vram` opt-in, tested on 8 GB laptop with FP8 AR; 16 GB lengths "remain to be measured" | Creator permission "prohibits illegal, harmful, deceptive, or unethical use". Pins **torch 2.10.0**. NC is allowed under ADR-009. | [YuE README](https://github.com/multimodal-art-projection/YuE), [hardware.md](https://github.com/multimodal-art-projection/YuE/blob/main/docs/hardware.md) |
| HeartMuLa-oss-3B (+ HeartCodec) | Apache-2.0 | **Apache-2.0** ("License update ... all related model weights to Apache 2.0") | via **MuLaCover**: weights **CC BY-NC 4.0** | **Yes** (lyrics + tags, multilingual) | full songs | `--lazy_load` for single GPU; RTF about 1.0; VRAM **not stated** | - | [heartlib](https://github.com/HeartMuLa/heartlib), [MuLaCover card](https://huggingface.co/HeartMuLa/MuLaCover) |
| DiffRhythm 2 (Xiaomi/ASLP) | Apache-2.0 | **Apache-2.0** ("code and weights") | reference WAV for style | **Yes** | full songs | about 1.1B params; VRAM not stated | Disclaimer "users must implement verification mechanisms ... disclose AI involvement" | [DiffRhythm2](https://github.com/ASLP-lab/DiffRhythm2), HF `ASLP-lab/DiffRhythm2` |
| SongBloom / SongGeneration (LeVo), Tencent | **Not verifiable**: `github.com/tencent-ailab/SongGeneration` and the official SongBloom repo return 404; HF `tencent/SongGeneration`, `tencent/SongBloom` return 401 (removed or gated). Mirrors show `unknown`. | - | - | - | - | - | **Exclude** (no primary source) | gh API / HF API, 2026-11-15 |
| MusicGen / AudioCraft | MIT | **CC BY-NC 4.0** | melody conditioning (MusicGen-melody) | **No vocals** | about 30 s windows | about 8-16 GB | NC allowed under ADR-009, but no vocals, so a poor fit | [audiocraft](https://github.com/facebookresearch/audiocraft), HF `facebook/musicgen-*` |
| Stable Audio Open 1.0 / small | MIT (stable-audio-tools) | **Stability AI Community License** (gated) | audio-to-audio (init audio) | no sung lyrics | up to about 47 s (1.0) | small | Free under US$1M revenue and for non-commercial use; **"Powered by Stability AI" attribution required**; **forbidden: using outputs "to create or improve any foundational generative AI model"**; AUP incorporated. Usable but a poor fit. | [Community License](https://stability.ai/community-license-agreement), HF API |

### 8.2 Recommendation: one family for 009 and 010

**ACE-Step 1.5** is the only candidate that has MIT code **and** MIT weights **and** covers both
features natively:

- **009 Style transfer**: `Cover` task with the imported song (or its mixdown/stems) as source audio, a
  style caption from our **style library** (each style = caption + tags + optional reference clip +
  optional LoRA), and a strength parameter. `Repaint` lets the user restyle a section only.
- **010 Own lyrics -> song**: text2music with the user's lyrics (with `[verse]`/`[chorus]` markers) +
  style; vocals are generated natively. Output lands in the export library (007) and can be
  re-imported and separated (001/002) for the mixer.
- Setup for 12.5 GB of usable VRAM: **2B turbo (or sft) DiT + 0.6B/1.7B LM** (README tier "8-16 GB"). XL
  only with offload + quantisation, as an opt-in "high quality" toggle.
- Packaging: a **separate `generator` service** (own Dockerfile, torch 2.10.0+cu128 as pinned by
  ACE-Step; cu128 supports sm_120) behind our own small FastAPI contract (OpenAPI, versioned) that wraps
  `acestep` or its `acestep-api`. Never import it into the main worker (torch 2.15 vs 2.10).
- Optional quality upgrade: **YuE2** in a second service as "studio quality" for lyrics-to-song and
  covers (NC weights are allowed under ADR-009; record "commercial use not covered"). Only after
  measuring 16 GB behaviour with `--low-vram`.

### 8.3 Ethics and content rules (UI + docs)

- Style transfer changes arrangement/timbre "in the style of a genre", never "in the voice of
  <artist>". The style library contains genres/moods/instrumentation, **no artist names**. Prompts that
  name a real artist or ask to imitate a real singer are rejected (simple deny-list + agent guardrail).
- Generated files carry "AI-generated" in metadata (ID3/Vorbis comment) and in the export library.
- Personal practice only; restyled covers of copyrighted songs are not for publication (same notice as
  imports).

---

## 9. Feature 010 add-on: VoiceStudio (debpalash/voicestudio) for sung vocals

### 9.1 What it is and what it really does (checked in the repo, 2026-11-15)

| Question | Finding | Source |
|---|---|---|
| Licence | **AGPL-3.0** (app); "Model weights, voices and generated output have their own licences". Contributions under a CLA; a closed "Pro" build exists. | [README](https://github.com/debpalash/voicestudio#license--responsible-use), GitHub licence API |
| Capabilities | Speech **TTS**, voice **cloning**, voice **design**, video **dubbing**, dictation/**ASR**, audiobooks; local API, MCP server | README |
| **Singing synthesis?** | **No, not in the current code.** The only singing work is docs: `docs/adr/SPIKE-02-singing.md` (omnivoice-singing finetune, **superseded**: "no melody (F0/MIDI) conditioning — it sings its own melody") and a pointer that SoulX-Singer was "selected in plan-06". GitHub code search for `singing` / `SoulX` finds only `docs/` files, and the singing-mode spec tree was removed. | [SPIKE-02](https://github.com/debpalash/voicestudio/blob/HEAD/docs/adr/SPIKE-02-singing.md); `gh search code singing --repo debpalash/voicestudio` |
| Anything usable for sung lines | (1) `backend/services/rvc.py`: **optional RVC post-processing** (disabled by default; needs `rvc-python` and a user `.pth` voice model; pitch shift, rmvpe F0). RVC converts timbre while keeping pitch, so it *can* turn an existing sung vocal into another voice. (2) `POST /convert` "keep my delivery" speech-to-speech voice changer (designed for speech). Neither *creates* a melody from lyrics. | [rvc.py](https://github.com/debpalash/voicestudio/blob/HEAD/backend/services/rvc.py), `backend/api/routers/voice_convert.py` |
| Underlying models (examples from its licence inventory) | Default engine **OmniVoice**: code Apache-2.0, **weights CC BY-NC** ("due to constraints from its training data (e.g., Emilia)"). Others: CosyVoice3 0.5B (Apache-2.0), VoxCPM2 (Apache-2.0), Qwen3-TTS (Apache-2.0), Kokoro (Apache-2.0), GPT-SoVITS (MIT), IndexTTS-2.5 ("other"), Chatterbox (NOASSERTION), NLLB (CC BY-NC), faster-whisper (MIT), pyannote (MIT / CC BY 4.0). User RVC models: NOASSERTION. | [model_licenses.json](https://github.com/debpalash/voicestudio/blob/HEAD/backend/config/model_licenses.json), [model-inventory.md](https://github.com/debpalash/voicestudio/blob/HEAD/docs/licensing/model-inventory.md), [OmniVoice card](https://huggingface.co/k2-fsa/OmniVoice) |
| GPU / VRAM | CUDA, ROCm, Vulkan (GGUF) and CPU. GPU workers auto-sized "1 worker per 5 GB, max 4"; `OMNIVOICE_SINGLE_ENGINE_RESIDENT=1` by default; idle VRAM is freed before generation when < 2 GB headroom. Per-engine VRAM not given in one table. | [docs/performance.md](https://github.com/debpalash/voicestudio/blob/HEAD/docs/performance.md) |
| Separate service? | **Yes.** Docker images on GHCR/Docker Hub (CPU, CUDA, ROCm), UI + API on **port 3900**, `OMNIVOICE_API_KEY` bearer. HTTP API includes **OpenAI-compatible `POST /v1/audio/speech`**, `/v1/audio/transcriptions`, `/v1/audio/voices`, plus MCP. Loopback needs no credential. | README "Docker"; [docs/install/docker.md](https://github.com/debpalash/voicestudio/blob/HEAD/docs/install/docker.md); [speech-platform.md](https://github.com/debpalash/voicestudio/blob/HEAD/docs/speech-platform.md); `backend/api/routers/openai_compat.py` |

### 9.2 AGPL-3.0 implications for StemStudio (MIT)

| Scenario | Effect |
|---|---|
| Run the **unmodified** VoiceStudio image as a separate container; StemStudio calls its HTTP API | Separate programs communicating over HTTP ("aggregate"), so StemStudio code stays MIT. No source-offer duty from running unmodified AGPL software. Record it in THIRD-PARTY-LICENSES as a separate AGPL service. |
| **Modify** VoiceStudio and let users interact with it over a network (AGPL §13) | You must offer those users the Corresponding Source of the modified version. For a local single-user tool the only "user" is the owner, so keep the modified fork public or in the repo; the duty is easy to meet but real. |
| **Copy VoiceStudio code into StemStudio** (the owner allows reuse) | The copied parts are AGPL-derived; the combined worker would have to be distributed under AGPL. This conflicts with "StemStudio code stays MIT". **Don't copy code; reuse ideas only**, or keep any reused code inside the separate AGPL service. |
| Weights it downloads | Separate licences; the default OmniVoice is CC BY-NC (allowed under ADR-009 only if listed with SHA-256 etc.). |

Not legal advice; based on the AGPL-3.0 text as commonly read (§13 "Remote Network Interaction").

### 9.3 Better fit for sung vocals: SoulX-Singer (what VoiceStudio planned to use)

| Item | Detail | Source |
|---|---|---|
| What | Zero-shot **singing voice synthesis** with **melody (F0) or score (MIDI) conditioning** and timbre cloning from a short prompt; plus **SoulX-Singer-SVC** (singing voice conversion, audio-to-audio, no lyrics/MIDI needed); singing voice editing (change lyrics, keep prosody) | [SoulX-Singer README](https://github.com/Soul-AILab/SoulX-Singer) |
| Licence | Code **Apache-2.0**, weights **Apache-2.0** ("free to use the codes and model weights"); HF `Soul-AILab/SoulX-Singer` and `-Preprocess` cards: apache-2.0 | README, HF API |
| Languages | trained on Mandarin, English, Cantonese (no Dutch claim) | README |
| Use restrictions | "Do not use the model to impersonate individuals without authorization or to create deceptive audio"; respect consent | README "Usage Disclaimer" |
| Env | Python 3.10 conda per README; needs its own container | README |
| VRAM | **not stated** | - |

### 9.4 How to combine for 010

| Pipeline | Steps | Verdict |
|---|---|---|
| **P1 native (recommended default)** | ACE-Step 1.5 lyrics -> full song with AI vocals -> Demucs split (001) -> mixer | Simplest, MIT end to end |
| **P2 "my own voice"** | P1 -> take the generated vocal stem -> **SoulX-Singer-SVC** with a reference recording of **the user's own voice** -> replace the vocal stem | Keeps ACE-Step's melody/timing; Apache-2.0; best quality path for "sing it with my voice" |
| P3 score-driven | ACE-Step instrumental (or Vocal2BGM) + melody MIDI (from 008 transcription of the generated vocal, or user-edited) + lyrics -> **SoulX-Singer SVS** -> mix | Most control, most moving parts; later |
| P4 VoiceStudio | VoiceStudio TTS cannot sing a melody; its RVC hook could convert P1's vocal, but needs `rvc-python` + a user-trained `.pth` and is off by default | **Not recommended**: adds an AGPL service for something SoulX-Singer-SVC does directly |

### 9.5 Ethics and safety: voice cloning

- Clone **only the user's own voice** (recorded in-app with an explicit consent checkbox and a spoken
  consent phrase stored with the reference) or **synthetic/designed voices**. Never upload or
  reference recordings of other people or real artists. Reference clips extracted from imported songs
  are **blocked** as clone sources.
- No artist-imitation presets; prompts naming real singers are refused (shared guardrail with 8.3 and
  the agent).
- Mark outputs as AI-generated (metadata + library badge). Local only; never shared.
- Matches the SoulX-Singer and VoiceStudio disclaimers ("Clone a voice only with its owner's
  permission").

---

## 10. Feature 011: AI composer agent

### 10.1 Backends

| Backend | API | Tools | Source |
|---|---|---|---|
| Ollama 0.40.16 (host) | `POST http://localhost:11434/api/chat` with `tools: [{type:"function", function:{name, description, parameters}}]`; response `message.tool_calls[].function{name, arguments}`; reply with `{role:"tool", tool_name, content}`; streaming collects `tool_calls` across chunks. `keep_alive` (default 5m; `0` unloads). From containers: `http://host.docker.internal:11434`. | yes | [Ollama tool calling](https://docs.ollama.com/capabilities/tool-calling); [api.md keep_alive](https://github.com/ollama/ollama/blob/main/docs/api.md) |
| Claude API | Messages API with client tools; **model `claude-opus-5-5`** (current default; `claude-sonnet-5-5` / `claude-haiku-5-5` as cheaper options only if the owner chooses). Key only via `ANTHROPIC_API_KEY`; feature off when unset. Opus 5.5 rejects forced `tool_choice` `any`/`tool` (use `auto`), and thinking is adaptive (effort default `medium`). | yes | claude-api skill reference (model table cached 2026-11-03); [Anthropic C# SDK](https://github.com/anthropics/anthropic-sdk-csharp) |

### 10.2 Local models that fit next to other GPU work

| Model (Ollama tag) | Size (Q4) | Tools | Licence | Source |
|---|---|---|---|---|
| **`qwen3.5:9b`** (already pulled) | 6.6-7.6 GB, 256K ctx | tools, thinking, vision | **Apache-2.0** | [ollama.com/library/qwen3.5](https://ollama.com/library/qwen3.5); [HF Qwen3.5-9B](https://huggingface.co/Qwen/Qwen3.5-9B) |
| `qwen3.5:4b` (low-VRAM fallback) | 3.3-4.0 GB | tools | Apache-2.0 | same |
| `gemma4:12b` / `gemma4:e4b` | about 8 / 9 GB (local e4b = 9.2 GB) | tools, thinking | **Apache-2.0** ("Gemma 4 license" page = Apache License 2.0) + a separate prohibited-use policy page | [ai.google.dev/gemma/docs/gemma_4_license](https://ai.google.dev/gemma/docs/gemma_4_license), HF card |
| `ministral-3:14b` / `:3b` (2512) | about 9 / 3 GB | tools | Apache-2.0 | [ollama ministral-3](https://ollama.com/library/ministral-3), HF card |
| `gpt-oss:20b` (already pulled) | about 13 GB | tools, thinking | Apache-2.0 | HF `openai/gpt-oss-20b` |
| `qwen3.8:27b` (already pulled) | 17 GB | tools | Apache-2.0 | HF `Qwen/Qwen3.8-27B` (too big for the GPU next to anything else) |
| `llama3.1:8b` | about 5 GB | tools | Llama 3.1 Community Licence (gated, AUP; not OSI) | HF `meta-llama/Llama-3.1-8B-Instruct` (`gated: manual`) |

Recommendation: default **`qwen3.5:9b`**, fallback `qwen3.5:4b` while a generative job holds the GPU,
Expert setting to choose any pulled model. Avoid Llama (custom licence) and Qwen3.8-Flash-Next
("qwen-community-1.0" licence, 180B).

### 10.3 .NET integration (both backends behind one abstraction)

| Package | Version | Licence | Role | Source |
|---|---|---|---|---|
| Microsoft.Extensions.AI | 11.0.0 | MIT | `IChatClient`, `ChatClientBuilder.UseFunctionInvocation()`, `AIFunctionFactory`, `FunctionInvokingChatClient`, **`ApprovalRequiredAIFunction`** -> `ToolApprovalRequestContent` / `ToolApprovalResponseContent` | [IChatClient docs](https://learn.microsoft.com/dotnet/ai/ichatclient#tool-calling), [FunctionInvokingChatClient](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.functioninvokingchatclient), NuGet nuspec |
| OllamaSharp | 5.5.2 | MIT | `OllamaApiClient` implements `IChatClient` (used in Microsoft's own samples) | [awaescher/OllamaSharp](https://github.com/awaescher/OllamaSharp), [Learn sample](https://learn.microsoft.com/dotnet/ai/ichatclient) |
| Anthropic (official C# SDK) | 13.8.0 | **Apache-2.0** | `AnthropicClient`; "supports Microsoft.Extensions.AI IChatClient integration with function invocation" (depends on MEAI.Abstractions) | [anthropic-sdk-csharp](https://github.com/anthropics/anthropic-sdk-csharp), NuGet nuspec; claude-api skill C# README |
| (rejected) Anthropic.SDK by tghamm | - | MIT | community SDK; prefer the official one | [tghamm/Anthropic.SDK](https://github.com/tghamm/Anthropic.SDK) |

Design: `IAgentChatClientFactory` picks Ollama (default) or Anthropic (only when `ANTHROPIC_API_KEY`
is set **and** the user enabled it in settings, with a "paid per use" notice). Pipeline
`.UseFunctionInvocation(o => o.MaximumIterationsPerRequest = 8).UseOpenTelemetry()`. Tools are thin
`AIFunction` wrappers over the **existing API client** (same contract as the UI), never direct DB or
file access.

### 10.4 Safety design

| Control | Implementation |
|---|---|
| Tool allow-list | One `AgentToolCatalog` with explicit tools: `search_library`, `import_youtube(url)`, `import_spotify(url)`, `separate(trackId, detail?)`, `set_mix(trackId, channel, gain/pan/mute/solo)`, `save_mix`, `export(sources, format)`, `transcribe(stemId)`, `restyle(trackId, styleId)`, `generate_song(lyrics, styleId)`, `get_job_status`. No shell, no file paths, no raw HTTP, no URL fetch. |
| Level awareness | Tools filtered by the user's journey level (003 `FeatureCatalog`) |
| Confirmation | Destructive or costly tools (`delete_*`, overwrite mix, start GPU-heavy jobs > N minutes, any Claude call that exceeds a token budget) wrapped in **`ApprovalRequiredAIFunction`**; UI shows a confirm card with the exact arguments. `AllowMultipleToolCalls = false` for those turns, because one approval-requiring call makes the whole batch require approval. |
| Input validation | Same validators as the HTTP API (allow-listed hosts, ids must exist, sizes). Tool arguments are parsed as JSON and schema-validated. |
| Prompt-injection | Treat track titles, lyrics, Wikipedia text and tool results as data. The system prompt says so; tools never execute content. |
| Limits | Max iterations per request, per-session job quota, cancel button, full tool-call log (Expert level) |
| Privacy | Claude backend sends only what the tools return (no audio). Shown in the settings notice. |
| Constitution | Needs the amendment noted in the ROADMAP (opt-in paid external service, key via env var). |

---

## 11. Licence records to add (when features land)

| Component | Kind | Licence | Notes |
|---|---|---|---|
| driver.js 1.14.0 | JS lib (shipped) | MIT | - |
| Inter, JetBrains Mono | fonts | OFL-1.1 | self-hosted |
| Markdig 1.6.0 | NuGet | BSD-2-Clause | - |
| tinytag 2.3.2 | PyPI | MIT | - |
| onnxruntime-gpu 1.32.0 | PyPI (GPU image) | MIT | replaces onnxruntime there |
| torch 2.15.0+cu132 + NVIDIA CUDA/cuDNN wheels | PyPI (GPU image) | BSD-3 / NVIDIA EULA (CUDA redistributables) | NVIDIA libs are not OSI. They are runtime redistributables in a local image; **needs an owner note** in THIRD-PARTY-LICENSES (same logic as the weights exception, or treat them as a system driver dependency) |
| stable-ts 2.19.1 + openai-whisper | PyPI | MIT | Whisper weights MIT/Apache |
| music21 10.5.2, pretty_midi 0.2.11 | PyPI | BSD-3 / MIT | - |
| Verovio 6.4.1, CairoSVG 2.9.1 | PyPI | LGPL-3.0 | unmodified libraries |
| LilyPond (optional) | Debian exe | GPL-3.0 | separate executable |
| YourMT3+ | code | GPL-3.0 | weights Apache-2.0 |
| basic-pitch ONNX | weights + vendored code | Apache-2.0 | - |
| piano_transcription_inference | code / weights | MIT / CC BY 4.0 | attribution |
| ACE-Step 1.5 | code + weights | MIT / MIT | separate generator image (torch 2.10+cu128) |
| SoulX-Singer (optional, 010 P2) | code + weights | Apache-2.0 / Apache-2.0 | separate image |
| YuE2 (optional) | code / weights | Apache-2.0 / CC BY-NC 4.0 + creator permission | ADR-009 list |
| Microsoft.Extensions.AI 11.0.0, OllamaSharp 5.5.2, Anthropic 13.8.0 | NuGet | MIT / MIT / Apache-2.0 | - |
| Qwen3.5 (via Ollama) | weights | Apache-2.0 | pulled by the user into Ollama, not by us |

---

## 12. Cross-cutting risks and the GPU job policy

### 12.1 Risks

| Risk | Detail | Mitigation |
|---|---|---|
| **VRAM contention** | About 12.5 GB usable (Windows desktop holds about 3.2 GB). Ollama (host) qwen3.5:9b about 7 GB + KV cache; Demucs htdemucs_6s about 3-5 GB (estimate); ACE-Step 2B + 1.7B LM about 8-12 GB; stable-ts turbo about 6 GB; YuE2 default 24 GB. Two of these together will OOM or spill into shared memory (WDDM pages to system RAM and gets **much slower** instead of failing). | GPU lease (below), unload Ollama (`keep_alive: 0`) before heavy jobs, small LM fallback |
| **GPU scheduling across containers** | Worker, generator and Ollama are separate processes; CUDA has no cross-process priority on WDDM | One **GPU broker** in the API (the only orchestrator); services never self-start GPU work |
| **Torch version split** | Worker torch 2.15+cu132, ACE-Step and YuE2 pin torch 2.10 | One container per model family; contracts over HTTP |
| **Disk** | Weights: ACE-Step 1.5 2B sft about 4.8 GB + XL about 10 GB + LM 4B about 8 GB; YuE2 several GB; YourMT3 + whisper + SoulX about 5 GB; existing Demucs + detail about 2.2 GB; GPU images +5-7 GB each (worker, generator). Total easily **40-60 GB** (estimate). Plus exports and generated audio. | Model manager page (sizes, delete), download on first use with confirmation, `STEMSTUDIO_MODEL_DIR` volume on a large disk, export quota warning |
| **Licence drift** | HF cards can change (e.g. HeartMuLa changed to Apache; Tencent repos vanished) | Pin model revision (commit hash) + SHA-256 in THIRD-PARTY-LICENSES (ADR-009 condition 1) |
| **Existing GPU override broken** | `cu128` default has no torch 2.15.0 | Fix in 004 (cu132) |
| **Docker Desktop memory** | Detail separation already needs 16 GB in the worker; generator + worker can exceed the WSL memory cap | Document `.wslconfig` memory (for example 48-64 GB of the 127 GB) |

### 12.2 Recommended GPU job policy

1. **Single GPU lease, owned by the API** (`GpuScheduler` hosted service): at most **one GPU job at a
   time** across worker and generator. Jobs declare `{kind, estimatedVramGb, estimatedSeconds,
   cpuFallbackAllowed}`.
2. **Priority classes**: (a) interactive and short (agent LLM turn, lyric alignment < 1 min), (b)
   separation/transcription, (c) generative (009/010). FIFO within a class; a running job is never
   pre-empted, and the next one waits.
3. **Before class (b)/(c) starts**: tell Ollama to unload (`POST /api/chat {model, messages: [], keep_alive: 0}`).
   During the job the agent uses `qwen3.5:4b` on CPU (`num_gpu: 0`), or Claude if enabled, or waits.
4. **Model residency**: load, run, unload for generative and transcription models. Demucs may stay warm
   only while the queue holds more separation jobs.
5. **Pre-flight**: check free VRAM (`torch.cuda.mem_get_info`) against the job's estimate + 1.5 GB
   margin. If it doesn't fit, wait or fall back to CPU (b) / refuse with a message (c).
6. **OOM handling**: catch `torch.cuda.OutOfMemoryError`, `empty_cache()`, retry once on CPU when
   allowed, otherwise fail with "GPU busy or too little VRAM".
7. **Visibility**: queue + current GPU job + VRAM in the UI status bar (Advanced/Expert), and the agent
   can read it through `get_job_status`.

## 13. Open questions for the owner / CTO

| # | Question |
|---|---|
| 1 | NVIDIA CUDA runtime wheels (non-OSI redistributables) in the GPU image: accept as a system-level dependency (like the driver), with a note in THIRD-PARTY-LICENSES? |
| 2 | Public contact URL for the MusicBrainz/Wikimedia User-Agent (repo URL if public, otherwise a project page). |
| 3 | 009/010: ACE-Step 1.5 only (MIT) or also YuE2 (CC BY-NC weights, higher quality, 24 GB default) as an optional "studio" model? |
| 4 | 010 P2 (sing with my own voice via SoulX-Singer-SVC): in scope for 010 or later? VoiceStudio is not recommended (no singing, AGPL service). |
| 5 | Constitution amendment for the optional Claude backend (already noted in the ROADMAP). |
