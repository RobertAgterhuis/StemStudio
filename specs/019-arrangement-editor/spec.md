# Feature Specification: Arrangement Editor (practice versions)

**Feature Branch**: `019-arrangement-editor`

**Created**: 2026-11-16

**Status**: Draft (planning round; Phase 0 runs when 019 starts, on top of 012-018)

**Input**: `docs/MoSCoW.md` C6 (owner, 2026-11-16): "Remix and arrangement editor: cut, repeat
and rearrange sections of stems into a new practice version (e.g. 'chorus 4x'). Practice versions
and band arrangements. Non-destructive edit list per mix; export via 007." Roadmap
(`docs/ROADMAP.md`): feature 019, depends on 007 (export library) and 014 (song sections, score
follow); contracts backend-api 1.18.0, worker-api 1.10.0, audio-engine 2.8.0, ui-test-ids 1.18.0,
render-vectors 1.2.0 (CTO allocation change 2026-11-16: 1.2.0 contains 015's 1.1.0). MoSCoW
principle: every instrument counts equally.

## Clarifications

### Session 2026-11-16

Answered autonomously by the architect (operating mode: fully autonomous). Binding design:
`docs/decisions/ADR-028-arrangement-timeline-model.md`; alternatives in [research.md](research.md).

- Q: What is an arrangement? → A: a named, non-destructive **edit list** for one song: an ordered
  list of **regions** (parts of the song, usually whole bars), each played 1-16 times, each with
  its own channel mutes. The stems never change. "Song order" (no arrangement) remains the
  default.
- Q: Where does it live: in a mix or on its own? → A: **its own item per song**, with a name. A
  saved mix may point to one arrangement; opening that mix plays the arrangement. One
  arrangement can be used by several mixes (e.g. "verse-chorus drill" with a guitar mix and a
  bass mix). Deleting an arrangement leaves the mixes in song order.
- Q: How are cut points chosen? → A: on **bar lines** from the beat grid (001) and the song
  sections (014): the user picks bars or whole sections. Experts can switch to free timing
  (any time, 10 ms display precision) for songs without a usable grid.
- Q: How are clicks avoided? → A: every jump (cut, repeat, reorder) gets a short **equal-power
  crossfade** centred on the boundary (default 10 ms; Expert: 0-50 ms). Where the song continues
  without a jump nothing is changed (bit-identical), and switching a channel off or on at a
  boundary fades only that channel.
- Q: What do lyrics, score and metronome do? → A: they **follow what you hear**: in a repeated
  chorus the karaoke ball and the score cursor jump back to the chorus; section highlight shows
  the section being played; the metronome and count-in play the arranged beats, with beat 1
  accents intact on bar-snapped regions. Muting the vocals in a region keeps the lyrics on screen.
- Q: Export? → A: through the 007 library: "Save to library" in the mixer offers "Use
  arrangement <name>" (on by default when one is active); "what I hear", "selected channels" and
  raw single stems can all be arranged; a range is then a part of the arranged version. The file
  sounds exactly like the editor plays it (shared golden vectors).
- Q: Undo/redo? → A: yes, in the editor, 100 steps, for every edit; saving is explicit with an
  unsaved-changes guard.
- Q: Limits? → A: 64 regions, repeat 1-16 per region, 256 parts after repeats, 30 minutes
  arranged length, regions at least 0.5 s, 50 arrangements per song.
- Q: Which user level? → A: **Advanced** (the editor is hidden for Beginners; a Beginner who
  opens a mix with an arrangement hears it and sees a read-only "Arranged: <name>" chip with a
  "Play song order" switch). Crossfade length, free timing and seconds fields are **Expert**.
- Q: Recording (015) with an arrangement? → A: not in 019: the record button is disabled with
  the reason "Switch to song order to record" while an arrangement is active.
- Q: Keyboard? → A: the whole editor works with the keyboard alone (focus, move, repeat,
  duplicate, split, mute, delete, undo/redo, play from region), with screen-reader
  announcements; shortcuts act only while the timeline has focus.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Build a practice version from sections and play it gaplessly (Priority: P1)

A guitarist wants to drill the chorus. In the mixer they open **Arrange**, choose "New from
sections", delete the intro and outro, set the chorus to 4x and press play. The chorus repeats
four times without a gap or click, the position bar shows the arranged timeline, and the
metronome keeps time across the repeats.

**Why this priority**: The literal C6 example ("chorus 4x"); it creates the arrangement model,
the resolver, the engine timeline and the editor every other story uses.

**Independent Test**: Open the reference song (sections from 014), create from sections, remove
the first and last region, set the chorus to repeat 4, play across each boundary; verify the
arranged duration, the region highlight, gapless boundaries (engine timeline vectors) and the
metronome clicks.

**Acceptance Scenarios**:

1. **Given** a song with detected sections and a reliable beat grid, **When** the user chooses
   "New from sections", **Then** an unsaved arrangement appears with one region per section, in
   song order, each starting and ending on a bar line, labelled with the section name, and the
   arranged duration equals the song duration (+/- 1 ms).
2. **Given** a region "Chorus", **When** the user sets repeat to 4, **Then** the region shows
   "x4", the arranged duration grows by three chorus lengths, and playback plays the chorus four
   times in a row.
3. **Given** playback across any boundary, **When** audio passes the boundary, **Then** there is
   no gap and no click (the crossfade law of the shared vectors), all channels stay sample-locked
   to each other, and the region under the playhead is highlighted.
4. **Given** the metronome is on and regions are bar-snapped, **When** playback crosses a
   boundary, **Then** the clicks continue on the arranged beats and beat 1 keeps its accent.
5. **Given** a song without sections, **When** the user opens Arrange, **Then** "New from
   sections" is unavailable with the reason, and "New from whole song" creates one region over
   the song which the user can split at bars.

---

### User Story 2 - Cut, reorder and repeat by bars (Priority: P1)

A bassist wants "bars 17-24, then the bridge, then bars 17-24 again twice". They select bars on
the song strip and add regions, split a region at the playhead bar, move regions left and right,
duplicate, delete, and undo a mistake.

**Why this priority**: Cut and rearrange is the core of C6; sections alone are not always the
right cut.

**Independent Test**: Starting from "whole song", select bars 17-24 and add a region, split, move,
duplicate, delete, undo and redo; verify the region list and the resolved timeline after each
step.

**Acceptance Scenarios**:

1. **Given** the song strip, **When** the user selects bars 17 to 24 and chooses "Add region",
   **Then** a region "Bars 17-24" is appended whose source span is exactly bar line 17 to bar line
   25.
2. **Given** a region of bars 1-16 and the playhead at bar 9, **When** the user chooses "Split",
   **Then** two regions 1-8 and 9-16 replace it and playback of them is bit-identical to before
   (contiguous boundary, no fade).
3. **Given** regions A, B, C, **When** the user moves C to the front, **Then** the order is C, A,
   B, and the arranged timeline and boundaries update while playback continues.
4. **Given** any sequence of edits, **When** the user presses undo repeatedly, **Then** each edit
   is reverted in reverse order up to 100 steps, and redo re-applies them; a new edit after undo
   clears redo.
5. **Given** an edit that would exceed a limit (65th region, repeat 17, more than 256 parts,
   more than 30 minutes, region under 0.5 s), **When** the user tries it, **Then** the edit is
   refused with a readable message naming the limit and nothing changes.

---

### User Story 3 - Drop an instrument in a part (Priority: P1)

A singer wants verse 2 without the lead vocal and the last chorus without drums; a drummer wants
the bridge without drums; a pianist drops the piano in chorus 2. In the region inspector they
untick channels for that region only.

**Why this priority**: "Drop the guitar in verse 2" turns a rearranged song into a practice
version for each instrument; every instrument family must be served equally (MoSCoW principle).

**Independent Test**: In one arrangement mute `vocals` in region 2, `drums` in region 3,
`guitar` in region 4, `bass` in region 5, `piano` in region 6 and `drums.snare` (expanded drums)
in region 7; play and export; check that each channel is silent exactly in its region with fades
at the edges and audible elsewhere.

**Acceptance Scenarios**:

1. **Given** region "Verse 2", **When** the user mutes `guitar` there, **Then** the guitar is
   silent in that region only, fades out and back in at its edges without a click, and the
   region shows a "guitar muted" badge.
2. **Given** drums expanded into detail stems, **When** the user mutes `drums.snare` in a region,
   **Then** only the snare is silent there; **When** they mute `drums`, **Then** the whole drums
   group is silent there, expanded or not.
3. **Given** a muted channel in a region and the mixer's own mute/solo, **When** playing,
   **Then** a channel is audible only if the mixer lets it through **and** the region does not
   mute it.
4. **Given** `vocals` muted in a region, **When** that region plays, **Then** the karaoke banner
   still shows and follows the lyrics of the material being played.
5. **Given** a channel the song does not have (e.g. a detail stem of a parent that was not
   split), **When** the inspector is shown, **Then** that channel is not offered; a stored mute
   of a channel that has since disappeared is ignored with a visible note.

---

### User Story 4 - Save, reuse and attach to a mix (Priority: P1)

The user names the arrangement "Chorus drill", saves it, saves the current mix with it, reloads
the page and finds both; they duplicate the arrangement, rename it, delete an old one.

**Why this priority**: Practice versions are used again over days; without saving the editor is
a toy.

**Independent Test**: Save an arrangement, save a mix that uses it, reload, open the mix, verify
the arrangement is active and plays; duplicate, rename, delete (mix falls back to song order).

**Acceptance Scenarios**:

1. **Given** an unsaved arrangement, **When** the user saves with a name (1-80 characters,
   unique per song), **Then** it appears in the arrangement list of the song with its arranged
   length and region count.
2. **Given** a saved arrangement active in the mixer, **When** the user saves the mix, **Then**
   the mix remembers the arrangement; reopening the mix plays it.
3. **Given** unsaved edits, **When** the user leaves the mixer, **Then** they are warned and can
   save, discard or stay.
4. **Given** the arrangement was changed elsewhere (another tab) since it was opened, **When** the
   user saves, **Then** they are told it changed and can reload it or save as a copy; nothing is
   overwritten silently.
5. **Given** an arrangement used by two mixes, **When** the user deletes it and confirms (the
   confirmation names the mixes), **Then** it is gone and both mixes play in song order.

---

### User Story 5 - Export the arranged practice version (Priority: P2)

The user exports "What I hear" with the arrangement to the library as MP3 and takes it to the
rehearsal room.

**Why this priority**: The roadmap item ends with "export"; it reuses 007 and comes after the
editor works.

**Independent Test**: With an active arrangement, open "Save to library", keep "Use arrangement"
ticked, save as WAV; check the file length equals the arranged length (+/- 1 ms), the library item
says "arranged: <name>", and the audio matches the timeline vectors and the mixer.

**Acceptance Scenarios**:

1. **Given** an active arrangement (saved or not), **When** the user exports with "Use
   arrangement", **Then** the file has the arranged length, the cuts, repeats, region mutes and
   crossfades exactly as played, and the item's contents end with ", arranged (<name>)".
2. **Given** an export with a range, **When** it uses an arrangement, **Then** the range is a part
   of the arranged version (e.g. the second chorus repeat).
3. **Given** a library item made from an arrangement, **When** the arrangement is later edited or
   deleted, **Then** the item is unchanged and "Try again" (if it failed) reproduces the original
   arranged version.
4. **Given** the export dialog, **When** "Use arrangement" is unticked, **Then** the export is in
   song order as in 007.

---

### User Story 6 - Edit with the keyboard and a screen reader (Priority: P2)

A user who does not use a mouse builds "intro, chorus 4x, outro without vocals" with the keyboard
only, hearing every change announced.

**Why this priority**: Constitution and 004 accessibility rules; the timeline is a drag-heavy
widget by nature, so a full keyboard model must be designed, not retro-fitted.

**Independent Test**: With keyboard only (Tab, arrows, documented shortcuts) perform the
Independent Test of US1 + US3; an automated accessibility scan reports no violations; every
action has a visible focus and a polite announcement.

**Acceptance Scenarios**:

1. **Given** focus in the region lane, **When** the user presses Left/Right, Home/End, **Then**
   focus moves between regions and the screen reader reads "Region 3 of 7: Chorus, bars 25 to 32,
   4 times, guitar muted".
2. **Given** a focused region, **When** the user presses Alt+Left/Alt+Right, + / -, Ctrl+D,
   Delete, S, M, Enter, **Then** the region moves, its repeat changes, it is duplicated, removed,
   split at the playhead bar, the mute list opens, or playback starts at it; each is announced
   and undoable (Ctrl+Z, Ctrl+Shift+Z or Ctrl+Y).
3. **Given** the song strip, **When** the user presses Left/Right and Shift+Left/Right then Enter,
   **Then** a bar range is selected and added as a region; the same is possible with the "From
   bar" / "To bar" number fields.
4. **Given** any drag interaction (move a region, resize an edge, select bars), **Then** a
   non-drag alternative exists (WCAG 2.5.7) and no single-key shortcut acts outside the timeline
   (WCAG 2.1.4).

---

### User Story 7 - Levels, help and the read-only view (Priority: P3)

A Beginner opens a mix their teacher made with an arrangement: it plays arranged, a chip says
"Arranged: Chorus drill" and "Play song order" switches it off. An Advanced user finds help on
every part of the editor.

**Why this priority**: Progressive disclosure and help are cross-cutting rules (004); they ride
on the finished editor.

**Independent Test**: Switch level Beginner / Advanced / Expert and check visibility rules;
open every help topic from its anchor.

**Acceptance Scenarios**:

1. **Given** level Beginner, **When** the mixer is shown, **Then** the Arrange tab is hidden, the
   help topic says it is available from Advanced, and a mix with an arrangement shows the
   read-only chip with "Play song order".
2. **Given** level Advanced, **When** Arrange is open, **Then** all editing actions are available
   with bar snapping and the default crossfade; crossfade length, free timing and seconds fields
   are not shown.
3. **Given** level Expert, **Then** crossfade length (0-50 ms), snap mode (bars/free) and
   seconds fields are shown.
4. **Given** any editor area, **When** the user asks for help (anchor or F1 on focus), **Then**
   the matching help topic opens.

### Edge Cases

- **Unreliable beat grid**: bar snapping still works but shows "Bar lines may be inaccurate for
  this song"; Expert can switch to free timing.
- **No downbeats at all** (grid failed): bars mode is unavailable; Advanced users get "whole song"
  regions they can only repeat; Expert can use free timing. The reason is shown.
- **Beat grid re-analysed or track re-processed** after saving: the arrangement keeps its stored
  times and sounds the same; it is marked "bar lines changed" and offers "Snap to new bar lines".
- **Detail phase re-run** removes a detail stem that a region mutes: the mute is ignored (note in
  the inspector), nothing else changes; saving drops it.
- **Region at the very start or end of the song**: crossfades read silence beyond the file
  (no wrap-around, no error).
- **Very short regions** (0.5 s) with the maximum 50 ms crossfade: allowed (fades never overlap).
- **Repeat of a region that follows itself** (A A): the boundary is a jump (end -> start) and is
  crossfaded.
- **Edits while playing**: applied live; the playhead keeps its arranged time (clamped to the new
  length; at the end it stops at 0).
- **Seek in song time** (e.g. the 014 section list, the 006 lyrics line click) while arranged:
  jumps to the first occurrence of that time at or after the playhead (wrapping); if the time is
  cut out, to the next region that starts after it.
- **A-B loop**: while arranged, the loop is set on the arranged timeline ("Loop this region" or
  a selection of regions); a song-time loop request is refused with "Loop regions on the arranged
  timeline while an arrangement is active". Loop wrap points are crossfaded like any jump.
- **Speed and transpose (012)** apply to the whole arranged version; crossfades scale with speed.
- **Effects (017)** sit after the region mutes: a reverb tail rings out naturally after a mute.
- **Remote (018)** play/pause/seek keep working; the remote shows song time.
- **Recording (015)** is disabled while an arrangement is active (reason shown).
- **Track deleted**: its arrangements are deleted; library items keep their snapshot.
- **50 arrangements per song** reached: "New" is refused with the limit.
- **Concurrent save** from two tabs: second save gets a conflict (US4 AS4).
- **Engine cannot keep up** (heavy CPU): scheduling is 2 s ahead, so short stalls do not cause
  gaps; a late run is started at its correct offset (no drift between channels).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users MUST be able to create, name (1-80 characters, unique per song), save, open,
  duplicate, rename and delete **arrangements** of a song; an arrangement is an ordered list of
  1-64 regions plus a snap mode and a crossfade length; the stems are never modified.
- **FR-002**: A region MUST be a span of the song with a start and an end; with bar snapping both
  lie on bar lines from the beat grid (bar 0 = pickup before the first downbeat, the song end is
  the last line); with free timing (Expert) any time; every region is at least 0.5 s.
- **FR-003**: Users MUST be able to create an arrangement from the song's sections (014; one
  region per section, label = section name) or from the whole song (one region), add a region
  from a selected bar range, split a region at the playhead bar, change a region's start/end bar,
  move a region to another position, duplicate it, remove it, and set its repeat count 1-16.
