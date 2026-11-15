"""005:T033: Wikipedia REST page summary with CC BY-SA attribution data (research F-05, FR-008)."""

from typing import Any

import httpx
import pytest

from stemstudio_worker.metadata.http import OnlineRequestError
from stemstudio_worker.metadata.wikipedia import MAX_EXTRACT_LENGTH, Wikipedia
from stemstudio_worker.schemas import WIKIPEDIA_LICENCE, WIKIPEDIA_LICENCE_URL

from .fakes import json_response, online_harness, routed_client
from .reference import load, router_for


def _summary(title: str, lang: str = "en", **changes: Any) -> dict[str, Any]:
    body: dict[str, Any] = {
        "type": "standard",
        "title": title,
        "lang": lang,
        "extract": f"Placeholder summary written for the StemStudio tests about {title}.",
        "revision": 1250000009,
        "content_urls": {"desktop": {"page": f"https://{lang}.wikipedia.org/wiki/{title}"}},
    }
    body.update(changes)
    return body


@pytest.mark.parametrize("name", ["metallica", "kevin-macleod"])
def test_reference_summaries(name: str) -> None:
    reference = load(name)
    title = reference["expected"]["summary"]["title"]

    summary = Wikipedia(routed_client(router_for(reference))).summary({"en": title}, ("en", "nl"))

    assert summary is not None
    dumped = summary.model_dump(by_alias=True)
    assert {key: dumped[key] for key in reference["expected"]["summary"]} == reference["expected"][
        "summary"
    ]
    assert summary.extract == reference["wikipedia"][0]["response"]["body"]["extract"]
    assert (summary.licence, summary.licence_url) == (WIKIPEDIA_LICENCE, WIKIPEDIA_LICENCE_URL)


def test_languages_are_tried_in_the_configured_order() -> None:
    harness = online_harness([json_response(_summary("Metallica", "nl"))])

    summary = Wikipedia(harness.client).summary(
        {"en": "Metallica", "nl": "Metallica"}, ("nl", "en")
    )

    assert summary is not None
    assert summary.language == "nl"
    assert (
        str(harness.recorder.requests[0].url)
        == "https://nl.wikipedia.org/api/rest_v1/page/summary/Metallica"
    )


def test_languages_without_a_sitelink_are_skipped() -> None:
    harness = online_harness([json_response(_summary("Metallica"))])

    summary = Wikipedia(harness.client).summary({"en": "Metallica"}, ("nl", "en"))

    assert summary is not None
    assert [r.url.host for r in harness.recorder.requests] == ["en.wikipedia.org"]


def test_no_sitelink_sends_no_request() -> None:
    harness = online_harness()

    assert Wikipedia(harness.client).summary({"de": "Metallica"}, ("en",)) is None
    assert harness.recorder.requests == []


def test_titles_are_quoted_in_the_path_and_the_history_link() -> None:
    harness = online_harness([json_response(_summary("AC/DC Band"))])

    summary = Wikipedia(harness.client).summary({"en": "AC/DC Band"}, ("en",))

    assert summary is not None
    assert harness.recorder.requests[0].url.raw_path == b"/api/rest_v1/page/summary/AC%2FDC_Band"
    assert (
        summary.history_url
        == "https://en.wikipedia.org/w/index.php?title=AC%2FDC_Band&action=history"
    )


@pytest.mark.parametrize(
    "changes",
    [
        {"type": "disambiguation"},
        {"extract": ""},
        {"extract": "x" * (MAX_EXTRACT_LENGTH + 1)},
        {"content_urls": {"desktop": {"page": "http://en.wikipedia.org/wiki/X"}}},
        {"content_urls": {}},
        {"revision": None},
        {"title": ""},
    ],
    ids=["not-standard", "empty", "too-long", "http-page", "no-page", "no-revision", "no-title"],
)
def test_unusable_summaries_are_dropped_and_the_next_language_is_tried(
    changes: dict[str, Any],
) -> None:
    harness = online_harness(
        [json_response(_summary("X", **changes)), json_response(_summary("X", "nl"))]
    )

    summary = Wikipedia(harness.client).summary({"en": "X", "nl": "X"}, ("en", "nl"))

    assert summary is not None
    assert summary.language == "nl"


def test_extract_of_exactly_2000_characters_is_kept_unmodified() -> None:
    extract = "y" * MAX_EXTRACT_LENGTH
    harness = online_harness([json_response(_summary("X", extract=extract))])

    summary = Wikipedia(harness.client).summary({"en": "X"}, ("en",))

    assert summary is not None
    assert summary.extract == extract
    assert MAX_EXTRACT_LENGTH == 2000


def test_missing_page_tries_the_next_language_then_gives_none() -> None:
    harness = online_harness([httpx.Response(404), httpx.Response(404)])

    assert Wikipedia(harness.client).summary({"en": "X", "nl": "X"}, ("en", "nl")) is None
    assert len(harness.recorder.requests) == 2


def test_failures_raise_request_errors() -> None:
    harness = online_harness([httpx.Response(500)])

    with pytest.raises(OnlineRequestError):
        Wikipedia(harness.client).summary({"en": "X"}, ("en",))


def test_malformed_summary_is_a_request_error() -> None:
    harness = online_harness([json_response(["not", "an", "object"])])

    with pytest.raises(OnlineRequestError, match="Wikipedia"):
        Wikipedia(harness.client).summary({"en": "X"}, ("en",))
