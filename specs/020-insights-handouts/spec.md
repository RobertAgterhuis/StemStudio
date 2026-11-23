# Feature Specification: Insights and Handouts

**Feature Branch**: `020-insights-handouts`

**Created**: 2026-11-16

**Status**: Ready for the wave (Phase 0 done 2026-11-23; User Story 5 deferred: gate G2 pending
the owner's MUSDB18-HQ dataset)

**Input**: MoSCoW backlog (`docs/MoSCoW.md`) items **C7** "Stem quality report: a bleed and
artefact score per stem with a tip ("try the detail model", "use the GPU model")" and **C8**
"Teacher handouts: one-click PDF with lyrics, chords, sections, tempo and score per instrument for
a lesson". Roadmap (`docs/ROADMAP.md`): feature 020, depends on 014 (sections, setlists, practice
log) and 016 (chords, tablature, drum notation, transposed parts); contract allocation backend-api
1.19.0, worker-api 1.11.0, ui-test-ids 1.19.0. Principle: every instrument counts equally.

## Clarifications

### Session 2026-11-16

Answered autonomously by the architect (operating mode: fully autonomous). Binding design:
[ADR-029](../../docs/decisions/ADR-029-stem-quality-report-and-handouts.md); evidence and open
measurements: [research.md](research.md).

- Q: What can a quality report honestly measure without the original multitrack? → A: only
  **estimates**. There is no validated reference-free metric for music source separation. The
  report uses simple signal measures on the stems themselves (how much of a stem looks like a
  scaled copy of another stem = bleed; frame-to-frame "flicker" and dropouts beyond what the full
  mix shows = artefacts; is the instrument present at all), plus the checks 002 already computes
  for detail stems. Every rating says it is an estimate. A dimension is labelled "validated" only
  for the stem types where the Phase 0 validation (research.md R-04, gate G1) passed; everything
  else is labelled "rough indication".
- Q: What does the user see? → A: per stem and detail stem a rating **Good / Fair / Poor**, or
  **Probably not in this song**, or **Not rated** (too little audio), with at most two
  plain-language tips. Advanced users also see the bleed and artefact levels and the main bleed
  source ("mostly drums"); Expert users see the index values, reliability labels and the analyzer
  version.
- Q: The backlog tip "use the GPU model": is there a better model on the GPU? → A: **No.** Feature
  003 runs the very same models on the GPU and the CPU (agreement >= 52 dB SDR), so "re-run on the
  GPU" would give the same stems; that tip would be false and is not offered. Instead, 020 adds an
  optional **high-quality vocal separation** (a RoFormer vocal model, then Demucs on the
  instrumental), which is fast on the GPU and slow but possible on the CPU. Its tip reads
  "Re-separate with the high-quality vocal model". This part (User Story 5) ships only if the
  Phase 0 measurement shows a real gain (gate G2); otherwise it is dropped and the tip with it.
- Q: When is the report made? → A: automatically after a standard separation, after a detail
  separation and after a high-quality re-separation; for existing tracks on request ("Check stem
  quality"). The analysis runs in the worker and does not block exports, mixing or imports.
- Q: What is an instrument family for handouts? → A: seven handout families, each with an
  equivalent content set (every instrument counts equally): **Band overview** (lyrics with chords,
  form chart; no notation), **Vocals**, **Guitar**, **Bass**, **Keys**, **Drums** and **Melodic
  instruments** (strings, brass, woodwinds; in C, B-flat, E-flat or F).
- Q: What goes into a handout? → A: what already exists for the song: title, artist, artwork,
  tempo, time signature and key; the sections with bar numbers and a chord chart per section; the
  lyrics with chords above the words; the family's notation (008 score, 016 tablature, drum
  notation or transposed part); the user's practice notes for that family; for setlists the
  setlist notes. Missing ingredients never block a handout: they are left out and listed in the
  dialog and on the library item ("Not included: tablature, not generated yet").
- Q: One click? → A: a "Lesson handout" menu on the track page (and "Setlist handout" on a
  setlist) lists the seven families; one click creates the PDF with the defaults. Advanced users
  can open "Customise" to choose ingredients, notation items, transposition and paper size.
- Q: Where does the PDF go? → A: into the 007 library as an item of kind "handout" (preview,
  download, rename, delete like other items), made by the worker.
