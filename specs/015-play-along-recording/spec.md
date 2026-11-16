# Feature Specification: Play-along Recording

**Feature Branch**: `015-play-along-recording`

**Created**: 2026-11-16

**Status**: Draft

**Input**: MoSCoW S3 (`docs/MoSCoW.md`): "Play-along recording: record yourself (microphone or audio
interface) over the mix with latency compensation, then listen back or export. Hearing yourself
against the original is how you improve." Roadmap item 015 (`docs/ROADMAP.md`), depends on 007
(export library) and 012 (practice tempo: speed, transpose, count-in). Principle: every
instrument counts equally (vocals, guitar, bass, keys, drums, acoustic and other instruments).
CTO brief of 2026-11-16.

## Clarifications

### Session 2026-11-16

Answered autonomously by the architect from the brief, the existing engine and the 007/010
designs; binding design in `docs/decisions/ADR-024-play-along-recording-and-latency.md`,
evidence in [research.md](research.md).

- Q: What can be recorded from? -> A: any audio input the browser offers: a built-in or USB
  microphone, or an audio interface input (guitar or bass DI / hi-Z input, amp-simulator or
  pedal output, keyboard line out, e-drum line out, a microphone on drums or an acoustic
  instrument). The user picks the input device and the channel mode: mono from input 1, mono
  from input 2, or stereo (inputs 1+2). Browsers expose at most the first two channels of an
  interface; this is stated in help.
- Q: How are takes kept in time with the song? -> A: the round-trip latency of the user's setup
  is measured once per input device with a loopback click test (a cable from output to input,
  or headphones held against the microphone), with a manual value as fallback and a browser
  estimate before the first calibration. Every take is shifted by that latency when it is
  saved, so it lines up sample-accurately with the stems.
- Q: What happens with 012's speed and transpose? -> A: recording works at 100 % speed and 0
  semitones only; the record button explains this and offers "Set to 100 %". Takes can be
  played back at any speed and transpose, together with the stems. Recording at a slower speed
  is a backlog item (time-stretching the user's own performance afterwards causes artefacts and
  needs a worker change for exports).
- Q: Count-in? -> A: recording uses 012's count-in (default 1 bar for recording, 0-2 bars);
  in loop recording a count-in can precede every pass (012 setting).
- Q: What does "one take per pass" mean? -> A: while the A-B loop is on, each pass through the
  loop is saved as its own take of the loop region; all passes are kept, grouped as one
  session, and the user marks the best and deletes the others in one action.
