#!/usr/bin/env bash
# Runs the agent E2E specs (feature 011, T059; quickstart A5/A6) against a running fake stack.
# Only the api is recreated with the agent settings (--no-deps); worker and web keep running.
#
# Usage (from the repository root): scripts/agent-e2e.sh fake|none
#   fake  for every e2e/agent-scripts/<name>.json: api with STEMSTUDIO_AGENT_BACKEND=fake and
#         STEMSTUDIO_AGENT_FAKE_SCRIPT=/scripts/<name>.json, then the agent.spec.ts tests tagged
#         @<name>; finally the @agent tests without a script tag (fake backend, no script).
#         A caller-set STEMSTUDIO_AGENT_FAKE_SCRIPT runs every @agent test once with that script.
#   none  api with STEMSTUDIO_AGENT_BACKEND=none, then the @agent-unavailable tests (SC-005)
# @agent-unavailable and @generative tests never run in the fake passes (the generative ones need
# the generator overlay). Passes with a notice while e2e/tests/agent.spec.ts does not exist.
# The 023 lyric tests need docker-compose.e2e.yml (added with -f when COMPOSE_FILE is empty).
# Compose project: COMPOSE_PROJECT_NAME (default: the name in docker-compose.yml). Playwright reads
# E2E_BASE_URL / E2E_API_URL as for npm test. Exits with the first failing step's code, 2 on usage.
set -euo pipefail
# Git Bash on Windows would rewrite /scripts/<name>.json into a Windows path before compose sees it.
export MSYS_NO_PATHCONV=1

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SPEC=tests/agent.spec.ts
SCRIPTS_DIR="$REPO_ROOT/e2e/agent-scripts"
EXCLUDED='@agent-unavailable|@generative'
# The 023 lyric tests of agent.spec.ts need e2e/lyric-scripts/ mounted at /lyric-scripts: that is the e2e override
# (CI sets COMPOSE_FILE with it); without COMPOSE_FILE it is added with -f.
COMPOSE_OVERRIDE=()
if [[ -z "${COMPOSE_FILE:-}" ]]; then COMPOSE_OVERRIDE=(-f docker-compose.yml -f docker-compose.e2e.yml); fi

usage() {
  sed -n '5,12p' "$0" | sed 's/^# \{0,1\}//' >&2
  exit 2
}

# api_with <backend> [script-path-in-container]: recreates only the api and waits until healthy.
api_with() {
  echo "=== api: agent backend $1${2:+, script $2} ==="
  (cd "$REPO_ROOT" && STEMSTUDIO_AGENT_BACKEND="$1" STEMSTUDIO_AGENT_FAKE_SCRIPT="${2:-}" \
    docker compose ${COMPOSE_OVERRIDE[@]+"${COMPOSE_OVERRIDE[@]}"} up -d --no-deps --wait --wait-timeout 180 api)
}

# playwright <args...>: the agent spec in both browsers (retries 0 from playwright.config.ts).
playwright() {
  (cd "$REPO_ROOT/e2e" && npx playwright test --project=chromium "$SPEC" "$@")
}

# Script names (basename without .json), sorted, one per line.
script_names() {
  local file
  for file in "$SCRIPTS_DIR"/*.json; do
    if [[ -f "$file" ]]; then basename "$file" .json; fi
  done
}

run_scripted() {
  local name tags="$EXCLUDED"
  for name in $(script_names); do
    api_with fake "/scripts/$name.json"
    playwright --grep "@$name( |$)" --grep-invert "$EXCLUDED" --pass-with-no-tests
    tags+="|@$name( |$)"
  done
  api_with fake
  playwright --grep @agent --grep-invert "$tags" --pass-with-no-tests
}

run_fake() {
  if [[ -n "${STEMSTUDIO_AGENT_FAKE_SCRIPT:-}" ]]; then
    api_with fake "$STEMSTUDIO_AGENT_FAKE_SCRIPT"
    playwright --grep @agent --grep-invert "$EXCLUDED" --pass-with-no-tests
    return
  fi
  run_scripted
}

run_none() {
  api_with none
  playwright --grep @agent-unavailable
}

main() {
  [[ $# -eq 1 ]] || usage
  if [[ ! -f "$REPO_ROOT/e2e/$SPEC" ]]; then
    echo "::notice::e2e/$SPEC does not exist yet; agent E2E ($1) skipped"
    exit 0
  fi
  case "$1" in
    fake) run_fake ;;
    none) run_none ;;
    *) usage ;;
  esac
}

main "$@"
