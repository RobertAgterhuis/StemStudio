"""005:T033: ``MetadataLookup`` orchestration (FR-003/008/010, SC-005, data-model 2.4-2.7)."""

from dataclasses import dataclass, field
from datetime import UTC, datetime
from typing import Any

import httpx
import pytest

from stemstudio_worker.config import OnlineSettings
from stemstudio_worker.metadata.lookup import (
    ARTIST_WARNING_TEXT,
    STAGES,
    WIKIDATA_WARNING_TEXT,
    OnlineMetadataLookup,
)
from stemstudio_worker.ports import MetadataLookup, MetadataLookupError, MetadataQuery
from stemstudio_worker.schemas import (
    COVER_WARNING_TEXT,
    METADATA_FAILED_TEXT,
    WIKIPEDIA_WARNING_TEXT,
    MetadataResult,
)

from .fakes import USER_AGENT, Router, routed_client
from .reference import MB_HOST, load, router_for, search_reply

FETCHED_AT = datetime(2026, 11, 15, 12, 0, 14, tzinfo=UTC)
SETTINGS = OnlineSettings(wikipedia_languages=("en", "nl"))
HERO = "c8361f8a-ece3-4ad0-a5a6-4bb6d06083b9"
MONKEYS = "9667c51c-3727-5ae2-a60c-a6c844a17e61"
METALLICA_QUERY = MetadataQuery(
    title="Hero of the Day", artist="Metallica", album="Load", duration_seconds=261.9
)
ALLOWED_PARAMS = {
    "query",
    "fmt",
    "limit",
    "inc",
    "action",
    "ids",
    "props",
    "sitefilter",
    "languages",
    "format",
}


@dataclass
class Stages:
    seen: list[tuple[float, str]] = field(default_factory=list)

    def __call__(self, fraction: float, stage: str) -> None:
        self.seen.append((fraction, stage))


def _metallica() -> tuple[dict[str, Any], Router]:
    reference = load("metallica")
    router = router_for(reference)
    router.add(MB_HOST, "/ws/2/recording", search_reply(reference, HERO, "Hero of the Day", 261893))
    return reference, router


def _lookup(router: Router, settings: OnlineSettings = SETTINGS) -> OnlineMetadataLookup:
    return OnlineMetadataLookup(routed_client(router), settings, clock=lambda: FETCHED_AT)


def test_is_a_metadata_lookup() -> None:
    lookup: MetadataLookup = _lookup(Router())

    assert lookup is not None
    assert STAGES == (
        "Searching MusicBrainz",
        "Fetching album cover",
        "Reading Wikidata",
        "Reading Wikipedia",
    )


def test_lookups_off_complete_as_disabled_without_requests() -> None:
    router = Router()

    findings = _lookup(router, OnlineSettings(lookups_enabled=False)).lookup(
        METALLICA_QUERY, Stages()
    )

    assert findings.result.outcome == "disabled"
    assert findings.artwork is None
    assert router.requests == []


def test_metallica_reference_flow() -> None:
    reference, router = _metallica()
    stages = Stages()

    findings = _lookup(router).lookup(METALLICA_QUERY, stages)

    result, expected = findings.result, reference["expected"]
    assert (result.outcome, result.warnings) == ("found", [])
    assert result.recording is not None
    assert result.recording.mbid == HERO
    assert result.release_group is not None
    assert result.release_group.model_dump(by_alias=True) == expected["releaseGroup"]
    _check_artist(result, expected)
    fractions = [fraction for fraction, _ in stages.seen]
    assert [stage for _, stage in stages.seen] == list(STAGES)
    assert fractions == sorted(fractions)


def _check_artist(result: MetadataResult, expected: dict[str, Any]) -> None:
    assert result.artist is not None
    artist = result.artist.model_dump(by_alias=True, exclude={"summary", "fetched_at"})
    assert artist == expected["artist"]
    assert result.artist.summary is not None
    summary = result.artist.summary.model_dump(by_alias=True)
    assert {key: summary[key] for key in expected["summary"]} == expected["summary"]
    assert result.artist.fetched_at == FETCHED_AT


