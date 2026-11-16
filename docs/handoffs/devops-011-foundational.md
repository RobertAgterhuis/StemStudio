# Hand-off: devops / 011 foundational (T009, T010)

Branch `011-lane-devops` (from `011-composer-agent` @ 15210c6), worktree `.claude/worktrees/011-devops`.

## What was built

### T009 - compose and dev scripts (DELTA.md 7, FR-018)

- `docker-compose.yml`, service `api`:
  - `environment` switched to list form so the Claude key is the literal bare entry
    `- ANTHROPIC_API_KEY` (the host's value when set, otherwise absent in the container; no value
    in any file). Existing entries unchanged; the generative overlay's mapping still merges
    (checked with `docker compose config`).
  - `StemStudio__Agent__Ollama__BaseUrl=${STEMSTUDIO_OLLAMA_URL:-http://host.docker.internal:11434}`,
    `StemStudio__Agent__Backend=${STEMSTUDIO_AGENT_BACKEND:-ollama}`,
    `STEMSTUDIO_AGENT_FAKE_SCRIPT=${STEMSTUDIO_AGENT_FAKE_SCRIPT:-}` (empty by default).
  - `extra_hosts: ["host.docker.internal:host-gateway"]` (Linux hosts and CI runners too).
  - Read-only bind `./e2e/agent-scripts:/scripts:ro` (always mounted; ignored unless the backend
    is `fake`).
  - Header comments: Ollama optional and on the host (not in compose), how to set the key for one
    shell session (`read -rs` + `export` / `Read-Host -MaskInput`), never in `.env`.
- `scripts/dev.sh` / `scripts/dev.ps1`: `init_agent_environment` / `Initialize-AgentEnvironment`
  set `StemStudio__Agent__Ollama__BaseUrl` (default `http://localhost:11434`) and
  `StemStudio__Agent__Backend` (default `ollama`) from the same shell variables; the key is
  inherited unchanged and only reported as `set`/`unset`.

### T010 - CI and test-all (SC-006)

- `scripts/secret_scan.py` (stdlib; tracked + untracked-not-ignored files via `git ls-files`):
  rules `anthropic-key` (the `sk-ant-` prefix followed by a key character, anywhere),
  `key-value-assigned` (the variable assigned a 16+ character key-like value; references `$X`,
  `${X}`, placeholders `<...>` and commands such as `Read-Host` stay allowed) and `ci-sets-key`
  (under `.github/` only: the variable as a YAML key, or `secrets.ANTHROPIC*`). Findings print
  `path:line` and the rule as `::error` annotations, never the value. `--self-test` plants three
  leaks and two allowed forms in a temp dir and requires exactly the three findings; `--root DIR`
  scans a plain directory.
- `scripts/coverage_floor.py <cobertura> [--min 90]`: fails when the report is missing or below
  the floor (the coverlet threshold in `Directory.Build.props` stays the real gate; this stops a
  component dropping out unnoticed).
- `.github/workflows/ci.yml`:
  - `dotnet` job: step "StemStudio.Agent is in the gate" after `dotnet test` (the Agent tests run
    in the solution test step already).
  - `contracts` job: Redocly lint of `specs/011-ai-composer-agent/contracts`
    (`backend-api.openapi.yaml` 1.10.0 + `agent-api.openapi.yaml`, its `redocly.yaml`); env
    `AGENT_CONTRACTS`. The 008 worker and 009/010 generator lint steps are unchanged.
  - New job `secret-scan` (self-test, then scan); `e2e` now `needs` it.
  - Header: the key is never set in CI.
- `scripts/test-all.{sh,ps1}`: steps "agent coverage floor (011)", "contracts lint
  (011-ai-composer-agent)", "secret scan self-test", "secret scan".

## Files changed

`docker-compose.yml`, `scripts/dev.sh`, `scripts/dev.ps1`, `scripts/test-all.sh`,
`scripts/test-all.ps1`, `scripts/secret_scan.py` (new), `scripts/coverage_floor.py` (new),
`.github/workflows/ci.yml`, `specs/011-ai-composer-agent/tasks.md` (T009, T010 ticked).

## Tests and coverage

The full suite was NOT run (instruction: three other lanes build on this PC). Run instead:

- `python scripts/secret_scan.py --self-test` -> `passed (3 planted leaks, allowed forms clean)`
- `python scripts/secret_scan.py` -> `3100 files clean`, exit 0
- `python scripts/coverage_floor.py` on synthetic reports: 100/95 -> met (0); 95/85 -> BELOW (1);
  missing file -> 1; `--min 80` -> met (0); bad usage -> 2
- No unit-test project exists for `scripts/*.py` (same as `cpu_image_guard.py` etc.); the
  self-test is the executable check of the scan rules and runs in CI and test-all.

## Quality checks

- `docker compose config -q`: default, `+gpu`, `+gpu+generative`, `+generative+generative-fake`
  all valid. Rendered api env without the key: `ANTHROPIC_API_KEY: null` (absent in the
  container); with a shell value it is passed through.
- `actionlint` 1.7.12 (container, includes shellcheck on `run:` blocks): 0 findings, all workflows.
- `shellcheck` 0.11.0: `scripts/test-all.sh`, `scripts/dev.sh` clean (fixed SC2209 in dev.sh).
- PowerShell: parser 0 errors; PSScriptAnalyzer 1.25.0 warnings/errors: none (dev.ps1 has
  pre-existing information-level positional-parameter notes only).
- Python: `ruff check`, `ruff format --check`, `mypy --strict` clean for both new scripts;
  `tools/quality/sig_check.py`: 13 units, 0 errors, 0 warnings.
- Redocly lint of the 011 documents was not re-run by me; the step's command is the architect's
  (valid, 8 + 2 known `operation-4xx-response` warnings, architect-011-plan.md).

## Dependencies added

None (stdlib Python only; no new action, image or package).

## Docs impact

Technical manual (setup): optional Ollama on the host (`STEMSTUDIO_OLLAMA_URL`, default
`http://host.docker.internal:11434` in compose, `http://localhost:11434` without Docker),
`STEMSTUDIO_AGENT_BACKEND` (`ollama|claude|none|fake`), how to set `ANTHROPIC_API_KEY` for one
shell session only (never in `.env` or a file), and that `docker compose up -d` recreates the api
to pick it up.

## Open issues / requests

1. **backend / ai-agent**: inside the container the backend arrives as
   `StemStudio__Agent__Backend` (the shell variable `STEMSTUDIO_AGENT_BACKEND` is mapped by
   compose and the dev scripts); only `STEMSTUDIO_AGENT_FAKE_SCRIPT` keeps its name and is an
   empty string when unset - treat empty as unset.
2. **qa**: `e2e/agent-scripts/` must exist after the merge (the bind mount creates an empty,
   root-owned folder on Linux otherwise).
3. **quality-gate**: `tools/quality` may flag `scripts/secret_scan.py` / `coverage_floor.py` in the
   dependency map like the other `scripts/*.py`; no exemption needed (sig_check clean).

## Next owner

devops (T059, e2e agent runs), then CTO at merge.
