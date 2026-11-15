# Feature Specification: Detail Separation

**Feature Branch**: `002-detail-separation`

**Created**: 2026-11-15

**Status**: Draft

**Input**: User description: "Detail separation (feature 002, builds on 001-stemstudio-mvp). After the
existing 6-stem separation a track is already playable. StemStudio then automatically determines, per
imported song, which further ("detail") splits are worthwhile and runs them in the background: drums
-> kick, snare, toms, hi-hat, cymbals; vocals -> lead and backing vocals; other/guitar/piano ->
instrument families such as strings, brass, woodwinds, synth, keys/organ, acoustic vs electric
guitar, as far as available models allow. [...] Limitations are shown in the UI. Models are
downloaded on first use and never committed." (full text in the feature request of 2026-11-15)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automatic detail stems for a new import (Priority: P1)

A musician imports a song as before. As soon as the six standard stems are ready the track is
playable, and StemStudio continues in the background: it checks which standard stems contain enough
sound, splits those further with the matching detail models, and keeps only the detail stems that
actually contain something audible in this song. When it is done, the track page lists the detail
stems found (for example "kick, snare, toms, cymbals" under drums, "lead, backing" under vocals,
"brass" under other) and the stems for which nothing further could be separated.

**Why this priority**: This is the feature's core value: more instruments to mute or solo, decided
per song without manual work.

**Independent Test**: Import a fixture song that contains drums, lead plus backing vocals and a horn
section; after the standard stems complete, the detail phase runs and the track ends with detail
stems for drums, vocals and other, while a silent standard stem (e.g. piano) is reported as skipped.

**Acceptance Scenarios**:

1. **Given** a newly imported song whose standard separation has completed, **When** the detail
   phase starts, **Then** the six standard stems remain playable in the mixer during the whole
   detail phase.
2. **Given** a standard stem that is silent or nearly silent, **When** the detail phase runs,
   **Then** that stem is not split and the track page states it was skipped because it is (almost)
   silent.
3. **Given** a detail model produced a sub-stem with no audible content for this song, **When** the
   detail phase finishes, **Then** that sub-stem is not offered; its sound (if any) is part of the
   parent's "rest" stem.
4. **Given** the detail phase finished, **When** the user plays all detail stems of a parent at
   their default settings, **Then** the result sounds the same as the parent stem (sub-stems plus
   rest add up to the parent).
5. **Given** a detail split that is implausible for this song (the detail stems do not add up to the
   parent within tolerance), **When** the phase finishes, **Then** that parent stays unsplit and the
   page says the split was not reliable.

---

### User Story 2 - Mix with detail stems (Priority: P1)

In the mixer each standard stem that has detail stems can be expanded. Expanded, its detail stems
(and its rest stem) appear as their own channel strips with fader, pan, mute, solo and meter, grouped
under the parent; the parent's own audio is then replaced by its detail stems so nothing is heard
twice. Collapsed, the parent plays as before. The drummer can mute only the snare, or the singer can
mute only the lead vocal and keep the backing vocals.

**Why this priority**: Detail stems are useless unless they can be mixed.

**Independent Test**: Open a track with detail stems, expand drums, mute the snare; the snare's
meter is silent, the other drum strips and all other stems keep playing, and the drums parent strip
no longer contributes audio.

**Acceptance Scenarios**:

1. **Given** a track with detail stems, **When** the user expands a parent, **Then** its detail
   strips appear grouped under it and the audible result at default settings is unchanged.
2. **Given** an expanded parent, **When** the user mutes, solos, pans or fades a detail stem,
   **Then** only that detail stem changes, with the same responsiveness as standard stems.
3. **Given** an expanded parent, **When** the user collapses it, **Then** the parent plays again
   with its own settings and the detail settings are kept for the next expansion.
4. **Given** solo is active on a detail stem, **When** playback runs, **Then** only soloed stems
   (standard or detail) are audible, consistent with the existing solo rules.
5. **Given** playback, seeking or an A-B loop, **When** detail stems are expanded, **Then** all
   audible stems stay sample-aligned, as standard stems do today.

---

### User Story 3 - Progress, failure and retry of the detail phase (Priority: P2)

The track page shows the detail phase separately from the standard separation: waiting, which
stem is being split, overall progress, and the result. If the detail phase fails or is interrupted
by a restart, the track stays usable with its standard stems, the page shows why, and the user can
retry only the detail phase.

**Why this priority**: The detail phase takes considerably longer than the standard separation; the
user needs to see that it is working and must never lose the standard stems because of it.

**Independent Test**: Force a detail-phase failure; the track remains "completed" and playable, the
detail section shows "failed" with a reason and a Retry button, and Retry runs only the detail phase.

**Acceptance Scenarios**:

