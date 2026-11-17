# Contract delta: feature 022 "library by artist and album"

Feature 022 adds the `library` tag to the backend API (folder view, live search, library info), one
required response member on `Asset`, UI test ids and four agent tools. **Worker API, generator API,
audio engine, render vectors, web-interop, help-content format, backup-archive and remote-protocol
are unchanged.**

## Status (design round, 2026-11-16; superseded by section 4 "Phase 0 part 2 outcome")

| Contract | Base | Feature document | State |
|---|---|---|---|
| Backend API | newest full document when 022 is built (1.21.0 or the merge of 012-021 + claude-key; 1.10.0 is the last full document on `main`) | [backend-api.library-folders.openapi.yaml](backend-api.library-folders.openapi.yaml) **1.22.0-delta.1** -> full `backend-api.openapi.yaml` **1.22.0** (T002) | delta written; Redocly 2.78.0 lint: **valid, 0 warnings** (OpenAPI 3.1) |
| UI test ids | newest full document (1.21.0 or later) | [ui-test-ids.delta.md](ui-test-ids.delta.md) -> full `ui-test-ids.md` **1.22.0** (T004) | delta written |
| Agent tool catalogue | 1.0.0 (011) | [tool-catalogue.delta.md](tool-catalogue.delta.md) -> **1.1.0** (T004) | delta written |
| Help content | 1.0.0 (004) | four new topics (format unchanged) | ids in the UI delta |
| C# | - | `src/StemStudio.Contracts/Library/*`, `AssetDto.Placement`, `ApiRoutes.Library*`, `ContractLimits` additions, `InstrumentLabels` (T003) | Phase 0 |

## 1. Merge rule (architect, T002)

1. Add the tag `library` and the paths `/api/library/folders`, `/api/library/folders/{folderId}`,
   `/api/library/search`, `/api/tracks/{trackId}/library-info`, `/api/assets/{assetId}/library-info`
   unchanged.
2. Add the parameters `FolderId`, `FolderSort`, `KindFilter`, `StatusFilter`, `FolderLimit`,
   `FolderOffset`, `IfNoneMatch`; the header `LibraryETag`; the responses `FolderNotFound`,
   `LibraryQueryInvalid`, `LibraryInfoInvalid`, `NotFileable`; the schemas `FolderId`, `FolderLevel`,
   `FolderSort`, `PlacementSource`, `FolderRef`, `FolderCounts`, `ArtImage`, `ArtPlaceholder`,
   `FolderArtKind`, `FolderArt`, `LibraryFolder`, `LibraryFolderView`, `SearchField`, `Highlight`,
   `FolderHit`, `ItemHit`, `SearchTotals`, `LibrarySearchResult`, `LibraryName`,
   `LibraryInfoRequest`, `LibraryInfoLevel`, `LibraryInfoState`, `PlacementLevel`, `Placement`; and
   the examples unchanged. Rename on collision only (none expected; checked against 1.10.0 and the
   012-021/claude-key deltas: `NotFound`, `TrackId`, `AssetId` already exist with the same meaning
   and are **not** added again - the external `$ref`s to 1.10.0 become local `#/components/...`).
3. In `Asset` add `placement` to `properties` (`oneOf: [Placement, null]`) and to `required`; keep
   every member the base has (later features may have added some). No other schema changes.
4. Bump `info.version` to 1.22.0 with the changelog line of this delta; contract tests
   (`LibraryContractTests`) prove the full document = base + this delta.
5. Lint the full document with `redocly.yaml` of the base (expected warnings: those of the base
   only; this delta adds none).

**Compatibility**: additive. Old clients ignore `Asset.placement` and the new paths. `GET
/api/assets` keeps its 1.5.0 semantics (`q` = name contains). The 013 write gate is not extended
(library info edits are metadata edits like rename).

## 2. Decisions that shape the delta (ADR-036)

| # | Decision | Where |
|---|---|---|
| D-1 | Folders are derived; folder ids are hashes of normalised keys (`ar|al|so` + 16 hex) | research R-03 |
| D-2 | Placement stored per item; per-level source (`item`, `track`, `match`, `import`, `fallback`) | R-01, data-model 2.3 |
| D-3 | One folder-view operation for every level, items at song level as full `Asset`s (reuse of 007 UI) | R-06 |
| D-4 | Server-side search over an in-memory index keyed by the library revision; highlights as UTF-16 ranges | R-07 |
| D-5 | Folder art references existing 005 artwork URLs; no new image endpoint | R-05 |
| D-6 | ETag = library revision on every `GET` of the tag | R-06 |

## 3. Cross-feature notes

| Feature | Note |
|---|---|
| 013 | No backup-archive change; restore of older archives relies on `FiledVersion = 0` + start-up backfill. Storage overview unchanged. |
| 014 | Shortcut `/` (scope `library`, "Focus library search") added to the FR-020 catalogue with owner 022. |
| 015 | `origin = recording` items are excluded from folders and search; `PUT/DELETE .../library-info` on them answers `409 not-fileable`. |
| 011 | Catalogue 1.1.0 (tool-catalogue.delta.md); `list_exports`, `list_scores`, `search_library` unchanged. |
| 012-021 | Any new asset origin is filed by the generic rule (track -> source item -> "My songs"); no 022 change needed. |

## 4. Phase 0 part 2 outcome (architect, 2026-11-17, tasks.md T002-T005)

