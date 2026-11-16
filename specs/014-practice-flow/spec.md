# Feature Specification: Practice Flow

**Feature Branch**: `014-practice-flow`

**Created**: 2026-11-16

**Status**: Draft

**Input**: Owner backlog `docs/MoSCoW.md`, Should-have items S2, S5, S6, S7, S8, accepted on
2026-11-16 as roadmap feature 014 ("Practice flow: song sections with one-click loops, score
follows playback, setlists, keyboard and foot-pedal control, practice log"; depends on 008 and
012). Principle (owner, 2026-11-16): **every instrument counts equally**. Everything in this
feature has to work for vocals, guitar, bass, keys/piano, drums and the other instruments the
detail models isolate.

- S2: song sections (intro / verse / chorus / bridge / solo / outro) detected automatically,
  editable, with one-click loops; sections feed the speed trainer (012) and the AI composer (011).
- S5: the 008 sheet music scrolls and highlights the current bar while the song plays; bar times
  from the beat grid; works with 012 speed changes.
- S6: setlists: ordered lists of tracks or mixes with notes per song, one-click next/previous,
  PDF export with lyrics (chords later, from 016).
- S7: complete keyboard shortcuts (play, loop, speed +/-, next section, mute/solo) and USB/
  Bluetooth page-turner foot pedals (which act as keyboards); configurable; a shortcut overview in
  help; no conflicts with screen readers.
- S8: practice log: time practised per song and section, tempo reached in the speed trainer, a
  simple progress chart; local only, nothing leaves the machine; can be switched off and deleted.

## Clarifications

### Session 2026-11-16

Answered autonomously by the architect (operating mode: fully autonomous, "make informed
decisions instead of asking"). The binding design is in
`docs/decisions/ADR-023-practice-flow.md`; measurements in [research.md](research.md).

- Q: Which section kinds exist? -> A: `intro`, `verse`, `pre-chorus`, `chorus`, `bridge`, `solo`,
  `instrumental`, `outro`, `other`. Automatic detection uses intro, verse, chorus, bridge, solo,
  instrumental and outro; `pre-chorus` and `other` are offered for manual edits. A solo names its
  instrument when one stands out ("Guitar solo", "Piano solo", "Bass solo", "Woodwinds solo" for a
  wind detail stem). Every section has a label the user can change ("Verse 2", "Riff").
- Q: How good is automatic detection? -> A: an honest first guess. Boundaries usually land on the
  right bar; the kind (verse vs chorus) is a rule-based guess from repetition, energy and whether
  the vocals stem is active. The UI says "Detected automatically - check and edit". On the
  synthetic test songs at least 80 % of the boundaries are found within one bar (SC-002); real songs
  are checked by the owner.
- Q: Are sections per mix or per song? -> A: per song (track). Every mix of the track uses the same
  sections. Edits are kept; re-detection asks before it replaces edited sections.
- Q: What does "one-click loop" do? -> A: clicking a section sets the loop region to the section
  (A at its start, B at its end) and switches looping on. With 012 count-in on, each pass gets its
  count-in. The 012 speed trainer runs on that region, so sections are trainer regions.
- Q: Which score follows the playback? -> A: any 008 sheet-music item made from this track (or from
  an export of it): the user picks one in the mixer's "Score" panel (e.g. bass part, drum part).
  The current bar is highlighted and the page scrolls with it, also at reduced speed. While
  transpose (012) is not 0, the panel says the notation is not transposed (transposed parts are
  016).
- Q: What is in a setlist? -> A: a name, a note, and an ordered list of songs; each entry is a track
  with an optional mix (the mix opens with the song) and a note ("Capo 2", "Start at the bridge").
  The PDF contains per song: title, artist, tempo, length, mix, note, sections and lyrics (when
  available); options switch lyrics, sections and notes off. Chord charts are added by 016.
- Q: Which keys? -> A: the 004 key map plus practice commands, all remappable (table in FR-020).
  Pedals: presets for two pedals and a "Learn" mode. Single-key shortcuts can be switched off as a
  whole (accessibility).
- Q: What counts as practice time? -> A: the time the song is playing in the mixer (wall-clock,
  so 1 minute at 50 % speed counts as 1 minute), including count-in, excluding pauses. It is split
  per section. "Tempo reached" is the highest speed at which a full loop pass of a section was
  played with the speed trainer on.
- Q: Is the practice log on by default? -> A: yes, because it never leaves the machine; the first
  time the mixer records, a one-line notice says so, with "Turn off". It can be switched off and
  deleted (per song or all) at any time.
- Q: Levels (004)? -> A: Beginner: section lane and one-click loops, default shortcuts and the
  overview, setlist play mode, score follow on/off, practice summary. Advanced: section editing,
  setlist editing and PDF options, shortcut remapping, pedals, per-section chart. Expert: section
  confidence and analyser warnings, re-detect, session list. The accessibility setting
  "Single-key shortcuts" is visible at every level.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Loop a song section with one click (Priority: P1)

A guitarist opens a song in the mixer and sees the song's sections as a lane above the waveform
area: Intro, Verse 1, Chorus 1, ... Guitar solo, Outro. They click "Guitar solo": the loop is set to
the solo and switched on; with the 012 speed trainer they practise it from 70 % up to 100 %. "Next
section" jumps to the next section start.

**Why this priority**: The quickest daily win for every instrument: no dragging of A-B markers;
it is also the base for the speed trainer, the log per section and the score follow.

**Independent Test**: Import the synthetic structured test song (fake stack: fixed sections),
open the mixer, click a section, check that the loop region equals the section and looping is on;
press N and check that the position jumps to the next section start.

**Acceptance Scenarios**:

1. **Given** a track whose separation has just completed, **When** the user opens the mixer,
   **Then** the section lane shows the detected sections (kind, label, bar range) within 30 s of
   the separation finishing (status "Detecting sections..." until then), each section starting
   on a downbeat when the beat grid is reliable.
2. **Given** the section lane, **When** the user clicks a section, **Then** the loop region is the
   section's start and end, looping is on, playback continues (or starts at the section start if
   it was outside) and the section is marked as looped.
