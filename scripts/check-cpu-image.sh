#!/usr/bin/env bash
# CPU-image guard (feature 003, FR-008, ADR-011; quickstart G2; 009 T010): fails when the worker
# image holds any NVIDIA/CUDA runtime (nvidia-* except nvidia-ml-py, cuda-*, triton,
# onnxruntime-gpu) or a CUDA torch build; also fails on matplotlib (excluded by the lock, 008) and
# when torchaudio or stable_whisper (006) do not import. With --generator it checks the fake
# generator image (009/010) instead: no NVIDIA/CUDA runtime, no torch or model stack at all, and
# the generator package imports. The check itself is scripts/cpu_image_guard.py, run inside the image.
# Used by the CI jobs cpu-image-guard and generator-image. No secrets are read or needed.
#
# Usage: scripts/check-cpu-image.sh [--build] [--generator] [image]
#   image        the image to check (default stemstudio/worker:local,
#                with --generator stemstudio/generator:local-fake)
#   --build      build the image first (worker: CPU variant; generator: GENERATOR_VARIANT=fake)
#   --generator  check the fake generator image (guard profile generator)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMAGE=""
BUILD=0
PROFILE=worker

while [[ $# -gt 0 ]]; do
  case "$1" in
    --build) BUILD=1 ;;
    --generator) PROFILE=generator ;;
    -h|--help) sed -n '2,14p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    -*) echo "Unknown option: $1" >&2; exit 2 ;;
    *) IMAGE="$1" ;;
  esac
  shift
done

if [[ $PROFILE == generator ]]; then
  IMAGE="${IMAGE:-stemstudio/generator:local-fake}"
  BUILD_ARGS=(-f "$REPO_ROOT/docker/generator.Dockerfile" --build-arg GENERATOR_VARIANT=fake)
else
  IMAGE="${IMAGE:-stemstudio/worker:local}"
  BUILD_ARGS=(-f "$REPO_ROOT/docker/worker.Dockerfile")
fi
if [[ $BUILD -eq 1 ]]; then
  docker build "${BUILD_ARGS[@]}" -t "$IMAGE" "$REPO_ROOT"
fi
echo "Checking $IMAGE ($PROFILE profile) for NVIDIA/CUDA runtime libraries ..."
docker run --rm -i --network none --entrypoint python "$IMAGE" -I - "$PROFILE" < "$REPO_ROOT/scripts/cpu_image_guard.py"
