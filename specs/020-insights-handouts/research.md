# Research: Insights and Handouts (feature 020)

Architect, 2026-11-16. Decisions are binding through
[ADR-029](../../docs/decisions/ADR-029-stem-quality-report-and-handouts.md). Items marked
**(P0)** are measurements of Phase 0 (tasks T005-T008). They run when 014 and 016 are merged and
their results are written into this file under "Phase 0 measurements". No number below is claimed
as measured unless it says so.

## R-01 What a reference-free quality report can and cannot do

**Question**: can StemStudio say how good a stem is without the original multitrack?

**Findings**

- The standard measures for source separation (BSS Eval SDR/SIR/SAR, Vincent et al. 2006; BSS
  Eval v4 in `museval` for SiSEC 2018) need the true sources. StemStudio never has them: users
  import finished mixes.
- Learned *non-intrusive* quality predictors are established for speech (e.g. DNSMOS). For music
  separation there is research on predicting SDR without references, but no open, validated
  predictor with a licence and size that StemStudio could ship and keep maintained was found.
  Training our own would need multitrack data whose licences (MUSDB18: non-commercial research
  terms) and size do not fit a local practice tool. **Rejected for 020**; re-check at Phase 0
  (T005) and record the search.
- What *is* possible: signal measures on the stems themselves and on their sum (the sum of the
  stems is the mix the user hears). They detect typical failure patterns: (a) one stem contains a
  scaled copy of another (bleed), (b) a stem flickers or drops out in ways the mix does not
  (separation artefacts, "musical noise"), (c) a stem is mostly spill because the instrument is
  not in the song. Feature 002 already computes level, active share, model residual and
  reconstruction checks for detail splits.

**Decision**: estimates only, with reliability labels, validated where possible (R-04). Every
report says so (spec FR-004). The UI never shows a number as "SDR".

## R-02 Bleed measures (candidates and choice)

Analysis representation (shared by all measures): mono mix-down, 22,050 Hz, STFT 2048/512,
64-band mel power spectrogram in dB; the "mix" is the sum of the six standard stems. Memory stays
small (5 min = about 13,000 frames x 64 bands x float32 = 3.3 MB per stem).

| Id | Measure | Idea | Strength | Weakness |
|---|---|---|---|---|
| B1 | Onset-shape coupling | at onsets of stem j, how similar is stem i's spectral change to stem j's | catches drum hits in vocals well | confused by instruments that hit together |
| **B2** | **Leakage share (non-negative per-band regression)** | per band b, fit `P_i(t,b) ~ g_ib * P_j(t,b)` (power, non-negative least squares over active frames of j); leakage energy `sum g_ib * P_j`, share = leakage / energy of i; take the largest source j | interpretable ("about 12 % of this stem looks like drums"), cheap, linear in the bleed amount by construction | co-occurrence inflates g (kick/bass, doubled guitars) |
| B3 | Phantom detection | active share and level of i relative to the mix, combined with B2 share | very reliable for absent instruments (002 gate statistics: real >= 66 % active frames, bleed <= 6.4 %) | only answers "present or not" |

**Co-occurrence guard** (for B2): bleed is a *constant-ratio copy*; joint playing is not. The fit
is repeated on the frames where i is *quiet relative to j* (i at least 15 dB below j in that
band); leakage is the minimum of the full fit and the quiet-frame fit. Pairs known to play
together (drums/bass, guitar/other, piano/other) additionally cap the level at Medium when the
two fits disagree by more than a factor 2 ("may be the two instruments playing together", spec
Edge Cases).

**Decision**: B2 with the guard as the bleed index (0..1, the leakage share), B3 for "Probably not
in this song"; B1 is computed in the Phase 0 validation as a challenger and replaces B2 only if
it ranks better on V1 and V2 (recorded in research).

## R-03 Artefact measures (candidates and choice)

| Id | Measure | Idea |
|---|---|---|
| **A1** | **Flicker ratio** | share of time-frequency cells in 1-8 kHz, 30-60 dB below the stem's band peak, whose level jumps by more than 12 dB between adjacent frames; divided by the same share computed on the mix (genuine transients are in the mix too). > 1 means flicker the music does not have ("watery" musical noise) |
| **A2** | **Dropouts** | share of the stem's active frames where the broadband level falls more than 30 dB below the median of its +-0.5 s neighbourhood and recovers within 0.3 s (gating holes), again relative to the mix |
| A3 | 002 checks (detail stems only) | `modelResidualDb` and the `rest` stem's share of the parent energy (high share = the model could not attribute the sound) |
| A4 | Bandwidth loss | high-frequency share vs the mix; **rejected**: instrument-dependent (a bass has no top end), no reliable reference |

**Decision**: artefact index = max(normalised A1, normalised A2), plus A3 as artefact *kinds* for
detail stems. Normalisation constants and level thresholds are calibrated in Phase 0 on V1 and
become constants in the worker's `insights/thresholds.py`; any change bumps `analyzerVersion`.

