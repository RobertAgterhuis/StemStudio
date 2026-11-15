# Feature Specification: Song Artwork and Band Info

**Feature Branch**: `005-artwork-band-info`

**Created**: 2026-11-15

**Status**: Draft (specified ahead of time; starts after 003 and 004, see `docs/ROADMAP.md`)

**Input**: Owner request (Dutch, verbatim): "Ik wil een afbeelding van het nummer dat wij hebben
geimporteerd tijdens mixen. Dit moet ook in de Track library als thumbnail voor het nummer staan.
Indien mogelijk iets van band info." (English: "I want a picture of the song we imported while
mixing. It must also be in the track library as a thumbnail for the song. If possible, some band
info.")

## Clarifications

### Session 2026-11-15

Answered autonomously by the architect (operating mode: fully autonomous), based on the desk
research in `docs/research/roadmap-003-011.md` section 4 and the feasibility checks in
[research.md](research.md); binding design in `docs/decisions/ADR-014-artwork-and-band-info.md`.

- Q: Which picture is "the picture of the song"? → A: the album cover when it can be found, else
  the source's own picture. Precedence: a picture the user chose (P3) > Spotify album image >
  cover embedded in an uploaded file > Cover Art Archive front cover of the matched album >
  YouTube video thumbnail cropped to a square > a generated placeholder (initials on a colour
  derived from the title; not stored).
- Q: Where is it shown? → A: mixer header (large, about 120 px), track detail page (large), track
  library (thumbnail, about 48 px per row). All in the 004 design system.
- Q: What is "band info"? → A: artist name and type (band or person), country, active years,
  genres, members (bands, up to 12), official website, a short Wikipedia excerpt with mandatory
  attribution, and the album and year of the matched recording. Data from MusicBrainz (CC0),
  Wikidata (CC0) and Wikipedia (CC BY-SA 4.0). No artist photos (per-file licences; backlog).
- Q: When does the lookup happen? → A: automatically during the import, as soon as title, artist
  and duration are known, in parallel with the separation; it never delays or fails the
  separation. Source artwork appears as soon as the download step has finished.
- Q: What if the match is wrong or missing? → A: nothing is guessed: only a confident match is
  shown (equal normalised title, artist similarity >= 0.9, closest duration). Otherwise "No band
  information found" with an option to correct title and artist and search again (P2).
- Q: What is sent to third parties, and how is the client identified? → A: only title, artist,
  album, duration and ISRC. The User-Agent is `StemStudio/<version> ( <contact> )` where the
  contact is the optional environment variable `STEMSTUDIO_CONTACT_URL` (a public https page, never
  an e-mail) or a generic token without personal data. `STEMSTUDIO_ONLINE_LOOKUPS=off` disables
  all lookups.
- Q: Are existing tracks (imported before 005) enriched? → A: yes, on demand: opening a track
  without metadata status starts the lookup once (FR-012); there is no bulk migration job.
- Q: Licence of images? → A: covers are copyrighted; they are shown in-app only for personal
  practice and never exported or shared; the personal-practice notice is extended accordingly.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See the song's cover while mixing and in the library (Priority: P1)

A musician imports a song. While stems are being separated, the song's cover appears on the
track page; in the mixer the cover is shown next to title and artist; in the track library every
song has a thumbnail so songs are recognised at a glance.

**Why this priority**: This is the owner's main request and works even without any online
lookup (source artwork comes with the import).

**Independent Test**: Import one YouTube link, one Spotify link and one MP3 with an embedded
cover (fake online lookups off); check the thumbnail in the library, the cover on the track page
and in the mixer, and the placeholder for a WAV without a cover.

**Acceptance Scenarios**:

1. **Given** a Spotify import, **When** the download step completes, **Then** the track page and
   the library row show the Spotify album image, and the mixer header shows it once opened.
2. **Given** a YouTube import with no album match, **When** the import completes, **Then** the
   video thumbnail cropped to a square is shown.
3. **Given** an uploaded MP3 or FLAC with an embedded front cover, **When** it is imported,
   **Then** that cover is shown; a WAV (no tags) shows the placeholder.
4. **Given** a track with a cover, **When** the library page with 100 tracks is opened, **Then**
   thumbnails load lazily and the page stays responsive (SC-003).
5. **Given** the artwork could not be obtained (network error, broken image), **When** the user
   looks at the track, **Then** the placeholder is shown and the import is not affected.

