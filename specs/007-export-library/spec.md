# Feature Specification: Export Isolated Channels (Asset Library)

**Feature Branch**: `007-export-library`

**Created**: 2026-11-15

**Status**: Draft

**Input**: Owner request (Dutch, verbatim): "Het moet mogelijk zijn om geisoleerde kanalen op te
slaan zodat wij die later kunnen gebruiken." ("It must be possible to save isolated channels so
that we can use them later.") Roadmap (`docs/ROADMAP.md`): save any stem, detail stem or a
selection of channels (as currently mixed) as an audio file in a personal export library for
later use; the library is also the input/output store of sheet music (008), style transfer
(009), own lyrics to song (010) and the AI agent (011).

## Clarifications

### Session 2026-11-15

Answered autonomously by the architect during planning (operating mode: fully autonomous), based
on the feasibility spike in [research.md](research.md); binding design in
`docs/decisions/ADR-016-asset-library-and-export-rendering.md`.

- Q: What exactly is "saved"? → A: an audio file in a personal **library** inside StemStudio
  (not a download folder). Every saved item can be played, downloaded, renamed and deleted later,
  and later features can use it as input. Downloading is one action on a library item; the user
  can also download right after saving.
- Q: What does "as currently mixed" mean? → A: the mixer's current settings, saved or not:
  each channel's volume and pan, mute, solo, which parents are expanded into detail stems, and the
  master volume. Two choices: **"What I hear"** (exactly the audible channels, as the mixer plays
  them) and **"Selected channels"** (the user ticks channels; each is rendered with its own
  volume and pan, mute/solo are ignored, the master volume applies). Metronome clicks are never
  part of an export.
- Q: What does a single stem export contain? → A: the stem as separated: unity gain, centre, no
  master volume ("raw"). A FLAC export of a raw single stem in 16-bit is bit-identical to the
  stored stem.
- Q: Which formats? → A: FLAC (default; 16 or 24-bit), WAV (16 or 24-bit), MP3 (192, 256 or
  320 kbps; default 320). Always 44.1 kHz stereo. Beginners see only the format choice with the
  default; bit depth and bit rate are advanced options.
- Q: Can only part of the song be exported? → A: yes (P2): the loop region of the mixer, or a
  start/end time; at least 0.5 s.
- Q: What happens when a mix would clip? → A: the export is scaled down so its loudest sample is
  at -0.1 dBFS, and the library shows "Lowered by X dB to avoid clipping". It never clips.
- Q: What happens to saved items when their track is deleted? → A: they stay in the library
  ("source track deleted" is shown); the user deletes them separately.
- Q: Is there a size limit? → A: no quota; the library shows its total size, and an export is
  refused with a readable message when less than 1 GiB of disk space would remain.
- Q: How does the redesigned UI (004) affect this? → A: the feature adds a "Library" page and
  export actions on the track page and in the mixer, built with the 004 design system; every new
  area gets an in-app help topic, and advanced options declare their minimum journey level
  (beginner sees the simple path). Nothing here depends on details of 004 beyond those two
  framework hooks.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Save one stem or detail stem (Priority: P1)

A musician wants the isolated bass of a song (or only the snare from the detail separation) as a
file to practise with elsewhere or to use later. On the track page they choose "Save to library"
next to the stem, pick a format, and the item appears in the library, where it can be played and
downloaded.

**Why this priority**: The literal owner request; the smallest useful slice; it creates the
library that every other story and later feature uses.

**Independent Test**: Import the reference song, save `bass` as FLAC and `drums.snare` as MP3,
open the library, play and download both, and compare the FLAC with the stored stem.

**Acceptance Scenarios**:

1. **Given** a completed track, **When** the user saves the `bass` stem as FLAC 16-bit, **Then**
   a library item "<track title> - bass" appears with status "ready" within 15 seconds for a
   5-minute song, and its downloaded file is bit-identical to the stored stem.
2. **Given** a track whose drums were split into detail stems, **When** the user saves
   `drums.snare` as MP3 320 kbps, **Then** the library item plays in the browser and downloads as
   an `.mp3` file named after the item.
3. **Given** a track whose detail phase has not split a parent, **When** the user looks at that
   parent, **Then** no detail stem export is offered for it.

---

### User Story 2 - Save what I hear, or a channel selection, with the current mix (Priority: P1)

In the mixer the user has turned the guitar down, panned the bass and soloed drums and bass. They
choose "Save to library" and either "What I hear" or "Selected channels" (for example drums +
bass + vocals at their current levels). The saved file sounds like the mixer.