- Q: Where are takes stored and in which format? -> A: on the StemStudio server on this
  computer, lossless FLAC 24-bit 44.1 kHz, next to the track; they are not shown in the Library
  list (they live in the mixer's Takes panel) until the user exports them. Deleting a track
  deletes its takes; exports stay.
- Q: Do recordings ever leave the machine? -> A: no. Audio goes only from the browser to the
  local StemStudio server; no other service is contacted, takes are never sent to the AI agent
  (011) and cannot be used as a voice sample (010).
- Q: Monitoring? -> A: off by default (direct monitoring on the interface or amp is
  recommended, because monitoring through the browser is late). When switched on, the input is
  heard through the browser with a volume control and a headphones warning; it is never part of
  a take or an export.
- Q: How many takes can a track have, and how many play at once? -> A: up to 200 takes per
  track; one take plays at a time in an extra "Take" channel strip with volume, pan, mute and
  solo. Layering several takes is out of scope.
- Q: User levels (004)? -> A: Beginner: record button, input choice, level meter, count-in,
  guided calibration, takes list, compare. Advanced: channel mode, monitoring volume, manual
  latency, loop recording, take pan, export options. Expert: latency in samples and ms, take
  timing adjustment, dropout and clipping counters.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record myself over the mix and hear it back (Priority: P1)

A guitarist opens the mixer of a song, opens the recording panel, allows the microphone, picks
the audio interface input, sees the level meter move while playing, chooses "Guitar" as the part
(the original guitar stem is muted while recording), presses Record, hears a one-bar count-in,
plays along and presses Stop. The take appears in the Takes panel and plays immediately as an
extra channel strip next to the stems.

**Why this priority**: The core of the feature; everything else builds on a recorded take.

**Independent Test**: With Chromium's fake capture device playing a fixture, record 10 s over
the reference song; a take of 10 s (+/- one render quantum) appears, plays in the Take strip,
and is stored as a 24-bit FLAC on the server.

**Acceptance Scenarios**:

1. **Given** the mixer and no microphone permission yet, **When** the user opens the recording
   panel, **Then** an explanation ("Recordings stay on this computer") and an "Allow microphone"
   button are shown; the browser prompt appears only after that click.
2. **Given** permission, **When** the user opens the input list, **Then** every audio input is
   listed by name, the last used input is preselected, and the input level meter shows the
   signal (peak, clip indicator).
3. **Given** a selected part and "Mute the original part while recording" on, **When** the user
   records, **Then** the count-in plays, the part's stem is silent while recording, a visible
   and announced "Recording" state with elapsed time is shown, and Stop ends the take.
4. **Given** a finished take, **When** recording stops, **Then** the take is playable in the Take
   strip within 0.5 s (from the local capture) and is saved on the server as lossless FLAC; the
   panel shows "Saved" when the server has stored it.
5. **Given** speed is not 100 % or transpose is not 0, **When** the user looks at Record,
   **Then** it is disabled with "Recording works at 100 % speed without transpose" and a
   "Set to 100 %" action.
6. **Given** monitoring is switched on, **When** the user plays, **Then** they hear the input
   with the monitoring volume, a "use headphones" warning is shown, and the monitored sound is
   not part of the take.
7. **Given** a take recorded at 100 %, **When** the user plays back at 70 % speed or transposed,
   **Then** the take stays in time with the stems.

---

### User Story 2 - Calibrate latency so takes line up (Priority: P1)

Before (or after) the first recording the user runs "Calibrate latency": the app explains the
loopback (connect the interface output to an input with a cable, or hold the headphones to the
microphone), plays a short series of clicks, and shows the measured round-trip latency. If the
test fails, the reason and a manual setting are offered. The value is remembered for this input.

**Why this priority**: Without compensation every take is audibly late; sample-accurate
alignment is the feature's promise.

**Independent Test**: In the digital loopback E2E test (the engine's input is fed from its own
output) the calibration succeeds, and a take of the click-track stem recorded through the
loopback lines up with the stem within 2 samples.

**Acceptance Scenarios**:

1. **Given** a loopback, **When** the user calibrates, **Then** within 10 s the result shows the
   latency in ms (Expert also in samples), the number of clicks found and the spread, and the
   value is saved for this input device.
2. **Given** no signal, too few clicks found, or an inconsistent result, **When** calibration
   ends, **Then** a readable reason and a tip are shown (turn up the volume, move the headphones
   closer, use a cable) with "Try again" and "Enter manually".
3. **Given** "Enter manually", **When** the user sets a value (0-1000 ms) and saves, **Then**
   later takes use it and are labelled "manual latency".
4. **Given** no calibration for the input, **When** the user records, **Then** the browser
   estimate is used and the panel shows "Latency estimated - calibrate for exact timing".
5. **Given** a calibration made with a different output setup (output latency differs by more
   than 5 ms), **When** the panel opens, **Then** "Your audio setup changed - calibrate again"
   is shown.

---

### User Story 3 - Record during an A-B loop, one take per pass (Priority: P2)

A singer loops a difficult chorus and records five passes in a row; each pass becomes its own
take of the loop region. Afterwards they listen to each pass, star the best one and delete the
other passes in one action.

**Why this priority**: The most effective way to practise a hard passage, but single takes
already work without it.

**Independent Test**: Loop 4.0-8.0 s, record three full passes and stop in the fourth after
2 s; three complete takes of 4.0 s and one partial take of 2 s appear in one session group with
passes 1-4; "Keep the starred pass" deletes the others.

**Acceptance Scenarios**:

1. **Given** an enabled loop, **When** the user records, **Then** the pass counter shows the
   current pass and each finished pass appears as a take starting at the loop start.
2. **Given** 012's "count-in before every loop pass", **When** loop-recording, **Then** the
   count-in plays before each pass and is not part of any take.
3. **Given** a stop inside a pass, **When** at least 1 s of that pass was recorded, **Then** it
   is kept and marked "partial"; otherwise it is dropped.
4. **Given** a session with a starred take, **When** the user chooses "Delete the other passes"
   and confirms, **Then** only the starred takes of that session remain.
5. **Given** 50 passes in one session (or 30 minutes of capture), **When** the limit is reached,
   **Then** recording stops with a message and every pass recorded so far is kept.

---

### User Story 4 - Compare my take with the original part (Priority: P2)

The bassist switches between "My take", "Original" and "Both" while the song plays: "My take"
mutes the original bass stem and plays the take, "Original" plays the stem without the take,
"Both" plays both. The switch is instant and keeps everything in time. The Take strip has its
own volume, pan, mute, solo and meter.

**Why this priority**: Hearing the difference is how the user improves; it needs a take (US1).

**Independent Test**: With a take whose part is bass, switch modes while playing; the gates of
the bass stem and the take channel change at one context time and the meters follow.

**Acceptance Scenarios**:

1. **Given** a take with part "bass", **When** the user selects "My take", **Then** the bass stem
   (or all bass detail channels when expanded) is silent and the take is audible; "Original"
   does the reverse; "Both" plays both; the user's own mute/solo settings are not changed.
2. **Given** a take with part "none", **When** the user opens compare, **Then** only "Take on /
   off" is offered.
3. **Given** the Take strip, **When** the user changes volume, pan, mute or solo, **Then** it
   behaves like a stem strip (solo of the take silences the other channels, and so on), and the
   settings are saved with the mix.

---

### User Story 5 - Manage takes (Priority: P2)

The user sees every take of the song: number and name, part, position and length in the song,
date, latency method, whether it clipped, and a star. They select a take to load it into the
Take strip, rename it, star it and delete takes (one, several, or the other passes of a
session).

**Why this priority**: Several takes per song need order; deleting keeps the disk clean.

**Independent Test**: Record three takes, rename one, star one, delete one with confirmation; the
list, the Take strip and the server agree, and the deleted take's files are gone from disk.

**Acceptance Scenarios**:

1. **Given** takes, **When** the user opens the Takes panel, **Then** they are listed newest
   first, grouped by session, with the details above and the total size.
2. **Given** a take, **When** the user selects it, **Then** it loads into the Take strip and
   "Play from take start" moves the playhead to its first sample.
3. **Given** a take that is still being saved or failed to save, **When** the list is shown,
   **Then** its status is visible and a failed take offers "Try again".
4. **Given** a take used by a saved mix, **When** it is deleted, **Then** the mix keeps its other
   settings and no longer refers to the take.
5. **Given** a track with takes, **When** the user deletes the track, **Then** the confirmation
   names the number of takes that will be deleted and suggests exporting them first.

---

### User Story 6 - Export a take, or the take with the mix (Priority: P2)

The user saves their take alone (trimmed to its length) or "take + mix" exactly as they hear it
(including the compare mode), as FLAC, WAV or MP3 in the 007 library, optionally limited to the
loop region.

**Why this priority**: Sharing a recording with a teacher or keeping a progress diary needs a
file; the 007 library already provides formats and management.

**Independent Test**: Export the take alone as WAV 24-bit (its samples equal the take's samples)
and "take + mix" as MP3; both appear in the Library with contents "Take 2 - Bass" and
"Take 2 - Bass + drums + vocals + ..., mixed".

**Acceptance Scenarios**:

1. **Given** a ready take, **When** the user exports "Take only", **Then** a library item of the
   take's length (starting at its first sample) is created in the chosen format.
2. **Given** the mixer with a take loaded and compare "My take", **When** the user exports "What
   I hear", **Then** the file contains the take and every audible stem except the muted original
   part, at the current settings, and no metronome or monitoring.
3. **Given** "Selected channels", **When** the user ticks the take and two stems, **Then** only
   those are rendered.
4. **Given** a take still being saved, **When** the user exports, **Then** export is disabled with
   "The take is still being saved".

---

### User Story 7 - Adjust the timing of a take (Priority: P3)

An Expert user whose calibration was off nudges a take earlier or later (up to 200 ms in 0.1 ms
steps) while listening, and applies the change; the stored take and later exports use it.

**Why this priority**: Rescues takes recorded with a wrong latency; rarely needed after a good
calibration.

**Independent Test**: Nudge a take by -10.0 ms, apply; the stored take starts 441 samples
earlier (data-model 2.4 rounding rule) and the take's `alignmentRevision` increases by 1.

**Acceptance Scenarios**:

1. **Given** a ready take, **When** the user moves the nudge control, **Then** playback uses the
   new offset at once (no save yet).
2. **Given** a nudge, **When** the user applies it, **Then** the take is stored again with the new
   start, the take shows "timing adjusted by X ms", and the nudge resets to 0.

### Edge Cases

- **Permission denied** (now or earlier): the panel explains how to allow the microphone in the
  browser's site settings and offers "Try again"; playback and the rest of the mixer keep working.
- **No input device**, **device busy** (used by another program) or **unplugged while
  recording**: readable message; an unplugged device stops the recording and keeps what was
  recorded as a partial take.
- **Insecure context** (not localhost/https): recording disabled with the reason (as 010).
- **Unsupported browser** (no AudioWorklet, no `getUserMedia`, Firefox refusing a sample-rate
  mismatch): recording disabled with the reason; Chrome or Edge recommended.
- **Input clipping**: the meter shows clip; a take with clipped samples is labelled "clipped -
  lower the input gain on your interface".
- **Silent take** (all samples zero, e.g. wrong input): rejected with "The take is silent - check
  the input"; nothing is stored.
- **Very quiet take** (peak below -50 dBFS): stored and labelled "very quiet".
- **Recording reaches the end of the song** (no loop): recording stops at the end; the take ends
  at the song end.
- **Take starting before 0 after compensation** (recording from the very start): the samples
  before song position 0 are dropped.
- **Seek, loop change, speed change or track change while recording**: not allowed; those
  controls are disabled while recording (Stop first).
- **Speed trainer (012) active**: Record is disabled with the reason; the trainer is not
  started while recording.
- **Monitoring with speakers**: feedback risk; monitoring stays off by default and warns.
- **Leaving the mixer while a take is being saved**: the upload finishes in the background of
  the circuit; if the page is closed first, the take is lost and the next visit says nothing was
  saved (no partial files remain on the server).
- **Server out of disk space** (less than 1 GiB would remain): the take is refused with the 007
  message; the local capture stays playable until the panel is closed, and "Save again" retries.
- **Take limit** (200 per track) reached: Record is disabled with the reason.
- **Detail stems expanded**: compare "My take" silences all detail channels of the part.
- **Calibration while the song plays**: not allowed; calibration pauses playback and mutes the
  mix and monitoring.
- **Takes and the AI agent (011)**: the agent has no tool for takes in 015; takes are never sent
  to any LLM backend.
- **Takes as voice samples (010)**: refused by the exact-copy guard (take files are in the audio
  hash index).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The mixer MUST offer a recording panel that lists the browser's audio input
  devices by name, remembers the last used device, and offers the channel modes mono (input 1),
  mono (input 2) and stereo (inputs 1+2) as far as the device provides them.
- **FR-002**: The panel MUST show a live input level meter (peak with clip indicator) whenever
  the input is open; the input MUST be opened with echo cancellation, noise suppression and
  automatic gain control switched off.
- **FR-003**: The microphone MUST be requested only after the user asks for it in the panel,
  after an explanation that recordings stay on this computer; it MUST be released when the panel
  or the mixer closes. Every permission and device state (prompt, denied, no device, busy,
  unplugged, insecure context, unsupported browser) MUST have a readable message and next step.
- **FR-004**: The user MUST be able to record a take over the playing mix with Record/Stop; the
  recording MUST start with 012's count-in (default 1 bar for recording, 0-2 bars) and show a
  visible and screen-reader-announced recording state with elapsed time.
- **FR-005**: The user MUST be able to choose the part being recorded (vocals, guitar, bass,
  keys/piano, drums, other/acoustic, or none) and whether the original part's stem is muted
  while recording (default on when a part is chosen).
- **FR-006**: The user MUST be able to switch input monitoring on and off (default off) with a
  monitoring volume; monitored sound MUST NOT be part of any take or export.
- **FR-007**: The system MUST measure the round-trip latency with a loopback click test (at most
  10 s), report the result (latency, clicks found, spread) or a readable failure reason, and
  store it per input device; the user MUST be able to enter the latency manually (0-1000 ms);
  without either, a browser estimate MUST be used and labelled.
- **FR-008**: Every take MUST be compensated by the latency in use so that its samples line up
  with the stems: within 2 samples in the automated digital loopback test and within 1 ms with a
  cable loopback on the reference machine.
- **FR-009**: Recording MUST be possible only at 100 % speed and 0 semitones transpose (012),
  with a reason and a one-click reset otherwise; takes MUST play back in time at any speed and
  transpose.
- **FR-010**: With the A-B loop enabled, each pass MUST be saved as its own take of the loop
  region within one session; a partial last pass of at least 1 s MUST be kept and marked; a
  session MUST stop at 50 passes or 30 minutes of capture, keeping every recorded pass.
- **FR-011**: Takes MUST be stored on the local StemStudio server as lossless FLAC (24-bit,
  44.1 kHz) whose samples equal the recorded samples; up to 200 takes per track; a silent take
  MUST be rejected.
- **FR-012**: The mixer MUST show the selected take as an extra channel strip ("Take") with
  volume, pan, mute, solo and meter that behave like a stem strip; the take strip settings MUST
  be saved with a mix.
- **FR-013**: The user MUST be able to compare take and original part with "My take", "Original"
  and "Both", switching sample-locked without changing the user's mute/solo settings.
- **FR-014**: The user MUST be able to list, select, play from the start of, rename (1-80
  characters), star and delete takes, and delete the other passes of a session in one confirmed
  action; deleting a take MUST remove its files from disk.
- **FR-015**: The user MUST be able to export a take alone (trimmed to the take) or together with
  the mix ("What I hear" including the compare mode, or selected channels), in the 007 formats
  and optionally only the loop region, as a 007 library item.
- **FR-016**: An Expert user MUST be able to adjust the timing of a take by up to +/-200 ms in
  0.1 ms steps, audible immediately and stored when applied.
- **FR-017**: Recordings MUST NOT leave the machine: captured audio is sent only to the local
  StemStudio server; takes MUST NOT be offered to the AI agent or accepted as a voice sample.
- **FR-018**: Deleting a track MUST delete its takes, and its confirmation MUST state how many.
- **FR-019**: Every new area MUST link to in-app help topics (004): overview and privacy,
  permissions, connecting each instrument family (vocals, guitar and bass, keys, drums, acoustic
  and other instruments), latency calibration, monitoring, loop takes, compare, export, speed
  rules, timing adjustment.
- **FR-020**: Controls MUST declare their minimum experience level (004) as listed in the
  clarifications; Beginner sees the guided path.
- **FR-021**: All recording controls MUST be keyboard operable (Record/Stop shortcut `R`, compare
  shortcut `C`, both listed in the shortcut overview), MUST NOT rely on colour alone, and MUST
  respect reduced motion; the meter MUST expose its level to assistive technology at a limited
  rate.

### Key Entities

- **Recording take**: one recorded performance on a track: number and name, part, position
  (start sample) and length in the song, session and pass, input mode and channels, latency used
  (samples, method), device name, peak, clipped samples, dropouts, star, timing revision, status
  (saving, ready, failed) and the library file that holds the audio.
- **Input calibration**: measured or manual round-trip latency for one input device name, with
  the output latency at that time and the measurement quality.
- **Take channel settings** (part of a mix): which take is loaded, with volume, pan, mute, solo.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In the automated digital loopback test, a take of the click-track fixture lines up
  with the stem within 2 samples after calibration (100 % of runs, Chromium).
- **SC-002**: With a cable loopback on the reference machine, 5 consecutive calibrations agree
  within 0.5 ms and a recorded take lines up within 1 ms (owner check).
- **SC-003**: Calibration completes (success or reason) in at most 10 s.
- **SC-004**: A take is playable within 0.5 s after Stop, and a 5-minute stereo take is stored
  (status ready) within 15 s on the reference machine.
- **SC-005**: Recording for 10 minutes over a 6-stem mix causes no audible playback dropout and
  no capture gap on the reference machine (engine gap counter 0).
- **SC-006**: The stored FLAC of a take decodes to exactly the uploaded 24-bit samples
  (automated, 100 %).
- **SC-007**: During recording and saving, the browser contacts no origin other than the
  StemStudio web and API origins (automated network assertion, 100 %).
- **SC-008**: One acceptance case per instrument family (vocals, guitar, bass, keys, drums,
  acoustic/other) passes end-to-end with the fake capture device.
- **SC-009**: Zero axe violations on the recording panel, calibration dialog and Takes panel;
  every control reachable by keyboard.
- **SC-010**: With the default stack, 100 % of existing automated tests pass unchanged
  (all contract changes additive).

## Assumptions

- Features 007 and 012 are implemented first; 014 (audio-engine 2.4.0, backend 1.13.0) is built
  in parallel and merged by the CTO; 015's contract edits are additive on top.
- 012 provides count-in (with an option before every loop pass), exposes the transport's pass
  boundaries and the processing latency of its tempo stage, and processes any channel added to
  the mixer the same way as a stem (ADR-024 D2/D3; requests in contracts/DELTA.md).
- The app runs on localhost (or https), so `getUserMedia` is available.
- Users provide their own input hardware; StemStudio cannot set hardware input gain, so the
  meter and help guide the user to set it on the interface.
- Physical latency depends on the device, driver and buffer settings; StemStudio measures it,
  it does not reduce it. Direct monitoring on the interface is recommended.
