# Feature Specification: Own Lyrics to Song (Music + Voice)

**Feature Branch**: `010-lyrics-to-song`

**Created**: 2026-11-15

**Status**: Draft

**Input**: Owner request (Dutch, verbatim): "Ik wil eigen teksten kunnen uploaden en met AI deze
kunnen voorzien van muziek en voice." (English: "I want to upload my own lyrics and have AI add
music and a voice to them.") Later owner note: "ten aanzien van eigen zang ik denk dat als je kijkt
naar voice studio: https://github.com/debpalash/voicestudio je een heleboel kan gebruiken om zang
lijnen te genereren, dus je mag code gebruiken van deze open source oplossing" (English: "regarding
own singing, I think VoiceStudio can be used a lot to generate vocal lines, so you may use code
from this open-source solution"). Roadmap item 010, research `docs/research/roadmap-003-011.md`
sections 8-9, CTO brief of 2026-11-15.

## Clarifications

### Session 2026-11-15

Answered by the architect from the CTO brief and the feasibility spike (`research.md`,
`docs/decisions/ADR-019-lyrics-to-song-and-voice-conversion.md`).

- Q: Which model writes the music and sings? -> A: ACE-Step 1.5 (MIT code and weights), the same
  generative service as 009: lyrics with section markers plus a style produce a full song with an
  AI singing voice, 10 s to 8 minutes, 50+ vocal languages including Dutch and English.
- Q: Is VoiceStudio code used? -> A: no. VoiceStudio is AGPL-3.0 (copying code would make
  StemStudio AGPL) and its current code cannot sing (singing exists only in its design documents;
  research section 9). It is used as inspiration only: its own plan selected SoulX-Singer for
  singing, which StemStudio uses directly.
- Q: How does "my own voice" work? -> A: with SoulX-Singer-SVC (Apache-2.0 code and weights): the
  generated song is separated (normal 001 separation), its vocal stem is converted to the timbre
  of the user's own voice sample while keeping melody, timing and lyrics, and is mixed back over
  the instrumental. The user records or uploads a 10-60 s sample of their own singing and gives
  explicit consent.
- Q: Whose voices may be used? -> A: only the user's own voice (with consent) or the model's
  synthetic voices. Never another person's or a real artist's voice. StemStudio offers no way to
  use an imported song or a stem as a voice sample, and rejects uploads identical to library audio.
- Q: Singing synthesis from a score (SoulX-Singer SVS: lyrics + MIDI melody)? -> A: not in this
  feature. It needs an aligned score and a melody editor and supports only Mandarin, English and
  Cantonese; the SVC path already delivers "my voice". Recorded as backlog (research.md).
- Q: Is a GPU required? -> A: yes, exactly as 009 (FR-010 of 009): GPU-only, clear reason
  otherwise, the rest of the app unaffected.
- Q: Lyrics format? -> A: plain text up to 4,096 characters, typed or uploaded (`.txt`, `.lrc`
  with timestamps stripped, `.md`), optional section markers `[intro]`, `[verse]`, `[pre-chorus]`,
  `[chorus]`, `[bridge]`, `[outro]`, `[instrumental]`; a helper inserts markers. Lyrics are saved
  in a personal lyrics list for reuse.
- Q: How is AI output labelled? -> A: as in 009 (library badge, take details, file metadata), plus
  "AI voice" or "AI voice converted to <profile name>" on takes with vocals.
- Q: What does the style come from? -> A: the 009 style library (same presets and user styles),
  plus vocal options (lead voice: female, male, any; delivery mood) and optional BPM, key and
  duration. Without a style the user can type a short description, checked by the same rules.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Turn my lyrics into a song with an AI voice (Priority: P1)

The user pastes or uploads their own lyrics, chooses a style from the style library and a lead
voice, sets the length, and generates. The result is a complete song with music and a sung voice,
playable on the page and stored in the personal library, marked as AI-generated.

**Why this priority**: The owner's core request.

**Independent Test**: With the generative service on the GPU, generate 60 s from the sample lyrics
with the "Pop rock" preset; a playable take with vocals appears in the library with the
AI-generated and AI-voice labels. With fakes (CI) the same flow completes with a fake take.

**Acceptance Scenarios**:

1. **Given** lyrics, a style and a GPU-ready service, **When** the user generates, **Then** a song
   job is created, its progress is shown, and on completion a take of the requested length is
   playable and present in the library.
2. **Given** lyrics without section markers, **When** the user clicks "Add sections", **Then**
   markers are proposed (blank-line blocks become verses; a repeated block becomes the chorus) and
   can be edited before generating.
3. **Given** lyrics over 4,096 characters, an empty text, or an unsupported file, **When** the user
   submits, **Then** the request is refused with the limit or reason.
4. **Given** a finished take, **When** the user opens its details, **Then** lyrics, style snapshot,
   voice options, seed, model, device and creation time are shown.

---

### User Story 2 - Keep and reuse my lyrics (Priority: P2)

The user saves lyrics with a title and language in a personal lyrics list, edits them later and
starts new generations from them; each take links to the lyrics version it was made from.

