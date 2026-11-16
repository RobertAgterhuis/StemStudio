# Feature Specification: Practice Partner and Studio Bridge

**Feature Branch**: `021-partner-studio-bridge`

**Created**: 2026-11-16

**Status**: Draft

**Input**: MoSCoW Could items C9, C10 and C11 (`docs/MoSCoW.md`, owner decision 2026-11-16 after
the comparison with Moises): "**Practice partner**: generate a missing part (bass line, drums,
accompaniment) in the song's key and tempo, as an extra strip"; "**Amp and pedal simulation** for
guitar and bass played through an audio interface"; "**Export to a DAW** as DAWproject (open
format) with stems, tempo, sections and the arrangement". Roadmap item 021 (`docs/ROADMAP.md`),
depends on 009/010 (generative stack), 015 (play-along recording), 017 (mixer effects) and 019
(arrangement editor). Contract allocation: backend-api 1.20.0, worker-api 1.12.0, audio-engine
2.9.0, ui-test-ids 1.20.0, generator-api 1.2.0.

## Clarifications

### Session 2026-11-16

Answered by the architect from the MoSCoW entries, the constitution (1.3.1), ADR-009, ADR-016,
ADR-018, ADR-019 and the research in [research.md](research.md). No question was left for the
owner; every decision is recorded in ADR-025, ADR-030 and ADR-034.

- Q: Which model generates a single part that fits the song? -> A: ACE-Step 1.5 **base** DiT
  (`acestep-v15-base`, MIT code and weights) with its **lego** task: it generates one named track
  (bass, drums, keyboard, guitar, strings, ...) from the audio of the other stems. It is the only
  open model found that (a) generates one part conditioned on the song's own audio, (b) covers all
  instrument families and (c) runs in the 009/010 generator service already installed. The turbo
  model used by 009/010 cannot do lego (research R-01). No other model qualified (R-02).
- Q: How are key, tempo, sections and chords used? -> A: the strongest condition is the audio of the
  song itself (the model hears harmony and groove). Tempo, time signature and key go to the model
  as metadata; the chord progression (016) and the section name go into the caption. After
  generation a **fit check** measures beat alignment, key agreement and chord agreement against
  the song's analysis and shows the result honestly (and corrects a small timing offset).
