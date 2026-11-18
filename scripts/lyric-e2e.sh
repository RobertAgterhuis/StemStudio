#!/usr/bin/env bash
# Runs the lyric assistant E2E passes (feature 023, T069/T070; quickstart Q1-Q6) against a running fake stack.
# Same pattern as scripts/agent-e2e.sh: only the api is recreated (--no-deps); worker and web keep running.
#
# Usage (from the repository root): scripts/lyric-e2e.sh fake|none
#   fake  api with STEMSTUDIO_AGENT_BACKEND=fake and STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT=/lyric-scripts
#         (e2e/lyric-scripts/, mounted read-only by docker-compose.e2e.yml), then `npm run test:lyric-assist`
#         (@lyric-assist; the @lyric-assist-none and @lyric-assist-claude tests never run in this pass)
#   none  api with STEMSTUDIO_AGENT_BACKEND=none, then `npm run test:lyric-assist-none` (Q6: no assistant, no outbound request)
# The Claude double has no variable in the contracts; `npm run test:lyric-assist-claude` self-skips and is not run here.
# The compose override docker-compose.e2e.yml carries the mount: it is added with -f when COMPOSE_FILE is empty (CI sets
# COMPOSE_FILE with it). Compose project: COMPOSE_PROJECT_NAME. Passes with a notice while e2e/package.json has no
# test:lyric-assist script. Exits with the first failing step's code, 2 on usage.
set -euo pipefail
# Git Bash on Windows would rewrite /lyric-scripts into a Windows path before compose sees it.
export MSYS_NO_PATHCONV=1

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LYRIC_FOLDER=/lyric-scripts

usage() {
  sed -n '5,12p' "$0" | sed 's/^# \{0,1\}//' >&2
  exit 2
}

# compose <args...>: the e2e override is part of the file list (the lyric scripts are mounted there).
compose() {
  local files=()
  case "${COMPOSE_FILE:-}" in
    "") files=(-f docker-compose.yml -f docker-compose.e2e.yml) ;;
    *docker-compose.e2e.yml*) ;;
    *) echo "::warning::COMPOSE_FILE lacks docker-compose.e2e.yml: /lyric-scripts is not mounted" >&2 ;;
  esac
  (cd "$REPO_ROOT" && docker compose ${files[@]+"${files[@]}"} "$@")
}

# api_with <backend> <lyric-script-folder-or-empty>: recreates only the api and waits until healthy.
api_with() {
  echo "=== api: agent backend $1${2:+, lyric scripts $2} ==="
  STEMSTUDIO_AGENT_BACKEND="$1" STEMSTUDIO_AGENT_FAKE_SCRIPT="" STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT="${2:-$LYRIC_FOLDER}" \
    compose up -d --no-deps --wait --wait-timeout 180 api
}

run_npm() {
  (cd "$REPO_ROOT/e2e" && npm run "$1")
}

main() {
  [[ $# -eq 1 ]] || usage
  if ! grep -q '"test:lyric-assist"' "$REPO_ROOT/e2e/package.json"; then
    echo "::notice::e2e/package.json has no test:lyric-assist script yet; lyric E2E ($1) skipped"
    exit 0
  fi
  case "$1" in
    fake) api_with fake "$LYRIC_FOLDER"; run_npm test:lyric-assist ;;
    none) api_with none; run_npm test:lyric-assist-none ;;
    *) usage ;;
  esac
}

main "$@"
