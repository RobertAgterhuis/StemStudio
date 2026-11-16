# Feature Specification: Mixer Effects and Controllers

**Feature Branch**: `017-effects-controllers`
**Created**: 2026-11-16
**Status**: Draft (architect, design complete; ready for `/speckit-analyze`)
**Input**: MoSCoW C2 (per-channel effects: EQ, compressor, reverb, a "practice EQ" preset that cuts
a band's frequency range) and C3 (MIDI controller support via Web MIDI: faders, mute/solo and
transport on a hardware controller; mapping screen; Chromium only). Roadmap feature 017, depends on
012 (practice tempo). Contract versions (docs/ROADMAP.md): backend-api **1.16.0**, audio-engine
**2.7.0**, ui-test-ids **1.16.0**; worker-api unchanged.

## Clarifications

### Session 2026-11-16 (architect, decisions recorded instead of asking; ADR-026)

- Q: Which effects per strip? → A: a **6-band parametric EQ** (high-pass, low shelf, two peaking
  mids, high shelf, low-pass), a **compressor** (threshold, ratio, knee, attack, release, make-up)
  and a **reverb send**. Inserts run pre-fader in the order EQ -> compressor; the send is
  post-fader, so a muted or faded-out strip also silences its reverb.
- Q: Reverb as insert or send? → A: **one shared reverb bus** per mix (kinds Room, Hall, Plate,
  with return level and pre-delay), fed by a send per strip. One reverb instead of up to 28 keeps
  the CPU low and is how mixers work; an insert reverb per strip is out of scope.
