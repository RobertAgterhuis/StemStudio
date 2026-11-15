#!/usr/bin/env bash
# CPU-image guard (feature 003, FR-008, ADR-011; quickstart G2): fails when the worker image holds
# any NVIDIA/CUDA runtime (nvidia-* except nvidia-ml-py, cuda-*, triton, onnxruntime-gpu) or a
# CUDA torch build; also fails on matplotlib (excluded by the lock, 008) and when torchaudio or
# stable_whisper (006) do not import. The check itself is scripts/cpu_image_guard.py, run inside
# the image.
# Used by the CI job cpu-image-guard. No secrets are read or needed.
#
# Usage: scripts/check-cpu-image.sh [--build] [image]
#   image    the image to check (default stemstudio/worker:local)
#   --build  build the CPU variant of docker/worker.Dockerfile with that tag first
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMAGE=stemstudio/worker:local
BUILD=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --build) BUILD=1 ;;
    -h|--help) sed -n '2,11p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    -*) echo "Unknown option: $1" >&2; exit 2 ;;
    *) IMAGE="$1" ;;
  esac
  shift
done

if [[ $BUILD -eq 1 ]]; then
  docker build -f "$REPO_ROOT/docker/worker.Dockerfile" -t "$IMAGE" "$REPO_ROOT"
fi
echo "Checking $IMAGE for NVIDIA/CUDA runtime libraries ..."
docker run --rm -i --network none --entrypoint python "$IMAGE" -I - < "$REPO_ROOT/scripts/cpu_image_guard.py"
