# Hand-off: quality-gate / round 022+023 wave 2 (022 T024, T089; 023 T071)

Branch `r22-lane-quality` (from round head `8f77df3e`). No push, no merge, no Docker.

## Commits

| Commit | Content |
|---|---|
| `50e02055` | test: fixtures for the new rules (red: 96 failed, 13 passed) |
| `27aa128e` | feat: the dependency rules (green) |
| `3ebcc04f` | feat: report rows, vendored dictionary and fixture exclusions, README |
| (this hand-off) | docs: hand-off, T024 ticked in 022 tasks.md |

## What was built

**Dependency rules (T024, T071)** in `tools/quality/dependency_check.py` (tables, same machinery as the
011 to 013 rules; findings read `forbidden dependency <folder> -> <name>`). The project rules stay in force.

| Folder below `src/` | Rule |
|---|---|
| `StemStudio.Domain` (project) | no `PackageReference` at all (`DOTNET_PACKAGES_ALLOWED["StemStudio.Domain"] = {}`) |
| `Domain/Library`, `Domain/Lyrics` (incl. `Assist/`) | nothing outside Domain: no other StemStudio project, no `Microsoft.*`, no `WeCantSpell`, not the sibling feature |
| `Api/Library` | no `Web`, `Agent`, `Api.LyricAssist` (so: Domain, Persistence, Contracts, other Api folders) |
| `Api/LyricAssist` | no `Web`, `Api.Library` (Agent, Domain, Persistence, Contracts allowed) |
| `Web/Library`, `Web/Components/Library` | Contracts and `Web.*` only; no `HttpClient`, `IHttpClientFactory`, `System.Net.Http*`, `WeCantSpell`; not the assist UI |
| `Web/Components/Lyrics/Assist` | same, and not `Web.Library` / `Web.Components.Library` |
| `Agent/LyricAssist` | no `Api`, `Domain`, `Persistence`, `Web`, `WeCantSpell` (Agent -> Contracts only; Domain checks come through the `ILyricVerification` port) |
| `Agent/Tools` (allow-list) | only `Agent.Model`, `Agent.Ports` (and itself); never `Runtime`, `Policy`, `Backends`, `LyricAssist` |

Machinery changes: folder rules now also read `*.razor` (comments `@* *@` and `<!-- -->` ignored);
new edge patterns for direct HTTP use; `DOTNET_EXTERNAL_NAMESPACES` split into SDK namespaces (Agent, unchanged
rule), framework namespaces (`Microsoft`, `WeCantSpell`) and HTTP names. `Web -> API client -> API` is
enforced as "UI folders do not name HttpClient; only `Web/Api` does".

**Measurement (T089, T071)**: report rows `Library (022)` and `Lyric assist (023)` (sub-rows across Domain, Api,
Web, Agent, also counted in the project rows); `Api/LyricAssist/Dictionaries/` (vendored Hunspell and CMUdict,
~10 MB data) excluded from SIG (`common.EXCLUDED_PATHS`), jscpd (`.jscpd.json`) and Sonar (`sonar.exclusions`,
`sonar.cpd.exclusions`); `tests/fixtures/lyrics/assist/**` excluded from Sonar. `sonar.sources=src` and
`sonar.tests=tests` already cover all new code and tests, so no new source path was needed (tested).

Existing test adjusted: `test_agent_graph_is_clean` gave every project the three AI packages; Domain now has an
empty package list.

## Tests and checks (tools/quality)

```
uv run --directory tools/quality pytest --cov --cov-branch   -> 901 passed (772 before), TOTAL 1450 stmts, 0 miss,
                                                                 292 branches, 0 partial, 100% line and branch
ruff check . / ruff format --check .  -> clean;   mypy (strict)  -> no issues in 47 files
python tools/quality/run_all.py (this branch, real tree)   -> 0 errors, 1096 warnings, exit 0
```

New test files: `test_library_rules.py`, `test_lyric_assist_rules.py` (violation fixtures per folder, allowed
fixtures incl. comments/strings/Razor, real-tree checks), `test_library_lyrics_measurement.py` (exclusions, rows,
owners). The "real tree" tests run against the repository, so they re-check the merged code at integration.

## Rules against the real lane code (pass 1: dependency rules)

