"""005:T031: recording matcher over the self-written case set (SC-004, ADR-014 D4, FR-004).

Every case runs the real ``MusicBrainz.find`` (query building, response parsing, matcher)
against a ``MockTransport`` that serves the case's responses in order.
"""

import json
from pathlib import Path
from typing import Any

import httpx
import pytest

from stemstudio_worker.metadata.matcher import (
    ARTIST_SIMILARITY,
    DURATION_WINDOW_SECONDS,
    Candidate,
    Credit,
    Release,
    best_release_group,
    qualifies,
    select,
)
from stemstudio_worker.metadata.musicbrainz import MusicBrainz
from stemstudio_worker.ports import MetadataQuery

from .fakes import online_harness

CASES: list[dict[str, Any]] = json.loads(
    (Path(__file__).parent / "matching_cases.json").read_text(encoding="utf-8")
)["cases"]


def _query(raw: dict[str, Any]) -> MetadataQuery:
    return MetadataQuery(
        title=raw["title"],
        artist=raw.get("artist"),
        duration_seconds=raw.get("durationSeconds"),
        isrc=raw.get("isrc"),
    )


def _reply(raw: object) -> httpx.Response:
    return httpx.Response(raw) if isinstance(raw, int) else httpx.Response(200, json=raw)


def test_the_case_set_has_at_least_20_cases_with_unique_names() -> None:
    names = [case["name"] for case in CASES]

    assert len(names) >= 20
    assert len(set(names)) == len(names)
    assert {"found", "not-found"} == {
        "not-found" if case["expected"] == "not-found" else "found" for case in CASES
    }


@pytest.mark.parametrize("case", CASES, ids=[case["name"] for case in CASES])
def test_matching_case(case: dict[str, Any]) -> None:
    harness = online_harness([_reply(raw) for raw in case["responses"]])

    found = MusicBrainz(harness.client).find(_query(case["query"]))

    if case["expected"] == "not-found":
        assert found is None
    else:
        assert found is not None
        assert found.recording.mbid == case["expected"]
        assert found.recording.matched_by == case["matchedBy"]
        if "releaseGroup" in case:
            assert found.release_group is not None
            assert found.release_group.mbid == case["releaseGroup"]
    assert len(harness.recorder.requests) == len(case["responses"])


def _credit(name: str, mbid: str = "2aaaaaaa-aaaa-4aaa-8aaa-000000000009") -> tuple[Credit, ...]:
    return (Credit(name=name, joinphrase="", artist_mbid=mbid),)


def _candidate(**changes: Any) -> Candidate:
    values: dict[str, Any] = {
        "mbid": "1aaaaaaa-aaaa-4aaa-8aaa-000000000009",
        "title": "Song",
        "length_seconds": 200.0,
        "score": 100,
        "credits": _credit("Band"),
        "releases": (),
    }
    values.update(changes)
    return Candidate(**values)


def test_thresholds() -> None:
    assert ARTIST_SIMILARITY == 0.9
    assert DURATION_WINDOW_SECONDS == 5.0


@pytest.mark.parametrize(
    ("length", "expected"), [(195.0, True), (205.0, True), (194.9, False), (205.1, False)]
)
def test_duration_window_is_five_seconds(length: float, expected: bool) -> None:
    assert qualifies(_candidate(length_seconds=length), "Song", "Band", 200.0) is expected


def test_unknown_duration_accepts_any_length() -> None:
    assert qualifies(_candidate(length_seconds=None), "Song", "Band", None)


def test_candidate_without_credits_does_not_qualify() -> None:
    assert not qualifies(_candidate(credits=()), "Song", "Band", 200.0)


def test_select_reports_the_recording_and_the_first_artist() -> None:
    candidate = _candidate(credits=_credit("Band", "2aaaaaaa-aaaa-4aaa-8aaa-000000000001"))

    found = select([candidate], MetadataQuery(title="song", artist="band"), "search")

    assert found is not None
    assert found.artist_mbid == "2aaaaaaa-aaaa-4aaa-8aaa-000000000001"
    assert (found.recording.title, found.recording.length_seconds) == ("Song", 200.0)
    assert (found.recording.score, found.release_group) == (100, None)


def test_select_without_candidates() -> None:
    assert select([], MetadataQuery(title="Song", artist="Band"), "search") is None


def test_isrc_matches_skip_the_title_and_artist_checks() -> None:
    candidate = _candidate(title="Other Title", credits=_credit("Other Artist"))

    found = select([candidate], MetadataQuery(title="Song", artist="Band"), "isrc")

    assert found is not None
    assert found.recording.matched_by == "isrc"


def _release(group: str, **changes: Any) -> Release:
    values: dict[str, Any] = {
        "status": "Official",
        "date": "2000-01-01",
        "group_mbid": group,
        "group_title": "Album",
        "primary_type": "Album",
        "secondary_types": (),
    }
    values.update(changes)
    return Release(**values)


G1 = "3aaaaaaa-aaaa-4aaa-8aaa-000000000001"
G2 = "3aaaaaaa-aaaa-4aaa-8aaa-000000000002"


@pytest.mark.parametrize(
    ("releases", "expected"),
    [
        ((), None),
        ((_release(G1, date="2005"), _release(G2, date="1999-02")), G2),
        ((_release(G1, status="Bootleg", date="1990"), _release(G2)), G2),
        ((_release(G1, primary_type="Single", date="1990"), _release(G2)), G2),
        ((_release(G1, secondary_types=("Live",), date="1990"), _release(G2)), G2),
        ((_release(G1, date=None), _release(G2, date="2010")), G2),
        ((_release(G1, status=None, primary_type=None, date=None),), G1),
    ],
    ids=["none", "earliest", "official", "album", "studio", "dated", "anything"],
)
def test_best_release_group(releases: tuple[Release, ...], expected: str | None) -> None:
    info = best_release_group(_candidate(releases=releases))

    assert (info.mbid if info else None) == expected


def test_release_group_year_and_type() -> None:
    info = best_release_group(_candidate(releases=(_release(G1, date="1996-06-04"),)))

    assert info is not None
    assert (info.title, info.primary_type, info.first_release_year) == ("Album", "Album", 1996)


@pytest.mark.parametrize("date", ["", "199", "x996-01-01", "0999", "3000-01-01"])
def test_unusable_release_dates_give_no_year(date: str) -> None:
    info = best_release_group(_candidate(releases=(_release(G1, date=date),)))

    assert info is not None
    assert info.first_release_year is None
