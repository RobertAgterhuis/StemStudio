# Hand-off: architect / 013 plan (library care design)

## What was built

The complete Spec Kit design for feature 013 (MoSCoW M4 backup/restore, M5 storage overview and
clean-up): spec with 5 user stories (US1 backup P1, US2 restore P1, US3 storage overview P2, US4
clean-up P2, US5 merge P3), research, plan, data model, quickstart, contract delta summary, tasks
(72 ids, TDD, per owner lane, parallel markers) and ADR-022. Contracts: backup-archive 1.0.0 (new
format + manifest JSON Schema), backend-api 1.12.0 as a standalone additive delta (base 1.11.0 of
012 is not on this branch), worker-api 1.7.0 as a full document (base 1.6.0 of 008), ui-test-ids
1.12.0 delta. The CTO's 015 cross-feature note (recorded takes and latency calibrations) is built
in: `recordings` category (original, optional, never in a clean-up), calibrations travel with the
database.

No production code: Phase 0 (DTOs, `schemas.py` 1.7.0, ports, spike, allow-list check) is planned
as T001, T003-T008 and is not executed yet.

## Files changed

- `specs/013-library-care/{spec.md, research.md, plan.md, data-model.md, quickstart.md, DELTA.md, tasks.md}`
- `specs/013-library-care/checklists/requirements.md`
- `specs/013-library-care/contracts/{backup-archive.md, backup-manifest.schema.json, backend-api.delta.openapi.yaml, worker-api.openapi.yaml, ui-test-ids.delta.md, redocly.yaml}`
- `docs/decisions/ADR-022-library-backup-and-storage-care.md`
- `docs/handoffs/architect-013-plan.md`

## Tests and coverage

No code, so no unit tests. Contract checks:

- `npx @redocly/cli@2.78.0 lint --config specs/013-library-care/contracts/redocly.yaml specs/013-library-care/contracts/backend-api.delta.openapi.yaml specs/013-library-care/contracts/worker-api.openapi.yaml`
  -> both valid OpenAPI 3.1, 0 errors, 7 warnings (accepted kinds: `operation-4xx-response` on
  operations without client errors, incl. 2 inherited from 1.6.0; unused merge-only component
  `DetailPhaseAddition`). The 1.6.0 base has 3 warnings of the same kind.
- `backup-manifest.schema.json`: Draft 2020-12 meta-schema check passed (jsonschema 4.26); the
  example manifest validates; entries with `..`, `//` or an absolute path are rejected.

## Quality checks

n/a (documents only). SIG impact planned: pipelines split into stage classes; quality-gate rules
T009 (layering of `Api.LibraryCare`, banned ZIP extraction APIs, worker `storage/` imports).

## Dependencies added

None. ZIP via `System.IO.Compression`, SQLite backup via `Microsoft.Data.Sqlite` (already present),
SHA-256 via the BCL; worker uses the standard library. JSON Schema tests reuse the dependency-free
`SchemaSubsetValidator` (JsonSchema.Net stays excluded).

## Docs impact

User manual: new chapter "Library care". Technical manual: deployment (backups bind mount,
`STEMSTUDIO_BACKUP_HOST_DIR`, Linux uid 1654), data model (`library-care/` folder, archive format),
security (hostile archives, consent proofs), operations (backup/restore runbook). Task T071 (CTO).

## Open issues / requests

1. ADR number 022 assumes 012 takes 021; renumber on collision (T001).
2. T005: merge the backend and ui-test-ids deltas after 012 fixes 1.11.0 (instructions in DELTA.md).
3. devops: compose gets a new bind mount `./backups:/backups` (T011); on Linux the folder must be
   owned by uid 1654.
4. Feature 015 must use provenance origin `recording` for play-along takes (the 013 classifier and
   scrub rely on it) and keep latency calibrations in the database; any other storage location
   needs a backup-archive minor.
5. Future features adding a consent statement version must keep the old embedded texts (ADR-022 D7).

## Next owner

CTO (review, T001) -> architect Phase 0 (T003, T004, T006-T008) -> wave.