I overlaid the changed `src/` files of `r22-lane-be022`, `-be022b`, `-be023`, `-agent`, `-fe022`, `-fe023` on
`8f77df3e` in a scratch tree and ran the gate there (the scratch tree is not committed).

**Dependency rules: 0 violations** (also all older rules). Coverage proof: the new folders contribute
Domain/Library 12 files, Domain/Lyrics 35, Api/Library 21, Api/LyricAssist 27, Web/Library 12,
Web/Components/Library 33 (19 Razor), Web/Components/Lyrics/Assist 43 (18 Razor), Agent/LyricAssist 31,
Agent/Tools 35 files. No lane needs to change for T024/T071.

## Rules against the real lane code (pass 2: SIG and duplication) - BLOCKS the integration gate

Baseline `8f77df3e` (src + tests): 0 errors. Overlay: **16 errors** (7 parameter breaches, 9 production clones),
`run_all.py` would exit 1. SIG estimate production: unit size 93.2 -> 90.9 % low, complexity 98.7 -> 97.4 % low,
interfacing unchanged 76.5 %; house estimate 4.8 stars (Sigrid is authoritative). Feature rows (overlay):

| Row | Units | LOC | Size % | Complexity % | Params % | Stars | Clones |
|---|---:|---:|---|---|---|---|---:|
| Library (022) | 352 | 2233 | 89.6 / 10.4 / 0.0 | 96.9 / 3.1 / 0.0 | 80.3 / 19.7 / 0.0 | 5 / 5 / 4 | 6 |
| Lyric assist (023) | 668 | 4675 | 75.8 / 24.2 / 0.0 | 87.3 / 12.7 / 0.0 | 74.6 / 25.2 / 0.2 | 4 / 5 / 3 | 3 |

### Refactor requests (hard-limit breaches, must be fixed before integration)

Parameters > 4 (tests count for this limit too):

| Lane | File:line | Unit | Request |
|---|---|---|---|
| ai-agent (023) | `src/StemStudio.Agent/LyricAssist/Fakes/AdversarialLyricModel.cs:63` | `Item` 6 params (production) | pass a small record/parameter object |
| ai-agent (023) | `tests/StemStudio.Agent.Tests/LyricAssist/LyricFixtures.cs:101` | `Item` 5 | parameter object |
| ai-agent (023) | `tests/StemStudio.Agent.Tests/LyricAssist/SuggestionValidatorKindsTests.cs:10` | `Rhyme` 6 | `[Theory]` with a data record |
| ai-agent (022) | `tests/StemStudio.Agent.Tests/Tools/Providers/Library022ToolProviderTests.cs:283` | `State` 5 | parameter object |
| ai-agent (023) | `tests/StemStudio.Agent.Tests/Tools/Providers/LyricAssist023ToolProviderTests.cs:89` | `SuggestLyricLines_MapsTheModes` 5 | data record per case |
| backend-022 | `tests/StemStudio.Api.Tests/Library/SnapshotBuilder.cs:41` | `Export` 5 | parameter object |
| backend-022 | `tests/StemStudio.Domain.Tests/Library/PlacementResolverTests.cs:22` | `Found` 5 | parameter object |

Duplication in production code (jscpd, min 6 lines / 40 tokens):

| Lane | Clone | Request |
|---|---|---|
| backend-022 / backend-023 | private `Check(List<DomainError>, bool, string, string)` is copied in `Api/Library/FolderViewRequest.cs:49`, `Api/Library/SearchQueryRequest.cs:25`, `Api/LyricAssist/AssistRequestRules.cs:100` and the 007 file `Api/Assets/AssetListQuery.cs:47` (3 clones, 8-11 lines) | one shared helper in `Api/QueryParsing.cs` (e.g. `QueryParsing.Require(errors, valid, field, message)`); backend-022 adds it, backend-023 uses it (or each lane owns one side after the CTO merges 022 first) |
| backend-022 | `Api/Library/LibraryBrowseService.cs:27` and `:46` (8 lines: `FolderAsync` and `SearchAsync` validate-snapshot-respond) | extract the shared "validate, load snapshot, respond with ETag" step |
| backend-023 | `Api/LyricAssist/LyricAssistEventWriter.cs:110` = `Api/Agent/SseWriter.cs:117` (`WriteAsync`, 8 lines) | make one `internal` SSE write helper and use it in both (SseWriter is the older file) |
| backend-023 | `Api/LyricAssist/LyricAssistEndpoints.cs:67` = `Api/Agent/AgentRunEndpointService.cs:144` (`TryPosition`, 10 lines) | one shared `StreamPosition.TryParse(HttpRequest, out int)` |
| ai-agent | `Agent/Tools/Providers/LibraryToolProvider.cs:2` = `ScoreToolProvider.cs:1` (7-line using block) | alphabetise/trim the `using` block (the clone is only the shared header), or add the missing `using`s via a `GlobalUsings` entry |
| frontend-022 | `Web/Components/Library/AssetTable.Actions.cs:65` and `:75` = `LibraryPage.Actions.cs:22` and `:31` (8 + 16 lines: `RenameAsync`, `DeleteAsync`, rename/delete state) | move rename/delete into one shared component partial or `AssetActions` helper used by both the table and the page |