- Q: Style? → A: the same look as the app and the public manuals: the app's light-theme design
  tokens and the Archivo font, A4 (Letter optional), page header and footer, PDF bookmarks.
- Q: Licence and copyright? → A: every page footer says "For personal practice and teaching
  only"; the last page has the full notice (song and lyrics belong to their rights holders;
  transcriptions and chords are automatic and may contain errors; do not publish or sell) and the
  attributions of the models and data sources used.

### Session 2026-11-23 (Phase 0)

Decided by the architect in Phase 0 (DELTA.md "Phase 0 outcome"); open for the owner's review.

- Q: The build order changed (020 before 017-019). Does 020 need anything from them? -> A: No. The
  contracts are merged onto 016's documents and keep the allocated labels.
- Q: Gate G2 cannot be measured (no MUSDB18-HQ on the machine). -> A: User Story 5, FR-007 and
  SC-007 are **deferred**; their contract parts are not merged. They come back as a later contract
  minor when the owner provides the dataset and G2 passes.
- Q: Gate G1 cannot be run either. What do users see? -> A: every dimension is labelled "rough
  indication" and all levels (Low/Medium/High) are shown; a level is hidden as "unknown" only after
  a dimension was measured and failed G1.
- Q: A setlist entry whose track was deleted? -> A: 014 already removes such entries from the
  setlist (with its own notice), so a setlist handout lists only the remaining songs.
- Q: Chords over lyrics and transposition: own rules or 016's? -> A: 016's chord sheet rules and
  chord spelling are reused, so the handout and the app's chord sheet always agree.
- Q: 014 already makes a setlist PDF. One or two? -> A: Both stay with different jobs: 014's quick
  "print the set" PDF (not stored) and 020's lesson handout per family (stored in the library).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See how clean each stem is (Priority: P1)

After a song is separated, the user opens the track and sees for every stem (and detail stem) a
simple rating with a short reason, so they know which stems they can trust for practice.

**Why this priority**: The C7 core: explains why a stem sounds bad, for every instrument, at no
extra effort.

**Independent Test**: Separate a song on the fake stack; the quality panel shows a rating for all
six stems within the time target, each with text and icon, and the disclaimer.

**Acceptance Scenarios**:

1. **Given** a standard separation that just completed, **When** the user opens the track page,
   **Then** within 60 seconds every stem shows Good, Fair, Poor, Probably not in this song or Not
   rated, with a one-line reason, and the panel states that the ratings are estimates made without
   the original recording.
2. **Given** a completed detail separation, **When** its report is ready, **Then** every kept
   detail stem (and its `rest`) has its own rating, shown under its parent.
3. **Given** a stem whose instrument does not play in the song (e.g. piano in a guitar song),
   **When** the report is ready, **Then** that stem is rated "Probably not in this song" with the
   reason "what you hear is spill from other instruments".
4. **Given** a rating, **When** the user is on Beginner level, **Then** they see the rating word,
   its icon and the tip; on Advanced also the bleed and artefact levels and the main bleed source;
   on Expert also the index values, the reliability label per dimension and the analyzer version.

---

### User Story 2 - Get a tip that leads to an action (Priority: P1)

For a stem rated Fair or Poor the user gets a plain-language tip with a button that starts the
suggested action, or an honest statement that the instrument is hard to isolate.

**Why this priority**: A rating without advice frustrates; the tip turns the report into a next
step and is the second half of C7.

**Independent Test**: On the fake stack (deterministic ratings per stem), check that each tip
rule produces its text and that "Split further" starts the 002 detail separation.

**Acceptance Scenarios**:

1. **Given** the `other` stem rated Fair with mixed content and no detail split yet, **When** the
   user reads its tip, **Then** it says "Split further (detail separation) may pull apart the
   instruments mixed into this stem." with a "Split further" button that starts the detail
   separation.
2. **Given** a guitar stem rated Poor after the detail split, **When** no better action exists,
   **Then** the tip says "Guitar is hard to isolate with today's models; this is close to the
   best StemStudio can do."
3. **Given** a detail split where a child stem rates better than its parent, **When** the user
   reads the parent's tip, **Then** it says "Use the <child> detail stem instead." with a button
   that opens the mixer with that detail stem expanded.
