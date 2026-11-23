# syntax=docker/dockerfile:1
# StemStudio worker: FastAPI + yt-dlp/spotDL + Demucs (CPU torch) + librosa, plus the detail
# models' runtimes (onnxruntime CPU, rotary-embedding-torch), lyrics alignment (006: stable-ts,
# openai-whisper, torchaudio) and sheet music (008: music21, verovio, cairosvg + libcairo2, pypdf,
# the Leipzig music font as a system font) and setlist PDFs (014: reportlab, the DejaVu fonts of
# fonts-dejavu-core) and handouts (020: weasyprint with Pango/PangoFT2). No model weights are baked in: they download at first
# use into /models (STEMSTUDIO_MODEL_DIR, HF_HOME, TORCH_HOME; a volume), e.g. Whisper turbo into
# /models/whisper (1.6 GB) and the piano/basic-pitch weights (008) below /models.
# Build context: repository root.  docker build -f docker/worker.Dockerfile .
#
# Two image variants from one Dockerfile (ADR-011, ADR-012 D1; research.md G-01):
#   CPU (default, CI): TORCH_VARIANT=cpu ORT_VARIANT=cpu installs exactly what worker/uv.lock pins
#     (torch 2.15.0+cpu, torchaudio 2.11.1+cpu, onnxruntime 1.32.0; triton and matplotlib are
#     excluded by uv overrides). Contains no NVIDIA runtime library; guarded by
#     scripts/check-cpu-image.{ps1,sh} (CI job cpu-image-guard).
#   GPU (optional, docker-compose.gpu.yml): TORCH_VARIANT=cu132 replaces torch and torchaudio with
#     the CUDA 13.2 wheels of the same versions (torch 2.15.0+cu132, torchaudio 2.11.1+cu132; 006
#     Phase 0 P0-1); ORT_VARIANT=gpu replaces onnxruntime with onnxruntime-gpu 1.32.0
#     (no extras: it reuses torch's CUDA libraries via onnxruntime.preload_dlls()) plus
#     cuda-toolkit[curand] 13.2.2 (libcurand, which torch does not ship). About 11.6 GB on disk;
#     the NVIDIA CUDA runtime libraries are proprietary system dependencies of this variant only.
#     WORKER_DEVICE=auto makes the GPU image try the GPU by default (falls back to the CPU with a
#     reason in /health when no usable GPU is present).
#   GPU build by hand (scripts/gpu-smoke.* does this):
#     docker build -f docker/worker.Dockerfile --build-arg TORCH_VARIANT=cu132
#       --build-arg ORT_VARIANT=gpu --build-arg WORKER_DEVICE=auto -t stemstudio/worker:local-gpu .
#   /gpu-lease (both variants, empty, owned by the runtime user) is the mount point of the shared
#   gpu-lease volume of docker-compose.gpu.yml (009 ADR-018 D3): an empty named volume takes the
#   ownership of the first image that mounts it, so the worker and the generator (same uid 10001)
#   can both create /gpu-lease/gpu.lock.

ARG PYTHON_IMAGE=python:3.12-slim
ARG UV_IMAGE=ghcr.io/astral-sh/uv:0.13.5
# yt-dlp needs a JavaScript runtime for YouTube signature challenges (deno, MIT).
ARG DENO_IMAGE=denoland/deno:bin-2.9.8

FROM ${UV_IMAGE} AS uv
FROM ${DENO_IMAGE} AS deno

# ---------------------------------------------------------------- build: resolve the venv
FROM ${PYTHON_IMAGE} AS build
COPY --from=uv /uv /usr/local/bin/uv
ENV UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy \
    UV_PYTHON_DOWNLOADS=never \
    UV_PROJECT_ENVIRONMENT=/app/.venv
WORKDIR /app
# Dependencies first (layer cached until the lock changes), then the project itself.
COPY worker/pyproject.toml worker/uv.lock worker/README.md ./
RUN --mount=type=cache,target=/root/.cache/uv \
    uv sync --frozen --no-dev --no-install-project
COPY worker/src ./src
RUN --mount=type=cache,target=/root/.cache/uv \
    uv sync --frozen --no-dev --no-editable
ARG TORCH_VARIANT=cpu
RUN --mount=type=cache,target=/root/.cache/uv \
    case "$TORCH_VARIANT" in \
      cpu) ;; \
      cu[0-9][0-9]*) uv pip install --python /app/.venv/bin/python \
          --reinstall-package torch --reinstall-package torchaudio \
          "torch==2.15.0" "torchaudio==2.11.1" \
          --index-url "https://download.pytorch.org/whl/${TORCH_VARIANT}" ;; \
      *) echo "TORCH_VARIANT must be cpu or cuNNN, got '$TORCH_VARIANT'" >&2; exit 1 ;; \
    esac
# onnxruntime-gpu and onnxruntime both provide the 'onnxruntime' package: remove the CPU one first.
ARG ORT_VARIANT=cpu
RUN --mount=type=cache,target=/root/.cache/uv \
    case "$ORT_VARIANT" in \
      cpu) ;; \
      gpu) uv pip uninstall --python /app/.venv/bin/python onnxruntime \
        && uv pip install --python /app/.venv/bin/python \
          "onnxruntime-gpu==1.32.0" "cuda-toolkit[curand]==13.2.2" ;; \
      *) echo "ORT_VARIANT must be cpu or gpu, got '$ORT_VARIANT'" >&2; exit 1 ;; \
    esac

