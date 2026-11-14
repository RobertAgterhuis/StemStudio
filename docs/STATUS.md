# StemStudio MVP — Status

_Owner: CTO (orchestrator). Updated after every reviewed hand-off._

## Phase overview

| # | Phase | Owner | Status |
|---|-------|-------|--------|
| 1 | Constitution (`/speckit-constitution`) | CTO | Done (v1.0.0) |
| 2 | Specification (`/speckit-specify`) | CTO | Done |
| 3 | Clarification (`/speckit-clarify`) | CTO (autonomous, ADR-001) | Done |
| 4 | Plan, contracts, data model, skeleton (`/speckit-plan`) | architect | Done (reviewed: build 0 warnings, 10+24+2 tests green) |
| 5 | Tasks (`/speckit-tasks`) | architect | In progress |
| 6 | Analyze (`/speckit-analyze`) | CTO | Pending |
| 7 | Implementation (`/speckit-implement`) | team | Pending |
| 8 | E2E verification + MVP report | qa / CTO | Pending |

## Blockers

None.

## Decisions

- ADR-001: clarification decisions (FLAC stems, source-key cache identity, restart handling,
  peak meters, no autosave).
- ADR-002..007 (architect): layers, worker contract/ML stack, stem storage/serving, audio engine,
  testing tooling, CPM + xUnit v3 on VSTest.
- CTO: accept Demucs 4.1.0 (4.0.1 is incompatible with current torchaudio; see ADR-003).
- CTO: accept spotDL's transitive GPL/LGPL dependencies (OSI-approved, local single-user tool);
  list them in THIRD-PARTY-LICENSES.md and name the unofficial Spotify-client ToS risk under
  known limitations.
