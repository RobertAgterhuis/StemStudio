"""LRCLIB client (research F-01/F-02, R-02): parameters, pacing, answers, 005 egress."""

from datetime import UTC, datetime

import httpx
import pytest
from stemstudio_worker.lyrics.lrclib import (
    LRCLIB_GET_URL,
    LRCLIB_HOST,
    LRCLIB_SEARCH_URL,
    MIN_REQUEST_GAP_SECONDS,
    LrclibClient,
)

from stemstudio_worker.metadata import http
from stemstudio_worker.metadata.http import (
    OnlineClient,
    OnlineRequestError,
    RequestPacing,
    ServiceBusyError,
    is_allowed_url,
)
from stemstudio_worker.metadata.ratelimit import RateLimiter
from stemstudio_worker.metadata.user_agent import build_user_agent
from stemstudio_worker.ports import LyricsQuery, LyricsUnavailableError

from .records import FakeClock, FakeGetter, answer, line_record, record

QUERY = LyricsQuery("Fake Song", 262.4, artist="The Fixture Band", album="Synthetic Sessions")


def client(*answers: object, clock: FakeClock | None = None) -> tuple[LrclibClient, FakeGetter]:
    getter = FakeGetter(list(answers))  # type: ignore[arg-type]
    clock = clock or FakeClock()
    return LrclibClient(getter, RateLimiter(clock, clock.sleep, MIN_REQUEST_GAP_SECONDS)), getter


class TestParameters:
    def test_get_with_album_sends_all_names_and_the_rounded_duration(self) -> None:
        lrclib, getter = client()
        lrclib.get(QUERY, with_album=True)
        assert getter.calls == [
            (
                LRCLIB_GET_URL,
                {
                    "track_name": "Fake Song",
                    "artist_name": "The Fixture Band",
                    "album_name": "Synthetic Sessions",
                    "duration": "262",
                },
            )
        ]

    def test_get_without_album_leaves_the_album_out(self) -> None:
        lrclib, getter = client()
        lrclib.get(QUERY, with_album=False)
        assert getter.calls[0][1] == {
            "track_name": "Fake Song",
            "artist_name": "The Fixture Band",
            "duration": "262",
        }

    def test_search_sends_title_and_artist(self) -> None:
        lrclib, getter = client()
        lrclib.search(QUERY)
        assert getter.calls == [
            (LRCLIB_SEARCH_URL, {"track_name": "Fake Song", "artist_name": "The Fixture Band"})
        ]

    def test_search_without_artist_sends_only_the_title(self) -> None:
        lrclib, getter = client()
        lrclib.search(LyricsQuery("Fake Song", 262.0))
        assert getter.calls[0][1] == {"track_name": "Fake Song"}

    def test_urls_are_https_on_lrclib(self) -> None:
        assert (
            f"https://{LRCLIB_HOST}/api/get",
            f"https://{LRCLIB_HOST}/api/search",
        ) == (LRCLIB_GET_URL, LRCLIB_SEARCH_URL)


class TestPacing:
    def test_requests_are_at_least_500_ms_apart(self) -> None:
        clock = FakeClock()
        lrclib, _ = client(clock=clock)
        lrclib.get(QUERY, with_album=True)
        lrclib.get(QUERY, with_album=False)
        lrclib.search(QUERY)
        assert clock.sleeps == [MIN_REQUEST_GAP_SECONDS, MIN_REQUEST_GAP_SECONDS]
        assert MIN_REQUEST_GAP_SECONDS >= 0.5

    def test_no_wait_after_a_pause(self) -> None:
        clock = FakeClock()
        lrclib, _ = client(clock=clock)
        lrclib.search(QUERY)
        clock.now += 10
        lrclib.search(QUERY)
        assert clock.sleeps == []