- **FR-004**: Users MUST be able to mute any channel of the song (standard stems and kept detail
  stems) per region; muting a parent mutes its detail channels; a channel is audible in a region
  only if the mixer lets it through and the region does not mute it. Every instrument family is
  offered equally.
- **FR-005**: Playback of an arrangement MUST be gapless and sample-locked across all channels;
  every boundary where the song jumps MUST be crossfaded with the equal-power law of the shared
  vectors (default 10 ms, Expert 0-50 ms, 0 = hard cut); boundaries where the song continues
  MUST be bit-transparent; a mute change at a boundary MUST fade only that channel.
- **FR-006**: The mixer MUST show the arranged timeline (regions, playhead, arranged position and
  length, the region being played) while an arrangement is active, and offer "Song order" to
  switch it off at any time without losing edits.
- **FR-007**: Karaoke lyrics (006), score follow and section highlight (014), and the position
  shown in song terms MUST follow the material being heard (source time); the metronome and
  count-in (012) MUST play the arranged beats, keeping beat-1 accents on bar-snapped regions.
- **FR-008**: Edits MUST apply to playback live (also while playing) within 200 ms, keeping the
  arranged playhead position (clamped to the new length).
- **FR-009**: The editor MUST provide undo and redo of every edit, at least 100 steps, for the
  editor session; saving MUST be explicit; leaving with unsaved edits MUST ask to save, discard
  or stay; a save over a newer version MUST be refused with a choice to reload or save as a copy.