3. **Given** a section loop and the 012 speed trainer, **When** the user opens the trainer,
   **Then** the looped section is the trainer's region and its label is shown in the trainer.
4. **Given** a track imported before 014 (no sections yet), **When** its mixer opens for the
   first time, **Then** detection starts automatically and the lane fills when it finishes.
5. **Given** a track whose beat grid is unreliable, **When** sections are detected, **Then** they
   are shown with the note "Tempo grid uncertain - boundaries may be off" and snap to beats
   (or 0.1 s without a grid).

---

### User Story 2 - Hands-free control with keys and foot pedals (Priority: P1)

A drummer has both hands on the sticks: they start and stop the song, restart the current
section and slow down with a USB foot pedal and the keyboard. A pianist uses a Bluetooth
page-turner pedal to go to the next section. A screen-reader user keeps using their screen reader
normally and can turn single-key shortcuts off.

**Why this priority**: Hands are on the instrument; without this the other practice features
need the mouse.

**Independent Test**: In the mixer, press each default shortcut and check the effect; in the
pedal setup, choose "Learn" for the right pedal, press PageDown (as the pedal sends it) and check
that PageDown now toggles play/pause; switch single-key shortcuts off and check that L no longer
toggles the loop while Ctrl+S still saves.

**Acceptance Scenarios**:

1. **Given** the mixer with focus not in a text field, slider or dialog, **When** the user presses
   a default shortcut of FR-020, **Then** the command runs exactly as its on-screen control would,
   and the same key typed in a text field types the character.
2. **Given** the shortcut overview (Ctrl+/ or from help), **When** it opens, **Then** it lists
   every command with its current keys, grouped (transport, loop and sections, speed, channels,
   setlist, score, navigation), including remapped keys and pedals.
3. **Given** the shortcut settings (Advanced), **When** the user assigns a key that is already used
   or reserved (e.g. Tab, Ctrl+W, Insert), **Then** the assignment is refused with the reason and
   the command that already uses it; "Reset to defaults" restores the defaults.
4. **Given** pedal mode on with the preset "Play / Back to section start", **When** the right pedal
   sends PageDown while focus is on a mute button, **Then** playback toggles; the left pedal
   (PageUp) jumps to the start of the current section.
5. **Given** "Single-key shortcuts" off, **When** the user presses L, M or 1, **Then** nothing
   happens; Ctrl/Cmd shortcuts and pedal bindings with a modifier still work, and pedal mode can
   still be used because pedal bindings are separate (and can be set to modifier chords).

---

### User Story 3 - Edit the sections (Priority: P2)

