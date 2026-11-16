# Specification Quality Checklist: Instrument Views

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

## Equal treatment of instrument families (owner principle, MoSCoW 2026-11-16)

- [x] Every family has its own view in this one feature: harmony (US1), guitar and bass (US2),
      drums (US3), vocals (US4), strings/brass/woodwinds (US5); all five are P1
- [x] Scope addition (CTO, 2026-11-16): key detection (US7, S11) is shown for every family incl.
      the written key for transposing instruments; capo mode (US8, S12) is the guitar family's
      equivalent of the written key, and bass/keys explicitly get no capo (US8 AS5)
- [x] One shared entry point treats every family the same way (US6, FR-060)
- [x] Acceptance tests cover at least one case per family (SC-006; tasks.md E2E per story)
- [x] No family's view depends on another family's view being built first (stories are
      independent at the contract level; tasks.md)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- ChordPro, MusicXML and MIDI are user-visible file formats, not implementation choices; model and
  library names (BTC, YIN, pYIN, music21, Verovio) live in research.md, plan.md and ADR-031..033.
- Cents tolerances (100/50/25) and the 0.2 s minimum melody per line are product rules, chosen
  and recorded in the clarifications; they are testable with synthetic fixtures (SC-004).
- Scope bound explicitly: no judging of the drummer's or instrumentalist's own playing (only
  vocals get live feedback, because only the voice is a single-pitch line measurable with a
  microphone at practice quality); scores are not stored. Both are backlog candidates.
- 014 (score-follows-playback) and 015 (input, latency, takes) are written in parallel; their
  names are placeholders reconciled in Phase 0 (spec Assumptions, tasks T001).
- SC-001/SC-004 use synthetic material because no ground truth exists for the owner's songs; the
  shown limits (FR-064) and the CTO's listening check cover real songs.