---

### User Story 2 - Read about the band (Priority: P2)

From the mixer or the track page the user opens a "Band info" panel and reads who the artist is:
band or solo artist, country, active years, genres, members, website, a short Wikipedia excerpt,
and the album and year of the song, with source credits.

**Why this priority**: Requested "if possible"; it enriches the practice context and the
metadata is reused by lyrics (006) and the agent (011).

**Independent Test**: Import the reference song "Hero of the Day" (Metallica) and the Spotify
link of "Monkeys Spinning Monkeys" (Kevin MacLeod); open the panel; compare with the expected
values in quickstart A4/A5.

**Acceptance Scenarios**:

1. **Given** a song whose recording is found in MusicBrainz, **When** the panel is opened,
   **Then** it shows the artist facts, the album and year, the Wikipedia excerpt with the
   attribution (title link, "Wikipedia contributors" history link, "CC BY-SA 4.0" licence link,
   "excerpt, unmodified") and "Data: MusicBrainz, Wikidata".
2. **Given** a better cover from the matched album (YouTube source), **When** the lookup
   completes, **Then** the Cover Art Archive cover replaces the video thumbnail everywhere.
3. **Given** no confident match, **When** the panel is opened, **Then** it says "No band
   information found" and offers "Correct title and artist".
4. **Given** the user corrects title and/or artist, **When** they search again, **Then** the
   lookup runs with the corrected values and the panel and cover update.
5. **Given** online lookups are disabled, **When** the panel is opened, **Then** it says that
   online lookups are switched off and how to switch them on (help topic).

---

### User Story 3 - Choose my own picture (Priority: P3)

The user replaces the automatic picture with an image file of their own (for example a photo of
their band for their own recording) or returns to the automatic picture.

**Why this priority**: Nice to have; covers own recordings and wrong matches.

**Independent Test**: Upload a PNG as the track's picture, reload the library, reset to
automatic.

**Acceptance Scenarios**:

1. **Given** a track, **When** the user uploads a JPEG/PNG/WebP image <= 10 MB, **Then** it is
   shown everywhere for that track, cropped to a square.
2. **Given** a user picture, **When** the user chooses "Use automatic picture", **Then** the
   automatic picture (precedence rule) returns.
3. **Given** an invalid file (not an image, too large, > 40 megapixels), **When** it is uploaded,
   **Then** a readable error is shown and the current picture stays.

### Edge Cases

- Several songs by the same artist: band info is fetched once per artist and reused (refreshed
  after 30 days).
- A YouTube upload whose title has no "Artist - Title" form and whose uploader is not the artist:
  usually no confident match -> source artwork only; the user can correct the names (US2-4).
- MusicBrainz busy (HTTP 503) or rate limited: retried with back-off; after 3 failures the
  status is `failed` with "Band information could not be retrieved; try again later." and a
  retry button; source artwork stays.
- Live versions and re-recordings of the same title: the closest duration wins; the album shown
  is that of the chosen recording.
- An artist that is a person (no members): the members row is hidden.
- No Wikipedia article in the preferred languages: the excerpt section is hidden; Wikidata facts
  are still shown.
- A Wikipedia extract that is empty or a disambiguation page (`type` not `standard`): hidden.
- An image that is huge, animated, CMYK or has an alpha channel: decoded, flattened on black (no
  alpha in JPEG), first frame only, re-encoded; decompression bombs are rejected.
- Deleting a track deletes its artwork files; artist records without tracks are deleted.
- The worker is unavailable: the status stays `pending` and is retried by the job sync; the
  mixer and library keep working with the placeholder.
- Tracks imported before 005: lookup on first open (FR-012); no artwork from the original
  source exists for them (the source info was not kept), so only matched artwork can appear.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST capture a picture from the import source when one is available:
  the album image of a Spotify track, the video thumbnail of a YouTube video, the embedded front
  cover of an uploaded MP3 or FLAC.
- **FR-002**: Capturing or processing a picture MUST never fail or delay the import or the
  separation; failures are logged and result in no picture.
- **FR-003**: The system MUST look up the recording, its album, the album's front cover and the
  artist in open data sources automatically after import, using only title, artist, album,
  duration and ISRC.
- **FR-004**: Only a confident match MUST be used (equal normalised title, artist similarity
  >= 0.9, closest duration within 5 s when the duration is known); otherwise the result is "not
  found".
