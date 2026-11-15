# Quickstart and validation guide: Song Artwork and Band Info

Builds on the 001-003 quickstarts. Contracts: [contracts/DELTA.md](contracts/DELTA.md). Entities:
[data-model.md](data-model.md). Checks: [research.md](research.md). Design: ADR-014.

## Running StemStudio

Unchanged start commands (`docker compose up --build`, GPU override optional). Online lookups are
on by default. Optional environment variables (worker):

```bash
STEMSTUDIO_CONTACT_URL=https://<a public page about your StemStudio install>   # never an e-mail
STEMSTUDIO_ONLINE_LOOKUPS=off          # no MusicBrainz/Cover Art Archive/Wikidata/Wikipedia requests
STEMSTUDIO_WIKIPEDIA_LANGUAGES=nl,en   # excerpt language order
```

What leaves the machine: the song title, artist, album, duration and ISRC, sent to MusicBrainz,
Cover Art Archive, Wikidata and Wikipedia with the User-Agent
`StemStudio/<version> ( <contact> )`. Pictures and band texts stay in the app.

## Validation (A1..A10)

| # | What | How | Expected |
|---|---|---|---|
| A1 | Unit and contract tests | `scripts/test-all.ps1` (or `.sh`) | green; coverage >= 90 % line and branch per component; no test touches the network |
| A2 | Source artwork, fake stack (US1, SC-001) | `STEMSTUDIO_SEPARATOR=fake STEMSTUDIO_FAKE_ONLINE=not-found docker compose up -d --build --wait`; Playwright `artwork.spec.ts` | YouTube/Spotify/upload-with-cover imports show `track-thumb-<id>`, `track-cover`, `mixer-cover` with `data-artwork-source` youtube/spotify/embedded; a WAV shows `none` (placeholder) |
| A3 | Band info, fake stack (US2) | same stack with `STEMSTUDIO_FAKE_ONLINE=found`; `band-info.spec.ts` | panel shows the fake artist, `band-info-attribution` has the three links; `failed` mode shows `band-info-retry`; `off` shows `band-info-disabled` |
| A4 | Reference song, real stack (SC-002) | import "Hero of the Day" (Metallica) from YouTube; note the time the download step ends | within 30 s: `metadata-status` `found`; `mixer-album` "Load (1996)"; panel: Metallica, group, US, 1981-, members listed, website metallica.com, English Wikipedia excerpt with attribution; cover = Cover Art Archive (source `coverartarchive`, replacing the YouTube thumbnail) |
| A5 | CC BY song via Spotify (SC-002) | import the Spotify link of "Monkeys Spinning Monkeys" | cover source `spotify` (album image stays: higher precedence than Cover Art Archive); panel: Kevin MacLeod, person, US, no members row |
| A6 | No wrong matches (SC-004) | worker tests over `tests/fixtures/metadata/matching-cases.json` | every case gives the expected recording or `not-found` |
| A7 | No personal data (FR-010, SC-005) | worker test over all requests recorded by the fake transport; on the real stack inspect the worker log line `metadata request host=... ua=...` (debug level) | UA matches the pattern; no e-mail, user or host name; only the documented query fields |
| A8 | Lookups off (FR-011) | `STEMSTUDIO_ONLINE_LOOKUPS=off`, import a YouTube link | source thumbnail still shown; band info "Online lookups are switched off"; no outgoing request (worker log) |
| A9 | Library performance (SC-003) | seed 100 tracks with artwork (qa script `e2e/support/seed-artwork.ts`), open `/tracks` with the browser cache empty | first screen < 1 s; each thumbnail <= 20 KB; images `loading="lazy"`; second visit served from cache (304/immutable) |
| A10 | Separation unaffected (SC-006) | import the reference song with lookups on and off (cached audio deleted in between) | standard job duration within 5 % |

## Developer commands

```bash
cd worker && uv run pytest tests/metadata tests/ingest --cov        # clients with recorded responses
cd worker && uv run pytest -m integration tests/metadata/test_live_services.py   # real services, manual only
npx @redocly/cli@2.78.0 lint --config specs/005-artwork-band-info/contracts/redocly.yaml \
  specs/005-artwork-band-info/contracts/worker-api.openapi.yaml \
  specs/005-artwork-band-info/contracts/backend-api.openapi.yaml
```