def test_default_clock_is_utc_now() -> None:
    _, router = _metallica()
    before = datetime.now(UTC)

    lookup = OnlineMetadataLookup(routed_client(router), SETTINGS)
    result = lookup.lookup(METALLICA_QUERY, Stages()).result

    assert result.artist is not None
    assert before <= result.artist.fetched_at <= datetime.now(UTC)


def test_missing_release_group_details_keep_the_search_release_group() -> None:
    path = "/ws/2/release-group/e389b7df-862d-3d91-a612-acca150f6e71"
    router = _failing(MB_HOST, path, httpx.Response(404))

    result = _lookup(router).lookup(METALLICA_QUERY, Stages()).result

    assert result.release_group is not None
    assert result.release_group.title == "Load"


def test_metallica_cover_comes_from_the_cover_art_archive() -> None:
    _, router = _metallica()

    findings = _lookup(router).lookup(METALLICA_QUERY, Stages())

    assert findings.artwork is not None
    assert findings.result.artwork == findings.artwork.info(
        "coverartarchive",
        "https://coverartarchive.org/release-group/e389b7df-862d-3d91-a612-acca150f6e71/front-500",
    )


def test_kevin_macleod_reference_flow_by_isrc() -> None:
    reference = load("kevin-macleod")
    router = router_for(reference)
    isrc_reply = search_reply(reference, MONKEYS, "Monkeys Spinning Monkeys", 125000)
    router.add(MB_HOST, "/ws/2/isrc/USUAN1400011", isrc_reply)
    query = MetadataQuery(title="Monkeys Spinning Monkeys", artist="Kevin", isrc="USUAN1400011")

    result = _lookup(router).lookup(query, Stages()).result

    assert result.recording is not None
    assert (result.recording.matched_by, result.warnings) == ("isrc", [])
    _check_artist(result, reference["expected"])


def test_requests_carry_the_user_agent_and_only_the_allowed_fields() -> None:
    _, router = _metallica()

    _lookup(router).lookup(METALLICA_QUERY, Stages())

    assert len(router.requests) == 8
    for request in router.requests:
        assert request.headers["User-Agent"] == USER_AGENT
        assert set(request.url.params) <= ALLOWED_PARAMS
        assert "Load" not in request.url.params.get("query", "")
        assert not {"cookie", "authorization", "referer", "from"} & set(request.headers)


def test_not_found_sends_only_the_search() -> None:
    router = Router()
    router.add(MB_HOST, "/ws/2/recording", httpx.Response(200, json={"recordings": []}))

    findings = _lookup(router).lookup(METALLICA_QUERY, Stages())

    assert findings.result.outcome == "not-found"
    assert (findings.result.recording, findings.result.artist, findings.artwork) == (
        None,
        None,
        None,
    )
    assert len(router.requests) == 1


def test_failed_recording_search_fails_the_job_with_the_fixed_text() -> None:
    router = Router()
    router.add(MB_HOST, "/ws/2/recording", httpx.Response(503))

    with pytest.raises(MetadataLookupError) as raised:
        _lookup(router).lookup(METALLICA_QUERY, Stages())

    assert str(raised.value) == METADATA_FAILED_TEXT
    assert len(router.requests) == 4


def _failing(host: str, path: str, reply: httpx.Response, props: str = "") -> Router:
    _, router = _metallica()
    router.routes[(host, path, props)] = [reply]
    return router


def test_wikipedia_failure_becomes_a_warning() -> None:
    router = _failing(
        "en.wikipedia.org", "/api/rest_v1/page/summary/Metallica", httpx.Response(500)
    )

    result = _lookup(router).lookup(METALLICA_QUERY, Stages()).result

    assert result.outcome == "found"
    assert result.warnings == [WIKIPEDIA_WARNING_TEXT]
    assert result.artist is not None
    assert result.artist.summary is None
    assert result.artist.genres


