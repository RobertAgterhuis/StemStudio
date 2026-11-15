"""005:T031: MusicBrainz queries, response parsing and artist facts (FR-003, research F-01/F-02)."""

from typing import Any

import httpx
import pytest

from stemstudio_worker.metadata.http import OnlineRequestError, ServiceBusyError
from stemstudio_worker.metadata.musicbrainz import (
    MB_ROOT,
    SEARCH_LIMIT,
    MusicBrainz,
    escape_lucene,
    ISRC_INCLUDES,
    search_query,
)
from stemstudio_worker.ports import MetadataQuery

from .fakes import json_response, online_harness

METALLICA = "65f4f0c5-ef9e-490c-aee3-909e7ae6b2ab"
RECORDING = "c8361f8a-ece3-4ad0-a5a6-4bb6d06083b9"


def _recording(**changes: Any) -> dict[str, Any]:
    value: dict[str, Any] = {
        "id": RECORDING,
        "score": 99,
        "title": "Hero of the Day",
        "length": 261893,
        "artist-credit": [{"name": "Metallica", "artist": {"id": METALLICA, "name": "Metallica"}}],
    }
    value.update(changes)
    return value


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("Hero Of The Day", "Hero Of The Day"),
        ('Say "Hi"', 'Say \\"Hi\\"'),
        ("AC/DC", "AC\\/DC"),
        ("What? (Remix) [x] {y}", "What\\? \\(Remix\\) \\[x\\] \\{y\\}"),
        ("a+b-c&&d||e!f^g~h*i:j\\k", "a\\+b\\-c\\&\\&d\\|\\|e\\!f\\^g\\~h\\*i\\:j\\\\k"),
    ],
)
def test_escape_lucene(text: str, expected: str) -> None:
    assert escape_lucene(text) == expected


def test_search_query_with_artist_and_duration_window() -> None:
    query = search_query("Hero Of The Day", "Metallica", 261.9)

    assert query == (
        'recording:"Hero Of The Day" AND artist:"Metallica" AND dur:[256900 TO 266900]'
    )


def test_search_query_without_duration() -> None:
    assert search_query('Say "Hi"', "Band", None) == 'recording:"Say \\"Hi\\"" AND artist:"Band"'


def test_search_query_window_never_goes_below_zero() -> None:
    assert search_query("Jingle", "Band", 2.0).endswith("dur:[0 TO 7000]")


def test_isrc_includes() -> None:
    assert ISRC_INCLUDES == "artist-credits+releases+release-groups"


def test_isrc_lookup_comes_first_and_scores_100() -> None:
    raw = _recording()
    del raw["score"]
    harness = online_harness([json_response({"isrc": "USEVI9692306", "recordings": [raw]})])
    query = MetadataQuery(
        title="Hero", artist="Metallica", duration_seconds=262.0, isrc="USEVI9692306"
    )

    found = MusicBrainz(harness.client).find(query)

    assert found is not None
    assert (found.recording.matched_by, found.recording.score) == ("isrc", 100)
    request = harness.recorder.requests[0]
    assert str(request.url).startswith(f"{MB_ROOT}/isrc/USEVI9692306?")
    assert dict(request.url.params) == {"inc": ISRC_INCLUDES, "fmt": "json"}


def test_unknown_isrc_404_falls_back_to_the_search() -> None:
    harness = online_harness([httpx.Response(404), json_response({"recordings": [_recording()]})])
    query = MetadataQuery(
        title="Hero of the Day", artist="Metallica", duration_seconds=262.0, isrc="USEVI9692306"
    )

    found = MusicBrainz(harness.client).find(query)

    assert found is not None
    assert found.recording.matched_by == "search"
    assert dict(harness.recorder.requests[1].url.params)["limit"] == str(SEARCH_LIMIT)


def test_search_sends_only_title_artist_and_duration() -> None:
    harness = online_harness([json_response({"recordings": []})])
    query = MetadataQuery(
        title="Hero (Official Video)", artist="Metallica", album="Load", duration_seconds=262.0
    )

    assert MusicBrainz(harness.client).find(query) is None

    params = dict(harness.recorder.requests[0].url.params)
    assert params["query"] == 'recording:"Hero" AND artist:"Metallica" AND dur:[257000 TO 267000]'
    assert "Load" not in str(harness.recorder.requests[0].url)


def test_found_recording_carries_score_length_and_title() -> None:
    harness = online_harness([json_response({"recordings": [_recording(score="97")]})])

    found = MusicBrainz(harness.client).find(
        MetadataQuery(title="Hero of the Day", artist="Metallica", duration_seconds=262.0)
    )

    assert found is not None
    assert found.recording.model_dump() == {
        "mbid": RECORDING,
        "title": "Hero of the Day",
        "length_seconds": 261.893,
        "score": 97,
        "matched_by": "search",
    }
    assert found.artist_mbid == METALLICA


