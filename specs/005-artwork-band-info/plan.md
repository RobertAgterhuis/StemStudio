# Implementation Plan: Song Artwork and Band Info

**Branch**: `005-artwork-band-info` | **Date**: 2026-11-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/005-artwork-band-info/spec.md`; constitution 1.2.0;
research `docs/research/roadmap-003-011.md` section 4 and [research.md](research.md); ADR-014.

**Note**: Produced by the architect agent (`/speckit-plan` equivalent) ahead of time. Contract
version numbers are assigned when the feature starts (after 003 and 004 are merged); the deltas
are in [contracts/DELTA.md](contracts/DELTA.md). Phase 0 tasks are listed in tasks.md but not
executed yet.

## Summary

The worker captures a picture from the import source during the standard job (YouTube
thumbnail, Spotify album image, embedded cover of an upload) and re-encodes it with Pillow into a
600 px cover and a 160 px thumbnail. A new network-only *metadata job* lane in the worker matches
the recording in MusicBrainz (ISRC first, else title + artist + duration), fetches the album
front from Cover Art Archive and artist facts from MusicBrainz and Wikidata plus a Wikipedia
excerpt, throttled to 1 request/s per service with a User-Agent that contains no personal data.
The backend starts the metadata job as soon as the standard job reports title, artist and
duration, stores the best picture by a fixed precedence (user > Spotify > embedded > Cover Art
Archive > YouTube), keeps one `Artist` row per MusicBrainz artist, and serves pictures with
immutable cache URLs. The UI (004 design system) shows the cover in the mixer header and on the
track page, a thumbnail per library row, and a band info panel with CC BY-SA attribution.

## Technical Context

**Language/Version**: unchanged (C# 14 / .NET 10; Python 3.12; TypeScript 6.0, engine untouched).

**Primary Dependencies (new)**: worker `tinytag` 2.3.2 (MIT, no deps). Promoted to direct runtime
dependencies (already in `uv.lock`): Pillow 13.0.0 (MIT-CMU), httpx 0.28.1 (BSD-3, from the dev
group), rapidfuzz 3.14.6 (MIT). No new .NET or npm packages.

**External services (data, no software dependency)**: MusicBrainz WS/2 (core data CC0), Cover
Art Archive (no image licence stated; in-app display only), Wikidata (CC0), Wikipedia REST
summary (CC BY-SA 4.0, attribution). Free, keyless; terms in research.md and ADR-014 D8.

**Storage**: SQLite: `Tracks.Artwork`, `Tracks.AutoArtwork`, `Tracks.Metadata` (JSON),
`Tracks.ArtistId` + new table `Artists` (migration `AddArtworkAndArtists`). Files under
`data/tracks/<id>/artwork/`. Worker: `jobs/<id>/artwork/`, `metadata-jobs/`.

**Testing**: as 003. Online clients are tested against recorded, trimmed CC0 responses
(`httpx.MockTransport`), images generated in tests, injected clock and sleeper. Fake provider
`STEMSTUDIO_FAKE_ONLINE` for the backend/UI/E2E. Real-network tests are
`@pytest.mark.integration` (deselected by default; run by the CTO in the integration wave).

**Target Platform**: unchanged. Needs internet for lookups; fully usable offline.

**Performance Goals**: SC-002 band info within 30 s after the download step; SC-003 library
first screen < 1 s with 100 tracks; SC-006 no measurable effect on separation time.

**Constraints**: no personal data in requests (FR-010); 1 req/s per service (FR-011); images
re-encoded, <= 200 KB / 20 KB (FR-007); pictures and band texts never exported (FR-014).

**Scale/Scope**: one user, hundreds of tracks, tens of artists.

## Constitution Check

*GATE: Must pass before Phase 0. Re-checked after Phase 1 design (below).*

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % line+branch | Every client, matcher, image normaliser and precedence rule is unit-tested first with recorded responses/fakes; backend applier, endpoints and migration with stub handlers and SQLite; bUnit for cover, thumbnail, panel; Playwright with the fake online provider. No test needs the network. | PASS |
| II | SIG 4 stars | New worker package `metadata/` of small modules (one per service, matcher, titles, images, ratelimit, user_agent); backend `Metadata/` folder mirrors `Detail/`; small Razor components. Dependency rule added for `metadata/`. | PASS |
| III | Contracts first | Worker API minor bump (metadata jobs, `Job.artwork`, `SourceMetadata` fields, artwork routes, user-image normalise route), backend API minor bump (artwork routes, band info, metadata retry/correct, `TrackDto.artwork/metadata`), UI test ids minor bump; Phase 0 writes the full OpenAPI 3.1 documents (Redocly-valid), `schemas.py`, `ports.py`, Contracts DTOs. ADR-014 records the design. | PASS |
| IV | Open source, lawful use | New code dependency tinytag (MIT); promoted Pillow (MIT-CMU), httpx (BSD-3), rapidfuzz (MIT): all OSI. Data services are free, keyless, no paid tier. **Licences/terms**: MusicBrainz core + Wikidata CC0 (credited anyway); Wikipedia CC BY-SA 4.0 with full attribution (title, authors link, licence link, unmodified); MusicBrainz supplementary data (tags) not used (CC BY-NC-SA); cover images copyrighted -> in-app display only, never exported; TheAudioDB/Last.fm excluded (ADR-014 D8). Personal-practice notice extended (FR-014). | PASS |
| V | Simplicity and honesty | No artist photos, no bulk re-enrichment job, no second image size beyond two, no new service container; "not found" instead of guesses; failures shown with reasons. | PASS |
| - | Tech & security | No secrets (no API keys at all). Outbound hosts allow-listed (https, <= 3 redirects re-validated, 10 MB cap); images decoded with a pixel cap and re-encoded (polyglot/EXIF stripped); user image upload size-limited and content-checked; User-Agent without personal data, contact URL validated; no path built from remote data (artwork paths are fixed names). | PASS |
| - | Workflow | Spec Kit flow; disjoint owner paths; hand-off per checkpoint (ADR-008). | PASS |

**Post-design re-check (after Phase 1)**: PASS; refinements tracked below.

## Project Structure

### Documentation (this feature)

```text
specs/005-artwork-band-info/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/DELTA.md     # deltas; full documents are written in Phase 0 (T003/T004)
```

### Source Code (additions)

```text
worker/src/stemstudio_worker/
├── ports.py      # + ArtworkCandidate, ArtworkImages, ImageNormalizer, MetadataLookup, MetadataQuery,
│                 #   SourceInfo/FetchedAudio artwork + album/isrc fields (architect)
├── schemas.py    # + ArtworkSource, ArtworkInfo, SourceMetadata.{album,isrc,durationSeconds,lookup*},
│                 #   Job.artwork, MetadataJob*, MetadataResult, ArtistInfo, WikipediaSummary (architect)
├── config.py     # + online_lookups, contact_url, wikipedia_languages, fake_online (architect)
├── metadata/                     # audio-ingest
│   ├── user_agent.py, ratelimit.py, http.py      # UA, per-host token bucket, allow-listed httpx client
│   ├── titles.py                                 # YouTube title cleaning, normalisation
│   ├── images.py                                 # Pillow normaliser (cover/thumb)
│   ├── source_artwork.py                         # YouTube thumbnails, Spotify cover_url, tinytag
│   ├── musicbrainz.py, coverart.py, wikidata.py, wikipedia.py
│   ├── matcher.py                                # candidate selection (ADR-014 D4)
│   ├── lookup.py                                 # MetadataLookup orchestration -> MetadataResult
│   └── fake.py                                   # STEMSTUDIO_FAKE_ONLINE provider
├── ingest/       # fetcher/spotify/upload hand artwork candidates + album/isrc to SourceInfo (audio-ingest)
├── jobs/         # artwork capture step in the standard job; metadata job lane, store, sweep (stem-separation)
├── api/          # artwork routes, metadata-jobs routes, POST /v1/artwork (stem-separation)
└── main.py       # composition (stem-separation)

