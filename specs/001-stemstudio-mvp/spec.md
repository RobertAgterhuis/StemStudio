# Feature Specification: StemStudio MVP

**Feature Branch**: `001-stemstudio-mvp`

**Created**: 2026-11-14

**Status**: Draft

**Input**: User description: "StemStudio MVP: a musician imports a song (YouTube URL, Spotify
track URL, or local file), the song is split into instrument stems, and the musician practises
along with a mixer (per-stem volume/pan/mute/solo/meters, master, transport, A-B loop), a
beat-synced metronome, and saved, reusable mixes."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Import a song and get its stems (Priority: P1)

A musician wants to practise a song. They paste a YouTube link, a Spotify track link, or upload a
local audio file. The app downloads/accepts the audio, splits it into stems (vocals, drums, bass,
guitar, piano, other) and analyses its tempo and beat grid in the background, showing progress
until the track is ready.

**Why this priority**: Without stems there is nothing to mix or practise with; this is the core
value and the prerequisite for every other story.

**Independent Test**: Upload a short local audio file, watch progress move from queued to
completed, and verify that the track lists its stems, its detected BPM and its duration.

**Acceptance Scenarios**:

1. **Given** the import screen, **When** the user uploads an MP3, WAV or FLAC file within the size
   limit, **Then** a track is created and a processing job starts and reports progress (0-100%)
   with a human-readable stage (e.g. "Separating stems").
2. **Given** the import screen, **When** the user submits a valid YouTube video URL, **Then** the
   audio is downloaded, converted, and processed like an uploaded file; the track title is taken
   from the source.
