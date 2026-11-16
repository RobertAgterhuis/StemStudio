# Hand-off: devops / 011 polish (T079f)

Branch `011-lane-devops`, worktree `.claude/worktrees/011-devops`. Closes the devops lane of 011
(T009, T010, T059, T079f); see `devops-011-foundational.md` and `devops-011-us3.md`.

## What was built

- Validation of everything the lane changed (results below).
- Fix found while validating: Git Bash on Windows rewrites an env value such as
  `/scripts/us1-command.json` into `C:/Program Files/Git/scripts/...` before compose sees it.
  `scripts/agent-e2e.sh` now exports `MSYS_NO_PATHCONV=1` (no effect on Linux/macOS), and the
  compose header tells Git Bash users to prefix it when they set `STEMSTUDIO_AGENT_FAKE_SCRIPT`
  by hand.
- CI `generator-image` job: the compose check also interpolates the fake agent variables
  (`STEMSTUDIO_AGENT_BACKEND=fake STEMSTUDIO_AGENT_FAKE_SCRIPT=/scripts/ci.json ... config -q`).

## Files changed

`scripts/agent-e2e.sh`, `docker-compose.yml` (comment only), `.github/workflows/ci.yml`,
`specs/011-ai-composer-agent/tasks.md` (T079f ticked), this note.

## Tests and coverage

No test suite and no `compose up/down` run (instruction; three lanes build on this PC and the
user's stack on :8080 stays untouched). The lane adds no product code; the executable checks are:

- `python scripts/secret_scan.py --self-test` -> passed (3 planted leaks, allowed forms clean)
- `python scripts/secret_scan.py` -> 3104 files clean, exit 0
- `scripts/coverage_floor.py` on synthetic reports (met / below / missing / `--min`) -> 0 / 1 / 1 / 0
- `scripts/agent-e2e.{sh,ps1}` without the spec -> skip, exit 0; wrong usage -> 2 / ValidateSet

## Quality checks

- `docker compose config -q` (ANTHROPIC_API_KEY unset): default; `+gpu`; `+gpu+generative`;
  `+generative+generative-fake`; default with the fake agent variables -> all valid. Rendered api
  env: `ANTHROPIC_API_KEY: null` (absent in the container), `StemStudio__Agent__Backend: ollama`,
  `StemStudio__Agent__Ollama__BaseUrl: http://host.docker.internal:11434`,
  `STEMSTUDIO_AGENT_FAKE_SCRIPT: ""`, `extra_hosts: host.docker.internal=host-gateway`,
  bind `./e2e/agent-scripts -> /scripts` read-only.
- `actionlint` 1.7.12 (rhysd/actionlint container, network none; shellcheck of `run:` blocks
  included): 0 findings over `ci.yml`, `e2e-real.yml`, `pages.yml` and the `fake-stack` action.
- `shellcheck` 0.11.0: `scripts/agent-e2e.sh`, `scripts/test-all.sh`, `scripts/dev.sh` clean.
- PowerShell 7 parser: 0 errors in `dev.ps1`, `test-all.ps1`, `agent-e2e.ps1`; PSScriptAnalyzer
  1.25.0 (Warning, Error): clean.
- Python (`scripts/secret_scan.py`, `scripts/coverage_floor.py`): `ruff check`,
  `ruff format --check`, `mypy --strict` clean; `tools/quality/sig_check.py`: 13 units, 0 errors,
  0 warnings.
- Redocly 2.78.0 lint of `specs/011-ai-composer-agent/contracts` (backend 1.10.0 + agent delta),
  the exact CI step: valid, 10 warnings (the known 8 + 2 `operation-4xx-response`).
- `docker`/images: no Dockerfile changed in 011, so no image rebuild was needed.

## Dependencies added

None.

## Docs impact

As in the foundational and US3 notes (Ollama optional on the host, `STEMSTUDIO_AGENT_BACKEND`,
the key from the shell only, `--agent-e2e`); plus: Git Bash users set `MSYS_NO_PATHCONV=1` when
passing a `/scripts/...` path by hand.

## Open issues / requests

Still open from `devops-011-us3.md`: qa to confirm the tag convention (`@agent`,
`@agent-unavailable`, `@<script basename>`, `@generative`; `npm test` excludes `@agent`), and the
CTO decisions on per-script api restarts and on the backend of the visual/a11y/lighthouse jobs.
From `devops-011-foundational.md`: backend treats an empty `STEMSTUDIO_AGENT_FAKE_SCRIPT` as
unset; `e2e/agent-scripts/` must exist after the merge.

## Next owner

CTO (T080: merge, then the full CI incl. the agent E2E passes).
