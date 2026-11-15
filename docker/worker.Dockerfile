# syntax=docker/dockerfile:1
# StemStudio worker: FastAPI + yt-dlp/spotDL + Demucs (CPU torch) + librosa, plus the detail
# models' runtimes (onnxruntime CPU, rotary-embedding-torch). No model weights are baked in: they
# download at first use into /models (STEMSTUDIO_MODEL_DIR, HF_HOME, TORCH_HOME; a volume).
# Build context: repository root.  docker build -f docker/worker.Dockerfile .
#
# Two image variants from one Dockerfile (ADR-011, ADR-012 D1; research.md G-01):
#   CPU (default, CI): TORCH_VARIANT=cpu ORT_VARIANT=cpu installs exactly what worker/uv.lock pins
#     (torch 2.15.0+cpu, onnxruntime 1.32.0). Contains no NVIDIA runtime library; guarded by
#     scripts/check-cpu-image.{ps1,sh} (CI job cpu-image-guard).
#   GPU (optional, docker-compose.gpu.yml): TORCH_VARIANT=cu132 replaces torch with the CUDA 13.2
#     wheel of the same version; ORT_VARIANT=gpu replaces onnxruntime with onnxruntime-gpu 1.32.0
#     (no extras: it reuses torch's CUDA libraries via onnxruntime.preload_dlls()) plus
#     cuda-toolkit[curand] 13.2.2 (libcurand, which torch does not ship). About 11.6 GB on disk;
#     the NVIDIA CUDA runtime libraries are proprietary system dependencies of this variant only.
#     WORKER_DEVICE=auto makes the GPU image try the GPU by default (falls back to the CPU with a
#     reason in /health when no usable GPU is present).
#   GPU build by hand (scripts/gpu-smoke.* does this):
#     docker build -f docker/worker.Dockerfile --build-arg TORCH_VARIANT=cu132
#       --build-arg ORT_VARIANT=gpu --build-arg WORKER_DEVICE=auto -t stemstudio/worker:local-gpu .

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
      cu[0-9][0-9]*) uv pip install --python /app/.venv/bin/python --reinstall-package torch \
          "torch==2.15.0" --index-url "https://download.pytorch.org/whl/${TORCH_VARIANT}" ;; \
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

# ---------------------------------------------------------------- runtime
FROM ${PYTHON_IMAGE} AS runtime
# Default STEMSTUDIO_DEVICE of the image: cpu (CPU variant) or auto (GPU variant).
ARG WORKER_DEVICE=cpu
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
