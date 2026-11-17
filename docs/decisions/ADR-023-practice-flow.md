# ADR-023: Practice flow: song sections, score follow, setlists, shortcuts and pedals, practice log (feature 014)

- **Status**: Accepted (architect, 014 Phase 0 part 2, 2026-11-17; proposed 2026-11-16). Number
  checked: ADR-023 is unique on `main`, on the 022/023 round and on the 015 design branch (015 holds
  ADR-024, 022 ADR-036, 023 ADR-037). Amendments from the reconciliation with 012/013 and the Phase 0
  part 2 measurements are in the section "Phase 0 part 2 amendments" at the end; none changes a
  decision.
- **Context documents**: `specs/014-practice-flow/{spec.md, research.md, data-model.md,
  plan.md, contracts/DELTA.md}`, `docs/MoSCoW.md` (S2, S5, S6, S7, S8; principle "every
  instrument counts equally"), `docs/ROADMAP.md` (contract allocation backend 1.13.0, worker
  1.8.0, audio-engine 2.4.0, ui-test-ids 1.13.0), ADR-005 (engine design), ADR-009 (weights
  exception), ADR-013 (design system), ADR-015 (karaoke banner, shared frame loop), ADR-016
  (asset library, worker lanes, read-only library mount), ADR-017 (sheet music engraving),
  constitution 1.3.1.

## Context

Feature 014 turns StemStudio's mixer into a practice workflow for every instrument family:
automatic song sections that loop with one click (S2), sheet music that follows playback (S5),
setlists for rehearsals and gigs (S6), complete keyboard control plus page-turner foot pedals
(S7) and a local practice log (S8). It builds on the beat grid (001), stems and detail stems
(001/002), synced lyrics (006), the score assets of 008 (MusicXML engraved with Verovio) and on
feature 012, which is specified in parallel and is assumed to deliver speed (50-120 % without
pitch change), transpose, a speed trainer on the loop region and count-in (engine 2.3.0, backend
1.11.0). Every contract change here is additive on top of those.

Spikes (research.md, 2026-11-16, local, throw-away scripts):

1. Verovio 6.4.0 (already in the worker, LGPL, unmodified wheel) with `svgBoundingBoxes: true`
   writes, inside each `<g class="measure">`, one `<g class="staff bounding-box"><rect .../></g>`
   per staff; the union of those rects is the bar's box in the page's 21000 x 29700 inner
   coordinate system (page margin `translate(500, 500)`). 120 bars of bass: 2 A4 pages in
   0.03-0.05 s. The SVG contains no `<script>`, no `foreignObject` and no external references;
   music symbols are inline `<defs>` paths, but tempo-mark text uses the `Leipzig` font family.
2. A synthetic song (intro, verse, chorus, verse, chorus, guitar solo, bridge, chorus, outro;
   120 s, known grid): plain Laplacian segmentation (McFee & Ellis 2014, librosa example)
   over-segments at chord level (boundary precision 0.22-0.33). A Foote checkerboard novelty
   over bar-synchronous features stacked over two bars (kernel 2 x 4 bars) finds 6 of 8
   boundaries within one bar (precision 6/7) in 0.55 s. Of the two it misses, the vocals-stem
   activity marks one exactly (chorus to outro, bar 56); the short 4-bar bridge to chorus boundary
   (bar 48) stays missed. Analysis of 120 s takes < 2 s on the CPU.

## Decisions

### D1 Song sections come from our own structure analyser on the stems, behind a port

The worker gets a `StructureAnalyzer` port and a first implementation `stems-novelty` 1.0 in
`analysis/structure/`, built only on libraries the worker already locks (librosa ISC, numpy,
scipy BSD, scikit-learn BSD). No new dependency.

1. Features per bar of the beat grid (fixed 2 s frames when the grid is unreliable): chroma
   (CQT), MFCC, RMS of the mix (sum of the stems), and per-stem RMS of the six stems (or four
   with the `htdemucs` fallback).
2. Boundaries: Foote novelty (checkerboard kernel 2 x 4 bars) on the self-similarity of the
   features stacked over two bars, peaks at least 4 bars apart, plus every bar where the vocal
   activity of the vocals stem switches and stays switched for at least 2 bars. Every boundary sits
   on a downbeat (reliable grid) or a beat (grid present but unreliable) or is rounded to 0.1 s
   (no grid).
