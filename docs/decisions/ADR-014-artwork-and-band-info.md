# ADR-014: Song artwork and band info (feature 005)

- **Status**: Accepted (architect, 2026-11-15, 005 Phase 0 T004; contracts worker 1.3.0, backend 1.4.0, UI test ids 1.4.0 in `specs/005-artwork-band-info/contracts/`)
- **Date**: 2026-11-15
- **Decider**: Architect (autonomous mode), to be confirmed by the CTO
- **Feature**: `specs/005-artwork-band-info/`
- **Research**: `docs/research/roadmap-003-011.md` section 4, `specs/005-artwork-band-info/research.md`

## Context

The owner wants a picture of the imported song while mixing, the same picture as a thumbnail in
the track library and, if possible, information about the band. Covers come from the import
source (YouTube thumbnail, Spotify album image via spotDL, tags embedded in an uploaded file) or
from open data (Cover Art Archive). Band information comes from MusicBrainz (CC0), Wikidata (CC0)
and Wikipedia (CC BY-SA 4.0, attribution required). MusicBrainz and Wikimedia require a
User-Agent that identifies the client and gives a contact. StemStudio is local, single-user and
open source; the repository has no public URL yet and personal data must never be sent.

## Decisions

### D1 The worker is the only component that talks to third-party services

All outbound lookups (MusicBrainz, Cover Art Archive, Wikidata, Wikipedia; in 006 also LRCLIB)
run in the Python worker, which already talks to YouTube and Spotify. The backend talks only to
the worker. Reasons: one egress point with one User-Agent, one throttle per host and one place to
switch online lookups off; image decoding and re-encoding with Pillow (MIT-CMU, already in
`uv.lock` as a transitive dependency; the usual .NET choice ImageSharp is not OSI-licensed);
the backend stays free of new HTTP clients and native image libraries.

### D2 Two sources of artwork, one precedence rule

1. **Source artwork** is captured inside the existing standard job right after the download
   (YouTube: largest `info["thumbnails"]` entry; Spotify: spotDL `Song.cover_url`; upload:
   `tinytag` front cover). It is best effort: any failure is logged and never fails the import.
2. **Matched artwork** comes from the new *metadata job* (D3): Cover Art Archive
   `release-group/{mbid}/front-500` for the matched release group.

Precedence (first available wins): user-chosen image (P3) > Spotify album image > embedded
cover of an upload > Cover Art Archive front > YouTube thumbnail (centre-cropped to a square) >
no artwork (the UI draws a placeholder; nothing is stored). Spotify and embedded covers describe
exactly the imported release; Cover Art Archive beats a YouTube video frame.

### D3 A separate, lightweight metadata job lane in the worker

`POST /v1/metadata-jobs` starts a network-only job (no model, no GPU) that runs on its own
single-thread lane, so it never waits behind a 30-minute detail separation and never competes
for the GPU lease (ADR-012). The backend starts it as soon as the standard job reports title,
artist and duration (new `SourceMetadata.durationSeconds`, `isrc`, `album`), i.e. while the
separation is still running. Steps: MusicBrainz recording match -> release group (album, year)
-> Cover Art Archive front -> MusicBrainz artist (`inc=url-rels`) -> Wikidata entity (CC0
claims: genres P136, members P527, inception P571, dissolved P576, country P495, website P856)
-> Wikipedia REST page summary in the first configured language that has a sitelink.

### D4 Matching rules (precision over recall)

- Spotify ISRC first (the exact lookup `/ws/2/isrc/<code>`, amended 2026-11 from the search
  syntax `isrc:<code>`; same intent, exact), then `recording:"<title>" AND artist:"<artist>"` with
  `dur:[d-5000 TO d+5000]` when the duration is known.
- YouTube titles are cleaned first (yt-dlp `track`/`artist` fields when present, else
  "Artist - Title" split, bracketed suffixes such as "(Official Music Video)", "[HD]",
  "Remastered 2021", "Lyrics" removed).
