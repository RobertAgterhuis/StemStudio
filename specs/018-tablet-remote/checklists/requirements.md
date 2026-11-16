# Specification Quality Checklist: Tablet and Remote

**Purpose**: Validate specification completeness and quality before planning
**Created**: 2026-11-16
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details leak into user stories and success criteria (technology names appear
      only in Clarifications, Assumptions and FRs where they are recorded decisions, e.g. mkcert;
      the user stories use plain language only)
- [x] Focused on user value (practising away from the desk, MoSCoW C4)
- [x] Written for non-technical stakeholders (stories and scenarios in plain language)
- [x] All mandatory sections completed (Clarifications, User Scenarios, Edge Cases, Requirements,
      Key Entities, Success Criteria, Assumptions, Won't have)

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain (autonomous answers recorded in Clarifications and
      ADR-027)
- [x] Requirements are testable and unambiguous (FR-001..FR-038, mapped to tasks; security list
      SV-01..SV-22 in plan.md)
- [x] Success criteria are measurable (SC-001 latency p95, SC-002 state push, SC-003 pairing time,
      SC-004 exposure and security list, SC-005 installability/offline, SC-006 a11y, SC-007 per
      family, SC-008 wake lock, SC-009 coverage/contracts)
- [x] Success criteria are technology-agnostic where possible
- [x] All acceptance scenarios are defined (US1-US6)
- [x] Edge cases are identified (IP change, other networks, certificate problems, two tabs, closed
      tab, restart, revoke while connected, limits, flooding, clocks, client isolation, iOS HTTP,
      missing 014/016 content)
- [x] Scope is clearly bounded (Won't have: audio on the tablet, internet access, built-in CA,
      agent tools, native apps)
- [x] Dependencies and assumptions identified (012, 014, 016; mkcert run by the owner; Docker
      Desktop host-IP publishing verified in Phase 0)

## Security

- [x] Network exposure is opt-in at deployment and in the app; default exposure reduced (FR-010,
      FR-011, FR-017)
- [x] Only the remote surface is reachable from the LAN; the app and API never are (FR-013, FR-014)
- [x] Pairing needs human approval with a check number; brute force bounded (FR-018..FR-020)
- [x] Keys: random, hashed at rest, never logged, revocable, idle expiry (FR-021..FR-023)
- [x] Browser-side attacks covered: CSWSH/CSRF (Origin), DNS rebinding (hosts), XSS (CSP, text only)
      (FR-015, FR-025)
- [x] Transport security decided and honest about HTTP (FR-012, FR-016; ADR-027 D8)
- [x] Threat model with residual risks (ADR-027)
- [x] Never exposed to the internet; no cloud (FR-038, W2)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover the primary flows (stand view, pairing, control, tablet stand, install,
      songs)
- [x] Every instrument family is covered equally (FR-002, FR-031, SC-007)
- [x] Help topics, journey levels, ui-test-ids, accessibility and manuals are specified (FR-033..FR-037)
