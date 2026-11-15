# Feature Specification: Synced Lyrics with Karaoke Ball

**Feature Branch**: `006-synced-lyrics-karaoke`

**Created**: 2026-11-15

**Status**: Draft (specified ahead of time; starts after 005, see `docs/ROADMAP.md`)

**Input**: Owner request (Dutch, verbatim): "Bij het importeren van een Song kunnen wij indien de
Lyrics online beschikbaar zijn deze ook downloaden en deze net als Spotify laten meelopen met de
stem, doe dit middels een Karaoke balletje zodat de gebruiker weet hoe hij moet mee zingen. Dit
wil ik aan de onderkant van het scherm laten meelopen als een soort banner." (English: "When
importing a song, if the lyrics are available online, we can download them too and let them run
along with the voice like Spotify does; do this with a karaoke ball so the user knows how to sing
along. I want this to run along at the bottom of the screen as a kind of banner.")

## Clarifications

### Session 2026-11-15

Answered autonomously by the architect (operating mode: fully autonomous), based on
`docs/research/roadmap-003-011.md` section 5 and the feasibility checks in
[research.md](research.md); binding design in `docs/decisions/ADR-015-synced-lyrics-karaoke.md`.

- Q: Where do lyrics come from? → A: LRCLIB (free, no key) is searched automatically during the
  import with title, artist, album and the song's duration (it must match within 2 seconds).
  Other lyrics sites are not used (terms of service, scraping, paid tiers).
- Q: How does the ball know when each word is sung? → A: when LRCLIB has word timings they are
  used; when it has only line timings the words of a line are spread over the line in proportion
  to their syllables; when only the text is available, or the user pastes the text, the text is
  aligned automatically to the separated vocal track (speech-recognition alignment on the GPU
  when available). Each word knows where its timing came from and the banner says so.
- Q: Does automatic alignment run by itself? → A: yes when the worker runs on the GPU (about a
  minute); on a CPU-only worker the user starts it with one click because it takes several
  minutes; the expected duration is shown.
- Q: What does the banner look like? → A: a band at the bottom of the mixer with the current
  line large and centred, the next line smaller below it and the previous line faded above; sung
  words change colour; a ball jumps from word to word and lands on each word when it should be
  sung. Before the first line and in long instrumental parts (> 5 s) a countdown of dots is shown.
- Q: How smooth and how accurate? → A: the ball moves every screen frame (target 60 frames per
  second) and follows the actual playback position of the audio, including seeking, pause and
  A-B loops, without drifting. A per-song offset slider (-5 to +5 s) corrects lyrics that are
  early or late.
- Q: Can the banner be hidden? → A: yes, with a toggle in the transport area, remembered per
  user; with "reduce motion" switched on in the operating system the ball does not jump and the
  current word is highlighted instead.
- Q: Copyright? → A: lyrics are copyrighted. StemStudio shows them only inside the app for the
  user's own imported song, for personal practice; they are stored locally, never exported,
  shared or logged; the source is credited. The personal-practice notice mentions lyrics.
- Q: Old tracks (imported before 006)? → A: the lookup runs the first time the mixer of such a
  track is opened.
- Q: Language? → A: any language LRCLIB has; automatic alignment supports the languages of the
  speech model (incl. English and Dutch); the language is taken from LRCLIB or detected.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sing along with downloaded synced lyrics (Priority: P1)

A musician imports a well-known song. When the mixer opens, a banner at the bottom shows the
lyrics in time with the music, and a bouncing ball shows exactly which word to sing next. Muting
the vocal stem and singing along works like karaoke.

**Why this priority**: This is the owner's request; most popular songs have synced lyrics on
LRCLIB (the reference song even has word timings).

**Independent Test**: Import the reference song "Hero of the Day"; open the mixer, play, mute
vocals; the ball lands on each word when it is sung (spot-check 10 words), and seek, pause and an
A-B loop keep the ball in time.

**Acceptance Scenarios**:

1. **Given** a song with word-synced lyrics online, **When** it is imported and the mixer is
   opened, **Then** the banner shows the current line, the next line and the ball, and the ball
   lands on each word at its start time (SC-002).
2. **Given** a song with only line-synced lyrics online, **When** it plays, **Then** lines change
   at their time and the ball moves over the words of the line with estimated word timing; the
   banner says "word timing estimated".
3. **Given** playback is paused, seeked or looping, **When** the position changes, **Then** the
   banner shows the line and word of the new position within one screen frame.