- **FR-010**: A saved mix MUST be able to reference one arrangement; opening the mix activates it;
  deleting an arrangement MUST set referencing mixes back to song order after a confirmation that
  names them.
- **FR-011**: Users MUST be able to export through the 007 library with the active arrangement
  (saved or unsaved) for "what I hear", "selected channels" and raw single stems; the export MUST
  match the in-browser law (shared vectors), have the arranged length, support a range in arranged
  time, and store a snapshot so retry reproduces it after the arrangement changes.
- **FR-012**: Limits MUST be enforced with readable messages naming the limit: 64 regions, repeat
  1-16, 256 parts after repeats, 30 minutes arranged length, region >= 0.5 s, crossfade 0-50 ms,
  50 arrangements per song.
- **FR-013**: The editor MUST be fully operable with the keyboard (focus, move, resize via bar
  fields, repeat, duplicate, split, mute, delete, undo/redo, play from region, bar selection),
  announce every change to assistive technology, offer a non-drag alternative for every drag,
  and scope single-key shortcuts to the focused timeline.
- **FR-014**: The editor MUST be available from level Advanced; crossfade length, snap mode and
  seconds fields from Expert; at Beginner a mix with an arrangement plays it and shows a read-only
  chip with "Play song order". Every editor area MUST have an in-app help topic (004 framework).