**Why this priority**: Iterating on a song is natural, but a one-off generation works without it.

**Independent Test**: Save lyrics, generate, edit the lyrics, generate again; both takes show
their own lyrics text.

**Acceptance Scenarios**:

1. **Given** saved lyrics, **When** the user edits and saves them, **Then** earlier takes keep the
   snapshot of the text they were made with.
2. **Given** the lyrics list, **When** the user deletes an entry, **Then** it disappears; takes made
   from it keep their snapshot.

---

### User Story 3 - Sing it with my own voice (Priority: P2)

The user creates a voice profile by recording (or uploading) a short sample of their own singing
and confirming consent. For a finished song take the user chooses "Use my voice"; StemStudio
separates the take, converts its vocal to the profile's timbre and mixes it back. The new take is
labelled "AI voice converted to <profile>".

**Why this priority**: Explicitly mentioned by the owner, but it builds on US1 and on a consented
voice profile, so it comes second.

**Independent Test**: Create a profile from a 20 s own-voice fixture with consent; convert a
finished take; a new take with the converted vocal appears, of the same length, labelled with the
profile name. Without consent the profile cannot be created.

**Acceptance Scenarios**:

1. **Given** no voice profile, **When** the user records or uploads a sample, **Then** the profile
   is only saved after the user ticks the consent statement and types their name; the consent
   text version, the time and the sample's checksum are stored with the profile.
2. **Given** an upload that is identical to any track, stem, take or export in the library, or a
   sample shorter than 10 s or longer than 60 s, or without detectable singing, **When** the user
   saves, **Then** the sample is rejected with the reason.
3. **Given** a finished song take and a profile, **When** the user chooses "Use my voice", **Then**
   the take is imported and separated if it was not yet, its vocal is converted, mixed back over the
   instrumental, and stored as a new take linked to the original take and the profile.
4. **Given** a profile, **When** the user deletes it, **Then** the sample and all derived data are
   deleted; the user is asked whether takes converted with it are deleted too (default: keep, they
   stay labelled with the deleted profile).
5. **Given** a profile, **When** the user views it, **Then** the consent statement, the date and a
   playback of the sample are shown, and "delete" is available.

---

### User Story 4 - Use the song for practice (Priority: P2)

The user imports a song take (with AI or own voice) as a track, separates it and practises with it
in the mixer like any song.

**Why this priority**: Same value as 009 US4; reuses that flow.

**Independent Test**: Import a song take; the track is separated and mixable and carries the
AI-generated label and a link to its lyrics.

**Acceptance Scenarios**:

1. **Given** a finished take, **When** the user imports it, **Then** the behaviour of 009 US4
   applies (same track on repeat, AI-generated label, provenance link).

---

### User Story 5 - Instrumental backing from lyrics structure (Priority: P3)

The user generates the same song as an instrumental (no vocals) to sing over it themselves.

**Why this priority**: Useful for practice, small; the mixer can already mute the vocal stem of an
imported take.

**Independent Test**: Generate with "Instrumental" switched on; the take has no lead vocal (vocal
stem RMS below -35 dBFS after separation).

**Acceptance Scenarios**:

1. **Given** lyrics with sections, **When** the user generates with "Instrumental", **Then** the
   take follows the section structure and length without a sung voice.

### Edge Cases

- No GPU or no generative service: as 009 US6 (disabled with reason; rest of the app unaffected).
- The lyrics contain a real artist's name in a style description or ask to sound like a singer:
  the description is rejected (lyrics text itself is not filtered for names; it is the user's own
  text). Lyrics are not otherwise content-filtered; the user is responsible for them.
- Lyrics language differs from the selected language: generation proceeds; the help entry advises
  choosing the language of the lyrics.
- Requested length too short for the lyrics (more than about 4 words per second): a warning is
  shown before generating; the user may continue.
- Voice conversion when the take's separation is still running: the conversion waits and starts
  when the stems are ready.
- The take has (almost) no vocals (instrumental): "Use my voice" is not offered.
- The voice sample has heavy background music or noise: rejected when less than 50 % of it is
  voiced singing; the help entry explains how to record.
- The profile is deleted while a conversion is queued: the conversion is cancelled.
- The microphone is not available or permission is denied: recording is disabled with the reason;
  upload remains possible.
- Restart of the generative service during a job: as 009.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The user MUST be able to enter or upload lyrics (`.txt`, `.lrc`, `.md`, UTF-8, at most
  4,096 characters after stripping LRC timestamps) and generate a song with music and an AI singing
  voice from them, with a chosen style, lead voice (female, male, any), language, optional BPM
  (40-220), key and duration (10 s to 8 min; default 2 min) and 1 or 2 takes.
- **FR-002**: The system MUST support section markers (`[intro]`, `[verse]`, `[pre-chorus]`,
  `[chorus]`, `[bridge]`, `[outro]`, `[instrumental]`) and offer a helper that proposes markers.
