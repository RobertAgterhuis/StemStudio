# Hand-off: quality-gate / 014 T015-T016 + 015 T007-T008 (L2, HO:devops)

## What was built

New rules in `tools/quality/dependency_check.py` (documented in its module docstring), tests in
`tools/quality/tests/test_practice_flow_rules.py` (written first: collection failed on the missing names).

- Worker scopes (`PYTHON_SCOPES`, file level, new `check_scopes`): `analysis/structure/**` -> `ports`, `schemas`,
  `analysis.structure` and numpy, scipy, sklearn, librosa, soundfile (+ stdlib); `engraving/score_view.py` ->
  `ports`, `schemas`, `engraving` and verovio (+ stdlib, so `xml`). `documents` is a new top-level module:
  `PYTHON_ALLOWED["documents"] = {ports, schemas}`, `PYTHON_EXTERNAL_ALLOWED["documents"] = {reportlab}`. `jobs`
  and `api` cannot import any of them (existing layer map; tests for each combination).
- Engine: `TS_MODULE_FORBIDDEN["audio-engine/score-follow"] = {callbacks, index}` (the Blazor interop modules);
  new importer allow-list `TS_MODULE_IMPORTERS = {"audio-engine/recording": {"audio-engine/engine"}}` with
  `check_importers` (message "... (imported only by audio-engine/engine)").
- .NET: `Domain/{Sections,Setlists,Practice}` -/- {Api, Persistence, Web}; `Api/Assets` and `Api/Tracks` -/-
  `StemStudio.Api.Recording`.
- Three older tests adjusted to the added entries (`test_render_rule`, `test_score_rules`, and
  `test_metadata_rule` now uses an unchecked `Api/Health` folder instead of `Api/Tracks`); helper
  `repo_tree.error_findings`.

Decision: `engraving/score_view.py` may import its own package (`engraving.*`, e.g. the 008 `VEROVIO_OPTIONS`),
otherwise the 008 options would have to be duplicated.

## Tests and coverage

`cd tools/quality && uv run pytest --cov --cov-branch`: **955 passed**; total line 100 %, branch 100 %
(`dependency_check.py` 100 % / 100 %). ruff check, ruff format --check, mypy: clean.

## Quality checks

`run_all.py --report docs/quality/SIG-REPORT.md`: **PASS**, 0 errors, 1480 warnings, 362 clones (jscpd ran,
its own line in the log), 4.8 stars house estimate. `dotnet format StemStudio.slnx --verify-no-changes`: exit 0.

Dry run of the new rules against the other lane worktrees (their current heads): L1, L3, L5, L6 clean;
**L4: 4 errors** (`Api/Assets/{AssetOriginPolicy,AssetSynchronizer,TakeExportResolver}.cs`,
`Api/Tracks/TrackDeletionService.cs` use `StemStudio.Api.Recording`). Reported to the orchestrator as a refactor
request; the rule commit is `194770bc` (tools/quality only).

## Dependencies added

none

## Docs impact

none (developer tooling)

## Next owner

Orchestrator / L4 (refactor of the four files), CTO T102 integration run.

## HO:polish addendum (after merging checkpoint head 136cbc8b)

`run_all.py` on the merged tree: **FAIL, 10 errors** (all `[parameters]` hard limit, more than 4 parameters, in
test helpers of other lanes), 431 clones (jscpd ran), 1819 warnings. Refactor requests (use a parameter object,
a record builder or `with` expressions):

| Owner | File:line | Unit |
|---|---|---|
| L3 backend 014 | `tests/StemStudio.Api.Tests/Practice/PracticeEndpointsTests.cs:19` | `Session` (5) |
| L3 backend 014 | `tests/StemStudio.Api.Tests/Sections/ReplaceSectionsTests.cs:14` | `Input` (6) |
| L3 backend 014 | `tests/StemStudio.Domain.Tests/Practice/PracticeRulesTests.cs:22` | `Part` (5) |
| L3 backend 014 | `tests/StemStudio.Domain.Tests/Sections/SectionRulesTests.cs:14, 24, 239` | `Input` (6), `Found` (5), `AnyChangeOfKindLabelBoundsOrInstrument_MakesAUserSection` (5) |
| L4 backend 015 | `tests/StemStudio.Api.Tests/Recording/ReplaceTakeAudioEndpointTests.cs:32` | `ReplaceAsync` (5) |
| L4 backend 015 | `tests/StemStudio.Domain.Tests/Recording/TakeWavReaderTests.cs:87` | `OtherFormats_AreUnsupported_WithTheField` (5) |
| L4 backend 015 | `tests/StemStudio.Domain.Tests/Recording/TestWav.cs:29` | `Fmt` (5) |
| L4 backend 015 | `tests/StemStudio.Persistence.Tests/RecordingPersistenceTests.cs:44` | `Take` (5) |

The L2 files have no error and no new production clone; `dependency_check` on the merged tree: 0 errors (the
015 T007 rule holds after the L4 fix).
