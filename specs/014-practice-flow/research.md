# Research: Practice Flow (feature 014)

Architect, 2026-11-16. Spikes ran locally with the main checkout's worker environment (Python
3.12, librosa 1.0.1, scikit-learn 1.9.1, music21 10.5.2, verovio 6.4.0) on synthetic material
only; scripts were throw-away (not committed). Web facts checked on 2026-11-16 (sources at the
end). Decisions: ADR-023.

## R-01 Structure analysis: candidates and licences (S2)

| Candidate | What it gives | Licence (code / weights) | Fit |
|---|---|---|---|
| librosa `segment` + scipy + scikit-learn (own analyser) | recurrence / self-similarity, Laplacian segmentation, agglomerative clustering, novelty building blocks | ISC / BSD-3 / BSD-3, all **already locked** by the worker (librosa 1.0.1 pulls scikit-learn 1.9.1, scipy) | **Chosen** (ADR-023 D1): no new dependency, CPU, seconds per song, uses our beat grid and our stems |
| all-in-one (`allin1`, mir-aidj) | beats, downbeats, functional labels (start, end, intro, outro, break, bridge, inst, solo, verse, chorus), trained on Harmonix | code MIT; weights licence not stated on the repository | Rejected for now: needs NATTEN (pre-built per torch version on Linux, built from source on Windows) and madmom from git (madmom models CC BY-NC-SA); runs Demucs itself; GPU benchmark only. Kept as a candidate behind the `StructureAnalyzer` port |
| MSAF | boundary + letter-label algorithms (Foote, SF, OLDA, scluster, ...) | MIT | Rejected: no functional labels, old packaging; the needed algorithm is short on librosa |
| SongFormer / transformer structure models (2025) | functional labels | research licences, large SSL backbones | Rejected: GPU-sized for a CPU practice aid; licences unclear |
| Essentia (`SBic`, structure tools) | segmentation | AGPL-3.0 | Rejected: copyleft, never imported (ADR-017 D2 rule) |

## R-02 Spike S-01: segmentation on a synthetic structured song

Material: a synthetic 120 s song at 120 BPM (4/4, known grid): Intro 4 bars, Verse 8, Chorus 8,
Verse 8, Chorus 8, Guitar solo 8, Bridge 4, Chorus 8, Outro 4 (8 true boundaries). Accompaniment:
harmonic tones per chord (brightness per section), noise-burst drums (louder in choruses), a
separate "vocals" signal in verse/chorus/bridge, a bright lead in the solo.

| Method | Found | Recall within 1 bar | Precision within 1 bar | Time |
|---|---|---|---|---|
| Laplacian segmentation (librosa example), beat-synchronous, k = 4/5/6 | 15/21/23 | 0.62 | 0.22-0.33 | 1.4-3.9 s |
| same + bar majority vote + merge of runs < 2 bars | 16-20 | 0.38-0.62 | 0.16-0.33 | - |
| **Foote novelty**, bar-synchronous chroma + MFCC + RMS stacked over 2 bars, checkerboard kernel 2 x 4 bars, peaks >= 4 bars apart | 7 | **0.75** (6/8) | **0.86** (6/7) | 0.55 s |
| Vocal activity (vocals-stem RMS > 10 % of its 95th percentile, per bar) | - | marks 3 boundaries exactly (bars 36, 44, 56) | - | - |

Findings:

1. The Laplacian method finds chord-level repetition (two-bar alternation inside verses and
   choruses), so it over-segments; novelty with a 4-bar half kernel works on the section level.
2. Novelty misses the short 4-bar bridge -> chorus boundary (bar 48) and lands 3 bars late on the
   outro (59 vs 56). The vocals stem fixes the outro boundary exactly (vocals stop at 56). The
   bridge boundary remains a known weakness (short sections between two vocal parts).
3. The vocal-activity ratio per true section was exactly 0 or 1 here; on real stems bleed makes it
   softer, hence a threshold relative to the stem's own 95th percentile and a minimum of 2 bars.
4. Speed: < 2 s for 120 s of audio including feature extraction; a 5-minute song is estimated at
   3-6 s (SC-001 budget 10 s).

