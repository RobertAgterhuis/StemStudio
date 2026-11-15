"""005:T010: allow-listed https client with redirects, caps, timeouts and retries (FR-010/011)."""

from collections.abc import Iterator

import httpx
import pytest

from stemstudio_worker.config import OnlineSettings
from stemstudio_worker.metadata.http import (
    ALLOWED_HOSTS,
    MAX_REDIRECTS,
    MAX_RESPONSE_BYTES,
    TIMEOUT_SECONDS,
    HostNotAllowedError,
    OnlineClient,
    OnlineRequestError,
    ResponseTooLargeError,
    ServiceBusyError,
    create_online_client,
    is_allowed_url,
)

from .fakes import USER_AGENT, json_response, online_harness

MB = "https://musicbrainz.org/ws/2/recording"
CAA = "https://coverartarchive.org/release-group/e389b7df-862d-3d91-a612-acca150f6e71/front-500"
ARCHIVE = "https://ia800.us.archive.org/download/mbid-x/cover_thumb500.jpg"


@pytest.mark.parametrize(
    "url",
    [
        "https://musicbrainz.org/ws/2/artist/x",
        "https://coverartarchive.org/release-group/x/front-500",
        "https://archive.org/download/x",
        "https://ia800.us.archive.org/download/x",
        "https://www.wikidata.org/w/api.php",
        "https://en.wikipedia.org/api/rest_v1/page/summary/X",
        "https://i.ytimg.com/vi/x/maxresdefault.jpg",
        "https://i.scdn.co/image/ab67616d0000b273",
        "https://MUSICBRAINZ.ORG/ws/2/",
    ],
)
def test_allow_listed_https_urls(url: str) -> None:
    assert is_allowed_url(url)


@pytest.mark.parametrize(
    "url",
    [
        "http://musicbrainz.org/ws/2/",
        "https://musicbrainz.org.evil.example/",
        "https://evilarchive.org/x",
        "https://example.org/",
        "https://user@musicbrainz.org/",
        "https://musicbrainz.org:8443/",
        "https://musicbrainz.org:99999/",
        "ftp://archive.org/x",
        "https://wikipedia.org/x",
        "not a url",
        "",
    ],
)
def test_other_urls_are_refused(url: str) -> None:
    assert not is_allowed_url(url)


def test_allowed_hosts_cover_the_services() -> None:
    assert {"musicbrainz.org", "coverartarchive.org", "i.ytimg.com", "i.scdn.co"} <= ALLOWED_HOSTS


def test_get_sends_the_user_agent_params_and_a_10_second_timeout() -> None:
    harness = online_harness([json_response({"recordings": []})])

    response = harness.client.get(MB, {"query": 'isrc:"USEVI9692306"', "fmt": "json"})

    assert response is not None
    assert response.json() == {"recordings": []}
    request = harness.recorder.requests[0]
    assert request.headers["User-Agent"] == USER_AGENT
    assert dict(request.url.params) == {"query": 'isrc:"USEVI9692306"', "fmt": "json"}
    assert set(request.extensions["timeout"].values()) == {TIMEOUT_SECONDS}
    assert TIMEOUT_SECONDS == 10.0


def test_response_carries_content_type_and_final_url() -> None:
    harness = online_harness(
        [httpx.Response(200, content=b"jpeg", headers={"content-type": "image/jpeg"})]
    )

    response = harness.client.get(ARCHIVE)

    assert response is not None
    assert response.content == b"jpeg"
    assert response.content_type == "image/jpeg"
    assert response.url == ARCHIVE


def test_not_found_returns_none() -> None:
    harness = online_harness([httpx.Response(404)])

    assert harness.client.get(CAA) is None


@pytest.mark.parametrize("url", ["http://musicbrainz.org/ws/2/", "https://example.org/"])
def test_refused_url_sends_no_request(url: str) -> None:
    harness = online_harness()

    with pytest.raises(HostNotAllowedError):
        harness.client.get(url)

    assert harness.recorder.requests == []


