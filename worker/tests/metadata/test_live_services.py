"""005:T051: live-service smoke test against the real services (``-m integration`` only).

Deselected by default (``pyproject.toml`` addopts ``-m "not integration"``); the CTO runs it in
005:T062 on the reference machine:
``uv run pytest -m integration tests/metadata/test_live_services.py``.
Roughly one request per service for the two reference songs (research F-01..F-05), through
one shared client (1 request/s per service, the StemStudio User-Agent from
``STEMSTUDIO_CONTACT_URL`` or the generic token). Only identifiers and shapes are asserted, never
third-party text.
"""

from collections.abc import Iterator

import pytest

from stemstudio_worker.config import load_settings
from stemstudio_worker.metadata.coverart import CoverArtArchive
from stemstudio_worker.metadata.http import OnlineClient, create_online_client
from stemstudio_worker.metadata.lookup import OnlineMetadataLookup
from stemstudio_worker.metadata.musicbrainz import MusicBrainz
from stemstudio_worker.metadata.wikidata import Wikidata
from stemstudio_worker.metadata.wikipedia import Wikipedia
from stemstudio_worker.ports import MetadataQuery

pytestmark = pytest.mark.integration

HERO = "c8361f8a-ece3-4ad0-a5a6-4bb6d06083b9"
LOAD = "e389b7df-862d-3d91-a612-acca150f6e71"
KEVIN = "8292bf44-2dd1-4bad-8019-276ec38f28e7"


@pytest.fixture(scope="module")
def client() -> Iterator[OnlineClient]:
    online = create_online_client(load_settings().online)
    yield online
    online.close()


def test_musicbrainz_search_finds_the_load_studio_take(client: OnlineClient) -> None:
    query = MetadataQuery(title="Hero of the Day", artist="Metallica", duration_seconds=261.9)

    found = MusicBrainz(client).find(query)

    assert found is not None
    assert found.recording.mbid == HERO
    assert found.release_group is not None
    assert found.release_group.mbid == LOAD


def test_cover_art_archive_front_of_load(client: OnlineClient) -> None:
    front = CoverArtArchive(client).front(LOAD)

    assert front is not None
    assert front[1].source == "coverartarchive"


def test_wikidata_and_wikipedia_for_metallica(client: OnlineClient) -> None:
    facts = Wikidata(client).facts("Q15920", ("en",))
    summary = Wikipedia(client).summary(facts.sitelinks, ("en",))

    assert facts.genres
    assert facts.sitelinks.get("en")
    assert summary is not None
    assert summary.page_url.startswith("https://en.wikipedia.org/")


def test_full_lookup_of_kevin_macleod_by_isrc(client: OnlineClient) -> None:
    query = MetadataQuery(
        title="Monkeys Spinning Monkeys", artist="Kevin MacLeod", isrc="USUAN1400011"
    )

    result = (
        OnlineMetadataLookup(client, load_settings().online).lookup(query, lambda f, s: None).result
    )

    assert result.outcome == "found"
    assert result.recording is not None
    assert result.recording.matched_by == "isrc"
    assert result.artist is not None
    assert result.artist.mbid == KEVIN
