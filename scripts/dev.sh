#!/usr/bin/env bash
# Runs StemStudio without Docker: worker (uv, :8000), API (dotnet run, :8081), web (dotnet run, :8080).
# Builds the audio-engine and web-interop bundles first; data lives under <repo>/data. Ctrl+C stops all three.
# No secrets are needed; the optional Claude key (feature 011) is entered in Settings (stored
# encrypted; key ring printed at start, per user, outside the repo) or inherited from your shell
# (export ANTHROPIC_API_KEY for this session only) and never written anywhere by this script.
#
# Usage: scripts/dev.sh [--fake] [--skip-engine-build] [--smoke-test] [--timeout <seconds>]
#   --fake               STEMSTUDIO_SEPARATOR=fake (stems are copies of the mix; detail faked; no download)
#   --skip-engine-build  reuse the existing wwwroot/js bundles (audio-engine, web-interop)
#   --smoke-test         wait for the three /health endpoints, print them, stop; exit 0 when all are 200
#   --timeout <s>        how long to wait per /health endpoint (default 180)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FAKE=0
BUILD_ENGINE=1
SMOKE_TEST=0
TIMEOUT=180
PIDS=()
HEALTH_URLS=(http://localhost:8000/health http://localhost:8081/health http://localhost:8080/health)

usage() {
  sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'
}

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --fake) FAKE=1 ;;
      --skip-engine-build) BUILD_ENGINE=0 ;;
      --smoke-test) SMOKE_TEST=1 ;;
      --timeout) TIMEOUT="$2"; shift ;;
      -h|--help) usage; exit 0 ;;
      *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
  done
}

init_environment() {
  local data_dir="$REPO_ROOT/data"
  mkdir -p "$data_dir/worker" "$data_dir/api"
  export STEMSTUDIO_DATA_DIR="$data_dir/worker"
  if [[ $FAKE -eq 1 ]]; then export STEMSTUDIO_SEPARATOR=fake; else export STEMSTUDIO_SEPARATOR="${STEMSTUDIO_SEPARATOR:-demucs}"; fi
  # Detail model weights (~1.9 GB, first real detail phase); data/ is git-ignored; existing values win.
  export STEMSTUDIO_MODEL_DIR="${STEMSTUDIO_MODEL_DIR:-$data_dir/models}"
  export STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS="${STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS:-1}"
  # Fake device of the fake separator (cpu, gpu, gpu-oom; feature 003); existing value wins.
  export STEMSTUDIO_FAKE_DEVICE="${STEMSTUDIO_FAKE_DEVICE:-cpu}"
  export StemStudio__DataDir="$data_dir/api"
  # Backups folder (feature 013, FR-005): <repo>/data/backups unless StemStudio__BackupDir is set.
  export StemStudio__BackupDir="${StemStudio__BackupDir:-$data_dir/backups}"
  export StemStudio__BackupHostDirHint="${StemStudio__BackupHostDirHint:-$StemStudio__BackupDir}"
  mkdir -p "$StemStudio__BackupDir"
  export StemStudio__WorkerBaseUrl=http://localhost:8000
  export StemStudio__WebOrigin=http://localhost:8080
  export StemStudio__ApiBaseUrl=http://localhost:8081
  export StemStudio__PublicApiBaseUrl=http://localhost:8081
  echo "Separator: $STEMSTUDIO_SEPARATOR (fake device $STEMSTUDIO_FAKE_DEVICE); data under $data_dir; models in $STEMSTUDIO_MODEL_DIR"
  init_library_environment "$data_dir/api"
  init_agent_environment
  init_key_ring_environment
}

init_agent_environment() {
  # Feature 011 (AI composer agent, api only): Ollama is optional and runs on this host; existing
  # values win. STEMSTUDIO_AGENT_FAKE_SCRIPT (with STEMSTUDIO_AGENT_BACKEND=fake) is a host path,
  # e.g. "$REPO_ROOT/e2e/agent-scripts/<name>.json". ANTHROPIC_API_KEY passes through unchanged.
  export StemStudio__Agent__Ollama__BaseUrl="${STEMSTUDIO_OLLAMA_URL:-http://localhost:11434}"
  export StemStudio__Agent__Backend="${STEMSTUDIO_AGENT_BACKEND:-ollama}"
  export STEMSTUDIO_AGENT_FAKE_SCRIPT="${STEMSTUDIO_AGENT_FAKE_SCRIPT:-}"
  local key_state="unset"
  if [[ -n "${ANTHROPIC_API_KEY:-}" ]]; then key_state="set"; fi
  echo "Agent: backend $StemStudio__Agent__Backend; Ollama $StemStudio__Agent__Ollama__BaseUrl; Claude key $key_state"
}