## R-04 Validation protocol and gate G1 (P0)

**V1, synthetic (committed, generated by the qa fixture generator, CI-safe)**: per family a
synthetic instrument line (vocals-like formant tone, drums from noise bursts and decaying sines,
bass, guitar-like plucked tone, piano-like tone, pad as `other`), 30 s at 44.1 kHz.

- Bleed: `stem_i' = stem_i + alpha * stem_j` for alpha in {0, 0.03, 0.1, 0.3} (amplitude), each
  family pair; plus the "playing together" control (bass and kick on the same onsets, alpha = 0).
- Artefacts: random binary time-frequency masking of the stem's STFT with drop probability p in
  {0, 0.05, 0.15, 0.3} in 1-8 kHz (musical noise), and 50-200 ms gating holes at rate r in
  {0, 0.5, 2} per second.
- Targets (SC-002): Spearman rho >= 0.8 between injected amount and index; level correct
  (Low/Medium/High mapped to the grid) for >= 80 % of cases; the "playing together" control
  never rated High.

**V2, real (local only, never committed, not in CI)**: MUSDB18-HQ test split (50 songs, four
sources: vocals, drums, bass, other; non-commercial research terms, used only to calibrate
offline). Separate the mixes with `htdemucs_6s` (the production model; guitar and piano are
summed into `other` for scoring), compute BSS Eval v4 SIR and SAR per song and stem with
`museval` 0.4.1 (MIT) in a throw-away environment, and the Spearman correlation of our bleed
index with -SIR and our artefact index with -SAR per stem type.

**Gate G1** (per stem type and dimension): rho >= 0.5 (bleed vs SIR) and rho >= 0.4 (artefacts
vs SAR) -> label "validated"; otherwise "rough indication" and only the High level is shown for
that dimension (Low/Medium become "unknown"). Guitar, piano and all detail stems are not covered
by V2 and are always "rough indication". The results table is published under "Phase 0
measurements" and summarised in the help topic `stem-quality-reliability`.

## R-05 Rating and tips (product rules)

- Levels per dimension: Low / Medium / High from the calibrated thresholds; the rating is the
  worse of the two (Low -> Good, Medium -> Fair, High -> Poor); B3 -> "Probably not in this song";
  fewer than 10 s of active audio -> "Not rated".
- Tips are product logic, not signal logic: they depend on the track's state (detail split
  done? pipeline? device? G2 shipped?). They are therefore derived in the backend domain from the
  worker's measures (data-model 5.3), so they update when the state changes without re-analysis.
- **"Re-run on the GPU model" is not a tip.** 003 (ADR-012) runs the same models, the same
  chunking and float32 on both devices; detail models agree with the CPU to >= 52 dB SDR. A GPU
  re-run gives the same stems. The backlog example is replaced by R-06.

## R-06 A real "better model": high-quality vocal pipeline and gate G2 (P0)

**Idea**: RoFormer vocal models outperform Demucs on vocals by a wide margin in public
leaderboards (e.g. the BS-RoFormer checkpoint `model_bs_roformer_ep_317_sdr_12.9755`, whose name
carries its reported multisong vocals SDR; Mel-Band RoFormer vocal checkpoints around 11 dB),
while `htdemucs` reports about 9 dB average on MUSDB18. StemStudio already runs BS-RoFormer and
Mel-Band RoFormer code (002, vendored MSST code, ADR-010) through the 003 device policy.

**Pipeline `vocals-hq`**: input = sum of the track's six standard stems (the original audio is not
kept by the API; Phase 0 measures the sum against the original on fixtures, expected below
-30 dB difference) -> RoFormer vocal model -> `vocals`; `instrumental = input - vocals` ->
`htdemucs_6s` -> drums, bass, guitar, piano, other (its own vocals output is folded into `other`
when above the 002 sub-stem gate, else dropped). Effect: better vocals and less vocal bleed in
every other stem.

**Candidates to verify in Phase 0 (T006)**: checkpoint URL, SHA-256, size, licence status (the
ADR-009 weights exception covers unclear or non-commercial licences; explicit prohibitions are
excluded), GPU memory need (NVML peak), time per minute of audio on the RTX 5070 Ti and on the
CPU.

**Gate G2** (SC-007, on V2): median vocals SDR gain >= 1.0 dB **and** median SIR gain of the
other stems >= 0.5 dB versus the production pipeline, with the CPU time per 5-minute song
recorded. If G2 fails, User Story 5, FR-007, SC-007, the `reseparation` contract parts and the
`rerun-high-quality` tip are removed (DELTA.md "Phase 0 outcome").

**Rejected**: Demucs test-time `shifts` (published gains of a few tenths of a dB at several times
the cost: not worth a tip); `htdemucs_ft` (four stems only, no guitar/piano; mixing it with 6s
breaks the stem sum).

