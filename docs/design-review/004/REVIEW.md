# Design review 004 (T038 / T068)

- **Reviewer**: CTO, under the owner's autonomous mandate of 2026-11-15 (owner review pending; the
  owner can request changes at any time).
- **Verdict**: approved. Coherent "colour belongs to the music" direction: neutral graphite/paper
  chrome, one fixed colour per stem, detail groups tied to their parent by colour and pattern,
  console-style mixer with pinned master and docked transport, level switch in the context bar.
- **Polish notes (non-blocking, backlog)**:
  1. Tracks page: the "Sort" label sits to the right of the search field while its select wraps
     below; align label and select.
  2. Mixer: long detail names ("acoustic-guitar", "electric-guitar") are truncated in narrow
     strips; consider a tooltip or two-line label (the strip-tab ellipsis CSS from T063 helps).
  3. Screenshots `forced-*.png` and the US1 mixer shots predate the last T063 CSS fixes; the visual
     baselines generated in the integration run are authoritative.