Consequence (ADR-023 D1): novelty boundaries **union** vocal-activity switches, merge < 4 bars,
cluster for repetition letters, rule-based kinds. Phase 0 part 2 task T010 repeats the spike
on the 6-form synthetic test set (one solo instrument per family) and sets the thresholds; the
SC-002 targets (recall >= 0.80, precision >= 0.75, solo labelled 5/6) are then fixed in the
worker's tests.

## R-03 Functional labelling rules (S2)

Inputs per segment: position, cluster letter and its count, mean mix RMS (relative to the song's
median), vocal ratio, per-stem energy shares (6-stem: vocals, drums, bass, guitar, piano, other;
4-stem fallback: vocals, drums, bass, other; split parents add their detail stems, e.g.
`other.woodwinds`, `other.brass`, `guitar.electric-guitar`, `piano.keys`), length in bars.

Rules, in order (first match wins), data-model 2.3 has the thresholds:

1. Only one segment -> `other` "Song".
2. First segment with vocal ratio < 0.25 -> `intro`; last segment with vocal ratio < 0.25 ->
   `outro` (also when the mix energy falls).
3. Vocal segments (ratio >= 0.5): among clusters occurring >= 2 times, the one with the highest
   mean energy -> `chorus`; other repeating vocal clusters -> `verse`; a vocal segment whose cluster
   occurs once and lies after the first chorus -> `bridge`; else `verse`.
4. Low-vocal segments in the middle: if one non-drum stem holds >= 40 % of the non-drum energy
   and at least 1.5 x its own song median -> `solo` with that instrument; else `instrumental`.
5. No cluster repeats (`few-repetitions`): kinds from rule 2 only; others `other` named "Part A",
   "Part B" by cluster.
6. Numbering per kind in time order ("Verse 1", "Verse 2"); a kind that occurs once gets no
   number ("Bridge"). Solo labels "<Instrument> solo" from the 004 stem display names.

## R-04 Score view: Verovio bar boxes (S5), spike S-02

Settings = the 008 binding options (`pageWidth 2100`, `pageHeight 2970`, `scale 40`, `breaks
auto`, `footer none`) plus `svgBoundingBoxes: true` for the bar map pass only.

| Observation | Value |
|---|---|
| Output size | outer `<svg width="840px" height="1188px">`, inner `viewBox="0 0 21000 29700"`, `g.page-margin` `translate(500, 500)` |
| Bars | one `<g class="measure" id=...>` per bar (120 of 120 found over 2 pages) |
| Bar box | `g.measure.bounding-box` is empty; each `g.staff` inside the measure has `g.staff.bounding-box > rect(x, y, width, height)`; the union of those rects (+ margin translate) is the bar box; grand staff / multi-staff = union over staves |
| Time | 120 bars: 0.05 s plain SVG, 0.03 s with bounding boxes (2 pages) |
| Size | 257 + 108 KB per page plain; 445 + 209 KB with bounding boxes (so the boxes pass is parsed and discarded, the plain pages are stored) |
| Safety | no `<script>`, no `foreignObject`, no external hrefs; glyphs are inline `<defs>` + `<use>` |
| Fonts | `font-family="Times, serif"` for titles, `Leipzig` for the tempo-mark glyph text (`<tspan>`) |

Consequences:

- The bar map is computed in the worker from the boxes pass; pages are served as `<img>`
  (ADR-023 D3). Box coordinates are stored as fractions of the page (x / 21000 after the margin
  translate), so the browser positions the highlight with CSS percentages.
- **Font**: an SVG used as an image cannot load system or external fonts; the worker inserts one
  `<style>@font-face{font-family:'Leipzig';src:url(data:font/woff2;base64,...)}</style>` taken from
  Verovio's packaged `data/Leipzig.css` (about 60-70 KB per page). Phase 0 part 2 (T011) verifies
  the glyph in Chromium and Firefox; fallback: strip the Leipzig text and show the tempo in the
  panel header.