@pytest.mark.parametrize(
    "reply",
    [httpx.Response(500), httpx.Response(200, content=b"<html>")],
    ids=["server-error", "not-an-image"],
)
def test_cover_failure_becomes_a_warning(reply: httpx.Response) -> None:
    path = "/download/mbid-00000000-0000-0000-0000-000000000000/front-500.jpg"
    router = _failing("archive.org", path, reply)

    findings = _lookup(router).lookup(METALLICA_QUERY, Stages())

    assert findings.result.warnings == [COVER_WARNING_TEXT]
    assert (findings.result.artwork, findings.artwork) == (None, None)
    assert findings.result.artist is not None


def test_missing_cover_is_no_warning() -> None:
    path = "/release-group/e389b7df-862d-3d91-a612-acca150f6e71/front-500"
    router = _failing("coverartarchive.org", path, httpx.Response(404))

    findings = _lookup(router).lookup(METALLICA_QUERY, Stages())

    assert (findings.result.warnings, findings.artwork) == ([], None)


def test_wikidata_failure_becomes_a_warning_and_skips_wikipedia() -> None:
    router = _failing(
        "www.wikidata.org", "/w/api.php", httpx.Response(500), props="sitelinks|claims"
    )

    result = _lookup(router).lookup(METALLICA_QUERY, Stages()).result

    assert result.warnings == [WIKIDATA_WARNING_TEXT]
    assert result.artist is not None
    assert (result.artist.genres, result.artist.members, result.artist.summary) == ([], [], None)
    assert result.artist.website == "https://www.metallica.com/"


def test_artist_failure_becomes_a_warning_without_artist() -> None:
    router = _failing(
        MB_HOST, "/ws/2/artist/847be904-7fc1-5a75-8c97-1b9818ca88cf", httpx.Response(500)
    )

    result = _lookup(router).lookup(METALLICA_QUERY, Stages()).result

    assert result.warnings == [ARTIST_WARNING_TEXT]
    assert result.artist is None
    assert result.release_group is not None


def test_release_group_failure_keeps_the_search_release_group() -> None:
    router = _failing(
        MB_HOST, "/ws/2/release-group/e389b7df-862d-3d91-a612-acca150f6e71", httpx.Response(500)
    )

    result = _lookup(router).lookup(METALLICA_QUERY, Stages()).result

    assert result.warnings == []
    assert result.release_group is not None
    assert (result.release_group.title, result.release_group.first_release_year) == ("Load", 1996)


def test_artist_without_wikidata_link_has_no_wikidata_or_wikipedia_requests() -> None:
    reference, router = _metallica()
    body = dict(reference["musicbrainz"][0]["response"]["body"], relations=[])
    router.routes[(MB_HOST, "/ws/2/artist/847be904-7fc1-5a75-8c97-1b9818ca88cf", "")] = [
        httpx.Response(200, json=body)
    ]

    result = _lookup(router).lookup(METALLICA_QUERY, Stages()).result

    assert result.artist is not None
    assert (result.artist.wikidata_id, result.artist.website, result.warnings) == (None, None, [])
    assert not {"www.wikidata.org", "en.wikipedia.org"} & {r.url.host for r in router.requests}


def test_recording_without_credited_artist_has_no_artist() -> None:
    router = Router()
    hit = {
        "id": HERO,
        "title": "Hero of the Day",
        "length": 261893,
        "artist-credit": [{"name": "Metallica"}],
    }
    router.add(MB_HOST, "/ws/2/recording", httpx.Response(200, json={"recordings": [hit]}))

    result = _lookup(router).lookup(METALLICA_QUERY, Stages()).result

    assert (result.outcome, result.artist, result.release_group) == ("found", None, None)
    assert len(router.requests) == 1