def test_redirects_are_followed_and_re_validated() -> None:
    harness = online_harness(
        [
            httpx.Response(307, headers={"location": "https://archive.org/download/mbid-x/c.jpg"}),
            httpx.Response(302, headers={"location": ARCHIVE}),
            httpx.Response(200, content=b"img", headers={"content-type": "image/jpeg"}),
        ]
    )

    response = harness.client.get(CAA)

    assert response is not None
    assert response.url == ARCHIVE
    hosts = [request.url.host for request in harness.recorder.requests]
    assert hosts == ["coverartarchive.org", "archive.org", "ia800.us.archive.org"]
    assert all(r.headers["User-Agent"] == USER_AGENT for r in harness.recorder.requests)


def test_relative_redirect_is_resolved_against_the_current_url() -> None:
    harness = online_harness(
        [
            httpx.Response(301, headers={"location": "/ws/2/recording/other"}),
            json_response({"id": "x"}),
        ]
    )

    response = harness.client.get(MB)

    assert response is not None
    assert response.url == "https://musicbrainz.org/ws/2/recording/other"


@pytest.mark.parametrize(
    "location", ["https://example.org/x.jpg", "http://archive.org/x.jpg", None]
)
def test_redirect_to_a_refused_location_fails(location: str | None) -> None:
    headers = {} if location is None else {"location": location}
    harness = online_harness([httpx.Response(302, headers=headers)])

    with pytest.raises(OnlineRequestError):
        harness.client.get(CAA)

    assert len(harness.recorder.requests) == 1


def test_more_than_three_redirects_fail() -> None:
    hop = httpx.Response(302, headers={"location": CAA})
    harness = online_harness([hop, hop, hop, hop, json_response({})])

    with pytest.raises(OnlineRequestError, match="redirect"):
        harness.client.get(CAA)

    assert len(harness.recorder.requests) == MAX_REDIRECTS + 1
    assert MAX_REDIRECTS == 3


def test_body_over_10_mb_is_refused() -> None:
    harness = online_harness([httpx.Response(200, content=b"x" * (MAX_RESPONSE_BYTES + 1))])

    with pytest.raises(ResponseTooLargeError):
        harness.client.get(ARCHIVE)

    assert MAX_RESPONSE_BYTES == 10 * 1024 * 1024


def test_body_of_exactly_10_mb_is_accepted() -> None:
    harness = online_harness([httpx.Response(200, content=b"x" * MAX_RESPONSE_BYTES)])

    response = harness.client.get(ARCHIVE)

    assert response is not None
    assert len(response.content) == MAX_RESPONSE_BYTES


def test_declared_content_length_over_the_cap_is_refused_before_reading() -> None:
    def huge(request: httpx.Request) -> httpx.Response:
        return httpx.Response(
            200,
            headers={"content-length": str(MAX_RESPONSE_BYTES + 1)},
            stream=httpx.ByteStream(b"tiny"),
        )

    harness = online_harness([huge])

    with pytest.raises(ResponseTooLargeError):
        harness.client.get(ARCHIVE)


def test_unexpected_status_fails() -> None:
    harness = online_harness([httpx.Response(400)])

    with pytest.raises(OnlineRequestError, match="400"):
        harness.client.get(MB)


