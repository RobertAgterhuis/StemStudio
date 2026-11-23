#!/usr/bin/env bash
# Worker PDF engine check (feature 020, FR-012, SC-005): inside the worker image, as the worker
# user, WeasyPrint imports and reports its version, the Pango library it loads is 1.44 or newer
# (WeasyPrint's minimum) and fontconfig lists DejaVu Sans (the handout body font fallback).
# Throwaway containers without network. Used by the CI job cpu-image-guard.
# No secrets are read or needed.
#
# Usage: scripts/check-worker-pdf.sh [--build] [image]
#   image    the image to check (default stemstudio/worker:local)
#   --build  build the CPU variant of docker/worker.Dockerfile with that tag first
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMAGE=stemstudio/worker:local
BUILD=0
PANGO_MIN=1.44

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
echo "Checking $IMAGE for WeasyPrint, Pango >= $PANGO_MIN and DejaVu Sans ..."
FAILED=0
# pango_version() is the version of the loaded library as major * 10000 + minor * 100 + micro.
PROBE='import weasyprint
from weasyprint.text.ffi import pango
v = pango.pango_version()
print(weasyprint.__version__, f"{v // 10000}.{v // 100 % 100}.{v % 100}")'
if VERSIONS="$(docker run --rm --network none --entrypoint python "$IMAGE" -I -c "$PROBE" 2>/dev/null)"; then
  VERSIONS="${VERSIONS//$'\r'/}"
  WEASY="${VERSIONS%% *}"
  PANGO="${VERSIONS##* }"
  echo "OK   weasyprint $WEASY imports as the worker user"
  if [[ "$(printf '%s\n%s\n' "$PANGO_MIN" "$PANGO" | sort -V | head -n1)" == "$PANGO_MIN" ]]; then
    echo "OK   Pango $PANGO >= $PANGO_MIN"
  else
    echo "FAIL Pango $PANGO is older than $PANGO_MIN (WeasyPrint needs $PANGO_MIN or newer)"
    FAILED=1
  fi
else
  echo "FAIL weasyprint does not import as the worker user (not installed, or Pango/HarfBuzz missing)"
  echo "FAIL Pango version unknown (needs $PANGO_MIN or newer)"
  FAILED=1
fi
FAMILIES="$(docker run --rm --network none --entrypoint fc-list "$IMAGE" : family || true)"
if grep -qi 'DejaVu Sans' <<<"$FAMILIES"; then
  echo "OK   fc-list lists DejaVu Sans"
else
  echo "FAIL fc-list does not list DejaVu Sans"
  FAILED=1
fi
exit "$FAILED"