4. **Given** a stem rated Probably not in this song, **When** the user reads the tip, **Then** it
   offers "Mute this stem" for the current mix.
5. **Given** any tip, **Then** at most two tips are shown per stem, ordered by usefulness
   (data-model 5.3).

---

### User Story 3 - One-click lesson handout per song and instrument family (Priority: P1)

A teacher (or a student) opens a song, picks "Lesson handout -> Bass" and gets a PDF with the
song's facts, the sections with bar numbers, the chord chart, the bass tablature and score and
their practice notes, in the library within seconds.

**Why this priority**: The C8 core; reuses the output of 006, 008, 014 and 016 in a form a
teacher can hand out.

**Independent Test**: On the fake stack with the per-family fixture song, create a handout for
each of the seven families; each becomes a ready library item whose PDF opens, has the expected
bookmarks and the licence footer on every page.

**Acceptance Scenarios**:

1. **Given** a completed track, **When** the user chooses "Lesson handout" and a family, **Then**
   a library item of kind "handout" is created and ready within 15 seconds (5-minute song, up to
   six notation pages, reference CPU) and its PDF preview opens in the browser.
2. **Given** a family, **Then** the handout contains the family's default ingredients that exist
   (data-model 6.2): e.g. Drums = facts, form chart with bars, drum notation, notes; Guitar =
   facts, form chart, chords over lyrics, chord chart, tablature, score, notes; every family gets
   facts, sections and notes.
3. **Given** missing ingredients (e.g. no tablature yet), **When** the handout is made, **Then**
   it is still made, the dialog/menu listed the missing parts beforehand with a link to create
   them, and the item shows "Not included: tablature (not generated yet)".
4. **Given** any handout page, **Then** its footer shows the song title, the family, "page n of
   N" and "For personal practice and teaching only", and the last page shows the full notice and
   the attributions.
5. **Given** the "Melodic instruments" family with transposition B-flat, **Then** the chord
   symbols are transposed up a major second, the notation is the 016 B-flat part when one exists,
   and the facts state "Part in B-flat (sounds a major second lower)".

---

### User Story 4 - Setlist handout (Priority: P2)

A band leader opens a setlist and creates one PDF for the whole set: a cover with the running
order, then one section per song for the chosen family.

**Why this priority**: Bands and teachers work in sets (014), but the per-song handout already
covers single lessons.

**Independent Test**: Create a setlist of three fixture songs, generate a "Band overview" setlist
handout; check the running-order page (titles, keys, tempos, durations, notes), one bookmark per
song and page numbers in the contents.

**Acceptance Scenarios**:

1. **Given** a setlist with songs, **When** the user picks "Setlist handout" and a family,
   **Then** one handout item is created with a cover (setlist name, notes), a running order with
   key, tempo, duration and the entry notes per song, and the song sections in setlist order,
   each starting on a new page.
2. **Given** a setlist of 20 songs, **Then** the handout is ready within 90 seconds (reference
   CPU) and progress is shown.
3. **Given** a setlist from which 014 removed an entry because its track was deleted, **When** the
   setlist handout is made, **Then** the running order and the song sections contain exactly the
   remaining entries, in order (Phase 0: 014 removes such entries, data-model 2.5).

---

### User Story 5 - High-quality vocal re-separation (Priority: P3, gated by G2; **deferred** in Phase 0: G2 pending the dataset)

For a song with poor vocals or vocal bleed in the other stems, the user re-separates the track
with the high-quality vocal model and sees the ratings before and after.

**Why this priority**: The only real "better model" action; useful for singers and for every
stem that suffers from vocal bleed, but expensive on the CPU and only worth it if measured.
**Ships only if Phase 0 gate G2 passes** (research.md R-06); otherwise the tasks of this story are
dropped and recorded in DELTA.md.

**Independent Test**: On the fake stack, accept the tip "Re-separate with the high-quality vocal
model"; the track re-separates, the report is recomputed and shows the previous rating next to
the new one.

**Acceptance Scenarios**:

1. **Given** a tip "Re-separate with the high-quality vocal model", **When** the user opens it,
   **Then** a confirmation shows the time estimate for the worker's device and the consequences
   (detail stems are removed and can be split again; mixes keep their settings; library items are
   not changed).
2. **Given** the user confirms, **When** the re-separation completes, **Then** the six stems are
   replaced, the track shows "Separated with the high-quality vocal model", the quality report is
   recomputed and each stem shows its previous rating ("was Fair").
