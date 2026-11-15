#!/usr/bin/env bash
# Licence report of built images (009 T083 / 010 T061 input for docs/THIRD-PARTY-LICENSES.md):
# one Markdown table per image of every Python distribution in it (name, version, licence, URL),
# read from the package metadata by scripts/image_licences.py inside a throwaway container without
# network. Used for the GPU images (generator, singer), whose extras CI never installs.
# No secrets are read or needed.
#
# Usage: scripts/image-licences.sh [--out <dir>] [image ...]
#   image   default: stemstudio/generator:local stemstudio/singer:local
#   --out   folder for <repository>-<tag>.md files (default licence-report)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT=licence-report
IMAGES=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --out) OUT="$2"; shift ;;
    -h|--help) sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    -*) echo "Unknown option: $1" >&2; exit 2 ;;
    *) IMAGES+=("$1") ;;
  esac
  shift
done
if [[ ${#IMAGES[@]} -eq 0 ]]; then IMAGES=(stemstudio/generator:local stemstudio/singer:local); fi

mkdir -p "$OUT"
for image in "${IMAGES[@]}"; do
  file="$OUT/$(echo "$image" | tr '/:' '--').md"
  { echo "# Python licences in $image"; echo; } > "$file"
  docker run --rm -i --network none --entrypoint python "$image" -I - \
    < "$REPO_ROOT/scripts/image_licences.py" >> "$file"
  echo "$image -> $file ($(tail -1 "$file"))"
done