- Q: Which instrument families? -> A: every family counts equally (MoSCoW principle): drums,
  percussion, bass, guitar, keys, synth, strings, brass, woodwinds and backing vocals (wordless).
  A lead vocal with words is not generated here (that is 010's lyrics-to-song).
- Q: Missing or alternative part? -> A: both. "Add a part" conditions on every stem; "Alternative
  for <stem>" conditions on every stem except that one and, when added, mutes the original so the
  user can A/B them.
- Q: GPU? -> A: GPU only, as 009/010 (constitution IV, GPU-only generative exception). Without a
  ready generator the controls are disabled with the reason; the rest of 021 (amp simulation, DAW
  export) works on every machine. Tests use the fake generator.
- Q: How does amp simulation work? -> A: Neural Amp Modeler (NAM, MIT) inference compiled to
  WebAssembly and run in an AudioWorklet in the browser, so the guitar or bass is processed with
  the lowest possible latency on the user's own machine. Chain per input: noise gate -> optional
  pedal capture -> amp capture (or a built-in basic amp) -> cabinet impulse response -> the 017
  strip effects.
- Q: Which amp profiles ship? -> A: four built-in basic amps written by StemStudio (guitar clean,
  crunch, lead; bass drive) that need no download, plus a curated catalogue of NAM captures that
  are **downloaded on request** (never committed), each with its own recorded licence, source and
  SHA-256 (admission rules in ADR-030). Users can add their own `.nam` files and cabinet impulse
  responses (WAV).
- Q: Is the dry signal kept when recording? -> A: yes. With the amp on, a 015 take stores the dry
  (DI) signal and the processed (wet) signal; the take plays the wet file by default and can be
  "re-amped" live through any rig later.
- Q: Which DAWs? -> A: DAWproject 1.0 (open format by Bitwig, MIT) is imported by Bitwig Studio,
  Studio One, Cubase, Cubasis, VST Live and n-Track; Reaper through a converter. **Ableton Live
  does not read DAWproject** (the MoSCoW entry named it); the help entry says so and points to the
  stem pack export of 007.
- Q: What goes into the DAWproject file? -> A: one audio track per exported channel (stems, detail
  stems, partner parts, takes), the tempo map from the beat grid, the time signature, sections as
  markers, the 019 arrangement as clips, and volume, pan, mute, solo, EQ and compressor of each
  strip. Settings without a DAWproject equivalent (reverb, practice EQ, amp captures) are listed
  in an export report; the wet take audio carries the amp sound.
- Q: Where does the DAWproject file land? -> A: in the 007 library as a new item kind, with its
  recipe, so it can be downloaded again or retried.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate a practice partner part (Priority: P1)

The user opens the mixer of a separated track, chooses "Practice partner", picks an instrument
family (for example bass) and either "add a part" or "alternative for the bass stem", optionally a
section range and a short style hint, and generates. When the job is done the user listens to the
takes with the song, sees the fit check, and adds the best take to the mixer as an extra strip
labelled AI-generated. The strip has volume, pan, mute and solo like every stem and is saved in
mixes.

**Why this priority**: The C9 core value: play with a band when a part is missing, or practise
against a different bass line or drum groove.

**Independent Test**: With the fake generator (CI), request a bass part for the fixture track; a
take appears, the fit check reports offset, key and chord agreement, "Add to mixer" creates a
`part-*` strip that plays sample-locked with the stems and survives a mix save and reload. On the
reference machine the same flow with ACE-Step produces a musically fitting bass line (owner
listening check, SC-003).

**Acceptance Scenarios**:

1. **Given** a ready track with a beat grid and a ready generator, **When** the user generates a
   part for one family, **Then** a partner job starts, shows its progress and on completion offers
   1 or 2 playable takes, each with a fit check (timing offset, key agreement, chord agreement
   where chords exist) and the AI-generated label.
2. **Given** a finished take, **When** the user chooses "Add to mixer", **Then** the take is
   conformed to the track (sample rate, start offset, length, loudness) and appears as an extra
   strip named after the family (e.g. "Bass (AI)"), playing in sync with the other channels.
3. **Given** "Alternative for <stem>", **When** the part is added, **Then** the original stem is
   muted in the current mix (not deleted) and the user can switch between them with mute or solo.
4. **Given** a track with sections (014), **When** the user picks a section, **Then** the part is
   generated for that range only and placed at the section's position; the rest of the strip is
   silent.
5. **Given** a style hint that names an artist or asks to imitate someone, **When** the user
   generates, **Then** the request is refused with the 009 imitation reason.
6. **Given** a part strip, **When** the user renames or deletes the part, **Then** the strip
   changes or disappears from every mix of the track; the library take stays.

---

### User Story 2 - Play guitar or bass through an amp simulation (Priority: P1)

The user plugs a guitar or bass into an audio interface, opens the input panel (015), switches on
"Amp", picks a rig (built-in basic amp, an installed capture, or their own `.nam`), adjusts input
gain, gate, output level and the cabinet, and hears the processed sound while the song plays. The
measured monitoring latency is shown with a warning and tips when it is too high.

**Why this priority**: The C10 core value: practise with a realistic tone without an amp; it does
not depend on a GPU and works on every machine with an audio interface.

**Independent Test**: With Chromium's fake audio input and the built-in "Guitar crunch" amp, the
input meter and the processed output meter both move, the processed signal differs from the dry
signal (spectral check in the E2E page probe), the latency panel shows a number, and bypass
returns the dry sound. In engine unit tests the NAM worklet processes a reference model to the
expected output.

**Acceptance Scenarios**:

1. **Given** an input device (015) and amp off, **When** the user switches the amp on with a
   built-in amp, **Then** the monitored signal is processed within one audio quantum and the input
   and output meters move.
2. **Given** an installed capture or own `.nam` file, **When** the user selects it as amp or pedal,
   **Then** it loads in under 2 s (standard-size model) and replaces the previous model without a
   click louder than the signal (cross-fade).
3. **Given** a measured round-trip latency above 30 ms, **When** monitoring is on, **Then** the
   panel shows a warning with tips (interface direct monitoring + "record dry, hear amp on
   playback", smaller buffer, close other audio apps).
4. **Given** the processing load of the chosen model exceeds the budget (audio glitches detected),
   **When** this happens, **Then** the panel says so and suggests a lighter model; the app never
   freezes.
5. **Given** a rig the user likes, **When** they save it, **Then** it is stored as a named rig
   preset (gate, pedal, amp, gains, cabinet, 017 effects) and can be chosen for any input later.
6. **Given** the input is a voice, keys or another instrument, **When** the user opens the input
   panel, **Then** the same chain is available (the catalogue offers guitar and bass captures; the
   other families use the clean chain with the 017 effects).

---

### User Story 3 - Export the song to my DAW (Priority: P2)

The user chooses "Export to DAW" on a track or mix, picks what to include (stems, detail stems,
partner parts, takes), the mix whose settings to use, and whether the arrangement (019) is used,
and gets a `.dawproject` file in the library. Opening it in Bitwig, Studio One or Cubase shows
one track per channel with the audio in place, the song's tempo map and time signature, sections
as markers, the arrangement as clips, and the volume, pan, mute, solo, EQ and compressor
settings. An export report lists what could not be carried over.

**Why this priority**: The C11 value: continue working on a practice version or a recording in a
real studio program. Useful but not needed for practising, hence P2.

**Independent Test**: Export the fixture track with a saved mix and an arrangement on the fake
stack; the downloaded file is a ZIP whose `project.xml` and `metadata.xml` validate against the
vendored DAWproject 1.0 XSD, contains one WAV per selected channel with the expected length,
tempo points that match the beat grid, one marker per section and the clip layout of the
arrangement. Owner check on the reference machine: the file opens in Bitwig Studio (or Studio One)
with audio aligned to the grid (SC-005).

**Acceptance Scenarios**:

1. **Given** a ready track, **When** the user exports to DAW with default options, **Then** a
   library item of kind "DAW project" is created, shows progress, and becomes downloadable as
   `<name>.dawproject`.
2. **Given** a mix with volume, pan, mute, solo and 017 effects, **When** exported, **Then**
   volume, pan, mute and solo are set on each DAW channel, EQ and compressor become the DAWproject
   built-in EQ and compressor, and reverb and practice EQ appear in the export report as "not
   carried over".
3. **Given** a track with sections and an arrangement, **When** exported with "Use arrangement",
   **Then** the clips follow the arrangement order (repeats and cuts) and the markers follow the
   arranged sections; without the option the song is exported as one clip per channel.
4. **Given** a beat grid with tempo changes, **When** exported, **Then** the DAW project has a tempo
   map with one point per bar and the audio plays without time-stretching.
5. **Given** partner parts and amp takes, **When** included, **Then** they become their own tracks
   (parts labelled AI-generated; for amp takes the wet audio plays and the dry DI track is
   included muted).
6. **Given** less than the required free disk space, **When** the user exports, **Then** the
   export is refused with the needed and free size.

---

### User Story 4 - Manage amp profiles and cabinets (Priority: P2)

The user opens the amp profile library, installs captures from the curated catalogue (each shows
its creator, licence and size), uploads own `.nam` captures and cabinet impulse responses, renames
and deletes them.

**Why this priority**: Needed for a wide choice of tones; US2 works with the built-in amps alone.

**Independent Test**: Install one catalogue entry from the fake catalogue host (checksum
verified), upload a valid `.nam` fixture and a cabinet WAV, see them in the rig picker; an invalid
or oversized file is rejected with the reason.

**Acceptance Scenarios**:

1. **Given** the catalogue, **When** the user installs an entry, **Then** the file is downloaded,
   its SHA-256 checked against the catalogue, and it appears with its licence and attribution.
2. **Given** a `.nam` file, **When** uploaded, **Then** it is accepted only if it is a valid NAM
   model file of a supported architecture and at most 16 MiB; its metadata (name, gear type,
   sample rate) is shown.
3. **Given** a cabinet IR WAV (mono or stereo, at most 2 s, 44.1 or 48 kHz), **When** uploaded,
   **Then** it becomes selectable as cabinet; other files are rejected with the reason.
4. **Given** a profile used by saved rigs, **When** deleted, **Then** the user is warned; rigs that
   used it fall back to "no capture" (built-in clean) and say so.

---

### User Story 5 - Record with the amp, keep the dry signal (Priority: P2)

While recording a 015 take with the amp on, both the dry input and the processed sound are
stored. The take plays the processed sound; the user can switch the take to "re-amp" and hear the
dry recording through any rig live, and can export both to the DAW.

**Why this priority**: Makes recording with the amp future-proof (change the tone afterwards); it
builds on 015 and US2.

**Independent Test**: Record a 5 s take with the fake input and the amp on; the take has a dry and
a wet file of equal length; in re-amp mode with another rig the output differs from the stored wet
file; with the amp off only the dry file is stored (015 behaviour).

**Acceptance Scenarios**:

1. **Given** the amp on, **When** a take is recorded, **Then** dry and wet files of the same length
   are stored with the rig snapshot, and the take plays the wet file with 015 latency compensation.
2. **Given** a take with a dry file, **When** the user switches on "re-amp", **Then** the take is
   played through the chosen rig live (no new file) and can be "printed" to a new wet file.
3. **Given** a measured latency too high for comfortable monitoring, **When** the user chooses
   "record dry, hear amp on playback", **Then** monitoring is dry (or off for interface direct
   monitoring) and the take plays through the rig afterwards.

---

### User Story 6 - Helpful at every level (Priority: P3)

Beginners see a simple "Practice partner" button with family and length only, and "Amp" with the
built-in presets; Advanced users get sections, style hint, takes, catalogue and rig presets;
Experts get guidance and step settings for the generator, model details, latency numbers and the
export report details. Every new control has a help entry.

**Why this priority**: Required by the 004 user-journey rules for every feature, small.

**Independent Test**: Switch levels; the controls shown match the level table in data-model.md
section 9; every new screen has a help link that opens the right topic.

**Acceptance Scenarios**:

1. **Given** the Beginner level, **When** the user opens the partner dialog, **Then** only family,
   "add/alternative", range "whole song" and generate are shown.
2. **Given** any new control with a help marker, **When** the user opens help, **Then** the topic
   for that control opens.

### Edge Cases

- Generator not installed, no GPU, or GPU busy: as 009 US6 (partner controls disabled with the
  reason, or the job waits for the GPU with a visible state); amp simulation and DAW export are
  unaffected.
- Base model weights not yet downloaded (about 4 GB more than 009/010): the dialog states the size
  before the first job; the download is a job phase with progress.
- Track without a beat grid or with a failed analysis: the partner dialog asks for a BPM and time
  signature (prefilled 120, 4/4); the fit check skips beat alignment and says so.
- Track without chords (016 not run) or key: the caption leaves them out; the fit check reports
  "no chord reference".
- The song is longer than 8 minutes: whole-song generation is offered for the first 8 minutes or
  per section; the dialog says so.
- The generated take does not contain the requested instrument (model failure) or is nearly silent:
  the fit check flags "part is very quiet / not detected" and the take is not offered as the
  recommended one.
- Fit check finds an offset above 250 ms or a tempo drift: the take is marked "does not lock to the
  beat"; it can still be added (no correction beyond 250 ms).
- The track is deleted: its parts are deleted with it; library takes stay (007 rule).
- No audio input device, permission denied, or the browser has no AudioWorklet/WebAssembly:
  the amp is disabled with the reason (015 rules for the device itself).
- The amp model has a different sample rate than the mixer (most captures are 48 kHz, the mixer
  runs at 44.1 kHz): the engine resamples inside the worklet and adds the extra latency to the
  shown number.
- A `.nam` file is corrupt, of an unsupported architecture, or too heavy for real time: rejected at
  upload (corrupt/architecture/size) or flagged at load (load), with the reason.
- Feedback howl when monitoring through speakers: the panel says to use headphones; the output is
  never above the master limiter of the mixer.
- DAW export while a channel has no audio yet (detail split still running): those channels are
  excluded with a note in the report.
- DAW export of a mix that references a deleted part or take: the channel is skipped and listed in
  the report.
- The DAW does not support FLAC audio in DAWproject: WAV is the default; FLAC is an Advanced option
  with a warning.

## Requirements *(mandatory)*

### Functional Requirements

**Practice partner (C9)**

- **FR-001**: The user MUST be able to request a generated part for one instrument family of a
  ready track: drums, percussion, bass, guitar, keys, synth, strings, brass, woodwinds or backing
  vocals (wordless), in mode "add" (conditioned on all audible stems) or "alternative for <stem>"
  (conditioned on all stems except that one).
- **FR-002**: The request MUST use the track's own audio as context and its tempo, time signature
  and key (from the beat grid and 016 analysis, editable) and MUST add the chord progression (016)
  and the section name (014) to the model caption when available.
- **FR-003**: The user MUST be able to choose the range: the whole song (at most 8 minutes) or one
  or more consecutive sections (014), 10 s to 8 minutes; and 1 or 2 takes.
- **FR-004**: An optional style hint (at most 200 characters) MUST pass the 009 imitation checks
  (no artist names, no "in the style of" phrases); StemStudio MUST NOT offer imitation of a named
  artist.
- **FR-005**: Partner jobs MUST follow the 009 job rules: states and progress, model download as a
  phase, cancellation, GPU-only with reason, one GPU step at a time across services, free-memory
  check with the low-memory profile and waiting, OOM retry, model release when idle, retry.
- **FR-006**: Each finished take MUST get a fit check: timing offset to the beat grid (ms),
  confidence, key agreement, chord agreement (share of beats whose chroma matches the 016 chord),
  and part loudness; the take with the best fit is marked recommended.
- **FR-007**: "Add to mixer" MUST conform the take to the track (44.1 kHz, offset corrected up to
  250 ms, placed at the range start, padded or trimmed to the track length, loudness matched to
  the replaced or median stem) and create a part strip with volume, pan, mute and solo, included
  in the solo/mute gate, in mixes, in 007 exports and in DAW exports.
- **FR-008**: In mode "alternative", adding the part MUST mute the original stem in the current mix
  (never delete it).
- **FR-009**: Parts MUST be listed per track and be renamable (1-60 characters) and deletable;
  a track keeps at most 16 parts.
- **FR-010**: Every take and part MUST be labelled AI-generated (strip badge, library badge, take
  details, file metadata `AI_GENERATED=true`, `GENERATOR=ACE-Step 1.5 base (<revision>)`,
  `STEMSTUDIO_GENERATION=<id>`, `STEMSTUDIO_PART_FAMILY=<family>`).
- **FR-011**: Every partner take MUST be stored in the 007 library (origin "generation", kind
  audio) with its recipe, so it can be re-added, downloaded or retried.

**Amp and pedal simulation (C10)**

- **FR-012**: The user MUST be able to switch an amp chain on for a 015 audio input: noise gate,
  optional pedal capture, amp (capture or built-in basic amp), cabinet (impulse response, built-in
  cabinet or none), input gain and output level, followed by the 017 strip effects; bypass MUST
  return the dry signal.
- **FR-013**: The amp chain MUST run in the browser in real time (AudioWorklet + WebAssembly) with
  no algorithmic latency beyond the resampler when the model rate differs from the mixer rate.
- **FR-014**: The system MUST show the monitoring latency (015 round-trip measurement plus the
  chain's own latency) and warn above 30 ms with tips, including "record dry, hear amp on
  playback".
- **FR-015**: The system MUST detect processing overload (render quanta exceeding the budget) and
  tell the user to choose a lighter model; it MUST NOT freeze the page.
- **FR-016**: StemStudio MUST ship four built-in basic amps (guitar clean, guitar crunch, guitar
  lead, bass drive) and a built-in cabinet per family that work without any download.
- **FR-017**: The user MUST be able to install captures from a curated catalogue; every entry MUST
  show creator, licence (SPDX id or exact licence name), source URL and size; the downloaded file
  MUST match the catalogue SHA-256; capture files MUST NOT be committed to the repository.
- **FR-018**: The user MUST be able to upload own `.nam` files (valid NAM model JSON, supported
  architecture, at most 16 MiB) and cabinet IRs (WAV PCM/float, mono or stereo, at most 2 s,
  44.1 or 48 kHz, at most 5 MiB), and rename and delete profiles and IRs.
- **FR-019**: The user MUST be able to save, load, rename and delete named rig presets; a rig whose
  capture was deleted MUST fall back to the built-in clean amp and say so.
- **FR-020**: When recording a take with the amp on, the system MUST store the dry and the wet
  signal (same length, same latency compensation) and the rig snapshot; the take MUST play the wet
  file by default and support live re-amp of the dry file through any rig, and "print" of a new
  wet file.

**DAW export (C11)**

- **FR-021**: The user MUST be able to export a track (optionally with a saved mix and an
  arrangement) as a DAWproject 1.0 file (`.dawproject`, ZIP with `project.xml`, `metadata.xml` and
  audio files), selecting stems, detail stems, partner parts and takes.
- **FR-022**: The file MUST contain one audio track per selected channel with its audio (WAV 24-bit
  by default; WAV 16-bit or FLAC as options) at the original timing, the tempo map (one tempo point
  per bar from the beat grid, or one constant tempo when chosen), the time signature, sections as
  markers, and track metadata (title, artist, comment "Exported from StemStudio").
- **FR-023**: With an arrangement (019), clips MUST follow the arrangement edit list; without it,
  one clip per channel spans the song.
- **FR-024**: Volume, pan, mute, solo, and 017 EQ and compressor settings MUST be mapped to
  DAWproject channel parameters and built-in devices; every setting that cannot be mapped MUST be
  listed in the export report stored with the library item.
- **FR-025**: Before the item becomes ready, `project.xml` and `metadata.xml` MUST validate against
  the DAWproject 1.0 XSD; a validation failure fails the export with a readable reason.
- **FR-026**: The export MUST land in the 007 library as a new item kind "DAW project" with its
  recipe (retry), download with Range support, and the 007 free-disk guard extended with the
  estimated size.
- **FR-027**: AI-generated parts in the export MUST keep their label (track comment and
  `metadata.xml` comment); the export MUST NOT contain voice profile samples (010).

**Cross-cutting**

- **FR-028**: Every new screen and control MUST link to in-app help (004 framework): practice
  partner, fit check, AI labels, amp chain, latency, captures and licences, own `.nam` files, IRs,
  rig presets, re-amp, DAW export, DAWproject compatibility (Ableton not supported), export report.
- **FR-029**: Controls MUST follow the user levels in data-model.md section 9.
- **FR-030**: All new controls MUST be keyboard operable, labelled for screen readers, and meet the
  004 contrast rules; meters and progress expose values to assistive technology.
- **FR-031**: The 011 agent tool catalogue MUST get entries for "generate partner part", "add part
  to mixer" and "export to DAW" in its next minor version (amp control stays a UI-only feature
  because it needs the live audio input).

### Key Entities

- **Partner request**: generation of kind partner (track, family, mode, replaced stem, range,
  sections, tempo, key, time signature, chords snapshot, style hint, takes, seeds).
- **Fit report**: per take: offset, confidence, key agreement, chord agreement, loudness, verdict.
- **Part**: an extra strip of a track made from a take (family, name, file, offset, gain match,
  source generation and take, fit report).
- **Amp profile**: a NAM capture (installed from the catalogue or uploaded): name, gear type,
  family, architecture, sample rate, file, SHA-256, source, licence, attribution.
- **Cabinet IR**: an impulse response file with name, channels, rate, length.
- **Catalogue entry**: curated, read-only capture reference (id, title, creator, licence, URL,
  SHA-256, size, family, gear type).
- **Rig preset**: named chain settings (gate, pedal, amp, cabinet, gains, 017 effect settings).
- **Take (015) additions**: dry file, wet file, rig snapshot.
- **DAW project export**: library item of kind DAW project with options, recipe and export report.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On the reference machine (RTX 5070 Ti 16 GB), a 60 s partner part completes in at
  most 3 minutes with warm models (excluding the one-time download).
- **SC-002**: For the 6-family acceptance set (one take per family: drums, bass, guitar, keys,
  strings, backing vocals) on 3 reference songs, at least 80 % of takes pass the fit check (offset
  <= 50 ms after correction, key agreement, chord agreement >= 60 % where chords exist).
- **SC-003**: In an owner listening check of 6 parts (one per family above), at least 4 are rated
  "usable for practice".
- **SC-004**: With a standard-size NAM capture, the amp chain uses at most 50 % of the render
  quantum budget on the reference machine (measured in the engine benchmark page), and adds no
  latency beyond the resampler (at most 2 ms).
- **SC-005**: 100 % of exported DAWproject files validate against the DAWproject 1.0 XSD
  (automated); the reference export opens in Bitwig Studio or Studio One with all tracks aligned
  to the grid within one audio block (owner check).
- **SC-006**: Exported channel audio is bit-identical to the 007 raw render of the same channel
  (automated for WAV 24-bit).
- **SC-007**: 100 % of partner takes and parts carry the AI-generated label in UI, library, file
  metadata and DAW exports (automated).
- **SC-008**: With the default CPU stack (no generator), 100 % of existing automated tests pass
  unchanged and amp simulation and DAW export work.

## Assumptions

- Features 009/010 (generator, GPU lease, job model, AI tags, imitation guard), 014 (sections),
  015 (inputs, latency calibration, takes; engine 2.5.0, backend 1.14.0), 016 (key and chords;
  worker 1.9.0, backend 1.15.0), 017 (per-strip effects; engine 2.7.0, backend 1.16.0) and 019
  (arrangement edit list; engine 2.8.0, backend 1.18.0, render-vectors 1.1.0) are merged before the
  021 wave. Their contracts are referenced by name only; every 021 change is additive.
- The mixer's AudioContext runs at 44.1 kHz (audio-engine contract) and 015 uses the same context
  for monitoring.
- Chromium-based browsers are the reference (AudioWorklet, WebAssembly SIMD, `setSinkId`);
  Firefox works without guarantees; Safari is not tested.
- Browser audio on Windows (WASAPI shared mode) typically gives 15-40 ms round-trip latency; ASIO
  is not reachable from a browser. This limit is stated in help (constitution V).
- Capture profiles are model weights under ADR-009; the catalogue admits only entries whose
  licence is recorded per entry (ADR-030).
- DAWproject 1.0 is stable (bitwig/dawproject, MIT); the XSD files are vendored with their licence.
