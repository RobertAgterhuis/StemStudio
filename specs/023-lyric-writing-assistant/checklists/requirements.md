# Specification Quality Checklist: Lyric Writing Assistant

**Purpose**: Validate specification completeness and quality before proceeding to implementation
**Created**: 2026-11-16
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details beyond what the owner fixed or the constitution requires (Ollama
      default and Claude opt-in come from constitution IV; dictionaries are named in the
      Clarifications with their reason because they define the offline baseline)
- [x] Focused on user value (writing help, correct text, poetic licence protected and proposed)
- [x] Written for non-technical stakeholders (stories and acceptance scenarios in plain language)
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain (answered in Clarifications and ADR-037; three genuine
      owner questions are listed in plan.md with safe defaults)
- [x] Requirements are testable and unambiguous (limits: 8 lines per block, 300-char explanations,
      200 marks, 5,000 words, 7-word verbatim runs, 25 % / 3 lines sheet guard, 500 ms rhymes)
- [x] Success criteria are measurable (recall, precision, licence false-positive rate, latency
      percentiles, gold-list accuracy, 0-request checks)
- [x] Success criteria are technology-agnostic where possible (SC-001/SC-004 name the model because
      the owner asked for an honest measurement of `qwen3.5:9b`)
- [x] All acceptance scenarios are defined (5 stories)
- [x] Edge cases are identified (imported lyrics, verbatim output, injection, anchors, mixed
      language, limits, two tabs, concurrency, deletion, model failures, undo after edits)
- [x] Scope is clearly bounded (own 010 sheets only; nl and en only; no background checking; no
      LanguageTool; no remembered rejections)
- [x] Dependencies and assumptions identified (010, 011, claude-key; 022 version order)

## Owner request coverage

- [x] On demand only, nothing in the background, nothing off the machine without Claude (FR-002,
      FR-026, SC-006)
- [x] Check spelling, grammar, flow in the chosen language with inline suggestions, categories,
      explanations, accept / reject / accept all, undo (FR-004 - FR-008)
- [x] Poetic licence always allowed: never an error, sensitivity, intentional marks, my words
      (FR-009 - FR-012)
- [x] The assistant suggests poetic licence and devices itself (FR-013, FR-014, US2)
- [x] Writing help: continue, alternatives, rhyme finder, syllables vs word rate/BPM, titles
      (FR-015 - FR-017, US3)
- [x] Guardrails: provenance per line, `lyrics-not-from-user` reconciled, verbatim check, imitation,
      injection (FR-018 - FR-024, SC-005)
- [x] Model and quality: Ollama default, eval set nl/en, deterministic layer, Claude optional, model
      shown (FR-005, FR-025, SC-001)
- [x] Journey levels, accessibility, performance (FR-028, FR-029, SC-004, SC-008)
- [x] Instrument-neutral principle: not applicable (the feature concerns lyrics only; it neither
      favours nor excludes an instrument family)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows (check, devices, writing help, agent, degraded/local)
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] Contracts drafted and linted (backend delta Redocly-valid; ids, catalogue, output schema, eval set)
