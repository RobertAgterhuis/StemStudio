# Specification Quality Checklist: Practice Flow

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-16
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - the spec names behaviour; library
      choices are in research.md / ADR-023 (FR-020's key names are user-visible behaviour)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain (answered autonomously, Clarifications 2026-11-16)
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined (US1-US6)
- [x] Edge cases are identified
- [x] Scope is clearly bounded (chords in setlist PDFs and transposed parts are 016; recording keys are 015)
- [x] Dependencies and assumptions identified (012 surface, 013 backup, 008 scores, 006 lyrics)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Principle checks (project-specific)

- [x] Every instrument family is served: sections and solos name any non-drum instrument; score
      follow for every 008 profile including drums; FR-033 requires one acceptance case per family
- [x] Local-only data and an off switch for the practice log (FR-026, FR-028, SC-009)
- [x] Accessibility: single-key shortcuts can be turned off and remapped (WCAG 2.1.4), native
      controls keep their keys, charts have table views, highlight not by colour alone
- [x] Honesty: automatic labels are presented as a guess and are editable

## Notes

- Count-in key decided by the CTO (2026-11-16): metronome keeps `K` (004), count-in is `Shift+K`
  (FR-020, DELTA R-2).