| Contract | Version | Document | Notes |
|---|---|---|---|
| Backend API | **1.22.0** | [backend-api.openapi.yaml](backend-api.openapi.yaml) | = **1.11.0** (012, `specs/012-practice-tempo/contracts/backend-api.openapi.yaml`, the newest full document on `main` at 31d43422) + the delta, built by [merge_full.py](merge_full.py) (textual merge: every base line is kept byte for byte). `x-stemstudio-contains` = the 1.11.0 list + `1.22.0`. Shared `TrackId`, `AssetId`, `NotFound` keep the base definitions (rule 2); `Asset` gains `placement` (rule 3). **Editorial addition**: the six base `Asset` examples (`QueuedAsset`, `RenderingAsset`, `ReadyAsset`, `FailedAsset`, `QueuedScoreAsset`, `ReadyScoreAsset`) gain a `placement` (Rick Astley > Unknown album > Never Gonna Give You Up, sources import/fallback/import, folder ids computed with the R-02/R-03 rules), otherwise they would no longer validate against the required member. Redocly 2.78.0: valid, **9 warnings = the 9 of 1.11.0** (all `operation-4xx-response`); the delta alone: valid, 0 warnings. `merge_full.py --check`: superset of 1.11.0 (only `Asset` and its six examples grow) and of the delta (every non-shared delta entry unchanged) |
| UI test ids | **1.22.0** | [ui-test-ids.md](ui-test-ids.md) | = 1.11.0 (012) + changelog paragraph + section "Additions in 1.22.0 (feature 022)" = the delta from "Changed behaviour" on (headings one level down), unchanged: 39 id rows, 48 ids, 46 new + 2 reused (`library-search`, `asset-row-<assetId>`) |
| Agent tool catalogue | **1.1.0** | `specs/011-ai-composer-agent/contracts/tool-catalogue.md` (edited in place, as 012 did for 1.3.0) | = **1.3.0** + the delta (labels by containment, ROADMAP): changelog, section "Feature 022", counts 61, group `library` (9), page context `library` (`/library`, `/library/{folderId}`) 14 + 9 + 3 + 6 = 32, evaluation rows LIB-01..LIB-05 |
| C# | - | `src/StemStudio.Contracts/Library/*` (5 enums, 17 records, `LibraryProblemTypes`, `InstrumentLabels`, `ApiRoutes.Library.cs`, `ContractLimits.Library.cs`), `AssetDto.Placement` (nullable, always written), `ContractLimits` now `partial` | `LibraryContractTests`, `LibraryUiTestIdsTests`, `InstrumentLabelsTests` |
| Agent port | - | `src/StemStudio.Agent/Ports/ILibraryActions.cs` (+ `LibraryFolderQuery`, `LibrarySearchQuery`, `LibraryInfoTarget`), `IStemStudioActions.Library` | skeletons: `LoopbackLibraryActions` (throws until T080), eval `FixtureLibraryActions` (501 until T083) |

**When 013 (1.12.0) lands on `main`** and `main` is merged into the round branch, the CTO:

1. re-runs `python -I specs/022-library-folders/contracts/merge_full.py --base specs/013-library-care/contracts/backend-api.openapi.yaml`
   (013 adds no 022 name and does not change `Asset`; the script fails loudly on any collision) and
   `--check` afterwards; `x-stemstudio-contains` then ends `..., 1.11.0, 1.12.0, 1.22.0`;
2. re-merges `ui-test-ids.md` onto 013's 1.12.0 the same way (title, changelog paragraph before
   "## Rules", the 1.22.0 section at the end; `LibraryUiTestIdsTests.Base` -> `Contract/013`);
3. resolves the expected conflicts: `tests/StemStudio.Api.Tests/StemStudio.Api.Tests.csproj` (the
   `Contract/backend-api.openapi.yaml` link -> 022's document; keep 013's `Contract/012` and
   `Contract/013` links), `ContractJson.cs` (both add `Backend012`; keep one), the doc comment of
   `PracticeContractTests`, `BackendContractTests*` (keep both pending lists) and re-points
   `LibraryContractTests.Base` to the 1.12.0 document (every expectation is relative to it);
4. 023 (1.23.0, catalogue 1.2.0) is merged on top of the result by the same rule.

Validation commands (repo root):

```text
python -I specs/022-library-folders/contracts/merge_full.py --check
npx @redocly/cli@2.78.0 lint --config specs/022-library-folders/contracts/redocly.yaml specs/022-library-folders/contracts/backend-api.library-folders.openapi.yaml specs/022-library-folders/contracts/backend-api.openapi.yaml
dotnet test tests/StemStudio.Api.Tests --filter "FullyQualifiedName~StemStudio.Api.Tests.Contract"
```

**Done at the 022+023 round start (2026-11-17, `main` e8d6a5d8 merged)**: steps 1-3 above were run.
`backend-api.openapi.yaml` 1.22.0 = 013's 1.12.0 + the delta (`x-stemstudio-contains` ends
`1.11.0, 1.12.0, 1.22.0`; Redocly: valid, 11 warnings = the 11 of 1.12.0; `--check`: superset of
1.12.0 and of the delta). `ui-test-ids.md` 1.22.0 = 1.12.0 + the 1.22.0 changelog paragraph + the
1.22.0 section. `LibraryContractTests.Base` and `LibraryUiTestIdsTests.Base` point to `Contract/013`;
`LibraryCareContractTests` now checks 013's own linked 1.12.0 (`ContractJson.Backend013`), since the
current full document is 022's.