- Q: What is the "practice EQ"? → A: a one-click preset per **instrument family** with two modes:
  **Focus** (on the instrument's own strip: keep its core frequency range, trim what lies outside,
  lift its presence) and **Make room** (on every *other* audible strip: cut the instrument's core
  range, which removes separation bleed and leaves space for the user's own playing). Families:
  vocals, drums, bass, guitar, keys, orchestral (strings, brass, woodwinds) and other. Every
  family gets both modes (every instrument counts equally); acceptance tests cover each family.
- Q: Which presets exist? → A: built-in presets only (practice EQ per family plus a small set of
  tone and dynamics presets such as "Clean low end", "Warm", "Bright", "Gentle glue", "Leveler").
  A preset fills concrete values; the user can adjust them ("modified" shows). Saving own presets
  is not part of 017 (backlog).
- Q: Bypass? → A: per strip (all its effects, the send included) and a global "Bypass all
  effects" A/B switch in the master section. The global switch is a listening aid and is not
  saved; per-strip bypass is saved.
- Q: Are effects saved in mixes, and do old mixes keep working? → A: yes. Strip effects and the
  reverb bus are optional parts of a mix; a mix without them (every mix saved before 1.16.0) loads
  with effects off and sounds exactly as before. Duplicating a mix copies its effects.
- Q: Do effects apply on export (007 "exports sound like the mixer")? → A: yes for "What I hear"
  and "Selected channels" exports made from the mixer: the export dialog offers "Include effects
  (as you hear them)", on by default when any effect is active. Such an export is rendered in the
  browser with the same audio graph as playback, then encoded and saved by the unchanged 007
  export pipeline. Raw exports (single stems, stem packs) never include effects. Exports started
  without a browser (the AI agent, an API client) cannot include effects; they are saved without
  effects and the library says so. Details: research R-03, ADR-026 D5.
- Q: CPU budget with many strips? → A: effects use the browser's built-in audio nodes; nodes are
  created only for effects that are switched on and are suspended while a strip is hidden
  (collapsed group). Budget: the worst-case mix (28 strips, every strip with full EQ and
  compressor, reverb on) plays without dropouts on the reference machine, and an offline render of
  that mix runs at least 3x faster than real time on the CI runner (SC-005).
- Q: Which MIDI functions? → A: faders (strip volume, master), pan, mute, solo, reverb send,
  transport (play/pause, stop, back to start), loop on/off and set A/B, metronome on/off, speed
  and transpose of 012 (absolute knob or up/down buttons), count-in on/off, bypass all effects,
  and bank switching. Strip targets address **strip slots** 1-16 in mixer order with bank
  left/right, so one mapping works for every track.
- Q: Learning and mapping? → A: "MIDI learn" in the mixer (click a control, move a hardware
  control) and a Controllers page (devices, permission, the binding table with mode, delete,
  re-learn). Learn infers the mode (CC -> absolute fader, note -> toggle button, pitch bend ->
  14-bit fader); experts can choose relative encoder modes, invert and pick-up.
- Q: Browser support and permission? → A: Web MIDI is officially supported on Chromium browsers
  (Chrome, Edge). Access is requested only when the user presses "Connect controller", never on
  page load, and never with SysEx. Unsupported browser, insecure connection, denied permission or
  no device each shows a readable reason and what to do; the mixer works the same without MIDI.
- Q: Where are mappings stored? → A: in the StemStudio library (backend), one profile per device
  (name + manufacturer), so they survive a browser change and are part of backups (013). A known
  device reconnects with its profile automatically.
- Q: Levels (004)? → A: **Beginner**: only the practice EQ (Focus / Make room / Off on each
  strip). **Advanced**: everything (effects panel, presets, bypass, reverb bus, export option,
  MIDI learn and Controllers page). **Expert**: plus Q, knee, attack and release, gain-reduction
  meters, the effects-load readout, numeric entry and the expert binding options. Effects saved in
  a mix always play, whatever the level; a Beginner sees a "has effects" marker on such a strip.
- Q: Does the controller drive the motor faders or LEDs of the device? → A: no; MIDI output and
  feedback are out of scope (backlog).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Practice EQ: focus on an instrument or make room for it (Priority: P1)

A bassist wants to hear the bass line clearly, then wants the bass range gone from the rest of the
song so their own bass is not masked by bleed in the other stems. On the bass strip they choose
"Practice EQ -> Focus", later "Make room". Any musician can do the same for their instrument.

**Why this priority**: the practice value of C2; works at every level and for every family.

**Independent Test**: for one strip of each family, apply Focus and Make room and check the
engine's EQ state and the audible result (band energy of a rendered excerpt).

**Acceptance Scenarios**:

1. **Given** a track in the mixer at any level, **When** the user opens "Practice EQ" on a strip,
   **Then** the menu offers Focus, Make room and Off, named for the strip's family
   ("Focus on bass", "Make room for bass").
2. **Given** Focus on a strip of family F, **When** playing, **Then** that strip's EQ is the
   family's Focus curve (data-model 5) and the strip shows `data-practice-eq="focus"`.
3. **Given** Make room for family F, **When** playing, **Then** every other strip (standard and
   expanded detail strips, hidden ones included so expanding later keeps the rule) gets the
   family's cut, the chosen strip itself is unchanged, and each changed strip shows
   `data-practice-eq="make-room"`.
4. **Given** an excerpt rendered with "Make room for bass" on the other strips, **When** its
   energy between 40 and 250 Hz is compared with the same excerpt without effects, **Then** it is
   at least 12 dB lower; the analogous check holds for one representative strip of each of the
   seven families (data-model 5.3 test bands).
5. **Given** Off, **Then** the practice EQ is removed from the strips it was applied to and other
   effects on those strips stay as they were.
6. **Given** a Beginner, **Then** the practice EQ is the only effect control visible; a strip
   with other saved effects shows the "has effects" marker with a help link.

### User Story 2 - Shape a strip: EQ, compressor, reverb, presets, bypass (Priority: P1)

An advanced user wants the separated vocal less boomy, more even and in a small room. They open
the strip's effects, pick "Clean low end", raise a mid band, turn on "Leveler" and add some reverb
send; they compare with "Bypass all effects".

**Why this priority**: the core of C2.

**Independent Test**: on one strip, set each effect parameter and verify the engine state, the
curve, the gain-reduction meter (Expert) and bypass, without saving.

**Acceptance Scenarios**:

1. **Given** level Advanced, **When** the user opens a strip's effects, **Then** EQ, compressor
   and reverb send are shown with an EQ curve, a preset menu, a bypass toggle, copy/paste and reset.
2. **Given** a parameter change, **Then** it is audible within 50 ms without clicks (ramped), and
   the curve and values update.
3. **Given** a preset, **Then** its values are applied and the preset name is shown; changing a
   value afterwards shows "modified".
4. **Given** strip bypass on, **Then** the strip sounds as without effects (inserts and send off)
   and the bypass button has `aria-pressed="true"`; turning it off restores the settings.
5. **Given** "Bypass all effects", **Then** every strip and the reverb return are bypassed until
   it is turned off; the state is not saved.
6. **Given** level Expert, **Then** Q, knee, attack, release, numeric entry, the gain-reduction
   meter per compressor and the effects-load readout are available.
7. **Given** the reverb bus in the master section, **Then** kind (Room/Hall/Plate), return level
   and (Expert) pre-delay can be set, and the reverb return has its own meter.
8. **Given** 28 strips with full EQ and compressor and reverb on, **Then** playback stays free of
   audible dropouts on the reference machine (SC-005).

### User Story 3 - Effects are saved in mixes; old mixes keep working (Priority: P1)

**Why this priority**: effects that are lost on reload are useless; old mixes must not change.

**Independent Test**: save a mix with effects, reload the page and the mix; open a mix saved
before 1.16.0.

**Acceptance Scenarios**:

1. **Given** effects on strips and a reverb bus, **When** the mix is saved and loaded again,
   **Then** every effect value, preset name, strip bypass and the reverb bus are restored and
   sound the same.
2. **Given** a mix saved before 1.16.0 (no effects), **When** it is loaded, **Then** all effects
   are off and the mix sounds exactly as before; saving it again without changes stores no
   effects.
3. **Given** a mix with effects, **When** it is duplicated, **Then** the copy has the same effects.
4. **Given** an effects change, **Then** the unsaved indicator shows, as for any mixer change.
5. **Given** invalid effect values from an API client, **Then** the API refuses the mix with a
   readable validation problem naming the field.

### User Story 4 - Export what I hear, effects included (Priority: P2)

**Why this priority**: 007's promise "exports sound like the mixer" must still hold.

**Independent Test**: export "What I hear" with a practice EQ and reverb, then compare the file
with an offline render of the same state and with an export "without effects".

**Acceptance Scenarios**:

1. **Given** active effects and mode "What I hear" or "Selected channels", **When** the export
   dialog opens, **Then** "Include effects (as you hear them)" is shown and on.
2. **Given** that option on, **When** the user exports, **Then** the dialog shows render progress
   (cancellable), the item appears in the library and its contents say "with effects".
3. **Given** the exported file and a second offline render of the same state, **Then** they match
   sample for sample before encoding (FLAC 24-bit: identical after decoding to within one LSB).
4. **Given** the option off, or an export made without a browser (agent/API), **Then** the item is
   rendered exactly as in 007 and its contents say "without effects" when the mix had effects.
5. **Given** a range export, **Then** reverb and compressor at the range start sound as when
   playback runs through that point (pre-roll of up to 3 s is rendered and trimmed).
6. **Given** a raw stem or a stem pack, **Then** no effects option is shown and no effects apply.
7. **Given** a failed effects export, **When** the user presses "Try again", **Then** it is
   re-encoded from the stored pre-render; after the item has completed or been deleted the
   pre-render is gone.

### User Story 5 - Control the mixer with a MIDI controller (Priority: P2)

A guitarist with a small 8-fader controller wants to ride the backing levels and start/stop with
the foot or a pad while holding the instrument.

**Why this priority**: C3; hands on the instrument, every instrument benefits.

**Independent Test**: with a fake MIDI device in Playwright: connect, learn a fader, a mute
button, play and speed, move them and check the mixer; reload and reconnect.

**Acceptance Scenarios**:

1. **Given** a Chromium browser and level Advanced, **When** the user presses "Connect
   controller", **Then** the browser asks for MIDI permission once; on grant the device list
   shows the connected inputs and the mixer's MIDI status chip shows the device.
2. **Given** MIDI learn on in the mixer, **When** the user selects a control (click or keyboard)
   and moves a hardware control, **Then** they are bound, the mode is inferred and the binding
   appears on the Controllers page; Escape or 10 s without input cancels.
3. **Given** a bound fader, **When** it moves, **Then** the mixer fader follows and the change is
   audible within 50 ms (p95) of the MIDI message.
4. **Given** pick-up on (default for absolute faders), **When** the hardware fader is far from
   the screen value, **Then** the value only changes after the hardware fader crosses it.
5. **Given** bindings for mute, solo, pan, master, play/pause, stop, loop, metronome, speed and
   transpose (012), count-in and bypass all, **Then** each does the same as its on-screen control.
6. **Given** strip slots 1-8 and a track with more strips, **When** bank right is pressed,
   **Then** slots address strips 9-16 and the bank indicator says so.
7. **Given** a control already bound, **When** it is learned for another target, **Then** the
   user confirms the replacement.
8. **Given** the page is reloaded or the device reconnected, **Then** its profile is active again
   without re-learning.

### User Story 6 - Graceful fallback and permissions (Priority: P3)

**Acceptance Scenarios**:

1. **Given** a browser without Web MIDI, **Then** the Controllers page and the status chip say
   "Your browser does not support MIDI controllers. Use Chrome or Edge, or the keyboard shortcuts"
   and link the shortcut help; no permission prompt appears.
2. **Given** an insecure connection (plain http from another machine), **Then** the reason says
   MIDI needs `localhost` or https.
3. **Given** permission denied, **Then** the reason explains how to allow MIDI in the site
   settings, and "Try again" re-checks.
4. **Given** no device, or the device is unplugged while playing, **Then** playback continues,
   the chip shows "No controller", and the profile resumes when it is plugged in again.
5. **Given** level Beginner, **Then** no MIDI control is shown, but an existing enabled profile
   still works.

### Edge Cases

- A detail strip with effects is hidden (group collapsed): its effects are suspended and resume on
  expand; the practice EQ "Make room" also covers hidden strips.
- The reverb tail keeps ringing after stop or pause; on a new track load it is cut.
- A preset or practice EQ applied while bypassed: values change, the strip stays bypassed.
- Make room is applied for family A while Focus is on a strip of family B: the Focus strip gets
  the cut as well (it is another strip); the UI says which rule is active per strip.
- Make room needs another strip: on a track with one audible strip it is disabled with a reason.
- A strip with compressor make-up can exceed 0 dBFS: the meter shows clip as today; exports stay
  protected by 007 clip protection.
- Export with "Bypass all effects" on: the dialog says effects are bypassed and the option is off.
- Export pre-render cancelled, page closed or upload failed: no library item is created; an
  orphaned pre-render is removed after 60 minutes.
- Long tracks: a 15-minute pre-render needs about 320 MB in the browser; the dialog warns above
  10 minutes; a pre-render larger than 320,000,000 bytes is refused (413).
- MIDI: a flood of messages (fast fader) is coalesced to at most 30 updates per second; running
  status, note-on velocity 0 (= note-off), 14-bit CC pairs and relative encoders are parsed;
  SysEx and clock messages are ignored.
- MIDI: two devices with the same name: one profile serves both (documented).
- MIDI: speed/transpose bindings when 012 is not available on the track: the action is ignored and
  the binding row shows "not available here".
- MIDI on a page without a mixer: transport and strip targets do nothing; the status chip still
  shows the device.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each strip (standard, detail, and later strip kinds such as 015 takes) MUST offer a
  6-band parametric EQ, a compressor and a reverb send with the ranges of data-model 2.
- **FR-002**: Inserts MUST run pre-fader (EQ then compressor); the reverb send MUST be post-fader
  and post-gate, so mute/solo/hidden rules also apply to the reverb.
- **FR-003**: One reverb bus per mix with kind Room/Hall/Plate, return level 0..1 and pre-delay
  0..200 ms; its impulse responses MUST be generated deterministically (same output for the same
  settings on every run).
- **FR-004**: Parameter changes MUST be ramped (no clicks) and audible within 50 ms; switching an
  effect on/off or bypass MUST cross-fade within 10 ms.
- **FR-005**: Practice EQ Focus and Make room MUST exist for each of the seven instrument families
  with the curves of data-model 5; each strip's family MUST follow from its channel id.
- **FR-006**: Built-in presets (data-model 5) MUST be selectable per strip; a preset applies
  concrete values; the mix stores the values and the preset id; changed values show "modified".
- **FR-007**: Strip bypass (saved) and global bypass (not saved) MUST exist.
- **FR-008**: Effects MUST be saved in mixes as optional parts; a mix without them MUST load and
  sound exactly as before 1.16.0; duplicate copies them; the API validates ranges.
- **FR-009**: Exports "What I hear" and "Selected channels" from the mixer MUST include effects
  when the user keeps "Include effects" on, rendered with the same graph as playback (sample-exact
  before encoding). Raw exports never include effects. Exports without effects MUST say so in
  their contents when the mix had effects.
- **FR-010**: The effects CPU budget of SC-005 MUST hold; nodes exist only for active effects and
  are suspended on hidden strips.
- **FR-011**: The app MUST support Web MIDI input on Chromium browsers: request access only on a
  user action, without SysEx, detect support and secure context, and show the permission state.
- **FR-012**: Users MUST be able to bind controls to the targets of data-model 3.3 via MIDI learn
  in the mixer and edit/delete bindings on the Controllers page; learn infers the mode.
- **FR-013**: Bindings MUST support absolute 7-bit and 14-bit values, relative encoders (three
  common encodings), toggle, momentary and trigger buttons, invert and pick-up.
- **FR-014**: Controller actions MUST go through the same mixer operations as the on-screen
  controls (single source of state); continuous input is coalesced to <= 30 updates/s.
- **FR-015**: Profiles MUST be persisted in the backend per device (name + manufacturer) and
  re-activated automatically when the device connects.
- **FR-016**: Without Web MIDI, with a denied permission, insecure context or no device, the app
  MUST show a readable reason and remedy and otherwise work unchanged.
- **FR-017**: Visibility per level MUST follow the Clarifications (Beginner practice EQ only;
  Advanced all; Expert detail) and ui-test-ids 1.16.0 rules.
- **FR-018**: Every new screen area MUST have help topics (contracts/help-topics.md), all controls
  MUST be keyboard operable with accessible names and values, and the EQ curve MUST never be the
  only way to read a value.
- **FR-019**: Every new control MUST carry its ui-test-id (contracts/ui-test-ids.delta.md).
- **FR-020**: User and technical manuals MUST describe effects, practice EQ, export with effects,
  controllers, browser support and the CPU budget.

### Key Entities

- **Channel effects**: per strip - EQ, compressor, reverb send, bypass, preset id (data-model 2.1).
- **Reverb bus**: per mix - kind, return level, pre-delay (data-model 2.2).
- **Effect preset**: built-in, engine catalogue; families, values (data-model 5).
- **Pre-render**: a temporary 32-bit float WAV of an effects export, uploaded by the browser and
  consumed by one export (data-model 4).
- **Controller profile**: per device - name, manufacturer, enabled, bindings (data-model 3).
- **Binding**: control (kind, MIDI channel, number) -> target (kind, slot or command) with mode,
  invert, pick-up.

## Success Criteria *(mandatory)*

- **SC-001**: For each of the seven families, "Make room" lowers the family's test band of the
  other strips by >= 12 dB and "Focus" lowers the energy outside the family's range on its own
  strip by >= 12 dB (offline-render measurement, data-model 5.3).
- **SC-002**: 100 % of mixes saved before 1.16.0 load with effects off and render identical
  export bytes to 007 (regression test on stored mixes).
- **SC-003**: An effects export decodes to the same samples as an offline render of the same
  state (FLAC 24-bit: max difference <= 1 LSB).
- **SC-004**: A bound fader's change is audible within 50 ms (p95) of the MIDI message on the
  reference machine (measured in E2E via timestamps of the fake device and the engine call).
- **SC-005**: Worst-case mix (28 strips, full EQ with 24 dB slopes + compressor each, reverb Hall
  on): no audible dropouts on the reference machine during a 5-minute playback; offline render of
  30 s finishes in <= 10 s in headless Chromium on the CI runner.
- **SC-006**: A first-time Advanced user binds a fader and a play button in under one minute
  (usability check in the qa walkthrough).
- **SC-007**: In a browser without Web MIDI, 0 permission prompts and 0 console errors; the reason
  is shown on the Controllers page.
- **SC-008**: axe shows no serious or critical violations on the effects panel, the practice EQ
  menu, the export dialog and the Controllers page.

## Assumptions

- 012 adds speed (50-120 %), transpose (+-12) and count-in in engine 2.3.0 / backend 1.11.0;
  014 adds keyboard/pedal shortcuts and a command registry (engine 2.4.0 / backend 1.13.0); 015
  adds recorded takes as strips (engine 2.5.0); 016 uses engine 2.6.0. 017 is additive on top of
  them; names of 012/014 commands are aligned at merge (plan, Integration).
- Exports keep 007's rules for tempo: the pre-render uses the playback speed rules 012 defines for
  exports (default: original tempo and pitch).
- The reference machine and browser: Windows 11, Chrome or Edge current stable.
- No new third-party dependency is needed (built-in Web Audio and Web MIDI APIs).

## Out of Scope

Insert reverb per strip; master-bus effects; delay/chorus/distortion; user-saved presets;
automation of effect parameters over time; MIDI output (motor faders, LEDs, displays); MIDI clock
sync; SysEx; Mackie/HUI protocol emulation; MIDI on non-Chromium browsers as a supported target
(it may work where available, untested); effects in exports started without a browser.
