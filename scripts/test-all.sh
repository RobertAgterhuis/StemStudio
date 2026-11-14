#!/usr/bin/env bash
# Runs every component's lint, test and coverage gate locally (the same commands as CI):
# .NET (build, format, tests with the per-component 90 % line+branch gate), worker (ruff, mypy,
# pytest + coverage), audio engine (lint, typecheck, coverage, build), contracts (Redocly),
# quality tools, fixture generator and the E2E lint. Components whose folder does not exist yet
# are skipped. Prints a summary; exits 1 when any step failed.
#
# Usage: scripts/test-all.sh [--continue-on-error]
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONTINUE=0
[[ "${1:-}" == "--continue-on-error" ]] && CONTINUE=1
REDOCLY=@redocly/cli@2.78.0
CONTRACTS=specs/001-stemstudio-mvp/contracts
QREQ=tools/quality/requirements.txt
SUMMARY=()
FAILED=0

# step <name> <needs-path> <working-dir> <command...>
step() {
  local name="$1" needs="$2" dir="$3"
  shift 3
  if [[ $FAILED -eq 1 && $CONTINUE -eq 0 ]]; then return; fi
  if [[ ! -e "$REPO_ROOT/$needs" ]]; then SUMMARY+=("skipped  $name"); return; fi
  echo -e "\n=== $name ==="
  if (cd "$REPO_ROOT/$dir" && "$@"); then SUMMARY+=("passed   $name"); else SUMMARY+=("FAILED   $name"); FAILED=1; fi
}

# Helpers invoked indirectly through step "$@"; uvq adds the quality tools' requirements.
# shellcheck disable=SC2329
uvw() { uv run --project worker --frozen "$@"; }
# shellcheck disable=SC2329
uvq() { uv run --project worker --frozen --with-requirements "$QREQ" "$@"; }

step "dotnet tool restore" StemStudio.slnx . dotnet tool restore
step "dotnet build" StemStudio.slnx . dotnet build StemStudio.slnx -c Release
step "dotnet format" StemStudio.slnx . dotnet format StemStudio.slnx --verify-no-changes
step "dotnet test + coverage gate" StemStudio.slnx . dotnet test StemStudio.slnx -c Release --no-build -p:CollectCoverage=true
step "worker uv sync" worker worker uv sync --frozen
step "worker ruff check" worker worker uv run --frozen ruff check .
step "worker ruff format" worker worker uv run --frozen ruff format --check .
step "worker mypy" worker worker uv run --frozen mypy src tests
step "worker pytest + coverage" worker worker uv run --frozen pytest --cov --cov-branch --cov-report=term --cov-fail-under=90
step "engine npm ci" src/audio-engine src/audio-engine npm ci --no-audit --no-fund
step "engine lint" src/audio-engine src/audio-engine npm run lint
step "engine typecheck" src/audio-engine src/audio-engine npm run typecheck
step "engine test:coverage" src/audio-engine src/audio-engine npm run test:coverage
step "engine build" src/audio-engine src/audio-engine npm run build
step "contracts lint" "$CONTRACTS" . npx --yes "$REDOCLY" lint --config "$CONTRACTS/redocly.yaml" \
  "$CONTRACTS/worker-api.openapi.yaml" "$CONTRACTS/backend-api.openapi.yaml"
step "quality tools ruff" tools/quality . uvw ruff check tools/quality
step "quality tools mypy" tools/quality . uvq mypy --strict --config-file tools/quality/pyproject.toml tools/quality
step "quality tools pytest" tools/quality . uvq pytest tools/quality/tests --cov=tools/quality --cov-branch --cov-config=tools/quality/pyproject.toml --cov-fail-under=90
step "quality run_all" tools/quality . uvq python tools/quality/run_all.py
step "fixtures ruff" tests/fixtures . uvw ruff check tests/fixtures
step "fixtures mypy" tests/fixtures . uvw mypy --strict tests/fixtures
step "fixtures pytest" tests/fixtures/tests . uvw pytest tests/fixtures/tests --cov=tests/fixtures --cov-branch --cov-fail-under=90
step "e2e npm ci" e2e e2e npm ci --no-audit --no-fund
step "e2e lint" e2e e2e npm run lint

echo -e "\n=== summary ==="
printf '%s\n' "${SUMMARY[@]}"
exit "$FAILED"
