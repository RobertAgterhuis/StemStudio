# Research: Song Artwork and Band Info

Decisions and feasibility checks for feature 005 (architect, 2026-11-15). Earlier desk research:
`docs/research/roadmap-003-011.md` section 4. Binding design: ADR-014.

## Feasibility checks (2026-11-15)

Small, read-only HTTP requests from the reference machine with the generic User-Agent
`StemStudio/0.1 ( local-practice-tool; contact-not-configured )` (no personal data; the same
default the feature will use when `STEMSTUDIO_CONTACT_URL` is not set). About 15 requests in
total, spaced >= 1.2 s for MusicBrainz. Responses were kept in the session scratchpad only;
nothing was committed.

### F-01 MusicBrainz recording search

| Query | Result |
|---|---|
| `recording:"Hero of the Day" AND artist:"Metallica"` (limit 10) | 200 in 0.28 s; 37 hits, **all with score 100**; the top 10 were live/bootleg/S&M/remaster versions (258-330 s) and **did not contain the 4:22 studio version**. Score alone is useless for selection. |
| same + `AND dur:[257000 TO 267000] AND status:official` | first attempt **503** ("server is currently busy"), retry after 3 s: 200, 4 hits: *Cunning Stunts* live 258.1 s (score 100), **`c8361f8a-ece3-4ad0-a5a6-4bb6d06083b9` 261.9 s, release group `e389b7df-862d-3d91-a612-acca150f6e71` "Load"** (score 99, ISRCs incl. `USEVI9692306`), and two "Mouldy (Hero of the Day take ...)" demos (score 85/79). |
| `recording:"Monkeys Spinning Monkeys" AND artist:"Kevin MacLeod"` | first attempt **503**, retry 200: 2 hits (125.0 s, 125.0 s), artist `8292bf44-2dd1-4bad-8019-276ec38f28e7`, ISRC `USUAN1400011`; one recording has no release, the other release group "25 Years (Volume 2)" `057247bb-7a93-4654-9a7d-4cb9abaf5c5c` (2024). |
| release group `e389b7df...` | "Load", primary type Album, `first-release-date` 1996-05-04 |
| Response headers | `X-RateLimit-Limit: 400`, `X-RateLimit-Remaining`, `X-RateLimit-Reset` (global); no `Retry-After` on the 503s seen |

Conclusions: (1) duration filter + exact normalised title + closest duration is required (D4 of
ADR-014; the closest-duration rule picks Load, not Cunning Stunts); (2) 503s happen even at
~1 req/s with the generic User-Agent, so retry with back-off is mandatory and a configured
contact URL is recommended (owner question O-1); (3) ISRC lookup is exact for Spotify imports
(spotDL provides `Song.isrc`).

### F-02 MusicBrainz artist with URL relations

`/ws/2/artist/{mbid}?inc=url-rels&fmt=json`: 200. Metallica: type Group, country US, begin
1981-10-28, not ended, relations `official homepage` https://www.metallica.com/ and `wikidata`
Q15920. Kevin MacLeod: type Person, US, begin 1972-09-28, homepages incompetech.com and
freepd.com, wikidata Q16731782.

### F-03 Cover Art Archive

| Request | Result |
|---|---|
| `release-group/e389b7df.../front-500` | **307** -> `archive.org/download/mbid-c488e108-.../...-5049025343_thumb500.jpg`; followed: 200 `image/jpeg`, 81,471 bytes, 500 x 499, 2.0 s |
| `release-group/057247bb.../front-500` (Kevin MacLeod) | 200 `image/jpeg`, 65,372 bytes, 500 x 500 |
| `release-group/e389b7df...` (JSON) | 200; images with `front`, `types`, `approved`, thumbnails `250/500/1200/small/large` |

Redirect hosts to allow-list: `coverartarchive.org` -> `archive.org` and `*.archive.org`
(`ia*.us.archive.org`). Images are not exactly square (500 x 499): centre-crop to square.

### F-04 Wikidata

