# Feature Specification: Style Transfer with a Style Library

**Feature Branch**: `009-style-transfer`

**Created**: 2026-11-15

**Status**: Draft

**Input**: Owner request (Dutch, verbatim): "Ik wil geimporteerde nummers kunnen omzetten naar
andere muziek stijlen. Dus ik moet een soort van muziekstijl library hebben die ik kan toepassen."
(English: "I want to convert imported songs into other music styles, so I need a kind of
music-style library that I can apply.") Roadmap item 009 (`docs/ROADMAP.md`), research
`docs/research/roadmap-003-011.md` section 8, CTO brief of 2026-11-15.

## Clarifications

### Session 2026-11-15

Answered by the architect from the CTO brief and the feasibility spike (`research.md`,
`docs/decisions/ADR-018-generative-service-and-style-library.md`).

- Q: Which model does the conversion? -> A: ACE-Step 1.5 (MIT code and MIT weights), its
  "cover" task: the source audio keeps its structure and melody while caption and parameters
  choose the new style. YuE2 is not used (24 GB VRAM default, non-commercial weights).
- Q: Does it work without a GPU? -> A: no. Generation is GPU-only (NVIDIA, at least 8.5 GB free
  VRAM; with about 14 GB free the faster standard profile is used; research.md S-03). On a CPU-only install, or when the generative
  service is not started, every generative control is shown disabled with the reason ("Requires
  an NVIDIA GPU ..." or "Generative service not installed ..."); import, separation, mixing and
  everything else keep working unchanged. A CPU fallback for generation is not offered (a 45 s
  restyle would take far longer than the user would wait; CTO decision).
- Q: What is a "style"? -> A: a named set of musical choices: genre, optional sub-genre, tempo
  feel, instrumentation tags, mood tags, vocal handling and a default strength. StemStudio turns
  it into the model's text prompt and parameters. The library holds curated presets (read-only,
  shipped with the app) and the user's own styles (create, edit, duplicate, delete).
- Q: What is the input audio? -> A: a completed track's standard stems summed at unity gain
  (the original mix as far as the separation preserves it). The user can leave out stems (for
  example the vocals, for an instrumental restyle) and choose the whole song or a time range.
- Q: How long may a restyle be? -> A: 10 s to 8 minutes per job (model tier limit for 16 GB
  cards); the whole reference song (4:22) is allowed.
- Q: Where do results go? -> A: every result ("take") is stored as an audio file in the personal
  export/asset library of feature 007, labelled AI-generated, and can be imported as a new track
  that is separated and mixed like any other song.
- Q: Can a style name a real artist ("in the style of <artist>")? -> A: no. Presets name genres,
  eras, instruments and moods only. User style texts are checked against imitation phrases
  ("in the style of", "sounds like", "voice of", ...) and against the artist names of the user's
  own library and a curated deny-list; matches are rejected with a reason.
- Q: How many results per request? -> A: 1 or 2 takes (variations with different seeds); default 1.
- Q: GPU sharing with separation? -> A: one GPU job at a time across the worker and the
  generative service (shared GPU lease, ADR-018). A restyle waits while a separation step holds the
  GPU and vice versa; models are unloaded when the generative queue is empty.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Restyle an imported song with a preset style (Priority: P1)

The user opens a completed track, chooses "Restyle", picks a style from the library (for example
"Jazz trio" or "Synthwave"), optionally adjusts the strength, and starts. After a short wait the
restyled version can be played and is saved in the personal library, marked as AI-generated.

**Why this priority**: This is the owner's request; without it the feature has no value.

**Independent Test**: With the generative service running on a GPU, restyle a 45-second range of
the reference song with the "Jazz trio" preset; a playable take appears in the library with the
AI-generated label. With fakes (CI), the same flow completes with a fake take.

**Acceptance Scenarios**:

1. **Given** a completed track and a GPU-ready generative service, **When** the user restyles it
   with a preset, **Then** a restyle job is created, its progress is shown, and on completion a
   take of the requested length is playable from the track page and present in the library.
2. **Given** a restyle job in progress, **When** the user cancels it, **Then** the job stops, no
   take is stored, and the GPU is released.
3. **Given** a restyle job, **When** the generative service fails or runs out of GPU memory,
   **Then** the job ends as failed with a readable reason and can be retried; nothing else in the
   app is affected.
4. **Given** a finished take, **When** the user opens its details, **Then** the style used, the
   strength, the seed, the source track and range, the model and the creation time are shown.

---

### User Story 2 - Browse and choose from the style library (Priority: P1)

The user browses the style library: curated presets grouped by genre family, each with a short
description, tags (mood, instrumentation, tempo feel) and filters. The user can search and filter
and see which styles are their own.

**Why this priority**: The owner explicitly asked for a library to choose from; a restyle without
it is just a free-text prompt.

**Independent Test**: Open the library page: at least 24 presets in at least 8 genre families are
listed; filtering by a mood tag and searching by name narrow the list.

**Acceptance Scenarios**:

1. **Given** a fresh install, **When** the user opens the style library, **Then** the curated
   presets are listed with name, genre family, description and tags, and are marked as presets.
2. **Given** the library, **When** the user filters by genre family, mood or instrumentation, or
   searches by text, **Then** only matching styles are shown.
3. **Given** the restyle dialog, **When** the user picks a style, **Then** its description and
   default strength are shown before starting.

---

### User Story 3 - Create and edit own styles (Priority: P2)

The user duplicates a preset or creates a new style, edits its genre, tempo feel, instrumentation,
mood, vocal handling, default strength and an optional free description, and saves it in the
library for later use.

**Why this priority**: Personalisation; the presets already cover the main use.

**Independent Test**: Duplicate "Bossa nova", change instrumentation and mood, save, restyle with
it; the take's details show the user style. Edit and delete work; presets cannot be changed.

**Acceptance Scenarios**:

1. **Given** a preset, **When** the user duplicates it, **Then** an editable user style with the
   same values and the name "<name> (copy)" is created.
2. **Given** a user style, **When** the user saves invalid values (empty name, too long text,
   duplicate name, an artist name or an imitation phrase), **Then** saving is refused with the
   field and the reason.
3. **Given** a user style used by earlier takes, **When** the user edits or deletes it, **Then**
   the earlier takes keep the snapshot of the style they were made with.
4. **Given** a preset, **When** the user tries to edit or delete it, **Then** this is not offered
   (presets are read-only; duplicate instead).

---

### User Story 4 - Import a restyled take as a track (Priority: P2)

The user imports a take as a new track; StemStudio separates it into stems and the user mixes and
practises with it like any other song. The track is marked as AI-generated and links back to its
source track and style.

**Why this priority**: Makes the result useful for practice, the core purpose of StemStudio; the
take itself is already usable without it.

**Independent Test**: Import a finished take; a new track appears, runs the normal separation, and
opens in the mixer; its page shows the AI-generated label and the source.

**Acceptance Scenarios**:

1. **Given** a finished take, **When** the user imports it, **Then** a new track is created and
   processed by the normal separation; importing the same take again returns the same track.
2. **Given** an imported take, **When** the user views its track page or the track list, **Then**
   the AI-generated label and a link to the source track are shown.

---

### User Story 5 - Restyle only a section (Priority: P3)

The user restyles a chosen time range (for example the chorus) with a style and gets a take of
the full song in which only that range is restyled and the rest is the original.

**Why this priority**: Nice to have; range restyle (US1) already covers practising a section.

**Independent Test**: Restyle 60-90 s of the reference song "in place": the take has the song's
full length; outside the range it matches the source mix (RMS difference below -40 dB).

**Acceptance Scenarios**:

1. **Given** a completed track, **When** the user chooses "Restyle section in place" with a range,
   **Then** the take has the source length and only the range differs from the source.

---

### User Story 6 - Clear behaviour without a GPU (Priority: P1)

On an install without a usable NVIDIA GPU or without the generative service, the user sees why
restyling is unavailable and what is needed, and the rest of the app works as before.

**Why this priority**: The default install is CPU-only; it must never break or confuse.

**Independent Test**: Run the default (CPU) stack: the restyle button is disabled with the reason
"Generative service not installed"; with the service started on a machine without a usable GPU,
the reason is "Requires an NVIDIA GPU: <probe reason>". All existing tests pass unchanged.

**Acceptance Scenarios**:

1. **Given** the default stack without the generative service, **When** the user opens a track or
   the style library, **Then** restyle controls are disabled with the "not installed" reason and a
   help link; the style library can still be browsed and edited.
2. **Given** the generative service without a usable GPU, **When** the user opens a track, **Then**
   the controls are disabled with the GPU reason; the API refuses a restyle with `409` and that
   reason.

### Edge Cases

- The source track is not completed (queued, running, failed): restyle is not offered.
- The requested range is shorter than 10 s or longer than 8 minutes, or outside the track: refused
  with the limit.
- The GPU is busy with a separation step: the restyle waits ("Waiting for the GPU") and starts when
  the lease is free; while it runs, a new separation step waits for the lease (up to the worker's
  wait limit) or falls back to the CPU with its reason (ADR-012/ADR-018).
- Another program (game, local LLM) holds GPU memory: the job waits up to 10 minutes for enough
  free memory, then fails with "GPU busy: needs X GB, Y GB free".
- The generative service restarts during a job: the job is reported as failed ("interrupted") and
  can be retried; queued jobs are re-queued.
- Model weights are not yet downloaded: the first job downloads them (about 10 GB) and shows
  "Downloading model (first use)"; a failed download fails the job with the reason.
- Disk full while writing a take: the job fails; no partial file stays in the library.
- The source track is deleted while a restyle runs or after takes exist: takes stay in the library
  with the source shown as "deleted track".
- Two restyles are requested at once: they queue; at most one runs.
- A style is deleted while a queued job uses it: the job uses the snapshot taken at creation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST let the user restyle a completed track with a style from the style
  library, producing an audio take whose length equals the selected range.
- **FR-002**: The user MUST be able to choose the range (whole song or start/end, 10 s to 8 min),
  which standard stems are included in the source (default: all), the strength (5 steps from
  "subtle" to "free"; default from the style) and the number of takes (1 or 2).
- **FR-003**: The style library MUST ship at least 24 curated presets in at least 8 genre families;
  each has a name, genre family, description, tempo feel, instrumentation tags, mood tags, vocal
  handling and default strength. Presets MUST NOT name real artists, bands, songs or labels.
- **FR-004**: The user MUST be able to list, filter (genre family, mood, instrumentation, own/preset)
  and search styles, and to create, duplicate, edit and delete own styles. Presets are read-only.
- **FR-005**: Style texts MUST be validated: name 1-60 characters and unique per library;
  description up to 300 characters; at most 8 tags per tag group; the generated model prompt at
  most 512 characters. Texts containing an imitation phrase or an artist name of the user's
  library or of the curated deny-list MUST be rejected with the field and reason.
- **FR-006**: A take MUST record a snapshot of the style, the strength, the seed, the source track
  and range, the included stems, the model name and revision, the device and the duration of the
  job; this record is shown in the take details.
- **FR-007**: Restyle jobs MUST show their state (queued, waiting for GPU, downloading model,
  running with progress, completed, failed, cancelled) and MUST be cancellable while queued or
  running.
- **FR-008**: Every take MUST be stored as a lossless audio file in the personal export/asset
  library (feature 007) and MUST be labelled AI-generated in the library, in the take details and
  in the file metadata (tag `AI_GENERATED=true` plus generator name and version).
- **FR-009**: The user MUST be able to import a take as a new track; it is separated and mixable
  like any track, carries the AI-generated label and links to its source track and style. Importing
  the same take twice MUST return the same track.
- **FR-010**: Generation MUST run on an NVIDIA GPU only. When the generative service is not
  installed or has no usable GPU, all generative controls MUST be disabled with the reason and a
  help link, the API MUST refuse generation requests with `409` and the reason, and every
  non-generative feature MUST work unchanged.
- **FR-011**: At most one GPU-heavy step MUST run at a time across all StemStudio services on the
  same GPU (the 003 rule extended across containers).
- **FR-012**: Before a generation step the system MUST check free GPU memory against the step's
  measured need plus a margin; if it does not fit, the job waits (state "waiting for GPU") up to 10
  minutes and then fails with a readable reason. An out-of-memory error during generation MUST
  fail the job with a readable reason after one retry with the low-memory profile.
- **FR-013**: Generative models MUST be loaded for a job and released when the generative queue is
  empty, so other GPU users get the memory back.
- **FR-014**: Restyle in place (US5): the system MUST be able to regenerate only a range of the
  source while the rest equals the source.
- **FR-015**: The restyle dialog, the style library and the take details MUST link to in-app help
  entries (feature 004 framework): style library, restyle options, strength, GPU requirement,
  AI-generated label and personal-use notice.
- **FR-016**: The UI MUST show a notice that restyled versions of copyrighted songs are for
  personal practice only and are not for publication (same responsibility notice as imports).
- **FR-017**: Model weights MUST be downloaded on first use into the model cache volume, pinned by
  revision and SHA-256, and never be part of an image or the repository.

### Key Entities

- **Style**: a named set of musical choices (genre family, sub-genre, tempo feel, instrumentation,
  mood, vocal handling, default strength, description); kind preset or user.
- **Generation job**: one request to the generative service (kind restyle), with its parameters,
  state, progress, device record, error and takes.
- **Take**: one generated audio result of a job (index, seed, duration, file, AI-generated label,
  library entry, optional imported track).
- **Generative service status**: installed or not, GPU usable or not with reason, models present.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On the reference machine (RTX 5070 Ti 16 GB), a 45-second restyle completes in at most
  60 s with warm models and at most 3 minutes including model loading (excluding the one-time
  download); a 4:22 full-song restyle completes in at most 5 minutes.
- **SC-002**: With the default CPU stack, 100 % of the existing automated tests pass unchanged and
  the restyle controls show the "not installed" reason.
- **SC-003**: The library ships >= 24 presets in >= 8 genre families; an automated check confirms no
  preset text contains a name from the deny-list or the imitation-phrase list.
- **SC-004**: In a blind listening check by the owner on 5 presets x 2 songs, at least 7 of 10
  takes are judged "recognisably the source song" and "recognisably the chosen style".
- **SC-005**: 100 % of takes carry the AI-generated label in the library and in file metadata
  (automated check on every take produced by the fake and by the GPU validation run).
- **SC-006**: In fault-injection tests (fake service: OOM, not enough free memory, restart,
  cancel), 100 % of jobs end in a defined state with a readable reason and the GPU lease released.
- **SC-007**: A user can find and apply a preset to a track in at most 4 interactions from the
  track page (open restyle, choose style, start, play).

## Assumptions

- Features 003 (GPU device policy, ADR-012) and 007 (export/asset library) are available. This
  feature only needs 007's ability to register an audio file as a library entry with a kind, a
  title, an AI-generated flag and provenance; the exact 007 API is bound at feature start.
- Feature 004 provides the design system and the in-app help framework; this feature supplies its
  help entries and uses the 004 components.
- The generative service is an optional, GPU-only container started with a documented compose
  override; the default stack stays CPU-only and fully open source.
- About 1-3 GB of the 16 GB VRAM is used by the Windows desktop; a restyle needs about 11-13 GB in
  the standard profile and about 7.5 GB in the low-memory profile, chosen automatically by free
  memory (research.md S-03).
- Restyled versions of imported (copyrighted) songs are for personal practice; StemStudio does not
  publish or share anything.
- Restyle quality varies by source and style; this limitation is stated in the help entry
  (constitution V).
- Model weights fall under the model weights rule (ADR-009); ACE-Step 1.5 weights are MIT, so no
  non-commercial restriction applies.