3. **Given** a re-separation that fails or is cancelled, **Then** the original stems stay
   unchanged.

### Edge Cases

- Very short or almost silent stems (less than 10 s of active audio): "Not rated" with the reason.
- Kick and bass playing exactly together look like bleed: the coupled pair is analysed with the
  co-occurrence guard (data-model 4.3) and, if still ambiguous, the bleed level is capped at
  Medium with the reason "may be the two instruments playing together".
- Analyzer version changes after an update: old reports show "Made with an older version" and a
  "Check again" button; they are not recomputed automatically.
- The worker restarts during an analysis or handout job: the job fails with "interrupted by
  restart"; the report keeps its previous content and can be re-run; a handout item fails and can
  be retried with the same recipe.
- Lyrics with characters the handout font lacks: the fallback font is used; characters that no
  installed font has are listed as a warning on the item.
- Notation PDFs in A4 for a Letter handout: pages are scaled to fit, never cropped.
- Handout of a setlist with more than 50 songs: refused with "A setlist handout can hold up to 50
  songs".
- Track deleted while a handout is made: the job finishes from its snapshot; the item stays.
- Lyrics, chords and notation are someone else's work: the notice is on every page and cannot be
  turned off.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: After every completed standard separation, detail separation and high-quality
  re-separation the system MUST compute a quality report for every stem and kept detail stem of
  the track in the worker, without blocking exports, mixing, imports or separations.
- **FR-002**: Users MUST be able to request (and repeat) the quality report for any completed
  track; a report made with an older analyzer version MUST be marked as such.
- **FR-003**: Each rated stem MUST get one rating (Good, Fair, Poor, Probably not in this song,
  Not rated), a bleed estimate (level, share, main source) and an artefact estimate (level and
  kinds), computed only from StemStudio's own stems (no reference multitrack).
- **FR-004**: Every report MUST state that the ratings are estimates; each dimension MUST carry a
  reliability label ("validated" only for stem types that passed gate G1, otherwise "rough
  indication"; until G1 is run every label is "rough indication"); the report MUST NOT present
  indices as measured truth.
- **FR-005**: The system MUST derive at most two tips per stem from the catalogue in data-model
  5.3; a tip MUST only suggest an action that exists for this track and device, and actionable
  tips MUST offer a button that starts the action.
- **FR-006**: The system MUST NOT offer "re-run on the GPU" as a quality improvement, because the
  GPU runs the same models (003).
- **FR-007** *(gated, G2; deferred in Phase 0)*: Users MUST be able to re-separate a completed track with the
  high-quality vocal pipeline, see the time estimate and the consequences first, and keep the
  original stems if it fails or is cancelled; afterwards the report MUST show the previous rating
  per stem.
- **FR-008**: Users MUST be able to create a handout PDF per song for each of the seven families
  in one click with defaults, and (Advanced) choose ingredients, notation items, transposition
  (Melodic instruments: C, B-flat, E-flat, F) and paper size (A4, Letter).
- **FR-009**: Users MUST be able to create one handout PDF for a setlist (up to 50 songs) for a
  chosen family, with a cover, a running order and one section per song.
- **FR-010**: A handout MUST include the family's default ingredients that exist (data-model 6.2)
  and MUST NOT fail because an optional ingredient is missing; missing ingredients MUST be listed
  before generating and on the finished item.
- **FR-011**: Chords MUST appear above the lyric words when word timings exist, above the word
  nearest their share of the line when only line timings exist (016's chord sheet rule), and as a
  separate chord chart per section otherwise;
  chord symbols MUST use ASCII accidentals (`Bb`, `F#m`) and be transposed with the part.
- **FR-012**: Handouts MUST be rendered by the worker in the app's visual style (light-theme
  design tokens, Archivo font), with a header and footer on every page (title, family, page n of
  N, the short notice), PDF bookmarks per song and section, document title and language metadata,
  and real (selectable) text for everything except notation pages.
- **FR-013**: Every handout MUST carry the personal-use notice on every page and the full notice
  plus attributions (lyrics source, automatic transcription and chord detection, model licences
  that require attribution) on its last page; users MUST NOT be able to remove them.
- **FR-014**: Handouts MUST land in the 007 library as items of kind "handout" with a PDF file,
  preview, download, rename and delete, and a recipe so that "Make again" re-creates them with
  current data.
- **FR-015**: Users MUST be able to write practice notes per track and family (up to 4,000
  characters), shown in that family's handout.
- **FR-016**: Every new screen area MUST have an in-app help topic (004 framework) and declare its
  minimum journey level (contracts/help-topics.md); ratings MUST NOT rely on colour alone; all new
  controls MUST be keyboard operable and announced to screen readers.
- **FR-017**: The same operations MUST be available through the HTTP API (quality report,
  handouts, practice notes, re-separation) so the 011 agent can use them in a later
  tool-catalogue version.
- **FR-018**: The worker MUST NOT fetch any network resource while rendering a handout; it MUST
  read only the allow-listed library files named in the request.

### Key Entities

- **Quality report** (per track): status, analyzer version, measured-at, previous ratings, and per
  stem: rating, bleed (level, index, share, main source, reliability), artefacts (level, index,
  kinds, reliability), activity, tips.
- **Tip**: code, text, optional action (split detail, open detail stem, mute, re-separate).
- **Handout item** (library asset of kind `handout`): one PDF; facts (kind song/setlist, family,
  pages, paper, included and omitted ingredients, warnings); recipe (source track or setlist,
  family, ingredients, notation items, transposition, paper).
- **Practice note**: track, family, text, updated-at.
- **Stem set** (per track, US5): pipeline (`standard`, `vocals-hq`), models, separated-at;
  re-separation phase (status, progress, error).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a 5-minute song with six stems and up to twelve detail stems, the quality report
  is ready within 60 seconds of the separation finishing on the reference machine's CPU (target
  for the analysis alone: <= 30 s).