`Special:EntityData/Q15920.json`: 200, **168 KB**; Q16731782: 91 KB. Present: sitelinks
`enwiki`/`nlwiki` ("Metallica", "Kevin MacLeod"), P571 inception `+1981-10-28`, P136 genres (2),
P527 has-part members (11), P856 website, P18 image (a Commons file; not used). Decision: use
`w/api.php?action=wbgetentities&ids=<Q>&props=sitelinks|claims&sitefilter=<wikis>&format=json`
(200, 138 KB) and a second `wbgetentities&props=labels&languages=en` call for the labels of
genres and members (<= 50 ids per call).

### F-05 Wikipedia REST page summary

`/api/rest_v1/page/summary/Metallica` (en): 200, 3.0 KB, `type: standard`, extract 720 chars,
`content_urls.desktop.page`, `revision` id, thumbnail (Commons; not used). nl: 200, extract 273
chars. Kevin MacLeod (en): 200, 485 chars. `cache-control: s-maxage=1209600, max-age=300`; no
deprecation header. The response has **no licence field**: the attribution text is ours
(CC BY-SA 4.0 for Wikipedia text, linked) per the content reuse guideline.

### F-06 Libraries

| Package | Version | Licence | Status in `worker/uv.lock` |
|---|---|---|---|
| tinytag | 2.3.2 | MIT (OSI classifier) | new; no dependencies |
| Pillow | 13.0.0 | MIT-CMU (OSI) | already locked (via spotapi) -> becomes direct |
| rapidfuzz | 3.14.6 | MIT | already locked (via spotdl) -> becomes direct |
| httpx | 0.28.1 | BSD-3-Clause | locked as a dev dependency only -> moves to the runtime dependencies for the online clients (no new package in the lock) |

`uv pip compile` of `torch==2.15.0 + tinytag==2.3.2` for Linux x86_64 resolved without conflicts.

## Decisions

### R-01 Egress in the worker, not in the backend

- **Decision**: the worker runs every online lookup (ADR-014 D1).
- **Rationale**: one egress point/User-Agent/throttle; Pillow for safe re-encoding; the backend
  stays the orchestrator and owner of persistence, like for separation.
- **Alternatives**: backend `HttpClient` + resilience pipeline (needs a .NET image library;
  ImageSharp is not OSI, SkiaSharp adds a native dependency), or the browser fetching covers
  directly (leaks requests from the UI, no caching, CSP issues). Rejected.

### R-02 Source artwork per source

| Source | Where | Choice |
|---|---|---|
| YouTube | `YtDlpFetcher` already has `info` | largest entry of `info["thumbnails"]` with `width >= 300` from `i.ytimg.com` (prefer `maxresdefault`, then `sddefault`, `hqdefault`); centre-crop to square. No second yt-dlp call (`--write-thumbnail` would re-request). |
| Spotify | `SpotdlMetadataClient` (spotDL `Song`) | `Song.cover_url` (`i.scdn.co`, 640 px album image); also `isrc`, `album_name`, `year` for matching |
| Upload | `FileUploadIntake` (MP3/FLAC only; WAV has no tags) | `tinytag.TinyTag.get(path, image=True).images.front_cover` (falls back to `images.any`); tag `title`/`artist`/`album` used for the lookup only |

Allow-listed image hosts: `i.ytimg.com`, `i.scdn.co`, `coverartarchive.org`, `archive.org`,
`*.archive.org`; https only, <= 3 redirects, each re-validated; 10 MB response cap.

### R-03 Image normalisation

Pillow: `Image.open` with `Image.MAX_IMAGE_PIXELS = 40_000_000` (bombs raise), `ImageOps.exif_transpose`,
first frame, convert to RGB (alpha flattened on black), centre-crop to square, `LANCZOS` resize to
600 and 160, JPEG `quality` 85/80, `optimize=True`, no EXIF/ICC (`save(..., exif=b"")`). Output
SHA-256 over the cover bytes is the artwork version. Tests use images generated in the test
itself (solid colours, gradients, a 1 x 1 PNG, a fake "bomb" header), never third-party images.

