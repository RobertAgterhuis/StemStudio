# Feature Specification: Sheet Music per Instrument

**Feature Branch**: `008-sheet-music`

**Created**: 2026-11-15

**Status**: Draft

**Input**: Owner request (Dutch, verbatim): "Ik wil per muziek instrument de mogelijkheid om
bladmuziek te kunnen genereren en downloaden (naar PDF)." ("Per musical instrument I want to be
able to generate sheet music and download it (as PDF).") Roadmap (`docs/ROADMAP.md`): transcribe a
stem to notation and download it as PDF (and MusicXML/MIDI); uses stems and the library of 007;
GPU optional.

## Clarifications

### Session 2026-11-15

Answered autonomously by the architect during planning (operating mode: fully autonomous), based
on the feasibility spike in [research.md](research.md); binding design in
`docs/decisions/ADR-017-sheet-music-transcription-and-engraving.md`.

- Q: What is an "instrument" here? → A: a separated stem or detail stem of a track (e.g. `bass`,
  `vocals.lead`, `guitar.electric-guitar`, `piano`, `drums`), or an audio item from the library.
  The user picks one; StemStudio suggests the matching instrument profile (vocals, bass, guitar,
  keys, piano, drums), which the user may change.
- Q: What quality is promised? → A: a **practice lead sheet**: correct bars, tempo, time
  signature and clef, with the notes the system heard, quantised to sixteenths (eighths for
  vocals and in "simplified" mode). Not a publishable score: ornaments, triplets, swing, dynamics,
  articulations, lyrics, chord symbols and tablature are not produced. The limits for the chosen
  profile are shown before and after generating.
- Q: Where do key, tempo and time signature come from? → A: tempo, time signature and bar lines
  from the track's beat grid (the same grid as the metronome); the key is estimated from the
  transcribed notes and shown with a confidence; the user can override the key. When the beat
  grid is unreliable, the user enters tempo, beats per bar and the first downbeat (the mixer's
  metronome override values are offered).
- Q: What files does the user get? → A: a PDF (A4) to view and print, MusicXML to open in
  notation programs (MuseScore, Dorico, ...) and MIDI. All three belong to one library item
  (kind "sheet music"), which can be previewed in the browser, downloaded, renamed and deleted
  like any library item.
- Q: Drums? → A: drum notation on a percussion staff (kick, snare, toms, hi-hat, cymbals) built
  from the drum detail stems of 002. If the drums of the track have not been split yet, the user
  is offered to split them first.
- Q: Piano? → A: two staves (grand staff) using a dedicated piano model. On a computer without
  a usable GPU this takes several minutes per song; the time estimate is shown and a faster,
  less accurate option is offered.
- Q: Part of a song? → A: yes (P3): a start/end time, or by choosing a library item that is a
  part of a song (e.g. an exported loop region).
- Q: UI 004? → A: a "Sheet music" action next to each stem (track page) and in the library,
  built with the 004 design system; every new area has an in-app help topic; profile and
  "simplified" are beginner options, key override, quality and manual tempo are advanced.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sheet music for a melodic instrument (Priority: P1)

A bass player opens a separated song, chooses "Sheet music" next to the bass stem and gets a
readable bass part in bass clef with the song's tempo and bars. They preview it in the browser
and download the PDF; MusicXML and MIDI are also available.

**Why this priority**: The literal owner request for the most common case (one melodic line);
proves the whole chain stem -> notes -> notation -> PDF.

**Independent Test**: For the reference song, generate sheet music for `bass` and for
`vocals.lead`; open the PDFs, check clef, tempo mark, time signature and that bar 1 starts at
the first downbeat; open the MusicXML in a notation program.

**Acceptance Scenarios**:

1. **Given** a completed track, **When** the user generates sheet music for `bass` with the
   suggested profile, **Then** within 2 minutes (CPU) a library item of kind "sheet music"
   becomes ready with a PDF, a MusicXML and a MIDI file, and the PDF preview opens in the
   browser.
2. **Given** the generated bass part, **When** the user looks at it, **Then** it is in bass
   clef (octave transposing, as bass parts are written), shows the song's tempo (rounded BPM) and
   time signature, and has as many bars as the beat grid has downbeats (plus a pickup bar when
   notes start before the first downbeat).
3. **Given** a profile, **When** the user opens the sheet music dialog, **Then** the quality
   limits of that profile are shown before generating, and the finished item shows them again
   with any warnings (e.g. "many very short notes were removed").

---

### User Story 2 - Key, tempo and time signature that fit the song (Priority: P1)

The notation uses the song's own grid: notes land in the right bar even if the band speeds up a
little; the key signature fits the notes; the user can correct the key or simplify the rhythm.

