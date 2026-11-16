# Hand-off: quality-gate / 011 polish (T079g)

Branch `011-lane-quality`. Follows `quality-gate-011-foundational.md`.

## Scope of this run (important)

The ai-agent, backend and frontend lanes are still building on their own branches, so this
polish check covers only what is on `011-lane-quality`: the Phase 0 skeleton of
`StemStudio.Agent` plus the new gate rules. **The final SIG run over the merged 011 code is part
of the CTO integration (T080).** That run should use the command below. It is a gate run: any
error (forbidden edge, package, SDK namespace outside `Backends`, cycle, production clone, hard
limit) blocks the merge.

```text
uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md
```

## Results on this branch

```text
quality gate: 1841 files, 14023 units, 146 clones, 1 exemptions, 0 errors, 549 warnings -> exit 0
dependency_check: 0 errors, 0 warnings
uv run --directory tools/quality pytest --cov --cov-branch   590 passed, 100.00 % line and branch
ruff check / ruff format --check / mypy                      clean
```

- `Agent` row: 26 units, 123 LOC, 5 / 5 / 4 stars (parameters 76.4 / 23.6 / 0).
- `tools/agent-eval` row: 2 units, 19 LOC, 5 / 5 / 5 stars.
- No finding is owned by ai-agent. The 1 exemption is the existing
  `ListLogger::Log<TState>`.

Production soft-limit warnings by owner (all from before 011, none are errors):

| Owner | Unit size | Complexity | Razor `@code` | Clones |
|---|---:|---:|---:|---:|
| backend | 38 | 14 | - | - |
| frontend | 15 | 4 | 5 | - |
| generative | - | - | - | 10 (cross-service, accepted 009/010) |
| architect | - | - | - | 4 (cross-service, accepted 009/010) |
| stem-separation | - | 1 | - | - |

## Refactor requests per owner

- **ai-agent** (for your wave, checked at T080):
  - Keep the Phase 0 numbers: units <= 15 LOC, CCN <= 5, <= 2 parameters where possible. The
    hard limits fail the merge.
  - Keep `OllamaSharp`/`Anthropic` inside `Backends/**`.
  - `Policy` must not use `Runtime` or `Backends`.
  - Policy handlers with many arguments: pass a parameter object (risk, taint, provenance,
    approval) rather than 5+ parameters.
- **backend**: `Api` -> `Agent` is now allowed. The new `LoopbackActions` and the agent
  endpoints should not add to the 38 unit-size warnings. Split the port group implementations
  per feature, as the ports are split.
- **frontend**: `src/StemStudio.Web/Components/Help/HelpTip.razor:17` has a 32-line `@code`
  block (limit 30). This is a soft limit; please move the logic to a code-behind. The new agent
  panel should start from code-behinds.
- Everything else is unchanged from the previous report (`docs/quality/SIG-REPORT.md`,
  "Refactor requests per owner").

## Open issues / requests

- **CTO (T080)**: run the gate over the merged code. Then check:
  - `Agent` and `tools/agent-eval` rows;
  - any `(data copy: ...)` clone note on `Policy/ImitationLexicon.cs`, which is the expected
    warning.
- **CTO**: the decision on the Policy rule scope (T007 vs plan text) is still open; see the
  foundational hand-off.
- **CTO / devops**: `sonar.sources` does not include `tools/agent-eval` (outside this lane's
  write scope).

## Files changed

- `docs/handoffs/quality-gate-011-polish.md`
- `specs/011-ai-composer-agent/tasks.md` (T079g ticked, with the T080 note)

## Next owner

CTO (T080 integration).