3. Segments shorter than 4 bars are merged into the more similar neighbour (intro/outro: 2 bars).
4. Repetition: segments are clustered (agglomerative, cosine distance on mean chroma + MFCC) into
   letters A, B, C, ...
5. Functional labels by rules on position, repetition, vocals and per-stem energy: low-vocal
   segments at the start and end are `intro`/`outro`; of the vocal clusters the one that repeats
   and has the highest energy is `chorus`, other repeating vocal clusters are `verse`; a vocal
   segment that occurs once after the first chorus is `bridge`; a low-vocal segment in the middle
   where one non-drum stem (guitar, piano, bass, other, a detail stem when the parent is split)
   dominates is `solo` **with that instrument** ("Guitar solo", "Piano solo", "Bass solo"), otherwise
   `instrumental`. Labels are numbered per kind ("Verse 2").
6. Each section has a confidence 0..1; the result lists warnings (`grid-unreliable`,
   `few-repetitions`, `no-vocals`).

The labels are an honest guess, shown as such ("Detected automatically - edit if needed"); the
user can edit, split, merge, rename, re-label and re-detect. The port keeps a later model-based
analyser possible without contract changes (`analyzer` name/version in the result).

Rejected:

| Candidate | Licence | Why not now |
|---|---|---|
| all-in-one (`allin1`, mir-aidj) | code MIT; weights licence not stated | Needs NATTEN (built per torch version, Windows from source) and madmom from git (madmom's own models are CC BY-NC-SA); runs its own Demucs although we already have stems; weights licence unclear (ADR-009 would allow it, but a second ML stack for one feature is not worth it before our analyser is measured). Revisit behind the same port if owner feedback on label quality is poor. |
| MSAF | MIT | Framework of older algorithms (boundaries and letter labels, no functional labels); stale packaging, pins old dependencies; the algorithms we need are a few dozen lines on librosa. |
| SongFormer and other 2025 transformer models | mixed / research | GPU-sized self-supervised backbones (MuQ/MusicFM), research licences; disproportionate for a practice aid that must run on the CPU. |
| Essentia | AGPL-3.0 | Copyleft would spread to the worker image; we import no GPL/AGPL code (ADR-017 D2). |
| Pure Laplacian segmentation (librosa example) | ISC | Spike: chord-level over-segmentation (precision 0.22-0.33). |

### D2 Sections are track data, edited as a whole, referenced by stable ids

Sections are stored per track in the backend (`TrackSections` + `Sections`), not per mix:
structure is a property of the song, every mix of the track uses it. The API returns and
replaces the whole ordered list (`PUT` with a `revision` for optimistic concurrency); existing
section ids are kept on edit so loops, the speed trainer and the practice log keep pointing at
them. Re-detection replaces the list (new ids) and needs `overwrite: true` once the user has
edited it. New tracks are analysed automatically after separation; existing tracks the first time
their mixer opens.

A mix's loop region gains an optional `sectionId` (additive on `LoopRegion`), set when the user
loops a section and cleared when A or B are moved by hand. The 012 speed trainer runs on the loop
region, so a section loop is a trainer region without any change to 012's contract.

### D3 Score follow: a server-made "score view" (SVG pages + bar map) and an engine-drawn highlight

- The worker renders a **score view** of a score asset (worker job `score-view`): the asset's
  MusicXML laid out with the 008 Verovio options, one SVG per page with the Leipzig font embedded
  as a `data:` URI `@font-face` (Verovio's own `data/Leipzig.css` WOFF2), and a **bar map**: per
  bar its number, page, box (union of the staff bounding boxes, in page fractions 0..1) and
  start/end time in the score's time base (from the same `TempoGrid` the score was made with).
  The API shifts the times to track seconds (range start, source asset range) and stores
  `assets/<id>/view/page-NNN.svg` + `bars.json`. New score assets get their view right after the
  score is ready; older ones on first open of the follow panel.
- The browser shows each page as `<img src=".../score-view/pages/{n}">`. SVG used as an image never
  runs scripts or loads external resources, so we do not need to inline or sanitise the SVG.
  Responses carry `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; font-src data:`
  and `X-Content-Type-Options: nosniff`.
- The **engine** (2.4.0) draws the highlight: `attachScoreFollower(elementId, view, options)`
  builds one absolutely positioned highlight element per page container (CSS percentages from the
  bar boxes, so no layout reads per frame) and on the shared animation frame (ADR-015 D5) moves it
  to the bar at the heard position (the karaoke clock: output timestamp minus output latency, loop
  wrap). It auto-scrolls the page container when the current bar moves to another system. It
  pauses auto-scroll for 4 s after the user scrolls by hand, and never scrolls under reduced motion
  unless the bar leaves the viewport. Bar lookup: forward cursor, binary search after a seek or
  loop wrap.
- **012 speed**: bar times are track seconds and the engine's heard position is in track seconds
  at every speed (assumption A-012-1), so the highlight follows slowed-down playback without
  recomputation. Transpose does not change the notation (a transposed part is feature 016); the
  panel says so while transpose is not 0.

Rejected: inline SVG with a sanitiser plus `getBBox()` in the browser (an injection surface, and
layout reads); Verovio's `renderToTimemap` (uses the MusicXML's constant tempo, not our beat grid,
so it drifts on live recordings); drawing in Blazor at 60 fps over interop (ADR-005: no
high-frequency interop).

### D4 Setlist PDFs are rendered in the worker with ReportLab

A setlist is backend data (`Setlists`, `SetlistEntries`); its PDF is rendered by a worker job
`setlist-document` with **ReportLab 5.1.x (BSD-3-Clause)** Platypus flowables and the DejaVu Sans
font (`fonts-dejavu-core`, Bitstream Vera / public-domain-style permissive licence) installed in
the worker image. The API sends the content (titles, artist, BPM, key if known, duration, mix
name, notes, sections, lyrics text), so the worker reads no database and no files. The API waits
for the short job (at most 30 s) and streams the PDF as an attachment, so the UI and the agent get
the file from one GET request. 016 later adds chord charts as a new optional content block
(`includeChords`), without changing this design.

Rejected: PDFsharp/MigraDoc (6.5.0 on NuGet is AGPL-3.0-only; older MIT 6.2.x no longer
maintained); QuestPDF (not an OSI licence); browser printing (not usable by the 011 agent,
results differ per browser, harder to test); hand-made SVG text layout through the 008
Cairo route (no line wrapping, no pagination).

### D5 Shortcuts: one configurable key map in web-interop, stored with the browser preferences

- The 004 key map (`web-interop` `registerShortcuts`, design-spec 6) becomes **data**: a default
  key map per scope plus the user's overrides, passed to `registerShortcuts(dotnet, scope,
  keymap)`. `web-interop` contract 1.1.0 (additive; the 004 call without a key map keeps the 004
  defaults). Space moves from `mixer-keys.js` into the key map with the same guard (native
  controls keep Space and Enter).
- Bindings are **chords** `{key, mod, shift}` on `KeyboardEvent.key` lower-cased (the 004
  convention, so labels match the keyboard layout). Alt/Option and the OS key are never used
  (they produce characters on macOS and are screen-reader modifiers on Windows). Up to two chords
  per command.
- Validation rules, enforced in C# (domain) and again in TS: one command per chord across all
  active scopes; **reserved chords** cannot be bound: Tab, Shift+Tab, Enter, Escape, F5-F7, F11,
  F12, `Mod` + W/T/N/R/L/Q/P/F/0/+/-/=, Insert and CapsLock (screen-reader modifier keys),
  the context-menu key; the browser's own zoom and reload stay intact.
- **Accessibility**: shortcuts never fire while focus is in a text field, select, slider
  (`input[type=range]`, `[role=slider]`), list box, grid, menu or dialog; Space and Enter on a
  focused button or link keep their native meaning. A setting "Single-key shortcuts" (on by
  default) switches off every shortcut without `Mod` (WCAG 2.2 SC 2.1.4 "turn off or remap").
  Screen readers in browse mode keep their own single-key navigation (they consume the keys
  before the page). Every command has a visible control, and the overlay lists the current
  bindings.
- **Foot pedals**: USB/Bluetooth page turners are keyboards (they send PageDown/PageUp, arrows,
  Space or Enter). A "Pedals" section offers presets for a left/right pedal pair ("Play / Back to
  section start" (default), "Next / Previous section", "Next / Previous song", "Faster / Slower")
  and a "Learn" mode that captures the key the pedal sends. Pedal chords are normal bindings
  in a `pedal` layer that, while **pedal mode** is on, also fire while focus is on a non-text
  control (pedals cannot move focus); text fields still win.
- **One catalogue for all features**: 014 owns the complete shortcut catalogue
  (`keymap-defaults.json`, with the owning feature per command). 012 (speed `-` `=`, transpose
  `,` `.`, reset `0`, trainer `T`, count-in) and 015 (record `R`, compare `C`, reserved until 015
  ships) register handlers only (CTO decisions 2026-11-16). Count-in vs. the shipped 004 metronome
  `K`: decided by the CTO (2026-11-16) - the metronome keeps `K`, count-in is `Shift+K` (DELTA R-2).
- Storage: the browser preferences record of 004 (`localStorage`, schemaVersion 2 adds
  `shortcuts {singleKey, pedalMode, overrides}`), like theme and density: a per-browser,
  per-device setting. Backup of a key map is not needed (defaults are one click away).

### D6 The practice log is backend data recorded by the mixer, local only

- The frontend `PracticeRecorder` measures **wall-clock playing time** on the mixer page (state
  `playing`, via `TimeProvider`; count-in included, paused time excluded), split per section by the
  position callbacks (<= 10 Hz) and per loop pass (engine `OnLoopPass`, D7), and records the
  highest **speed of a completed loop pass** per section while the speed trainer is on ("tempo
  reached"). It upserts one session record every 30 s while playing and when playback stops or
  the page is left (`PUT /api/practice/sessions/{sessionId}`, idempotent, client-made UUID).
  A session ends after 10 minutes without playback.
- The data stays in the local SQLite database (and therefore in 013 backups); there is no
  endpoint that sends it anywhere, no telemetry, no third-party chart service (Radzen charts are
  rendered locally). The log is **on by default** with a one-line notice on first use and can be
  switched off (the API then rejects writes with 409 `practice-log-off`, so a stale tab cannot
  record) and deleted per track or completely. Deleting a track deletes its practice records.
- Days are bucketed in the user's local time: the UI passes `utcOffsetMinutes` (the API runs in
  UTC in Docker).

### D7 Engine 2.4.0 adds the score follower and a loop-pass callback

Additive only: `attachScoreFollower`, `setScoreFollowOptions`, `detachScoreFollower` and the
`ScoreView*` types; one new .NET callback `OnLoopPass(LoopPassInfo)` fired once per completed pass
of an enabled loop (start, end, pass index, speed percent of that pass) at the moment the wrap is
heard. If 012's 2.3.0 already provides an equivalent loop-pass callback, Phase 0 part 2 keeps 012's
and drops this one (reconciliation item R-3 in DELTA.md). Every 2.3.0 call behaves as before.

### D8 New worker lane `practice` for short jobs

Section analysis (about 1-5 s per song), score views (< 1 s) and setlist PDFs (< 2 s) run on a new
fourth lane `practice` (one job at a time, FIFO), so they never wait behind a CPU piano
transcription (`transcribe` lane, minutes) or delay exports (`render` lane, 15 s target) or
separations. `Health.lanes.practice` reports it.

## Consequences

- Contracts (all additive, minor bumps): worker 1.8.0 (section, score-view and
  setlist-document jobs; `Health.lanes.practice`; library allow-list + `assets/<id>/score.musicxml`),
  backend 1.13.0 (sections, score views, setlists, practice log, `LoopRegion.sectionId`),
  engine 2.4.0, ui-test-ids 1.13.0, web-interop 1.1.0 (an "other" contract, allocated to 014 in
  the roadmap by the CTO, 2026-11-16), help content unchanged (topics go into the existing areas).
- New dependencies: ReportLab 5.1.x (BSD-3) in the worker; `fonts-dejavu-core` (Debian package,
  Bitstream Vera/DejaVu licence) in the worker image. No new .NET or npm packages.
- Every instrument family benefits: sections and loops, follow, setlists, pedals and the log are
  instrument-neutral; solo detection names any non-drum instrument; score follow works for every
  008 profile (vocals, bass, guitar, keys, piano, drums); E2E cases cover one score per family.
- Risks: label quality on real songs (mitigated by editing, honesty text and the port); Leipzig in
  `<img>` SVG (Phase 0 verifies the `data:` font in Chromium and Firefox; fallback: drop the text
  tempo glyph from the view and show the tempo in the panel header); 012 contract drift
  (reconciliation list in DELTA.md).

## Phase 0 part 2 amendments (2026-11-17)

- **D1 constants (spike T010, research.md R-10)**: checkerboard half size **6 bars** (was 4), novelty
  peaks with `delta = 0`, `wait = 3`; vocal activity `RMS > 0.1 x p95`, active when > 30 % of a bar,
  switch held >= 2 bars; merge < 4 bars (first/last < 2); clustering threshold **0.25**; the solo rule
  compares the source's **share** of the non-drum energy with its own median share over the song
  (>= 0.4 and >= 1.3 x median share) instead of its absolute RMS (a guitar or bass that plays all song
  long is only slightly louder in its solo). On the 6-form synthetic set: boundary recall 0.97,
  precision 0.97 within one bar, solo/bridge/break kind right 6/6, labels 41/42, 1.9 s for a 5-minute
  song.
- **D3 font (check T011, research.md R-11)**: Verovio 6.4.0 already writes the full Leipzig WOFF2 as an
  `@font-face` `<style>` into every SVG page (58.6 KB per page); as an `<img>` the tempo glyph renders in
  Chromium 159 and Firefox 159. The worker keeps that style and checks it is present (it does not add a
  second copy); the fallback (strip the Leipzig text) stays unused.
- **D5 key map (reconciliation R-1/R-2)**: the catalogue `wwwroot/js/keymap-defaults.json` uses 012's
  shipped command ids for the practice tempo (`mixer.speed:-5`, `mixer.speed:5`, `mixer.transpose:-1`,
  `mixer.transpose:1`, `mixer.practice.reset`, `mixer.countin.toggle`, `mixer.trainer.toggle`); 012's
  keys `r` (reset) and `c` (count-in) move to `0` and `Shift+K` (CTO decision 2026-11-16) and `r`/`c`
  are held for 015. 022's `/` (scope `library`, web-interop 1.2.0) and 023's lyrics editor keys
  (Ctrl+Alt+C, F8, Shift+F8, Alt+Enter, Alt+Backspace, Alt+I) are in the catalogue as owned or external
  chords, so no default or user binding can take them. The "G then X" sequences stay fixed.
- **D6 naming**: the practice log switch is `PracticeLogSettings` (operation ids
  `getPracticeLogSettings`/`updatePracticeLogSettings`, table `PracticeLogSettings`) because 012's
  backend 1.11.0 already defines `PracticeSettings` for the speed of a mix; the practice log panel's
  UI test ids are `practice-log-*` because 012's `practice-panel` is the speed and key panel.
- **D7**: 012's engine 2.3.0 has no loop-pass callback with region and speed (`OnPracticeChanged`
  reports only the trainer's pass), so `OnLoopPass` stays (R-3). The follower reads the transport's
  heard anchor exactly as the karaoke clock does (A-012-1 holds).
- **Library gate (013)**: no 014 operation deletes or replaces library files, so none carries the 013
  write gate; the two new job appliers skip their ticks during maintenance like every synchroniser.
  Full restore copies the new tables (rebuild from migrations copies every table). **Merge restore
  does not plan the new tables yet** (013's planner has fixed categories): open item for the CTO
  (hand-off `architect-014-phase0.md`).
- **Worker allow-list**: `assets/<uuid>/score.musicxml` joins `LIBRARY_PATH_PATTERNS`; section jobs
  additionally require the stem/detail patterns per field and score-view jobs the MusicXML pattern
  (named patterns in `ports.py`).
