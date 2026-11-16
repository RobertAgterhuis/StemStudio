#!/usr/bin/env bash
# Key volume check (011 follow-up claude-key, T019/T020; CK-FR-003, CK-FR-005): the Data
# Protection key ring of the stored Claude key is the named volume api-keys at /keys, mounted
# only into api; api has StemStudio__DataProtection__KeyRingDir=/keys and
# StemStudio__Agent__KeyEntry__InternalCaller=web; docker/api.Dockerfile creates /keys 0700 for
# $APP_UID and declares VOLUME /keys; ANTHROPIC_API_KEY stays a bare pass-through; the non-Docker
# key ring of scripts/dev.sh (StemStudio__DataProtection__KeyRingDir or the per-user default) is
# outside the repository and data/ (T050).
# Static: reads `docker compose config` (no container is started, stopped or changed) and runs
# scripts/key_volume_checks.py. The resolved config is piped, never printed. No secrets needed.
#
# Usage: scripts/check-key-volume.sh [-f <compose file>]...
#   -f, --file <file>  compose file(s), as for docker compose (default: docker-compose.yml)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FILES=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    -f|--file) FILES+=("$2"); shift ;;
    -h|--help) sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
  shift
done
[[ ${#FILES[@]} -gt 0 ]] || FILES=(docker-compose.yml)

COMPOSE=(docker compose)
CHECK=(--dockerfile docker/api.Dockerfile --repo-root .)
for file in "${FILES[@]}"; do
  COMPOSE+=(-f "$file")
  CHECK+=(--compose-file "$file")
done
# First interpreter that really runs (Windows has a python3 store stub), else uv's Python.
PYTHON=(uv run --no-project python -I)
for candidate in python3 python; do
  if "$candidate" -c 'import sys; sys.exit(sys.version_info < (3, 10))' >/dev/null 2>&1; then
    PYTHON=("$candidate" -I)
    break
  fi
done

echo "Checking the api key ring volume (${FILES[*]}) ..."
cd "$REPO_ROOT"
"${COMPOSE[@]}" config --format json | "${PYTHON[@]}" scripts/key_volume_checks.py --config - "${CHECK[@]}"
