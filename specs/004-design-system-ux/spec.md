# Feature Specification: Design System and UX Foundation

**Feature Branch**: `004-design-system-ux`

**Created**: 2026-11-15

**Status**: Draft

**Input**: Owner request 2026-11-15 (verbatim, Dutch): "De ui moet echt de ui zijn van een moderne
web applicatie, moet onderscheidend zijn van alle andere soortgelijke oplossing en mag een eigen
radzen theme hebben die je moet ontwikkelen zodat het er allemaal heel smooth en visueel
aantrekkelijk uit ziet. De user journey in de UI moet zowel voor een nieuwe als gevorderde en
extreem ervaren gebruikers aansluiten. Ik wil in app help in de UI." Roadmap item 004
(`docs/ROADMAP.md`); research `docs/research/roadmap-003-011.md` section 2 (numbered "003" there).

In English: the UI must look like a modern web application, be distinctive from all similar
products, may have its own Radzen theme so everything looks smooth and visually attractive; the
user journey must fit new, advanced and extremely experienced users; the owner wants in-app help.

## Clarifications

### Session 2026-11-15

Answered autonomously by the architect (operating mode: fully autonomous); binding design
decisions are in `docs/decisions/ADR-013-design-system-and-ux-foundation.md`, the reasoning in
[research.md](research.md) and the visual specification in [design-spec.md](design-spec.md).

- Q: What does "own Radzen theme" mean, given that Radzen premium themes, swatches and Radzen Blazor
  Studio are paid (constitution IV)? → A: one free MIT base theme (`software`) loaded once, plus
  StemStudio's own token layer that redefines the Radzen CSS variables (`--rz-*`) and adds
  StemStudio tokens (`--ss-*`) for a dark and a light set. No premium theme, no swatch, no paid
  designer. If a Phase 1 spike shows that the variables cannot express the design, the fallback is
  compiling Radzen's MIT SCSS with our values (ADR-013 D1).
- Q: What makes the design "distinctive"? → A: the direction "Colour belongs to the music": the
  interface chrome is quiet graphite (dark) or cool paper (light) and hue is reserved for the six
  stems, each with a fixed "cable colour" used everywhere the stem appears (track list ribbon,
  channel strip label, meter, help illustrations). The signature moment is the separation
  progress, drawn as one grey strand splitting into the coloured stem strands. Typography is one
  variable family (Archivo) whose width axis is used deliberately: condensed on channel labels,
  normal for text, expanded for page titles. See design-spec.md section 1.
