#!/usr/bin/env bash
# Backups folder check (feature 013, T011; FR-005): the host folder the api writes backups into
# (STEMSTUDIO_BACKUP_HOST_DIR, default ./backups next to docker-compose.yml, bind-mounted at
# /backups) exists, is writable for the api user (uid 1654 on Linux hosts; the current user on
# Docker Desktop) and has free space. Run it before `docker compose up`; the api never changes
# host permissions. Host-side only: no container is started. The checks are
# scripts/backup_folder_checks.py. Linux: mkdir -p backups && sudo chown 1654:1654 backups
#
# Usage: scripts/check-backup-folder.sh [--folder <path>] [--min-free-bytes <n>]
#   --folder <path>          folder to check (default: $STEMSTUDIO_BACKUP_HOST_DIR or ./backups)
#   --min-free-bytes <n>     minimum free bytes (default 268435456 = 256 MiB)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  sed -n '2,11p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
fi

# First interpreter that really runs (Windows has a python3 store stub), else uv's Python.
PYTHON=(uv run --no-project python -I)
for candidate in python3 python; do
  if "$candidate" -c 'import sys; sys.exit(sys.version_info < (3, 10))' >/dev/null 2>&1; then
    PYTHON=("$candidate" -I)
    break
  fi
done

echo "Checking the backups folder ..."
"${PYTHON[@]}" "$REPO_ROOT/scripts/backup_folder_checks.py" --repo-root "$REPO_ROOT" "$@"