init_key_ring_environment() {
  # 011 follow-up claude-key (api only): Data Protection key ring of the Claude key stored in
  # Settings, per user and outside the repository and data/ (never copy it with the library or a
  # backup); an existing value wins. Windows (Git Bash): %LOCALAPPDATA%/StemStudio/keys, else
  # $XDG_DATA_HOME/stemstudio/keys or ~/.local/share/stemstudio/keys (the API's own defaults).
  if [[ -z "${StemStudio__DataProtection__KeyRingDir:-}" ]]; then
    local dir="${XDG_DATA_HOME:-}"
    [[ "$dir" == /* ]] || dir="$HOME/.local/share"
    dir="$dir/stemstudio/keys"
    if [[ "$OSTYPE" == msys* || "$OSTYPE" == cygwin* ]] && [[ -n "${LOCALAPPDATA:-}" ]]; then
      dir="$LOCALAPPDATA\StemStudio\keys"
    fi
    export StemStudio__DataProtection__KeyRingDir="$dir"
  fi
  echo "Claude key ring (api, per user; never copy it with data/): $StemStudio__DataProtection__KeyRingDir"
}

init_library_environment() {
  # Feature 007: the worker reads the API data dir (it never writes there; the API is the only writer).
  export STEMSTUDIO_LIBRARY_DIR="$1"
  # Feature 005: STEMSTUDIO_ONLINE_LOOKUPS, STEMSTUDIO_CONTACT_URL, STEMSTUDIO_WIKIPEDIA_LANGUAGES and
  # STEMSTUDIO_FAKE_ONLINE pass through from your shell unchanged (worker defaults: on, generic
  # User-Agent token, en, found); no value is set here. Features 006/008 likewise:
  # STEMSTUDIO_ALIGN_MODEL, STEMSTUDIO_ALIGN_AUTO, STEMSTUDIO_FAKE_LYRICS, STEMSTUDIO_TRANSCRIBER and
  # STEMSTUDIO_TRANSCRIBE_THREADS (worker defaults: turbo, gpu, word, automatic, 8). Sheet-music
  # PDFs need libcairo2 and the Leipzig font on the host (the Docker image has both).
  echo "Library (read-only for the worker): $STEMSTUDIO_LIBRARY_DIR; online lookups: ${STEMSTUDIO_ONLINE_LOOKUPS:-on}"
}

build_engine() {
  local bundle
  for bundle in src/audio-engine src/web-interop; do
    (cd "$REPO_ROOT/$bundle" && npm ci --no-audit --no-fund && npm run build)
  done
}

start_all() {
  echo "Starting worker, api and web ..."
  (cd "$REPO_ROOT/worker" && exec uv run uvicorn --factory stemstudio_worker.main:create_app --host 127.0.0.1 --port 8000) &
  PIDS+=("$!")
  (cd "$REPO_ROOT" && exec dotnet run --no-build --project src/StemStudio.Api --launch-profile http) &
  PIDS+=("$!")
  (cd "$REPO_ROOT" && exec dotnet run --no-build --project src/StemStudio.Web --launch-profile http) &
  PIDS+=("$!")
}

kill_tree() {
  local child
  for child in $(pgrep -P "$1" 2>/dev/null || true); do kill_tree "$child"; done
  kill "$1" 2>/dev/null || true
}

stop_all() {
  local pid
  for pid in "${PIDS[@]}"; do kill_tree "$pid"; done
  wait 2>/dev/null || true
  echo "Stopped worker, api and web."
}

wait_healthy() {
  local url="$1" deadline=$((SECONDS + TIMEOUT)) body
  while [[ $SECONDS -lt $deadline ]]; do
    if body="$(curl --fail --silent --max-time 5 "$url")"; then
      echo "$url -> 200 $body"
      return 0
    fi
    sleep 2
  done
  echo "WARNING: $url did not become healthy within ${TIMEOUT}s" >&2
  return 1
}

wait_all_healthy() {
  local url status=0
  for url in "${HEALTH_URLS[@]}"; do wait_healthy "$url" || status=1; done
  return "$status"
}

main() {
  parse_args "$@"
  init_environment
  if [[ $BUILD_ENGINE -eq 1 ]]; then build_engine; fi
  # one build up front, so API and web do not race on shared outputs (Contracts)
  (cd "$REPO_ROOT" && dotnet build StemStudio.slnx)
  trap stop_all EXIT
  trap 'exit 130' INT TERM
  start_all
  if [[ $SMOKE_TEST -eq 1 ]]; then
    wait_all_healthy
    return
  fi
  wait_all_healthy || true
  echo "StemStudio runs at http://localhost:8080 (API http://localhost:8081). Press Ctrl+C to stop."
  wait
}

main "$@"