- **FR-015**: While an arrangement is active, a loop MUST be set on the arranged timeline
  ("Loop this region" or a region selection); a seek in song time MUST land on the first
  occurrence at or after the playhead; recording (015) MUST be disabled with the reason.
- **FR-016**: All arrangement operations MUST be available through the HTTP API with closed,
  documented schemas (agent-ready, no UI-only behaviour), including a stateless resolve
  operation that validates an unsaved arrangement and returns its timeline.
- **FR-017**: An arrangement whose stored edges no longer lie on the current bar lines (grid
  re-analysis) MUST keep sounding the same, be marked, and offer "Snap to new bar lines".

### Key Entities

- **Arrangement**: id, song (track), name, revision, snap mode (`bars` | `free`), crossfade (ms),
  regions, created/updated time; computed: resolved timeline, arranged length, stale flag,
  number of mixes using it.
- **Region**: source start and end (seconds), repeat 1-16, muted channels, label (0-40
  characters), originating section (optional).
- **Resolved timeline** (computed): sample rate, crossfade frames, segments (one per repeat:
  region index, repeat index, arranged start, source start, frame count, mutes, bar numbers),
  total frames, warnings.
- **Mix** (existing, extended): optional reference to one arrangement.
- **Export recipe** (existing, extended): optional arrangement snapshot (name, id, revision,
  resolved timeline).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user builds "chorus 4x" from sections and starts playback in under 30 seconds
  (Advanced level, moderated check by the CTO on the reference machine).