A singer finds that the automatic "Verse 2" is really a pre-chorus plus a verse. They split the
section at the current bar, change the kinds, rename one "Pre-chorus", and drag a boundary one bar
later. Sections stay as edited; "Detect again" asks first.

**Why this priority**: Automatic labels are a guess; editing makes them trustworthy, but the
lane already helps without it.

**Independent Test**: Split, merge, rename, re-kind and move a boundary; reload the page and check
the result; re-detect and confirm the overwrite prompt.

**Acceptance Scenarios**:

1. **Given** the section editor (Advanced), **When** the user splits a section at the playhead,
   **Then** two sections result, the boundary snapped to the nearest downbeat (beat or 0.1 s
   without a reliable grid), labels numbered again per kind unless the user named them.
2. **Given** two adjacent sections, **When** the user merges them, **Then** one section covers
   both and keeps the first one's kind, label and id.
3. **Given** a boundary, **When** the user moves it (drag or the bar field, also with the keyboard),
   **Then** neither section becomes shorter than 1 s and the change is saved when the editor is
   closed with "Done" (or discarded with "Cancel").
4. **Given** edited sections, **When** the user chooses "Detect again", **Then** they must confirm
   "Replace your edited sections?"; loops and log entries that pointed at removed sections keep
   their label but no longer jump.
5. **Given** two browser tabs, **When** both save edits, **Then** the second save is refused with
   "The sections were changed in another window" and reloads the newer version.

---

### User Story 4 - The score follows the playback (Priority: P2)

A bassist has generated a bass part (008). In the mixer they open the "Score" panel, choose the
bass part, and press play: the current bar is highlighted and the pages scroll with the music, also
in a loop and at 70 % speed. A drummer does the same with the drum part; a saxophonist with a part
made from a detail stem.

**Why this priority**: Links reading and listening for every instrument that reads notation;
depends on 008 items existing.

**Independent Test**: For the fake-stack track, create a score for one stem of each family
(vocals, bass, guitar, piano, drums), open it in the Score panel, seek to a known bar time, and
check that the highlighted bar number equals the bar of that time; repeat at 50 % speed.

**Acceptance Scenarios**:

1. **Given** a track with at least one sheet-music item, **When** the user opens the Score panel,
   **Then** the items of this track are listed (instrument, name, date) and the chosen one shows
   its pages in the browser within 3 s (first open of an older item: "Preparing the score view..."
   up to 10 s).
2. **Given** playback, **When** the position passes a bar line of the score, **Then** the
   highlight moves to that bar within one animation frame of the moment it is heard (same clock as
   the karaoke banner), and the page scrolls so the current system stays visible.
3. **Given** a loop over bars 17-24 at 60 % speed, **When** the loop wraps, **Then** the highlight
   jumps back to bar 17 when the wrap is heard, and bar changes keep matching the slowed audio.
4. **Given** the user scrolls the score by hand while it plays, **When** they stop scrolling,
   **Then** auto-scroll pauses for 4 s (or until "Follow" is pressed) and then resumes.
5. **Given** a score made from a part of the song (008 range) or from an exported loop (007),
   **When** it plays, **Then** the highlight starts at the score's first bar when the song reaches
   the range start and shows no bar outside the range.
6. **Given** a click on a bar in the score, **When** the user clicks, **Then** playback seeks to
   that bar's start (Advanced: Shift+click sets a loop over the clicked bars).

---

### User Story 5 - Setlists for rehearsals and gigs (Priority: P2)

A band member makes a setlist "Friday rehearsal" with six songs, each with the mix to use and a
note ("Capo 2", "Start at the bridge"). At rehearsal they open the setlist, the first song opens in
its mix, and "Next song" (button, key or pedal) opens the next. They print the setlist with lyrics
as a PDF.

**Why this priority**: Bands and teachers practise in sets; valuable but independent of the other
stories.

**Independent Test**: Create a setlist with three tracks (one with a mix), reorder by keyboard,
play through with "Next song", export the PDF and check its pages contain the titles, notes,
sections and lyrics in order.

**Acceptance Scenarios**:

1. **Given** the Setlists page, **When** the user creates a setlist and adds songs (from the track
   list or "Add to setlist" on a track or mix), **Then** the setlist shows them in order with
   tempo and length and a total duration.
2. **Given** a setlist, **When** the user reorders entries (drag, or move up/down buttons for
   keyboard users), edits a note or removes an entry, **Then** the change is saved.
