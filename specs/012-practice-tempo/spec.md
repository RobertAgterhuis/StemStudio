# Feature Specification: Practice Tempo (slow down, transpose, speed trainer, count-in)

**Feature Branch**: `012-practice-tempo`

**Created**: 2026-11-16

**Status**: Draft (specified ahead of time; starts after 011 per `docs/ROADMAP.md`, built in
parallel with 013)

**Input**: Owner backlog `docs/MoSCoW.md` items M1, M2 and M3 (owner prioritisation
2026-11-16): "Slow down without changing pitch (50-120 % speed) and transpose (+-12 semitones)
for the whole mix, live in the mixer" (M1); "Speed trainer: an A-B loop that starts slow and speeds
up by n % after each clean pass, up to the target tempo" (M2); "Count-in and pre-roll before play
and before every loop pass (1-2 bars, metronome sound)" (M3). Owner decision 2026-11-16: time
stretching uses **SoundTouch (LGPL-2.1) compiled to WebAssembly**, not Rubber Band (GPL), so
StemStudio stays MIT. Principle: **every instrument counts equally**.

## Clarifications

### Session 2026-11-16

Answered autonomously by the architect (operating mode: fully autonomous); rationale in
[research.md](research.md), binding design in `docs/decisions/ADR-021-practice-tempo-time-stretch.md`.

- Q: What exactly changes with "speed"? → A: the whole mix (every stem and detail stem) plays
  slower or faster, 50 % to 120 % in 1 % steps, while the pitch stays the same. All stems stay
  exactly together. The metronome clicks, the karaoke ball and the position follow the slower
  music.
- Q: And "transpose"? → A: the whole mix is shifted by -12 to +12 semitones while the speed stays
  the same. Speed and transpose can be combined. Vocals sound "chipmunk"-like when shifted up
  (no formant correction) - this is a documented limitation.
- Q: Are speed and transpose applied live? → A: yes, while playing; a change is heard within a
  quarter of a second without stopping. Only the very first change away from the original speed
  and key in a playing session restarts playback at the same position once (a short gap).
- Q: What about quality at 50 %? → A: time stretching always adds some artefacts; below 70 % they
  become clearly audible ("smeary" attacks). The mixer shows a hint below 70 % and the help explains
  it. This is the trade-off of the owner's SoundTouch decision and is not hidden.
- Q: Are exports slowed down or transposed? → A: **no**. Exports (007) always use the original
  speed and key, so a library item is always the real recording. The export dialog says so while
  the mixer is slowed or transposed. Exporting a practice version may come with play-along
  recording (015).
- Q: What is a "clean pass" for the speed trainer? → A: StemStudio cannot hear whether the user
  played cleanly, so every completed pass counts; the user chooses how many passes are played at
  each speed (1-8, "passes per step") and can stop the trainer at any time. Settings: start speed,
  step, target speed, passes per step.
- Q: Count-in or pre-roll? → A: a count-in of 1 or 2 bars of metronome clicks (accent on beat 1 of
  each counted bar) before playback starts and, optionally, before every loop pass. The count-in
  continues the song's own beat grid so the music enters on the beat. A pre-roll that plays the
  music before the start point is not part of this feature (it belongs to recording, 015).
- Q: What does the count-in need? → A: a tempo: the detected beat grid when it is reliable, or the
  manual BPM of the metronome. Without one the count-in is unavailable with the reason shown.
- Q: Is the practice setting saved? → A: yes, in the mix (speed, transpose, trainer and count-in
  settings). Mixes saved before 012 open at 100 %, 0 semitones, trainer off, no count-in, and
  behave exactly as before.
- Q: Who sees what? → A: beginners see speed, transpose, reset and a count-in switch; advanced
  users also get the speed trainer, 2-bar count-in and count-in before every pass; experts also get
  exact number entry and the stretch diagnostics (latency, dropouts).
- Q: Every instrument? → A: speed, transpose, count-in and trainer work on the whole mix and are
  independent of the instrument; acceptance tests cover a case per instrument family (vocals,
  guitar, bass, keys, drums, other).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Slow down or transpose the song while practising (Priority: P1)

A guitarist mutes the guitar stem and slows the song to 70 % to learn a fast solo; a singer moves
the song two semitones down to fit her voice; a saxophonist transposes for his instrument. The song
keeps its key (or its speed), all stems stay together, and the metronome and karaoke ball follow.

