"""Consistency checks of the committed feature 005 metadata fixtures (tests/fixtures/metadata).

A small reference matcher (ADR-014 D4, research.md R-04/R-05) proves that every case's expected
outcome follows the documented rules, so the worker matcher (audio-ingest 005:T031) is tested
against a set that is right by construction.
"""

from __future__ import annotations

import json
import re
import string
import unicodedata
from pathlib import Path
from typing import Any

import pytest
from rapidfuzz import fuzz

METADATA = Path(__file__).resolve().parents[1] / "metadata"
CASES: dict[str, Any] = json.loads((METADATA / "matching-cases.json").read_text("utf-8"))
REFERENCE = {
    p.stem: json.loads(p.read_text("utf-8")) for p in (METADATA / "reference").glob("*.json")
}
FAKE: dict[str, Any] = json.loads((METADATA / "fake-provider.json").read_text("utf-8"))
UUID = re.compile(r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")
PUNCTUATION = str.maketrans(dict.fromkeys(string.punctuation))
MAX_RETRIES = 3

REQUIRED_COVERAGE = {
    "reference-song", "live-decoys", "isrc", "cover-version", "same-title-other-artist",
    "official-video-suffix", "feat", "fan-channel", "missing-duration", "busy-then-ok", "non-latin",
}  # fmt: skip


def normalise(text: str) -> str:
    folded = unicodedata.normalize("NFKC", text).casefold().replace("&", " and ")
    return " ".join(folded.translate(PUNCTUATION).split())


def _qualifies(recording: dict[str, Any], query: dict[str, Any]) -> bool:
    credit = " ".join(c["name"] for c in recording["artist-credit"])
    same_title = normalise(recording["title"]) == normalise(query["title"])
    return same_title and fuzz.ratio(normalise(credit), normalise(query["artist"])) >= 90


def _is_plain_album(release: dict[str, Any]) -> bool:
    group = release["release-group"]
    plain = group["primary-type"] == "Album" and not group["secondary-types"]
    return bool(plain and release["status"] == "Official")


def _official_album_date(recording: dict[str, Any]) -> str:
    plain = filter(_is_plain_album, recording["releases"])
    return min((r["release-group"]["first-release-date"] or "9999" for r in plain), default="9999")


def _rank(recording: dict[str, Any], duration: float | None) -> tuple[float, str]:
    distance = 0.0 if duration is None else abs(recording["length"] / 1000 - duration)
    return round(distance, 3), _official_album_date(recording)


def _no_answer_kind(case: dict[str, Any]) -> str | None:
    busy = sum(e["response"]["status"] == 503 for e in case["exchanges"])
    return None if busy > MAX_RETRIES else "empty"


def _final_recordings(case: dict[str, Any]) -> tuple[list[dict[str, Any]], str | None]:
    """Recordings of the first 200 answer and its kind; kind None = retries exhausted."""
    useful = [e for e in case["exchanges"] if e["response"]["status"] == 200]
    if not useful:
        return [], _no_answer_kind(case)
    return useful[0]["response"]["body"]["recordings"], useful[0]["request"]["kind"]


def _matched_by(kind: str) -> str:
    return "isrc" if kind == "isrc-lookup" else "search"


def reference_match(case: dict[str, Any]) -> dict[str, Any]:
    recordings, kind = _final_recordings(case)
    if kind is None:
        return {"outcome": "failed"}
    query = case["input"]
    hits = [r for r in recordings if _qualifies(r, query)]
    if not hits:
        return {"outcome": "not-found", "recordingMbid": None}
    best = min(hits, key=lambda r: _rank(r, query["durationSeconds"]))
    return {"outcome": "found", "recordingMbid": best["id"], "matchedBy": _matched_by(kind)}


@pytest.mark.parametrize("case", CASES["cases"], ids=lambda c: c["id"])
def test_expected_outcome_follows_the_matching_rules(case: dict[str, Any]) -> None:
    actual = reference_match(case)
    expected = {k: case["expected"][k] for k in actual}
    assert actual == expected


FOUND = [case for case in CASES["cases"] if case["expected"]["outcome"] == "found"]


@pytest.mark.parametrize("case", FOUND, ids=lambda c: c["id"])
def test_found_release_group_belongs_to_the_recording(case: dict[str, Any]) -> None:
    expected = case["expected"]
    recordings, _ = _final_recordings(case)
    groups = {
        r["release-group"]["id"]
        for rec in recordings
        if rec["id"] == expected["recordingMbid"]
        for r in rec["releases"]
    }
    assert expected["releaseGroupMbid"] in groups


def test_the_set_is_large_enough_and_covers_research_r05() -> None:
    covered = {tag for case in CASES["cases"] for tag in case["covers"]}
    assert len(CASES["cases"]) >= 20
    assert covered >= REQUIRED_COVERAGE


def test_case_ids_are_unique() -> None:
    ids = [case["id"] for case in CASES["cases"]]
    assert len(ids) == len(set(ids))


def _ids(document: Any) -> list[str]:
    return re.findall(r'"id": "([^"]+)"', json.dumps(document))


def test_musicbrainz_ids_are_lower_case_uuids() -> None:
    ids = _ids([case["exchanges"] for case in CASES["cases"]])
    assert len(ids) > 50
    assert [i for i in ids if not UUID.match(i)] == []


@pytest.mark.parametrize("name", ["metallica", "kevin-macleod"])
def test_wikipedia_extracts_are_placeholders(name: str) -> None:
    for exchange in REFERENCE[name]["wikipedia"]:
        extract = exchange["response"]["body"]["extract"]
        assert extract.startswith("Placeholder summary written for the StemStudio tests.")


@pytest.mark.parametrize("name", ["metallica", "kevin-macleod"])
def test_reference_attribution_is_complete(name: str) -> None:
    summary = REFERENCE[name]["expected"]["summary"]
    title = summary["title"].replace(" ", "_")
    assert summary["pageUrl"] == f"https://en.wikipedia.org/wiki/{title}"
    assert summary["historyUrl"].endswith(f"title={title}&action=history")
    assert summary["licenceUrl"] == "https://creativecommons.org/licenses/by-sa/4.0/"


def test_members_only_for_groups() -> None:
    artists = [r["expected"]["artist"] for r in REFERENCE.values()] + [FAKE["found"]["artist"]]
    for artist in artists:
        assert artist["type"] == "group" or artist["members"] == []
        assert len(artist["genres"]) <= 8 and len(artist["members"]) <= 12


def test_fake_provider_lists_every_mode() -> None:
    statuses = {mode: v["status"] for mode, v in FAKE["modes"].items()}
    assert statuses == {
        "found": "found",
        "not-found": "notFound",
        "failed": "failed",
        "off": "disabled",
    }
    assert "not from Wikipedia" in FAKE["found"]["artist"]["summary"]["extract"]
