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

| nvidia-ml-py (NVML bindings, GPU memory checks; both images) | 13.615.71 | BSD-3-Clause | https://pypi.org/project/nvidia-ml-py/ |

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
| soxr 1.1.0 | LGPL-2.1+ | librosa |
| mutagen 1.48.1 | GPL-2.0+ | spotdl |
| unidecode 1.4.0 | GPL-2.0+ | spotdl |
| text-unidecode 1.3 | Artistic-1.0 / GPL | spotdl |
| pykakasi 2.3.0 | GPL-3.0+ | spotdl |
| spotapi 1.2.8 | GPL-3.0 | spotdl (unofficial Spotify web client, metadata only) |
| certifi, pathspec | MPL-2.0 | requests stack / lizard |

**Distribution note:** anyone who redistributes the worker image must comply with the GPL/LGPL
terms of these components (offer source, keep notices).

## Online data services (feature 005, FR-014)

The worker queries these services only when online lookups are on (`STEMSTUDIO_ONLINE_LOOKUPS`,
default `on`) and sends only the song title, artist, duration and ISRC (MusicBrainz) or the ids
found there; the User-Agent names StemStudio and, when set, `STEMSTUDIO_CONTACT_URL` (never an
e-mail address). Nothing they return is bundled with StemStudio, and pictures and band texts are
never written into exports.

| Service | Data used | Licence / terms | How StemStudio complies |
|---|---|---|---|
| MusicBrainz WS/2 (https://musicbrainz.org/doc/MusicBrainz_API) | core data: recordings, releases, release groups, artists, URL relations | **CC0 1.0** (core data; supplementary data such as tags and annotations is not used) | 1 request/s, descriptive User-Agent, stored with the track |
| Cover Art Archive (https://coverartarchive.org) | front cover of a release group | **No licence stated**; images belong to their rights holders | images are shown **in the app only** (cover, thumbnails), never exported or redistributed; personal-practice notice |
| Wikidata (https://www.wikidata.org) | genres, members, founding year, country, website, Wikipedia site links | **CC0 1.0** (structured data) | ids and facts only |
| Wikipedia REST `page/summary` (https://en.wikipedia.org/api/rest_v1/) | the summary excerpt of the artist article (`STEMSTUDIO_WIKIPEDIA_LANGUAGES`) | **CC BY-SA 4.0** (text) | excerpt shown unmodified with **in-app attribution** (article link, "Wikipedia contributors" history link, licence link, "Excerpt, unmodified"; `band-info-attribution`); never exported |

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
| NSubstitute (+ Analyzers) | 6.2.0 / 1.0.17 | BSD-3-Clause / MIT |
| Shouldly | 4.3.0 | BSD-3-Clause |
| YamlDotNet | 18.1.0 | MIT |
| dotnet-ef (local tool) | 10.0.15 | MIT |
| dotnet-sonarscanner (local tool, CI only, not shipped) | 12.0.0 | LGPL-3.0 |
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