- **FR-005**: The picture shown MUST follow the precedence: user picture > Spotify album image >
  embedded cover > open-data album cover > video thumbnail > placeholder.
- **FR-006**: The picture MUST be shown in the mixer header, on the track detail page and as a
  thumbnail in each track library row; tracks without a picture show a placeholder.
- **FR-007**: Pictures MUST be stored locally per track in two square sizes (cover and thumbnail),
  re-encoded without any embedded metadata, and served with long-lived cache headers that change
  when the picture changes.
- **FR-008**: The band info panel MUST show the available facts (name, type, country, active
  years, genres, members, website), the album and year, and a Wikipedia excerpt with the
  attribution required by CC BY-SA 4.0 (title link, authors link, licence link, unmodified
  excerpt); source credits for MusicBrainz and Wikidata.
- **FR-009**: The lookup status MUST be visible (pending, searching, found, not found, failed,
  disabled) with a readable reason, and failed or not-found lookups MUST be retryable; the user
  MUST be able to correct title and artist for the lookup (P2).
- **FR-010**: Requests to third-party services MUST identify the client with a User-Agent that
  contains no personal data; the contact part MUST be configurable through
  `STEMSTUDIO_CONTACT_URL` (validated https URL) with a generic default.
- **FR-011**: The system MUST respect the services' rate limits (1 request per second for
  MusicBrainz and Cover Art Archive, at most 1 per second to Wikimedia, `Retry-After` honoured)
  and MUST offer a switch that disables all online lookups.
- **FR-012**: Tracks imported before this feature MUST get a lookup the first time they are
  opened.
- **FR-013**: The user MUST be able to set their own picture for a track and to return to the
  automatic picture (P3).
- **FR-014**: Pictures and band texts MUST be shown in-app only; they MUST NOT be included in any
  export, and the personal-practice notice MUST mention pictures and band information.
- **FR-015**: The new UI MUST follow the 004 design system and journey levels (beginner: picture
  and short band summary; advanced/expert: all facts, sources and the correction dialog) and MUST
  provide in-app help topics for artwork, band info and online lookups.
- **FR-016**: Deleting a track MUST delete its pictures; artist records no longer used MUST be
  deleted.

### Key Entities

- **Track artwork**: source (user, spotify, embedded, coverartarchive, youtube), content hash,
  cover and thumbnail files, time stamp.
- **Track metadata match**: lookup status and reason, matched recording, album (title, year),
  artist reference, confidence, the names used for the lookup (incl. user corrections).
- **Artist info**: MusicBrainz artist id, name, type, country, active years, genres, members,
  website, Wikipedia excerpt with attribution data, fetched-at time.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For Spotify imports and uploads with an embedded cover, 100 % of tracks show a
  picture as soon as the download step is done; for YouTube imports 100 % show at least the video
  thumbnail (fake and reference runs).
- **SC-002**: For the two reference songs, the band info panel shows the correct artist and album
  (Metallica / Load (1996); Kevin MacLeod) within 30 seconds after the download step finished.
- **SC-003**: The track library with 100 tracks renders its first screen in under 1 second on the
  reference machine, with at most 20 KB per thumbnail.
- **SC-004**: Zero wrong band matches on the matching test set (research.md, at least 20 cases
  including live versions, covers and ambiguous titles): when unsure, "not found" is shown.
- **SC-005**: 100 % of outgoing requests in the tests carry the StemStudio User-Agent and none
  contains personal data (checked by a test over all recorded fake requests).
- **SC-006**: The import and separation duration is unchanged (within 5 %) with online lookups
  on versus off.

## Assumptions

- Features 003 (GPU device policy) and 004 (design system, journey levels, in-app help) are
  merged before 005 starts; 005 adds help topics and screen parts in 004's conventions without
  depending on details not yet specified.
- The reference machine has internet access; StemStudio remains fully usable offline (pictures
  from uploads still work, everything else shows the placeholder or "lookups off").
- MusicBrainz, Cover Art Archive, Wikidata and Wikipedia remain free and keyless; their terms are
  recorded in research.md and ADR-014.
- The owner may later publish a project URL for `STEMSTUDIO_CONTACT_URL`; until then the generic
  User-Agent is used (MusicBrainz accepted it in the feasibility check, with occasional 503s).
- The UI language is English (as in 001-003); the Wikipedia language order is configurable
  (`STEMSTUDIO_WIKIPEDIA_LANGUAGES`, default `en`).