- **FR-003**: The user MUST be able to save, list, edit and delete lyrics (title 1-80 characters,
  language); takes keep a snapshot of the lyrics they were made from.
- **FR-004**: The user MUST be able to generate an instrumental version (no vocals) from the same
  inputs.
- **FR-005**: The user MUST be able to create voice profiles from a recording in the app or an
  uploaded file (WAV, FLAC or MP3, 10-60 s) of their own singing.
- **FR-006**: A voice profile MUST NOT be saved without explicit consent: a ticked statement
  ("This is my own voice. I agree that StemStudio uses it on this computer to convert generated
  vocals into my voice.") and the typed name of the person. The consent text version, timestamp,
  name and the sample's SHA-256 MUST be stored with the profile and shown on it.
- **FR-007**: The system MUST NOT offer imported tracks, stems, takes or exports as a voice sample
  and MUST reject an uploaded sample whose audio content hash equals any audio stored in the
  library; it MUST reject samples with less than 50 % voiced frames.
- **FR-008**: The user MUST be able to convert the vocal of a finished song take to a voice profile
  ("Use my voice"): the take is separated (001 pipeline) if needed, the vocal stem is converted
  keeping melody, timing and lyrics, and the result is mixed back over the other stems into a new
  take linked to the source take and the profile.
- **FR-009**: Deleting a voice profile MUST delete its sample, derived features and cached files
  from disk; the user chooses whether takes converted with it are deleted too.
- **FR-010**: Style descriptions and free style texts MUST pass the 009 checks (no imitation
  phrases, no artist names from the library or deny-list). StemStudio MUST NOT offer any feature to
  imitate a real artist or another person's voice.
- **FR-011**: Every take MUST be labelled AI-generated (library badge, take details, file metadata
  `AI_GENERATED=true`, generator name and version) and, when it has vocals, "AI voice" or "AI voice
  converted to <profile name>" (metadata `AI_VOICE=synthetic|converted`).
- **FR-012**: Song and conversion jobs MUST follow the 009 job rules: states and progress,
  cancellation, GPU-only with reason, one GPU step at a time across services, free-memory check and
  wait, out-of-memory handling, model release when idle, readable failures, retry.
- **FR-013**: Every take MUST be stored in the personal export/asset library (007) and be
  importable as a track with the 009 FR-009 behaviour; tracks show a link to their lyrics.
- **FR-014**: The lyrics page, the generate dialog, the voice profile page and the take details MUST
  link to in-app help entries (004 framework): lyrics format and sections, generate options,
  voice profiles and consent, voice conversion limits, AI labels and personal-use notice.
- **FR-015**: Voice samples and profiles MUST stay on the local machine; no feature sends them
  elsewhere.
- **FR-016**: The UI MUST state that generated songs are for personal use, that the user is
  responsible for the lyrics, and that voices may only be the user's own.

### Key Entities

- **Lyrics sheet**: title, language, text (with section markers), timestamps.
- **Song generation job**: generation job of kind song (lyrics snapshot, style snapshot, voice
  options, duration, BPM, key, instrumental flag) with takes.
- **Voice profile**: display name, consent record (statement version, name, time), sample file
  and checksum, sample duration, voiced ratio.
- **Voice conversion job**: generation job of kind voice conversion (source take, profile) with one
  take.
- **Take**: as in 009, plus vocal label (none, synthetic, converted with profile).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On the reference machine, a 60 s song from lyrics completes in at most 90 s with warm
  models and at most 4 minutes including model loading (excluding the one-time download); a 3-minute
  song in at most 4 minutes warm.
- **SC-002**: Voice conversion of a 60 s take (after its separation) completes in at most 2 minutes
  on the reference machine.
- **SC-003**: In an owner listening check of 5 songs from own lyrics (English and Dutch), at least
  4 have intelligible lyrics in the right order for at least 80 % of the lines.
- **SC-004**: 100 % of attempts to save a voice profile without consent, or with a sample identical
  to library audio, are refused (automated tests).
- **SC-005**: 100 % of takes carry the AI-generated and vocal labels in library and file metadata.
- **SC-006**: After deleting a voice profile, no file derived from its sample remains on disk
  (automated test over the data directories).
- **SC-007**: With the default CPU stack, 100 % of existing automated tests pass unchanged.

## Assumptions

- Feature 009 is implemented first; 010 reuses its generative service, style library, job, take,
  library and import flows, GPU policy and checks.
- Features 003, 004 and 007 as in 009.
- The user's own voice sample is recorded through the browser microphone (feature uses the
  standard browser recording API) or uploaded.
- Conversion quality is best for clean, dry singing samples of 20-30 s; spoken samples work less
  well. These limits are stated in the help entry (constitution V).
- SoulX-Singer is trained on Mandarin, English and Cantonese; SVC is language-agnostic (it keeps
  the source's words), so Dutch songs can be converted, with quality not guaranteed.
- Model weights fall under ADR-009; ACE-Step 1.5 (MIT), SoulX-Singer (Apache-2.0) and Whisper-base
  (Apache-2.0, used by SoulX-Singer for content features) have permissive licences.
