#!/usr/bin/env bash
# Library mount check (feature 007, FR-012, ADR-016 D5): the running worker sees the API data
# volume read-only at /library. Checks STEMSTUDIO_LIBRARY_DIR=/library, /health
# library.readable: true and that a write inside /library as the worker user fails with
# "Read-only file system". The checks are scripts/library_mount_checks.py, run inside the worker.
# Used by the CI e2e job after the fake stack is up. No secrets are read or needed.
#
# Usage: scripts/check-library-mount.sh [-p <project>] [-f <compose file>]...
#   -p, --project <name>  compose project of the running stack (default: the compose file's name)
#   -f, --file <file>     compose file(s), as for docker compose (default: docker-compose.yml)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE=(docker compose)

while [[ $# -gt 0 ]]; do
  case "$1" in
    -p|--project) COMPOSE+=(-p "$2"); shift ;;
    -f|--file) COMPOSE+=(-f "$2"); shift ;;
    -h|--help) sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
  shift
done

echo "Checking the worker's read-only /library mount ..."
(cd "$REPO_ROOT" && "${COMPOSE[@]}" exec -T worker python -I - < scripts/library_mount_checks.py)