- Bar times are not taken from Verovio (`renderToTimemap` uses the MusicXML tempo). The worker
  maps measure *k* to the time base the score was made with (008 data-model 2.4: bar 1 at the
  first downbeat, a pickup measure before it); the API shifts by the score's range start and, for
  scores from an exported audio item, by that item's range start.
- Verovio's measure order equals MusicXML measure order (no repeats are written by 008), so bar *k*
  is the *k*-th `g.measure` in document order across pages.

## R-05 Clock and speed for the follower (S5 + 012)

The karaoke banner (ADR-015 D5) already draws on the audio clock with output-latency compensation
and loop-wrap handling. The follower uses the same function, so its accuracy equals the banner's.
With 012's time-stretch worklet, the engine's position is assumed to stay in **track seconds**
(A-012-1: `transportPosition` scales context time by the playback rate and subtracts the
worklet's latency). If 012 exposes the heard position differently, the follower calls whatever
the karaoke banner calls; no separate clock is introduced. Bar lookup is O(1) per frame (forward
cursor) and O(log n) after seeks; per frame the engine writes at most one `style` and two
attributes.

## R-06 PDF generation for setlists (S6)

| Option | Licence | Notes | Verdict |
|---|---|---|---|
| ReportLab 5.1.0 (PyPI, 2026-10-22) | BSD (ReportLab open-source licence, BSD-style) | Python >= 3.9, classifiers to 3.14; Platypus flowables (paragraphs, tables, page templates, automatic pagination); TrueType embedding | **Chosen**, in the worker |
| PDFsharp / MigraDoc 6.5.0 (NuGet, 2026-10-14) | **AGPL-3.0-only** | The 6.5 line is AGPL; older MIT 6.2.x unmaintained | Rejected |
| QuestPDF | Community licence (not OSI) | | Rejected |
| PdfPig (Apache-2.0) writer | Apache-2.0 | Low-level builder, no line wrapping or pagination | Rejected (we would write a layout engine) |
| 008 route (SVG -> CairoSVG -> pypdf) | LGPL / BSD | No text flow; lyrics pagination by hand | Rejected |
| Browser printing | - | Not usable by the 011 agent; output differs per browser | Rejected |

Font: ReportLab's built-in Type 1 fonts are Latin-1 only. DejaVu Sans (Debian `fonts-dejavu-core`,
Bitstream Vera licence with DejaVu changes in the public domain; permissive, OSI-compatible
font licence) covers Latin, Greek and Cyrillic; CJK is a documented limitation (edge case in
spec.md). The worker image already installs fontconfig; devops adds the package (T014).
ReportLab's optional `accel`, `pycairo`, `bidi` and `shaping` extras are not needed. Its runtime
dependencies (pillow, charset-normalizer) are already in `worker/uv.lock` (pillow 13.0.0,
charset-normalizer 3.5.2); Phase 0 part 2 (T002) locks ReportLab and records what it adds (R-12).

## R-07 Keyboard, screen readers and pedals (S7)

- WCAG 2.2 SC 2.1.4 (Character Key Shortcuts, level A): single-character shortcuts need a way to
  turn them off, remap them, or be active only on focus. 014 offers both "turn off" (one setting)
  and remapping.
- Screen readers: NVDA and JAWS in browse mode consume single letters (quick navigation, e.g. H,
  B, L for list, K for link) before the page sees them; in focus/forms mode keys reach the page.
  Our guard keeps sliders, list boxes, grids and menus native (arrow keys) and our modifier chords
  avoid Insert/CapsLock (screen-reader keys) and Alt combinations. VoiceOver uses Ctrl+Option
  (never bound: no Alt/Option chords).
- Browser-reserved chords that the page must not take: Ctrl/Cmd + W, T, N, L, R, Q, P, F, 0, +, -, =,
  Tab; F5, F6, F7 (caret browsing), F11, F12. `Ctrl+Right/Left` is free in pages outside text fields
  (on macOS 014 uses Cmd, because Ctrl+Arrow switches Spaces).