4. **Given** the lyrics are slightly early or late, **When** the user moves the offset slider,
   **Then** the ball shifts immediately and the offset is kept for the song.
5. **Given** an instrumental song (LRCLIB marks it instrumental, e.g. "Monkeys Spinning
   Monkeys"), **When** the mixer is opened, **Then** no banner is shown and the track page says
   "Instrumental - no lyrics".
6. **Given** a song with a long instrumental part, **When** the gap is longer than 5 s, **Then**
   the banner shows "instrumental" and a countdown before the next line.

---

### User Story 2 - Lyrics without timing are aligned automatically (Priority: P2)

When only plain lyrics are online, or when the user pastes the lyrics of a song that has none
online (for example their own song), StemStudio aligns the words to the separated vocals so the
ball can follow them.

**Why this priority**: Covers songs without synced lyrics and own songs; needs a speech model.

**Independent Test**: Paste the lyrics of a short test song with clear vocals; alignment
completes (GPU: within 2 minutes for a 4-minute song); the ball follows the vocals within the
SC-003 tolerance.

**Acceptance Scenarios**:

1. **Given** plain lyrics only and a GPU worker, **When** the standard stems are ready, **Then**
   alignment starts automatically and the banner appears when it completes; it is marked
   "timing: automatic alignment".
2. **Given** a CPU-only worker, **When** plain lyrics are found, **Then** the track page offers
   "Align lyrics" with the expected duration; alignment starts only when clicked.
3. **Given** no lyrics online, **When** the user pastes the text and confirms, **Then** the text
   is stored and aligned; the source is shown as "entered by you".
4. **Given** detail separation has produced a lead vocal stem, **When** alignment runs, **Then**
   it uses the lead vocal instead of the full vocal stem.
5. **Given** alignment fails or the model cannot be downloaded, **When** the user looks at the
   track, **Then** a readable reason and "Try again" are shown; plain lyrics can still be read
   without the ball (static view).

---

### User Story 3 - Control the banner (Priority: P3)

The user can hide or show the banner, choose a text size, and correct the lyrics source (search
again, replace with pasted text, remove lyrics for this song).

**Why this priority**: Comfort and corrections.

**Independent Test**: Toggle the banner, change the text size, remove the lyrics and search
again.

**Acceptance Scenarios**:

1. **Given** the mixer, **When** the user hides the banner, **Then** it disappears, the mixer uses
   the space, and the choice is remembered.
2. **Given** wrong lyrics were found, **When** the user removes them or pastes others, **Then**
   the banner updates and the removal is remembered (no automatic lookup again).
3. **Given** reduce-motion is on, **When** the song plays, **Then** the ball does not jump; the
   current word is highlighted.

### Edge Cases

- The imported version's duration differs from all online records by more than 2 s (e.g. a
  music video with an intro): their timings are not used; plain text from the closest record is
  aligned (US2) or, on a CPU worker, offered for alignment.
- LRC with several timestamps per line, metadata tags (`[ar:]`, `[offset:]`), empty lines (=
  instrumental breaks), non-Latin scripts, right-to-left text: parsed and rendered correctly;
  `[offset:]` is applied.
- Very long lines (> 80 characters): wrapped to two lines in the banner; the ball follows the
  wrapped word positions.
- The vocal stem is (almost) silent (002 says "skipped: silent"): alignment is not offered;
  reason shown.
- The user seeks while the ball is in mid-air: the ball is placed at the new position at once (no
  animation across the seek).
- A-B loop shorter than one line: the ball repeats within the loop.
- Browser tab hidden: the animation stops and resumes on return at the right position.
- Lyrics of a deleted track are deleted with it.
- LRCLIB unreachable or rate limited: retried with back-off; status `failed` with "Lyrics could
  not be retrieved; try again later." and "Search again".
- Online lookups switched off (005 setting): no LRCLIB request; pasting lyrics and alignment
  still work.
- The speech model is not yet downloaded (1.6 GB): the first alignment shows the download size
  and progress; offline -> readable error, retry later.
- A song longer than the alignment limit (15 minutes, = the import limit): alignment runs in
  windows; no extra limit for the user.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: During import the system MUST search LRCLIB for the song's lyrics with title,
  artist, album (when known) and duration (match within 2 s), without delaying or failing the
  import or the separation.
- **FR-002**: The system MUST store per track the lyrics text, the timing per line and per word,
  the timing origin per word (from the source, estimated, aligned), the language, the source and
  the status (none, searching, found, instrumental, not found, aligning, failed, disabled,
  removed).
