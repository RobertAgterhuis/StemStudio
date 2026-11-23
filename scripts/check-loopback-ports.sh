#!/usr/bin/env bash
# Loopback ports check (refactor 004, 003 step 4, ADR-027 D3, threat T7): with
# STEMSTUDIO_BIND_ADDRESS unset, every published port of api and web binds to 127.0.0.1 and no
# other service publishes a port; with STEMSTUDIO_BIND_ADDRESS=0.0.0.0 the opt-in is honoured.
# Static: reads `docker compose config --format json` (no container is started, stopped or
# changed) and runs scripts/loopback_ports_checks.py twice. A STEMSTUDIO_BIND_ADDRESS in a local
# .env is read by compose, so the default check then fails (intended: the file opens the ports).
#
# Usage: scripts/check-loopback-ports.sh [-f <compose file>]...
#   -f, --file <file>  compose file(s), as for docker compose (default: docker-compose.yml)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FILES=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    -f|--file) FILES+=("$2"); shift ;;
    -h|--help) sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
  shift
done
[[ ${#FILES[@]} -gt 0 ]] || FILES=(docker-compose.yml)

COMPOSE=(docker compose)
for file in "${FILES[@]}"; do COMPOSE+=(-f "$file"); done
# First interpreter that really runs (Windows has a python3 store stub), else uv's Python.
PYTHON=(uv run --no-project python -I)
for candidate in python3 python; do
  if "$candidate" -c 'import sys; sys.exit(sys.version_info < (3, 10))' >/dev/null 2>&1; then
    PYTHON=("$candidate" -I)
    break
  fi
done

# check <expected host address> [VAR=value]: one compose resolution, one checker run.
check() {
  local expected="$1"; shift
  echo "Checking the published ports (${FILES[*]}; ${*:-STEMSTUDIO_BIND_ADDRESS unset}) ..."
  env -u STEMSTUDIO_BIND_ADDRESS "$@" "${COMPOSE[@]}" config --format json |
    "${PYTHON[@]}" scripts/loopback_ports_checks.py --config - --expect "$expected"
}

cd "$REPO_ROOT"
status=0
check 127.0.0.1 || status=1
check 0.0.0.0 STEMSTUDIO_BIND_ADDRESS=0.0.0.0 || status=1
exit "$status"
