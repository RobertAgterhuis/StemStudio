# Specification Quality Checklist: Insights and Handouts

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-16
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Instrument equality (MoSCoW principle)

- [x] Every stem and detail stem gets a rating (US1), no instrument is skipped
- [x] Seven handout families with equivalent content sets (data-model 6.2), including drums and
      melodic instruments in C/B-flat/E-flat/F (US3)
- [x] Acceptance tests cover one case per family (SC-006) and per stem type (US1 fixtures)

## Honesty (constitution V)

- [x] Ratings are labelled estimates; "validated" only where gate G1 passed (FR-004)
- [x] The false "re-run on the GPU" tip is excluded with the reason (FR-006, Clarifications)
- [x] The only "better model" action (US5) is gated by a measurement (G2, SC-007)
- [x] Known confusions (kick and bass together) are named in Edge Cases

## Notes

- PDF is a user-visible format named by the backlog item, not an implementation choice. The
  renderer, models and measures are named only in ADR-029, research.md and plan.md.
- SC-002 uses a synthetic set for the hard target because no reference multitracks of the user's
  songs exist; the real-set correlation decides the reliability labels instead of a pass/fail,
  which keeps the claim honest.
- US5 is P3 and gated; if G2 fails, FR-007 and SC-007 are removed in DELTA.md "Phase 0 outcome"
  and the tip catalogue loses `rerun-high-quality`.
- Dependencies on 014 and 016 are assumptions with a reconciliation step (DELTA.md section 6),
  because those specs are written in parallel.