- **FR-003**: When word timings are available they MUST be used; when only line timings are
  available the words MUST be spread over each line in proportion to estimated syllables.
- **FR-004**: When only plain text is available, or the user pastes text, the system MUST align
  the words to the track's vocal stem (lead vocal when available) automatically on a GPU worker,
  and on user request on a CPU worker, showing progress and the expected duration.
- **FR-005**: The mixer MUST show a banner at the bottom of the screen with the previous, current
  and next line, sung/unsung word states and a ball that lands on each word at its start time.
- **FR-006**: The ball and line changes MUST follow the actual audio playback position, including
  play, pause, stop, seek and A-B loops, updated every screen frame, without drift over the whole
  song.
- **FR-007**: The user MUST be able to adjust a per-track lyrics offset (-5000..+5000 ms, 10 ms
  steps) that applies immediately and is stored.
- **FR-008**: The banner MUST show a countdown before the first line and in instrumental gaps
  longer than 5 s; instrumental songs show no banner.
- **FR-009**: The user MUST be able to hide/show the banner, choose a text size, search again,
  paste replacement lyrics and remove lyrics for a track.
- **FR-010**: Lyrics MUST be shown in the app only; they MUST NOT be exported, included in mixes,
  written to logs or shared; the source MUST be credited in the banner; the personal-practice
  notice MUST mention lyrics.
- **FR-011**: With reduced motion requested by the operating system the ball MUST NOT bounce; the
  current word is highlighted instead. The banner MUST be readable in both 004 themes (contrast
  >= 4.5:1).
- **FR-012**: Lookups MUST respect LRCLIB's rate limits, identify the client like 005 (no personal
  data) and obey the 005 switch that disables online lookups.
- **FR-013**: Tracks imported before 006 MUST get a lookup the first time their mixer is opened.
- **FR-014**: Alignment MUST follow the 003 device policy (GPU when usable, one GPU job at a time,
  CPU fallback on out-of-memory) and MUST NOT delay a waiting standard separation.
- **FR-015**: The new UI MUST follow the 004 design system and journey levels (beginner: banner
  and toggle; advanced: offset and text size; expert: timing origin, alignment controls, source
  details) and MUST provide in-app help topics for the karaoke banner, lyrics sources, alignment
  and the offset.

### Key Entities

- **Track lyrics**: status and reason, source (LRCLIB record id or "user"), language, sync level
  (word, line, plain), offset, lines (start, end, text) and words (start, end, text, origin),
  credit text, timestamps.
- **Lyrics job** (worker): lookup or alignment, progress, device, result.
- **Banner preferences** (per user, in the browser): visible, text size.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For the reference song, word-synced lyrics are found and shown within 10 seconds
  after the download step finished.
- **SC-002**: With word-synced lyrics, the ball lands on each word within 50 ms of its timing in
  the lyrics (measured on the audio clock in automated tests over the whole song, and by
  spot-checks on the reference machine), and stays within that tolerance after 50 loop
  repetitions and 20 seeks.
- **SC-003**: Automatic alignment places >= 80 % of word starts within 300 ms of a manual
  reference annotation on the alignment test clip (self-made, research.md), and the banner stays
  usable (no word more than 1 s off) for the reference song.
- **SC-004**: The banner animation runs at >= 55 frames per second on the reference machine during
  playback with all meters visible, and adds < 2 ms per frame of main-thread work.
- **SC-005**: On the GPU, alignment of a 4-minute song completes within 2 minutes including model
  load (first download excluded); on the CPU the shown estimate is within 50 % of the measured
  time.
- **SC-006**: Zero lyrics text appears in logs, exports or API list responses (automated test).

## Assumptions

- Features 003 (GPU device policy), 004 (design system, journey levels, in-app help) and 005
  (online lookup lane, User-Agent, cleaned title/artist, `STEMSTUDIO_ONLINE_LOOKUPS`) are merged
  before 006 starts.
- LRCLIB remains free and keyless; it states no licence for its data, so lyrics are treated as
  copyrighted works shown only for personal practice.
- The speech model (Whisper large-v3-turbo, MIT) is downloaded once at first use (about 1.6 GB)
  into the model cache; VRAM need is measured in Phase 0 (estimate about 6 GB).
- The karaoke ball is a visual aid: the constitution's rule that timers must not determine
  audible timing is honoured anyway because the ball follows the audio clock.