- A candidate qualifies when the normalised title is equal and the artist similarity is >= 0.9
  (rapidfuzz, MIT, already locked). Among qualifying candidates the one with the closest
  duration wins; ties prefer an official album release group with the earliest date.
- No qualifying candidate -> status `not-found`; nothing is guessed. The user can correct title
  and artist and search again (P2).

### D5 Storage and serving

The worker re-encodes every image (decode with Pillow, reject > 10 MB input, > 40 MP or a format
other than JPEG/PNG/WebP, strip all metadata) into two square JPEGs: `cover` 600 x 600 (q 85,
<= 200 KB) and `thumb` 160 x 160 (q 80, <= 20 KB). The backend copies both into
`data/tracks/<trackId>/artwork/{cover,thumb}.jpg` (atomic write, like stems) and serves them at
`GET /api/tracks/{id}/artwork/{variant}` with a strong `ETag` (SHA-256) and, for URLs carrying the
`?v=<first 12 hex of the SHA-256>` that `TrackDto.artwork` hands out,
`Cache-Control: private, max-age=31536000, immutable`. Band info is stored once per MusicBrainz
artist (`Artists` table, refreshed after 30 days) and linked from the track.

### D6 Identification without personal data

User-Agent: `StemStudio/<version> ( <contact> )`. `<contact>` is `STEMSTUDIO_CONTACT_URL` when set
and valid (absolute `https` URL, <= 200 characters, no user-info, no `@` anywhere, not `mailto:`);
otherwise the generic token `local-practice-tool; contact not configured`. No personal data is
ever sent (no e-mail, user name, machine name or file path). Only the song title, artist, album,
duration and ISRC leave the machine. `STEMSTUDIO_ONLINE_LOOKUPS=off` disables every lookup
(source artwork from the import still works).

### D7 Throttling and failure

One process-wide rate limiter per host: MusicBrainz and Cover Art Archive 1 request/s, Wikimedia
1 request/s (well below the 200/min limit for identified clients). 503/429 honour `Retry-After`
(else 2 s, 4 s, 8 s with jitter; at most 3 retries), then the step fails with a readable message;
earlier results (e.g. the cover) are kept. Timeouts 10 s connect+read per request, 60 s per job.

### D8 Licences and attribution

- MusicBrainz core data and Wikidata: CC0, no attribution needed; we still credit them in the
  panel ("Data: MusicBrainz, Wikidata").
- Wikipedia summary: CC BY-SA 4.0; the panel shows the article title linked to the article,
  "Wikipedia contributors" linked to the history page, "CC BY-SA 4.0" linked to the licence, and
  "Excerpt, unmodified" (we show the API's `extract` as is, never edited).
- Cover images: copyrighted by their owners; shown in-app only, never exported or shared; the
  existing personal-practice notice is extended. Cover Art Archive states no image licence.
- Not used: TheAudioDB (no data or image licence), Last.fm (non-commercial only, images
  excluded, key required), artist photos from Wikimedia Commons (per-file licences and
  attribution; deferred), MusicBrainz tags/genres (supplementary data, CC BY-NC-SA), Spotify
  Web API (needs an app key).

## Consequences

- New worker packages `metadata/` (clients, matching, images) and job lane plumbing in `jobs/`;
  new runtime dependency `tinytag` 2.3.2 (MIT); Pillow becomes a direct dependency (MIT-CMU).
- Worker and backend contracts get additive minor versions (numbers assigned at feature start).
- Lookups need network access; CI uses fakes (`STEMSTUDIO_FAKE_ONLINE`) and recorded,
  self-authored fixtures (no third-party lyrics or images in the repository); real calls are
  `@pytest.mark.integration` and run manually.
- 006 (lyrics) reuses D1, D3, D6 and D7 (LRCLIB client in the same lane, same User-Agent).