3. **Given** setlist play mode, **When** the user presses "Next song", **Then** the next entry's
   mixer opens with its mix (or the track's last state when no mix is chosen), the setlist bar
   shows "3 of 6", the entry note is visible, and unsaved mixer changes are asked about first.
4. **Given** a setlist, **When** the user exports the PDF with lyrics, **Then** an A4 PDF downloads
   within 10 s for 20 songs: a cover list (order, title, artist, tempo, length, note) followed by
   one page or more per song with sections and lyrics; songs without lyrics say "No lyrics".
5. **Given** a track in a setlist is deleted, **When** the setlist is opened, **Then** the entry is
   gone and the setlist says "1 song was removed because its track was deleted" once.

---

### User Story 6 - Practice log and progress (Priority: P2)

A beginner on bass wants to see that they are getting better. The mixer's "Practice" panel shows
time practised today and this week for this song, per section, and the tempo reached in the speed
trainer per section over the last weeks. The Practice page lists all songs with total time.
Everything stays on the computer; they can switch it off and delete it.

**Why this priority**: Motivation and insight, especially for the 004 beginner journey; it
needs sections (US1) and the speed trainer (012) for its richer parts.

**Independent Test**: With a fake clock, play a song for 5 minutes with 2 minutes in one section
loop at 80 % then 90 % speed; check the panel totals, the per-section times, the tempo chart
points, then delete the song's log and check it is empty; switch the log off and check nothing is
recorded.

**Acceptance Scenarios**:

1. **Given** the log is on, **When** the user plays a song for 3 minutes in total (with pauses),
   **Then** the Practice panel shows 3 minutes (+-2 s) today, and the time is split over the
   sections in which it was played.
2. **Given** the speed trainer on a section loop, **When** passes complete at 70 %, 75 %, 80 %,
   **Then** the section's "tempo reached" today is 80 %, and the chart shows one point per day
   with the highest value.
3. **Given** the Practice page, **When** it opens, **Then** it shows per song total time, last
   practised and a 30-day bar chart of minutes per day, each chart with a "Show as table" view.
4. **Given** "Practice log" switched off, **When** the user plays, **Then** nothing is recorded,
   the panel says "Practice log is off", and existing records stay until deleted.
5. **Given** "Delete practice log" (for one song or all), **When** the user confirms, **Then** the
   records are removed from the database and the totals show 0.
6. **Given** the browser crashes while playing, **When** the user returns, **Then** at most the
   last 30 s of practice are lost.

### Edge Cases

- A song without vocals (instrumental; the vocals stem is nearly silent): kinds are based on
  repetition, energy and the leading instrument only, and the result carries the warning
  `no-vocals`; when nothing repeats it carries `few-repetitions` and the parts are named
  "Part A", "Part B", ... of kind `other` instead of guesses.
- Very short songs (< 30 s) or songs with no beat grid: at least one section covering the song;
  detection never fails just because there is no structure.
- Free tempo / rubato songs (unreliable grid): sections snap to beats or 0.1 s; score follow uses
  the score's own grid (manual tempo of 008), which may drift; the panel says so.
- More than 64 detected boundaries: the shortest sections are merged until 64 remain.
- Track re-separated (retry): sections are kept (structure does not depend on the stems' quality);
  "Detect again" is offered.
- A score item that was made from a library audio item without a source track: it cannot follow
  this track and is not listed in the panel.
- A score whose time base does not match the track (manual tempo far off): follow still works by
  the score's own times; the panel shows "Follows the tempo you entered for this score".
- Two shortcuts on one key after a 014 upgrade of stored 004 preferences: stored overrides are
  validated on load; conflicting ones are dropped with a notice.
- Pedals that send auto-repeat while held: repeats are ignored.
- A pedal that sends Space while a button has focus: in pedal mode the pedal binding wins only if
  the chord is in the pedal layer; otherwise the button's native action wins.
- Setlist with 100 entries (maximum) and long lyrics: PDF paginates; lyrics longer than 10,000
  characters are cut with "Lyrics shortened".
- Lyrics in scripts not covered by the PDF font (e.g. CJK): characters render as missing glyphs; the
  PDF notes "Some characters could not be shown" (documented limitation).
- Practice log while two tabs play the same song: each tab records its own session; totals add up
  (documented: play in one tab).
- Clock changes (DST, manual): sessions store UTC start/end; days are bucketed with the offset the
  UI sends.
- Practice log off while a session is open: the next upsert is refused (409), the recorder stops
  and drops its buffer.

## Requirements *(mandatory)*

### Functional Requirements

**Sections (S2)**

- **FR-001**: The system MUST detect song sections automatically for every completed track (new
  tracks after separation; existing tracks the first time their mixer opens), using the beat grid
  and the stems, and MUST NOT need a GPU or a network connection.
- **FR-002**: Each section MUST have a kind (intro, verse, pre-chorus, chorus, bridge, solo,
  instrumental, outro, other), a label (1-40 characters), a start and end time, an optional
  instrument (for solos) and, for detected sections, a confidence; sections are ordered and do not
  overlap.
- **FR-003**: Section boundaries MUST snap to downbeats when the grid is reliable, to beats when a
  grid exists but is unreliable, and to 0.1 s otherwise; the UI MUST say when the grid is
  unreliable.
- **FR-004**: Users MUST be able to loop a section with one click (sets the loop region to the
  section and enables looping) and jump to the next/previous section and to the current section's
  start.
- **FR-005**: The section looped MUST be usable as the 012 speed trainer region, and the loop MUST
  remember which section it came from (until A or B is moved by hand), stored with the mix.
- **FR-006**: Users (Advanced) MUST be able to split, merge, rename, re-kind, delete and add
  sections and move boundaries (pointer and keyboard), saved as a whole; edits MUST be kept until
  the user explicitly re-detects (with confirmation when edited); concurrent edits MUST be detected.
- **FR-007**: The section lane MUST show detection status (detecting, ready, failed with reason and
  "Try again") and MUST be usable with a keyboard and a screen reader (list of buttons with kind,
  label and bar range).
- **FR-008**: Sections MUST be available through the HTTP API (read, replace, detect) so the 011
  agent and later features can use them.

**Score follow (S5)**

- **FR-009**: The mixer MUST offer a Score panel listing the sheet-music items made from this
  track (directly or through an export of it), for every 008 profile including drums and detail
  stems.
- **FR-010**: The chosen score MUST be shown page by page in the browser and the current bar MUST
  be highlighted following the heard playback position, including loops, seeks, pauses and every
  012 speed; the page MUST scroll to keep the current bar visible.
- **FR-011**: Manual scrolling MUST pause auto-scroll for 4 s or until "Follow" is pressed; with
  reduced motion the page MUST jump instead of scroll smoothly.
- **FR-012**: Clicking a bar MUST seek to its start; Shift+click (Advanced) MUST set the loop over
  the bars from the current loop start bar or the clicked bar range.
- **FR-013**: While transpose (012) is not 0, the panel MUST state that the notation is shown in
  the original key.
- **FR-014**: The score view (pages and bar times) MUST be produced without user action: right
  after a new score item is ready, and on first open for older items.

**Setlists (S6)**

- **FR-015**: Users MUST be able to create, rename, annotate (note up to 2,000 characters), and
  delete setlists, and add, remove and reorder entries (up to 100), each entry being a track with an
  optional mix of that track and a note (up to 1,000 characters).
- **FR-016**: Reordering MUST be possible without a pointer (move up/down).
- **FR-017**: Setlist play mode MUST open each entry's mixer with its mix and offer next/previous
  (button, shortcut, pedal), showing position and the entry note; the mixer's unsaved-changes rule
  MUST apply.
- **FR-018**: Users MUST be able to export a setlist as an A4 PDF with an overview page and per song
  its facts, note, sections and lyrics (each part can be switched off); the export MUST be extensible
  with chord charts (016) without changing the setlist data.

**Keyboard and pedals (S7)**

- **FR-019**: Every practice command MUST have a visible control and a keyboard shortcut; shortcuts
  MUST be configurable per command (up to two key chords), with reset to defaults.
- **FR-020**: **Complete default key map.** 014 owns the whole shortcut catalogue for every
  feature (CTO decisions 2026-11-16); the column "Owner" names the feature that provides the
  command's handler. Keys are `KeyboardEvent.key` (letters shown upper-case, typed without Shift
  unless Shift is listed).

  | Command | Default | Scope | Owner |
  |---|---|---|---|
  | Play / pause | Space | mixer | 001 (moved into the key map by 014) |
  | Stop and return to start | Home | mixer | 004 |
  | Seek -5 s / +5 s (Shift: 1 s) | Left / Right | mixer | 004 |
  | Set loop A / B | `[` / `]` | mixer | 004 |
  | Loop on/off | L | mixer | 004 |
  | Metronome on/off | K | mixer | 004 |
  | Select channel 1-9 | 1..9 | mixer | 004 |
  | Mute / solo selected channel | M / S | mixer | 004 |
  | Save mix / save as | Ctrl+S / Ctrl+Shift+S | mixer | 004 |
  | Command palette | Ctrl+K | global | 004 |
  | Shortcut overview | Ctrl+/ | global | 004 (live bindings: 014) |
  | Help | ? or F1 | global | 004 |
  | Go to Import / Tracks / Mixes | G then I / T / M | global | 004 |
  | Speed -5 % / +5 % | `-` / `=` (also `+`) | mixer | 012 |
  | Transpose -1 / +1 semitone | `,` / `.` | mixer | 012 |
  | Reset speed and transpose | 0 | mixer | 012 |
  | Count-in on/off | Shift+K | mixer | 012 |
  | Speed trainer on/off | T | mixer | 012 |
  | Loop current section | Shift+L | mixer | 014 |
  | Next / previous section | N / Shift+N | mixer | 014 |
  | Back to start of current section | Backspace | mixer | 014 |
  | Score panel on/off; follow on/off | F; Shift+F | mixer | 014 |
  | Next / previous song (setlist play mode) | Ctrl+Right / Ctrl+Left (Cmd on macOS) | setlist | 014 |
  | Record / stop recording | R | mixer | 015 (reserved until 015 ships) |
  | Compare: my take / original / both | C | mixer | 015 (reserved until 015 ships) |

  Pedal default (pedal mode on): right pedal PageDown = play/pause, left pedal PageUp = back to start
  of current section.

  Clash check (architect, 2026-11-16; **decided by the CTO, 2026-11-16**): the metronome keeps
  **K** (shipped in 004: design-spec 6, the help topic `shortcuts`, E2E tests) and count-in gets
  **Shift+K** (same key family: count-in is a metronome pre-roll). No other clashes:
  `,` `.` `0` `-` `=` `T` `R` `C` `N` `F` `Backspace` are unused by 004; `T` after `G` is part of the
  global G-sequence and does not fire the trainer (the sequence consumes it).

  Commands owned by a later feature (015) are registered in 014's catalogue and default key map so
  that no other binding can take their keys; they are hidden in the overview and settings until
  that feature registers a handler, and remappable like every other command once shown.
- **FR-021**: Shortcuts MUST NOT fire while focus is in a text field, select, slider, list box,
  grid, menu or dialog; Space and Enter MUST keep their native action on focused buttons and links;
  Alt/Option and OS-key chords and reserved chords (Tab, Shift+Tab, Enter, Escape, F5-F7, F11, F12,
  browser Ctrl/Cmd combinations for tabs, windows, address bar, reload, print, find and zoom, Insert,
  CapsLock, the context-menu key) MUST NOT be bindable.
- **FR-022**: A setting "Single-key shortcuts" (default on, reachable at every level) MUST switch
  off every shortcut without Ctrl/Cmd (WCAG 2.2 SC 2.1.4); remapping MUST be offered as well.
- **FR-023**: A pedal setup MUST offer presets for two pedals, a "Learn" mode that captures the key a
  pedal sends, and a pedal mode in which pedal bindings also work while focus is on a non-text
  control; auto-repeat MUST be ignored.
- **FR-024**: The shortcut overview MUST list the current bindings (including pedals) and be
  reachable from the help panel at every level and with Ctrl+/.

**Practice log (S8)**

- **FR-025**: While the log is on, the system MUST record practice sessions per track: wall-clock
  playing time (including count-in, excluding pauses), time per section, loop passes per section and
  the highest speed of a completed loop pass per section while the speed trainer was on.
- **FR-026**: Practice data MUST be stored only in the local database; the system MUST NOT send it
  to any other host or service, and no part of it may be logged beyond the local application log
  level "debug".
- **FR-027**: Users MUST be able to see per track: today, last 7 days, total, per-section time, and a
  chart of minutes per day and of tempo reached per section over time; and on a Practice page the
  totals per track; every chart MUST have an accessible table view.
- **FR-028**: Users MUST be able to switch the log off and on and to delete it per track or
  completely; deleting a track MUST delete its practice records. While off, nothing is recorded.
- **FR-029**: At most 30 s of practice MAY be lost when the browser or the computer stops
  unexpectedly.

**Cross-cutting**

- **FR-030**: Every new screen area MUST have an in-app help topic (004 framework) and declare its
  minimum journey level; tours: a Beginner tour "Practise a section" and an Advanced tour
  "Hands-free practice".
- **FR-031**: Every new interactive element MUST have the `data-testid` of the 1.13.0 UI test ids,
  MUST be operable by keyboard, have an accessible name and meet the 004 contrast tokens; the
  score highlight and the section lane MUST NOT rely on colour alone (outline plus pattern/label).
- **FR-032**: Sections, setlists and the practice log MUST be available through the HTTP API for
  the 011 agent (tools are added in a later tool-catalogue minor).
- **FR-033**: Acceptance tests MUST cover at least one case per instrument family (vocals, guitar,
  bass, keys/piano, drums, other/detail) for section solos and score follow.

### Key Entities

- **Section set** (per track): status (none, queued, detecting, ready, failed), origin
  (detected or edited), analyser name and version, warnings, revision, the ordered sections.
- **Section**: id, kind, label, start, end, instrument (optional), confidence (detected only),
  origin of the section (detected or user).
- **Score view** (per sheet-music item): status, pages (count, size), bars (number, page, box,
  start and end in track seconds).
- **Setlist**: name, note, created/updated, ordered entries.
- **Setlist entry**: track, optional mix, note, position.
- **Practice settings**: log on/off, notice shown.
- **Practice session**: id (made by the browser), track, mix, start, end, playing seconds, loop
  passes, per-section records (section id or none, label and kind as recorded, seconds, passes,
  highest trainer speed reached).
- **Key map** (browser preferences): single-key shortcuts on/off, pedal mode on/off, overrides per
  command.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a 5-minute song, sections are shown at most 30 s after separation completes on
  the reference machine's CPU (measured analysis budget: <= 10 s).
- **SC-002**: On the synthetic structured test songs of the test suite (at least 6 forms, one per
  instrument family playing the solo), at least 80 % of the true boundaries are found within one bar
  and at least 75 % of found boundaries are true (within one bar), and the solo of each song is
  labelled `solo` with the right instrument in at least 5 of 6 songs.
- **SC-003**: Score follow: on the test suite's score views, the highlighted bar equals the bar of
  the heard position for 100 % of sampled positions at speeds 50 %, 75 %, 100 % and 120 % (tolerance
  one animation frame at a bar line).
- **SC-004**: The score follower and the section lane cost no more than 2 ms per animation frame
  on the reference machine (Performance trace, 5-page score, karaoke banner attached as well).
- **SC-005**: A one-click section loop takes one click or one key; a first-time user loops the
  chorus within 30 s without help (moderated check by the CTO).
- **SC-006**: Every command of FR-020 is reachable by keyboard; the axe-core scan of the mixer,
  setlist, practice and shortcut pages shows no serious or critical violations; with NVDA in
  browse mode no StemStudio shortcut intercepts NVDA's single-key navigation (manual check).
- **SC-007**: Practice time recorded differs from the real playing time by at most 2 s per 10
  minutes (fake-clock unit test) and at most 30 s are lost on a crash (FR-029).
- **SC-008**: A setlist PDF of 20 songs with lyrics is ready in under 10 s and opens in a standard
  PDF reader with every Latin-script character rendered.
- **SC-009**: No network request carries practice data to any host other than the local API
  (E2E check of the request log on the fake stack).

## Assumptions

- Feature 012 is merged before 014's implementation wave and provides (engine 2.3.0, backend
  1.11.0): speed 50-120 % and transpose +-12 semitones for the whole mix, a speed trainer that runs
  on the loop region, count-in, and an engine position that stays in track seconds at every speed.
  Where 012's names differ from those assumed in [contracts/DELTA.md](contracts/DELTA.md)
  ("Assumed 012 surface"), Phase 0 part 2 reconciles them without changing 014's behaviour.
- Feature 013 (backend 1.12.0, worker 1.7.0) is merged first; its backup includes the new tables
  automatically (one database file).
- 008 score items keep their MusicXML and recipe; 006 lyrics are available as plain lines.
- The reference machine and the fake stack are unchanged; sections and score views run on the CPU.
- English UI only (owner decision W7).
- Section detection is a practice aid, not musicology: wrong kinds are expected sometimes and are
  corrected by the user.