**Why this priority**: the most used function of every practice tool (MoSCoW M1), useful for every
instrument; M2 depends on it.

**Independent Test**: open the mixer of an imported song, set 70 %, play: the song is slower, the
pitch of a test tone is unchanged, the position advances at 0.7x, the karaoke ball and the
metronome stay in time; set +2 semitones: the tone is 2 semitones higher at the same speed; save
the mix, reload, the settings come back.

**Acceptance Scenarios**:

1. **Given** a playing song, **When** the user sets the speed to 70 %, **Then** within 250 ms the
   whole mix plays at 70 % of its tempo with unchanged pitch, and the position, the metronome and
   the karaoke ball follow the slower music.
2. **Given** a playing song, **When** the user sets +2 semitones, **Then** within 250 ms the whole
   mix sounds 2 semitones higher at unchanged speed.
3. **Given** speed and transpose combined (e.g. 80 % and -3), **When** playing, **Then** both
   apply at once; all stems, including expanded detail stems, stay in sync.
4. **Given** the speed is below 70 %, **When** the user looks at the panel, **Then** a hint
   explains that slow speeds sound smeary and that this is normal.
5. **Given** a slowed or transposed mix, **When** the user saves and later reopens it, **Then**
   speed and transpose are restored; a mix saved before 012 opens at 100 % and 0 semitones.
6. **Given** the browser cannot run the time-stretch module, **When** the mixer opens, **Then**
   the speed and transpose controls are disabled with the reason and playback works normally.
7. **Given** a slowed mix, **When** the user opens the export dialog, **Then** it says that
   exports use the original speed and key.
8. **Given** the user presses `-`/`=` or `,`/`.` in the mixer, **When** focus is not in a text
   field, **Then** the speed changes by 5 % or the key by one semitone.

---

### User Story 2 - Speed trainer on the A-B loop (Priority: P2)

A drummer loops a difficult fill, starts the trainer at 60 % with steps of 5 % and two passes per
step up to 100 %: the loop plays twice at 60 %, twice at 65 %, and so on until it reaches full
speed, where it keeps looping.

**Why this priority**: turns the loop into an exercise (MoSCoW M2); needs US1.

**Independent Test**: set a 4-second loop, enable the trainer (start 60, step 10, target 80, 1 pass
per step), play: pass 1 at 60 %, pass 2 at 70 %, pass 3 and later at 80 %; the status shows pass and
speed.

**Acceptance Scenarios**:

1. **Given** an enabled loop and the trainer on, **When** playback starts, **Then** the first pass
   plays at the start speed and the status shows "Pass 1 - 60 %".
2. **Given** passes per step = n, **When** n passes are completed, **Then** the next pass plays
   exactly one step faster, never faster than the target; at the target the status says so and
   the loop continues at the target speed.
3. **Given** the trainer runs, **When** the user pauses and resumes, **Then** pass and speed are
   kept; **When** the user stops, changes the loop or a trainer setting, **Then** the trainer starts
   again at pass 1.
4. **Given** no loop is set, **When** the user looks at the trainer, **Then** it is disabled with
   "Set a loop (A-B) to use the speed trainer."
5. **Given** the trainer settings, **When** the mix is saved and reopened, **Then** they are
   restored (the trainer progress is not saved).
6. **Given** the trainer runs, **When** the user changes the transpose, **Then** it applies live;
   the speed control shows the trainer speed and cannot be dragged.

---

### User Story 3 - Count-in before play and before every loop pass (Priority: P3)

A bassist enables a one-bar count-in: after pressing play she hears four clicks, then the music
starts on the beat. With "before every loop pass" she gets a count-in before each repetition to
reset her hands.

**Why this priority**: small, valuable for every instrument (MoSCoW M3); uses the existing
metronome and beat grid.

**Independent Test**: on a track with a reliable beat grid enable a 1-bar count-in, press play:
four clicks at the song's tempo (scaled by the speed), the indicator counts 4-3-2-1, and the music
starts one beat after the last click; with an enabled loop and "before every pass" every repetition
is preceded by a count-in.

**Acceptance Scenarios**:

1. **Given** count-in 1 bar in 4/4, **When** the user presses play, **Then** four clicks sound
   (accent on the first), the indicator shows the remaining beats, and the music starts on the
   next beat of the song's grid.