def test_not_found_search_answer_is_no_match() -> None:
    harness = online_harness([httpx.Response(404)])

    assert MusicBrainz(harness.client).find(MetadataQuery(title="x", artist="y")) is None


@pytest.mark.parametrize(
    "body",
    [
        {"recordings": [{"id": "not-a-uuid", "title": "x"}]},
        {"recordings": "nope"},
        [1, 2],
    ],
    ids=["bad-id", "bad-list", "not-an-object"],
)
def test_malformed_answers_are_request_errors(body: object) -> None:
    harness = online_harness([json_response(body)])

    with pytest.raises(OnlineRequestError, match="MusicBrainz"):
        MusicBrainz(harness.client).find(MetadataQuery(title="x", artist="y"))


def test_invalid_json_is_a_request_error() -> None:
    harness = online_harness([httpx.Response(200, content=b"<html>busy</html>")])

    with pytest.raises(OnlineRequestError):
        MusicBrainz(harness.client).find(MetadataQuery(title="x", artist="y"))


def test_busy_service_propagates_after_retries() -> None:
    harness = online_harness([httpx.Response(503)] * 4)

    with pytest.raises(ServiceBusyError):
        MusicBrainz(harness.client).find(MetadataQuery(title="x", artist="y"))


def test_recordings_without_length_score_or_credits_are_parsed() -> None:
    raw = {"id": RECORDING, "title": "Hero of the Day"}
    harness = online_harness([json_response({"recordings": [raw]})])

    found = MusicBrainz(harness.client).find(
        MetadataQuery(title="Hero of the Day", isrc="USEVI9692306")
    )

    assert found is not None
    assert (found.recording.score, found.recording.length_seconds, found.artist_mbid) == (
        0,
        None,
        None,
    )


# ------------------------------------------------------------------ artist


def _artist(**changes: Any) -> dict[str, Any]:
    value: dict[str, Any] = {
        "id": METALLICA,
        "name": "Metallica",
        "type": "Group",
        "country": "US",
        "life-span": {"begin": "1981-10-28", "end": None, "ended": False},
        "relations": [
            {"type": "official homepage", "url": {"resource": "https://www.metallica.com/"}},
            {"type": "wikidata", "url": {"resource": "https://www.wikidata.org/wiki/Q15920"}},
            {"type": "discogs", "url": {"resource": "https://www.discogs.com/artist/18839"}},
        ],
    }
    value.update(changes)
    return value


def test_artist_facts() -> None:
    harness = online_harness([json_response(_artist())])

    artist = MusicBrainz(harness.client).artist(METALLICA)

    assert artist is not None
    assert (artist.mbid, artist.name, artist.type, artist.country) == (
        METALLICA,
        "Metallica",
        "group",
        "US",
    )
    assert (artist.begin_year, artist.end_year) == (1981, None)
    assert (artist.website, artist.wikidata_id) == ("https://www.metallica.com/", "Q15920")
    request = harness.recorder.requests[0]
    assert str(request.url).startswith(f"{MB_ROOT}/artist/{METALLICA}?")
    assert dict(request.url.params) == {"inc": "url-rels", "fmt": "json"}


def test_ended_person_with_unknown_type_values() -> None:
    raw = _artist(
        type="Person",
        country=None,
        relations=[],
        **{"life-span": {"begin": "1950", "end": "2016-01-10", "ended": True}},
    )
    artist = MusicBrainz(online_harness([json_response(raw)]).client).artist(METALLICA)

    assert artist is not None
    facts = (artist.type, artist.country, artist.begin_year, artist.end_year, artist.website)
    assert facts == ("person", None, 1950, 2016, None)
    assert artist.wikidata_id is None


@pytest.mark.parametrize(
    ("changes", "field", "expected"),
    [
        ({"type": "Band of brothers"}, "type", None),
        ({"type": None}, "type", None),
        ({"country": "usa"}, "country", None),
        ({"life-span": {}}, "begin_year", None),
        (
            {
                "relations": [
                    {"type": "official homepage", "url": {"resource": "http://x.example/"}}
                ]
            },
            "website",
            None,
        ),
        (
            {"relations": [{"type": "wikidata", "url": {"resource": "https://example.org/Q1"}}]},
            "wikidata_id",
            None,
        ),
        ({"relations": [{"type": "wikidata"}]}, "wikidata_id", None),
    ],
)
def test_unusable_artist_values_are_dropped(
    changes: dict[str, Any], field: str, expected: object
) -> None:
    harness = online_harness([json_response(_artist(**changes))])

    artist = MusicBrainz(harness.client).artist(METALLICA)

    assert artist is not None
    assert getattr(artist, field) == expected


def test_unknown_artist() -> None:
    harness = online_harness([httpx.Response(404)])

    assert MusicBrainz(harness.client).artist(METALLICA) is None


def test_malformed_artist_is_a_request_error() -> None:
    harness = online_harness([json_response({"id": METALLICA})])

    with pytest.raises(OnlineRequestError, match="MusicBrainz"):
        MusicBrainz(harness.client).artist(METALLICA)
