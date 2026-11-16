# Feature Specification: Instrument Views

**Feature Branch**: `016-instrument-views`

**Created**: 2026-11-16

**Status**: Draft

**Input**: Owner backlog `docs/MoSCoW.md` (S1, S4, S9, S10 and the "other instruments" line of the
suggested order), roadmap item 016 (`docs/ROADMAP.md`): "Instrument views: chords
(keys/guitar/vocals), tablature (guitar/bass), drum trainer, singing feedback, transposed parts
for B-flat/E-flat/F instruments". Owner principle of 2026-11-16: **every instrument counts
equally** - an instrument-specific view is never built alone; each family gets its own
equivalent in the same feature, and acceptance tests cover at least one case per family.
Scope addition by the CTO (owner decision 2026-11-16, after a comparison with Moises), harmony
part: **key detection** (MoSCoW S11: the song key and key changes, shown next to the tempo,
following the 012 transpose, used for chord spelling) and **capo mode** (MoSCoW S12: capo fret and
chord shapes relative to the capo so the sounding key stays correct, with suggested easy capo
positions).

## Clarifications

### Session 2026-11-16

Answered autonomously by the architect (operating mode: fully autonomous); binding design in
ADR-031 (chord recognition), ADR-032 (singing pitch tracking) and ADR-033 (instrument views
architecture: analyses, timeline views, tablature and parts). Evidence in [research.md](research.md).

- Q: Which instrument families get a view? → A: five families, one view each, all P1:
  **harmony** (keys, guitar and singers who accompany themselves) - a synced chord chart;
  **guitar and bass** - tablature next to standard notation; **drums** - a drum trainer with hit
  lanes and a drumless mix; **vocals** - singing feedback with a live pitch line and per-line
  scores; **other instruments** (strings, brass, woodwinds) - a part in the written pitch and
  clef of the player's instrument, playable with score-follows-playback (014). A sixth story
  (P2) adds the shared entry point "Practise as ..." that opens the family's view and the
  matching minus-one mix for every family.
- Q: Where are chords shown? → A: above the lyrics in the 006 karaoke banner (live), above the
  lyrics in the lyrics card on the track page (static chord sheet), and in a "Chords" lane in the
  mixer that also covers instrumental parts. Chords follow the playback transpose of 012 and an
  optional capo for display. A ChordPro download and chord symbols in 008 lead sheets are P2/P3
  extras of the same story.
- Q: What chord vocabulary? → A: "Simple" (major and minor) by default, "Full" (7ths, 6ths,
  sus, dim, aug, half-diminished and slash bass) at Advanced. Chord changes are placed on beats of
  the track's beat grid.
- Q: Which tunings for tablature? → A: guitar (6 strings): standard, drop D, half step down,
  full step down, DADGAD, open G, open D, with a capo 0-12; bass: 4-string standard, 4-string
  drop D, 4-string half step down, 5-string (low B), 6-string (low B, high C). Layout "notation +
  tab" (default) or "tab only".
- Q: "MIDI download per instrument where missing"? → A: every channel (stem or detail stem,
  including single drum pieces and the strings/brass/woodwinds detail stems) gets a one-click
  "MIDI" action; it reuses a ready sheet music item of that channel or creates one, then downloads
  its MIDI. Drum MIDI uses the General MIDI percussion map. MIDI is always at sounding (concert)
  pitch.
- Q: What does the drum trainer contain? → A: hit lanes (cymbals, hi-hat, toms, snare, kick)
  scrolling with playback, built from the 002 drum detail stems; the 008 drum notation of the
  same song with score-follows-playback (014); and one click "Drumless" that silences every drum
  channel (also expanded drum detail strips), switches on the metronome with count-in (012) and
  opens the lanes. Judging the drummer's own playing is out of scope (backlog).
- Q: How does singing feedback get the voice? → A: through the microphone/audio-interface input
  and the latency calibration of 015. The pitch is tracked live in the browser; the lead vocal's
  reference pitch is analysed once by the worker. Scores are octave-independent (a singer an
  octave lower than the original scores the same). Scores are shown live and in a session
  summary; they are not stored (the 014 practice log may pick them up later). A recorded 015
  take can also be scored afterwards (P2).
- Q: Tolerance per level? → A: Beginner within 100 cents (a semitone), Advanced 50 cents, Expert
  25 cents; Advanced and Expert users may change it.
