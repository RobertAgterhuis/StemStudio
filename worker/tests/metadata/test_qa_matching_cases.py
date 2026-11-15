"""005:T031: the qa matching set ``tests/fixtures/metadata/matching-cases.json`` (SC-004).

Each case replays its MusicBrainz exchanges in order through ``httpx.MockTransport``; the
transport checks the request kind and path of every exchange (and the exact Lucene query in the
case ``lucene-special-characters``, which pins the escaping). Cases with a raw YouTube ``source``
also check that the title cleaning (research R-04) yields the lookup input.
"""

import json
from pathlib import Path
from typing import Any

import httpx
import pytest

from stemstudio_worker.metadata.http import ServiceBusyError
from stemstudio_worker.metadata.matcher import RecordingFound
from stemstudio_worker.metadata.musicbrainz import MusicBrainz
from stemstudio_worker.metadata.titles import normalize_for_compare, youtube_lookup_names
from stemstudio_worker.ports import MetadataQuery

from .fakes import online_harness

FIXTURE = Path(__file__).resolve().parents[3] / "tests" / "fixtures" / "metadata"
CASES: list[dict[str, Any]] = json.loads(
    (FIXTURE / "matching-cases.json").read_text(encoding="utf-8")
)["cases"]
PINNED_QUERY = "lucene-special-characters"


def _replay(case: dict[str, Any]) -> list[httpx.Response | Any]:
    """One handler per exchange that checks the request and answers with the recorded reply."""

    def handler_for(exchange: dict[str, Any]) -> Any:
        def handle(request: httpx.Request) -> httpx.Response:
            expected = exchange["request"]
            assert request.url.path == expected["path"]
            if expected["kind"] == "recording-search":
                assert request.url.params["query"].startswith("recording:")
            if case["id"] == PINNED_QUERY:
                assert request.url.params["query"] == expected["query"]
            reply = exchange["response"]
            return httpx.Response(
                reply["status"], headers=reply.get("headers") or {}, json=reply.get("body")
            )

        return handle

    return [handler_for(exchange) for exchange in case["exchanges"]]


def _query(raw: dict[str, Any]) -> MetadataQuery:
    return MetadataQuery(
        title=raw["title"],
        artist=raw["artist"],
        album=raw["album"],
        duration_seconds=raw["durationSeconds"],
        isrc=raw["isrc"],
    )


def test_the_qa_set_has_at_least_20_cases() -> None:
    assert len(CASES) >= 20
    assert {case["expected"]["outcome"] for case in CASES} == {"found", "not-found", "failed"}


@pytest.mark.parametrize("case", CASES, ids=[case["id"] for case in CASES])
def test_qa_matching_case(case: dict[str, Any]) -> None:
    harness = online_harness(_replay(case))
    musicbrainz = MusicBrainz(harness.client)

    if case["expected"]["outcome"] == "failed":
        with pytest.raises(ServiceBusyError):
            musicbrainz.find(_query(case["input"]))
    else:
        _check(musicbrainz.find(_query(case["input"])), case["expected"])
    assert len(harness.recorder.requests) == len(case["exchanges"])


def _check(found: RecordingFound | None, expected: dict[str, Any]) -> None:
    """Compare a lookup outcome with the case's ``expected`` block."""
    if expected["outcome"] == "not-found":
        assert found is None
        return
    assert found is not None
    assert found.recording.mbid == expected["recordingMbid"]
    assert found.recording.matched_by == expected["matchedBy"]
    assert found.release_group is not None
    assert found.release_group.mbid == expected["releaseGroupMbid"]


SOURCED = [case for case in CASES if "source" in case]


@pytest.mark.parametrize("case", SOURCED, ids=[case["id"] for case in SOURCED])
def test_youtube_source_cleans_to_the_lookup_input(case: dict[str, Any]) -> None:
    source = case["source"]
    info = {
        "title": source["rawTitle"],
        "uploader": source["uploader"],
        "track": source["track"],
        "artist": source["artist"],
    }

    title, artist = youtube_lookup_names(info)

    assert title is not None
    assert normalize_for_compare(title) == normalize_for_compare(case["input"]["title"])
    assert artist == case["input"]["artist"]