2. **Given** speed 50 %, **When** counting in, **Then** the clicks are spaced at the slowed tempo.
3. **Given** 2 bars and "before every loop pass", **When** the loop repeats, **Then** each pass
   is preceded by a two-bar count-in in silence.
4. **Given** no reliable beat grid and no manual BPM, **When** the user looks at the count-in,
   **Then** it is unavailable with "Count-in needs a beat grid or a manual BPM."
5. **Given** a count-in is running, **When** the user pauses, stops or seeks, **Then** the
   count-in is cancelled; the next play counts in again.
6. **Given** the metronome is off, **When** counting in, **Then** the clicks still sound at the
   metronome volume; with volume 0 the count-in is visual only.

### Edge Cases

- Speed or transpose changed rapidly (slider drag): changes are coalesced; no clicks, no build-up of
  delay, no drift of the position.
- Very short loop (< 0.5 s of the song): plain looping stays seamless at any speed; the speed
  trainer and the count-in before every pass need a loop of at least 0.5 s and say so otherwise.
- Detail stems expanded or collapsed while slowed: the swap stays sample-locked.
- Seek, pause or stop while slowed: the next start uses the same speed; nothing of the previous
  position is heard.
- Very long session (1 h at 50 %): no drift between music, clicks and ball (positions derive from
  the audio clock).
- Track without beat grid: speed and transpose work; count-in unavailable; effective BPM hidden.
- `bpmOverride` set: the count-in and the effective-BPM text use it.
- The time-stretch module fails mid-session (processor error): the engine falls back to the
  original speed and key, shows the reason and keeps playing.
- Track with 20 channels (6 stems + detail stems): one stretch stage for the whole mix keeps the
  cost independent of the channel count.
- Old browser without AudioWorklet or WebAssembly: practice controls disabled with the reason.
- Mix saved with trainer on and later its loop removed: the trainer stays saved but inactive.
- Reduced motion: the count-in indicator does not pulse.
- Exports while slowed: original speed and key (note in the dialog); the karaoke offset and the
  score (008) keep using original track time.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The mixer MUST let the user set the playback speed of the whole mix from 50 % to 120 %
  in 1 % steps (buttons in 5 % steps) without changing the pitch.
- **FR-002**: The mixer MUST let the user transpose the whole mix by -12 to +12 semitones in 1
  semitone steps without changing the speed; speed and transpose MUST combine.
- **FR-003**: Speed and transpose changes MUST apply while playing, audible within 250 ms, without
  stopping playback (one restart with a gap of at most 350 ms is allowed for the first change away
  from the original speed and key in a playback session).
- **FR-004**: All stems and detail stems MUST stay sample-locked (<= 1 ms apart) at every speed,
  across loops, seeks, detail expansion and speed changes.
- **FR-005**: The playback position, the metronome clicks, the A-B loop, the karaoke banner (006)
  and the effective tempo MUST follow the changed speed; positions and timings MUST keep referring
  to the original recording (track time), so loops, lyrics offset and score positions stay valid.
- **FR-006**: The time-stretch and pitch-shift processing MUST use SoundTouch (LGPL-2.1) compiled to
  WebAssembly, running in the browser's audio thread, as a separately replaceable module; its
  licence text and the exact source MUST ship with the application.
- **FR-007**: At the original speed and key the audio MUST be processed exactly as before 012
  (no time-stretch module loaded).
- **FR-008**: The mixer MUST show a quality hint below 70 % speed or beyond +-7 semitones, and the
  effective tempo (track tempo x speed) when a tempo is known.
- **FR-009**: When the time-stretch module cannot run, speed, transpose and trainer MUST be disabled
  with the reason, while playback, count-in and every other function keep working.
- **FR-010**: The speed trainer MUST play the first loop pass at a start speed (50-119 %) and raise
  the speed by a step (1-20 %) after every n completed passes (1-8) up to a target speed
  (51-120 %, above the start), staying at the target; it MUST work only with an enabled loop and
  show pass number, speed and state.
- **FR-011**: Speed steps of the trainer MUST happen exactly at pass boundaries (sample-accurate).
- **FR-012**: The count-in MUST play 1 or 2 bars of metronome clicks (beats per bar of the
  metronome, accent on beat 1 of each counted bar) before playback starts and, when chosen, before
  every loop pass, at the tempo of the song's grid (or manual BPM) scaled by the speed, aligned so
  the music enters on the song's next beat.