- Q: How are beginner, advanced and expert users served? → A: three experience levels (Beginner,
  Advanced, Expert) chosen on first run and switchable in one click from the top bar at any time.
  The level decides which controls are shown and which aids are on (progressive disclosure);
  **it never hides data, safety or legal information**, and the backend behaves identically for all
  levels. Beginner = guided journey rail, practice presets, inline hints, simplified controls;
  Advanced = the full 001-003 feature set (exactly today's controls); Expert = everything plus
  keyboard-first work (command palette, full shortcut map, numeric entry, technical job details,
  compact density).
- Q: Which level applies before the user has chosen one? → A: Beginner, with the welcome panel on
  the import page inviting a choice. The welcome panel is non-modal, so it never blocks the page.
- Q: Where are preferences stored? → A: in the browser (local storage), one versioned JSON record
  for the single local user: level, theme (system, light, dark), density, motion and help progress.
  No server endpoint: the backend API stays 1.3.0 (contracts/DELTA.md). Clearing the browser's site
  data resets them to the defaults, which is harmless (ADR-013 D4).
- Q: What does "in-app help" cover? → A: four layers on one content source: (1) a short tooltip on
  every help-marked control, (2) a contextual help panel on the right that opens the topic of the
  current screen or focused control (help button, `?` key or F1), (3) a searchable help library
  including a glossary (stem, detail stem, BPM, downbeat, pan, solo, loop, ...), and (4) guided
  tours of about one minute per main screen. Help content is Markdown with front matter, versioned
  with the code and checked by automated tests.
- Q: Are tours started automatically? → A: no tour starts by itself. A dismissible "Take the tour"
  offer appears once per tour version for the levels the tour targets; tours can always be
  started from the help panel.
- Q: How is "visually attractive and smooth" made measurable? → A: through the success criteria
  below: visual regression baselines approved by the owner, no theme flash on load, first-paint
  and layout-shift budgets, and reduced-motion support, next to the accessibility criteria.
- Q: Must existing automated tests keep working? → A: yes. Every `data-testid` of ui-test-ids
  1.2.0 keeps its name, element and meaning; at level Advanced every one of them is rendered under
  the same conditions as in 1.2.0. The existing end-to-end suite runs at level Advanced.
- Q: Which languages? → A: English UI and help content, like 001-003. Localisation is out of
  scope (content format allows adding a language later).
- Q: Is mobile in scope? → A: the app shell, import, track list, track detail, mix library and
  help must work from 360 px wide; the mixer must stay usable from 768 px (tablet) and is designed
  for desktop. Touch drag on faders is not optimised in this feature.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A modern, distinctive studio look on every screen (Priority: P1)

A musician opens StemStudio and finds a coherent, modern application with its own identity: a
calm app shell with clear navigation, the stems recognisable by colour everywhere, readable
typography, smooth and purposeful motion, and a light and a dark appearance. Every existing screen
(import, track list, track detail with detail stems, mixer, mix library, dialogs) uses the new
design, with designed empty, loading and error states.

**Why this priority**: the owner's first and most explicit request, and every later roadmap feature
builds its UI on this design system; doing it later means redoing all screens.

**Independent Test**: walk through import -> track detail -> mixer -> save -> mix library in both
themes and compare with the approved visual baselines; check that no existing automated test
breaks.

**Acceptance Scenarios**:

1. **Given** any screen, **When** it is shown in the dark or the light theme, **Then** it uses only
   the design tokens (colours, type, spacing, radius, motion) of the design system and matches its
   approved visual baseline.
2. **Given** a completed track, **When** the user looks at the track list, track detail and mixer,
   **Then** every stem has the same colour and name in all three places.
3. **Given** a track being separated, **When** the user watches the track page, **Then** the
   progress is shown as the signature "split" visual together with the numeric progress, and with
   reduced motion enabled the visual is static.
4. **Given** the app is loaded, **When** the first frame is painted, **Then** it already shows the
   chosen theme (no flash of the other theme).
5. **Given** the operating system prefers dark or light and the theme preference is "system",
   **When** the app loads, **Then** it follows the operating system and updates when the system
   setting changes.

---

### User Story 2 - A journey that fits beginners, advanced and expert users (Priority: P1)

A first-time user is welcomed, picks "I am new to this" and is guided along a visible journey
(import a song, wait for the separation, practise in the mixer, save the mix) with simplified
controls, practice presets ("play along on drums", "karaoke") and short explanations. An
experienced user picks Advanced and gets all controls exactly as today. A very experienced user
picks Expert and gets everything plus a denser layout and technical details. Anyone can switch
level at any time with one click, and the choice is remembered.

**Why this priority**: explicit owner request; it makes the product usable for new users without
slowing down experienced ones.

**Independent Test**: start with empty preferences, follow the beginner journey to a saved mix
using only what the screen offers, then switch to Advanced and Expert and verify the extra
controls; reload and verify the level is remembered.

**Acceptance Scenarios**:

1. **Given** no stored preferences, **When** the user opens the app, **Then** the import page shows
   a welcome panel with the three levels explained in one sentence each; the page stays fully
   usable while the panel is shown.
2. **Given** level Beginner, **When** the user is on import, track detail or mixer, **Then** a
   journey rail shows the four steps with the current step marked, and the next action is the
   primary button of the screen.
3. **Given** level Beginner in the mixer, **When** the user picks the preset "Play along: drums",
   **Then** drums are muted, the metronome is on, and the mix shows unsaved changes, exactly as
   if the user had done it by hand.
4. **Given** level Beginner, **When** the mixer is shown, **Then** pan, typed loop times, manual
   BPM, metronome offset, beats per bar and metronome reset are not shown, while every stem, its
   volume, mute, solo, the meters, transport, "Set A"/"Set B", loop on/off, metronome on/off and
   volume, save and the detail-stem groups are.
5. **Given** level Advanced, **When** any screen is shown, **Then** all controls of features
   001-003 are available exactly as before this feature.
6. **Given** any level, **When** the user switches level in the top bar, **Then** the current screen
   updates immediately without losing unsaved mixer changes or playback position, and the level is
   still active after a reload.
7. **Given** level Beginner and a control that is hidden at this level, **When** the user reads the
   help topic for it, **Then** the topic says at which level the control is available and offers a
   button to switch.

---

### User Story 3 - Help exactly where I need it (Priority: P1)

While working, the user can get help without leaving the screen: hovering or focusing a marked
control shows a one-line explanation; the help button (always in the same place), the `?` key or
F1 opens a help panel on the right with the topic for the current screen or focused control; a
search box finds topics and glossary terms; related topics are linked.

**Why this priority**: explicit owner request; it also carries the honesty principle (limitations of
separation are explained where they matter).

**Independent Test**: on every screen, open help through the button, the keyboard and a control's
help hint; search for "pan", "detail stems" and "BPM"; check that every help marker has a topic.

**Acceptance Scenarios**:

1. **Given** any screen, **When** the user presses the help button, `?` (outside text fields) or F1,
   **Then** the help panel opens with the topic of the focused control, or of the screen when no
   marked control is focused, and focus moves into the panel.
2. **Given** the help panel is open, **When** the user types "bpm" in the search box, **Then**
   matching topics are listed within 100 ms, ranked by title and keyword matches first.
3. **Given** a control with a help marker, **When** it is hovered or focused, **Then** a tooltip
   with at most 140 characters appears, and its "Learn more" link opens the full topic.
4. **Given** the help panel is open, **When** the user presses Escape or the close button, **Then**
   the panel closes and focus returns to where it was.
5. **Given** the help panel on a wide screen, **When** it is open, **Then** the page content stays
   visible and usable next to it (the panel does not cover the mixer transport).
6. **Given** the limitations of separation (stem limitation, detail limitation), **When** the user
   opens their help topics, **Then** the full explanation is shown, and the in-page limitation
   notes stay visible at every level.

---

### User Story 4 - Guided tours (Priority: P2)

A user who wants a quick orientation takes a guided tour of about one minute: the screen is dimmed
except for the highlighted part, a short text explains it, and the user steps forward, back or
closes the tour at any moment, with the keyboard or the mouse.

**Why this priority**: valuable for first use, but help panel and tooltips already cover the need.

**Independent Test**: run each tour from the help panel with keyboard only and with the mouse;
check that the offer for a completed tour does not return.

**Acceptance Scenarios**:

1. **Given** level Beginner on the import page for the first time, **When** the page is shown,
   **Then** a "Take the tour" offer is shown, and no tour starts without the user's action.
2. **Given** a running tour, **When** the user presses the arrow keys, Enter or Escape, **Then** the
   tour moves forward, back or closes, and focus is kept inside the tour popover.
3. **Given** a tour that was completed or dismissed, **When** the user returns, **Then** its offer is
   not shown again, unless the tour content got a new version.
4. **Given** a tour step whose target is not on the screen (for example a detail group that does
   not exist for this song), **When** the tour reaches it, **Then** the step is skipped.

---

### User Story 5 - Expert speed: keyboard first, dense and technical (Priority: P2)

An expert works mostly with the keyboard: a command palette (Ctrl+K) jumps to any track, mix,
screen, help topic or action; a shortcut map covers the mixer (play/pause, stop, loop, set A/B,
metronome, mute/solo per channel, select channel); a shortcut overview is one key away; values can
be typed exactly; technical job details (model, stage, device, timings) are visible; the layout is
compact.

**Why this priority**: important for heavy users, but builds on US1-US3.

**Independent Test**: complete import -> mixer -> loop -> save -> load from the mix library using
only the keyboard at level Expert.

**Acceptance Scenarios**:

1. **Given** any screen at any level, **When** the user presses Ctrl+K (Cmd+K on macOS), **Then** the
   command palette opens, filters as the user types, and Enter runs the selected command.
2. **Given** the mixer at any level, **When** the user presses a mixer shortcut outside text fields,
   **Then** the same action runs as with the mouse (space = play/pause stays unchanged).
3. **Given** level Expert, **When** the user presses Ctrl+/ , **Then** an overview of all shortcuts of
   the current screen is shown; at levels Beginner and Advanced the overview is reachable from the
   help panel.
4. **Given** level Expert in the mixer, **When** the user types a value in a channel's numeric volume
   or pan field, **Then** the fader follows exactly, and peak meters show their dB value as text.
5. **Given** level Expert, **When** a track page is shown, **Then** a "Job details" section lists the
   model, stage, device and fallback reason of the standard and detail jobs.

### Edge Cases

- Stored preferences are corrupt, from a newer schema, or larger than the limit: the app uses the
  defaults field by field and overwrites the record on the next change; it never fails to start.
- Local storage is unavailable (private mode, blocked): the app works with defaults in memory and
  says once, in the preferences panel, that choices will not be remembered.
- Two browser tabs are open: a level or theme change in one tab is applied in the other.
- The level is switched while a mixer has unsaved changes or is playing: nothing is lost; hidden
  controls keep their values (for example a pan set at Advanced stays applied at Beginner and is
  shown in the strip as a read-only "panned" marker).
- A tour or the help panel is open when the user navigates: the tour closes; the help panel stays
  open and switches to the new screen's topic.
- A help topic referenced by a control is missing in a build: the automated help check fails the
  build; at runtime the help panel shows the help library index instead of an error.
- Keyboard shortcuts never fire while typing in a text field, number field or dialog.
- High contrast / forced colours mode: controls stay visible and operable (system colours).
- Text zoom 200 % and 320 px reflow: no content or function is lost (WCAG 1.4.4, 1.4.10), the mixer
  scrolls horizontally inside its own region only.

## Requirements *(mandatory)*

### Functional Requirements

**Design system and theme**

- **FR-001**: The UI MUST use one StemStudio theme built on a free MIT Radzen base theme plus own
  design tokens; no premium Radzen theme, swatch or paid tool may be used.
- **FR-002**: The theme MUST provide a dark and a light appearance; the preference MUST offer
  System (default), Light and Dark, and System MUST follow the operating system live.
- **FR-003**: All colours, type sizes, spacing, radii, shadows and motion values in the UI MUST come
  from the documented design tokens (design-spec.md section 2); raw colour values outside the
  token files MUST be rejected by an automated check.
- **FR-004**: Each of the six standard stems MUST have one fixed identity colour per theme that is
  used for that stem (and its detail stems) everywhere it is shown; stem identity MUST never be
  conveyed by colour alone (name or label always present).
- **FR-005**: Every existing screen and dialog (import, track list, track detail including detail
  section, mixer with all its panels, mix library, save/rename and confirm dialogs) MUST be
  rebuilt in the design system, including designed empty, loading and error states.
- **FR-006**: An app shell MUST provide primary navigation (Import, Tracks, Mixes), the level
  switch, the help button and the preferences menu in the same place on every screen, plus a
  "skip to content" link.
- **FR-007**: The separation progress MUST be shown with the signature split visual next to the
  numeric progress; the visual MUST be decorative for assistive technology (the progress bar keeps
  its accessible value).
- **FR-008**: Motion MUST be limited to state changes the user caused or needs to notice (panel open,
  expand, save confirmation, progress), MUST use the motion tokens, and MUST be reduced to
  instant or opacity-only changes when the user or the operating system prefers reduced motion.
- **FR-009**: Fonts and icons MUST be self-hosted and open-licensed (no runtime requests to third
  parties); the chosen theme MUST be applied before the first paint.

**Experience levels and journey**

- **FR-010**: The system MUST support three experience levels, Beginner, Advanced and Expert, with
  Beginner applied until the user chooses.
- **FR-011**: A first-run welcome panel MUST explain the levels and let the user choose one, start
  the tour, or dismiss it; it MUST be non-modal and MUST not return after a choice or dismissal.
- **FR-012**: The level MUST be switchable from the app shell in one action on every screen, take
  effect immediately and keep unsaved work and playback untouched.
- **FR-013**: Which controls each level shows MUST be defined in one feature catalogue
  (design-spec.md section 5); levels MUST only change controls and aids, never hide data, legal
  notices, limitation notes, errors or the user's own settings.
- **FR-014**: At level Advanced every control of features 001-003 MUST be available with unchanged
  behaviour; Expert MUST be a superset of Advanced; Beginner MUST hide only the controls listed in
  the catalogue.
- **FR-015**: Level Beginner MUST show a journey rail (Import, Separate, Mix, Save) on the import,
  track detail and mixer screens with the current step marked.
- **FR-016**: The mixer MUST offer practice presets (play along drums/bass/guitar/piano, karaoke,
  focus vocals, reset) at every level; a preset only changes mute, solo, volume and metronome
  through the same actions as manual changes, only lists presets whose stems exist, and marks the
  mix as changed.
- **FR-017**: Level Expert MUST add: numeric entry for channel volume and pan, dB text on meters,
  a shortcut overview on Ctrl+/, a "Job details" section on the track page, and compact density by
  default.
- **FR-018**: A command palette (Ctrl+K / Cmd+K) MUST be available at every level, listing screens,
  tracks, mixes, help topics and the actions of the current screen.
- **FR-019**: Mixer keyboard shortcuts (design-spec.md section 6) MUST work at every level outside
  text fields and dialogs; the existing space = play/pause MUST keep working.
- **FR-020**: Preferences (level, theme, density, motion, help progress) MUST persist locally for
  the single user, survive reloads and restarts, synchronise across open tabs, and fall back to
  defaults when missing or invalid.

**In-app help**

- **FR-021**: Help content MUST be written as Markdown topics with front matter (id, title, area,
  summary <= 140 characters, levels, keywords, related topics, anchors, review version) and
  shipped with the application; rendering MUST not allow raw HTML or scripts.
- **FR-022**: Controls with a help marker MUST show the topic's summary as a tooltip on hover and
  keyboard focus, with a link to the full topic.
- **FR-023**: A help panel MUST open from the help button, the `?` key and F1, show the topic of the
  focused control or current screen, support Escape to close with focus return, and keep the page
  usable next to it on screens >= 1280 px wide.
- **FR-024**: The help panel MUST offer a search over titles, keywords, summaries and text, a topic
  index per area, a glossary, links between topics, the shortcut overview and the list of tours.
- **FR-025**: Help topics MUST cover every existing screen, every help-marked control, the glossary
  terms of design-spec.md section 7, the legal notice and both limitation explanations.
- **FR-026**: An automated check MUST fail the build when a help marker has no topic, a topic
  anchor is not present in the UI, a related link is broken, a topic is invalid or a tour step
  targets an unknown marker.
- **FR-027**: Guided tours MUST exist for the import page, the track page and the mixer (beginner
  and advanced variants) and one expert tour for shortcuts and the command palette; they MUST be
  offered, never auto-started, be operable by keyboard, skip missing targets and be recorded as
  completed or dismissed per tour version.
- **FR-028**: Later features MUST be able to add help topics, tours and feature-catalogue entries
  without changing the help framework (cross-cutting rule of the roadmap).

**Quality and compatibility**

- **FR-029**: Every `data-testid` of ui-test-ids 1.2.0 MUST keep its name, element and meaning;
  new ids are added in ui-test-ids 1.3.0 ([contracts/ui-test-ids.md](contracts/ui-test-ids.md)).
- **FR-030**: The UI MUST meet WCAG 2.2 level AA at every level in both themes, including focus
  visible and not obscured, 24 x 24 px minimum targets, non-drag alternatives for faders and
  sliders, consistent help location, and 3:1 contrast for control boundaries and meters.
- **FR-031**: No backend, worker or audio-engine contract changes are required (contracts/DELTA.md).

### Key Entities

- **User preferences**: level, theme, density, motion, help progress (welcome dismissed, tours
  completed/dismissed with version, dismissed hints); one record per browser, versioned schema.
- **Experience level**: Beginner, Advanced, Expert; ordered.
- **UI feature**: an identified control group with the minimum level at which it is shown and the
  help topic that explains it.
- **Help topic**: id, title, area, summary, body, levels, keywords, related topics, anchors, review
  version.
- **Tour**: id, version, route, target levels, ordered steps that point to help markers and topics.
- **Design token**: named value for colour, type, space, radius, elevation or motion, per theme.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Automated accessibility scans report 0 violations of WCAG 2.0/2.1/2.2 A and AA rules
  on every screen (import, track list, track detail with and without detail stems, mixer collapsed
  and expanded, mix library, help panel open, welcome panel, a tour step) at each of the 3 levels in
  both themes; a keyboard-only walkthrough of the main flow succeeds at every level.
- **SC-002**: Colour contrast of all text is >= 4.5:1 (>= 3:1 for text >= 24 px or 18.66 px bold), and
  of control boundaries, focus indicators, meters and stem identity marks >= 3:1, verified by a token
  contrast test in both themes.
- **SC-003**: On the reference machine against the local stack, each screen reaches first contentful
  paint <= 1.0 s and largest contentful paint <= 1.5 s (cold cache, desktop profile), cumulative
  layout shift <= 0.05, and the design system adds at most 200 KB (compressed) of CSS, fonts and
  scripts over 003.
- **SC-004**: The first painted frame uses the correct theme in 100 % of 20 consecutive reloads per
  theme (no flash of the other theme).
- **SC-005**: Visual regression: every screen x level x theme combination (at least 30 baselines,
  plus help panel, welcome panel, a tour step and 360 px views) matches its owner-approved baseline
  within 0.1 % of pixels; any intended change requires an updated, reviewed baseline.
- **SC-006**: 100 % of the 1.2.0 end-to-end tests pass unchanged in behaviour at level Advanced,
  and 100 % of the existing unit and component tests pass.
- **SC-007**: A first-time user at level Beginner can go from an empty app to a saved practice mix of
  the fixture song using only on-screen guidance, verified by an end-to-end test that uses only
  journey-rail, primary-button and preset interactions.
- **SC-008**: An expert completes import -> mixer -> loop -> save -> reload the mix with the keyboard
  only, in at most 25 key presses after the import URL is typed (end-to-end test).
- **SC-009**: 100 % of help markers have a topic, 100 % of topic anchors and tour targets exist in
  the UI, and help search returns the expected topic first for the 20 reference queries of
  quickstart.md V-H3.
- **SC-010**: Switching level or theme updates the visible screen within 200 ms without a reload and
  without changing mixer state or playback position.
- **SC-011**: Mixer meters keep updating at the display frame rate with all strips of a fully split
  song expanded (no long tasks > 50 ms caused by styles during playback, performance trace).

## Assumptions

- Single local user, one machine (spec 001); no accounts, so per-browser preferences are enough.
- Radzen.Blazor 13.1.0 (MIT) stays the component library; the free `software` base theme exposes
  enough CSS variables for the design (verified by the Phase 1 spike; otherwise ADR-013 D1 fallback).
- Fonts: Archivo (variable, SIL OFL-1.1) self-hosted; icons: Material Symbols Outlined (Apache-2.0)
  as shipped by Radzen. Tours: driver.js 1.14.0 (MIT). Markdown: Markdig 1.6.0 (BSD-2-Clause). Front
  matter: YamlDotNet (MIT, already used in tests). Accessibility scans: @axe-core/playwright
  (MPL-2.0, dev only). Exact versions and licences are re-verified when added (constitution IV).
- English only; content tone follows design-spec.md section 8 (plain, active, sentence case).
- The owner approves the visual direction on screenshots of the first re-skinned screens before the
  remaining screens are built (tasks.md checkpoint "design approval").
- Audio behaviour, the backend API (1.3.0), the worker API (1.2.0) and the audio-engine contract
  (2.0.0) are unchanged; the audio engine keeps writing `--meter-level`, `data-peak-db` and
  `data-clip`, which the new styles consume.
