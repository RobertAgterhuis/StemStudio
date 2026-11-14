# syntax=docker/dockerfile:1
# StemStudio worker: FastAPI + yt-dlp/spotDL + Demucs (CPU torch) + librosa.
# Build context: repository root.  docker build -f docker/worker.Dockerfile .
#
# TORCH_VARIANT=cpu (default) installs exactly what worker/uv.lock pins (torch 2.15.0+cpu).
# Any other value (e.g. cu128) replaces torch with the CUDA wheel of the same version after the
# frozen sync; used only by docker-compose.gpu.yml.

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
    if [ "$TORCH_VARIANT" != "cpu" ]; then \
      uv pip install --python /app/.venv/bin/python --reinstall-package torch \
        "torch==2.15.0" --index-url "https://download.pytorch.org/whl/${TORCH_VARIANT}"; \
    fi

# ---------------------------------------------------------------- runtime
FROM ${PYTHON_IMAGE} AS runtime
# ffmpeg provides both ffmpeg and ffprobe on PATH.
COPY --from=deno /deno /usr/local/bin/deno
RUN apt-get update \
 && apt-get install -y --no-install-recommends ffmpeg libsndfile1 \
 && rm -rf /var/lib/apt/lists/* \
 && groupadd --gid 10001 stemstudio \
 && useradd --uid 10001 --gid stemstudio --no-log-init --create-home --home-dir /home/stemstudio stemstudio \
 && mkdir -p /data /models/hf /models/torch \
 && chown -R stemstudio:stemstudio /data /models
COPY --from=build --chown=stemstudio:stemstudio /app/.venv /app/.venv
# HOME must be writable for the non-root user: spotDL keeps ~/.spotdl, yt-dlp and deno their caches.
ENV HOME=/home/stemstudio \
    DENO_DIR=/home/stemstudio/.cache/deno \
    PATH="/app/.venv/bin:$PATH" \
    PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    STEMSTUDIO_DATA_DIR=/data \
    STEMSTUDIO_SEPARATOR=demucs \
    STEMSTUDIO_DEVICE=cpu \
    HF_HOME=/models/hf \
    TORCH_HOME=/models/torch \
    NUMBA_CACHE_DIR=/tmp/numba-cache
USER stemstudio
WORKDIR /home/stemstudio
VOLUME ["/data", "/models"]
EXPOSE 8000
HEALTHCHECK --interval=10s --timeout=5s --start-period=60s --retries=6 \
  CMD ["python", "-c", "import sys, urllib.request; sys.exit(0 if urllib.request.urlopen('http://127.0.0.1:8000/health', timeout=4).status == 200 else 1)"]
CMD ["uvicorn", "--factory", "stemstudio_worker.main:create_app", "--host", "0.0.0.0", "--port", "8000"]