**Why this priority**: Without our grid and a sensible key, the output is unreadable (wrong bar
lines, accidentals everywhere); this is the main quality lever measured in the spike.

**Independent Test**: Generate a part, compare its bar lines with the downbeats of the beat grid,
then regenerate with a key override and with "simplified" and compare.

**Acceptance Scenarios**:

1. **Given** a track with a reliable beat grid, **When** sheet music is generated, **Then** every
   bar line corresponds to a downbeat of the grid and the time signature is
   "<beats per bar>/4".
2. **Given** the estimated key and its confidence shown in the item, **When** the user
   regenerates with another key, **Then** the key signature changes and accidentals are spelled
   for that key.
3. **Given** "simplified" rhythm, **When** generated, **Then** no note or rest shorter than an
   eighth appears.
4. **Given** a track whose beat grid is unreliable, **When** the user opens the dialog, **Then**
   they must enter tempo, beats per bar and the first downbeat (prefilled from the metronome
   override if set) before generating.

---

### User Story 3 - Piano on two staves (Priority: P2)

A pianist generates sheet music for the piano stem and gets a grand staff (treble and bass) with
chords, from a model made for piano.

**Why this priority**: Important for keyboard players, but slower and only useful for songs with
a real piano part.

**Independent Test**: Generate sheet music for a piano stem with "accurate" and with "fast";
check the grand staff and compare times with the estimates.

**Acceptance Scenarios**:

1. **Given** the piano stem, **When** the user opens the dialog, **Then** the piano profile is
   suggested with "accurate" preselected on a GPU worker and "fast" on a CPU-only worker, each
   with its estimated duration for this song.
2. **Given** "accurate", **When** the item is ready, **Then** the PDF shows a grand staff with
   right-hand notes (from middle C up) on the treble staff and left-hand notes on the bass staff.
3. **Given** a generation that took the CPU path, **When** it runs, **Then** progress is shown and
   the user can keep using StemStudio (exports and mixing are not blocked).

---

### User Story 4 - Drum notation (Priority: P2)

A drummer generates a drum part: kick, snare, toms, hi-hat and cymbals on a percussion staff.

**Why this priority**: Drummers are a key audience (002 detail stems were built for them), but
drum notation depends on the drum detail stems.

**Independent Test**: For the reference song (drums split), generate drum notation and check
that the snare mostly sits on beats 2 and 4 and the kick on the lower voice.

**Acceptance Scenarios**:

1. **Given** a track whose drums were split, **When** the user generates sheet music for `drums`,
   **Then** the PDF shows a five-line percussion staff with x noteheads for hi-hat and cymbals,
   hands (cymbals, hi-hat, snare, toms) stems up and kick stems down, rests where nothing is
   played.
2. **Given** a track whose drums were not split, **When** the user chooses sheet music for
   `drums`, **Then** they are told that the drums must be split first and can start the detail
   separation from there.

---

### User Story 5 - Sheet music for a part of the song or a library item (Priority: P3)

The user wants only the solo they practise: they pick a start/end time, or a library item that
is a part of the song (e.g. an exported loop region), and get sheet music for that part.

**Why this priority**: Convenience; the full song works without it.

**Independent Test**: Export a loop region (007) and generate sheet music from that library item;
check that bar numbering starts at the first full bar of the region.

**Acceptance Scenarios**:

1. **Given** an audio library item exported from a track with a range, **When** the user
   generates sheet music from it, **Then** the track's beat grid shifted to the range start is
   used and the item's provenance links both the audio item and the track.
2. **Given** an audio library item without a source track, **When** the user generates sheet
   music, **Then** they must enter the tempo manually.

### Edge Cases

- A stem that is almost silent (e.g. the piano stem of a song without piano): the item is
  created with "No notes were detected" and an empty-bar score is not produced; instead the item
  fails with that readable reason.
- Extremely dense input (e.g. distorted guitar chords): notes per bar are capped (32 per voice
  per bar for pitched profiles); excess notes are dropped with a warning.
- The worker restarts during transcription: the item fails with "interrupted by restart" and can
  be retried (recipe).
- The piano model is not downloaded yet: first use downloads it (172 MB) with progress "Loading
  the piano model"; without network the item fails with a readable reason and "fast" remains
  available.
- The track is deleted while the item is generated: as 007 (finishes or fails readably; the item
  stays in the library afterwards).
- Songs up to the maximum length (15 min) produce multi-page PDFs; long piano parts on the CPU
  can take about 25 minutes (estimate shown).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users MUST be able to generate sheet music for any standard stem or kept detail
  stem of a completed track, and for any audio item of the library.
- **FR-002**: The system MUST suggest an instrument profile per stem (vocals, bass, guitar, keys,
  piano, drums) and let the user choose another pitched profile; each profile defines clef(s),
  staves, pitch range and whether the line is treated as single-voice.