- Q: Which transposing instruments? → A: B-flat (trumpet/cornet, clarinet, soprano sax, tenor
  sax, bass clarinet), E-flat (alto sax, baritone sax, E-flat clarinet), F (horn, English horn)
  and concert-pitch instruments (flute, oboe, violin, viola, cello, double bass, trombone,
  bassoon, tuba). Clef choice: automatic, treble, bass, alto, tenor, treble 8vb, bass 8vb. New
  sheet music profiles `strings`, `brass` and `woodwinds` for the detail stems of 002.
- Q: GPU? → A: every view works on the CPU. The chord model may use the GPU through the 003
  device policy; it is small, so the CPU is the expected path. Live pitch tracking runs in the
  browser and never uses the worker.
- Q: Key detection (S11)? → A: the worker detects the global key and key changes (regions of at
  least 8 bars) from the same harmonic stems as the chords, in the same job. The key is shown next
  to the tempo on the track page and in the mixer (with its confidence at Advanced); with a 012
  transpose the resulting key is shown ("D major, original C major, +2"); during playback the
  mixer shows the key of the current region. Chord spelling uses the key of the region a chord is
  in (F# in D major, Gb in Db major). For a transposing instrument chosen in "Practise as"
  (strings/brass/woodwinds) the written key for that instrument is shown too. Key detection has
  its own status but no separate job.
- Q: Capo mode (S12)? → A: for the guitar family (6-string tunings; bass and keys get no capo).
  Capo mode shows a capo badge ("Capo 3") and every chord as the shape played with the capo
  (sounding key unchanged); at Advanced the sounding symbol is shown in small next to the shape.
  With a playback transpose of +1..+7 semitones it offers "Capo <n>: play the original shapes".
  StemStudio suggests up to three easy capo positions (0-7) ranked by how much of the song becomes
  open-position shapes; one click applies a suggestion. The capo of capo mode is the capo used by
  all chord views and prefilled in the tablature options (it replaces the plain display capo).
- Q: Levels (004)? → A: every view is available at every level; options are graded (data-model
  6). Every new area has an in-app help topic.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Harmony: a synced chord chart (Priority: P1)

A guitarist (or pianist, or a singer who accompanies themselves) opens a song in the mixer and
sees the chord symbols above the lyrics in the karaoke banner and in a "Chords" lane, moving with
the song. On the track page the lyrics card shows the same chords above the lyrics as a chord
sheet. If they practise with a capo or with the song transposed (012), the chords shown match
what they play.

**Why this priority**: Guitarists and keyboard players practise from chords more than from
notation (MoSCoW S1); singers who accompany themselves need the same chart.

**Independent Test**: On the fake stack, import a song; chords become ready automatically; the
chord lane's current chord changes on the expected beats; the banner shows chord symbols above
words; the lyrics card shows the chord sheet; with capo 2 and transpose +2 the symbols update.

**Acceptance Scenarios**:

1. **Given** a song whose standard separation has completed, **When** the user opens the mixer,
   **Then** chords are detected without a click (or already ready), with progress shown while
   running, and the Chords lane shows the current and the next chords, moving with playback.
2. **Given** ready chords and word- or line-synced lyrics, **When** the song plays, **Then** each
   chord symbol is shown above the word where it starts in the banner, and the chord starting at
   the current position is marked as current.
3. **Given** the track page, **When** the user opens the lyrics card, **Then** a static chord sheet
   shows the chords above the lyric lines (and chords of instrumental parts between lines).
4. **Given** a playback transpose of +2 semitones (012), or capo mode on fret 2 (US8), **When**
   chords are shown, **Then** every view (lane, banner, sheet) shows the transposed symbols
   (sounding pitch minus capo), spelled for the resulting key.
5. **Given** Advanced level, **When** the user switches the vocabulary to "Full" and re-detects,
   **Then** seventh, sixth, suspended, diminished, augmented and slash chords may appear.
6. **Given** an incorrect chord (Advanced), **When** the user edits it, **Then** the new symbol is
   kept for that segment in every view and a re-detection asks before overwriting edits.
7. **Given** ready chords (P2/P3), **When** the user downloads the chord sheet, **Then** they get a
   ChordPro text file; and **when** they generate sheet music for vocals, keys or guitar with
   "include chords", **Then** the chord symbols appear above the staff.

---

### User Story 2 - Guitar and bass: tablature next to notation (Priority: P1)

A guitarist generates sheet music for the guitar stem and gets standard notation with a TAB staff
underneath; they choose drop D or a capo. A bass player with a 5-string bass gets a 5-line tab.
Players without a score yet download the MIDI of their channel in one click.

**Why this priority**: Many guitar and bass players do not read notation (MoSCoW S4).

**Independent Test**: Generate guitar sheet music with "notation + tab", standard tuning, and
bass with 5-string tuning; check the MusicXML string/fret indications, the tuning and the PDF.

**Acceptance Scenarios**:

1. **Given** a guitar stem or a guitar detail stem, **When** the user opens the sheet music
   dialog, **Then** "notation + tab" is preselected with standard tuning, and tuning, capo and
   layout are offered.
2. **Given** a generated guitar part, **When** the user opens the PDF, **Then** a six-line TAB
   staff under the notation shows a fret number on the right string for every note, and every
   fret/string pair sounds the written pitch in the chosen tuning and capo.
3. **Given** drop D tuning, **When** the part contains a low D, **Then** it is played on the open
   sixth string (fret 0).
4. **Given** the bass stem and "5-string", **When** generated, **Then** the TAB staff has five
   lines and notes below low E use the low B string.
5. **Given** a note that cannot be played in the chosen tuning (too low or too high), **When**
   generated, **Then** it is moved by an octave in the tab and a warning names how many notes
   were moved.
6. **Given** any channel in the mixer or on the track page, **When** the user clicks "MIDI",
   **Then** the MIDI file of a ready sheet music item of that channel is downloaded, or such an
   item is generated first (progress shown) and then downloaded.

---

### User Story 3 - Drums: a drum trainer (Priority: P1)

A drummer clicks "Drumless": all drums go silent (also when the drum detail strips are
expanded), the metronome comes on with a count-in, and the drum trainer opens: five hit lanes
(cymbals, hi-hat, toms, snare, kick) scroll with the music and show what the original drummer
plays; the 008 drum notation follows playback next to it.

**Why this priority**: Drummers are a large group of stem-practice users (MoSCoW S10).

**Independent Test**: On the fake stack with drums split, open the drum trainer; the lanes show
the fake pattern (kick 1 and 3, snare 2 and 4, hi-hat eighths) at the right positions; "Drumless"
mutes every drum channel; the drum score follows the playback bar.

**Acceptance Scenarios**:

1. **Given** a track whose drums were split (002), **When** the user opens the drum trainer,
   **Then** drum hits are available (detected without a click after the split, or on first
   open), and five lanes show the upcoming hits with a "now" line, synced with playback.
2. **Given** drums not split, **When** the user opens the drum trainer, **Then** they are told
   the drums must be split first and can start the detail separation from there; "Drumless"
   works anyway (silences the drums stem).
3. **Given** the mixer, **When** the user clicks "Drumless", **Then** the drums channel and every
   drum detail channel are muted, the metronome is switched on with a one-bar count-in (012),
   other channels keep their settings, and one more click restores the previous drum settings.
4. **Given** the lanes, **When** the user clicks a lane label (Advanced), **Then** that piece is
   soloed as a guide on top of the drumless mix ("guide hi-hat"); the lane look-ahead can be set
   from 2 to 8 seconds.
5. **Given** a drum sheet music item of the same track (008), **When** the trainer is open,
   **Then** it is shown next to the lanes with score-follows-playback (014); without one, a
   "Generate drum notation" action is offered.

---

### User Story 4 - Vocals: singing feedback (Priority: P1)

A singer connects a microphone (calibrated in 015), turns on "Singing feedback" and sings along.
A pitch view shows the lead vocal's melody as bars and their own voice as a live line; after
every lyric line the banner shows a score for that line, and at the end a summary.

**Why this priority**: Vocals are one of the five families; builds on 006 and 015 (MoSCoW S9).

**Independent Test**: On the fake stack with Chromium's fake microphone fed with a fixture that
sings the fake reference melody, every scored line gets >= 90; with a fixture a semitone off,
lines score <= 20 at 50-cent tolerance.

**Acceptance Scenarios**:

1. **Given** a track with a vocals stem, **When** the user opens singing feedback the first time,
   **Then** the reference melody is analysed (progress and time estimate shown) from the lead
   vocal detail stem when vocals were split, else from the vocals stem.
2. **Given** a ready reference and an input device chosen in 015, **When** the user starts
   singing feedback and plays the song, **Then** the pitch view shows the reference melody and the
   singer's pitch line within one display frame of the analysis, aligned using the 015 latency.
3. **Given** a lyric line that ends, **When** it had enough reference melody (at least 0.2 s),
   **Then** a score 0-100 for that line appears in the karaoke banner next to the line and is
   announced to screen readers; lines without melody get no score.
4. **Given** the singer sings an octave lower than the original, **When** scored, **Then** the
   score equals singing at the original octave.
5. **Given** the song is transposed with 012, **When** scored, **Then** the reference follows the
   transpose.
6. **Given** no microphone permission or no input device, **When** the user starts feedback,
   **Then** a readable reason and a link to the 015 input settings are shown; playback continues.
7. **Given** a recorded take of 015 (P2), **When** the user chooses "Score this take", **Then**
   the same per-line scores and summary are computed from the recording.

---

### User Story 5 - Other instruments: a part for my instrument (Priority: P1)

A saxophonist, horn player or violinist generates sheet music for the brass, woodwinds or
strings detail stem (or any stem) and picks their instrument: an alto sax gets a part in E-flat,
a horn in F, a viola in alto clef. They play from it in the score view, which follows playback
(014).

**Why this priority**: Strings, brass and woodwinds players are the fifth family; without the
transposition the 008 score is unreadable for transposing instruments.

**Independent Test**: Generate a part for the fake brass stem as "Alto sax (E-flat)" and as
"Horn in F"; check the MusicXML transposition and key signature, the clef, and that the score
view highlights the same bars as a concert-pitch part.

**Acceptance Scenarios**:

1. **Given** a `strings`, `brass` or `woodwinds` detail stem, **When** the user opens the sheet
   music dialog, **Then** the matching new profile is suggested with an instrument picker
   (default: concert pitch).
2. **Given** "Alto sax (E-flat)", **When** generated, **Then** the part is written a major sixth
   above sounding pitch, the key signature is transposed accordingly (concert C major -> A
   major), the part name says "Alto Saxophone in E-flat", and the MusicXML declares the
   transposition so notation programs play it at concert pitch.
3. **Given** any profile, **When** the user picks a clef (Advanced), **Then** the part uses it
   (e.g. viola alto clef, trombone tenor clef).
4. **Given** notes outside the chosen instrument's playable range, **When** generated, **Then** a
   warning names how many notes are out of range (notes are kept).
5. **Given** a transposed part, **When** it is opened in the score view with playback, **Then**
   score-follows-playback highlights the current bar exactly as for the concert-pitch part.
6. **Given** a transposed part, **When** the user downloads MIDI, **Then** the MIDI is at
   concert pitch.

---

### User Story 6 - "Practise as ..." for every family (Priority: P2)

From the mixer the user chooses the instrument they practise ("Vocals", "Guitar", "Bass",
"Keys", "Drums", "Strings / brass / woodwinds"); StemStudio applies the minus-one mix for that
family (karaoke for vocals) and opens the family's view.

**Why this priority**: One equal entry point for every family; the views are useful without it.

**Independent Test**: For each family on the fake stack, choose it in the picker and check the
muted channels and the opened view.

**Acceptance Scenarios**:

1. **Given** the mixer, **When** the user chooses a family, **Then** every channel of that family
   (the stem and all its detail channels, whether expanded or not) is muted, other channels are
   unmuted and unsoloed, and the family's view opens: chords for keys, tab (or "Generate tab")
   for guitar and bass, drum trainer for drums, singing feedback for vocals, the part generator
   or score view for strings/brass/woodwinds.
2. **Given** the 004 practice presets (`play-along-<stem>`), **When** they are applied to a
   track with expanded detail strips, **Then** they also mute the detail channels of that
   family (same family rule as the picker).
3. **Given** the chosen family, **When** the user returns to the mixer later, **Then** the choice
   is remembered in the browser (not on the server) and offered again, not applied silently.

---

### User Story 7 - Harmony: the song key for every player (Priority: P1)

Every musician sees the song's key next to the tempo: a guitarist and a pianist know which scale
to improvise in, a singer sees which key a transpose leads to, a saxophonist sees the key for
their instrument. When the song changes key, the mixer shows the key of the current part.

**Why this priority**: Owner scope addition (MoSCoW S11); the key is the basis of correct chord
spelling and of choosing a transpose or capo.

**Independent Test**: On the fake stack the key "C major" appears next to the tempo after the
chords job; the fake key change (to D major from bar 9) is shown while playing from bar 9; with
transpose +2 the key reads "D major (original C major, +2)"; "Practise as" strings/brass/woodwinds
with an alto sax shows the written key "A major" for concert C major.

**Acceptance Scenarios**:

1. **Given** a song whose chord job has completed, **When** the user opens the track page or the
   mixer, **Then** the key (tonic and major/minor) is shown next to the tempo, marked "uncertain"
   when its confidence is low (and with the confidence in % and the relative alternative at
   Advanced).
2. **Given** a song with a key change, **When** the user plays past the change, **Then** the mixer
   shows the key of the current region, and the track page lists the key regions with their bars.
3. **Given** a 012 transpose of n semitones, **When** the key is shown, **Then** it shows the
   resulting key, the original key and the interval.
4. **Given** chords in a region in D-flat major, **When** they are displayed, **Then** they are
   spelled with flats (Gb, not F#); in a region in D major with sharps.
5. **Given** "Practise as" strings/brass/woodwinds with a transposing instrument chosen (e.g. alto
   sax in E-flat), **When** the key is shown, **Then** the written key for that instrument is shown
   too ("Concert C major - your part: A major").
6. **Given** vocals, bass, drums, keys or guitar as the chosen family, **When** the mixer is open,
   **Then** the key is shown the same way (no family lacks it).

---

### User Story 8 - Guitar: capo mode with suggested positions (Priority: P1)

A guitarist turns on capo mode: StemStudio suggests "Capo 3 - C shapes, 92 % open chords", they
pick it, and the chord lane, banner and chord sheet show the shapes to play (C, G, Am, F instead of
Eb, Bb, Cm, Ab) with a "Capo 3" badge, while the song sounds unchanged. After transposing the song
up two semitones they choose "Capo 2: play the original shapes".

**Why this priority**: Owner scope addition (MoSCoW S12); without it, songs in keys that are
awkward on the guitar are hard to play from the chord chart.

**Independent Test**: On the fake stack, transpose the fake song (C major) by +3 so it sounds in
E-flat major; capo mode offers "Capo 3: play the original shapes" and suggestions ranked by the
capo rule (data-model 2.9); choosing capo 3 shows C, G, Am, F in the lane and the sheet with the
badge "Capo 3"; guitar tablature opens with capo 3 prefilled.

**Acceptance Scenarios**:

1. **Given** ready chords and the guitar family (or capo mode switched on in the chord view),
   **When** the user opens capo mode, **Then** up to three suggested capo positions (0-7) are
   shown, each with the key of the shapes and the share of open-position shapes, best first.
2. **Given** a chosen capo, **When** chords are shown in the lane, the banner and the chord sheet,
   **Then** they show the shapes relative to the capo, a "Capo <n>" badge is visible, and at
   Advanced the sounding chord is shown next to each shape.
3. **Given** a 012 transpose of +1..+7 semitones, **When** capo mode is on, **Then** "Capo
   <transpose>: play the original shapes" is offered first; for -5..-1 the capo 12 + transpose
   (when <= 7) is offered.
4. **Given** a chosen capo, **When** the user generates guitar tablature, **Then** the capo is
   prefilled and the tab frets are relative to it.
5. **Given** the bass or keys family, **When** chord views are shown, **Then** capo mode is not
   offered, no capo badge appears and chords show sounding pitch.

### Edge Cases

- Ambiguous key (atonal, modal or a drum-only track): the key is shown as "uncertain" (confidence
  below 0.5) or "no key" when there are no chords; chord spelling falls back to the global key,
  then to sharps.
- Relative major/minor (C major vs A minor): the better fit is shown; at Advanced the alternative
  is named ("or A minor").
- Key changes shorter than 8 bars are not reported (usually borrowed chords).
- Every capo position needs barre chords: the best position is still suggested, labelled "few
  open shapes".

- No chords found (e.g. a drum solo, or a silent harmonic mix): the chord chart is ready with no
  chords ("N" everywhere) and says "No chords were detected"; views stay empty.
- The chord model cannot be downloaded (offline at first use): the fast built-in method is used
  automatically, the chart says so ("Chords: fast method") and the user can re-detect later.
- The track is re-separated or its detail stems change: views based on the old stems are marked
  "out of date" and re-run (chords, drum hits automatically; reference melody on next open).
- Lyrics missing, plain or instrumental: chords still show in the lane and the sheet (without
  lyric lines); singing feedback scores phrases (014 sections when present, otherwise four-bar
  blocks of the beat grid) instead of lyric lines.
- The 4-stem fallback model (no guitar/piano stems): chords use the stems that exist (bass and
  other).
- A beat grid that is unreliable: chord changes are not snapped to beats (shown with a note);
  tablature and parts need the manual tempo of 008 as before.
- Tablature chords with more notes than strings: the quietest extra notes are left out with a
  warning; chords spanning more than four frets are re-fingered or split with a warning.
- Singing feedback on a computer that cannot keep up (the pitch tracker misses its time budget):
  the view shows "Pitch tracking is too slow on this device" and stops itself, playback continues.
- Latency not calibrated in 015: feedback works with the browser's latency estimate and shows a
  hint to calibrate.
- Headphones not used: the microphone hears the song; the view recommends headphones; the lead
  vocal is muted automatically while feedback runs (karaoke), so bleed of the original singer
  cannot raise the score.
- The worker restarts during an analysis: it fails readably ("interrupted by restart") and can
  be re-run.
- The track is deleted: its analyses are deleted with it.

## Requirements *(mandatory)*

### Functional Requirements

General

- **FR-001**: Every instrument family (harmony, guitar, bass, drums, vocals, strings/brass/
  woodwinds) MUST have its own view in this feature, reachable from the mixer, and every view
  MUST work on a computer without a GPU.
- **FR-002**: Analyses that feed the views (chords, drum hits, reference melody) MUST run as
  background jobs per track with status, progress, readable errors and re-run; they MUST NOT
  block imports, separations, exports or sheet music generation, and MUST be marked out of date
  when their source stems change.
- **FR-003**: All views MUST follow the playback position of the mixer without drift, including
  loops (001), speed changes (012) and seeking, and MUST update without blocking the mixer's
  controls.

Harmony (chords)

- **FR-010**: The system MUST detect chords for a track from its harmonic stems (all stems except
  drums and vocals) after the standard separation, without a click, and on request.
- **FR-011**: Chord changes MUST be placed on beats of the track's beat grid when the grid is
  reliable; every chord lasts at least one beat.
- **FR-012**: Users MUST be able to choose the vocabulary "Simple" (major/minor) or "Full"
  (Advanced) and re-detect; re-detection MUST ask before overwriting edited chords.
- **FR-013**: The system MUST show chord symbols in a Chords lane in the mixer, above the words in
  the karaoke banner, and above the lyric lines in a chord sheet in the lyrics card.
- **FR-014**: Displayed chords MUST follow the playback transpose (012) and, for the guitar
  family, the capo of capo mode (0-12, FR-075), spelled for the resulting key (FR-073).
- **FR-015**: Users at Advanced MUST be able to correct a chord symbol of a segment.
- **FR-016** (P2): Users MUST be able to download the chord sheet as ChordPro text, with the
  transpose and capo in use.
- **FR-017** (P2): Sheet music for the vocals, guitar, keys and piano profiles MUST offer "include
  chords", which places the chord symbols above the top staff.

Guitar and bass (tablature)

- **FR-020**: Sheet music for the guitar and bass profiles MUST offer tablature with layout
  "notation + tab" (default for guitar and bass) or "tab only", a tuning from the catalogue
  (data-model 4.1) and, for guitar, a capo 0-12.
- **FR-021**: Every tab note MUST sound the notated pitch in the chosen tuning and capo; fret
  numbers MUST stay within 0..24 (relative to the capo); simultaneous notes MUST use different
  strings and span at most four fretted frets; fingering MUST prefer small hand movements.
- **FR-022**: Notes outside the tuning's range MUST be moved by octaves into range; chord notes
  beyond the string count MUST be dropped (quietest first); both MUST raise warnings with counts.
- **FR-023**: Every channel (stem, detail stem, single drum piece) MUST offer a one-click MIDI
  download that reuses or creates a sheet music item of that channel; drum MIDI MUST use the
  General MIDI percussion map; MIDI MUST be at sounding pitch.

Drums (drum trainer)

- **FR-030**: The system MUST detect drum hits per piece (kick, snare, toms, hi-hat, cymbals) from
  the 002 drum detail stems after the drum split, without a click, and on request.
- **FR-031**: The drum trainer MUST show five hit lanes with a "now" line and a look-ahead of 4 s
  (2-8 s at Advanced), synced with playback; hit marks MUST reflect the hit strength.
- **FR-032**: "Drumless" MUST, in one click, mute the drums channel and every drum detail channel
  (expanded or not), switch on the metronome with a one-bar count-in (012), and on a second click
  restore the previous drum and metronome settings.
- **FR-033**: Users at Advanced MUST be able to keep one drum piece audible as a guide on top of
  the drumless mix.
- **FR-034**: The drum trainer MUST show the 008 drum notation of the track with score-follows-
  playback (014) when a drum sheet music item exists, and offer to generate it otherwise.

Vocals (singing feedback)

- **FR-040**: The system MUST analyse the reference melody of a track once, from the lead vocal
  detail stem when vocals were split, otherwise from the vocals stem.
- **FR-041**: Singing feedback MUST track the singer's pitch live from the 015 input device,
  compensated by the 015 latency, and draw it against the reference melody.
- **FR-042**: The system MUST score every lyric line (or phrase, edge cases) with at least 0.2 s of
  reference melody from 0 to 100 as the share of reference frames sung within the tolerance,
  octave-independent and transpose-aware; the score MUST be shown in the banner and announced to
  screen readers; a summary MUST be shown at the end or on stop.
- **FR-043**: The tolerance MUST default per level (100/50/25 cents) and be adjustable at Advanced
  and Expert.
- **FR-044**: While singing feedback runs, the lead vocal MUST be muted by default (karaoke) and
  the view MUST recommend headphones.
- **FR-045**: Live pitch tracking MUST stop itself with a readable message when the device cannot
  keep up, without interrupting playback.
- **FR-046** (P2): Users MUST be able to score a recorded 015 take of the track with the same
  rules.
- **FR-047**: Microphone audio MUST stay in the browser; no singing audio or score is sent to the
  server by this feature.

Other instruments (parts)

- **FR-050**: Sheet music MUST offer the profiles `strings`, `brass` and `woodwinds` (single line,
  suggested for the matching detail stems) in addition to the 008 profiles.
- **FR-051**: Sheet music for every pitched profile MUST offer a written instrument from the
  catalogue (data-model 4.2) and write the part at that instrument's written pitch with a
  transposed key signature, the instrument's part name, and a transposition declaration in the
  MusicXML; MIDI stays at concert pitch.
- **FR-052**: Users at Advanced MUST be able to choose the clef (automatic, treble, bass, alto,
  tenor, treble 8vb, bass 8vb).
- **FR-053**: Notes outside the written instrument's range MUST raise a warning with a count.
- **FR-054**: Transposed parts MUST have the same bars as the concert-pitch part so that
  score-follows-playback (014) works unchanged.

Cross-cutting

- **FR-060** (P2): The mixer MUST offer "Practise as ..." for every family, applying the family's
  minus-one mix (every channel of the family muted) and opening its view; the 004 practice
  presets MUST mute whole families likewise.
Harmony: key and capo (scope addition S11, S12)

- **FR-070**: The system MUST detect the global key (tonic, major/minor, confidence, relative
  alternative) and key regions of at least 8 bars from the harmonic stems as part of chord
  detection, and store them with the chord chart.
- **FR-071**: The key MUST be shown next to the tempo on the track page and in the mixer for every
  family; the mixer MUST show the key of the current region during playback; the track page MUST
  list the regions with their start bars.
- **FR-072**: With a 012 transpose the shown key MUST be the resulting key together with the
  original key and the interval.
- **FR-073**: Chord spelling in every view and export MUST use the key of the region the chord
  starts in, falling back to the global key and then to sharps.
- **FR-074**: For a transposing written instrument chosen for the strings/brass/woodwinds family,
  the written key MUST be shown next to the concert key.
- **FR-075**: Capo mode MUST be offered for the guitar family only: a capo 0-12 shown as a badge,
  chord shapes relative to the capo in the lane, the banner and the chord sheet/ChordPro, and (at
  Advanced) the sounding symbol next to each shape.
- **FR-076**: The system MUST suggest up to three capo positions 0-7 for the song at the current
  transpose, ranked by the duration-weighted share of open-position shapes (lower frets first on
  ties), each with its shape key and open-shape share.
- **FR-077**: With a transpose of +1..+7 semitones capo mode MUST offer the capo that keeps the
  original song's shapes; for -5..-1 the capo 12 + transpose when it is <= 7.
- **FR-078**: The capo of capo mode MUST prefill the guitar tablature options (FR-020).

- **FR-061**: Every new screen area MUST have an in-app help topic (004), including the limits of
  each view and the licences/attribution of the models used; options MUST declare their minimum
  journey level (data-model 6).
- **FR-062**: All new views MUST be operable by keyboard, have text alternatives for visual-only
  content (chord sheet for the lane, scores as text for the pitch view, hit list per bar as text
  for the lanes) and respect "reduced motion".
- **FR-063**: The same operations MUST be available through the HTTP API (for the 011 agent and
  tests).
- **FR-064**: The quality limits of every analysis MUST be shown with its result (e.g. "chord
  detection is about 80 % accurate on pop songs; check by ear").

### Key Entities

- **Track analysis**: one per track and kind (`chords`, `drum-hits`, `vocal-pitch`): status,
  progress, engine, device, source fingerprint, result document, edits, errors.
- **Chord chart**: ordered chord segments (start, end, symbol, root, quality, bass, confidence,
  edited), vocabulary, estimated key, engine.
- **Drum hit map**: hits (time, piece, strength) of the five drum pieces.
- **Reference melody**: pitch frames every 10 ms (voiced or not), source stem, range.
- **Tablature settings**: tuning, strings, capo, layout; per item the counts of moved and dropped
  notes.
- **Part settings**: written instrument, transposition, clef; per item out-of-range count.
- **Key region**: start, end, tonic, mode, confidence; the global key with its relative
  alternative is part of the chord chart.
- **Capo suggestion** (computed): capo fret, shape key, open-shape share.
- **Singing session** (browser only): tolerance, latency used, per-line scores, summary.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On the synthetic chord test set of the test suite, at least 85 % of the time is
  labelled with the correct major/minor chord by the default method and 75 % by the fast
  fallback; for a 5-minute song chords are ready within 30 seconds of the separation completing
  on the reference machine's CPU.
- **SC-002**: 100 % of tab notes in the test suite's generated parts sound the notated pitch in
  their tuning and capo, use frets 0..24, and no chord spans more than four fretted frets; on the
  golden fingering fixtures at least 95 % of notes match the expected string.
- **SC-003**: Drum hits are ready within 30 seconds after the drum split for a 5-minute song; on
  generated click tracks the lane marks are within 20 ms of the true hit times; "Drumless" leaves
  0 audible drum channels in 100 % of the E2E checks (meters at -60 dB).
- **SC-004**: The live pitch tracker's median error is at most 20 cents on synthetic voices of
  80-1000 Hz with at least 95 % of voiced frames detected; the on-pitch E2E fixture scores >= 90
  on every scored line and the semitone-off fixture <= 20 at 50 cents; the tracker uses at most
  0.5 ms per audio block on average on the reference machine with no audible dropouts.
- **SC-005**: For every instrument of the catalogue, the generated parts in the test suite have
  the expected written pitches, key signature and transposition declaration (100 % of the
  vectors), and the same bar count and bar times as the concert-pitch part.
- **SC-006**: The E2E suite on the fake stack has at least one passing scenario per family
  (harmony, guitar, bass, drums, vocals, strings, brass, woodwinds) with zero retries.
- **SC-007**: With the chord lane, banner chords, hit lanes and pitch view all active, the mixer
  keeps drawing at the display rate (95th-percentile frame time <= 16.7 ms on the reference
  machine) and controls react within 100 ms.
- **SC-008**: Automated accessibility checks report 0 serious or critical violations on every new
  area; every new control is reachable and operable by keyboard.
- **SC-010**: On the synthetic key test set (cadences and progressions in all 24 keys rendered by
  the test suite) the global key is correct for at least 90 % of the pieces and correct or a
  fifth/relative key for 100 %; on the modulation fixtures the change is reported within two bars
  of the true change, and no region shorter than 8 bars is reported.
- **SC-011**: For 100 % of the capo vectors (every root x quality x capo 0-12) shape plus capo
  sounds the original chord; for the capo fixtures (progressions in E-flat, A-flat, B-flat and
  F-sharp major) the top suggestion is the expected position.
- **SC-009**: A first-time user of each family reaches their view from the mixer in at most three
  clicks without help (moderated check by the CTO).

## Assumptions

- 008 is merged (sheet music, profiles, drum onset detection, MusicXML/MIDI/PDF, Verovio); 002
  detail stems, 003 device policy, 004 help/levels/presets, 006 lyrics and banner, 007 library and
  012 (speed, transpose, count-in) exist.
- **014 Practice flow** delivers sections and **score-follows-playback** (backend 1.13.0, worker
  1.8.0, engine 2.4.0): a score view in the browser that highlights the current bar of any score
  library item while the song plays. 016 reuses it for drum notation, tablature and parts and
  adds nothing to it except passing the new score items.
- **015 Play-along recording** delivers input device selection, microphone permission handling,
  latency calibration (a round-trip value in ms) and recorded takes (backend 1.14.0, engine
  2.5.0). 016 reads the selected input and the calibrated latency through the engine and reads
  takes through their file URL.
- The names used here for 014/015 artefacts (score view, input, latency, takes) are placeholders
  reconciled with the merged 014/015 contracts in Phase 0 (tasks T001); the 016 contracts are
  additive on top of whatever they define.
- Chord detection, tablature fingering and pitch scoring are practice aids, not ground truth; the
  limits are shown (FR-064). Transcriptions and chord sheets of copyrighted songs are personal
  practice material (existing notice).
- Chord model weights are downloaded at first use into the model cache (ADR-009 rules; MIT).