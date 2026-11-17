# Hand-off: quality-gate / 013 foundational (T009-T010)

Branch `013-lane-worker`. Rules of tasks.md T009 (a)-(c), red (ad76f1e4) -> green (f15ca2c0).

## What was built

- **`tools/quality/library_care_rules.py`** (new, check name `library-care`, owner of findings =
  path owner, i.e. backend for `src/StemStudio.Api`):
  - (a) Layering: a C#/Razor file in `src/StemStudio.Api` outside `LibraryCare/` and
    `ApiComposition.cs` may reference only `StemStudio.Api.LibraryCare.Gate[.*]`. Matched forms:
    `StemStudio.Api.LibraryCare.X`, `global::...`, `Api.LibraryCare.X`, and namespace-relative
    `LibraryCare.X` (a `StemStudio.Api.*` file resolves it to the API folder); also the root
    namespace itself (`using StemStudio.Api.LibraryCare;`). Not matched:
    `StemStudio.Contracts.LibraryCare.*`, `LibraryCareLimits`, member access `this.LibraryCare`,
    comments and strings. Message: `forbidden dependency on <name>: outside Api/LibraryCare only
    StemStudio.Api.LibraryCare.Gate may be referenced`.
  - (b) Banned APIs in all production `src/**` files: member calls of `ExtractToDirectory`,
    `ExtractToFile`, `CreateFromDirectory` (+ `Async`), on any receiver (covers
    `ZipFile.*`, `ZipFileExtensions.*`, `archive.ExtractToDirectory`, `entry.ExtractToFile`),
    and `using static System.IO.Compression.ZipFile[Extensions];`. Message: `banned API <name>:
    read and write archive entries one by one (ArchiveReader, research R-06)`.
  - Reuses `secret_rules.Source`/`load` (comment and literal blanking); `secret_rules._sources`
    became the public `production_sources`.
- **`dependency_check.py`** (c): `PYTHON_ALLOWED["storage"] = {jobs, ports, schemas, config}`,
  `api` += `storage`; docstring map updated. `jobs -> storage` and adapter -> storage are errors.
- **`run_all.py`** / **`report.py`**: `GateResult.library_care_findings`; report section
  "Library care". **`common.py`**: owners `worker/src/stemstudio_worker/storage/` and
  `worker/tests/storage/` -> stem-separation. **`components.py`**: row `worker/storage`.
- **`README.md`**: table row and rule prose; **`docs/quality/SIG-REPORT.md`** regenerated.

Decisions (rows in `docs/decisions/CTO-DECISIONS-2026-11-15.md`, 013): T009 (c) read as "storage
may import `jobs`" (the janitor must update the live job stores); the banned names are reserved
for own methods too, and `ApiComposition.cs` is the only API file outside `LibraryCare/` with
full access.

## Files changed

`tools/quality/{library_care_rules.py (new), dependency_check.py, secret_rules.py, run_all.py,
report.py, common.py, components.py, README.md}`, `tools/quality/tests/{test_library_care_rules.py,
test_storage_rule.py (new), test_components.py, test_run_all.py}`, `docs/quality/SIG-REPORT.md`,
`specs/013-library-care/tasks.md` (T009, T010 ticked), `docs/decisions/CTO-DECISIONS-2026-11-15.md`.

## Tests and coverage

```text
cd tools/quality
uv run --frozen pytest --cov --cov-branch     772 passed; coverage 100 % line and branch (total)
```

## Quality checks

```text
uv run --frozen ruff check . / ruff format --check .     clean
uv run --frozen mypy                                     Success: no issues found in 44 source files
uv run --project tools/quality --frozen python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md
  quality gate: 2418 files, 19362 units, 267 clones, 3 exemptions, 0 errors, 868 warnings -> exit 0
  Library care: PASS; the 013 worker code (storage/, api/storage.py) has no SIG warnings
```

## Dependencies added

None.

## Docs impact

None (developer tooling only).

## Open issues / requests

- **backend**: name own methods without `ExtractToDirectory`/`ExtractToFile`/`CreateFromDirectory`
  (e.g. `StagingExtractor.ExtractAsync`); register LibraryCare services and map its endpoints in
  `ApiComposition.cs` (other API folders: `LibraryCare.Gate` only).
- The namespace-relative detection is specific to `LibraryCare`; the older folder rules keep the
  documented gap for partially qualified names.

## Next owner

backend (T013+ under the new rules), CTO (integration).
