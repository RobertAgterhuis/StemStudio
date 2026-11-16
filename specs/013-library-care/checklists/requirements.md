# Specification Quality Checklist: Library Care (Backup, Restore, Storage Overview and Clean-up)

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-16
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details beyond the decisions the owner asked to record (archive format,
      backups folder, SQLite snapshot are named because they are user-visible or safety-relevant)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders (stories and acceptance scenarios)
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain (11 clarifications answered, ADR-022)
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic where possible (SC-002 names the reference machine)
- [x] All acceptance scenarios are defined (US1-US5)
- [x] Edge cases are identified (16)
- [x] Scope is clearly bounded (no encryption, no incremental or scheduled backups, no cloud targets,
      no model-cache removal, no browser upload of archives; merge is P3)
- [x] Dependencies and assumptions identified (001-010 data, 007 read-only library mount, 010
      consent rules, 004 help/levels)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows (back up, restore, see storage, clean up, merge)
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into the user stories

## Data safety

- [x] Backups are consistent without pausing jobs (snapshot + write gate, FR-002)
- [x] Restore verifies everything before it changes anything (FR-011) and is crash-safe (FR-017,
      SC-005)
- [x] Replace keeps the previous library until the user discards it (FR-012)
- [x] Clean-up never removes originals; two-step preview and confirm (FR-022, FR-023, SC-007)
- [x] Hostile archives: zip-slip, links, bombs, unlisted entries, unsafe stored paths (FR-011,
      FR-034, SC-004)
- [x] Excluded always: secrets, configuration, model caches, worker data (FR-004)

## Ethics and consent (010)

- [x] A voice profile is never archived without its consent proof (FR-009)
- [x] A restored profile keeps its consent proof, checked on restore (FR-016, SC-008)
- [x] The user can leave voice profiles out of a backup and a restore (FR-003, FR-016)
- [x] The UI states that archives contain voice samples and are not encrypted (FR-009)

## Notes

- SC-002 (throughput) is measured on the reference machine in the CTO validation (quickstart V9);
  CI checks only functional behaviour.
- US5 (merge) can be cut without affecting US1-US4; its tasks are in their own phase.
