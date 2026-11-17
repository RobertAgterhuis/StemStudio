# Hand-off: qa / 013 polish (T066, qa part) - lane complete

Branch `013-lane-qa` (worktree `.claude/worktrees/013-qa`), base `7253cf37` (architect Phase 0).
Lane tasks T012, T041, T053, T057, T061, T065 are done and ticked; T066 is a shared row (one per
owner) and stays unticked for the other owners. Checkpoints: `qa-013-us1.md` .. `qa-013-us5.md`.
Per the lane brief no stack was started: every E2E spec is authored against backend-api 1.12.0,
ui-test-ids 1.12.0 and backup-archive 1.0.0 and validated statically; the CTO executes them in the
integration wave (separate compose project, see TEST-STRATEGY 19.4/19.5).

## T066 (qa part)

| Item | Result |
|---|---|
| Accessibility of every new dialog | `e2e/a11y/library-care.a11y.spec.ts` (project `a11y`, 3 per theme): backup, restore and clean-up dialogs opened with the keyboard, `aria-modal="true"`, axe clean in dark and light at Expert, focus trapped for 12 Tab + 12 Shift+Tab, Escape closes and returns the focus to the opener; the archive delete confirmation scanned and closed with Escape. Page-level axe on both tabs is in `library-care-backup.spec.ts` |
| Traceability FR/SC -> tests | TEST-STRATEGY 19.8 (FR-001..FR-036, SC-001..SC-010) and 19.6 (acceptance scenarios) |
| SIG limits | e2e: ESLint complexity 5, 4 parameters, 30 lines per support function, 60 per test callback, depth 3: clean; fixtures: ruff mccabe 5, 4 args, 15 statements, 5 branches: clean |
| Strategy status | TEST-STRATEGY 19.4 (idle-library rule) and 19.9 |

## Totals

```text
tests/fixtures/backup: ruff + format clean; mypy --strict 12 files clean; pytest 107 passed
  coverage line 99.1 % (782/789), branch 94.1 % (111/118); missed: the two __main__ guards and the
  default-folder catalogue writes (qa-013-us1.md reported the combined figure 98.5 %)
e2e: typecheck clean; lint clean (0 warnings)
  playwright --list: 132 library-care tests in 6 files (chromium + firefox), 6 in the a11y project
  stack-free self-test library-care-archive.spec.ts: 14 passed (chromium + firefox)
```

| Spec | Tests per browser | Tags |
|---|---|---|
| `library-care-backup.spec.ts` | 11 | `@library-care @backup` (3 need the throttle) |
| `library-care-restore.spec.ts` | 31 | `@library-care @restore` (+ `@compose`, `@fresh-library`) |
| `library-care-storage.spec.ts` | 7 | `@library-care @storage` (+ `@compose`) |
| `library-care-cleanup.spec.ts` | 8 | `@library-care @cleanup` (1 needs the throttle) |
| `library-care-merge.spec.ts` | 3 | `@library-care @merge` |
| `library-care-archive.spec.ts` | 7 | `@library-care @self-test` (no stack) |
| `a11y/library-care.a11y.spec.ts` | 6 (a11y project) | `@library-care` |

## Decisions (docs/decisions/CTO-DECISIONS-2026-11-15.md, 013 rows)

Committed corpus + catalogues; `formatVersion` 2 -> `archive-incompatible`; H9 / H17 shape; SC-010
value `Recording`; **throttle option `StemStudio:LibraryCare:ThrottleBytesPerSecond` (backend +
devops to implement)**; E2E isolation (replace -> undo -> discard, `@fresh-library`, `@compose`);
verify does not check free space, `migratedFrom` = archive `schema.lastMigration`.

## Open items

1. backend: the throttle option (default 0) and the readings listed in the us1-us5 hand-offs
   (newest archive first, refusal codes, `SkippedItem` shape, `detailFilesRemoved`).
2. devops: e2e override (temp backups folder uid 1654 exported as `E2E_BACKUP_DIR`, the throttle,
   `E2E_LIBRARY_THROTTLE=1`); CI job for `pytest -c tests/fixtures/backup/pyproject.toml` + ruff/mypy.
3. CTO: run `npm run test:library-care-fresh` first on a fresh project, then `test:library-care`,
   `test:library-care-compose` (`E2E_COMPOSE=1`, `E2E_COMPOSE_PROJECT`), `test:a11y`; help.spec 013
   reference queries (FR-030) like 012 in the integration run; V9/V10 manual.
4. If 015 names the recording origin differently, update `recording-asset.json`.

## Dependencies

None new (Python stdlib + numpy/soundfile/jsonschema of the worker environment; Node built-ins
`node:zlib`, `node:crypto`, `node:sqlite`). Licences unchanged.