### R-04 Title cleaning for YouTube sources

Order: yt-dlp `track` + `artist` (YouTube Music metadata) -> `"<artist> - <title>"` split on the
first ` - ` / ` – ` -> raw title with the uploader as artist (minus suffixes ` - Topic`, `VEVO`,
`Official`). Removed suffix patterns (case-insensitive, bracketed or after ` - `): official
(music) video, official audio, lyric(s) video, lyrics, audio, HD, HQ, 4K, remaster(ed) [year];
"feat./ft." parts are moved out of the title. "Live" is kept, because live versions are separate
recordings in MusicBrainz. Normalisation for comparison: NFKC, casefold, `&` -> `and`,
punctuation removed, whitespace collapsed.

### R-05 Matching test set (SC-004)

A fixture file `tests/fixtures/metadata/matching-cases.json` (qa, Phase 0 of the wave) with at
least 20 self-written cases, each a recorded MusicBrainz search response (CC0 data, trimmed to the
fields we read) plus the input (title, artist, duration, ISRC) and the expected outcome
(`recordingMbid` or `not-found`). Must include: the F-01 Metallica case (live/demo/remaster
decoys), Kevin MacLeod by ISRC, a cover version by another artist, a same-title song by another
artist, a title with "(Official Music Video)", a "feat." title, a YouTube upload by a fan
channel, a missing duration, a 503-then-200 sequence, and a non-Latin title.

### R-06 Rate limiting and retries

Process-wide token bucket per host family (`musicbrainz.org`+`coverartarchive.org`: 1/s;
Wikimedia: 1/s); `Retry-After` honoured (seconds or HTTP date, capped at 30 s); otherwise
back-off 2/4/8 s with +-20 % jitter, max 3 retries; then the step fails with a readable message.
The clock and sleeper are injected (tests run instantly). The metadata lane processes one job at
a time, so bursts cannot happen.

### R-07 Contact URL and User-Agent

`STEMSTUDIO_CONTACT_URL` validation: `https` scheme, host present, no user-info, no `@`
anywhere, <= 200 chars, no whitespace or control characters; otherwise the worker logs a
warning (without the value) and uses the generic token. The version is the worker package
version. A unit test asserts that the default User-Agent matches
`^StemStudio/[0-9.]+ \( local-practice-tool; contact not configured \)$`.

### R-08 Where band info lives

Backend `Artists` table keyed by MusicBrainz artist id (one row per artist, shared by tracks),
`Track.Metadata` JSON (status, match, names used, artist id). Refresh when older than 30 days on
the next lookup of a track by that artist. Rationale: one Wikipedia excerpt per artist, and 011
(agent) can query artists.

### R-09 Not chosen

TheAudioDB (no licence for data or images; fan-made images), Last.fm (non-commercial clause,
images excluded, key and account), Discogs API (key, CC0 data dumps only), Spotify Web API (needs
an app registration and key), Commons artist photos (per-file licences; would need per-image
attribution; backlog), MusicBrainz tags as genres (supplementary data CC BY-NC-SA 3.0; Wikidata
P136 genres are CC0).

## Open items for Phase 0 (not executed now)

- P0-1: record the fixture responses for R-05 (qa) and the fake online provider
  (`STEMSTUDIO_FAKE_ONLINE`) behaviour table (data-model 1).
- P0-2: confirm spotDL 4.6.0 `Song` exposes `cover_url`, `isrc`, `album_name`, `year` on our
  locked version (expected from the spotDL source; one unit test with a stub `Song`).

## Owner questions

| # | Question | Blocking? |
|---|---|---|
| O-1 | Is there (or will there be) a public page for StemStudio that may be used as `STEMSTUDIO_CONTACT_URL` in the MusicBrainz/Wikimedia/LRCLIB User-Agent? Until then the generic token is used. | No (works with the generic token; occasional 503s are retried) |
