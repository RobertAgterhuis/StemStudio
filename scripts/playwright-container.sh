#!/usr/bin/env bash
# Runs a command in e2e/ inside the official Playwright Linux image whose version equals the
# @playwright/test pin in e2e/package.json (deterministic rendering for visual baselines, feature
# 004 research.md). The repository is mounted at /work; the host network is shared, so the compose
# stack on localhost:8080/8081 is reachable. Files are written as the calling user.
#
# Usage (from the repository root): scripts/playwright-container.sh <command> [args...]
#   e.g. scripts/playwright-container.sh scripts/ci-e2e-suite.sh test:visual visual
# Passes E2E_BASE_URL, E2E_API_URL, E2E_RUN_ID and CI through when they are set.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

playwright_image() {
  local version
  version="$(cd "$REPO_ROOT/e2e" && node -p 'require("./package.json").devDependencies["@playwright/test"]')"
  if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "e2e/package.json must pin @playwright/test exactly (found '$version')" >&2
    exit 2
  fi
  echo "mcr.microsoft.com/playwright:v${version}-noble"
}

main() {
  if [[ $# -lt 1 ]]; then
    sed -n '7,8p' "$0" | sed 's/^# \{0,1\}//' >&2
    exit 2
  fi
  local image
  image="$(playwright_image)"
  echo "Playwright image: $image"
  docker run --rm --network host --ipc host \
    --user "$(id -u):$(id -g)" -e HOME=/tmp \
    -e E2E_BASE_URL -e E2E_API_URL -e E2E_RUN_ID -e CI \
    -v "$REPO_ROOT:/work" -w /work/e2e \
    "$image" bash -c 'npm ci --no-audit --no-fund && cd /work && "$@"' playwright "$@"
}

main "$@"