- **SC-002**: On the synthetic validation set (controlled bleed and artefacts, research.md R-04 V1)
  the bleed and artefact indices rank the injected amounts with Spearman rho >= 0.8 and the levels
  are correct for >= 80 % of the cases; on the real validation set (V2) the result per stem type
  is published in research.md and decides the "validated" labels (G1 targets rho >= 0.5 for bleed
  vs SIR and >= 0.4 for artefacts vs SAR).
- **SC-003**: 100 % of tips shown in the E2E fixtures name an action that is available for that
  track, and no tip ever suggests re-running on the GPU.
- **SC-004**: A song handout (5-minute song, up to six notation pages) is ready within 15 seconds
  and a 20-song setlist handout within 90 seconds on the reference CPU.
- **SC-005**: 100 % of generated handouts in the test suite open in a standard PDF reader, carry
  the notice on every page, have one bookmark per song section and contain no missing-glyph
  boxes for Latin text (checked by text extraction and font inspection).
- **SC-006**: For each of the seven families the E2E suite creates a handout from the fixture song
  and finds the family's default ingredients in its facts (at least one case per instrument
  family, MoSCoW principle).
- **SC-007** *(gated, G2; deferred in Phase 0)*: On the real validation set the high-quality vocal pipeline improves
  the median vocals SDR by >= 1.0 dB and the median SIR of the other stems by >= 0.5 dB.
- **SC-008**: A first-time user creates a bass handout and finds the bass stem's quality tip in
  under two minutes without help (moderated check by the CTO).

## Assumptions

- 014 is merged (backend 1.13.0, worker 1.8.0): track sections with kind, label and times;
  setlists with ordered entries and notes; practice log with tempo reached per section.
- 016 is merged (backend 1.15.0, worker 1.9.0): a chord track per track (time-stamped chord
  symbols and a key estimate); tablature, drum notation and transposed parts as score library
  items with a PDF file. The exact shapes are reconciled in Phase 0 (DELTA.md section 6).
- 006 lyrics (lines, optional word timings), 008 score items, 005 artwork, 007 library and lanes,
  002 detail metrics and 003 device policy exist as merged.
- Features 017-019 add nothing 020 needs. The owner changed the build order: 020 is built before
  017-019 and 021, so 020's contract minors are merged onto 016's documents and keep their
  allocated labels; 017-019 merge onto 020's later (DELTA.md "Phase 0 outcome").
- The owner accepts estimate-quality ratings with honest labels (constitution V).
- Handouts are for the user's own lessons and practice; StemStudio does not share or publish them.