1. **Given** the detail phase is running, **When** the user opens the track page, **Then** they see
   the current step (which stem), a progress percentage that updates at least every 10 seconds, and
   a notice that this can take a long time.
2. **Given** the detail phase failed, **When** the user clicks Retry, **Then** only the detail phase
   is queued again and the standard stems stay playable.
3. **Given** the application restarts during the detail phase, **When** it is back, **Then** the
   detail phase shows "interrupted by restart" with Retry, and the standard stems are intact.
4. **Given** detail phases of other tracks are queued, **When** a new import arrives, **Then** the
   standard separation of the new import is not delayed behind them.

---

### User Story 4 - Detail-separate existing tracks (Priority: P2)

Tracks imported before this feature (or whose detail phase was cancelled) show a "Split further"
action on the track page that starts the detail phase for that track.

**Why this priority**: The user already has a library of tracks; they should benefit without
re-importing.

**Independent Test**: Open a completed track without detail stems, click "Split further"; the
detail phase runs and the mixer afterwards offers the detail stems.

**Acceptance Scenarios**:

1. **Given** a completed track without detail stems, **When** the user clicks "Split further",
   **Then** the detail phase is queued and progress appears as in US3.
2. **Given** the same source was detail-separated before (another track, or before deletion while
   the cache exists), **When** the detail phase is requested, **Then** the cached result is reused
   and the phase completes within a few seconds.

---

### User Story 5 - Mixes with detail settings (Priority: P3)

Saving a mix stores the settings of detail stems and which parents are expanded; loading restores
them exactly. Mixes saved before detail stems existed still load and sound as before.

**Why this priority**: Builds on the existing mix library; lower priority because a session can be
practised without saving.

**Independent Test**: Expand drums, mute the snare, save "No snare", reload the page, load it: drums
are expanded and the snare is muted; an older mix of the same track loads with all parents collapsed.

**Acceptance Scenarios**:

1. **Given** a mix with expanded parents and changed detail settings, **When** it is saved and
   loaded again, **Then** expansion state and every detail setting are restored exactly.
2. **Given** a mix saved before the track had detail stems, **When** it is loaded, **Then** all
   parents are collapsed and the mix sounds exactly as when it was saved.
3. **Given** a mix is duplicated or edited, **When** it contains detail settings, **Then** they are
   copied or updated like standard stem settings.

### Edge Cases

- A standard stem has content but none of its detail sub-stems is audible: the parent stays unsplit
  ("nothing further to separate").
- Only one audible detail sub-stem under a parent (e.g. only lead vocals): the parent stays unsplit,
  since a split would add nothing.
- The 4-stem fallback model was used for the standard separation (no guitar/piano stems): the
  detail phase works on the stems that exist.
- A detail model cannot be downloaded (offline first use): the detail phase fails with "detail model
  could not be downloaded; try again when online"; standard stems unaffected.
- Not enough memory or disk for a detail model: the phase fails with a readable reason; no partial
  detail stems are shown.
- The user deletes a track whose detail phase is queued or running: refused with the reason, until
  the phase completes, fails or is cancelled (FR-016).
- Very long songs (up to the existing 15-minute limit): allowed; the page warns that the detail phase
  may take a long time.
- Two tracks of the same source request the detail phase at once: the work is done once and both
  get the result.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: After the standard separation of a new import completes, the system MUST
  automatically queue a detail phase for that track. The track MUST be fully usable (mixer, mixes)
  with its standard stems while the detail phase is queued, running, failed or completed.
- **FR-002**: The system MUST support detail splits for at least: drums (kick, snare, toms,
  hi-hat and/or cymbals as the chosen model provides), vocals (lead, backing), and the remaining
  instruments (other, guitar, piano) into the instrument families the chosen models provide (e.g.
  strings, brass, woodwinds, synth, keys/organ, acoustic guitar, electric guitar). Bass is not split.
- **FR-003**: Before splitting, the system MUST skip standard stems that are silent or nearly
  silent, and record "skipped: (almost) silent" for them.
- **FR-004**: After a detail model runs, the system MUST keep only sub-stems with audible content
  relative to their parent; inaudible sub-stems MUST NOT be offered.
