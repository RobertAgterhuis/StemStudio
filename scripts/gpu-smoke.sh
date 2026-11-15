#!/usr/bin/env bash
# GPU smoke test of the GPU worker image (feature 003, FR-009, FR-003; quickstart G3/G4).
# Needs an NVIDIA GPU usable by Docker (docker run --gpus all). Never touches the compose stack:
# it uses throwaway containers that are removed afterwards. No secrets are read or needed.
#   1. builds the GPU variant as <image> (unless --no-build)
#   2. with --gpus all: torch +cu132, sm_<cc> in the arch list, matmul, onnxruntime CUDA EP
#      after preload_dlls(), and /health deviceInfo.kind == gpu
#   3. without --gpus: the worker still starts, /health deviceInfo.kind == cpu with a reason
# The checks are scripts/gpu_smoke_checks.py, run inside the image.
#
# Usage: scripts/gpu-smoke.sh [--no-build] [image]   (default image stemstudio/worker:local-gpu)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CHECKS="$REPO_ROOT/scripts/gpu_smoke_checks.py"
IMAGE=stemstudio/worker:local-gpu
BUILD=1
CONTAINER="stemstudio-gpu-smoke-$$"

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --no-build) BUILD=0 ;;
      -h|--help) sed -n '2,11p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
      -*) echo "Unknown option: $1" >&2; exit 2 ;;
      *) IMAGE="$1" ;;
    esac
    shift
  done
}

build_image() {
  docker build -f "$REPO_ROOT/docker/worker.Dockerfile" --build-arg TORCH_VARIANT=cu132 \
    --build-arg ORT_VARIANT=gpu --build-arg WORKER_DEVICE=auto -t "$IMAGE" "$REPO_ROOT"
}

cleanup() {
  docker rm -f -v "$CONTAINER" >/dev/null 2>&1 || true
}

# check_health <gpu|cpu> [docker run options...]: start the worker, check /health, remove it.
check_health() {
  local expected="$1" status=0
  shift
  echo "--- /health, expecting deviceInfo.kind == $expected"
  docker run -d --name "$CONTAINER" --network none -e STEMSTUDIO_DEVICE=auto "$@" "$IMAGE" >/dev/null
  docker exec -i "$CONTAINER" python -I - health "$expected" < "$CHECKS" || status=1
  if [[ $status -ne 0 ]]; then docker logs --tail 50 "$CONTAINER" >&2 || true; fi
  cleanup
  return "$status"
}

main() {
  parse_args "$@"
  trap cleanup EXIT
  if [[ $BUILD -eq 1 ]]; then build_image; fi
  echo "--- runtime checks in $IMAGE with --gpus all"
  docker run --rm -i --gpus all --network none --entrypoint python "$IMAGE" -I - runtime < "$CHECKS"
  check_health gpu --gpus all
  check_health cpu
  echo "GPU smoke test passed for $IMAGE"
}

main "$@"