- Page-turner pedals (e.g. AirTurn, PageFlip, Donner, generic Bluetooth "page turners") pair as
  HID keyboards and send one of: PageDown/PageUp (most common default), Right/Left, Down/Up,
  Space/Enter, sometimes media keys. Auto-repeat while a pedal is held depends on the model. A
  "Learn" capture plus presets covers all of them; media keys (`MediaPlayPause`) are bindable
  because `KeyboardEvent.key` reports them in Chromium and Firefox when the page has focus.
- The 004 implementation already matches on `event.key` lower-cased, ignores repeats and Alt/meta
  chords, and guards typing contexts; 014 extends the guard (sliders, list boxes, grids, menus) and
  makes the map data-driven (web-interop 1.1.0).

## R-08 Practice time measurement (S8)

- The engine reports `OnPositionChanged` at <= 10 Hz while playing and `OnPlaybackStateChanged`
  on every change. Wall-clock time between `playing` and the next non-playing state, measured with
  .NET `TimeProvider` in the Blazor circuit, is exact to the interop latency (< 50 ms per
  transition), well inside SC-007 (2 s per 10 min).
- Per-section split: each elapsed interval is assigned to the section containing the last reported
  position; with 10 Hz positions the misattribution at a section boundary is <= 100 ms per
  crossing.
- Loop passes and the speed of a pass need an event at the wrap: engine 2.4.0 `OnLoopPass`
  (ADR-023 D7), unless 012 already has one.
- Upserting every 30 s bounds the loss at a crash (FR-029). A session record is about 1 KB; a year
  of daily practice is < 1 MB in SQLite.

## R-09 Charts (S8)

Radzen Blazor (MIT, already used) has `RadzenChart` with column and line series; rendering is
local SVG, no external service. Every chart gets a "Show as table" toggle (table with the same
data) and a text summary for screen readers ("12 minutes today, 48 this week").

## R-10 Spike T010: segmentation thresholds on the 6-form synthetic set (Phase 0 part 2, 2026-11-17)

Material (throw-away script, worker environment, librosa 1.0.1, scikit-learn 1.9.1): six synthetic
songs at 120 BPM, 4/4 (bar = 2 s), separate stems (vocals, drums, bass, guitar, piano, other with an
`other.woodwinds` detail stem), 3 % bleed of the mix into every stem. Forms (bars): guitar solo
`I4 V8 C8 V8 C8 S8 C8 O4`; piano solo `I4 V8 C8 S8 V8 C8 O4`; bass solo `I4 V8 C8 V8 S8 C8 O4`;
woodwinds detail solo `I4 V8 C8 S8 C8 O4`; vocals-led bridge `I4 V8 C8 V8 C8 B4 C8 O4`; drums-only break
`I4 V8 C8 D4 C8 O4`. Verse and chorus differ in harmony, brightness and drum level; in a solo the other
instruments drop to 35 %. Grid known (downbeats every 2 s).

| Setting | Values tried | Frozen |
|---|---|---|
| checkerboard half size (bars) | 2, 3, 4, 6 | **6** |
| novelty `delta` | 0, 0.05, 0.1 | **0** (`pre/post_max 3`, `pre/post_avg 2`, `wait 3`) |
| vocal activity threshold (x p95 of the vocals RMS) | 0.1, 0.2 | **0.1** (no difference) |
| stack memory | 1, 2 | **2** |
| minimum section (bars; first/last) | 3, 4 | **4** (2) (no difference) |
| cluster distance (cosine, average linkage) | 0.10, 0.15, 0.25, 0.35 | **0.25** |
| solo rule | absolute RMS >= 1.5 x median (data-model draft); share >= 1.3/1.5/2.0 x median share | **share >= 0.4 and >= 1.3 x the source's median share** |

| Configuration | Boundary recall (+-1 bar) | Precision (+-1 bar) | Solo/bridge/break kind right | Section kinds right |
|---|---|---|---|---|
| Phase 0 part 1 draft (half 4, absolute RMS rule) | 0.78 | 0.80 | 4/6 | - |
| half 6, absolute RMS rule | 0.97 | 0.97 | 4/6 (guitar and bass solos missed: their RMS is only 1.3x / 1.0x their median) | 37/42 |
| **frozen** (half 6, cluster 0.25, share rule 1.3) | **0.97** | **0.97** | **6/6** | **41/42** |

