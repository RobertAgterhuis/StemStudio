#!/usr/bin/env bash
# Checks that a running StemStudio web serves the audio engine and every AudioWorklet module the
# engine loads by URL (feature 016 T011, engine contract 2.6.0, ADR-032):
#   - /js/audio-engine.js                  the engine bundle (vite.config.ts)
#   - /js/time-stretch-processor.js        the 012 time-stretch worklet (vite.processor.config.ts)
#   - /js/audio-engine-capture-worklet.js  the 015 capture worklet (vite.capture.config.ts)
#   - /js/audio-engine-pitch-worklet.js    the 016 pitch tracker (vite.pitch.config.ts)
# each with 200 and Content-Type text/javascript; the pitch module must be the real build (it
# registers the processor "stemstudio-pitch-tracker").
#
# Usage: scripts/check-web-assets.sh [BASE_URL]
#   BASE_URL  default http://localhost:8080
# Exit codes: 0 all checks passed, 1 a check failed (all failures are listed), 2 usage error.
set -euo pipefail

BASE=http://localhost:8080
FAILURES=0
MODULES=(audio-engine.js time-stretch-processor.js audio-engine-capture-worklet.js audio-engine-pitch-worklet.js)
PITCH_PROCESSOR=stemstudio-pitch-tracker
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      -h | --help) sed -n '2,/^set -euo/p' "${BASH_SOURCE[0]}" | sed '$d; s/^# \{0,1\}//'; exit 0 ;;
      -*) echo "check-web-assets: unknown option: $1" >&2; exit 2 ;;
      *) BASE="${1%/}" ;;
    esac
    shift
  done
}

fail() { echo "FAIL $*"; FAILURES=$((FAILURES + 1)); }
pass() { echo "ok   $*"; }

# fetch <path> <file>: prints "<status> <content type>" and stores the body in <file>.
fetch() { curl --silent --show-error --output "$2" --write-out '%{http_code} %{content_type}' "$BASE/$1" 2> /dev/null || echo "000 -"; }

check_module() {
  local path="js/$1" got
  got="$(fetch "$path" "$TMP/$1")"
  if [[ "$got" == "200 text/javascript"* ]]; then pass "$path ($got)"; else fail "$path: expected 200 text/javascript, got $got"; return 1; fi
}

check_pitch_build() {
  local path=js/audio-engine-pitch-worklet.js
  if grep -q "registerProcessor" "$TMP/audio-engine-pitch-worklet.js" && grep -q "$PITCH_PROCESSOR" "$TMP/audio-engine-pitch-worklet.js"; then
    pass "$path registers $PITCH_PROCESSOR"
  else
    fail "$path does not register $PITCH_PROCESSOR"
  fi
}

main() {
  local name
  parse_args "$@"
  echo "checking $BASE"
  for name in "${MODULES[@]}"; do
    if check_module "$name" && [[ "$name" == audio-engine-pitch-worklet.js ]]; then check_pitch_build; fi
  done
  if [[ $FAILURES -gt 0 ]]; then echo "$FAILURES check(s) failed"; exit 1; fi
  echo "all web asset checks passed"
}

main "$@"