- **FR-005**: For every split parent the system MUST provide a "rest" stem so that kept sub-stems
  plus rest reproduce the parent (difference at least 30 dB below the parent's level).
- **FR-006**: If fewer than two meaningful sub-stems remain, or the reconstruction check of FR-005
  fails, the parent MUST stay unsplit with a recorded reason.
- **FR-007**: The track page MUST show the detail phase status separately (queued, running with
  current step and progress, completed, failed with reason, cancelled), with the per-parent outcome
  (split into ..., skipped because silent, nothing further to separate, not reliable).
- **FR-008**: A failed, cancelled or interrupted detail phase MUST be retryable on its own; the
  standard stems and existing mixes MUST be unaffected by detail-phase failures and retries.
- **FR-009**: Tracks without a detail phase (imported earlier, or cancelled) MUST offer a "Split
  further" action that queues the detail phase.
- **FR-010**: Detail results MUST be cached per source (as the standard stems are), so the same
  source is not detail-separated twice while the cache exists.
- **FR-011**: Standard separation jobs MUST take priority over detail phases in the processing
  queue; a queued detail phase MUST NOT delay the start of a new import's standard separation. A
  running detail step may finish its current parent stem first.
- **FR-012**: In the mixer, parents with detail stems MUST be expandable and collapsible. When
  expanded, the parent's own audio MUST be replaced by its detail stems plus rest stem; each detail
  stem MUST have fader, pan, mute, solo and a post-fader meter like a standard stem.
- **FR-013**: Solo and mute rules MUST apply across standard and detail stems consistently: if any
  currently audible-level stem is soloed, only soloed stems are heard; a muted stem, or a parent's
  own audio while it is expanded, is never heard.
- **FR-014**: All audible stems (standard and detail) MUST stay sample-aligned during play, seek and
  A-B loop.
- **FR-015**: Mixes MUST store and restore the settings of detail stems and the expansion state per
  parent; mixes saved before a track had detail stems MUST load with all parents collapsed and sound
  unchanged.
- **FR-016**: Deleting a track MUST also remove its detail stems. A track whose detail phase is
  queued or running MUST NOT be deletable until the detail phase completes, fails or is cancelled;
  the user MUST be able to cancel a queued or running detail phase.
- **FR-017**: The UI MUST show the limitations of detail separation: quality varies per song and
  instrument, some instruments cannot be separated, and the detail phase takes considerably longer
  than the standard separation.
- **FR-018**: Detail models MUST be downloaded on first use into the model cache and MUST NOT be
  part of the repository; every model MUST be listed with its licence status in the third-party
  licence document (constitution IV, model weights exception).
- **FR-019**: After an application restart, a detail phase that was running MUST be marked
  "interrupted by restart" with Retry available; queued detail phases MUST either resume or be
  marked the same way.

### Key Entities

- **Detail phase**: Per track; status (none, queued, running, completed, failed, cancelled),
  current step, progress, error, and the per-parent outcomes.
- **Parent outcome**: Per standard stem: split / skipped (silent) / nothing further / not reliable,
  plus the list of its detail stems.
- **Detail stem**: Belongs to one parent stem of one track; name (e.g. kick, snare, lead, backing,
  brass, rest), audio file, and its order within the group.
- **Mix (extended)**: Existing mix plus, per parent, an expanded flag and per detail stem the same
  channel settings as standard stems (volume, pan, mute, solo).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a 4-minute song on the reference machine, the detail phase completes within
  30 minutes (target to be confirmed by the plan's feasibility measurement), and the standard stems
  are playable throughout.
- **SC-002**: On the fixture set, detail stems plus rest reproduce their parent with a difference at
  least 30 dB below the parent's level in 100 % of split parents.
- **SC-003**: On the fixture set, no offered detail stem is inaudible, and every (almost) silent
  standard stem is reported as skipped.
- **SC-004**: Mixer controls of detail stems take effect as fast as those of standard stems (within
  50 ms), with all audible stems sample-aligned after 50 loop repetitions.
- **SC-005**: A mix with detail settings, saved and reloaded, restores 100 % of its settings; 100 %
  of mixes saved before this feature load unchanged.
- **SC-006**: A new import's standard separation starts within 5 seconds of the worker becoming
  free, regardless of how many detail phases are queued (a running detail step may first finish its
  current parent stem).
- **SC-007**: A repeated detail request for an already detail-separated source completes within
  5 seconds.

## Assumptions

- Builds on 001-stemstudio-mvp: same users (single local user), import flow, standard 6-stem
  separation (or 4-stem fallback), mixer, mixes, track deletion (contract 1.1.0).
- Model choice is a plan decision based on `docs/research/detail-separation-models.md`; per ADR-009
  any available weights may be used (owner decision 2026-11-15). Code and libraries remain OSI-only.
- The detail phase starts automatically for new imports; there is no global on/off switch in this
  feature (a per-track Cancel exists). A settings switch can follow later.
- Expansion is per parent stem; collapsed is the default when a track is opened without a mix.
- Detail stems are stored in the same audio format as standard stems.
- The reference machine is the one used for the MVP measurements (CPU only); GPU support is not
  part of this feature.
- The thresholds for "silent", "audible" and "reliable" are tuned on the QA fixtures and recorded in
  the plan; the 30 dB reconstruction tolerance is fixed by FR-005.
- Disk usage grows with detail stems (roughly up to 3x the standard stems per track); no disk quota
  is introduced in this feature.
