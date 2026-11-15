# Hand-off: audio-ingest / 005 us2 (005:T031-T034)

The band information lookup of metadata jobs, the fake online provider and the factories the
composition root calls (FR-003, FR-004, FR-008, FR-010, FR-011, SC-004, SC-005). Earlier
checkpoints: [foundational](audio-ingest-005-foundational.md), [us1](audio-ingest-005-us1.md).

The qa wave 005+007 was merged into this branch (`git merge 005-007-artwork-export`, c2d26ac).
That brings `tests/fixtures/metadata/**` here, and the tests use those files directly, as the CTO
asked mid-task.

## What was built

- **`metadata/matcher.py`** implements ADR-014 D4:
  - A candidate qualifies when its normalised title equals the cleaned query title, its full
    artist credit or first credited artist is at least 90 % similar (`rapidfuzz.fuzz.ratio`
    over normalised names), and its length is within ±5 s when the duration is known. An
    unknown length never qualifies against a known duration.
  - ISRC hits skip these checks.
  - Selection takes the closest duration. Ties go to the best release: official, then Album,
    then no secondary type, then the earliest date.
  - `best_release_group`, `year_of` and the `Candidate`/`Release`/`Credit`/`RecordingFound`
    dataclasses live here. No match means not found; nothing is guessed. A query without an
    artist and without an ISRC sends no request.
- **`metadata/musicbrainz.py`**:
  - `MusicBrainz.find(query)` runs the ISRC lookup first:
    `GET /ws/2/isrc/{isrc}?inc=artist-credits+releases+release-groups&fmt=json`. A 404 means
    unknown, and the search runs next. ISRC matches score 100.
  - The search is `GET /ws/2/recording?query=recording:"…" AND artist:"…" AND dur:[d-5000 TO d+5000]&fmt=json&limit=25`.
    The title passes through `clean_title` and the names are Lucene-escaped.
  - `release_group(mbid)` reads `first-release-date`. `artist(mbid)` reads `inc=url-rels`:
    type, ISO country, life-span years, the https official homepage and the wikidata Q id.
  - Answers are validated with pydantic. A malformed answer raises `OnlineRequestError`.
- **`metadata/coverart.py`**: `CoverArtArchive.front(mbid)` gets
  `/release-group/{mbid}/front-500`, follows the 307 to archive.org (re-validated) and
  normalises the picture. It returns the images plus `ArtworkInfo(source="coverartarchive",
  originUrl=<CAA URL>)`, or None on 404.
- **`metadata/wikidata.py`**: `Wikidata.facts(qid, languages)` makes two requests:
  - `wbgetentities` with `props=sitelinks|claims` and `sitefilter=<lang>wiki|…`, which gives
    P136, P527 (P580/P582), P571, P576 and P856;
  - one labels call (`props=labels&languages=en`, at most 50 ids).

  Genres are capped at 8 and cut to 100 characters, members at 12. Times count only with year
  precision. Items without a label are skipped.
- **`metadata/wikipedia.py`**: `Wikipedia.summary(sitelinks, languages)` reads the REST
  `page/summary` of the first configured language that has a sitelink and a usable summary.
  - Usable means `type=standard`, a title, a revision, an https page and an extract of
    1..2000 characters. The extract is never cut or edited, only dropped.
  - It builds the history URL (`index.php?title=<slug>&action=history`) and the revision id.
    Licence and licence URL come from the contract defaults.
- **`metadata/lookup.py`**: `OnlineMetadataLookup(client, settings.online, clock)` runs the
  steps (search, release group, CAA, artist, Wikidata, Wikipedia) and reports the stages
  "Searching MusicBrainz", "Fetching album cover", "Reading Wikidata" and "Reading Wikipedia".
  - Lookups off gives `disabled` without any request.
  - A failed recording search raises `MetadataLookupError(METADATA_FAILED_TEXT)`.
  - Later failures become warnings: `COVER_WARNING_TEXT`, `WIKIPEDIA_WARNING_TEXT`, and two new
    texts, `WIKIDATA_WARNING_TEXT` "Wikidata could not be reached." and `ARTIST_WARNING_TEXT`
    "The artist details could not be loaded.". A CAA 404 is no warning.
  - MusicBrainz facts are preferred, and Wikidata fills begin/end year and website. Persons
    get no members.
- **`metadata/fake.py`** follows `tests/fixtures/metadata/fake-provider.json` exactly:
  - `FakeMetadataLookup(mode)`: `found` gives "The Fixture Band", "Synthetic Sessions" (2020),
    the three members, the placeholder summary and a generated `coverartarchive` picture in
    colour (40, 90, 160). The recording title follows the looked-up title, so corrected lookups
    work. `not-found` and `off` (`disabled`) give the listed outcomes. `failed` raises the fixed
    error text.
  - `FakeImageNormalizer` generates a solid picture for URL candidates (no download) and
    re-encodes embedded ones.
- **`metadata/__init__.py`** has the factories. They match what stem-separation's composition
  already loads via `optional_factory`:
  - `create_image_normalizer(settings)` and `create_metadata_lookup(settings)` share **one**
    `OnlineClient` and rate limiter per process. On the fake stack they return the fakes;
    lookups off forces the fake into mode `off`.
  - `create_source_details(settings)` returns the reader for `create_ingest_adapters`.