Runtime: 1.9 s for a 296 s song (features + analysis, one core), inside the SC-001 budget of 10 s.
Remaining miss: one boundary in 34 lands two bars off (bass song, chorus -> bass solo), and one verse is
labelled as an instrumental part. On synthetic stems the margins of the share rule are thin (bass solo
share 0.69 vs a median share of about 0.5); the SC-002 target (5 of 6) leaves room for one miss on real
songs. The constants are frozen in data-model 2.2/2.3 and become the defaults of the beat-analysis tests
(T020-T025, T050).

## R-11 Check T011: the Leipzig font in score-view pages shown as images (2026-11-17)

A Verovio 6.4.0 page (008 options) with a tempo mark (`MetronomeMark` "quarter = 96") was rendered as an
`<img>` (data URI) in Chromium 159.0.8154.6 and Firefox 159.0 (Playwright 1.65.1). Findings:

- Verovio 6.4.0 **already embeds** the full Leipzig WOFF2 as an `@font-face` in a `<style>` of every page
  (58,635 bytes of an 82,261-byte page); the tempo glyph (`U+ECA5` in a `tspan font-family="Leipzig"`)
  renders correctly in both browsers.
- A probe SVG with only that glyph: with the `@font-face` both browsers draw the note; without it Chromium
  draws an empty box and Firefox a hex box, so the embedded font is what makes it work (the host has no
  Leipzig font installed).
- Adding Verovio's `Leipzig.css` again would add another 58.6 KB per page for nothing.

Consequence: data-model 2.5 step 4 changes from "insert the `@font-face`" to "keep Verovio's embedded
`@font-face` and verify that it is present" (the T026 test asserts its presence instead of a first-child
position). The fallback (strip the Leipzig text, tempo in the panel header) is not needed. Page size per
view page about 80-260 KB (the font accounts for 58.6 KB of each).

## R-12 ReportLab lock (T002, 2026-11-17)

`reportlab==5.1.0` (PyPI upload 2026-10-22, 26 days before the lock; newest release; pure-Python wheel
`reportlab-5.1.0-py3-none-any.whl`) is locked in `worker/pyproject.toml` and `worker/uv.lock`. Licence:
"BSD license (see license.txt)", classifier "OSI Approved :: BSD License" (the ReportLab open-source
licence is BSD-3-Clause style). PyPI lists no known vulnerabilities for 5.1.0. Runtime requirements
`pillow>=9.0.0` and `charset-normalizer` are already locked (pillow 13.0.0, charset-normalizer 3.5.2), so
`uv lock` added **only** `reportlab` (15 lines in `uv.lock`); no extras (`accel`, `pycairo`, `bidi`,
`shaping`). mypy: `reportlab.*` is in the `ignore_missing_imports` overrides (no type information).

## Sources

- all-in-one: https://github.com/mir-aidj/all-in-one (MIT code; NATTEN and madmom install notes)
- MSAF: https://github.com/urinieto/msaf (MIT)
- McFee & Ellis 2014, "Analyzing song structure with spectral clustering", ISMIR; librosa
  example "Laplacian segmentation": https://librosa.org/doc/latest/auto_examples/plot_segmentation.html
- Foote 2000, "Automatic audio segmentation using a measure of audio novelty", ICME
- PDFsharp / PDFsharp-MigraDoc 6.5.0 on NuGet (AGPL-3.0-only): https://www.nuget.org/packages/PDFsharp-MigraDoc
- ReportLab 5.1.0 on PyPI (BSD): https://pypi.org/project/reportlab/
- WCAG 2.2 SC 2.1.4 Character Key Shortcuts: https://www.w3.org/WAI/WCAG22/Understanding/character-key-shortcuts.html
- Verovio toolkit options (`svgBoundingBoxes`): https://book.verovio.org/toolkit-reference/toolkit-options.html
- ReportLab 5.1.0 release files and classifiers: https://pypi.org/pypi/reportlab/5.1.0/json (checked 2026-11-17)