**Why this priority**: "Selection of channels as currently mixed" is the core of the roadmap
item and the most common practice need (a backing track without your own instrument).

**Independent Test**: In the mixer, set volumes, pans, a mute and a solo; save "What I hear";
compare the rendered file with a reference render of the same settings (shared vectors) and
listen to it.

**Acceptance Scenarios**:

1. **Given** unsaved mixer changes, **When** the user saves "What I hear", **Then** the export
   uses those changes (not the last saved mix), includes exactly the channels the mixer plays,
   and excludes the metronome.
2. **Given** an expanded drums group with `drums.hihat` muted, **When** the user saves "What I
   hear", **Then** the export contains the other drum detail stems and not the collapsed `drums`
   stem.
3. **Given** "Selected channels" with drums and bass ticked, **When** the user saves, **Then**
   only drums and bass are in the file, each at its mixer volume and pan, regardless of their
   mute/solo state.
4. **Given** settings that would push the sum above full scale, **When** the export is rendered,
   **Then** the file's peak is -0.1 dBFS and the library item shows by how many dB it was lowered.

---

### User Story 3 - Manage the personal library (Priority: P1)

The user opens the Library page to see every saved item with its source song, what it contains,
format, length, size and date. They play an item, download it, rename it and delete items they
no longer need.

**Why this priority**: "Later use" needs a place to find things again; without management the
library becomes unusable.

**Independent Test**: Save three items, then filter the library by song, play one, rename one,
delete one (with confirmation) and check the total size.

**Acceptance Scenarios**:

1. **Given** saved items, **When** the user opens the Library, **Then** they see name, source
   track (or "source track deleted"), contents (e.g. "drums + bass, mixed"), format, duration,
   size, date and status, newest first, plus the library's total size.
2. **Given** an item, **When** the user renames it to a new name (1-120 characters), **Then** the
   new name is shown and used for the downloaded file name.
3. **Given** an item, **When** the user deletes it and confirms, **Then** it disappears and its
   files are removed from disk.
4. **Given** an item still rendering, **When** the library is open, **Then** its status updates
   without a page reload and failed items offer "Try again".
5. **Given** a track that is deleted, **When** the user opens the Library, **Then** its saved
   items are still there and playable.

---

### User Story 4 - Export only a part of the song (Priority: P2)

A drummer wants just the bridge they are practising: they save the mixer's loop region (or a
start/end time) instead of the whole song.

**Why this priority**: Useful but not needed for the first release of the library.

**Independent Test**: Set a loop region in the mixer, save "What I hear" with "Only the loop
region", and check the file length.

**Acceptance Scenarios**:

1. **Given** a loop region of 12.0-28.5 s, **When** the user saves with "Only the loop region",
   **Then** the file is 16.5 s long (+/- 1 ms) and starts at 12.0 s of the song.
2. **Given** a start/end shorter than 0.5 s or outside the song, **When** the user saves,
   **Then** a readable validation message is shown and nothing is saved.

---

### User Story 5 - Save all stems at once (Priority: P3)

The user wants every stem of a song (optionally including detail stems) as separate files, for
example to load them into another program later.

**Why this priority**: Convenience; achievable with US1 repeatedly.

**Independent Test**: On a track page choose "Save all stems", then see one library item per
stem.

**Acceptance Scenarios**:

1. **Given** a completed track with split drums, **When** the user chooses "Save all stems"
   including detail stems, **Then** one raw item per standard stem and per kept detail stem is
   created (rest stems included), each with the chosen format.

### Edge Cases

- The worker restarts while an item is rendering: the item becomes "failed (interrupted by
  restart)" and "Try again" re-renders it from its stored recipe.
- The track is deleted while an item renders from it: the render may still finish (files were
  read before deletion) or fail with "The source track was deleted"; either way no broken item
  stays in "rendering".
- Two identical exports: allowed; each is its own item (names get a " (2)" suffix).
- A detail phase is re-run after an export: existing items are not changed (they are copies).
- Less than 1 GiB of free disk space would remain: the export is refused with a readable message.
- A channel id that does not exist on the track (e.g. a detail stem of a parent that was not
  split): rejected with a validation message naming the channel.
- An empty selection or a selection whose channels are all silent (e.g. "What I hear" with every
  channel muted): rejected with "Nothing to export: no channel is audible".
- Song of the maximum length (15 min) with all detail stems: rendering memory stays bounded
  (block-wise), and the item is ready within 60 s.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users MUST be able to save any standard stem of a completed track, and any kept
  detail stem of a split parent, as an audio file in their library, unprocessed ("raw").