- **FR-013**: The count-in MUST use the metronome sound and volume even when the metronome is off,
  MUST show a visual count, and MUST be unavailable (with the reason) without a usable tempo.
- **FR-014**: Speed, transpose, trainer settings and count-in settings MUST be saved in the mix;
  mixes saved before 012 MUST open with the defaults (100 %, 0, trainer off, no count-in) and
  behave as before.
- **FR-015**: Exports (007) MUST use the original speed and key; the export dialog MUST say so while
  the mixer is slowed or transposed.
- **FR-016**: The new UI MUST follow the 004 design system and journey levels (beginner: speed,
  transpose, reset, count-in switch; advanced: + trainer, count-in bars and every pass; expert: +
  number entry and stretch diagnostics) and MUST provide help topics for speed, transpose, count-in,
  speed trainer and sound quality/limits.
- **FR-017**: The controls MUST be accessible: keyboard operable (incl. the shortcuts `-`, `=`, `,`,
  `.`, `r`, `c`, `t`), with spoken values ("75 percent", "+2 semitones"), polite announcements for
  trainer steps and the count-in start, no announcement per beat, contrast >= 4.5:1 in both themes,
  and reduced motion respected.
- **FR-018**: Audible timing (speed changes, trainer steps, count-in clicks, music entry) MUST be
  scheduled on the audio clock, never by timers (constitution V).

### Key Entities

- **Practice settings** (part of a mix): speed percent, transpose semitones, speed trainer settings
  (enabled, start, step, target, passes per step), count-in settings (bars 0-2, every pass).
- **Practice status** (live, not stored): speed heard now, transpose, stretch state (bypass,
  loading, active, unavailable) and reason, latency, dropouts, trainer state/pass/speed, count-in
  availability and remaining beats.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A speed or transpose change is heard within 250 ms (measured in the browser from the
  control change to the changed output), and after the first engagement changes cause zero
  interruptions (no dropouts reported over 100 consecutive changes).
- **SC-002**: The pitch of a test tone stays within 1 % (about 17 cents) of the original at every
  tested speed (50, 70, 100, 120 %), and a transpose of n semitones gives the frequency ratio
  2^(n/12) within 1 % (tested -12, -5, +2, +12, also combined with 50 % and 120 %).
- **SC-003**: The playback position advances at the chosen speed within 2 % over 10 s; the karaoke
  ball lands within 50 ms of each word (006 SC-002) at 50 %, 75 % and 120 %; stems stay <= 1 ms
  apart after 50 loop passes and 20 speed changes.
- **SC-004**: With 20 channels at 50 % speed on the reference machine, playback runs 10 minutes
  without a dropout; the stretch processing uses at most 35 % of the audio thread's time budget, and
  the stretch module alone processes audio at least 10x faster than real time on the CI runner.
- **SC-005**: Count-in clicks sound within 1 ms of their planned times and the music enters exactly
  one beat after the last click (engine tests on the audio clock); in the browser the count-in
  duration matches bars x beats x beat length / speed within 50 ms.
- **SC-006**: The trainer changes speed exactly at the pass boundary (engine tests) and never above
  the target; in the browser the shown speed sequence for start 60, step 10, target 80 is
  60, 70, 80, 80.
- **SC-007**: 100 % of mixes saved before 012 load with the defaults and play with no time-stretch
  module requested (zero requests for the module in the browser test).
- **SC-008**: At least one acceptance test per instrument family (vocals, guitar, bass, keys,
  drums, other) slows down or transposes a mix in which that family's stem is soloed or muted.

## Assumptions

- Features 001-011 are merged; 006 (karaoke clock), 007 (exports), 004 (design, help, levels) are
  in place. The 011 agent's mix tools carry the new `practice` field through when they update a mix
  (they round-trip the mix DTO).
- SoundTouch 2.4.1 (LGPL-2.1) builds to standalone WebAssembly with Emscripten 6.0.7; Phase 0
  confirms the build, the latency and the CPU cost (research.md P0-1, P0-2); if the WASM build
  fails, the JavaScript port of SoundTouch (soundtouchjs, LGPL-2.1) is the fallback behind the same
  interface (ADR-021 D7).
- Supported browsers are the current Chromium-based browsers and Firefox (AudioWorklet and
  WebAssembly available); Safari is not tested (as before).
- Time-stretch quality at 50 % is lower than with commercial tools; the owner accepted this with the
  SoundTouch decision ("revisit only with the owner").