def test_network_error_fails_with_a_typed_error() -> None:
    def broken(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectTimeout("timed out", request=request)

    harness = online_harness([broken])

    with pytest.raises(OnlineRequestError) as raised:
        harness.client.get(MB)

    assert not isinstance(raised.value, ServiceBusyError)


@pytest.mark.parametrize("status", [503, 429])
def test_busy_then_ok_is_retried_with_backoff(status: int) -> None:
    harness = online_harness([httpx.Response(status), json_response({"ok": True})])

    response = harness.client.get(MB)

    assert response is not None
    assert response.json() == {"ok": True}
    assert len(harness.recorder.requests) == 2
    assert 2.0 in [pytest.approx(s) for s in harness.clock.sleeps]


def test_retry_after_seconds_is_honoured() -> None:
    harness = online_harness([httpx.Response(503, headers={"retry-after": "5"}), json_response({})])

    harness.client.get(MB)

    assert harness.clock.sleeps == [pytest.approx(5.0)]


def test_retry_after_is_capped_at_30_seconds() -> None:
    harness = online_harness(
        [httpx.Response(429, headers={"retry-after": "3600"}), json_response({})]
    )

    harness.client.get(MB)

    assert harness.clock.sleeps == [pytest.approx(30.0)]


def test_retry_after_http_date_is_honoured() -> None:
    date = "Sun, 15 Nov 2026 12:00:04 GMT"
    harness = online_harness(
        [httpx.Response(503, headers={"retry-after": date}), json_response({})]
    )

    harness.client.get(MB)

    assert harness.clock.sleeps == [pytest.approx(4.0)]


def test_three_retries_then_a_typed_error() -> None:
    harness = online_harness([httpx.Response(503)] * 5, jitter=0.0)

    with pytest.raises(ServiceBusyError):
        harness.client.get(MB)

    assert len(harness.recorder.requests) == 4
    assert harness.clock.sleeps == [pytest.approx(1.6), pytest.approx(3.2), pytest.approx(6.4)]


def test_requests_are_rate_limited_per_family() -> None:
    harness = online_harness([json_response({}), json_response({}), json_response({})])

    harness.client.get(MB)
    harness.client.get("https://en.wikipedia.org/api/rest_v1/page/summary/X")
    harness.client.get(MB)

    assert harness.clock.sleeps == [pytest.approx(1.0)]


def test_service_busy_error_is_an_online_request_error() -> None:
    assert issubclass(ServiceBusyError, OnlineRequestError)
    assert issubclass(HostNotAllowedError, OnlineRequestError)
    assert issubclass(ResponseTooLargeError, OnlineRequestError)


def test_close_closes_the_http_client() -> None:
    client = OnlineClient(USER_AGENT)

    assert not client.closed

    client.close()

    assert client.closed


def test_factory_builds_a_client_with_the_settings_user_agent() -> None:
    seen: list[httpx.Request] = []

    def handler(request: httpx.Request) -> httpx.Response:
        seen.append(request)
        return json_response({})

    client = create_online_client(
        OnlineSettings(contact_url="https://stemstudio.example/"),
        transport=httpx.MockTransport(handler),
    )
    client.get(MB)

    assert seen[0].headers["User-Agent"].endswith("( https://stemstudio.example/ )")


class ChunkStream(httpx.SyncByteStream):
    """A streamed body without Content-Length; optionally fails after the chunks."""

    def __init__(self, chunks: list[bytes], fail: bool = False) -> None:
        self._chunks = chunks
        self._fail = fail

    def __iter__(self) -> Iterator[bytes]:
        yield from self._chunks
        if self._fail:
            raise httpx.ReadError("connection reset")


def test_streamed_body_over_the_cap_is_refused_while_reading() -> None:
    chunk = b"x" * (1024 * 1024)
    stream = ChunkStream([chunk] * 11)
    harness = online_harness([httpx.Response(200, stream=stream)])

    with pytest.raises(ResponseTooLargeError):
        harness.client.get(ARCHIVE)


def test_read_error_while_streaming_fails_with_a_typed_error() -> None:
    harness = online_harness([httpx.Response(200, stream=ChunkStream([b"abc"], fail=True))])

    with pytest.raises(OnlineRequestError, match="ReadError"):
        harness.client.get(ARCHIVE)


def test_success_on_the_last_retry_is_returned() -> None:
    harness = online_harness([httpx.Response(503)] * 3 + [json_response({"late": 1})])

    response = harness.client.get(MB)

    assert response is not None
    assert response.json() == {"late": 1}
    assert len(harness.recorder.requests) == 4
