"""005:T033: Wikidata claims and labels (research F-04; CC0)."""

from typing import Any

import httpx
import pytest

from stemstudio_worker.metadata.http import OnlineRequestError
from stemstudio_worker.metadata.wikidata import (
    MAX_GENRES,
    MAX_LABEL_IDS,
    MAX_MEMBERS,
    Wikidata,
    WikidataFacts,
)
from stemstudio_worker.schemas import ArtistMember

from .fakes import Router, json_response, online_harness, routed_client
from .reference import load, query_fields, router_for

LANGUAGES = ("en", "nl")


def _facts(name: str, qid: str) -> tuple[WikidataFacts, Router]:
    router = router_for(load(name))
    return Wikidata(routed_client(router)).facts(qid, LANGUAGES), router


def test_metallica_claims_and_labels() -> None:
    expected = load("metallica")["expected"]["artist"]

    facts, _ = _facts("metallica", "Q15920")

    assert facts.genres == expected["genres"]
    assert [m.model_dump(by_alias=True) for m in facts.members] == expected["members"]
    assert (facts.begin_year, facts.end_year) == (1981, None)
    assert facts.website == "https://www.metallica.com/"
    assert facts.sitelinks == {"en": "Metallica", "nl": "Metallica"}


def test_requests_match_the_reference_queries() -> None:
    reference = load("metallica")

    _, router = _facts("metallica", "Q15920")

    sent = [dict(request.url.params) for request in router.requests]
    assert sent == [
        query_fields(exchange["request"]["query"]) for exchange in reference["wikidata"]
    ]


def test_person_without_members() -> None:
    facts, router = _facts("kevin-macleod", "Q16731782")

    assert facts.genres == ["film score"]
    assert facts.members == []
    assert len(router.requests) == 2


def _claim(value: object, qualifiers: dict[str, Any] | None = None) -> dict[str, Any]:
    claim: dict[str, Any] = {"mainsnak": {"datavalue": {"value": value}}}
    if qualifiers:
        claim["qualifiers"] = qualifiers
    return claim


def _time(text: str, precision: int = 9) -> dict[str, Any]:
    return {"time": text, "precision": precision}


def _entity(
    qid: str, claims: dict[str, Any], sitelinks: dict[str, Any] | None = None
) -> httpx.Response:
    return json_response(
        {"entities": {qid: {"id": qid, "sitelinks": sitelinks or {}, "claims": claims}}}
    )


def _labels(names: dict[str, str]) -> httpx.Response:
    entities = {qid: {"id": qid, "labels": {"en": {"value": name}}} for qid, name in names.items()}
    return json_response({"entities": entities})


def test_caps_genres_members_and_label_ids() -> None:
    genres = [_claim({"id": f"Q{100 + i}"}) for i in range(30)]
    members = [_claim({"id": f"Q{200 + i}"}) for i in range(30)]
    labels = {f"Q{100 + i}": f"genre {i}" for i in range(30)} | {
        f"Q{200 + i}": f"member {i}" for i in range(30)
    }
    harness = online_harness([_entity("Q1", {"P136": genres, "P527": members}), _labels(labels)])

    facts = Wikidata(harness.client).facts("Q1", LANGUAGES)

    assert (len(facts.genres), len(facts.members)) == (MAX_GENRES, MAX_MEMBERS) == (8, 12)
    ids = harness.recorder.requests[1].url.params["ids"].split("|")
    assert len(ids) == MAX_GENRES + MAX_MEMBERS <= MAX_LABEL_IDS == 50


def test_dissolved_group_end_year_and_member_qualifiers() -> None:
    since_1971 = {"P580": [{"datavalue": {"value": _time("+1971-00-00T00:00:00Z")}}]}
    decade_only = {"P582": [{"datavalue": {"value": _time("+1980-00-00T00:00:00Z", 8)}}]}
    claims = {
        "P571": [_claim(_time("+1970-01-01T00:00:00Z", 11))],
        "P576": [_claim(_time("+1999-00-00T00:00:00Z"))],
        "P527": [_claim({"id": "Q5"}, since_1971), _claim({"id": "Q6"}, decade_only)],
    }
    harness = online_harness([_entity("Q1", claims), _labels({"Q5": "A", "Q6": "B"})])

    facts = Wikidata(harness.client).facts("Q1", LANGUAGES)

    assert (facts.begin_year, facts.end_year) == (1970, 1999)
    assert facts.members == [ArtistMember(name="A", from_year=1971), ArtistMember(name="B")]


@pytest.mark.parametrize(
    "value",
    [
        _time("-0500-00-00T00:00:00Z"),
        _time("+0999-00-00T00:00:00Z"),
        _time("1981"),
        "+1981-00-00T00:00:00Z",
        {"time": 1981, "precision": 9},
    ],
    ids=["bc", "too-early", "no-sign", "not-a-time", "not-a-string"],
)
def test_unusable_times_give_no_year(value: object) -> None:
    harness = online_harness([_entity("Q1", {"P571": [_claim(value)]})])

    assert Wikidata(harness.client).facts("Q1", LANGUAGES).begin_year is None


def test_items_without_a_label_or_an_id_are_skipped() -> None:
    claims = {
        "P136": [
            _claim({"id": "Q7"}),
            _claim({"id": "Q8"}),
            _claim("not an item"),
            {"mainsnak": {}},
        ],
        "P856": [_claim("http://insecure.example/"), _claim(42)],
    }
    claims["P527"] = [_claim({"id": "Q9"})]
    harness = online_harness([_entity("Q1", claims), _labels({"Q7": "rock"})])

    facts = Wikidata(harness.client).facts("Q1", LANGUAGES)

    assert facts.genres == ["rock"]
    assert (facts.members, facts.website) == ([], None)


def test_long_labels_are_cut() -> None:
    claims = {"P136": [_claim({"id": "Q7"})], "P527": [_claim({"id": "Q8"})]}
    harness = online_harness([_entity("Q1", claims), _labels({"Q7": "g" * 300, "Q8": "m" * 900})])

    facts = Wikidata(harness.client).facts("Q1", LANGUAGES)

    assert (len(facts.genres[0]), len(facts.members[0].name)) == (100, 500)


def test_entity_without_items_sends_no_labels_call() -> None:
    sitelinks = {"enwiki": {"title": "X"}, "dewiki": {"title": "Y"}, "nlwiki": {"title": ""}}
    harness = online_harness([_entity("Q1", {}, sitelinks)])

    facts = Wikidata(harness.client).facts("Q1", LANGUAGES)

    assert facts == WikidataFacts(sitelinks={"en": "X"})
    assert len(harness.recorder.requests) == 1


@pytest.mark.parametrize(
    "reply",
    [
        json_response({"entities": {"Q1": {"id": "Q1", "missing": ""}}}),
        json_response({}),
        httpx.Response(404),
    ],
    ids=["missing", "empty", "404"],
)
def test_unknown_entities_give_empty_facts(reply: httpx.Response) -> None:
    harness = online_harness([reply])

    assert Wikidata(harness.client).facts("Q1", LANGUAGES) == WikidataFacts()


def test_malformed_answer_is_a_request_error() -> None:
    harness = online_harness([json_response({"entities": {"Q1": {"claims": "nope"}}})])

    with pytest.raises(OnlineRequestError, match="Wikidata"):
        Wikidata(harness.client).facts("Q1", LANGUAGES)
