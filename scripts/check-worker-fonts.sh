#!/usr/bin/env bash
# Worker font check (feature 008, SC-004; quickstart M2): fontconfig inside the worker image lists
# the Leipzig music font (Verovio's SVG text glyphs) and the engraving stack imports as the worker
# user (verovio, cairosvg with its native libcairo, music21). Throwaway containers without network.
# Used by the CI job cpu-image-guard. No secrets are read or needed.
#
# Usage: scripts/check-worker-fonts.sh [--build] [image]
#   image    the image to check (default stemstudio/worker:local)
#   --build  build the CPU variant of docker/worker.Dockerfile with that tag first
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMAGE=stemstudio/worker:local
BUILD=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --build) BUILD=1 ;;
    -h|--help) sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    -*) echo "Unknown option: $1" >&2; exit 2 ;;
    *) IMAGE="$1" ;;
  esac
  shift
done

if [[ $BUILD -eq 1 ]]; then
  docker build -f "$REPO_ROOT/docker/worker.Dockerfile" -t "$IMAGE" "$REPO_ROOT"
fi
echo "Checking $IMAGE for the Leipzig font and the engraving imports ..."
FAILED=0
if docker run --rm --network none --entrypoint fc-list "$IMAGE" : family | grep -qx 'Leipzig'; then
  echo "OK   fc-list lists Leipzig"
else
  echo "FAIL fc-list does not list Leipzig"
  FAILED=1
fi
if docker run --rm --network none --entrypoint python "$IMAGE" -I -c 'import verovio, cairosvg, music21'; then
  echo "OK   verovio, cairosvg and music21 import as the worker user"
else
  echo "FAIL verovio, cairosvg or music21 does not import"
  FAILED=1
fi
exit "$FAILED"
