# Hand-off: quality-gate / 011 follow-up claude-key, foundational (T017, T018)

Branch `ck-lane-quality` (worktree `.claude/worktrees/ck-quality`, from `011-claude-key-spec`). Nothing pushed or merged; no
docker; Anthropic never called; no key-shaped literal written (the rule tests use `key`/`apiKey` identifiers, never a value).
Only `pytest` on `tools/quality/tests` and `tools/quality/run_all.py` were run.

## What was built

- **`tools/quality/secret_rules.py`** (new, wired into `run_all.py` and the SIG report section "Claude key handling"; check name
  `secret`, every finding is an error):
  - **(a) Data Protection confinement**: in production `src/**/*.cs` and `*.razor` (comments and string literals ignored), the
    namespace `Microsoft.AspNetCore.DataProtection` and its entry points usable without a `using` (`AddDataProtection`,
    `IDataProtector`, `IDataProtectionProvider`, `IPersistedDataProtector`, `IDataProtectionBuilder`, `DataProtectionProvider`,
    `EphemeralDataProtectionProvider`, `PersistKeysToFileSystem`, `ProtectKeysWithDpapi`) only below
    `src/StemStudio.Api/Agent/Secrets/`; a `global using` of the namespace is an error **everywhere** (it would leak to the whole
    project); `StemStudio.Api.Agent.Secrets` is referenced only from `src/StemStudio.Api/` (so Web, Agent, Persistence,
    Contracts never see it); a `Microsoft.AspNetCore.DataProtection[.*]` `PackageReference` is an error in every `src` project
    but `StemStudio.Api`. One finding per line.
  - **(b)** `ClaudeApiKey` and `StoreClaudeKeyRequest` are not declared as `record` (any form: `record`, `record class`,
    `readonly record struct`, `partial record`, positional) in production code.
  - **(c)** in `src/StemStudio.Api/Agent/Secrets/**` and `src/StemStudio.Agent/Backends/**/Claude*`: no log call (`Log`,
    `Log{Trace..Critical}`, `BeginScope`, and every `[LoggerMessage]` method of the file at its call sites) passes an argument
    whose last name segment is `key`/`apiKey` (any case, leading `_`/`@`), calls `.Reveal()` in an argument, or names
    `{Key}`/`{ApiKey}` (with `@`, format or alignment) in its template; no `[LoggerMessage]` method takes such a parameter or
    names it in `Message`.
- **Owners** (`common.py`): `src/StemStudio.Agent/Model/ClaudeApiKey.cs` -> ai-agent (plan.md: architect stub, ai-agent T008;
  the rest of `Model/` stays architect); `tests/Shared/` -> architect (shared test doubles, T005); `scripts/` -> devops.
  `src/StemStudio.Api/Agent/Secrets/**` stays backend (Api prefix), `ClaudeKeyProvider` ai-agent (Agent prefix).
- **Measured rows**: `scripts` is a default root (devops' tested Python, `scripts/pyproject.toml` from `ck-lane-devops`) and a
  component row with scope "all"; `Api/Agent/Secrets` is a sub-row of `Api`. Sonar: `scripts` in `sonar.sources`,
  `scripts/tests` in `sonar.tests` and `sonar.test.inclusions`.
- **Not added**: a separate key-shaped-literal check. The plan delegates that to `scripts/secret_scan.py` (research R-07),
  which already scans every tracked and untracked file for the prefix followed by a key character; a second copy would
  duplicate the rule. The `[sec]` review (T051) covers how the tests search for leaks.

## Files changed

`tools/quality/secret_rules.py` (new), `tools/quality/{common,components,report,run_all}.py`, `tools/quality/README.md`,
`tools/quality/tests/test_claude_key_rules.py` (new), `tools/quality/tests/{test_components,test_run_all,test_sonar_args}.py`,
`sonar-project.properties`, `docs/quality/SIG-REPORT.md` (regenerated).

## Tests and coverage

Red: `083ed8ff` (collection error, module missing). Green: `73fd7159`.

```
cd tools/quality && uv run --frozen pytest -q --cov --cov-branch --cov-report=term
666 passed in 8.21s
secret_rules.py   175 stmts  0 miss  40 branches  0 partial  100%
TOTAL            1328 stmts  0 miss 274 branches  0 partial  100%   (line 100 %, branch 100 %)
```

Mutation probe on the real lane code (integrated snapshot, see the polish hand-off): a `string apiKey` parameter added to
`ProtectedClaudeKeyStore.LogStored`, a `key.Reveal()` log argument in `Agent/Backends/ClaudeLeak.cs` and a Data Protection
`using` in Web each gave one error; the unmodified lane code gives 0.

## Quality checks

`ruff check .` all passed; `ruff format --check .` 41 files formatted; `mypy` (strict) no issues in 40 files;
`sig_check.py tools/quality/secret_rules.py`: 25 units, 0 errors, 0 warnings.
`run_all.py --report docs/quality/SIG-REPORT.md` on this branch: **2143 files, 16657 units, 220 clones, 2 exemptions,
0 errors, 745 warnings -> exit 0**.

## Dependencies added

None.

## Docs impact

None for the public manuals (internal quality gate only).

## Open issues / requests

- **CTO**: after the lanes are merged, regenerate `docs/quality/SIG-REPORT.md` (this branch only has the Phase 0 skeleton;
  the integrated numbers are in `quality-gate-ck-polish.md`).

## Next owner

quality-gate T051 (polish hand-off `quality-gate-ck-polish.md`).
