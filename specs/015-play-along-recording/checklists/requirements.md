# Specification Quality Checklist: Play-along Recording

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-16
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) in the requirements; the
  clarifications name browser behaviour only where it limits the user (two input channels,
  supported browsers)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain (all answered in Clarifications, ADR-024)
- [x] Requirements are testable and unambiguous (limits: 0-1000 ms, 200 takes, 50 passes, 30 min,
  +/-200 ms nudge, 1 s partial pass, 5 ms setup-change threshold)
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (sample and ms tolerances, seconds, counts)
- [x] All acceptance scenarios are defined (7 stories)
- [x] Edge cases are identified (permissions, devices, browser support, clipping, silence, limits,
  disk space, speed trainer, track deletion, agent and voice-sample isolation)
- [x] Scope is clearly bounded: recording at other speeds, layering several takes, hearing the
  selected take while recording, more than two interface channels and hardware gain control are
  out of scope
- [x] Dependencies and assumptions identified (007, 012; 014 in parallel; requests to 012)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows (record, calibrate, loop takes, compare, manage, export,
  adjust timing)
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Every instrument counts equally (MoSCoW principle)

- [x] The part choice covers every family: vocals, guitar, bass, keys/piano, drums, other and
  acoustic instruments (FR-005)
- [x] Input options cover microphones, interface DI/hi-Z inputs, amp-simulator and line outputs,
  stereo sources (keys, drum overheads, e-drums) (FR-001)
- [x] Help has a connection topic per family (FR-019)
- [x] Acceptance runs one case per family (SC-008)

## Privacy and safety

- [x] Recordings stay on the machine; automated network assertion (FR-017, SC-007)
- [x] Microphone requested only on demand and released when leaving (FR-003)
- [x] Takes cannot become voice samples (010 guard) and are not given to the AI agent (FR-017)
- [x] Monitoring off by default with a feedback warning (FR-006)

## Notes

- SC-002 is an owner check with a physical cable loopback (cannot run in CI).
- Firefox recording is best effort (ADR-024 D8); the E2E suite runs recording in Chromium only.
