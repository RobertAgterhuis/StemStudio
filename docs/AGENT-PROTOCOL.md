# Agent protocol (applies to every StemStudio agent)

Read this file, `.specify/memory/constitution.md`, `specs/001-stemstudio-mvp/spec.md`,
`specs/001-stemstudio-mvp/plan.md` and the contracts in `specs/001-stemstudio-mvp/contracts/`
before you start. The constitution is non-negotiable.

## Working rules

1. **Stay in your lane.** Only create or modify files inside the paths your agent definition
   owns. If you need a change elsewhere (a contract, a shared config, another agent's code),
   do not make it: describe it under *Open issues / requests* in your hand-off note.
2. **Contracts are fixed.** Implement exactly the OpenAPI contracts and interface definitions in
   `specs/001-stemstudio-mvp/contracts/`. If one is wrong, report it; do not silently diverge.
3. **Test-first.** Write a failing test, make it pass, refactor. Keep tests fast and deterministic;
   use fakes for networks, subprocesses, models and the clock.
4. **Quality limits** (SIG 4 stars or higher): <= 15 LOC per unit (hard max 30), cyclomatic
   complexity <= 5 (hard max 10), <= 4 parameters, no duplicated blocks of 6 or more lines,
   one-way dependencies. Run `python tools/quality/sig_check.py <your paths>` when it exists.
5. **Coverage >= 90% line AND branch** for your component. Report the exact numbers.
6. **Lint clean:** .NET `TreatWarningsAsErrors` + `dotnet format --verify-no-changes`; Python
   `ruff check`, `ruff format --check`, `mypy --strict`; TS `eslint` + `tsc --noEmit`.
7. **Document public APIs:** XML docs (C#), docstrings (Python), TSDoc (TS).
8. **Security:** no secrets in files or command lines; validate external input; subprocesses get
   argument lists (never `shell=True`); no path traversal.
9. **Open source only.** For every new dependency record name, version, licence and URL in your
   hand-off note (the CTO consolidates `docs/THIRD-PARTY-LICENSES.md`).
10. **Commit** your work in your worktree with Conventional Commits (`feat(worker): ...`), ending
    each message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Do not edit
    `docs/STATUS.md` or `docs/THIRD-PARTY-LICENSES.md` (CTO-owned).

## Hand-off note

Finish every task with `docs/handoffs/<agent>-<task>.md`:

```markdown
# Hand-off: <agent> / <task>
## What was built
## Files changed
## Tests and coverage   (commands run + exact line/branch % + pass counts)
## Quality checks       (lint/analyzers/sig_check output summary)
## Dependencies added   (name | version | licence | url)
## Open issues / requests
## Next owner
```

Never claim "done" without pasting the real test and coverage output summary.
