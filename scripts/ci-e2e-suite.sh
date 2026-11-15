#!/usr/bin/env bash
# Runs one optional E2E suite of the e2e package (feature 004: visual, a11y, lighthouse) and passes
# when the suite does not exist yet, so the CI jobs stay green while qa adds the specs (tasks.md T009).
#
# Usage (from the repository root): scripts/ci-e2e-suite.sh <npm-script> <marker> [args...]
#   <npm-script>  script in e2e/package.json, e.g. test:visual
#   <marker>      path relative to e2e/: a directory that must hold at least one *.spec.ts, or a
#                 file (e.g. lighthouserc.json) that must exist
#   [args...]     passed to the npm script after "--" (e.g. --update-snapshots)
# Exits with the suite's exit code, 0 when the suite is skipped, 2 on wrong usage.
set -euo pipefail

E2E_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../e2e" && pwd)"

skip() {
  echo "::notice::$1; suite skipped (passes until its specs exist)"
  exit 0
}

# Exits 0 when e2e/package.json defines the npm script $1 (node: npm pkg get differs between npm versions).
has_script() {
  node -e 'process.exit(require("./package.json").scripts?.[process.argv[1]] ? 0 : 1)' "$1"
}

has_marker() {
  local marker="$E2E_DIR/$1"
  if [[ -f "$marker" ]]; then return 0; fi
  [[ -d "$marker" ]] && [[ -n "$(find "$marker" -name '*.spec.ts' -print -quit)" ]]
}

main() {
  if [[ $# -lt 2 ]]; then
    sed -n '5,9p' "$0" | sed 's/^# \{0,1\}//' >&2
    exit 2
  fi
  local script="$1" marker="$2"
  shift 2
  cd "$E2E_DIR"
  if ! has_script "$script"; then skip "npm script '$script' is not defined in e2e/package.json"; fi
  if ! has_marker "$marker"; then skip "no specs at e2e/$marker"; fi
  if [[ $# -gt 0 ]]; then npm run "$script" -- "$@"; else npm run "$script"; fi
}

main "$@"