class TestAnswers:
    def test_get_returns_the_record(self) -> None:
        lrclib, _ = client(answer(record(546388, 262.0)))
        found = lrclib.get(QUERY, with_album=True)
        assert found is not None
        assert (found.id, found.duration, found.has_word_sync) == (546388, 262.0, True)
        assert found.album_name == "Synthetic Sessions"

    def test_get_404_is_none(self) -> None:
        lrclib, _ = client(None)
        assert lrclib.get(QUERY, with_album=True) is None

    @pytest.mark.parametrize(
        "body",
        [b"<html>not json</html>", b"", b"[1, 2]", b'{"id": "x"}', b'{"duration": 1}'],
        ids=["html", "empty", "list", "bad-id", "no-id"],
    )
    def test_an_unreadable_get_answer_is_not_found(self, body: bytes) -> None:
        lrclib, _ = client(http.OnlineResponse(LRCLIB_GET_URL, body))
        assert lrclib.get(QUERY, with_album=True) is None

    def test_search_returns_the_readable_records(self) -> None:
        bodies = [record(1, 262.0), {"id": "broken"}, "text", line_record(2, 285.0)]
        lrclib, _ = client(answer(bodies, LRCLIB_SEARCH_URL))
        assert [found.id for found in lrclib.search(QUERY)] == [1, 2]

    @pytest.mark.parametrize(
        "reply",
        [None, http.OnlineResponse(LRCLIB_SEARCH_URL, b"oops"), answer({"id": 1})],
        ids=["404", "not-json", "not-a-list"],
    )
    def test_an_unreadable_search_answer_has_no_records(self, reply: object) -> None:
        lrclib, _ = client(reply)
        assert lrclib.search(QUERY) == []

    def test_missing_optional_fields_get_defaults(self) -> None:
        lrclib, _ = client(answer({"id": 7, "duration": 100}))
        found = lrclib.get(QUERY, with_album=True)
        assert found is not None
        assert (found.instrumental, found.has_word_sync, found.plain_lyrics) == (False, False, None)

    @pytest.mark.parametrize(
        "error",
        [OnlineRequestError("network"), ServiceBusyError("busy")],
        ids=["network", "busy"],
    )
    def test_request_failures_are_unavailable(self, error: Exception) -> None:
        lrclib, _ = client(error)
        with pytest.raises(LyricsUnavailableError):
            lrclib.get(QUERY, with_album=True)
        lrclib, _ = client(error)
        with pytest.raises(LyricsUnavailableError):
            lrclib.search(QUERY)


ALLOWED = is_allowed_url(LRCLIB_GET_URL)


@pytest.mark.xfail(
    not ALLOWED,
    reason="lrclib.net is not on the 005 ALLOWED_HOSTS yet (requested in the 006 hand-off)",
    strict=True,
)
def test_the_005_client_allows_lrclib() -> None:
    assert is_allowed_url(LRCLIB_GET_URL)
    assert is_allowed_url(LRCLIB_SEARCH_URL)


class TestThrough005Client:
    """The real 005 ``OnlineClient`` over ``httpx.MockTransport`` (lrclib.net allowed)."""

    @pytest.fixture(autouse=True)
    def _allow_lrclib(self, monkeypatch: pytest.MonkeyPatch) -> None:
        monkeypatch.setattr(http, "ALLOWED_HOSTS", http.ALLOWED_HOSTS | {LRCLIB_HOST})

    def wired(
        self, *responses: httpx.Response
    ) -> tuple[LrclibClient, list[httpx.Request], FakeClock]:
        requests: list[httpx.Request] = []
        replies = list(responses)

        def handler(request: httpx.Request) -> httpx.Response:
            requests.append(request)
            return replies.pop(0)

        clock = FakeClock()
        pacing = RequestPacing(
            RateLimiter(clock, clock.sleep),
            sleep=clock.sleep,
            now=lambda: datetime(2026, 11, 15, tzinfo=UTC),
            jitter=lambda: 0.5,
        )
        online = OnlineClient(build_user_agent(None, "0.1.0"), httpx.MockTransport(handler), pacing)
        lrclib = LrclibClient(online, RateLimiter(clock, clock.sleep, MIN_REQUEST_GAP_SECONDS))
        return lrclib, requests, clock

    def test_sends_the_005_user_agent_and_the_query(self) -> None:
        lrclib, requests, _ = self.wired(httpx.Response(200, json=record(546388, 262.0)))
        found = lrclib.get(QUERY, with_album=True)
        assert found is not None
        assert found.id == 546388
        sent = requests[0]
        assert sent.headers["user-agent"] == build_user_agent(None, "0.1.0")
        assert sent.url.host == LRCLIB_HOST
        assert sent.url.params["duration"] == "262"

    def test_honours_retry_after(self) -> None:
        busy = httpx.Response(429, headers={"Retry-After": "3"})
        lrclib, requests, clock = self.wired(busy, httpx.Response(200, json=[]))
        assert lrclib.search(QUERY) == []
        assert len(requests) == 2
        assert 3.0 in clock.sleeps

    def test_still_busy_after_the_retries_is_unavailable(self) -> None:
        busy = [httpx.Response(503, headers={"Retry-After": "1"}) for _ in range(4)]
        lrclib, _, _ = self.wired(*busy)
        with pytest.raises(LyricsUnavailableError):
            lrclib.search(QUERY)

    def test_404_is_not_found(self) -> None:
        lrclib, _, _ = self.wired(httpx.Response(404, json={"message": "Failed"}))
        assert lrclib.get(QUERY, with_album=False) is None
