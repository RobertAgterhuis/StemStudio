# About this manual

This manual describes how StemStudio is built, deployed, operated and developed. It is meant
for whoever installs and maintains StemStudio and for the agent team that builds it. The
user-facing behaviour is described in the companion *User Manual*.

It covers the final product of features 001 to 013, 022 and 023. Sections about a feature that is not
delivered yet carry the marker <span class="planned">In development</span> and describe the
approved specification and plan (`specs/NNN-*/spec.md`, `plan.md`, `contracts/DELTA.md` and
the related ADR). Contract versions and file names in those sections are planned values and
are fixed when the feature's Phase 0 writes its contracts.

## Feature status

Generated from `docs/ROADMAP.md` and `docs/STATUS.md` at build time. A feature is
**Delivered** when `docs/STATUS.md` records its integration results.

<!-- feature-status -->

## Sources of truth

| Topic | Authoritative source |
|---|---|
| Principles and rules | `.specify/memory/constitution.md` |
| Agent working rules | `docs/AGENT-PROTOCOL.md` |
| Feature requirements | `specs/NNN-*/spec.md`, `plan.md`, `tasks.md`, `quickstart.md` |
| Interfaces | `specs/NNN-*/contracts/` (OpenAPI, UI test ids, engine and interop contracts) |
| Decisions | `docs/decisions/ADR-*.md`, `docs/decisions/CTO-DECISIONS-*.md` |
| Licences | `docs/THIRD-PARTY-LICENSES.md` |
| Progress | `docs/STATUS.md`, `docs/ROADMAP.md` |

When this manual and one of these sources disagree, the source wins and the manual must be
corrected.

## A living document

This documentation is updated continuously. Every feature that changes user-visible
behaviour or technical setup must update the manual sources in `docs/public/src` and
regenerate the PDFs before its integration wave closes (constitution 1.3.1, Governance).
Chapter 12 describes the rule, the build and the CI check that enforces it.
