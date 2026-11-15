#!/usr/bin/env bash
# Runs StemStudio without Docker: worker (uv, :8000), API (dotnet run, :8081), web (dotnet run, :8080).
# Builds the audio-engine bundle first; data lives under <repo>/data. Ctrl+C stops all three.
# No secrets are read or needed.
#
# Usage: scripts/dev.sh [--fake] [--skip-engine-build] [--smoke-test] [--timeout <seconds>]
#   --fake               STEMSTUDIO_SEPARATOR=fake (stems are copies of the mix; detail faked; no download)
#   --skip-engine-build  reuse the existing wwwroot/js/audio-engine.js
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
  sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'
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
  export StemStudio__WorkerBaseUrl=http://localhost:8000
  export StemStudio__WebOrigin=http://localhost:8080
  export StemStudio__ApiBaseUrl=http://localhost:8081
  export StemStudio__PublicApiBaseUrl=http://localhost:8081
  echo "Separator: $STEMSTUDIO_SEPARATOR (fake device $STEMSTUDIO_FAKE_DEVICE); data under $data_dir; models in $STEMSTUDIO_MODEL_DIR"
}

build_engine() {
  (cd "$REPO_ROOT/src/audio-engine" && npm ci --no-audit --no-fund && npm run build)
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