# ---------------------------------------------------------------- font: Leipzig (008, SC-004)
# Verovio's SVG text glyphs use font-family "Leipzig"; Cairo renders them only when fontconfig
# knows the font. worker/scripts/install_music_font.py converts the WOFF2 embedded in the pinned
# verovio wheel to Leipzig.ttf (OFL-1.1). fontTools[woff] (MIT, with brotli) is installed into
# /opt/fonttools of this stage only and never reaches the runtime image.
# fontTools stamps head.modified with the current time unless SOURCE_DATE_EPOCH is set; the value
# below is the wheel font's own head.modified (Mac epoch 3869993456), so the output is reproducible
# and keeps the upstream timestamp: verovio 6.4.0 -> 119,416 B, SHA-256 1dcb5a73...81f5.
FROM build AS font
ARG FONTTOOLS_VERSION=4.68.0
ARG LEIPZIG_SOURCE_DATE_EPOCH=1787148656
COPY worker/scripts/install_music_font.py /opt/font-script/install_music_font.py
RUN --mount=type=cache,target=/root/.cache/uv \
    uv pip install --python /app/.venv/bin/python --target /opt/fonttools \
      "fonttools[woff]==${FONTTOOLS_VERSION}" \
 && SOURCE_DATE_EPOCH="${LEIPZIG_SOURCE_DATE_EPOCH}" PYTHONPATH=/opt/fonttools \
      /app/.venv/bin/python /opt/font-script/install_music_font.py /opt/fonts \
 && test -f /opt/fonts/Leipzig.ttf

# ---------------------------------------------------------------- runtime
FROM ${PYTHON_IMAGE} AS runtime
# Default STEMSTUDIO_DEVICE of the image: cpu (CPU variant) or auto (GPU variant).
ARG WORKER_DEVICE=cpu
# ffmpeg provides both ffmpeg and ffprobe on PATH; libcairo2 is cairosvg's native library and
# fontconfig makes the Leipzig font visible to Cairo (scripts/check-worker-fonts.* checks fc-list);
# fonts-dejavu-core (Bitstream Vera / public-domain licence) provides DejaVu Sans and DejaVu Sans
# Bold in /usr/share/fonts/truetype/dejavu for the setlist PDFs (feature 014, both variants).
# Handouts (feature 020, FR-012): WeasyPrint (pinned in worker/uv.lock) loads Pango and PangoFT2 at
# run time through cffi. libpango-1.0-0 and libpangoft2-1.0-0 (LGPL-2.1+; HarfBuzz and fontconfig
# come with them) are already pulled in by ffmpeg's dependencies (Pango 1.56.3, P0-4) but are
# listed explicitly so the PDF engine never depends on that. Handout fonts: the DejaVu fonts above
# and the Archivo copy inside the worker package; fc-cache refreshes every font folder.
# scripts/check-worker-pdf.* checks the result (SC-005, both variants).
COPY --from=deno /deno /usr/local/bin/deno
COPY --from=font /opt/fonts/Leipzig.ttf /usr/local/share/fonts/verovio/Leipzig.ttf
RUN apt-get update \
 && apt-get install -y --no-install-recommends ffmpeg libsndfile1 libcairo2 fontconfig fonts-dejavu-core \
      libpango-1.0-0 libpangoft2-1.0-0 \
 && rm -rf /var/lib/apt/lists/* \
 && fc-cache -f \
 && groupadd --gid 10001 stemstudio \
 && useradd --uid 10001 --gid stemstudio --no-log-init --create-home --home-dir /home/stemstudio stemstudio \
 && mkdir -p /data /models/hf /models/torch /models/whisper /gpu-lease \
 && chown -R stemstudio:stemstudio /data /models /gpu-lease
COPY --from=build --chown=stemstudio:stemstudio /app/.venv /app/.venv
# HOME must be writable for the non-root user: spotDL keeps ~/.spotdl, yt-dlp and deno their caches.
ENV HOME=/home/stemstudio \
    DENO_DIR=/home/stemstudio/.cache/deno \
    PATH="/app/.venv/bin:$PATH" \
    PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    STEMSTUDIO_DATA_DIR=/data \
    STEMSTUDIO_SEPARATOR=demucs \
    STEMSTUDIO_DEVICE=${WORKER_DEVICE} \
    HF_HOME=/models/hf \
    TORCH_HOME=/models/torch \
    STEMSTUDIO_MODEL_DIR=/models \
    NUMBA_CACHE_DIR=/tmp/numba-cache
USER stemstudio
WORKDIR /home/stemstudio
VOLUME ["/data", "/models"]
EXPOSE 8000
HEALTHCHECK --interval=10s --timeout=5s --start-period=60s --retries=6 \
  CMD ["python", "-c", "import sys, urllib.request; sys.exit(0 if urllib.request.urlopen('http://127.0.0.1:8000/health', timeout=4).status == 200 else 1)"]
CMD ["uvicorn", "--factory", "stemstudio_worker.main:create_app", "--host", "0.0.0.0", "--port", "8000"]