Owner mapping for `owner_of`: all four Api/Domain rows are `backend`, Web `frontend`, Agent `ai-agent`.
Note: `r22-lane-be023` already contains the ai-agent LyricAssist files (it was built on the agent lane), so the
Agent breaches show in both branches; fix them on the agent lane.

### Soft-limit warnings (not blocking, for information)

Production warnings in lane files: ai-agent 54 (34 unit size, 20 complexity), backend-023 68 (41 / 27),
backend-022 9, frontend-022 11, frontend-023 1. Worst production units: `SuggestionCandidate::VerifyRhyme` (CCN 10),
`EnglishPhonology::Rule` (CCN 10), `FolderView::EditTrackInfo` (CCN 10, `FolderView.Filing.cs:16`),
`LyricAssistant::CallAsync` / `ParsedLyricOutput::Miss` (CCN 9), `AssistPreferencesService::CreateMarkAsync` and
`WordMiss::Classify` (CCN 9). Several units sit at CCN 10, exactly on the hard limit (above 10 fails); one more branch breaks the gate.
Persistence unit size 5 -> 4 stars (size 90.9 -> 84.2 % low) is mostly new 16 to 30 NLOC units in the Persistence additions (for example
`LyricsSheetConfiguration::ConfigureAssist`, 30 NLOC, exactly at the limit); backend-023 may want to split it.

### Sonar (T089)

Not run: it needs the SonarQube server and `SONAR_TOKEN` (CI). The configuration is verified by tests: new
code is covered by `sonar.sources`/`sonar.tests`, data folders are excluded. At integration run the normal Sonar job
and send findings to the owners.

## Decisions

- Folder rules are namespace-based like the existing ones, so they also cover Razor (`@using`, `@inject`).
- `Microsoft.*` is forbidden in the two Domain feature folders (not in the whole of Domain; the project-level package
  rule already blocks packages there).
- Agent/Tools is an allow-list (Model, Ports) as the 022 task says "Agent tools -> Ports only"; Model was added because
  every provider uses its types and Policy does too. If the CTO wants strict Ports, the table entry is one line.
- Vendored dictionaries and the lyric gold fixtures are data, hence excluded from Sonar and jscpd; the licence record
  stays in `PROVENANCE.md` (backend-023).

## Deviations from contract or tasks

None. T024 is ticked in 022 `tasks.md` (own row). T071 and T089 stay **unticked**: they close when the merged tree passes
`run_all.py` (the requests above are done) and Sonar has run; I did not tick them to avoid claiming a pass that is not yet true.

## For the CTO at integration

1. Merge the lanes, then run `uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md`
   as the very last doc step. **`docs/quality/SIG-REPORT.md` was not regenerated here** (as instructed); it currently
   shows the round-head numbers and will gain the two new component rows.
2. Expect exit 1 until the 16 requests above are done; ask the lanes to fix them first (no new rule change needed).
3. The new "real tree" tests in `tools/quality/tests/test_library_rules.py` and `test_lyric_assist_rules.py` run against
   the merged tree in CI and will fail on any layering breach.
4. Tick T071 and T089 after the green gate and the Sonar run.

## New dependencies and licences

None.

## Blockers

None. Time per checkpoint: not measured precisely (rules and fixtures first, then lane analysis, then measurement and hand-off).
