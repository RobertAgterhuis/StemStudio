# ADR-029: Stem quality report, high-quality vocal pipeline and lesson handouts (feature 020)

- **Status**: Proposed (architect, 020 planning, 2026-11-16); becomes Accepted at 020 Phase 0
  with the G1/G2 results recorded under "Phase 0 outcome"
- **Numbering**: features 012-020 are specified in parallel; to avoid collisions each takes the
  ADR number `ADR-0(21 + feature - 12)`, so 020 -> ADR-029. The CTO may renumber at merge.
- **Context documents**: `specs/020-insights-handouts/{spec.md, research.md, data-model.md,
  DELTA.md}`, ADR-009 (weights exception), ADR-010 (detail models, vendored MSST code), ADR-012
  (device policy), ADR-013 (design system), ADR-016 (asset library, lanes, read-only library
  mount), ADR-017 (score PDFs)

## Context

MoSCoW C7 asks for a bleed and artefact score per stem with a tip, C8 for one-click lesson PDFs
per instrument. StemStudio has no reference multitrack for imported songs, runs identical models
on CPU and GPU (003), and already produces lyrics (006), score PDFs (008), sections and setlists
(014) and chords, tablature, drum notation and transposed parts (016). The constitution requires
honesty about limits (V), OSI code (IV), contracts first (III) and every feature working on the
CPU.

## Decisions

**D1 - Estimates, not metrics.** The quality report uses reference-free signal measures on the
stems and their sum: leakage share by non-negative per-band regression with a quiet-frame
co-occurrence guard (bleed), flicker and dropout ratios relative to the mix (artefacts), activity
and level (instrument absent), plus the 002 detail metrics. No learned predictor (none open and
validated; training data licences do not fit). Every report is labelled an estimate.

**D2 - Validation decides the labels.** Thresholds are calibrated on a committed synthetic set
(V1). A local-only run on MUSDB18-HQ (V2, never committed, not in CI) correlates the indices with
BSS Eval SIR/SAR per stem type. Gate G1 (rho >= 0.5 bleed/SIR, >= 0.4 artefacts/SAR) sets the
`validated` label; otherwise `indicative`, and only High is shown. Guitar, piano and detail stems
are always `indicative` (not covered by V2).

**D3 - Measures in the worker, tips in the backend.** The worker returns measures, levels and a
rating (signal knowledge, versioned by `analyzerVersion`). Tips depend on track state (detail
split, pipeline, device), so the backend domain derives them with pure rules; they update without
re-analysis.

**D4 - No "re-run on the GPU" tip.** The GPU gives the same stems (ADR-012). The only "better
model" action is D5.

**D5 - Optional `vocals-hq` pipeline, gated by G2.** RoFormer vocal model on the sum of the six
stems, then `htdemucs_6s` on the instrumental. Runs through the 003 device policy and the vendored
MSST code of ADR-010; weights under ADR-009 when their licence is unclear. It ships only if the
measured median vocals SDR gain is >= 1.0 dB and the other stems' SIR gain >= 0.5 dB (G2).
Re-separation replaces the six stems atomically, removes detail stems only with consent, prunes
detail settings in mixes, never changes library items, and keeps the old stems on failure.

**D6 - The API assembles, the worker renders.** The backend builds a complete, resolved
`HandoutDocument` (facts, transposed chords placed over lyric words, bars per section, notes,
library paths of notation PDFs). The worker lays it out without database or network access. Chord
placement, transposition and bar maths are pure .NET domain functions with unit tests.

**D7 - WeasyPrint + Jinja2 + pypdf.** HTML/CSS paged media rendered by WeasyPrint (BSD-3; Pango
LGPL as a system library) with Jinja2 (BSD-3, autoescape on) templates; notation pages from
existing PDFs are merged into frame pages by pypdf. The style reuses the app's light-theme tokens
and the Archivo font, synced into the worker by a script and checked for drift. A locked-down
`url_fetcher` serves only allow-listed library files and packaged assets; any other URL fails.
Rejected: Chromium in the worker image (size, Node), Typst (second styling language; kept as a
fallback), ReportLab (no CSS reuse).

**D8 - One `insights` lane.** Quality and handout jobs share a new worker lane, so exports (007)
and transcriptions (008) are never delayed and separations never block a handout.

**D9 - Handouts are library items.** Kind `handout`, one `pdf` file, `HandoutFacts` and a
`HandoutRecipe` for "Make again". The personal-use notice is fixed text on every page and
validated by the worker; it cannot be turned off.

**D10 - Seven equal families.** Band overview, vocals, guitar, bass, keys, drums, melodic
instruments (C/B-flat/E-flat/F). Each has facts, form chart, notation or chords, and notes; E2E
covers each family.

**D11 - Adapters to 014/016.** The assembler reads sections, setlists, practice log, chords and
notation through one port each; the assumed shapes (data-model 8) are reconciled at Phase 0
without touching the rest.

## Consequences

- New worker dependency WeasyPrint 70.0 and its Python deps (pydyf, tinyhtml5, Pyphen,
  fonttools[woff], zopfli) plus Pango/HarfBuzz/fontconfig system packages in both images; Jinja2
  becomes a direct dependency. Licences recorded by the CTO in `docs/THIRD-PARTY-LICENSES.md`.
- If G2 passes, one more model download (size measured in Phase 0) and the separation queue gains
  a job type; if it fails, nothing of D5 is built.
- Ratings can be wrong; the UI and help say so, and kick/bass-style co-occurrence is capped at
  Medium.
- Handout PDFs are not PDF/UA conformant (notation pages are untagged graphics).
- 011's agent can call the new endpoints; its tool-catalogue minor is a later step (roadmap).

## Phase 0 outcome

To be written at Phase 0 (G1 table, G2 result, WeasyPrint checks P0-4).
