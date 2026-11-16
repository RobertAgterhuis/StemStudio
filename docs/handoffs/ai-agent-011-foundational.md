# Hand-off: ai-agent / 011 foundational (T012-T017)

Branch `011-lane-agent` (from `011-composer-agent` 15210c6). No LLM called; no key anywhere.

## What was built

- **Tools** (`src/StemStudio.Agent/Tools`): `AgentToolDescriptor` (contract values + handler/estimator/UI mapper,
  URL / imitation / lyrics argument lists, target argument), `IAgentToolProvider`, `AgentToolRegistry` (duplicate names
  and > 32-tool page profiles rejected at start-up; `For(context)` = page profile groups + extras, minus `+mixer` /
  `+service generation|voice`, sorted by name; `Describe` with unavailable reasons -> `AgentToolInfoDto`),
  `ToolSelection` (the four page profiles of catalogue 1.0.0: 32 / 32 / 32 / 29), `ToolContext`, `ServiceAvailability`,
  `ToolSchema`/`ToolParameter`/`Param` (closed-object JSON Schema for the model), `ToolArgumentValidator` (types,
  ranges, enums, uuid, pattern, nested objects/arrays, `AtMostOne`/`AtLeastOne` rules, 8 KB), `CanonicalJson` (sorted
  keys, minimal escaping, SHA-256 lower-hex), `ResultProjector` (forbidden-field safety net, 4 KB cap with `truncated`
  marker, taint on non-empty `untrusted_text`, `Untrusted()` cleaning), `ToolInvocation`/`ToolResult`/`ToolEstimate`.
- **Policy** (`src/StemStudio.Agent/Policy`): `ToolPolicy.Decide` (pure), `ProvenanceChecker` (URL literal substring with
  whitespace normalisation; lyrics after the 010 normalisation), `ImitationGuard` + `ImitationLexicon` (copy of the 009
  v1 lists), `ApprovalBinding` (approved + identical hash; plan-scope grant), `PolicyInput`/`PolicyDecision`/`PlanHeavyGrant`.

Design note (deviation from the data-model 6 pseudo code, stricter): `tainted` is added for **every** non-read risk
(also heavy/destructive) so the card always explains "proposed after reading external content"; as a consequence a
plan-scoped approval never auto-covers a tainted heavy step.

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T012 / T013 | 105c6bf | faac53e |
| T014 / T015 | e260e63 | 5ff35d3 |
| T016 / T017 | 153031a | 8e73b7a |

### Security fix after review (coordinator finding on `ToolPolicy.CoveredByPlan`)

A plan-scoped (`plan_heavy`) approval covered more than was approved: (1) only the first string of an array target
was compared, so a grant for `A` covered `["A","B"]`, and non-string targets yielded null; (2) a null grant target
matched a null call target, so a target-less grant covered any later target-less heavy step; (3) the grant was not
bound to the tool, so it covered any other heavy tool in the plan. Fixed: `PlanHeavyGrant(ToolName, TargetId)`,
`ApprovalBinding.Grant(approval, toolName, targetId)` returns null without a target, and coverage requires the same
tool plus a target argument that is exactly one JSON string equal to the grant's non-null target. Decision (data-model
4.3, plan scope is per target): **target-less heavy steps are never plan-covered**.

| Pair | Red | Green |
|---|---|---|
| plan-scope bypasses (array / non-string / missing / null target, null-to-null, other tool, injected other track) | 8fa4619 | 9d6da08 |

## Tests and coverage

`dotnet test tests/StemStudio.Agent.Tests -p:CollectCoverage=true`: **252 passed**, 0 failed (after the security fix).
`StemStudio.Agent`: **line 98.53 %, branch 91.39 %, method 94.19 %** (gate 90/90 passed); `Policy/*` **100 % line and
branch**. `ToolInvocation` is not yet
exercised (providers arrive in US1).

## Quality checks

`dotnet format --verify-no-changes` (src + tests): clean. Build: 0 warnings (TreatWarningsAsErrors).
`uv run python tools/quality/sig_check.py src/StemStudio.Agent`: 65 files, 125 units, **0 errors, 9 warnings**
(unit size 17-22 LOC / complexity 6-8 in `ToolArgumentValidator`, `CanonicalJson.Write`, `ToolSchema`, `ToolPolicy.Decide`;
refactor in T079b).

## Dependencies added

None.

## Docs impact

None (library internals).

## Open issues / requests

1. **architect**: `ImitationLexicon` duplicates the 009 v1 resource lists because `StemStudio.Agent` may not reference
   Domain. Preferred: link `src/StemStudio.Domain/Styles/{imitation-phrases,artist-denylist,genre-words}.v1.txt` as
   embedded resources in `StemStudio.Agent.csproj` (architect-owned) so the copy can go.
2. **architect**: data-model 4.3 / 6 could state explicitly that a `plan_heavy` grant is bound to the approved tool and
   its single target, and that target-less heavy steps are never plan-covered (my lane does not own data-model.md).
3. **quality-gate**: the lexicon file is data (no logic); exclude it from duplication if it trips the check.

## Next owner

ai-agent continues with US1 (T024-T031).
