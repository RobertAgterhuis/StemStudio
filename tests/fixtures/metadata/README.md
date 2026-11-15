# Metadata fixtures (feature 005)

Owner: qa (005:T009). Consumers: audio-ingest (`worker/tests/metadata/`, 005:T031/T033/T034),
the E2E specs `e2e/tests/band-info.spec.ts` and `artwork.spec.ts`, and the fixture tests in
`tests/fixtures/tests/test_metadata_fixtures.py`.

| File | What | Used by |
|---|---|---|
| `matching-cases.json` | 23 matching cases (research.md R-05, ADR-014 D4, SC-004): lookup input, optional raw YouTube fields (`source`, for the R-04 title cleaning), the MusicBrainz exchanges in request order, and the expected outcome (`found` with `recordingMbid`/`releaseGroupMbid`/`matchedBy`, `not-found`, or `failed`) | `test_matcher.py`, `test_musicbrainz.py`, `test_titles.py` (replay with `httpx.MockTransport`) |
| `reference/metallica.json`, `reference/kevin-macleod.json` | The two reference artists (quickstart A4/A5): MusicBrainz artist (url-rels) and release group, Cover Art Archive exchanges (307 to archive.org for Load, 200 for Kevin MacLeod), Wikidata claims (P136, P527 + P580/P582, P571, P495, P856) and labels, Wikipedia REST summary; plus the `expected` ArtistInfo/ReleaseGroupInfo/WikipediaSummary | `test_coverart.py`, `test_wikidata.py`, `test_wikipedia.py`, `test_lookup.py` |
| `fake-provider.json` | Proposed behaviour table of the fake online provider (`STEMSTUDIO_FAKE_ONLINE` = `found`, `not-found`, `failed`, `off`) and the facts it returns in mode `found` | `metadata/fake.py` (005:T034) should return exactly these values; the E2E specs read their expectations from here |

## Provenance and licences

- **Nothing here is a capture of live traffic.** The files are self-written in the shape of the
  MusicBrainz WS/2, Cover Art Archive, Wikidata `wbgetentities` and Wikipedia REST
  `page/summary` JSON, trimmed to the fields StemStudio reads.
- Real identifiers and facts are taken only from the feasibility check of 2026-11-15
  (`specs/005-artwork-band-info/research.md` F-01..F-05): Metallica recording
  `c8361f8a-ece3-4ad0-a5a6-4bb6d06083b9` (261.9 s), release group "Load"
  `e389b7df-862d-3d91-a612-acca150f6e71` (1996-05-04), ISRC `USEVI9692306`, Wikidata `Q15920`;
  Kevin MacLeod artist `8292bf44-2dd1-4bad-8019-276ec38f28e7`, ISRC `USUAN1400011`, release group
  `057247bb-7a93-4654-9a7d-4cb9abaf5c5c`, Wikidata `Q16731782`; plus `Q30` (United States).
  MusicBrainz core data and Wikidata are CC0. Band member names and years of Metallica are
  public facts.
- **Every other identifier is synthetic**: MusicBrainz ids are UUIDv5 in the namespace
  `5a1d3c1e-0c5e-4d9b-9a57-57e3575701d0` (including the Metallica artist id), Wikidata ids of
  genres and members are `Q9000000nn`, Wikipedia revision ids are `12500000nn`. The live-service
  test (audio-ingest 005:T051, CTO 005:T062) checks the real ids at run time.
- Artists other than Metallica and Kevin MacLeod ("Nora Fieldings", "The Quiet Meters",
  "Северный ветер", "The Fixture Band", ...) are fictional.
- **No Wikipedia text is committed** (CC BY-SA 4.0): every `extract` is a self-written
  placeholder starting with "Placeholder summary written for the StemStudio tests." (enforced by
  `test_wikipedia_extracts_are_placeholders`).
- **No image bytes are committed**: the Cover Art Archive exchanges carry `imageSize`; tests
  generate a JPEG of that size (research.md R-03). Audio and pictures for the E2E suite come from
  `tests/fixtures/generate_artwork_fixtures.py` (generated gradients).

## Matching rules the expected outcomes follow

`test_metadata_fixtures.py` contains a small reference matcher and checks every case against it:
title equal after NFKC + casefold + `&` -> `and` + punctuation removed + whitespace collapsed;
artist similarity >= 0.9 (`rapidfuzz.fuzz.ratio / 100`; every case is either equal after
normalisation or far below the threshold, so other similarity functions give the same result);
closest duration wins; ties prefer an official `Album` release group without secondary types with
the earliest date; four 503 answers in a row (first try + 3 retries) fail the lookup with
"Band information could not be retrieved; try again later.". `request.query` is the expected
Lucene query; a replaying transport may match by `request.kind` and order only (escaping is
pinned by case `lucene-special-characters`).

## Changing the fixtures

Edit the JSON by hand, keep ids lower-case, keep every extract a placeholder, then run:

```bash
uv run --project worker pytest -c tests/fixtures/pyproject.toml tests/fixtures/tests/test_metadata_fixtures.py
```
