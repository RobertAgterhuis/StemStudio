#!/usr/bin/env bash
# API image size check (023 T069, ADR-037): the vendored lyric dictionaries in the api image
# (/app/LyricAssist/Dictionaries) stay below 15 MB; nothing is downloaded at run time.
# Read-only: `docker image inspect` and a throwaway `docker run --rm --entrypoint du`; the image is
# not changed. With --baseline-image (an api image built before the dictionaries existed) the whole
# image must also be less than 15 MB larger than that baseline (one-off measurement, not run in CI).
# Runs scripts/api_image_size_checks.py. Never point it at a running stack's images by accident:
# CI passes the image its own compose project built (stemstudio/api:local on the runner).
#
# Usage: scripts/check-api-image-size.sh [--image <tag>] [--baseline-image <tag>] [--budget-bytes <n>]
#   --image <tag>            the api image to measure (default: stemstudio/api:local)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARGS=()
HAS_IMAGE=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --image) HAS_IMAGE=1; ARGS+=("$1" "$2"); shift ;;
    --baseline-image|--budget-bytes) ARGS+=("$1" "$2"); shift ;;
    -h|--help) sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
  shift
done
[[ $HAS_IMAGE -eq 1 ]] || ARGS+=(--image stemstudio/api:local)

# First interpreter that really runs (Windows has a python3 store stub), else uv's Python.
PYTHON=(uv run --no-project python -I)
for candidate in python3 python; do
  if "$candidate" -c 'import sys; sys.exit(sys.version_info < (3, 10))' >/dev/null 2>&1; then
    PYTHON=("$candidate" -I)
    break
  fi
done

cd "$REPO_ROOT"
# MSYS_NO_PATHCONV: Git Bash on Windows would rewrite the /app path of the container command.
MSYS_NO_PATHCONV=1 "${PYTHON[@]}" scripts/api_image_size_checks.py "${ARGS[@]}"
