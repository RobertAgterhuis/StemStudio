#!/usr/bin/env bash
# Runs every component's lint, test and coverage gate locally (the same commands as CI):
# .NET (build, format, tests with the per-component 90 % line+branch gate), worker (ruff, mypy,
# pytest + coverage), audio engine and web-interop (lint, typecheck, coverage, build), contracts (Redocly, render vectors),
# quality gate (self-checks + SIG checks), public manuals check (docs/public up to date), fixture generator (checks + generation) and the E2E suite
# (lint, typecheck, list). The E2E run itself needs the compose stack (see README / CI).
# Prints a summary; exits 1 when any step failed.
#
# Usage: scripts/test-all.sh [--continue-on-error] [--gpu-e2e]
#   --gpu-e2e  also start a fake stack as compose project stemstudio-e2e twice, with a fake GPU
#              (STEMSTUDIO_FAKE_DEVICE=gpu) and one that runs out of memory (gpu-oom), and run the
#              Playwright @gpu specs each time (feature 003); needs ports 8080/8081 free and the Playwright
#              browsers installed; the project and its volumes are removed afterwards
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONTINUE=0
GPU_E2E=0
E2E_PROJECT=stemstudio-e2e
for arg in "$@"; do
  case "$arg" in
    --continue-on-error) CONTINUE=1 ;;
    --gpu-e2e) GPU_E2E=1 ;;
    *) echo "Unknown option: $arg" >&2; exit 2 ;;
  esac
done
REDOCLY=@redocly/cli@2.78.0
CONTRACTS=specs/007-export-library/contracts  # 007 documents contain 005's unchanged
FX=tests/fixtures/pyproject.toml
SUMMARY=()
FAILED=0

# step <name> <working-dir> <command...>; a missing working directory fails the step.
step() {
  local name="$1" dir="$2"
  shift 2
  if [[ $FAILED -eq 1 && $CONTINUE -eq 0 ]]; then return; fi
  echo -e "\n=== $name ==="
  if (cd "$REPO_ROOT/$dir" && "$@"); then SUMMARY+=("passed   $name"); else SUMMARY+=("FAILED   $name"); FAILED=1; fi
}

# Helpers invoked indirectly through step "$@": uvw = worker env, uvq = quality-gate env.
# shellcheck disable=SC2329
uvw() { uv run --project worker --frozen "$@"; }
# shellcheck disable=SC2329
uvq() { uv run --frozen "$@"; }

step "dotnet tool restore" . dotnet tool restore
step "dotnet build" . dotnet build StemStudio.slnx -c Release
step "dotnet format" . dotnet format StemStudio.slnx --verify-no-changes
step "dotnet test + coverage gate" . dotnet test StemStudio.slnx -c Release --no-build -p:CollectCoverage=true
step "worker uv sync" worker uv sync --frozen
step "worker ruff check" worker uv run --frozen ruff check .
step "worker ruff format" worker uv run --frozen ruff format --check .
step "worker mypy" worker uv run --frozen mypy src tests
step "worker pytest + coverage" worker uv run --frozen pytest --cov --cov-branch --cov-report=term --cov-fail-under=90
step "engine npm ci" src/audio-engine npm ci --no-audit --no-fund
step "engine lint" src/audio-engine npm run lint
step "engine typecheck" src/audio-engine npm run typecheck
step "engine test:coverage" src/audio-engine npm run test:coverage
step "engine build" src/audio-engine npm run build
step "web-interop npm ci" src/web-interop npm ci --no-audit --no-fund
step "web-interop lint" src/web-interop npm run lint
step "web-interop typecheck" src/web-interop npm run typecheck
step "web-interop test:coverage" src/web-interop npm run test:coverage
step "web-interop build" src/web-interop npm run build
step "contracts lint" . npx --yes "$REDOCLY" lint --config "$CONTRACTS/redocly.yaml" \
  "$CONTRACTS/worker-api.openapi.yaml" "$CONTRACTS/backend-api.openapi.yaml"
step "render vectors check" . uv run --project worker --frozen python "$CONTRACTS/generate_render_vectors.py" --check
step "quality ruff check" tools/quality uvq ruff check .
step "quality ruff format" tools/quality uvq ruff format --check .
step "quality mypy" tools/quality uvq mypy
step "quality pytest + coverage" tools/quality uvq pytest --cov --cov-branch --cov-report=term
step "quality gate (run_all)" . uv run --project tools/quality --frozen python tools/quality/run_all.py
step "docs manuals up to date" . node tools/docs/check.mjs
step "fixtures ruff check" . uvw ruff check tests/fixtures
step "fixtures ruff format" . uvw ruff format --check tests/fixtures
step "fixtures mypy" . uvw mypy --config-file "$FX" tests/fixtures
step "fixtures pytest + coverage" . uvw pytest -c "$FX" --cov --cov-branch --cov-config="$FX" tests/fixtures/tests
step "fixtures generate" . uvw python tests/fixtures/generate_fixtures.py
step "e2e npm ci" e2e npm ci --no-audit --no-fund
step "e2e lint" e2e npm run lint
step "e2e typecheck" e2e npm run typecheck
step "e2e list" e2e npx playwright test --list
if [[ $GPU_E2E -eq 1 ]]; then
  # Two passes; the mode goes to compose (worker env) and to Playwright (STEMSTUDIO_FAKE_DEVICE).
  for mode in gpu gpu-oom; do
    step "gpu e2e: fake stack ($mode) up" . env STEMSTUDIO_SEPARATOR=fake STEMSTUDIO_FAKE_DEVICE="$mode" \
      docker compose -p "$E2E_PROJECT" up -d --build --wait --wait-timeout 300
    step "gpu e2e: playwright @gpu ($mode)" e2e env STEMSTUDIO_FAKE_DEVICE="$mode" npm run test:gpu
  done
  docker compose -p "$E2E_PROJECT" down -v
fi

echo -e "\n=== summary ==="
printf '%s\n' "${SUMMARY[@]}"
exit "$FAILED"
