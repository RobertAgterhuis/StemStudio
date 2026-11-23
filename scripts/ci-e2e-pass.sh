#!/usr/bin/env bash
# Runs one feature pass of the E2E suite against a running fake stack (feature 016 T144, LESSONS
# 014/015: the single E2E job hit its time limit). The CI job `e2e-passes` runs one pass per matrix
# entry, each on its own stack; the default suite runs per browser in the job `e2e`.
#
#   recording      015 play-along recording, full Chromium with fake capture and the digital loopback
#   practice-flow  014 sections scenario 4: the api without detection on complete
#   restart        restart api and web, then the @restart persistence specs (FR-022)
#   agent          011 agent specs with the fake backend, then the unavailable spec (backend none)
#   lyric          023 lyric assistant with the fake model, then the unavailable spec
#   gpu            003 @gpu specs with a fake GPU, then with a fake GPU that runs out of memory
#   instrument     016 instrument views (@instrument, Chromium and Firefox)
#   fake-mic       016 singing feedback with the qa microphone fixtures (projects fake-mic-*)
#
# Usage (from the repository root): scripts/ci-e2e-pass.sh [--dry-run] <pass> | --list
#   --dry-run  print the commands instead of running them
# Exit codes: the pass's exit code; 2 on wrong usage or an unknown pass.
set -euo pipefail

PASSES=(recording practice-flow restart agent lyric gpu instrument fake-mic)
DRY=0
WAIT=(--wait --wait-timeout 300)

# run <command...>: echo the command; execute it unless --dry-run.
run() {
  echo "+ $*"
  if [[ $DRY -eq 0 ]]; then eval "$@"; fi
}

worker_health() {
  run "docker compose exec -T worker python -c \"import urllib.request as u; print(u.urlopen('http://127.0.0.1:8000/health').read().decode())\""
}

pass_recording() { run "(cd e2e && E2E_RECORDING_LOOPBACK=1 npm run test:recording)"; }

pass_practice_flow() {
  run "STEMSTUDIO_SECTION_DETECT_ON_COMPLETE=false docker compose up -d ${WAIT[*]} api"
  run "(cd e2e && E2E_SECTION_DETECT_ON_COMPLETE=false npm run test:practice-flow-no-detect)"
  run "docker compose up -d ${WAIT[*]} api"
}

pass_restart() {
  run "docker compose restart api web"
  run "docker compose up -d ${WAIT[*]} api web"
  run "(cd e2e && npm run test:restart)"
}

pass_agent() { run "scripts/agent-e2e.sh fake" && run "scripts/agent-e2e.sh none"; }

pass_lyric() { run "scripts/lyric-e2e.sh fake" && run "scripts/lyric-e2e.sh none"; }

pass_gpu() {
  local device
  for device in gpu gpu-oom; do
    # Only the worker's environment changes, so compose recreates just the worker.
    run "STEMSTUDIO_FAKE_DEVICE=$device docker compose up -d ${WAIT[*]}"
    worker_health
    # The @gpu specs read the device themselves (e2e/support/env.ts), as in the 003 job.
    run "(cd e2e && STEMSTUDIO_FAKE_DEVICE=$device npm run test:gpu)"
  done
}

pass_instrument() { run "(cd e2e && npm run test:instrument)"; }

pass_fake_mic() { run "(cd e2e && npm run test:fake-mic)"; }

usage() {
  sed -n '17,19p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2
  exit 2
}

main() {
  if [[ "${1:-}" == --list ]]; then echo "${PASSES[*]}"; exit 0; fi
  if [[ "${1:-}" == --dry-run ]]; then DRY=1; shift; fi
  [[ $# -eq 1 ]] || usage
  if [[ " ${PASSES[*]} " != *" $1 "* ]]; then echo "ci-e2e-pass: unknown pass: $1" >&2; exit 2; fi
  "pass_${1//-/_}"
}

main "$@"
