# Hand-off: quality-gate / 011 foundational (T007, T008)

Branch `011-lane-quality` (from `011-composer-agent` at 15210c6). This closes architect hand-off
open issue 5, the dependency map for `StemStudio.Agent`. T079g (HO:polish) follows.

## What was built

- **Project map** (`dependency_check.DOTNET_ALLOWED`):
  - `StemStudio.Agent -> {StemStudio.Contracts}`.
  - `StemStudio.Api` may also reference `StemStudio.Agent`.
  - `StemStudio.Web` and `StemStudio.Persistence` still may not reference it. A reference from
    them is a `forbidden dependency` error.
  - The old warning `StemStudio.Agent is not in the dependency map` is gone.
- **Package rule** (new, `DOTNET_PACKAGES_ALLOWED`, `read_package_edges`, `check_packages`):
  - The `PackageReference`s of `StemStudio.Agent.csproj` must belong to the families
    `Microsoft.Extensions.AI`, `OllamaSharp` or `Anthropic`.
  - A family name also allows `<name>.<anything>`, so `Microsoft.Extensions.AI.Abstractions` is
    allowed. A longer name such as `OllamaSharpExtras` is not.
  - Error text: `forbidden package StemStudio.Agent -> <package>`.
  - Projects that are not listed are not restricted.
- **SDK namespaces** (folder rule `StemStudio.Agent` -/- `OllamaSharp`, `Anthropic`):
  - The rule covers every C# file below `src/StemStudio.Agent/` except `Backends/**`. The
    exemption is a new mechanism, `DOTNET_FOLDER_EXEMPT`.
  - These count as references: `using`, `global using`, `using static`, alias directives, and
    qualified names (`new OllamaSharp.OllamaApiClient`, `global::Anthropic.X`).
  - These do not count: `AgentBackendDto.Anthropic` (member access), an enum member
    `Anthropic`, an identifier such as `OllamaModel`, comments and strings.
- **Policy rule**: `StemStudio.Agent/Policy` -/- `StemStudio.Agent.Backends`,
  `StemStudio.Agent.Runtime` (and every namespace below them).
- **Owners** (`common.OWNERS`):
  - architect: `src/StemStudio.Agent/{Ports,Model}/` and `StemStudio.Agent.csproj`.
  - ai-agent: the rest of `src/StemStudio.Agent/`, `tests/StemStudio.Agent.Tests/` and
    `tools/agent-eval/`.
- **Measured**:
  - New component row `Agent` (`src/StemStudio.Agent/`).
  - `tools/agent-eval` is now a default root with its own row (scope all). It is measured but is
    not production code, so its clones are warnings.
- **Data copies** (cross-owner request from the ai-agent lane):
  - Added `duplication.DATA_COPIES` (path -> mandatory reason).
  - A clone that touches a listed file is a warning ending in `(data copy: <reason>)`.
  - Listed: `src/StemStudio.Agent/Policy/ImitationLexicon.cs`, the C# copy of the 009 v1
    imitation lexicon (Agent may not reference Domain).
  - The Domain originals are `.txt` resources that jscpd does not scan, so this matters only if
    the copy clones other C# code.
  - The file is not on this branch yet; the entry covers it once the branches are merged.
  - If the architect replaces the copy with embedded `.txt` resources, remove the entry.
- `tools/quality/README.md` and `docs/quality/SIG-REPORT.md` are updated.

## Interpretation (please confirm or correct)

- **Policy rule follows T007, not the stricter plan text.** Plan.md says `Agent.Policy`
  references "only `Model` and `Tools`". T007 forbids only `Backends` and `Runtime`, and that is
  what I enforce. If the CTO wants the plan's version, add `StemStudio.Agent.Ports` and
  `StemStudio.Agent` (the root namespace) to the forbidden set. That is one line and one test,
  but forbidding the root namespace would also forbid Model and Tools, so it needs an allow-list
  form.
- **Known gap**: a partially qualified name that relies on the enclosing namespace is not
  detected. Example: `Runtime.RunLoop` written inside `namespace StemStudio.Agent.Policy`. This
  is the same limit as the 010 rules, documented in the README. Review catches it.

## Files changed

- `tools/quality/{dependency_check,duplication,common,components}.py`, `tools/quality/README.md`
- `tools/quality/tests/test_agent_rule.py` (new), `tools/quality/tests/test_data_copies.py`
  (new), `tools/quality/tests/test_components.py`
- `docs/quality/SIG-REPORT.md`
- `specs/011-ai-composer-agent/tasks.md` (T007 and T008 ticked)

## Tests and coverage

TDD:

- **ce82c09**: `test_agent_rule.py` committed failing (collection error: the new names were
  missing).
- **5c55b46**: implementation.
- The data-copy tests and the agent-eval root test were run and seen failing before the code.
  They are committed together with that code in 5c55b46.

```text
uv run --directory tools/quality pytest --cov --cov-branch
590 passed; TOTAL 100.00 % line and branch
```

## Quality checks

```text
uv run --directory tools/quality ruff check .            All checks passed!
uv run --directory tools/quality ruff format --check .   39 files already formatted
uv run --directory tools/quality mypy                    Success: no issues found in 38 source files
python tools/quality/sig_check.py tools/quality          38 files, 470 units, 0 errors, 0 warnings
python tools/quality/dependency_check.py                 dependency_check: 0 errors, 0 warnings
uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md
quality gate: 1841 files, 14023 units, 146 clones, 1 exemptions, 0 errors, 549 warnings -> exit 0
```

`Agent` row: 26 units, 123 LOC, size and complexity 100 % low, parameters 76.4 / 23.6 / 0.
`tools/agent-eval`: 2 units, 19 LOC, all low.

Per the machine constraint, only the quality-tool pytest and `run_all.py` were run. No
`dotnet test` and no `scripts/test-all.ps1`.

## Dependencies added

None.

## Open issues / requests

- **CTO**: the Policy rule scope (see Interpretation).
- **CTO / devops**: `sonar-project.properties` does not list `tools/agent-eval` in
  `sonar.sources`. The file is outside this lane's write scope; add it at integration if Sonar
  should see the harness.
- **ai-agent**: keep `OllamaSharp`/`Anthropic` usings and qualified names inside `Backends/**`.
  `AgentServiceCollectionExtensions.cs` must build backends through `Backends` factories and must
  not construct `OllamaApiClient`/`AnthropicClient` itself.

## Next owner

ai-agent, backend and frontend continue. quality-gate returns for T079g (HO:polish).
