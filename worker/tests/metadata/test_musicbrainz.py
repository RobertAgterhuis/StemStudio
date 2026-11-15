"""005:T031: MusicBrainz queries, response parsing and artist facts (FR-003, research F-01/F-02)."""

from typing import Any

import httpx
import pytest

from stemstudio_worker.metadata.http import OnlineRequestError, ServiceBusyError
from stemstudio_worker.metadata.musicbrainz import (
    ISRC_INCLUDES,
    ISRC_RECORDING_LIMIT,
    MB_ROOT,
    RECORDING_INCLUDES,
    SEARCH_LIMIT,
    MusicBrainz,
    escape_lucene,
    search_query,
)
from stemstudio_worker.ports import MetadataQuery

from .fakes import json_response, online_harness

METALLICA = "65f4f0c5-ef9e-490c-aee3-909e7ae6b2ab"
RECORDING = "c8361f8a-ece3-4ad0-a5a6-4bb6d06083b9"
LOAD = "e389b7df-862d-3d91-a612-acca150f6e71"


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


def test_isrc_includes_are_valid_for_the_isrc_resource() -> None:
    # MusicBrainz answers 400 to ``release-groups`` on /isrc (live check of 005:T062).
    assert ISRC_INCLUDES == "artist-credits+releases"
    assert RECORDING_INCLUDES == "artist-credits+releases+release-groups"


def _isrc_answer(*recordings: dict[str, Any]) -> httpx.Response:
    """An ``/isrc`` answer as MusicBrainz sends it: recordings without releases."""
    return json_response({"isrc": "USEVI9692306", "recordings": list(recordings)})


def _album_release(group: str = LOAD) -> dict[str, Any]:
    return {
        "id": "8d1b0d8f-1f7e-4d1c-9a4e-2b9e2d0d8a11",
        "title": "Load",
        "status": "Official",
        "date": "1996-06-04",
        "release-group": {
            "id": group,
            "title": "Load",
            "primary-type": "Album",
            "secondary-types": [],
            "first-release-date": "1996-06-04",
        },
    }


def test_isrc_lookup_then_recording_lookup_scores_100() -> None:
    bare = _recording()
    del bare["score"]
    harness = online_harness(
        [_isrc_answer(bare), json_response(_recording(releases=[_album_release()]))]
    )
    query = MetadataQuery(
        title="Hero", artist="Metallica", duration_seconds=262.0, isrc="USEVI9692306"
    )

    found = MusicBrainz(harness.client).find(query)

    assert found is not None
    assert (found.recording.matched_by, found.recording.score) == ("isrc", 100)
    assert found.release_group is not None
    assert (found.release_group.mbid, found.release_group.first_release_year) == (LOAD, 1996)
    isrc, recording = harness.recorder.requests
    assert str(isrc.url).startswith(f"{MB_ROOT}/isrc/USEVI9692306?")
    assert dict(isrc.url.params) == {"inc": ISRC_INCLUDES, "fmt": "json"}
    assert str(recording.url).startswith(f"{MB_ROOT}/recording/{RECORDING}?")
    assert dict(recording.url.params) == {"inc": RECORDING_INCLUDES, "fmt": "json"}


def test_isrc_recording_lookups_skip_unknown_recordings() -> None:
    other = "0b1f3c86-38a5-5f0e-8d0e-6c2e9e4a7b21"
    harness = online_harness(
        [
            _isrc_answer(_recording(id=other), _recording()),
            httpx.Response(404),
            json_response(_recording(releases=[_album_release()])),
        ]
    )

    found = MusicBrainz(harness.client).find(
        MetadataQuery(title="Hero of the Day", artist="Metallica", isrc="USEVI9692306")
    )

    assert found is not None
    assert found.recording.mbid == RECORDING
    assert len(harness.recorder.requests) == 3


def test_isrc_recording_lookups_are_capped() -> None:
    ids = [f"0b1f3c86-38a5-5f0e-8d0e-6c2e9e4a7b{n:02d}" for n in range(ISRC_RECORDING_LIMIT + 2)]
    lookups = [json_response(_recording(id=mbid)) for mbid in ids[:ISRC_RECORDING_LIMIT]]
    harness = online_harness([_isrc_answer(*(_recording(id=mbid) for mbid in ids)), *lookups])

    found = MusicBrainz(harness.client).find(
        MetadataQuery(title="Hero of the Day", isrc="USEVI9692306")
    )

    assert found is not None
    assert len(harness.recorder.requests) == 1 + ISRC_RECORDING_LIMIT


def test_isrc_without_known_recordings_falls_back_to_the_search() -> None:
    harness = online_harness(
        [_isrc_answer(_recording()), httpx.Response(404), json_response({"recordings": []})]
    )

    found = MusicBrainz(harness.client).find(
        MetadataQuery(title="Hero of the Day", artist="Metallica", isrc="USEVI9692306")
    )

    assert found is None
    assert harness.recorder.requests[2].url.path == "/ws/2/recording"


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


def test_isrc_recordings_without_length_score_or_credits_are_parsed() -> None:
    raw = {"id": RECORDING, "title": "Hero of the Day"}
    harness = online_harness([json_response({"recordings": [raw]}), json_response(raw)])

    found = MusicBrainz(harness.client).find(
        MetadataQuery(title="Hero of the Day", isrc="USEVI9692306")
    )

    assert found is not None
    assert (found.recording.score, found.recording.length_seconds, found.artist_mbid) == (
        100,
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