src/StemStudio.Contracts/Tracks/{ArtworkDto,ArtworkSourceDto,TrackMetadataDto,MetadataStatusDto}.cs,
    Artists/{BandInfoDto,ArtistInfoDto,WikipediaSummaryDto,DataSourceDto}.cs,
    Tracks/CorrectMetadataRequest.cs, ApiRoutes (+ artwork, band info, metadata)      (architect)
src/StemStudio.Domain/Tracks/{TrackArtwork,ArtworkSource,ArtworkPrecedence,TrackMetadata}.cs,
    Artists/Artist.cs                                                             (backend)
src/StemStudio.Persistence/Migrations/*_AddArtworkAndArtists.cs, ArtistRepository  (backend)
src/StemStudio.Api/Metadata/{MetadataSynchronizer,MetadataJobApplier,BandInfoEndpoints,MetadataService}.cs,
    Artwork/{ArtworkEndpoints,ArtworkFileStore,ArtworkService}.cs, Worker client additions   (backend)
src/StemStudio.Web/Components/Shared/{TrackArtwork,ArtworkPlaceholder}.razor,
    Components/BandInfo/{BandInfoPanel,BandInfoFacts,WikipediaAttribution,CorrectMetadataDialog}.razor,
    Tracks.razor (thumbnail column), TrackDetail, Mixer header, Help/<005 topics>          (frontend)
docker-compose.yml (+ env vars), README env table via CTO, CI unchanged except contract dir (devops)
e2e/tests/artwork.spec.ts, band-info.spec.ts; tests/fixtures/metadata/**                (qa)
```

**Dependency direction**: worker `api -> jobs -> ports <- {ingest, metadata}`; `metadata` imports
only `ports`, `schemas`, `config` (its image-host allow-list lives in `metadata/http.py`, not in
`ingest`); `ingest` does not import `metadata` (the composition root passes the
`ImageNormalizer`). .NET: `Api.Metadata -> Worker client`, `Api.Artwork -> Storage`;
Web -> API client.

### Ownership map (additions)

| Path | Owner |
|---|---|
| `specs/005-*/**`, ADR-014, `ports.py`, `schemas.py`, `config.py`, `pyproject.toml`/`uv.lock`, `StemStudio.Contracts/**`, project files | architect |
| `worker/src/stemstudio_worker/{metadata,ingest}/**`, `worker/tests/{metadata,ingest}/**` | audio-ingest |
| `worker/src/stemstudio_worker/{jobs,api}/**`, `main.py`, `worker/tests/{jobs,api}/**` | stem-separation |
| `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | backend |
| `src/StemStudio.Web/**` (incl. help topics), `tests/StemStudio.Web.Tests/**` | frontend |
| `tests/fixtures/**`, `e2e/**`, `docs/testing/**` | qa |
| `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | devops |
| `tools/quality/**`, `docs/quality/**` | quality-gate |
| `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md` | CTO |

audio-engine and beat-analysis have no work in this feature.

## Key flows

1. **Import (source artwork)**: resolver/fetcher/upload intake return an `ArtworkCandidate`
   (remote URL on an allow-listed host, or embedded bytes) -> the standard job's new step
   "Saving cover" (after download, before normalisation; <= 15 s budget, errors swallowed and
   logged) -> `ImageNormalizer` -> `jobs/<id>/artwork/` -> `Job.artwork` set.
2. **Backend sync**: on each standard poll: `Job.artwork` new hash -> download both variants ->
   `Track.OfferArtwork(youtube|spotify|embedded)`; `metadata.durationSeconds` present and
   `Track.Metadata.Status == none` -> `pending`.
3. **Metadata job**: `MetadataSynchronizer` creates `POST /v1/metadata-jobs` for `pending`
   tracks -> worker lane: MusicBrainz (ISRC / search) -> matcher -> release group -> CAA front ->
   artist + url-rels -> Wikidata claims + labels -> Wikipedia summary -> result ->
   backend: upsert `Artist`, set `TrackMetadata`, offer `coverartarchive` artwork.
4. **Correction (P2)**: `POST /api/tracks/{id}/metadata/lookup {title?, artist?}` -> names
   stored, status `pending` -> flow 3.
5. **User picture (P3)**: `PUT /api/tracks/{id}/artwork` (multipart) -> backend forwards the
   stream to worker `POST /v1/artwork` (normalise only) -> stores `user/` files ->
   `Track.SetUserArtwork`; `DELETE /api/tracks/{id}/artwork` -> back to `AutoArtwork`.
6. **Old tracks**: `GET /api/tracks/{id}` of a completed track with `Metadata.Status == none`
   moves it to `pending` (FR-012).

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| A second worker job lane (network only) next to the separation scheduler | Lookups must finish within 30 s (SC-002) while a detail separation can take 30 min on the CPU | Queueing lookups behind separation violates SC-002; running them in the backend needs a .NET image library (ImageSharp is not OSI) and a second egress point |
| Backend forwards user images to the worker for re-encoding | Images must be decoded and re-encoded safely (FR-007, security) and .NET has no OSI image library in the stack | Storing user files as-is would keep EXIF/GPS data and allow polyglot files |
| Generic User-Agent contact token instead of a URL | No public project URL exists yet; no personal data may be sent | A made-up URL could point to someone else; an e-mail is personal data. Owner question O-1 |