- **FR-002**: Users MUST be able to save, from the mixer, either "what I hear" (exactly the
  audible channels under the mixer's current, possibly unsaved settings: volume, pan, mute, solo,
  expanded groups, master volume) or a selection of channels rendered with their current volume
  and pan plus the master volume.
- **FR-003**: Exports MUST sound like the mixer: the same gain, pan and solo/mute rules as the
  in-browser player, verified against shared reference vectors; the metronome is never included.
- **FR-004**: Supported formats: FLAC 16/24-bit (default), WAV 16/24-bit, MP3 CBR 192/256/320
  kbps, all 44.1 kHz stereo. A raw single-stem FLAC 16-bit export MUST be bit-identical to the
  stored stem.
- **FR-005**: An export MUST never clip: when the render's peak exceeds full scale, it is scaled
  to -0.1 dBFS and the applied reduction is stored and shown.
- **FR-006**: Users MUST be able to restrict an export to the loop region or a start/end time of
  at least 0.5 s inside the song (P2).
- **FR-007**: The library MUST list every item with name, kind, source track (or "deleted"),
  contents description, format details, duration, size, creation date and status, newest first,
  filterable by source track and kind, and show the library's total size.
- **FR-008**: Users MUST be able to play an item in the browser, download it (file name derived
  from the item name), rename it (1-120 characters) and delete it (with confirmation, removing its
  files).
- **FR-009**: Items MUST be independent of tracks and mixes: deleting a track or mix MUST NOT
  delete or change library items.
- **FR-010**: Each item MUST store how it was made (source track, channels, settings, range,
  format), so a failed item can be retried and later features or the agent can describe it.
- **FR-011**: Exports MUST NOT wait for running separations: a 5-minute song's export is ready
  within 15 s (single stem) / 30 s (mix of up to 22 channels) on the reference machine even while
  a separation runs.
- **FR-012**: The library MUST be usable as input and output by later features (008-011): items
  have stable ids, typed files and a machine-readable description, and the same operations are
  available through the HTTP API with documented request schemas (no UI-only behaviour).
- **FR-013**: Users MUST be able to save all stems of a track (optionally with detail stems) in
  one action, one item per stem (P3).
- **FR-014**: Every new screen area (library page, export dialog, export actions) MUST have an
  in-app help topic in the 004 help framework; advanced options (bit depth, bit rate, range,
  "selected channels") MUST declare their minimum journey level, with the beginner path being
  "Save to library" + format.
- **FR-015**: An export MUST be refused with a readable message when less than 1 GiB of free
  disk space would remain in the data directory.

### Key Entities

- **Library item (asset)**: id, kind (`audio` in this feature), name, status, files (each with a
  role, file name, type, size and checksum), audio facts (duration, format, bit depth or bit
  rate, peak, applied gain), provenance (origin, source track id and title snapshot, whether the
  track still exists, recipe), timestamps, error.
- **Export recipe**: the normalised request: track, channel selection or "audible", mode (raw or
  mix), the mix snapshot used, range, format.
- **Render plan** (internal): the flat list of input files with gain and pan plus master gain
  that the processing service executes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A single stem of a 5-minute song is in the library, ready to play, within 15 s of
  the request on the reference machine (measured: about 4 s for 4:22, FLAC).
- **SC-002**: A "what I hear" export of up to 22 channels of a 5-minute song is ready within 30 s
  (measured: about 9 s for six stems to MP3 320).
- **SC-003**: For every case in the shared reference vectors, the export's per-channel gains and
  the summed output differ from the reference by less than -90 dBFS (float rounding only), and
  the browser's own panner matches the vectors to within 1e-6.
- **SC-004**: A raw FLAC 16-bit single-stem export is bit-identical to the stored stem in 100 %
  of test runs.
- **SC-005**: No exported file contains a sample at or above full scale (100 % of renders).
- **SC-006**: A first-time user saves a stem and finds it again in the library in under one
  minute without help (moderated check by the CTO on the reference machine).

## Assumptions

- Single local user (as 001); no sharing, no accounts, no cloud storage.
- Stems are 44.1 kHz 16-bit stereo FLAC (measured); exports keep 44.1 kHz.
- 004 (design system, journey levels, help framework) is merged before this feature's UI work
  starts; if a framework hook is missing at that time, the frontend adds the help text and level
  declarations in the shape 004 defines and the CTO reconciles in the integration wave.
- Copyright: exports are for personal practice, like the stems; the existing legal notice
  applies and the library page repeats it in one line.