3. **Given** the import screen, **When** the user submits a valid Spotify track URL, **Then** the
   track metadata is resolved and matching audio is fetched from a public YouTube source (never
   from Spotify's protected stream) and processed; title and artist come from Spotify metadata.
4. **Given** a completed job, **When** the user opens the track, **Then** six stems are available
   (or four if the fallback model was used), plus BPM, beat positions and time signature.
5. **Given** a track that was already processed from the same source, **When** the user imports it
   again, **Then** the cached result is reused and no new separation runs.
6. **Given** an invalid URL, an unsupported host, an unsupported file type or a file that is too
   large, **When** the user submits it, **Then** the import is rejected with a clear message and
   no job is created.
7. **Given** the import screen, **When** it is shown, **Then** a notice states that imports are
   for personal practice only and that the user is responsible for complying with copyright and
   the platforms' terms of service.
8. **Given** a running job that fails (download error, corrupt audio, model failure), **When** the
   failure occurs, **Then** the job shows status "failed" with a readable reason and the user can
   retry.

---

### User Story 2 - Mix the stems and play them in sync (Priority: P1)

The musician opens a processed track in the mixer. Each stem has a channel strip with a volume
fader, pan control, mute, solo and a level meter. A master section controls overall volume.
Transport controls (play, pause, stop, seek) keep all stems sample-locked. The musician can set
an A-B loop region to repeat a difficult passage.

**Why this priority**: Mixing (e.g. muting the guitar to play along) is the practice workflow
itself; together with US1 it forms the smallest viable product.

**Independent Test**: Load a processed track, mute one stem, solo another, move faders and pan,
set a loop from 0:10 to 0:20, and verify that playback stays in sync and loops.

**Acceptance Scenarios**:

1. **Given** a processed track, **When** the mixer opens, **Then** one channel strip per stem is
   shown with fader (0-100%, default 80%), pan (-100 L to +100 R, default 0), mute, solo and a
   live level meter, plus a master fader.
2. **Given** playback, **When** the user presses play/pause/stop or seeks, **Then** all stems start,
   pause, stop or jump together and remain aligned (no audible drift over the song).
3. **Given** one or more soloed stems, **When** playing, **Then** only soloed stems are audible;
   mute on a soloed stem still silences it.
4. **Given** A and B markers with A < B, **When** loop is enabled and playback reaches B, **Then**
   playback jumps back to A seamlessly and repeats until loop is disabled.
5. **Given** invalid loop markers (B <= A or outside the track), **When** the user sets them,
   **Then** the loop is not enabled and the user is told why.

---

### User Story 3 - Practise with a beat-synced metronome (Priority: P2)

The musician enables a metronome click that follows the song's detected beat grid, with its own
volume, an accent on beat 1 of each bar, and manual correction of BPM and offset when the
detection is off.

**Why this priority**: Strongly improves practice value, but the mixer is usable without it.

**Independent Test**: Load a processed track, enable the metronome, and verify that clicks align
with the detected beats, beat 1 is accented, and changing BPM/offset moves the clicks.

**Acceptance Scenarios**:

1. **Given** a processed track with a beat grid, **When** the metronome is on and the song plays,
   **Then** a click sounds on every beat, aligned to the detected grid, with an accented click on
   the first beat of each bar.
2. **Given** the metronome, **When** the user changes its volume or toggles it, **Then** the change
   takes effect immediately without affecting the stems.
3. **Given** a wrong detection, **When** the user sets a manual BPM (30-300) and/or an offset
   (-2000 to +2000 ms), **Then** the clicks follow the corrected grid; resetting restores the
   detected grid.
4. **Given** seeking, looping or pausing, **When** playback resumes, **Then** clicks stay aligned
   with the beat grid at the new position.

---

### User Story 4 - Save, load and manage mixes (Priority: P2)

The musician saves the current mixer state (all channel settings, master, metronome settings,
loop region) under a name, and later lists, loads, plays, edits, duplicates or deletes saved
mixes.

**Why this priority**: Saves set-up time across practice sessions; not needed for a first
practice session.

**Independent Test**: Save a mix, reload the page, load the mix, and verify every setting is
restored; duplicate, rename and delete mixes from the library.

**Acceptance Scenarios**:

1. **Given** a mixer state, **When** the user saves it with a name (1-100 characters), **Then** it
   appears in the mix library with the track title and last-modified time.
2. **Given** a saved mix, **When** the user loads it, **Then** the track opens in the mixer and all
   channel, master, metronome and loop settings are restored exactly.
3. **Given** a loaded mix, **When** the user changes settings and saves, **Then** the mix is
   updated (edit); "save as" with a new name creates a new mix.
4. **Given** a saved mix, **When** the user duplicates it, **Then** a copy named "<name> (copy)"
   is created with identical settings.
5. **Given** a saved mix, **When** the user deletes it and confirms, **Then** it disappears from
   the library; the track and its stems remain.
6. **Given** a name already used for the same track, **When** saving a new mix, **Then** the user
   is told the name is taken.

---

### Edge Cases

- A song shorter than a few beats or without a clear pulse: BPM detection may fail; the metronome
  is then disabled by default and the user can enter a BPM manually.
- Very long songs (longer than 15 minutes) are rejected at import to bound processing time.
- The worker is unavailable: imports are rejected with "processing service unavailable" and
  existing tracks and mixes remain usable.
- The 6-stem model cannot be loaded: processing falls back to the 4-stem model and the track
  records which model was used; mixes only reference stems that exist.
- The browser blocks audio until a user gesture: playback starts only after the user presses play.
- Duplicate concurrent imports of the same source reuse the same job.
- A Spotify track with no matching YouTube audio: the job fails with a clear reason.
- Deleting a track that has saved mixes is out of scope for the MVP (tracks are not deletable).

## Requirements *(mandatory)*

### Functional Requirements

**Import**
- **FR-001**: Users MUST be able to import audio from a YouTube video URL
  (`youtube.com`, `youtu.be`, `music.youtube.com`).
- **FR-002**: Users MUST be able to import a Spotify track URL (`open.spotify.com/track/...`); the
  system MUST resolve metadata and fetch matching audio from YouTube and MUST NOT circumvent DRM.
- **FR-003**: Users MUST be able to upload a local MP3, WAV or FLAC file of at most 200 MB.
- **FR-004**: The system MUST reject URLs from other hosts, playlists/albums, unsupported file types
  and oversized files with a clear message.
- **FR-005**: The import screen MUST show the personal-practice / copyright notice.

**Processing**
- **FR-006**: The system MUST separate each track into six stems (vocals, drums, bass, guitar,
  piano, other), falling back to four stems (vocals, drums, bass, other) if the six-stem model is
  unavailable.
- **FR-007**: The system MUST detect BPM, beat positions, downbeats and an assumed time signature
  for each track.
- **FR-008**: Processing MUST run as a background job exposing status (queued, downloading,
  separating, analysing, completed, failed), progress percentage and an error reason.
- **FR-009**: Results MUST be cached per source so re-importing the same source reuses them.
- **FR-010**: Documentation and UI help MUST state that separating every instrument individually
  beyond the available stems is not reliably possible with current open-source models.

**Mixer and playback**
- **FR-011**: The mixer MUST provide per-stem volume, pan, mute, solo and a level meter, and a
  master volume.
- **FR-012**: Playback MUST keep all stems sample-synchronised through play, pause, stop and seek.
- **FR-013**: Users MUST be able to define an A-B loop region and toggle looping.

**Metronome**
- **FR-014**: The metronome MUST click on the track's beat grid with an accent on beat 1, have its
  own volume and on/off toggle, and support manual BPM (30-300) and offset (-2000..+2000 ms)
  correction with reset.
- **FR-015**: Metronome clicks MUST be scheduled against the audio clock (sample-accurate), not
  against general-purpose timers.

**Mixes**
- **FR-016**: Users MUST be able to save a mix (track reference, channel settings, master,
  metronome settings, loop region) under a name unique per track.
- **FR-017**: Users MUST be able to list, load, play, edit (update), duplicate and delete mixes.
- **FR-018**: Users MUST be able to list previously imported tracks and open them in the mixer.

### Key Entities

- **Track**: A song imported once. Title, artist (optional), source type (YouTube, Spotify,
  upload), source reference, duration, processing status, model used, analysis results.
- **Stem**: One separated instrument part of a track. Name (vocals, drums, ...), audio file.
- **Beat analysis**: BPM, beat times, downbeat times, beats per bar.
- **Processing job**: The background work for a track. Status, stage, progress, error.
- **Mix**: A named, saved mixer configuration for one track. Channel settings per stem, master
  volume, metronome settings, loop region, timestamps.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can go from pasting a link to playing the separated stems of a 4-minute song
  without any manual steps other than pressing import and play.
- **SC-002**: Stems stay aligned within 1 ms of each other throughout playback, including after
  seeks and 50 loop repetitions.
- **SC-003**: Metronome clicks deviate by at most 1 ms from their scheduled beat times.
- **SC-004**: Mixer controls respond (audible change) within 50 ms of user input.
- **SC-005**: A saved mix restores 100% of its settings after an application restart.
- **SC-006**: Re-importing a previously processed source shows the track as ready within 2 seconds.
- **SC-007**: 100% of rejected imports show a reason the user can act on.

## Assumptions

- Single local user; no accounts or authentication (MVP runs on the user's own machine).
- The default tempo map is constant-BPM with detected beat times; time signature assumed 4/4
  when it cannot be determined. Tempo changes within a song are followed through the detected beat
  times, while the manual BPM override assumes a constant tempo.
- Stems are stored as 44.1 kHz stereo audio files that the browser can decode.
- Processing runs on CPU by default; a GPU is used automatically when available. Processing time
  for a 4-minute song on CPU may be several minutes; progress keeps the user informed.
- One job is processed at a time; further jobs are queued.
- Only individual Spotify tracks are supported (not playlists, albums or podcasts).
- Exporting/rendering a mix to an audio file is out of scope (first backlog item).
- Track deletion, user accounts and cloud hosting are out of scope.
