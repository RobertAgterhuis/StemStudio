#!/usr/bin/env bash
# Generative smoke test (009 T010 / quickstart Q3, 010 T008): the GPU images of the generator
# (ACE-Step) and the singer (SoulX-Singer-SVC). Needs an NVIDIA GPU usable by Docker
# (docker run --gpus all). The checks are scripts/generative_smoke_checks.py, run inside the images.
# No secrets are read or needed.
#
# Stack mode (default): starts (unless --no-up) the GPU stack
#   docker-compose.yml + docker-compose.gpu.yml + docker-compose.generative.yml
# and checks in the running generator and singer: torch 2.10.0+cu128, sm_<cc> in the arch list,
# /health ready with their kinds, one 10 s restyle and one 10 s voice conversion (skip with
# --skip-jobs). The stack keeps running afterwards.
#
# Image mode (--images): throwaway containers only (never touches a compose stack): builds both
# images (unless --no-build), then with --gpus all checks torch/CUDA, the engine imports and the
# pre-downloaded weights (REVISION, SHA256SUMS, listed files) in the model volume mounted read-only.
#
# Usage: scripts/generative-smoke.sh [--images] [--no-build] [--no-up] [--skip-jobs]
#          [--project <name>] [--model-volume <name>]
#   --project       compose project name (stack mode; default: compose's own default)
#   --model-volume  model volume for image mode (default stemstudio_model-cache)
# Image tags: STEMSTUDIO_GENERATOR_IMAGE (default stemstudio/generator:local),
#             STEMSTUDIO_SINGER_IMAGE (default stemstudio/singer:local).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CHECKS="$REPO_ROOT/scripts/generative_smoke_checks.py"
GENERATOR_IMAGE="${STEMSTUDIO_GENERATOR_IMAGE:-stemstudio/generator:local}"
SINGER_IMAGE="${STEMSTUDIO_SINGER_IMAGE:-stemstudio/singer:local}"
MODEL_VOLUME=stemstudio_model-cache
IMAGES=0
BUILD=1
UP=1
JOBS=1
COMPOSE=(docker compose -f "$REPO_ROOT/docker-compose.yml" -f "$REPO_ROOT/docker-compose.gpu.yml"
  -f "$REPO_ROOT/docker-compose.generative.yml")

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --images) IMAGES=1 ;;
      --no-build) BUILD=0 ;;
      --no-up) UP=0 ;;
      --skip-jobs) JOBS=0 ;;
      --project) COMPOSE+=(-p "$2"); shift ;;
      --model-volume) MODEL_VOLUME="$2"; shift ;;
      -h|--help) sed -n '2,22p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
      *) echo "Unknown option: $1" >&2; exit 2 ;;
    esac
    shift
  done
}

# in_image <image> <mode> [arg]: run one check mode in a throwaway GPU container.
in_image() {
  local image="$1"
  shift
  echo "--- $image: $*"
  docker run --rm -i --gpus all --network none -v "$MODEL_VOLUME:/models:ro" \
    --entrypoint python "$image" -I - "$@" < "$CHECKS"
}

# in_service <service> <mode> [arg]: run one check mode in a running compose service.
in_service() {
  local service="$1"
  shift
  echo "--- $service: $*"
  "${COMPOSE[@]}" exec -T "$service" python -I - "$@" < "$CHECKS"
}

image_mode() {
  if [[ $BUILD -eq 1 ]]; then
    docker build -f "$REPO_ROOT/docker/generator.Dockerfile" --build-arg GENERATOR_VARIANT=acestep \
      -t "$GENERATOR_IMAGE" "$REPO_ROOT"
    docker build -f "$REPO_ROOT/docker/generator.Dockerfile" --build-arg GENERATOR_VARIANT=soulx \
      -t "$SINGER_IMAGE" "$REPO_ROOT"
  fi
  nvidia-smi --query-gpu=name,memory.used,memory.total,driver_version --format=csv
  for mode in runtime "imports acestep" "models acestep"; do
    # shellcheck disable=SC2086 # mode is "name [arg]" on purpose
    in_image "$GENERATOR_IMAGE" $mode
  done
  for mode in runtime "imports soulx" "models soulx"; do
    # shellcheck disable=SC2086
    in_image "$SINGER_IMAGE" $mode
  done
}

stack_mode() {
  if [[ $UP -eq 1 ]]; then "${COMPOSE[@]}" up -d --build --wait --wait-timeout 900; fi
  in_service generator runtime
  in_service generator health restyle
  in_service singer runtime
  in_service singer health voiceConversion
  if [[ $JOBS -eq 1 ]]; then
    in_service generator restyle
    in_service singer convert
  fi
}

main() {
  parse_args "$@"
  if [[ $IMAGES -eq 1 ]]; then image_mode; else stack_mode; fi
  echo "Generative smoke test passed"
}

main "$@"