- `http.py` gained `LenientModel` and `parse_answer` (shared response validation).
  `PillowImageNormalizer.client` is now public, for the shared-client check.

## Red -> green commits

| Task pair | Red | Green |
|---|---|---|
| 005:T031 / T032 | 828785b (24 own cases) | 4286a12 |
| 005:T031 / T032 (qa set, ISRC lookup endpoint) | cf4439d | d62599a |
| 005:T033 / T034 | 99e630f (includes the fake and factory tests) | 30b7076 |

## Files changed

- `worker/src/stemstudio_worker/metadata/{matcher,musicbrainz,coverart,wikidata,wikipedia,lookup,fake}.py`
  (new); `metadata/{__init__,http,images}.py`
- `worker/tests/metadata/{test_matcher,test_musicbrainz,test_qa_matching_cases,test_coverart,test_wikidata,test_wikipedia,test_lookup,test_fake}.py`,
  `matching_cases.json`, `reference.py` (new); `fakes.py` (`Router`, `routed_client`)
- `specs/005-artwork-band-info/tasks.md` (ticked T031-T034), this note

## Tests and coverage

`cd worker && uv run pytest -q --cov --cov-branch` (whole worker): **1438 passed**, 3 deselected.
TOTAL 5308 statements, 0 missed, 612 branches, 1 partial (it already existed and is outside my
paths). Every module of `metadata/**` and `ingest/**` is at 100 % line and 100 % branch:

```text
metadata: __init__ 22, coverart 18, fake 57, http 150, images 99, lookup 112, matcher 96,
          musicbrainz 131, ratelimit 52, source_artwork 75, titles 46, user_agent 33,
          wikidata 117, wikipedia 44      -> 1052 stmts, 142 branches, 0 missed
ingest:   unchanged since us1             -> 100 %
```

The tests cover:
- the qa matching set (23 cases): request kind, path and order, the exact Lucene query of
  `lucene-special-characters`, and the R-04 cleaning of the 6 sourced cases;
- my own 24 cases;
- the two qa reference artists end to end (CAA 307 to archive.org, Wikidata, Wikipedia,
  expected `ArtistInfo`/`ReleaseGroupInfo`/`WikipediaSummary`);
- a privacy check over every recorded request: the StemStudio User-Agent on all of them, only
  the allowed query fields, the album never sent, and no cookie, authorization, referer or from
  header;
- the comparison with `fake-provider.json` for all four modes.

None of them touches the network.

## Quality checks

- `ruff check src tests` and `ruff format --check`: clean (196 files). `mypy --strict src tests`:
  no issues in 200 files.
- `sig_check.py` on `metadata/**` and `tests/metadata/**`: 33 files, 404 units, **0 errors,
  0 warnings**. `duplication.py` on my paths: 0 clones.
- Dependency direction: `metadata/**` imports only `ports`, `schemas`, `config` and itself.

## Dependencies added

None.

## Deviations and decisions (please review)

1. **ISRC lookup**: I use the `/ws/2/isrc/{isrc}` lookup instead of a search query `isrc:…`,
   to match the qa fixture and because it is exact. ADR-014 D4 says "isrc:<code>" (search
   syntax); the intent, ISRC first and exact, is unchanged.
2. **Extra MusicBrainz request**: `release-group/{mbid}?fmt=json` provides the exact first
   release year, following the qa reference flow. If it fails, the search's release group is
   kept and no warning is added.
3. **Two new warning texts**, `WIKIDATA_WARNING_TEXT` and `ARTIST_WARNING_TEXT`
   (data-model 2.7 lists only the Wikipedia and cover warnings). They are constants in
   `metadata/lookup.py`. Architect: add them to data-model 2.7 if you agree.
4. **Wikidata P495 (country)** is not mapped. Mapping it needs a second item lookup. The
   country comes from MusicBrainz only.
5. **Per-job budget**: the 60 s budget of ADR-014 D7 is not enforced inside the lookup.
   Requests have 10 s timeouts and at most 3 retries each. A wall-clock limit belongs to the
   metadata lane (stem-separation), if wanted.
6. **The fake artist carries `wikidataId` `Q900000099` and `revisionId` `1250000099`**, which
   `fake-provider.json` does not list. Without them the result could not name the Wikidata
   source, and `WikipediaSummary` requires a revision id.

## Open issues / requests

- **stem-separation**: the factories `create_image_normalizer(settings)` and
  `create_metadata_lookup(settings)` exist under the names your composition loads. Please also
  pass `create_source_details(settings)` to `create_ingest_adapters(settings, details)` in
  `default_adapters`; without it there is no source artwork (see us1).
- **architect**: deviations 1 and 3 (ADR-014 D4 wording, data-model 2.7 warning texts).
- **qa**: none. The fake matches `fake-provider.json` and every case of `matching-cases.json`
  passes as recorded.

## Next owner

audio-ingest A continues with 005:T051 (HO:polish).