- **FR-003**: Each sheet music item MUST contain a PDF (A4), a MusicXML file and a MIDI file,
  stored as one library item that can be previewed (PDF in the browser), downloaded per file,
  renamed and deleted like other library items.
- **FR-004**: Tempo, time signature and bar lines MUST come from the track's beat grid (bar 1 at
  the first downbeat, a pickup bar for earlier notes); notes MUST be placed by the grid's beats so
  tempo drift does not shift bar lines.
- **FR-005**: The key MUST be estimated from the transcribed notes and shown with its confidence;
  users MUST be able to override it; accidentals MUST be spelled for the key in use.
- **FR-006**: Users MUST be able to choose the rhythmic resolution: sixteenths or "simplified"
  eighths; the default is sixteenths, except for the vocals profile (eighths, because sung
  melodies transcribe into many short fragments).
- **FR-007**: When the beat grid is unreliable or missing (e.g. a library item without a source
  track), users MUST provide tempo, beats per bar and the first downbeat before generating.
- **FR-008**: Piano MUST be notated on a grand staff using a piano-specific transcription; a
  faster general option MUST be offered, with time estimates for both based on the device in use.
- **FR-009**: Drums MUST be notated on a percussion staff from the drum detail stems (kick,
  snare, toms, hi-hat, cymbals) with two voices; when the drums are not split, the user MUST be
  offered to split them first.
- **FR-010**: The quality limits of the chosen profile MUST be shown before generating and on the
  finished item, together with item-specific warnings (unreliable grid used with manual tempo,
  low key confidence, notes removed by cleaning, dense input capped, source is a detail stem with
  possible bleed).
- **FR-011**: Generating sheet music MUST NOT block exports, mixing or the import of new songs;
  it MUST work on the CPU; on a GPU worker the piano model SHOULD use the GPU.
- **FR-012**: Each item MUST record how it was made (source, profile, quality, resolution, key
  override, tempo used, range) so it can be retried and described by later features and the
  agent; the same operations MUST be available through the HTTP API.
- **FR-013**: Users MUST be able to restrict sheet music to a start/end time of the source (P3).
- **FR-014**: Every new screen area MUST have an in-app help topic (004 framework), including what
  the profiles can and cannot do and the licence attribution of the piano model; advanced options
  (quality, key override, manual tempo, range) MUST declare their minimum journey level.

### Key Entities

- **Sheet music item** (library asset of kind "score"): files (PDF, MusicXML, MIDI), score facts
  (profile, engine, quality, key and its source and confidence, tempo, time signature, bars,
  notes, pages, resolution, warnings, attribution), provenance (source track/stem or source
  audio item, recipe).
- **Instrument profile**: name, suggested for which stems, clef(s)/staves, pitch range,
  single-voice or not, default resolution, engine(s), quality note.
- **Score recipe**: source, profile, quality (accurate/fast), resolution, key override, manual
  tempo, range.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a 5-minute song on the reference machine, sheet music for a melodic stem is
  ready within 2 minutes (measured: about 10-30 s transcription + about 1 s notation and PDF for
  4:22), and for drums within 2 minutes once the drums are split.
- **SC-002**: On the synthetic test material of the test suite, transcription plus quantisation
  reproduces at least 90 % of the notes correctly (onset within 50 ms and correct pitch;
  F-measure) for a single-voice line and at least 85 % for a polyphonic piano texture (measured
  in the spike: 1.00 and 0.96).
- **SC-003**: 100 % of generated items have bar lines at the beat grid's downbeats (checked on the
  MusicXML of the test suite), the grid's time signature and the rounded grid tempo.
- **SC-004**: 100 % of generated PDFs open in a standard PDF reader and contain only rendered
  music symbols (no missing-glyph boxes), checked by text/glyph inspection in the test suite.
- **SC-005**: While a sheet music job runs, a single-stem export (007) of the same song is still
  ready within its 15-second target.
- **SC-006**: A first-time user generates and downloads a bass part as PDF in under two minutes
  of interaction without help (moderated check by the CTO).

## Assumptions

- 007 is merged (asset library, lanes, read-only library mount, `AudioSource`); 002 detail stems
  and 003 device policy exist; 004's help framework and journey levels exist.
- The practice lead sheet quality is acceptable to the owner (research expectation: monophonic
  lines good to usable, piano good on clean piano stems, guitar/keys rough, drums usable for
  kick/snare/hi-hat grooves).
- The piano model's weights (CC BY 4.0) are downloaded at first use into the model cache.
- Copyright: transcriptions of copyrighted songs are for personal practice; the existing notice
  applies and the sheet music dialog repeats it in one line.
