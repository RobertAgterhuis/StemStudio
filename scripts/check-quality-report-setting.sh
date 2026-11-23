#!/usr/bin/env bash
# Quality report setting check (feature 020, owner decision 2026-11-23: gate G1 failed): api and
# web both get StemStudio__QualityReport__Enabled, false in docker-compose.yml (the default) and
# true with the e2e override (docker-compose.e2e.yml: E2E, visual and a11y stacks); no other
# service gets it. Static: reads `docker compose config` (no container is started, stopped or
# changed) and runs scripts/quality_report_setting_checks.py. The resolved config is piped, never
# printed. No secrets needed.
#
# Usage: scripts/check-quality-report-setting.sh --expect true|false [-f <compose file>]...
#   --expect <value>   the value api and web must have
#   -f, --file <file>  compose file(s), as for docker compose (default: docker-compose.yml)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FILES=()
EXPECT=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --expect) EXPECT="$2"; shift ;;
    -f|--file) FILES+=("$2"); shift ;;
    -h|--help) sed -n '2,11p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
  shift
done
[[ -n "$EXPECT" ]] || { echo "--expect true|false is required" >&2; exit 2; }
[[ ${#FILES[@]} -gt 0 ]] || FILES=(docker-compose.yml)

COMPOSE=(docker compose)
for file in "${FILES[@]}"; do
  COMPOSE+=(-f "$file")
done
# First interpreter that really runs (Windows has a python3 store stub), else uv's Python.
PYTHON=(uv run --no-project python -I)
for candidate in python3 python; do
  if "$candidate" -c 'import sys; sys.exit(sys.version_info < (3, 10))' >/dev/null 2>&1; then
    PYTHON=("$candidate" -I)
    break
  fi
done

echo "Checking the quality report setting (${FILES[*]}, expect $EXPECT) ..."
cd "$REPO_ROOT"
"${COMPOSE[@]}" config --format json | "${PYTHON[@]}" scripts/quality_report_setting_checks.py --config - --expect "$EXPECT"
