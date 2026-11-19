# 002 Project norm map
- Status: proposed · Date: 2026-11-19 · Area: norm / governance · Priority: P2

## Problem statement
The project carries its own norm (layered rules, decision records, its own skills, a release and documentation chain), but Tipoca
does not know it yet: Tipoca's practice and skills apply as if the project had none. `/tipoca:adopt` detected the norm below
without any manual mapping and drafted `.claude/tipoca/norm.json` with `status: "proposed"`. Nothing is active until the owner
approves it through the inbox.

### Detected layers and their precedence
```
1  User's explicit instruction in the session
2  Tipoca guardrails      hooks, W7 secrets, W8 outward changes, W10 no self-approval   (never shadowed)
3  Project norm          Conventions (conventions)
4  Tipoca practice       the rest of TIER0 and the Tipoca skills; other project instructions sharpen it
5  General defaults
```
| Layer | Label | Sources | Changed by |
|---|---|---|---|
| conventions | Conventions | `docs/public/src/user-manual`, `docs/public/src/technical-manual`, `.claude/agents`, `.agents/skills`, `.claude/skills` | - |

Confidence: core 0%.

### Decision records
- Record home `docs/decisions`: 37 records (26 accepted, 11 proposed); header formats lines; id pattern `^ADR-\d{3}$`; approval owner. Confidence 100%.

### Shadow table
Project skills: 18 (common prefix `speckit-` stripped). A shadow makes Tipoca load the project file instead of its own skill once approved.

| Project skill | Tipoca skill | Score | Match | Outcome |
|---|---|---|---|---|
| `speckit-analyze` | - | 0.03 | - | project only |
| `speckit-checklist` | - | 0.04 | - | project only |
| `speckit-clarify` | - | 0.04 | - | project only |
| `speckit-constitution` | - | 0.02 | - | project only |
| `speckit-converge` | - | 0.05 | - | project only |
| `speckit-implement` | - | 0.02 | - | project only |
| `speckit-plan` | - | 0.02 | - | project only |
| `speckit-specify` | - | 0.03 | - | project only |
| `speckit-tasks` | - | 0.03 | - | project only |
| `speckit-analyze` | - | 0.03 | - | project only |
| `speckit-checklist` | - | 0.04 | - | project only |
| `speckit-clarify` | - | 0.04 | - | project only |
| `speckit-constitution` | - | 0.02 | - | project only |
| `speckit-converge` | - | 0.05 | - | project only |
| `speckit-implement` | - | 0.02 | - | project only |
| `speckit-plan` | - | 0.02 | - | project only |
| `speckit-specify` | - | 0.03 | - | project only |
| `speckit-tasks` | - | 0.03 | - | project only |

Numbered rules that shadow a stack-rules reference:
- `docs/public/src/technical-manual/06-apis.md` → `stack-rules/integration-api` (0.7)
- `docs/public/src/technical-manual/08-security.md` → `stack-rules/arch-files-and-security` (0.67)

### Mirror drift
- `.codex/agents/architect.toml`: codex, informational (other tools: no Tipoca guardrails).
- `.codex/agents/beat-analysis.toml`: codex, informational (other tools: no Tipoca guardrails).
- `.codex/agents/generative.toml`: codex, informational (other tools: no Tipoca guardrails).
- `.codex/agents/qa.toml`: codex, informational (other tools: no Tipoca guardrails).
- `.codex/agents/stem-separation.toml`: codex, informational (other tools: no Tipoca guardrails).

### Stages, doc targets and release units
No staged process found in the root instructions.

Doc targets: 8. Builder commands are shown here only; they never run in audit mode.

| Target | Kind | Unit | Build | Outputs | Publish |
|---|---|---|---|---|---|
| scripts-soundtouch | other | project | `pwsh -NoProfile -File scripts/build-soundtouch.ps1` | `scripts/` | - |
| contracts-generate-render-vectors-exit-0 | other | project | `python specs/007-export-library/contracts/generate_render_vectors.py --check exit 0` | `specs/007-export-library/contracts/` | - |
| contracts-generate-render-vectors | other | project | `python specs/015-play-along-recording/contracts/generate_render_vectors.py` | `specs/015-play-along-recording/contracts/` | - |
| contracts-generate-render-vectors-2 | other | project | `python specs/019-arrangement-editor/contracts/generate_render_vectors.py` | `specs/019-arrangement-editor/contracts/` | - |
| docs-build | other | project | `node tools/docs/build.mjs` | `tools/docs/` | - |
| site-build | other | project | `node tools/site/build.mjs` | `tools/site/` | - |
| lib-render | other | project | `node tools/site/lib/render.mjs` | `tools/site/lib/` | - |
| site-review | other | project | `node tools/site/review.mjs` | `tools/site/` | - |

Release units: 0.
- Gates: sonar = OK (sonar-project.properties).

## Impact
Once approved, Tipoca enforces and shows the project's own norm instead of working past it:
- the precedence is injected once per session from the norm map (no Tier header needed in each file), and no core guard applies (no core layer);
- 0 project skill(s) and 2 rule file(s) replace their Tipoca counterpart (shadows);
- 37 decision record(s) get one lifecycle and one index; 11 proposed record(s) get an inbox approval;
- 0 stage gate(s), 8 doc target(s) and 0 release unit(s) become visible on the site and in `/tipoca:status`;
- the mirror gate checks 0 instruction mirror(s).

## Risk
Gaps and risks found by the detection (each needs the owner's eye before approval):
- Detection is heuristic (risk R-8): every entry is a proposal with its evidence; `changes` in the inbox re-runs the detection with the owner's note.
- Repository commands (doc builders, bump script) are only listed. They never run in audit mode or from a hook; in factory mode they run on request, shown first, dry run first.

## Effort
S (under a day): review this proposal and the draft `norm.json`, answer the inbox items. No project file changes.

## Step-by-step plan
1. Review the draft `.claude/tipoca/norm.json` against this proposal (`node "${CLAUDE_PLUGIN_ROOT}/scripts/norm.mjs" check` must pass).
2. Answer the inbox approval **Approve the project norm map** (`approve`, `changes` or `stop`) and the questions on ambiguous shadows or conflicts.
3. On `approve`, Lama Su sets `status: "approved"` with the approval reference at pickup (W10: never by the proposer). On `changes`, the detection runs again with the note.
4. Verify at the next session start: the norm block lists the layers and the active shadows; `/tipoca:status` shows the norm.

What Tipoca will do differently once approved:
- know which documents a change or a version touches and run the project's builders (factory mode only);

## Dependencies and related proposals
- `tipoca:project-norm` (precedence and shadowing), `tipoca:knowledge-change` (records), the doc chain and release skills.