## R-07 PDF engine for handouts

Requirements: rendered in the Python worker (spec), the app's style (tokens, Archivo), page
header/footer with "page n of N", contents with page numbers, bookmarks, real text, notation
pages from existing PDFs, no network access, deterministic enough for tests, OSI licence.

| Option | Licence | Fit | Verdict |
|---|---|---|---|
| **WeasyPrint 70.0** (2026-09-08) | BSD-3-Clause; deps pydyf, tinyhtml5, tinycss2/cssselect2 (already locked), Pyphen (MPL-1.1/LGPL tri-licence, used under MPL), Pillow (locked), fonttools[woff] (MIT; brotli MIT, zopfli Apache-2.0); system Pango >= 1.44 (LGPL-2.1+), HarfBuzz (MIT), fontconfig | HTML + CSS paged media (`@page`, running footers, `counter(pages)`, `target-counter()` for the contents), bookmarks from headings, custom `url_fetcher` (we block everything but allow-listed files), PDF metadata; reuses the CSS approach of `tools/docs/styles/print.css` and the app tokens | **Chosen** |
| Chromium via Playwright (as `tools/docs`) | BSD/Apache | same CSS, but a browser (about 300 MB) and Node in the worker image | rejected for the worker image |
| Typst (`typst` Python wheel) | Apache-2.0 | excellent typography, sandboxed, embeds PDF/SVG | rejected: a second styling language, tokens would need translation; recorded as fallback if WeasyPrint fails P0-4 |
| ReportLab | BSD | low-level drawing, no CSS reuse | rejected |

Notation pages: WeasyPrint cannot place a PDF page as an image. The renderer lays out one
*frame page* per notation page (header, footer, an empty frame of known size); afterwards `pypdf`
(already locked, 6.21.4) merges the notation page scaled into the frame
(`merge_transformed_page`). Page numbering and bookmarks stay WeasyPrint's. A4 notation on Letter
is scaled to fit.

Templates: Jinja2 3.1.6 (already locked as a transitive dependency; becomes direct) with
`autoescape=True`: lyrics and notes are user/online text and must never become markup.

**(P0-4)**: render the fixture handout with WeasyPrint 70.0 in the worker image: Archivo variable
WOFF2 (`font-stretch` 125 % for the title) renders, chord row alignment is stable, a 20-song
setlist renders within 90 s, no `.notdef` glyphs for Latin-1 + Latin Extended-A, Pango version in
both image variants. Fallback if the variable font misrenders: static instances (Regular, Bold,
Expanded Bold) generated with the fontTools instancer at build (OFL allows).

## R-08 Chords over lyrics

- With word timings (006 aligned lyrics): each chord is attached to the word whose start is the
  last one at or before the chord start (+-150 ms tolerance forward, so a chord just before the
  word sits on it); several chords on one word are kept in order; position = character index of
  the word in the line.
- With line timings only: chords that start within a line's time span are listed at the line
  start, in order (`[Am] [F] [C]`).
- Without timings or without lyrics: no chords-over-lyrics block; the chord chart per section is
  printed instead (it is in every family that has chords).
- Chord chart per section: one cell per bar (beat grid downbeats), the chord(s) active on the
  bar's beats, repeated chords shown as `%` only for whole-bar repeats.
- Accidentals in ASCII (`Bb`, `F#m7`): avoids glyph gaps in Archivo and matches common chord
  charts. Transposition by semitones with the target key's spelling (flats for F, B-flat, E-flat
  keys; sharps otherwise).

The alignment and transposition are pure functions in the backend domain (the API assembles the
handout document; the worker only lays it out), so they are unit-tested without PDFs.

## R-09 Style consistency

The app's light-theme tokens (`src/StemStudio.Web/wwwroot/css/tokens.css`) and the Archivo font
(`wwwroot/fonts/archivo/`, OFL-1.1) are the single source. A Phase 0 script
(`worker/scripts/sync_handout_style.py`) copies the token block and the font into the worker
package (`handouts/assets/`) and writes their SHA-256 into `handouts/assets/manifest.json`; a
quality-gate check fails when the copies drift from the sources. Handout CSS uses only
`var(--ss-*)` tokens (no literal colours), like `tools/docs/styles/` (guard reused).

## R-10 Accessibility of the PDFs (honest scope)

Delivered: document title and language (`en`), bookmarks, real text in reading order for every
non-notation page, minimum text size 9 pt, AA contrast from the tokens, no information by colour
only (ratings are not in handouts). **Not delivered**: tagged PDF/UA conformance for the merged
document, because the notation pages are graphics from 008/016 without a tag tree; the help
topic says so.

## Phase 0 measurements

To be filled by T005-T008 (G1 table, G2 result, P0-4 rendering checks, analysis timing for
SC-001, the sum-of-stems difference, model facts for `vocals-hq`).
