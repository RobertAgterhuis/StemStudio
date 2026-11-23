#!/usr/bin/env bash
# Runs every component's lint, test and coverage gate locally (the same commands as CI):
# .NET (build, format, tests with the per-component 90 % line+branch gate), worker and generator
# (ruff, mypy, pytest + coverage; the generator's base install only, no torch), audio engine and web-interop (lint, typecheck, coverage, build), contracts (Redocly, render vectors),
# performance budgets (Category=Perf, uninstrumented), 022/023 contracts (Redocly, merge_full.py --check, output schema, merge tests),
# secret scan (no Claude key or key value in the repo, feature 011), key volume checks (011 claude-key: tests + static compose check),
# quality gate (self-checks + SIG checks), public manuals check (docs/public up to date), website build + check (tools/site), fixture generator (checks + generation) and the E2E suite
# (lint, typecheck, list); feature 012: the 012 contract lint + superset check, the soundtouch emsdk pin check and,
# when soundtouch.wasm has been built, the engine's Node WASM suite (test:wasm). The E2E run itself needs the compose stack (see README / CI).
# Prints a summary; exits 1 when any step failed.
#
# Usage: scripts/test-all.sh [--continue-on-error] [--affected [--from <ref>]] [--gpu-e2e] [--agent-e2e] [--recording-e2e]
#   --affected  run only the step groups a change needs (refactor proposal 001): scripts/affected_select.py
#              maps the files changed since <ref> (default: the merge-base with main), uncommitted and untracked
#              files included, through scripts/affected/components.json and narrows the .NET projects with
#              dotnet-affected; it prints which groups and projects run or are skipped and why. An unmapped file,
#              an always-build file (.claude/tipoca/affected/always-build.txt), a missing base or a tool error
#              runs everything. For lane checkpoints only: integration and pre-merge runs stay FULL (no flag).
#   --gpu-e2e  also start a fake stack as compose project stemstudio-e2e twice, with a fake GPU
#              (STEMSTUDIO_FAKE_DEVICE=gpu) and one that runs out of memory (gpu-oom), and run the
#              Playwright @gpu specs each time (feature 003); needs ports 8080/8081 free and the Playwright
#              browsers installed; the project and its volumes are removed afterwards
#   --agent-e2e  also start a fake stack (STEMSTUDIO_AGENT_BACKEND=none) as compose project
#              stemstudio-e2e and run scripts/agent-e2e.sh fake, then none (feature 011, T059); same
#              prerequisites, the project and its volumes are removed afterwards
#   --recording-e2e  also generate the 015 capture fixtures, start a fake stack with the e2e override
#              (web StemStudio__Recording__TestLoopback=true) as compose project stemstudio-e2e and run
#              npm run test:recording (project `recording`, full Chromium build, E2E_RECORDING_LOOPBACK=1;
#              feature 015, T010); same prerequisites, the project and its volumes are removed afterwards
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONTINUE=0
GPU_E2E=0
AGENT_E2E=0
RECORDING_E2E=0
E2E_PROJECT=stemstudio-e2e
AFFECTED=0
AFFECTED_FROM=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --continue-on-error) CONTINUE=1 ;;
    --gpu-e2e) GPU_E2E=1 ;;
    --agent-e2e) AGENT_E2E=1 ;;
    --recording-e2e) RECORDING_E2E=1 ;;
    --affected) AFFECTED=1 ;;
    --from) [[ $# -ge 2 ]] || { echo "--from needs a ref" >&2; exit 2; }; AFFECTED_FROM=(--from "$2"); shift ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
  shift
done
if [[ ${#AFFECTED_FROM[@]} -gt 0 && $AFFECTED -eq 0 ]]; then echo "--from needs --affected" >&2; exit 2; fi
REDOCLY=@redocly/cli@2.78.0
# Round 014+015: the newest full backend document is 015's 1.14.0 (= 014's 1.13.0 + the recording
# delta); the worker document is 014's 1.8.0. Render vectors 1.1.0 (007's + the take cases) are 015's.
CONTRACTS=specs/015-play-along-recording/contracts
# Feature 016: backend 1.15.0, worker 1.9.0, both deltas, merge_full.py and the chord spelling vectors 1.0.0.
INSTRUMENT_CONTRACTS=specs/016-instrument-views/contracts
RENDER_VECTORS=specs/015-play-along-recording/contracts/generate_render_vectors.py
PRACTICE_FLOW_CONTRACTS=specs/014-practice-flow/contracts  # backend 1.13.0, worker 1.8.0 and both deltas
# Features 009/010 (generator 1.0.0/1.1.0, backend 1.8.0/1.9.0); 010's documents contain 009's.
GENERATIVE_CONTRACTS=(specs/009-style-transfer/contracts specs/010-lyrics-to-song/contracts)
# Feature 011 (backend 1.10.0 = 010's 1.9.0 + the agent delta); worker stays 008's, generator 010's.
AGENT_CONTRACTS=specs/011-ai-composer-agent/contracts
# 011 follow-up claude-key: full backend 1.21.0 (= 1.10.0 + claude-key delta.2) and the delta itself.
CLAUDE_KEY_CONTRACTS=specs/011-ai-composer-agent/followups/claude-key/contracts
# Feature 012 (backend 1.11.0 = 1.21.0 + the practice settings); superset check needs PyYAML only.
PRACTICE_CONTRACTS=specs/012-practice-tempo/contracts
# Feature 013 (backend 1.12.0 = 1.11.0 + library care, worker 1.7.0); backup fixture tools have their own pyproject.
LIBRARY_CARE_CONTRACTS=specs/013-library-care/contracts
BACKUP_FX=tests/fixtures/backup/pyproject.toml
PYYAML=pyyaml==6.0.3
# Features 022 (backend 1.22.0 = 1.12.0 + library folders) and 023 (backend 1.23.0 = 1.22.0 + lyric assistant):
# full document + delta, merge_full.py (freshness, superset), 023 output schema check and merge tests.
LIBRARY_FOLDERS_CONTRACTS=specs/022-library-folders/contracts
LYRIC_ASSIST_CONTRACTS=specs/023-lyric-writing-assistant/contracts
JSONSCHEMA=jsonschema==4.26.0  # as worker/uv.lock
PYTEST=pytest==9.2.0
# SoundTouch module (012): built by scripts/build-soundtouch.sh; test:wasm runs only when it exists.
SOUNDTOUCH_WASM=src/StemStudio.Web/wwwroot/js/soundtouch/soundtouch.wasm
SCRIPTS_CFG=scripts/pyproject.toml  # tested devops checks (key_volume_checks.py, build-soundtouch tests), quality-gate env
KEY_CHECKS=(scripts/key_volume_checks.py scripts/backup_folder_checks.py scripts/api_image_size_checks.py scripts/affected_select.py scripts/tests)
AGENT_COVERAGE=tests/StemStudio.Agent.Tests/TestResults/coverage/coverage.cobertura.xml
AGENT_EVAL_COVERAGE=tests/StemStudio.AgentEval.Tests/TestResults/coverage/coverage.cobertura.xml
FX=tests/fixtures/pyproject.toml
SUMMARY=()
FAILED=0
# Affected selection (--affected): FULL_RUN=0 and SELECTED lists the groups that run; every step belongs to the
# group named by the last `group` line above it (an empty group always runs: the opt-in E2E passes).
FULL_RUN=1
SELECTED=""
CURRENT_GROUP=""
DOTNET_TARGET=StemStudio.slnx
group() { CURRENT_GROUP="$1"; }
selected() { [[ $FULL_RUN -eq 1 || -z "$1" || " $SELECTED " == *" $1 "* ]]; }

# affected_line <kind> <key> <verb> <rest>: applies and prints one line of `affected_select.py --format lines`.
affected_line() {
  case "$1" in
    mode) [[ $2 == affected ]] && FULL_RUN=0; echo "mode: $2" ;;
    group) [[ $3 == run ]] && SELECTED+=" $2"; printf '  %-5s %-24s %s\n' "$3" "$2" "$4" ;;
    dotnet) if [[ $2 == target ]]; then DOTNET_TARGET="$3${4:+ $4}"; echo "  .NET build input: $DOTNET_TARGET"; else echo "  .NET project: $3"; fi ;;
    *) echo "$1: $2 $3 $4" ;;
  esac
}

# Runs the selector; exits 0 when nothing changed, keeps FULL_RUN=1 when it fails.
affected_select() {
  local out code kind key verb rest
  echo -e "\n=== affected selection ==="
  out="$(cd "$REPO_ROOT" && uv run --no-project python -I scripts/affected_select.py --format lines "${AFFECTED_FROM[@]}")"
  code=$?
  if [[ $code -eq 166 ]]; then echo "Nothing changed since the base: no step runs."; exit 0; fi
  if [[ $code -ne 0 ]]; then echo "affected_select.py failed (exit $code): running everything"; return; fi
  while read -r kind key verb rest; do affected_line "$kind" "$key" "$verb" "$rest"; done <<< "$out"
}
[[ $AFFECTED -eq 1 ]] && affected_select

# step <name> <working-dir> <command...>; a missing working directory fails the step.
step() {
  local name="$1" dir="$2"
  shift 2
  if [[ $FAILED -eq 1 && $CONTINUE -eq 0 ]]; then return; fi
  if ! selected "$CURRENT_GROUP"; then SUMMARY+=("skipped  $name (affected: $CURRENT_GROUP not selected)"); return; fi
  echo -e "\n=== $name ==="
  if (cd "$REPO_ROOT/$dir" && "$@"); then SUMMARY+=("passed   $name"); else SUMMARY+=("FAILED   $name"); FAILED=1; fi
}

# Helpers invoked indirectly through step "$@": uvw = worker env, uvq = quality-gate env.
# shellcheck disable=SC2329
uvw() { uv run --project worker --frozen "$@"; }
# shellcheck disable=SC2329
uvq() { uv run --frozen "$@"; }
# shellcheck disable=SC2329
uvk() { uv run --project tools/quality --frozen "$@"; }
# Static key volume check (011 claude-key) for every compose combination CI validates.
# shellcheck disable=SC2329
key_volume_all() {
  scripts/check-key-volume.sh &&
    scripts/check-key-volume.sh -f docker-compose.yml -f docker-compose.gpu.yml &&
    scripts/check-key-volume.sh -f docker-compose.yml -f docker-compose.gpu.yml -f docker-compose.generative.yml &&
    scripts/check-key-volume.sh -f docker-compose.yml -f docker-compose.generative.yml -f docker-compose.generative-fake.yml
}

group dotnet
step "dotnet tool restore" . dotnet tool restore
step "dotnet build" . dotnet build "$DOTNET_TARGET" -c Release
step "dotnet format" . dotnet format "$DOTNET_TARGET" --verify-no-changes
step "dotnet test + coverage gate" . dotnet test "$DOTNET_TARGET" -c Release --no-build -p:CollectCoverage=true
group dotnet-agent-floor
step "agent coverage floor (011)" . uvw python scripts/coverage_floor.py "$AGENT_COVERAGE"
group dotnet-agent-eval-floor
step "agent-eval coverage floor (011)" . uvw python scripts/coverage_floor.py "$AGENT_EVAL_COVERAGE"
# Timing asserts skip under coverlet: the performance budgets (022 SC-004) run uninstrumented (as in CI).
group dotnet-perf
step "performance budgets, uninstrumented (Category=Perf)" . dotnet test tests/StemStudio.Api.Tests -c Release --no-build --filter Category=Perf -p:RunSettingsFilePath=
group worker
step "worker uv sync" worker uv sync --frozen
step "worker ruff check" worker uv run --frozen ruff check .
step "worker ruff format" worker uv run --frozen ruff format --check .
step "worker mypy" worker uv run --frozen mypy src tests
step "worker pytest + coverage" worker uv run --frozen pytest --cov --cov-branch --cov-report=term --cov-fail-under=90
group generator
step "generator uv sync" generator uv sync --frozen
step "generator ruff check" generator uv run --frozen ruff check .
step "generator ruff format" generator uv run --frozen ruff format --check .
step "generator mypy" generator uv run --frozen mypy src tests
step "generator pytest + coverage" generator uv run --frozen pytest --cov --cov-branch --cov-report=term --cov-fail-under=90
group audio-engine
step "engine npm ci" src/audio-engine npm ci --no-audit --no-fund
step "engine lint" src/audio-engine npm run lint
step "engine typecheck" src/audio-engine npm run typecheck
step "engine test:coverage" src/audio-engine npm run test:coverage
step "engine build" src/audio-engine npm run build
group soundtouch
if [[ -f "$REPO_ROOT/$SOUNDTOUCH_WASM" ]]; then
  step "engine test:wasm (012, real soundtouch.wasm)" src/audio-engine npm run test:wasm
else
  echo -e "
engine test:wasm skipped: $SOUNDTOUCH_WASM not built (scripts/build-soundtouch.sh)"
fi
step "soundtouch emsdk pin (012)" . scripts/build-soundtouch.sh --check-pins
group web-interop
step "web-interop npm ci" src/web-interop npm ci --no-audit --no-fund
step "web-interop lint" src/web-interop npm run lint
step "web-interop typecheck" src/web-interop npm run typecheck
step "web-interop test:coverage" src/web-interop npm run test:coverage
step "web-interop build" src/web-interop npm run build
group contracts
step "contracts lint (015-play-along-recording)" . npx --yes "$REDOCLY" lint --config "$CONTRACTS/redocly.yaml" \
  "$CONTRACTS/backend-api.openapi.yaml" "$CONTRACTS/recording-api.openapi.yaml"
step "contracts lint (014-practice-flow)" . npx --yes "$REDOCLY" lint --config "$PRACTICE_FLOW_CONTRACTS/redocly.yaml" \
  "$PRACTICE_FLOW_CONTRACTS/backend-api.openapi.yaml" "$PRACTICE_FLOW_CONTRACTS/worker-api.openapi.yaml" \
  "$PRACTICE_FLOW_CONTRACTS/practice-api.openapi.yaml" "$PRACTICE_FLOW_CONTRACTS/worker-practice.openapi.yaml"
for dir in "${GENERATIVE_CONTRACTS[@]}"; do
  feature="${dir#specs/}"
  step "contracts lint (${feature%%/*})" . npx --yes "$REDOCLY" lint --config "$dir/redocly.yaml"     "$dir/generator-api.openapi.yaml" "$dir/backend-api.openapi.yaml"
done
step "contracts lint (011-ai-composer-agent)" . npx --yes "$REDOCLY" lint --config "$AGENT_CONTRACTS/redocly.yaml"   "$AGENT_CONTRACTS/backend-api.openapi.yaml" "$AGENT_CONTRACTS/agent-api.openapi.yaml"
step "contracts lint (011 claude-key)" . npx --yes "$REDOCLY" lint --config "$CLAUDE_KEY_CONTRACTS/redocly.yaml" \
  "$CLAUDE_KEY_CONTRACTS/backend-api.openapi.yaml" "$CLAUDE_KEY_CONTRACTS/backend-api.claude-key.openapi.yaml"
step "contracts lint (012-practice-tempo)" . npx --yes "$REDOCLY" lint --config "$PRACTICE_CONTRACTS/redocly.yaml"   "$PRACTICE_CONTRACTS/backend-api.openapi.yaml"
step "contracts superset (012 contains 1.21.0)" . uv run --no-project --with "$PYYAML" python -I "$PRACTICE_CONTRACTS/check_superset.py"
step "contracts lint (013-library-care)" . npx --yes "$REDOCLY" lint --config "$LIBRARY_CARE_CONTRACTS/redocly.yaml"   "$LIBRARY_CARE_CONTRACTS/backend-api.openapi.yaml" "$LIBRARY_CARE_CONTRACTS/backend-api.delta.openapi.yaml" "$LIBRARY_CARE_CONTRACTS/worker-api.openapi.yaml"
step "contracts superset (013 contains 1.11.0)" . uv run --no-project --with "$PYYAML" python -I "$LIBRARY_CARE_CONTRACTS/check_superset.py"
step "contracts superset (013 contains its delta)" . uv run --no-project --with "$PYYAML" python -I "$LIBRARY_CARE_CONTRACTS/check_superset.py"   "$LIBRARY_CARE_CONTRACTS/backend-api.delta.openapi.yaml" "$LIBRARY_CARE_CONTRACTS/backend-api.openapi.yaml"
step "contracts lint (022-library-folders)" . npx --yes "$REDOCLY" lint --config "$LIBRARY_FOLDERS_CONTRACTS/redocly.yaml"   "$LIBRARY_FOLDERS_CONTRACTS/backend-api.openapi.yaml" "$LIBRARY_FOLDERS_CONTRACTS/backend-api.library-folders.openapi.yaml"
step "contracts lint (023-lyric-writing-assistant)" . npx --yes "$REDOCLY" lint --config "$LYRIC_ASSIST_CONTRACTS/redocly.yaml"   "$LYRIC_ASSIST_CONTRACTS/backend-api.openapi.yaml" "$LYRIC_ASSIST_CONTRACTS/backend-api.delta.openapi.yaml"
step "contracts merge check (022 full 1.22.0)" . uv run --no-project --with "$PYYAML" python -I "$LIBRARY_FOLDERS_CONTRACTS/merge_full.py" --check
step "contracts merge check (023 full 1.23.0)" . uv run --no-project --with "$PYYAML" python -I "$LYRIC_ASSIST_CONTRACTS/merge_full.py" --check
step "contracts superset (014 contains 1.23.0)" . uv run --no-project --with "$PYYAML" python -I "$PRACTICE_FLOW_CONTRACTS/check_superset.py"
step "contracts superset (014 contains its delta)" . uv run --no-project --with "$PYYAML" python -I "$PRACTICE_FLOW_CONTRACTS/check_superset.py" \
  "$PRACTICE_FLOW_CONTRACTS/practice-api.openapi.yaml" "$PRACTICE_FLOW_CONTRACTS/backend-api.openapi.yaml"
step "contracts merge check (014 full 1.13.0)" . uv run --no-project --with "$PYYAML" python -I "$PRACTICE_FLOW_CONTRACTS/merge_full.py" --check
step "contracts superset (015 contains 1.13.0)" . uv run --no-project --with "$PYYAML" python -I "$CONTRACTS/check_superset.py"
step "contracts superset (015 contains its delta)" . uv run --no-project --with "$PYYAML" python -I "$CONTRACTS/check_superset.py" \
  "$CONTRACTS/recording-api.openapi.yaml" "$CONTRACTS/backend-api.openapi.yaml"
step "contracts merge check (015 full 1.14.0)" . uv run --no-project --with "$PYYAML" python -I "$CONTRACTS/merge_recording_delta.py" --check
step "contracts output schema (023 check_output_schema.py)" . uv run --no-project --with "$JSONSCHEMA" python -I "$LYRIC_ASSIST_CONTRACTS/check_output_schema.py"
step "contracts merge tests (023 test_merge_full.py)" . uv run --no-project --with "$PYYAML" --with "$PYTEST" python -m pytest "$LYRIC_ASSIST_CONTRACTS"
step "contracts lint (016-instrument-views)" . npx --yes "$REDOCLY" lint --config "$INSTRUMENT_CONTRACTS/redocly.yaml" "$INSTRUMENT_CONTRACTS/backend-api.openapi.yaml" "$INSTRUMENT_CONTRACTS/worker-api.openapi.yaml" "$INSTRUMENT_CONTRACTS/backend-api.delta.openapi.yaml" "$INSTRUMENT_CONTRACTS/worker-api.delta.openapi.yaml"
step "contracts merge check (016 full 1.15.0 / 1.9.0)" . uv run --no-project --with "$PYYAML" python -I "$INSTRUMENT_CONTRACTS/merge_full.py" --check
step "chord spelling vectors check (016)" . uv run --no-project python -I "$INSTRUMENT_CONTRACTS/generate_chord_spelling_vectors.py" --check
step "chord spelling vector tests (016)" . uv run --no-project --with "$PYTEST" python -m pytest "$INSTRUMENT_CONTRACTS"
group scripts-tests
step "split E2E jobs check (016 T144)" . uv run --no-project --with "$PYYAML" --with "$PYTEST" python -m pytest -c scripts/pyproject.toml -q scripts/tests/test_ci_e2e_split.py
group secret-scan
step "secret scan self-test" . uvw python scripts/secret_scan.py --self-test
step "secret scan" . uvw python scripts/secret_scan.py
group contracts
step "render vectors check" . uv run --project worker --frozen python "$RENDER_VECTORS" --check
group quality-tools
step "quality ruff check" tools/quality uvq ruff check .
step "quality ruff format" tools/quality uvq ruff format --check .
step "quality mypy" tools/quality uvq mypy
step "quality pytest + coverage" tools/quality uvq pytest --cov --cov-branch --cov-report=term
group scripts-tests
step "key volume checks ruff check" . uvk ruff check --config "$SCRIPTS_CFG" "${KEY_CHECKS[@]}"
step "key volume checks ruff format" . uvk ruff format --config "$SCRIPTS_CFG" --check "${KEY_CHECKS[@]}"
step "key volume checks mypy" . uvk mypy --config-file "$SCRIPTS_CFG" "${KEY_CHECKS[@]}"
step "key volume checks pytest + coverage" . uvk pytest -c "$SCRIPTS_CFG" --cov --cov-branch --cov-config="$SCRIPTS_CFG" --cov-report=term scripts/tests
group compose
step "key volume check (compose config, all combinations)" . key_volume_all
group quality-gate
step "quality gate (run_all)" . uv run --project tools/quality --frozen python tools/quality/run_all.py
group docs-manuals
step "docs manuals up to date" . node tools/docs/check.mjs
group site
step "site npm ci (tools/docs markdown-it)" tools/docs npm ci --no-audit --no-fund
step "site unit tests" . node --test tools/site/test/site.test.mjs
step "site build + check" . node tools/site/check.mjs
group fixtures
step "fixtures ruff check" . uvw ruff check tests/fixtures
step "fixtures ruff format" . uvw ruff format --check tests/fixtures
step "fixtures mypy" . uvw mypy --config-file "$FX" --exclude '^tests/fixtures/backup/' tests/fixtures
step "fixtures pytest + coverage" . uvw pytest -c "$FX" --cov --cov-branch --cov-config="$FX" tests/fixtures/tests
step "backup fixtures ruff check (013)" . uvw ruff check --config "$BACKUP_FX" tests/fixtures/backup
step "backup fixtures ruff format (013)" . uvw ruff format --config "$BACKUP_FX" --check tests/fixtures/backup
step "backup fixtures mypy (013)" . uvw mypy --config-file "$BACKUP_FX" tests/fixtures/backup
step "backup fixtures pytest + coverage (013)" . uvw pytest -c "$BACKUP_FX" --cov --cov-branch --cov-config="$BACKUP_FX" --cov-report=term tests/fixtures/backup/tests
step "fixtures generate" . uvw python tests/fixtures/generate_fixtures.py
group e2e-lint
step "e2e npm ci" e2e npm ci --no-audit --no-fund
step "e2e lint" e2e npm run lint
step "e2e typecheck" e2e npm run typecheck
step "e2e list" e2e npx playwright test --list
# The opt-in E2E passes below run whenever their flag is given (no group).
CURRENT_GROUP=""
if [[ $GPU_E2E -eq 1 ]]; then
  # Two passes; the mode goes to compose (worker env) and to Playwright (STEMSTUDIO_FAKE_DEVICE).
  for mode in gpu gpu-oom; do
    step "gpu e2e: fake stack ($mode) up" . env STEMSTUDIO_SEPARATOR=fake STEMSTUDIO_FAKE_DEVICE="$mode" \
      docker compose -p "$E2E_PROJECT" up -d --build --wait --wait-timeout 300
    step "gpu e2e: playwright @gpu ($mode)" e2e env STEMSTUDIO_FAKE_DEVICE="$mode" npm run test:gpu
  done
  docker compose -p "$E2E_PROJECT" down -v
fi
if [[ $AGENT_E2E -eq 1 ]]; then
  # The agent passes recreate only the api of this project (COMPOSE_PROJECT_NAME).
  step "agent e2e: fake stack up" . env STEMSTUDIO_SEPARATOR=fake STEMSTUDIO_AGENT_BACKEND=none     docker compose -p "$E2E_PROJECT" up -d --build --wait --wait-timeout 300
  for backend in fake none; do
    step "agent e2e: playwright agent.spec.ts ($backend)" . env COMPOSE_PROJECT_NAME="$E2E_PROJECT"       STEMSTUDIO_SEPARATOR=fake scripts/agent-e2e.sh "$backend"
  done
  # 023 lyric assistant passes (fake model with /lyric-scripts, then backend none); same api-only recreate.
  for backend in fake none; do
    step "agent e2e: lyric assistant ($backend)" . env COMPOSE_PROJECT_NAME="$E2E_PROJECT"       STEMSTUDIO_SEPARATOR=fake scripts/lyric-e2e.sh "$backend"
  done
  docker compose -p "$E2E_PROJECT" down -v
fi
if [[ $RECORDING_E2E -eq 1 ]]; then
  # 015: the capture inputs come from a committed generator (module run from tests/fixtures).
  step "recording e2e: capture fixtures" tests/fixtures uv run --project ../../worker --frozen \
    python -m recording.generate_recording_fixtures
  step "recording e2e: fake stack up (e2e override)" . env STEMSTUDIO_SEPARATOR=fake \
    docker compose -p "$E2E_PROJECT" -f docker-compose.yml -f docker-compose.e2e.yml up -d --build --wait --wait-timeout 300
  step "recording e2e: playwright @recording" e2e env E2E_RECORDING_LOOPBACK=1 npm run test:recording
  docker compose -p "$E2E_PROJECT" -f docker-compose.yml -f docker-compose.e2e.yml down -v
fi

echo -e "\n=== summary ==="
printf '%s\n' "${SUMMARY[@]}"
if [[ $FULL_RUN -eq 0 ]]; then echo "Affected run (lane checkpoint): integration and pre-merge runs stay full."; fi
exit "$FAILED"
