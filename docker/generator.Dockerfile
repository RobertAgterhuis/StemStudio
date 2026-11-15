# syntax=docker/dockerfile:1
# StemStudio generator service (features 009/010, ADR-018, ADR-019): FastAPI job API around
# ACE-Step 1.5 (style transfer, songs) or SoulX-Singer-SVC (own-voice conversion). GPU-only and
# opt-in (constitution 1.3.0); the default stack never builds or starts it. No model weights are
# baked in: they live in the shared model-cache volume at /models (STEMSTUDIO_MODEL_DIR).
# Build context: repository root.
#
# Three image variants from one Dockerfile (build arg GENERATOR_VARIANT):
#   fake     base install of generator/uv.lock only (no torch, no NVIDIA wheels, OSI licences only):
#            CI, E2E and the fake stack (docker-compose.generative-fake.yml); guarded by
#            scripts/check-cpu-image.{sh,ps1} --generator.
#              docker build -f docker/generator.Dockerfile --build-arg GENERATOR_VARIANT=fake -t stemstudio/generator:local-fake .
#   acestep  + extra `acestep` (ACE-Step 1.5 @ ca1e85f, torch 2.10.0+cu128): service `generator`,
#            image stemstudio/generator:local (docker-compose.generative.yml).
#   soulx    + extra `soulx` (torch 2.10.0+cu128, transformers 4.41.2) and the SoulX-Singer source
#            at commit 81aeb3a in /opt/soulx-singer on PYTHONPATH (ADR-019 Phase 0 outcome):
#            service `singer`, image stemstudio/singer:local.
# The GPU variants contain the proprietary NVIDIA CUDA runtime wheels (system dependency, ADR-011).
#
# Size: the venv is resolved in the build stage with a uv cache mount (the cache never enters a
# layer) and copied into the runtime stage once, already owned by the runtime user (no second
# chown layer). Only the variant's own stages are built (FROM variant-${GENERATOR_VARIANT}); an
# unknown variant therefore fails there with "pull access denied" for image variant-<name>.

ARG PYTHON_IMAGE=python:3.12-slim
ARG UV_IMAGE=ghcr.io/astral-sh/uv:0.13.5
ARG GENERATOR_VARIANT=acestep

FROM ${UV_IMAGE} AS uv

# ---------------------------------------------------------------- build: resolve the venv
FROM ${PYTHON_IMAGE} AS build
COPY --from=uv /uv /usr/local/bin/uv
# git: uv fetches the ACE-Step and nano-vllm git pins (acestep variant); build stage only.
RUN --mount=type=cache,target=/var/cache/apt,sharing=locked \
    --mount=type=cache,target=/var/lib/apt,sharing=locked \
    rm -f /etc/apt/apt.conf.d/docker-clean \
 && apt-get update \
 && apt-get install -y --no-install-recommends git ca-certificates
ENV UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy \
    UV_PYTHON_DOWNLOADS=never \
    UV_PROJECT_ENVIRONMENT=/app/.venv
WORKDIR /app
ARG GENERATOR_VARIANT
# Dependencies first (layer cached until the lock changes), then the project itself.
COPY generator/pyproject.toml generator/uv.lock generator/README.md ./
# The variant's extra: none for fake, --extra acestep / --extra soulx otherwise.
RUN case "$GENERATOR_VARIANT" in \
      fake|acestep|soulx) ;; \
      *) echo "GENERATOR_VARIANT must be fake, acestep or soulx, got '$GENERATOR_VARIANT'" >&2; exit 1 ;; \
    esac
RUN --mount=type=cache,target=/root/.cache/uv \
    uv sync --frozen --no-dev --no-install-project \
      $([ "$GENERATOR_VARIANT" = fake ] || echo --extra "$GENERATOR_VARIANT")
COPY generator/src ./src
RUN --mount=type=cache,target=/root/.cache/uv \
    uv sync --frozen --no-dev --no-editable \
      $([ "$GENERATOR_VARIANT" = fake ] || echo --extra "$GENERATOR_VARIANT")

# ---------------------------------------------------------------- SoulX-Singer source (soulx only)
# Fetched by BuildKit at the pinned commit (the commit hash is the integrity check); only the SVC
# package and the self-contained RMVPE file are kept. Nothing of SoulX is in the repository.
FROM ${PYTHON_IMAGE} AS soulx-source
ARG SOULX_COMMIT=81aeb3ae772c70093c3de74dc23c92d983801ae4
ADD --keep-git-dir=false https://github.com/Soul-AILab/SoulX-Singer.git#${SOULX_COMMIT} /src/soulx
RUN mkdir -p /opt/soulx-singer/preprocess/tools \
 && cp -r /src/soulx/soulxsinger /opt/soulx-singer/soulxsinger \
 && cp /src/soulx/preprocess/tools/f0_extraction.py /opt/soulx-singer/preprocess/tools/f0_extraction.py \
 && cp /src/soulx/LICENSE /opt/soulx-singer/LICENSE \
 && echo "${SOULX_COMMIT}" > /opt/soulx-singer/COMMIT \
 && find /opt/soulx-singer -name '__pycache__' -prune -exec rm -rf {} +

# ---------------------------------------------------------------- runtime (shared by all variants)
FROM ${PYTHON_IMAGE} AS runtime
# ffmpeg (decode/encode, torchcodec's FFmpeg libraries) and libsndfile (soundfile).
RUN apt-get update \
 && apt-get install -y --no-install-recommends ffmpeg libsndfile1 \
 && rm -rf /var/lib/apt/lists/* \
 && groupadd --gid 10001 stemstudio \
 && useradd --uid 10001 --gid stemstudio --no-log-init --create-home --home-dir /home/stemstudio stemstudio \
 && mkdir -p /data /models /gpu-lease \
 && chown stemstudio:stemstudio /data /models /gpu-lease
COPY --from=build --chown=stemstudio:stemstudio /app/.venv /app/.venv
ENV HOME=/home/stemstudio \
    PATH="/app/.venv/bin:$PATH" \
    PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    STEMSTUDIO_DATA_DIR=/data \
    STEMSTUDIO_MODEL_DIR=/models \
    STEMSTUDIO_GPU_LEASE_FILE=/gpu-lease/gpu.lock \
    HF_HOME=/models/hf \
    HF_HUB_DISABLE_TELEMETRY=1 \
    NUMBA_CACHE_DIR=/tmp/numba-cache
USER stemstudio
WORKDIR /home/stemstudio
EXPOSE 8010
HEALTHCHECK --interval=10s --timeout=5s --start-period=60s --retries=6 \
  CMD ["python", "-c", "import sys, urllib.request; sys.exit(0 if urllib.request.urlopen('http://127.0.0.1:8010/health', timeout=4).status == 200 else 1)"]
CMD ["uvicorn", "--factory", "stemstudio_generator.main:create_app", "--host", "0.0.0.0", "--port", "8010"]

# ---------------------------------------------------------------- variants
FROM runtime AS variant-fake
ENV STEMSTUDIO_GENERATOR=fake \
    STEMSTUDIO_GPU_LEASE_FILE=

FROM runtime AS variant-acestep
ENV STEMSTUDIO_GENERATOR=acestep

FROM runtime AS variant-soulx
COPY --from=soulx-source /opt/soulx-singer /opt/soulx-singer
ENV STEMSTUDIO_GENERATOR=soulx \
    STEMSTUDIO_GENERATOR_KINDS=voiceConversion \
    PYTHONPATH=/opt/soulx-singer

FROM variant-${GENERATOR_VARIANT} AS final