- **SC-002**: For every timeline case in the shared vectors, the backend resolver, the engine's
  run builder and clock, and the worker renderer match the expected values exactly (frames,
  flags) and to 1e-9 (gains, samples, seconds); the browser rendering of the sample cases matches
  to 1e-4.
- **SC-003**: Across 100 boundaries in the E2E suite, the arranged output has no gap (0 missing
  frames) and channels stay locked to within 1 frame of each other.
- **SC-004**: An exported arrangement's length equals the arranged length to within 1 ms (WAV/FLAC)
  in 100 % of test runs, and a song-order arrangement exports bit-identical to a 007 export without
  an arrangement.
- **SC-005**: An edit reaches the speakers within 200 ms of the keystroke while playing (resolve
  round trip + engine re-plan) on the reference machine.
- **SC-006**: A 10-minute arranged version of 6 stems exports in under 30 s on the reference
  machine (MP3 320).
- **SC-007**: The keyboard-only task of US6 is completed without a mouse in under 3 minutes, and the
  automated accessibility scan of the editor reports 0 violations.
- **SC-008**: For each instrument family (vocals, drums, bass, guitar, keys/piano, other) at least
  one acceptance test mutes it in a region and verifies silence there and audibility elsewhere.

## Assumptions

- 012 (engine 2.3.0, backend 1.11.0), 014 (worker 1.8.0, backend 1.13.0, engine 2.4.0), 015-018
  are merged before 019 Phase 0; 014 exposes the song's sections with start/end times through the
  backend (`GET /api/tracks/{trackId}/sections` or equivalent); Phase 0 adapts the names to the
  merged 014 contract without changing this feature's behaviour.
- The beat grid of 001 (`BeatGrid`: beats, downbeats, beatsPerBar, reliable) is the source of bar
  lines; no new analysis is needed (beat-analysis has no work in this feature).
- Stems are 44.1 kHz stereo (as 007); frame arithmetic uses 44,100 Hz everywhere.
- Single local user; "another tab" is the only source of concurrent edits.
- The 011 agent gets arrangement tools in a later tool-catalogue minor; the API is agent-ready now.
- Copyright: arranged versions are personal practice copies like stems and exports; the existing
  legal notice applies.
